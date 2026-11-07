using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.MatchAuthority;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
        if (await HasOtherActiveMatchAsync(
                hostUserId,
                excludedMatchId: null,
                cancellationToken))
        {
            return Failure<MatchAuthorityResponse>(
                "Account is already participating in another active match",
                ErrorCodes.AccountAlreadyInActiveMatch);
        }

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

        if (await HasOtherActiveMatchAsync(userId, matchId, cancellationToken))
        {
            return Failure<JoinProofResponse>(
                "Account is already participating in another active match",
                ErrorCodes.AccountAlreadyInActiveMatch);
        }

        if (match!.Status == MatchAuthorityStatus.InMatch)
        {
            var existingMember = await _db.MatchPlayerBindings.AsNoTracking()
                .AnyAsync(item => item.MatchId == matchId
                    && item.UserId == userId && item.Participated, cancellationToken);

            if (!existingMember)
            {
                return Failure<JoinProofResponse>(
                    "Only existing roster members may reconnect",
                    ErrorCodes.MatchAlreadyEnded);
            }
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

    public Task<ServiceResult<MatchPlayerBindingResponse>> BindPlayerAsync(Guid hostUserId, Guid matchId, BindMatchPlayerRequest request, CancellationToken cancellationToken) =>
        WithMatchLockAsync(matchId, () => BindPlayerAsyncCore(hostUserId, matchId, request, cancellationToken), cancellationToken);

    private async Task<ServiceResult<MatchPlayerBindingResponse>> BindPlayerAsyncCore(
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

        if (await HasOtherActiveMatchAsync(proof.UserId, matchId, cancellationToken))
        {
            return Failure<MatchPlayerBindingResponse>(
                "Account is already participating in another active match",
                ErrorCodes.AccountAlreadyInActiveMatch);
        }

        var actorBinding = match.Players.SingleOrDefault(
            item => item.FusionActorNumber == request.FusionActorNumber
                && item.DisconnectedAtUtc == null);
        var userBinding = match.Players.SingleOrDefault(
            item => item.UserId == proof.UserId);
        if (match.Status == MatchAuthorityStatus.InMatch && userBinding?.Participated != true)
            return Failure<MatchPlayerBindingResponse>("Player is not in the started roster", ErrorCodes.MatchPlayerBindingConflict);

        var isRosterActorRemap =
            userBinding is not null
            && userBinding.DisconnectedAtUtc == null
            && userBinding.FusionActorNumber != request.FusionActorNumber;

        if ((actorBinding is not null
                && actorBinding.UserId != proof.UserId)
            || (userBinding is not null
                && actorBinding is not null
                && userBinding.Id != actorBinding.Id)
            || (userBinding is not null
                && userBinding.DisconnectedAtUtc == null
                && userBinding.FusionActorNumber != request.FusionActorNumber
                && !isRosterActorRemap))
        {
            return Failure<MatchPlayerBindingResponse>(
                "Fusion actor or backend user is already bound to another player",
                ErrorCodes.MatchPlayerBindingConflict);
        }

        var connectedCount = match.Players.Count(item => item.DisconnectedAtUtc is null);
        if ((userBinding is null || userBinding.DisconnectedAtUtc.HasValue) && connectedCount >= match.MaxPlayers)
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

    public Task<ServiceResult<MatchAuthorityResponse>> TakeoverAsync(Guid newHostUserId, Guid matchId, TakeoverMatchAuthorityRequest request, CancellationToken cancellationToken) =>
        WithMatchLockAsync(matchId, () => TakeoverAsyncCore(newHostUserId, matchId, request, cancellationToken), cancellationToken);

    private async Task<ServiceResult<MatchAuthorityResponse>> TakeoverAsyncCore(
        Guid newHostUserId,
        Guid matchId,
        TakeoverMatchAuthorityRequest request,
        CancellationToken cancellationToken)
    {
        var match = await _db.MatchAuthorityBindings
            .Include(item => item.Players)
            .SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);

        if (match is null)
        {
            return Failure<MatchAuthorityResponse>(
                "Match not found",
                ErrorCodes.MatchNotFound);
        }

        if (match.Status == MatchAuthorityStatus.Ended)
        {
            return Failure<MatchAuthorityResponse>(
                "Match already ended",
                ErrorCodes.MatchAlreadyEnded);
        }

        var sessionName = request.FusionSessionName?.Trim() ?? string.Empty;
        if (!string.Equals(
                match.FusionSessionName,
                sessionName,
                StringComparison.Ordinal))
        {
            return Failure<MatchAuthorityResponse>(
                "Fusion session does not match",
                ErrorCodes.MatchSessionConflict);
        }

        var candidate = match.Players.SingleOrDefault(item =>
            item.UserId == newHostUserId
            && item.DisconnectedAtUtc == null);

        if (candidate is null)
        {
            return Failure<MatchAuthorityResponse>(
                "Only a connected bound roster member can take over Host authority",
                ErrorCodes.MatchAuthorityForbidden);
        }

        var actorCollision = match.Players.SingleOrDefault(item =>
            item.Id != candidate.Id
            && item.FusionActorNumber == request.FusionActorNumber
            && item.DisconnectedAtUtc == null);

        if (actorCollision is not null
            && actorCollision.UserId != match.HostUserId)
        {
            return Failure<MatchAuthorityResponse>(
                "Migrated Host actor number is already owned by another connected player",
                ErrorCodes.MatchPlayerBindingConflict);
        }

        if (await HasOtherActiveMatchAsync(
                newHostUserId,
                matchId,
                cancellationToken))
        {
            return Failure<MatchAuthorityResponse>(
                "Account is already participating in another active match",
                ErrorCodes.AccountAlreadyInActiveMatch);
        }

        var now = UtcNow();

        if (match.HostUserId == newHostUserId)
        {
            if (match.LeaseExpiresAtUtc <= now)
            {
                return Failure<MatchAuthorityResponse>(
                    "Match authority lease expired",
                    ErrorCodes.MatchLeaseExpired);
            }

            match.LeaseExpiresAtUtc =
                now.AddSeconds(_settings.LeaseLifetimeSeconds);
            match.UpdatedAtUtc = now;
            candidate.FusionActorNumber =
                request.FusionActorNumber;
            candidate.LastSeenAtUtc = now;

            await _db.SaveChangesAsync(cancellationToken);

            return ServiceResult<MatchAuthorityResponse>.Success(
                Map(match),
                "Host authority already assigned");
        }

        if (match.LeaseExpiresAtUtc > now)
        {
            return Failure<MatchAuthorityResponse>(
                "Current Host authority lease is still active",
                ErrorCodes.MatchAuthorityTakeoverNotReady);
        }
        var previousHost = match.Players.SingleOrDefault(item =>
            item.UserId == match.HostUserId
            && item.DisconnectedAtUtc == null);

        if (previousHost is not null)
        {
            previousHost.DisconnectedAtUtc = now;
            previousHost.LastSeenAtUtc = now;
        }

        IDbContextTransaction? actorReuseTransaction = null;

        try
        {
            // With the PostgreSQL partial unique index, the old Host must be
            // persisted as disconnected before its actor number can be
            // assigned to the promoted Host. Keep both writes atomic.
            if (previousHost is not null
                && previousHost.FusionActorNumber
                    == request.FusionActorNumber
                && _db.Database.IsRelational() && _db.Database.CurrentTransaction == null)
            {
                actorReuseTransaction =
                    await _db.Database.BeginTransactionAsync(
                        cancellationToken);

                await _db.SaveChangesAsync(
                    cancellationToken);
            }

            candidate.FusionActorNumber =
                request.FusionActorNumber;
            candidate.LastSeenAtUtc = now;

            match.HostUserId = newHostUserId;
            match.LeaseExpiresAtUtc =
                now.AddSeconds(_settings.LeaseLifetimeSeconds);
            match.UpdatedAtUtc = now;

            await _db.SaveChangesAsync(cancellationToken);

            if (actorReuseTransaction is not null)
            {
                await actorReuseTransaction.CommitAsync(
                    cancellationToken);
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure<MatchAuthorityResponse>(
                "Host authority changed concurrently; retry against current authority state",
                ErrorCodes.MatchAuthorityTakeoverNotReady);
        }
        catch (DbUpdateException)
        {
            return Failure<MatchAuthorityResponse>(
                "Migrated Host actor remap conflicted with the active roster",
                ErrorCodes.MatchPlayerBindingConflict);
        }
        finally
        {
            if (actorReuseTransaction is not null)
            {
                await actorReuseTransaction.DisposeAsync();
            }
        }

        return ServiceResult<MatchAuthorityResponse>.Success(
            Map(match),
            "Host authority transferred");
    }

    public Task<ServiceResult<MatchAuthorityResponse>> RelinquishAsync(
        Guid hostUserId,
        Guid matchId,
        CancellationToken cancellationToken) =>
        WithMatchLockAsync(matchId, () => RelinquishAsyncCore(hostUserId, matchId, cancellationToken), cancellationToken);

    private async Task<ServiceResult<MatchAuthorityResponse>> RelinquishAsyncCore(
        Guid hostUserId,
        Guid matchId,
        CancellationToken cancellationToken)
    {
        var match = await _db.MatchAuthorityBindings
            .Include(item => item.Players)
            .SingleOrDefaultAsync(
                item => item.MatchId == matchId,
                cancellationToken);

        var authorityFailure =
            ValidateActiveHost(match, hostUserId);

        if (authorityFailure is not null)
        {
            return Failure<MatchAuthorityResponse>(
                authorityFailure.Value.Message,
                authorityFailure.Value.Code);
        }

        var now = UtcNow();

        var hostPlayer =
            match!.Players.SingleOrDefault(item =>
                item.UserId == hostUserId
                && item.DisconnectedAtUtc == null);

        if (hostPlayer is not null)
        {
            hostPlayer.DisconnectedAtUtc = now;
            hostPlayer.LastSeenAtUtc = now;
        }

        match.LeaseExpiresAtUtc = now;
        match.UpdatedAtUtc = now;
        if (match.Players.All(item => item.DisconnectedAtUtc.HasValue))
        {
            match.Status = MatchAuthorityStatus.Ended;
            match.EndedAtUtc = now;
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure<MatchAuthorityResponse>(
                "Host authority changed concurrently; relinquish was not applied",
                ErrorCodes.MatchAuthorityTakeoverNotReady);
        }

        return ServiceResult<MatchAuthorityResponse>.Success(
            Map(match),
            "Host authority relinquished");
    }

    public async Task<ServiceResult<MatchPlayerEntitlementsResponse>> GetPlayerEntitlementsAsync(
        Guid hostUserId,
        Guid matchId,
        int fusionActorNumber,
        CancellationToken cancellationToken)
    {
        var match = await _db.MatchAuthorityBindings.AsNoTracking()
            .Include(item => item.Players)
            .SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);
        var authorityFailure = ValidateActiveHost(match, hostUserId);
        if (authorityFailure is not null)
            return Failure<MatchPlayerEntitlementsResponse>(
                authorityFailure.Value.Message, authorityFailure.Value.Code);

        var player = match!.Players.SingleOrDefault(item =>
            item.FusionActorNumber == fusionActorNumber && item.DisconnectedAtUtc == null);
        if (player is null)
            return Failure<MatchPlayerEntitlementsResponse>(
                "Player is not bound to this match", ErrorCodes.MatchPlayerBindingConflict);

        var itemIds = await _db.InventoryItems.AsNoTracking()
            .Where(item => item.UserId == player.UserId)
            .Select(item => item.ShopItemId)
            .ToArrayAsync(cancellationToken);
        return ServiceResult<MatchPlayerEntitlementsResponse>.Success(new MatchPlayerEntitlementsResponse
        {
            UserId = player.UserId,
            ShopItemIds = itemIds
        });
    }

    public Task<ServiceResult<MatchAuthorityResponse>> RenewLeaseAsync(Guid hostUserId, Guid matchId, CancellationToken cancellationToken) =>
        WithMatchLockAsync(matchId, () => RenewLeaseAsyncCore(hostUserId, matchId, cancellationToken), cancellationToken);

    private async Task<ServiceResult<MatchAuthorityResponse>> RenewLeaseAsyncCore(
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
        var renewedUntil = now.AddSeconds(_settings.LeaseLifetimeSeconds);
        if (match!.LeaseExpiresAtUtc < renewedUntil) match.LeaseExpiresAtUtc = renewedUntil;
        match.UpdatedAtUtc = now;
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<MatchAuthorityResponse>.Success(Map(match), "Match authority lease renewed");
    }

    public Task<ServiceResult<MatchAuthorityResponse>> StartAsync(
        Guid hostUserId,
        Guid matchId,
        CancellationToken cancellationToken) =>
        StartAsync(hostUserId, matchId, cancellationToken, null);

    public Task<ServiceResult<MatchAuthorityResponse>> StartAsync(Guid hostUserId, Guid matchId,
        CancellationToken cancellationToken, StartMatchAuthorityRequest? request) =>
        WithMatchLockAsync(matchId,
            () => StartCoreAsync(hostUserId, matchId, cancellationToken, request, null),
            cancellationToken);

    public Task<ServiceResult<MatchAuthorityResponse>> StartV2Async(
        Guid hostUserId,
        Guid matchId,
        StartMatchV2Request request,
        CancellationToken cancellationToken) =>
        WithMatchLockAsync(matchId,
            () => StartCoreAsync(hostUserId, matchId, cancellationToken, null, request),
            cancellationToken);

    private async Task<ServiceResult<MatchAuthorityResponse>> StartCoreAsync(Guid hostUserId, Guid matchId,
        CancellationToken cancellationToken, StartMatchAuthorityRequest? legacyRequest,
        StartMatchV2Request? v2Request)
    {
        var isV2 = v2Request is not null;
        var mapId = (v2Request?.MapId ?? legacyRequest?.MapId)?.Trim();
        var difficulty = (v2Request?.Difficulty ?? legacyRequest?.Difficulty)?.Trim().ToUpperInvariant();
        var mode = v2Request?.ResolutionMode?.Trim().ToUpperInvariant();
        var source = v2Request?.BalanceSource?.Trim().ToUpperInvariant();
        var protocol = v2Request?.BalanceProtocolVersion?.Trim();

        MatchAuthorityBinding? match = await _db.MatchAuthorityBindings.Include(item => item.Players)
            .SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);
        if (isV2)
        {
            var existingFailure = ValidateActiveHost(match, hostUserId);
            if (existingFailure is not null)
                return Failure<MatchAuthorityResponse>(existingFailure.Value.Message, existingFailure.Value.Code);

            if (match!.Status == MatchAuthorityStatus.InMatch)
            {
                if (match.MapId != mapId || match.Difficulty != difficulty
                    || match.ResolutionMode != mode || match.BalanceSource != source
                    || match.BalanceProtocolVersion != protocol)
                    return Failure<MatchAuthorityResponse>(
                        "Locked match context differs from the request", ErrorCodes.MatchSessionConflict);

                return ServiceResult<MatchAuthorityResponse>.Success(Map(match), "Match already started");
            }
        }
        else if (match?.Status == MatchAuthorityStatus.InMatch && match.BalanceProtocolVersion is not null)
        {
            var existingFailure = ValidateActiveHost(match, hostUserId);
            if (existingFailure is not null)
                return Failure<MatchAuthorityResponse>(existingFailure.Value.Message, existingFailure.Value.Code);
            return Failure<MatchAuthorityResponse>(
                "Legacy request conflicts with locked V2 match context", ErrorCodes.MatchSessionConflict);
        }

        if ((mapId is null) != (difficulty is null)
            || (mapId is not null && !LeaderboardCatalog.IsKnownMap(mapId))
            || (difficulty is not null && !LeaderboardCatalog.IsKnownDifficulty(difficulty)))
        {
            return Failure<MatchAuthorityResponse>(
                "Map and difficulty must be supplied together with supported values",
                ErrorCodes.ValidationError);
        }

        if (isV2 && (mapId is null || difficulty is null
            || mode is not ("FIXED" or "ADAPTIVE")
            || source is not ("LOCAL" or "REMOTE_FIXED")
            || (source == "REMOTE_FIXED" && mode != "FIXED")
            || (source == "REMOTE_FIXED" && (!MatchContentCatalog.Entries.Any(x => x.Map == mapId) ||
                (mapId == "Scene_B" && (v2Request?.SupportsGameplayTuning != true || string.IsNullOrWhiteSpace(v2Request.GameVersion)))))))
        {
            return Failure<MatchAuthorityResponse>(
                "Invalid Remote Fixed start context", ErrorCodes.ValidationError);
        }

        var authorityFailure = ValidateActiveHost(match, hostUserId);
        if (authorityFailure is not null)
        {
            return Failure<MatchAuthorityResponse>(authorityFailure.Value.Message, authorityFailure.Value.Code);
        }

        if (match!.Status == MatchAuthorityStatus.InMatch)
        {
            if (isV2)
            {
                if (match.MapId != mapId || match.Difficulty != difficulty
                    || match.ResolutionMode != mode || match.BalanceSource != source
                    || match.BalanceProtocolVersion != protocol)
                    return Failure<MatchAuthorityResponse>(
                        "Locked match context differs from the request", ErrorCodes.MatchSessionConflict);
            }
            else if (match.BalanceProtocolVersion is not null ||
                     (mapId is not null && (match.MapId != mapId || match.Difficulty != difficulty)))
                return Failure<MatchAuthorityResponse>(
                    "Legacy request conflicts with locked match context", ErrorCodes.MatchSessionConflict);

            return ServiceResult<MatchAuthorityResponse>.Success(Map(match), "Match already started");
        }

        if (isV2 && protocol != RemoteFixedBalanceContract.ProtocolVersion)
            return Failure<MatchAuthorityResponse>(
                "Unsupported balance protocol version", ErrorCodes.ValidationError);

        if (match.Players.Count(item => item.DisconnectedAtUtc is null) < 1)
        {
            return Failure<MatchAuthorityResponse>(
                "At least one bound player is required to start",
                ErrorCodes.ValidationError);
        }

        RemoteFixedBalanceVersion? selected = null;
        if (source == "REMOTE_FIXED")
        {
            if (!_settings.EnableRemoteFixedTest)
                return Failure<MatchAuthorityResponse>("Remote Fixed testing is disabled", ErrorCodes.BalanceTestDisabled);

            selected = await _db.RemoteFixedBalanceVersions.AsNoTracking().SingleOrDefaultAsync(item =>
                item.IsActive && item.MapId == mapId && item.Difficulty == difficulty &&
                item.ProtocolVersion == protocol, cancellationToken);
            if (selected is null)
                return Failure<MatchAuthorityResponse>("No active Remote Fixed Balance version", ErrorCodes.BalanceVersionUnavailable);

            if (!RemoteFixedBalanceContract.IsValidSnapshot(selected.SnapshotJson, mapId) ||
                !string.Equals(RemoteFixedBalanceContract.ComputeHash(selected.SnapshotJson),
                    selected.ContentHash, StringComparison.Ordinal))
                return Failure<MatchAuthorityResponse>("Remote Fixed Balance snapshot is invalid", ErrorCodes.BalanceSnapshotInvalid);
            if (RemoteFixedBalanceContract.UsesGameplayTuning(selected.SnapshotJson) && v2Request?.SupportsGameplayTuning != true)
                return Failure<MatchAuthorityResponse>("This balance version requires a build with RFB-2 gameplay tuning", ErrorCodes.BalanceSnapshotInvalid);
            if (RemoteFixedBalanceContract.GameVersion(selected.SnapshotJson) is { } build &&
                (build != v2Request?.GameVersion || !_settings.RemoteFixedGameVersions.Contains(build)))
                return Failure<MatchAuthorityResponse>("Unity build does not match the selected content version", ErrorCodes.BalanceSnapshotInvalid);
        }

        var now = UtcNow();
        match.MapId = mapId;
        match.Difficulty = difficulty;
        match.ResolutionMode = mode;
        match.BalanceSource = source;
        match.BalanceProtocolVersion = protocol;
        match.BalanceVersion = selected?.Version;
        match.BalanceHash = selected?.ContentHash;
        match.MatchDataPolicy = selected is null
            ? RemoteFixedBalanceContract.Standard
            : RemoteFixedBalanceContract.TestOnly;
        foreach (var player in match.Players)
            player.Participated = player.DisconnectedAtUtc == null;
        match.Status = MatchAuthorityStatus.InMatch;
        match.StartedAtUtc ??= now;
        // Unity scene loading can pause host heartbeats; resume the normal lease on the next renewal.
        match.LeaseExpiresAtUtc = now.AddSeconds(_settings.LeaseLifetimeSeconds * 2);
        var pendingPreMatch = await _db.AEDV02Decisions.Where(x => x.MatchId == matchId && x.HostUserId == hostUserId &&
            x.DecisionPoint == "PRE_MATCH" && x.ApprovalStatus == "APPROVED" && x.ApplyStatus == "PENDING").ToListAsync(cancellationToken);
        foreach (var decision in pendingPreMatch) decision.ExpiresAtUtc = match.LeaseExpiresAtUtc;
        match.UpdatedAtUtc = now;
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<MatchAuthorityResponse>.Success(Map(match), "Match started");
    }

    public async Task<ServiceResult<LockedBalanceResponse>> GetLockedBalanceAsync(
        Guid userId,
        Guid matchId,
        CancellationToken cancellationToken)
    {
        var match = await _db.MatchAuthorityBindings.AsNoTracking().Include(item => item.Players)
            .SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);
        if (match is null)
            return Failure<LockedBalanceResponse>("Match not found", ErrorCodes.MatchNotFound);

        if (match.HostUserId != userId && !match.Players.Any(item => item.UserId == userId && item.Participated))
            return Failure<LockedBalanceResponse>("Not authorized for this match", ErrorCodes.MatchAuthorityForbidden);

        if (match.BalanceSource != "REMOTE_FIXED" || match.BalanceVersion is null ||
            match.BalanceHash is null || match.BalanceProtocolVersion is null)
            return Failure<LockedBalanceResponse>("Match has no Remote Fixed Balance", ErrorCodes.BalanceNotRemoteFixed);

        var version = await _db.RemoteFixedBalanceVersions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Version == match.BalanceVersion, cancellationToken);
        if (version is null || version.MapId != match.MapId || version.Difficulty != match.Difficulty ||
            version.ProtocolVersion != match.BalanceProtocolVersion ||
            !RemoteFixedBalanceContract.IsValidSnapshot(version.SnapshotJson, version.MapId) ||
            !string.Equals(RemoteFixedBalanceContract.ComputeHash(version.SnapshotJson), match.BalanceHash, StringComparison.Ordinal) ||
            !string.Equals(version.ContentHash, match.BalanceHash, StringComparison.Ordinal))
            return Failure<LockedBalanceResponse>("Locked balance snapshot is invalid", ErrorCodes.BalanceSnapshotInvalid);

        return ServiceResult<LockedBalanceResponse>.Success(new LockedBalanceResponse
        {
            MatchId = matchId,
            BalanceVersion = version.Version,
            BalanceHash = match.BalanceHash,
            BalanceProtocolVersion = version.ProtocolVersion,
            SnapshotPayloadBase64 = Convert.ToBase64String(RemoteFixedBalanceContract.GetBytes(version.SnapshotJson))
        });
    }

    public Task<ServiceResult<MatchPlayerBindingResponse>> MarkPlayerDisconnectedAsync(
        Guid hostUserId,
        Guid matchId,
        int fusionActorNumber,
        Guid expectedUserId,
        CancellationToken cancellationToken) =>
        WithMatchLockAsync(matchId, () => MarkPlayerDisconnectedAsyncCore(hostUserId, matchId, fusionActorNumber, expectedUserId, cancellationToken), cancellationToken);

    private async Task<ServiceResult<MatchPlayerBindingResponse>> MarkPlayerDisconnectedAsyncCore(
        Guid hostUserId,
        Guid matchId,
        int fusionActorNumber,
        Guid expectedUserId,
        CancellationToken cancellationToken)
    {
        if (fusionActorNumber < 1 || expectedUserId == Guid.Empty)
        {
            return Failure<MatchPlayerBindingResponse>(
                "An expected bound user is required for disconnect",
                ErrorCodes.MatchPlayerBindingConflict);
        }

        var match = await _db.MatchAuthorityBindings.Include(item => item.Players)
            .SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);
        var authorityFailure = ValidateActiveHost(match, hostUserId);
        if (authorityFailure is not null)
        {
            return Failure<MatchPlayerBindingResponse>(authorityFailure.Value.Message, authorityFailure.Value.Code);
        }

        var player = match!.Players.SingleOrDefault(item =>
            item.FusionActorNumber == fusionActorNumber
            && item.DisconnectedAtUtc == null);
        if (player is null)
        {
            return Failure<MatchPlayerBindingResponse>("Player binding not found", ErrorCodes.NotFound);
        }

        if (player.UserId != expectedUserId)
        {
            return Failure<MatchPlayerBindingResponse>(
                "Fusion actor no longer belongs to the expected player",
                ErrorCodes.MatchPlayerBindingConflict);
        }

        var now = UtcNow();

        if (_db.Database.IsRelational())
        {
            // Use a single conditional SQL UPDATE: a rebind to another user,
            // Host transfer, or expired lease between SELECT and UPDATE must
            // not disconnect the new roster owner.
            var affected = await _db.MatchPlayerBindings
                .Where(item => item.MatchId == matchId
                    && item.FusionActorNumber == fusionActorNumber
                    && item.UserId == expectedUserId
                    && item.DisconnectedAtUtc == null
                    && _db.MatchAuthorityBindings.Any(binding =>
                        binding.MatchId == matchId
                        && binding.HostUserId == hostUserId
                        && binding.Status != MatchAuthorityStatus.Ended
                        && binding.LeaseExpiresAtUtc > now))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.DisconnectedAtUtc, (DateTime?)now)
                    .SetProperty(item => item.LastSeenAtUtc, now)
                    .SetProperty(item => item.HadGameplayDisconnect,
                        item => item.HadGameplayDisconnect || match.Status == MatchAuthorityStatus.InMatch),
                    cancellationToken);

            if (affected != 1)
            {
                return Failure<MatchPlayerBindingResponse>(
                    "Disconnect rejected because the roster binding changed",
                    ErrorCodes.MatchPlayerBindingConflict);
            }

            // ExecuteUpdate bypasses EF change tracking; refresh this object
            // so the API response reports the committed disconnect state.
            player.DisconnectedAtUtc = now;
            player.LastSeenAtUtc = now;
        }
        else
        {
            // Unit-test in-memory provider does not support ExecuteUpdate.
            player.DisconnectedAtUtc = now;
            player.LastSeenAtUtc = now;
            if (match.Status == MatchAuthorityStatus.InMatch) player.HadGameplayDisconnect = true;
            await _db.SaveChangesAsync(cancellationToken);
        }


        if (match.Players.All(item => item.DisconnectedAtUtc.HasValue))
        {
            match.Status = MatchAuthorityStatus.Ended;
            match.EndedAtUtc = now;
            match.UpdatedAtUtc = now;
            match.LeaseExpiresAtUtc = now;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return ServiceResult<MatchPlayerBindingResponse>.Success(Map(player), "Player marked disconnected");
    }

    public Task<ServiceResult<MatchAuthorityResponse>> EndAsync(
        Guid hostUserId,
        Guid matchId,
        EndMatchAuthorityRequest request,
        CancellationToken cancellationToken) =>
        WithMatchLockAsync(matchId, () => EndAsyncCore(hostUserId, matchId, request, cancellationToken), cancellationToken);

    private async Task<ServiceResult<MatchAuthorityResponse>> EndAsyncCore(
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

        if (match.Status != MatchAuthorityStatus.Lobby
            && match.Status != MatchAuthorityStatus.InMatch)
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

    private async Task<bool> HasOtherActiveMatchAsync(
        Guid userId,
        Guid? excludedMatchId,
        CancellationToken cancellationToken)
    {
        var now = UtcNow();

        var activeMatches = _db.MatchAuthorityBindings
            .AsNoTracking()
            .Where(match =>
                match.Status != MatchAuthorityStatus.Ended
                && match.LeaseExpiresAtUtc > now
                && (!excludedMatchId.HasValue
                    || match.MatchId != excludedMatchId.Value));

        if (await activeMatches.AnyAsync(
                match => match.HostUserId == userId,
                cancellationToken))
        {
            return true;
        }

        var activeMatchIds = activeMatches.Select(match => match.MatchId);

        return await _db.MatchPlayerBindings
            .AsNoTracking()
            .AnyAsync(
                player =>
                    player.UserId == userId
                    && player.DisconnectedAtUtc == null
                    && activeMatchIds.Contains(player.MatchId),
                cancellationToken);
    }

    private async Task<ServiceResult<T>> WithMatchLockAsync<T>(Guid matchId,
        Func<Task<ServiceResult<T>>> action, CancellationToken cancellationToken)
    {
        if (_db.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL") return await action();
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _db.MatchAuthorityBindings.FromSqlInterpolated(
            $"SELECT * FROM \"MatchAuthorityBindings\" WHERE \"MatchId\" = {matchId} FOR UPDATE")
            .LoadAsync(cancellationToken);
        var result = await action();
        if (result.IsSuccess) await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private static MatchAuthorityResponse Map(MatchAuthorityBinding match) => new()
    {
        MatchId = match.MatchId,
        FusionSessionName = match.FusionSessionName,
        HostUserId = match.HostUserId,
        MaxPlayers = match.MaxPlayers,
        Status = match.Status,
        LeaseExpiresAtUtc = match.LeaseExpiresAtUtc,
        MapId = match.MapId,
        Difficulty = match.Difficulty,
        ResolutionMode = match.ResolutionMode,
        BalanceSource = match.BalanceSource,
        BalanceProtocolVersion = match.BalanceProtocolVersion,
        BalanceVersion = match.BalanceVersion,
        BalanceHash = match.BalanceHash,
        MatchDataPolicy = match.MatchDataPolicy,
        Players = match.Players.Where(player => match.Status != MatchAuthorityStatus.InMatch || player.Participated).Select(player => new MatchRosterPlayerResponse
        {
            UserId = player.UserId,
            Disconnected = player.DisconnectedAtUtc.HasValue
        }).ToArray()
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
