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
    private const int MinimumDurationSeconds = 60;
    private const int MaximumDurationSeconds = 15 * 60;

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

            var now = UtcNow();
            if (match.Status != MatchAuthorityStatus.InMatch)
            {
                return Failure(
                    "Only an active InMatch match can accept a result",
                    ErrorCodes.MatchResultInvalidState);
            }

            if (match.LeaseExpiresAtUtc <= now)
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
            if (durationSeconds is < MinimumDurationSeconds or > MaximumDurationSeconds)
            {
                return Failure(
                    $"Match duration must be between {MinimumDurationSeconds} and {MaximumDurationSeconds} seconds",
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
                PlayerCount = request.Players.Count,
                PayloadHash = payloadHash,
                RewardStatus = MatchRewardStatus.Pending,
                SubmittedAtUtc = now,
                Players = request.Players.Select(player => new MatchResultPlayer
                {
                    MatchId = match.MatchId,
                    UserId = player.UserId,
                    Survived = player.Survived,
                    Disconnected = player.Disconnected,
                    DetectionCount = player.DetectionCount,
                    DownedCount = player.DownedCount,
                    ReviveCount = player.ReviveCount,
                    ObjectiveContribution = player.ObjectiveContribution
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

        return null;
    }

    private static (string Message, string Code)? ValidateRoster(
        IReadOnlyCollection<SubmitMatchResultPlayerRequest> requestedPlayers,
        ICollection<MatchPlayerBinding> boundPlayers)
    {
        var bindingsByUser = boundPlayers.ToDictionary(item => item.UserId);
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
            writer.WriteNumber("objectiveCompletion", request.ObjectiveCompletion);
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
