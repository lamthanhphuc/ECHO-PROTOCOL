using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.MatchAuthority;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EchoProtocol.Api.Services;

public sealed class MatchAuthorityService : IMatchAuthorityService
{
    private readonly AppDbContext _db;
    private readonly IMatchJoinProofService _proofs;
    private readonly MatchAuthoritySettings _settings;
    private readonly TimeProvider _timeProvider;

    public MatchAuthorityService(
        AppDbContext db,
        IMatchJoinProofService proofs,
        IOptions<MatchAuthoritySettings> settings,
        TimeProvider timeProvider)
    {
        _db = db;
        _proofs = proofs;
        _settings = settings.Value;
        _timeProvider = timeProvider;
    }

    public async Task<ServiceResult<MatchAuthorityResponse>> CreateAsync(
        Guid hostUserId,
        CreateMatchAuthorityRequest request,
        CancellationToken cancellationToken)
    {
        var sessionName = request.FusionSessionName.Trim();
        var now = UtcNow();
        var alreadyActive = await _db.MatchAuthorityBindings.AnyAsync(
            item => item.FusionSessionName == sessionName
                && item.Status != MatchAuthorityStatus.Ended
                && item.LeaseExpiresAtUtc > now,
            cancellationToken);
        if (alreadyActive)
        {
            return Failure<MatchAuthorityResponse>(
                "An active authority binding already exists for this Fusion session",
                ErrorCodes.MatchSessionConflict);
        }

        var binding = new MatchAuthorityBinding
        {
            MatchId = Guid.NewGuid(),
            FusionSessionName = sessionName,
            HostUserId = hostUserId,
            MaxPlayers = request.MaxPlayers,
            Status = MatchAuthorityStatus.Lobby,
            LeaseExpiresAtUtc = now.AddSeconds(_settings.LeaseLifetimeSeconds),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _db.MatchAuthorityBindings.Add(binding);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<MatchAuthorityResponse>.Success(Map(binding), "Match authority created");
    }

    public async Task<ServiceResult<JoinProofResponse>> IssueJoinProofAsync(
        Guid userId,
        Guid matchId,
        IssueJoinProofRequest request,
        CancellationToken cancellationToken)
    {
        var match = await _db.MatchAuthorityBindings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);
        var stateFailure = ValidateJoinable(match, request.FusionSessionName.Trim());
        if (stateFailure is not null)
        {
            return Failure<JoinProofResponse>(stateFailure.Value.Message, stateFailure.Value.Code);
        }

        var expiresAt = UtcNow().AddSeconds(_settings.JoinProofLifetimeSeconds);
        var payload = new MatchJoinProofPayload(
            Guid.NewGuid(), matchId, userId, request.FusionActorNumber,
            match!.FusionSessionName, expiresAt);
        return ServiceResult<JoinProofResponse>.Success(new JoinProofResponse
        {
            Proof = _proofs.Issue(payload),
            ExpiresAtUtc = expiresAt
        }, "Join proof issued");
    }

    public async Task<ServiceResult<MatchPlayerBindingResponse>> BindPlayerAsync(
        Guid hostUserId,
        Guid matchId,
        BindMatchPlayerRequest request,
        CancellationToken cancellationToken)
    {
        var match = await _db.MatchAuthorityBindings
            .Include(item => item.Players)
            .SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);
        var authorityFailure = ValidateActiveHost(match, hostUserId);
        if (authorityFailure is not null)
        {
            return Failure<MatchPlayerBindingResponse>(authorityFailure.Value.Message, authorityFailure.Value.Code);
        }

        if (!_proofs.TryValidate(request.JoinProof, out var proof)
            || proof is null
            || proof.MatchId != matchId
            || proof.FusionActorNumber != request.FusionActorNumber
            || !string.Equals(proof.FusionSessionName, match!.FusionSessionName, StringComparison.Ordinal))
        {
            return Failure<MatchPlayerBindingResponse>("Join proof is invalid or expired", ErrorCodes.JoinProofInvalid);
        }

        var actorBinding = match.Players.SingleOrDefault(
            item => item.FusionActorNumber == request.FusionActorNumber);
        var userBinding = match.Players.SingleOrDefault(item => item.UserId == proof.UserId);
        if ((actorBinding is not null && actorBinding.UserId != proof.UserId)
            || (userBinding is not null && actorBinding is not null && userBinding.Id != actorBinding.Id)
            || (userBinding is not null && userBinding.DisconnectedAtUtc is null
                && userBinding.FusionActorNumber != request.FusionActorNumber))
        {
            return Failure<MatchPlayerBindingResponse>(
                "Fusion actor or backend user is already bound to another player",
                ErrorCodes.MatchPlayerBindingConflict);
        }

        var connectedCount = match.Players.Count(item => item.DisconnectedAtUtc is null);
        if (userBinding is null && connectedCount >= match.MaxPlayers)
        {
            return Failure<MatchPlayerBindingResponse>("Match capacity reached", ErrorCodes.MatchCapacityReached);
        }

        var now = UtcNow();
        var binding = userBinding ?? new MatchPlayerBinding
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            UserId = proof.UserId,
            BoundAtUtc = now
        };
        binding.FusionActorNumber = request.FusionActorNumber;
        binding.JoinProofId = proof.ProofId;
        binding.LastSeenAtUtc = now;
        binding.DisconnectedAtUtc = null;
        if (userBinding is null)
        {
            _db.MatchPlayerBindings.Add(binding);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<MatchPlayerBindingResponse>.Success(Map(binding), "Player identity bound");
    }

    public async Task<ServiceResult<MatchAuthorityResponse>> RenewLeaseAsync(
        Guid hostUserId,
        Guid matchId,
        CancellationToken cancellationToken)
    {
        var match = await _db.MatchAuthorityBindings
            .SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);
        var authorityFailure = ValidateActiveHost(match, hostUserId);
        if (authorityFailure is not null)
        {
            return Failure<MatchAuthorityResponse>(authorityFailure.Value.Message, authorityFailure.Value.Code);
        }

        var now = UtcNow();
        match!.LeaseExpiresAtUtc = now.AddSeconds(_settings.LeaseLifetimeSeconds);
        match.UpdatedAtUtc = now;
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<MatchAuthorityResponse>.Success(Map(match), "Match authority lease renewed");
    }

    public async Task<ServiceResult<MatchAuthorityResponse>> StartAsync(
        Guid hostUserId,
        Guid matchId,
        CancellationToken cancellationToken)
    {
        var match = await _db.MatchAuthorityBindings.Include(item => item.Players)
            .SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);
        var authorityFailure = ValidateActiveHost(match, hostUserId);
        if (authorityFailure is not null)
        {
            return Failure<MatchAuthorityResponse>(authorityFailure.Value.Message, authorityFailure.Value.Code);
        }

        if (match!.Players.Count(item => item.DisconnectedAtUtc is null) < 2)
        {
            return Failure<MatchAuthorityResponse>(
                "At least two bound players are required to start",
                ErrorCodes.ValidationError);
        }

        match.Status = MatchAuthorityStatus.InMatch;
        match.UpdatedAtUtc = UtcNow();
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<MatchAuthorityResponse>.Success(Map(match), "Match started");
    }

    public async Task<ServiceResult<MatchPlayerBindingResponse>> MarkPlayerDisconnectedAsync(
        Guid hostUserId,
        Guid matchId,
        int fusionActorNumber,
        CancellationToken cancellationToken)
    {
        var match = await _db.MatchAuthorityBindings.Include(item => item.Players)
            .SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);
        var authorityFailure = ValidateActiveHost(match, hostUserId);
        if (authorityFailure is not null)
        {
            return Failure<MatchPlayerBindingResponse>(authorityFailure.Value.Message, authorityFailure.Value.Code);
        }

        var player = match!.Players.SingleOrDefault(item => item.FusionActorNumber == fusionActorNumber);
        if (player is null)
        {
            return Failure<MatchPlayerBindingResponse>("Player binding not found", ErrorCodes.NotFound);
        }

        player.DisconnectedAtUtc = UtcNow();
        player.LastSeenAtUtc = player.DisconnectedAtUtc.Value;
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<MatchPlayerBindingResponse>.Success(Map(player), "Player marked disconnected");
    }

    public async Task<ServiceResult<MatchAuthorityResponse>> EndAsync(
        Guid hostUserId,
        Guid matchId,
        EndMatchAuthorityRequest request,
        CancellationToken cancellationToken)
    {
        var match = await _db.MatchAuthorityBindings
            .SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);
        if (match is null)
        {
            return Failure<MatchAuthorityResponse>("Match not found", ErrorCodes.MatchNotFound);
        }

        if (match.HostUserId != hostUserId)
        {
            return Failure<MatchAuthorityResponse>("Only the bound Host can end this match", ErrorCodes.MatchAuthorityForbidden);
        }

        if (match.Status != MatchAuthorityStatus.Ended)
        {
            var now = UtcNow();
            match.Status = MatchAuthorityStatus.Ended;
            match.EndedAtUtc = now;
            match.UpdatedAtUtc = now;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return ServiceResult<MatchAuthorityResponse>.Success(Map(match), "Match authority ended");
    }

    public async Task<bool> CanSubmitTelemetryAsync(
        Guid submittingUserId,
        Guid matchId,
        Guid eventUserId,
        CancellationToken cancellationToken)
    {
        var now = UtcNow();
        var cutoff = now.AddHours(-_settings.TelemetryDelegationRetentionHours);
        return await _db.MatchAuthorityBindings.AsNoTracking().AnyAsync(
            match => match.MatchId == matchId
                && match.HostUserId == submittingUserId
                && (match.Status != MatchAuthorityStatus.Ended
                    ? match.LeaseExpiresAtUtc > now
                    : match.EndedAtUtc >= cutoff)
                && match.Players.Any(player => player.UserId == eventUserId),
            cancellationToken);
    }

    public async Task<bool> CanSubmitSystemTelemetryAsync(
        Guid submittingUserId,
        Guid matchId,
        CancellationToken cancellationToken)
    {
        var now = UtcNow();
        var cutoff = now.AddHours(-_settings.TelemetryDelegationRetentionHours);
        return await _db.MatchAuthorityBindings.AsNoTracking().AnyAsync(
            match => match.MatchId == matchId
                && match.HostUserId == submittingUserId
                && (match.Status != MatchAuthorityStatus.Ended
                    ? match.LeaseExpiresAtUtc > now
                    : match.EndedAtUtc >= cutoff),
            cancellationToken);
    }

    private (string Message, string Code)? ValidateJoinable(
        MatchAuthorityBinding? match,
        string sessionName)
    {
        if (match is null)
        {
            return ("Match not found", ErrorCodes.MatchNotFound);
        }

        if (!string.Equals(match.FusionSessionName, sessionName, StringComparison.Ordinal))
        {
            return ("Fusion session does not match", ErrorCodes.JoinProofInvalid);
        }

        if (match.Status != MatchAuthorityStatus.Lobby)
        {
            return ("Match no longer accepts join proofs", ErrorCodes.MatchAlreadyEnded);
        }

        return match.LeaseExpiresAtUtc <= UtcNow()
            ? ("Match authority lease expired", ErrorCodes.MatchLeaseExpired)
            : null;
    }

    private (string Message, string Code)? ValidateActiveHost(
        MatchAuthorityBinding? match,
        Guid hostUserId)
    {
        if (match is null)
        {
            return ("Match not found", ErrorCodes.MatchNotFound);
        }

        if (match.HostUserId != hostUserId)
        {
            return ("Only the bound Host can perform this operation", ErrorCodes.MatchAuthorityForbidden);
        }

        if (match.Status == MatchAuthorityStatus.Ended)
        {
            return ("Match already ended", ErrorCodes.MatchAlreadyEnded);
        }

        return match.LeaseExpiresAtUtc <= UtcNow()
            ? ("Match authority lease expired", ErrorCodes.MatchLeaseExpired)
            : null;
    }

    private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private static MatchAuthorityResponse Map(MatchAuthorityBinding match) => new()
    {
        MatchId = match.MatchId,
        FusionSessionName = match.FusionSessionName,
        HostUserId = match.HostUserId,
        MaxPlayers = match.MaxPlayers,
        Status = match.Status,
        LeaseExpiresAtUtc = match.LeaseExpiresAtUtc
    };

    private static MatchPlayerBindingResponse Map(MatchPlayerBinding player) => new()
    {
        UserId = player.UserId,
        FusionActorNumber = player.FusionActorNumber,
        BoundAtUtc = player.BoundAtUtc
    };

    private static ServiceResult<T> Failure<T>(string message, string code) =>
        ServiceResult<T>.Failure(message, code);
}
