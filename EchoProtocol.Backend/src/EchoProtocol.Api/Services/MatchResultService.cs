using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.MatchResults;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EchoProtocol.Api.Services;

public sealed class MatchResultService : IMatchResultService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public MatchResultService(AppDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<ServiceResult<MatchResultResponse>> SubmitAsync(
        Guid hostUserId,
        Guid matchId,
        SubmitMatchResultRequest request,
        CancellationToken cancellationToken = default)
        => await SubmitCoreAsync(hostUserId, matchId, request, cancellationToken);

    public async Task<ServiceResult<MatchResultResponse>> RecoverAsync(Guid adminId, Guid matchId,
        SubmitMatchResultRequest request, DateTime endedAtUtc, string reason, CancellationToken ct)
    {
        if (endedAtUtc.Kind != DateTimeKind.Utc || endedAtUtc > UtcNow()
            || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10 || reason.Length > 1000
            || !await _db.Users.AnyAsync(user => user.Id == adminId && user.Role == UserRole.ADMIN, ct))
            return Failure("Admin approval, UTC end time and a review reason (10-1000 characters) are required", ErrorCodes.ValidationError);
        var host = await _db.MatchAuthorityBindings.AsNoTracking().Where(match => match.MatchId == matchId)
            .Select(match => (Guid?)match.HostUserId).SingleOrDefaultAsync(ct);
        if (host == null) return Failure("Match not found", ErrorCodes.MatchNotFound);
        return await SubmitCoreAsync(host.Value, matchId, request, ct, endedAtUtc,
            $"Admin={adminId:D}; ReviewedAt={UtcNow():O}; Reason={reason.Trim()}");
    }

    // Recovery trusts only a snapshot already received under a live host lease, never a client replay timestamp.
    public async Task<ServiceResult<MatchResultResponse>> RecoverSalvageCheckpointAsync(Guid owner,Guid matchId,CancellationToken ct)
    {
        var snapshot=await _db.SalvageCheckpoints.AsNoTracking().SingleOrDefaultAsync(x=>x.MatchId==matchId&&x.OwnerId==owner,ct);
        if(snapshot==null)return Failure("Checkpoint not found",ErrorCodes.MatchNotFound);
        var match=await _db.MatchAuthorityBindings.AsNoTracking().SingleAsync(x=>x.MatchId==matchId,ct);
        if(match.LeaseExpiresAtUtc>UtcNow())return Failure("Session is still active",ErrorCodes.MatchResultInvalidState);
        var request=JsonSerializer.Deserialize<SubmitMatchResultRequest>(snapshot.ResultJson,new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if(request==null)return Failure("Checkpoint has no result",ErrorCodes.MatchResultInvalidPayload);
        return await SubmitCoreAsync(match.HostUserId,matchId,request,ct,snapshot.SavedAtUtc,"Salvage server checkpoint recovery");
    }
    private async Task<ServiceResult<MatchResultResponse>> SubmitCoreAsync(Guid hostUserId, Guid matchId,
        SubmitMatchResultRequest request, CancellationToken cancellationToken,
        DateTime? reviewedEndUtc = null, string? recoveryAudit = null)
    {
        var requestFailure = ValidateRequest(request);
        if (requestFailure is not null)
        {
            return Failure(requestFailure.Value.Message, requestFailure.Value.Code);
        }

        var payloadHash = ComputePayloadHash(request);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var match = await LoadMatchForUpdateAsync(matchId, cancellationToken);
            if (match is null)
            {
                return Failure("Match not found", ErrorCodes.MatchNotFound);
            }

            if (match.HostUserId != hostUserId)
            {
                return Failure(
                    "Only the bound Host can submit this match result",
                    ErrorCodes.MatchAuthorityForbidden);
            }

            await _db.Entry(match).Collection(item => item.Players).LoadAsync(cancellationToken);
            await _db.Entry(match).Reference(item => item.Result).LoadAsync(cancellationToken);

            if (match.Result is not null)
            {
                await _db.Entry(match.Result).Collection(item => item.Players).LoadAsync(cancellationToken);
                await transaction.RollbackAsync(cancellationToken);
                return ResolveReplay(match.Result, payloadHash);
            }

            if (match.MatchDataPolicy != RemoteFixedBalanceContract.TestOnly &&
                match.RewardPolicyVersion is "REWARD_V2" or "REWARD_V3" &&
                (request.EvidenceVersion != 2 || request.Players.Any(player => player.EvidenceComplete != true)))
                return Failure("Complete V2 reward evidence is required", ErrorCodes.MatchResultInvalidPayload);

            bool salvage=MatchContentCatalog.IsSalvage(match.MapId);
            if(salvage ? request.SalvageCompletedLevels is null || request.SalvageCompletedDays is null
                || !SalvageRewardPolicy.ValidEvidence(request.SalvageCompletedLevels.Value,request.SalvageCompletedDays.Value)
                || request.EvidenceVersion!=2 || request.Players.Any(p=>p.EvidenceComplete!=true)
                : request.SalvageCompletedLevels.HasValue || request.SalvageCompletedDays.HasValue)
                return Failure("Invalid Salvage result evidence",ErrorCodes.MatchResultInvalidPayload);
            var wallNow = UtcNow();
            var now = reviewedEndUtc ?? wallNow;
            if (match.Status != MatchAuthorityStatus.InMatch && !(salvage && match.Status==MatchAuthorityStatus.Ended && recoveryAudit=="Salvage server checkpoint recovery"))
            {
                return Failure(
                    "Only an active InMatch match can accept a result",
                    ErrorCodes.MatchResultInvalidState);
            }

            if (reviewedEndUtc.HasValue && match.LeaseExpiresAtUtc > wallNow)
                return Failure("Active authority cannot be overridden by recovery", ErrorCodes.MatchAuthorityForbidden);
            if (!reviewedEndUtc.HasValue && match.LeaseExpiresAtUtc <= now)
            {
                return Failure("Match authority lease expired", ErrorCodes.MatchLeaseExpired);
            }

            if (!match.StartedAtUtc.HasValue || match.StartedAtUtc.Value > now)
            {
                return Failure(
                    "Match start timestamp is missing or invalid",
                    ErrorCodes.MatchResultInvalidState);
            }

            var durationSeconds = checked((int)Math.Floor((now - match.StartedAtUtc.Value).TotalSeconds));
            if (durationSeconds < GameConstants.MinimumMatchDurationSeconds || (!salvage && durationSeconds > GameConstants.MaximumAcceptedMatchDurationSeconds))
            {
                return Failure(
                    $"Match duration must be between {GameConstants.MinimumMatchDurationSeconds} and {GameConstants.MaximumAcceptedMatchDurationSeconds} seconds",
                    ErrorCodes.MatchResultInvalidDuration);
            }

            var rosterFailure = ValidateRoster(request.Players, match.Players);
            if (rosterFailure is not null)
            {
                return Failure(rosterFailure.Value.Message, rosterFailure.Value.Code);
            }

            var result = new MatchResult
            {
                MatchId = match.MatchId,
                SubmittedByUserId = hostUserId,
                Outcome = request.Outcome!.Value,
                StartedAtUtc = match.StartedAtUtc.Value,
                EndedAtUtc = now,
                DurationSeconds = durationSeconds,
                ObjectiveCompletion = request.ObjectiveCompletion,
                EvidenceVersion = request.EvidenceVersion,
                SalvageCompletedLevels = request.SalvageCompletedLevels,
                SalvageUsedDebugCheats = salvage ? request.UsedDebugCheats : null,
                SalvageCompletedDays = request.SalvageCompletedDays,
                PlayerCount = request.Players.Count,
                RosterKey = string.Join(",", request.Players
                    .Select(player => player.UserId.ToString("N"))
                    .OrderBy(userId => userId, StringComparer.Ordinal)),
                PayloadHash = payloadHash,
                RewardStatus = MatchRewardStatus.Pending,
                SubmittedAtUtc = wallNow,
                RecoveryAudit = recoveryAudit,
                Players = request.Players.Select(player => new MatchResultPlayer
                {
                    MatchId = match.MatchId,
                    UserId = player.UserId,
                    Survived = player.Survived,
                    Disconnected = player.Disconnected,
                    DetectionCount = player.DetectionCount,
                    DownedCount = player.DownedCount,
                    ReviveCount = player.ReviveCount,
                    ObjectiveContribution = player.ObjectiveContribution,
                    ObjectiveActions = player.ObjectiveActions,
                    RescueActions = player.RescueActions,
                    EvidenceComplete = player.EvidenceComplete
                }).ToList()
            };

            match.Status = MatchAuthorityStatus.Ended;
            match.EndedAtUtc = now;
            match.UpdatedAtUtc = now;
            _db.MatchResults.Add(result);

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ServiceResult<MatchResultResponse>.Success(
                Map(result, isReplay: false),
                "Match result submitted");
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            await transaction.RollbackAsync(cancellationToken);
            _db.ChangeTracker.Clear();
            return await ResolveConcurrentSubmissionAsync(
                hostUserId,
                matchId,
                payloadHash,
                cancellationToken);
        }
    }

    private async Task<MatchAuthorityBinding?> LoadMatchForUpdateAsync(
        Guid matchId,
        CancellationToken cancellationToken)
    {
        if (_db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            return await _db.MatchAuthorityBindings
                .FromSqlInterpolated(
                    $"SELECT * FROM \"MatchAuthorityBindings\" WHERE \"MatchId\" = {matchId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
        }

        return await _db.MatchAuthorityBindings
            .SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);
    }

    private async Task<ServiceResult<MatchResultResponse>> ResolveConcurrentSubmissionAsync(
        Guid hostUserId,
        Guid matchId,
        string payloadHash,
        CancellationToken cancellationToken)
    {
        var match = await _db.MatchAuthorityBindings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);
        if (match is null)
        {
            return Failure("Match not found", ErrorCodes.MatchNotFound);
        }

        if (match.HostUserId != hostUserId)
        {
            return Failure(
                "Only the bound Host can submit this match result",
                ErrorCodes.MatchAuthorityForbidden);
        }

        var stored = await _db.MatchResults.AsNoTracking()
            .Include(item => item.Players)
            .SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);
        if (stored is null)
        {
            throw new InvalidOperationException(
                "A unique constraint was violated but no persisted match result could be loaded.");
        }

        return ResolveReplay(stored, payloadHash);
    }

    private static ServiceResult<MatchResultResponse> ResolveReplay(
        MatchResult stored,
        string payloadHash)
    {
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(stored.PayloadHash),
                Encoding.ASCII.GetBytes(payloadHash)))
        {
            return Failure(
                "A different result has already been submitted for this match",
                ErrorCodes.MatchResultConflict);
        }

        return ServiceResult<MatchResultResponse>.Success(
            Map(stored, isReplay: true),
            "Match result already accepted");
    }

    private static (string Message, string Code)? ValidateRequest(SubmitMatchResultRequest request)
    {
        if (request.ExtensionData is { Count: > 0 })
        {
            return ("Match result contains unsupported fields", ErrorCodes.MatchResultInvalidPayload);
        }

        if (!request.Outcome.HasValue || !Enum.IsDefined(request.Outcome.Value))
        {
            return ("Match outcome is invalid", ErrorCodes.MatchResultInvalidPayload);
        }

        if (request.ObjectiveCompletion is < 0 or > 1)
        {
            return ("Objective completion must be between 0 and 1", ErrorCodes.MatchResultInvalidPayload);
        }

        if (request.Players is null || request.Players.Count is < 1 or > 4)
        {
            return ("Match result must contain between 1 and 4 players", ErrorCodes.MatchResultInvalidRoster);
        }

        if (request.Players.Any(item =>
                item.UserId == Guid.Empty ||
                item.ExtensionData is { Count: > 0 } ||
                item.DetectionCount < 0 ||
                item.DownedCount < 0 ||
                item.ReviveCount < 0 ||
                item.ObjectiveContribution < 0))
        {
            return ("One or more player result fields are invalid", ErrorCodes.MatchResultInvalidPayload);
        }

        if (request.Players.Select(item => item.UserId).Distinct().Count() != request.Players.Count)
        {
            return ("A player can only appear once in a match result", ErrorCodes.MatchResultDuplicatePlayer);
        }

        if (request.EvidenceVersion is not null and not 2)
            return ("Unsupported evidence version", ErrorCodes.MatchResultInvalidPayload);
        if (request.Players.Any(player => player.ObjectiveActions is < 0 or > 10000 ||
            player.RescueActions is < 0 or > 10000))
            return ("Invalid reward evidence", ErrorCodes.MatchResultInvalidPayload);
        if (request.EvidenceVersion == 2 && request.Players.Any(player =>
            player.ObjectiveActions is null || player.RescueActions is null || player.EvidenceComplete is null))
            return ("Missing V2 reward evidence fields", ErrorCodes.MatchResultInvalidPayload);

        return null;
    }

    private static (string Message, string Code)? ValidateRoster(
        IReadOnlyCollection<SubmitMatchResultPlayerRequest> requestedPlayers,
        ICollection<MatchPlayerBinding> boundPlayers)
    {
        var bindingsByUser = boundPlayers.Where(item => item.Participated).ToDictionary(item => item.UserId);
        if (bindingsByUser.Count != requestedPlayers.Count ||
            requestedPlayers.Any(item => !bindingsByUser.ContainsKey(item.UserId)))
        {
            return (
                "Result players must exactly match the backend-bound roster",
                ErrorCodes.MatchResultInvalidRoster);
        }

        foreach (var player in requestedPlayers)
        {
            var binding = bindingsByUser[player.UserId];
            if (player.Disconnected != binding.DisconnectedAtUtc.HasValue)
            {
                return (
                    "Player disconnected state does not match the backend binding",
                    ErrorCodes.MatchResultInvalidRoster);
            }
        }

        return null;
    }

    private static string ComputePayloadHash(SubmitMatchResultRequest request)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("outcome", request.Outcome!.Value.ToString());
            if (request.UsedDebugCheats) writer.WriteBoolean("usedDebugCheats", true);
            writer.WriteNumber("objectiveCompletion", request.ObjectiveCompletion);
            if(request.SalvageCompletedLevels.HasValue)writer.WriteNumber("salvageCompletedLevels",request.SalvageCompletedLevels.Value);
            if(request.SalvageCompletedDays.HasValue)writer.WriteNumber("salvageCompletedDays",request.SalvageCompletedDays.Value);
            if (request.EvidenceVersion.HasValue)
                writer.WriteNumber("evidenceVersion", request.EvidenceVersion.Value);
            writer.WriteStartArray("players");
            foreach (var player in request.Players.OrderBy(item => item.UserId))
            {
                writer.WriteStartObject();
                writer.WriteString("userId", player.UserId.ToString("D"));
                writer.WriteBoolean("survived", player.Survived);
                writer.WriteBoolean("disconnected", player.Disconnected);
                writer.WriteNumber("detectionCount", player.DetectionCount);
                writer.WriteNumber("downedCount", player.DownedCount);
                writer.WriteNumber("reviveCount", player.ReviveCount);
                writer.WriteNumber("objectiveContribution", player.ObjectiveContribution);
                if (request.EvidenceVersion == 2)
                {
                    writer.WriteNumber("objectiveActions", player.ObjectiveActions!.Value);
                    writer.WriteNumber("rescueActions", player.RescueActions!.Value);
                    writer.WriteBoolean("evidenceComplete", player.EvidenceComplete!.Value);
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };

    private static MatchResultResponse Map(MatchResult result, bool isReplay) => new()
    {
        MatchId = result.MatchId,
        Outcome = result.Outcome,
        StartedAtUtc = result.StartedAtUtc,
        EndedAtUtc = result.EndedAtUtc,
        DurationSeconds = result.DurationSeconds,
        ObjectiveCompletion = result.ObjectiveCompletion,
        PlayerCount = result.PlayerCount,
        RewardStatus = result.RewardStatus,
        SubmittedAtUtc = result.SubmittedAtUtc,
        IsReplay = isReplay,
        Players = result.Players
            .OrderBy(item => item.UserId)
            .Select(item => new MatchResultPlayerResponse
            {
                UserId = item.UserId,
                Survived = item.Survived,
                Disconnected = item.Disconnected,
                DetectionCount = item.DetectionCount,
                DownedCount = item.DownedCount,
                ReviveCount = item.ReviveCount,
                ObjectiveContribution = item.ObjectiveContribution
            })
            .ToArray()
    };

    private static ServiceResult<MatchResultResponse> Failure(string message, string code) =>
        ServiceResult<MatchResultResponse>.Failure(message, code);
}
