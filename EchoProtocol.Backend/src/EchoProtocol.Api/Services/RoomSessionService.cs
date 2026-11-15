using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.MatchAuthority;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EchoProtocol.Api.Services;

public sealed class RoomSessionService(AppDbContext db, IOptions<MatchAuthoritySettings>? settings = null)
{
    private static ServiceResult<T> Deny<T>(string message, string code = ErrorCodes.Conflict) =>
        ServiceResult<T>.Failure(message, code);

    public async Task<ServiceResult<RoomSessionResponse>> CreateAsync(Guid host, CreateRoomRequest request, CancellationToken ct)
    {
        var name = request.FusionSessionName?.Trim() ?? "";
        var region = request.Region?.Trim().ToLowerInvariant() ?? "";
        if (host == Guid.Empty || request.RequestId == Guid.Empty || name.Length is < 1 or > 128 || region.Length is < 1 or > 32)
            return Deny<RoomSessionResponse>("Invalid room request", ErrorCodes.ValidationError);
        var existing = await db.RoomSessions.AsNoTracking().SingleOrDefaultAsync(x => x.CreateRequestId == request.RequestId, ct);
        if (existing is not null)
            return existing.HostUserId == host && existing.FusionSessionName == name && existing.Region == region
                ? ServiceResult<RoomSessionResponse>.Success(Map(existing))
                : Deny<RoomSessionResponse>("Request ID already used");
        var now = DateTime.UtcNow;
        await db.RoomSessions.Where(x => x.ClosedAtUtc == null && x.LeaseExpiresAtUtc <= now
                && (x.HostUserId == host || x.FusionSessionName == name))
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.ClosedAtUtc, now)
                .SetProperty(x => x.CloseReason, "ROOM_LEASE_EXPIRED"), ct);
        var room = new RoomSession { Id = Guid.NewGuid(), CreateRequestId = request.RequestId,
            FusionSessionName = name, Region = region, HostUserId = host, CreatedAtUtc = now,
            LeaseExpiresAtUtc = now.AddSeconds(settings?.Value.LeaseLifetimeSeconds ?? 45) };
        db.RoomSessions.Add(room);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation }
            || db.Database.ProviderName?.Contains("Sqlite", StringComparison.Ordinal) == true)
        {
            db.Entry(room).State = EntityState.Detached;
            existing = await db.RoomSessions.AsNoTracking().SingleOrDefaultAsync(x => x.CreateRequestId == request.RequestId, ct);
            if (existing is not null && existing.HostUserId == host && existing.FusionSessionName == name && existing.Region == region)
                return ServiceResult<RoomSessionResponse>.Success(Map(existing));
            return Deny<RoomSessionResponse>("Room name or Host already reserved");
        }
        return ServiceResult<RoomSessionResponse>.Success(Map(room));
    }

    public async Task<ServiceResult<RoomSessionResponse>> ActivateAsync(Guid host, Guid id, ActivateRoomRequest? request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var room = await LockRoom(id, ct);
        if (room is null || room.HostUserId != host || room.ClosedAtUtc is not null)
            return Deny<RoomSessionResponse>("Room authority unavailable", ErrorCodes.MatchAuthorityForbidden);
        if (room.LeaseExpiresAtUtc <= DateTime.UtcNow)
            return Deny<RoomSessionResponse>("Room lease expired", ErrorCodes.MatchLeaseExpired);
        var actual = request?.ActualRegion?.Trim().ToLowerInvariant();
        if (actual?.Length > 32 || room.ActualRegion is not null && actual is not null && room.ActualRegion != actual)
            return Deny<RoomSessionResponse>("Actual region conflicts with activation");
        if (!string.IsNullOrEmpty(actual)) room.ActualRegion = actual;
        room.ActivatedAtUtc ??= DateTime.UtcNow;
        room.LeaseExpiresAtUtc = DateTime.UtcNow.AddSeconds(settings?.Value.LeaseLifetimeSeconds ?? 45);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<RoomSessionResponse>.Success(Map(room));
    }

    public async Task<ServiceResult<RoomSessionResponse>> RenewLeaseAsync(Guid host, Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var room = await LockRoom(id, ct);
        if (room is null || room.HostUserId != host || room.ClosedAtUtc is not null
            || room.ActivatedAtUtc is null || room.LeaseExpiresAtUtc <= DateTime.UtcNow)
            return Deny<RoomSessionResponse>("Room authority unavailable", ErrorCodes.MatchAuthorityForbidden);
        room.LeaseExpiresAtUtc = DateTime.UtcNow.AddSeconds(settings?.Value.LeaseLifetimeSeconds ?? 45);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<RoomSessionResponse>.Success(Map(room));
    }

    public async Task<ServiceResult<RoomSessionResponse>> ReleaseAsync(Guid host, Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var room = await LockRoom(id, ct);
        if (room is null || room.HostUserId != host)
            return Deny<RoomSessionResponse>("Room authority unavailable", ErrorCodes.MatchAuthorityForbidden);
        room.ClosedAtUtc ??= DateTime.UtcNow;
        room.CloseReason ??= "HOST_LEFT";
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<RoomSessionResponse>.Success(Map(room));
    }

    public async Task<ServiceResult<RoomSessionResponse>> SyncHostAfterTakeoverAsync(Guid host, Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var room = await LockRoom(id, ct);
        if (room is null || room.ClosedAtUtc is not null || room.CurrentMatchId is not Guid matchId)
            return Deny<RoomSessionResponse>("Room authority unavailable", ErrorCodes.MatchAuthorityForbidden);
        var match = await db.MatchAuthorityBindings.SingleAsync(x => x.MatchId == matchId, ct);
        if (match.HostUserId != host || match.Status == MatchAuthorityStatus.Ended || match.LeaseExpiresAtUtc <= DateTime.UtcNow)
            return Deny<RoomSessionResponse>("Current match Host authority required", ErrorCodes.MatchAuthorityForbidden);
        room.HostUserId = host;
        room.LeaseExpiresAtUtc = DateTime.UtcNow.AddSeconds(settings?.Value.LeaseLifetimeSeconds ?? 45);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<RoomSessionResponse>.Success(Map(room));
    }

    public async Task<ServiceResult<MatchAuthorityResponse>> PrepareMatchAsync(Guid host, Guid id, PrepareRoomMatchRequest request, CancellationToken ct)
    {
        if (request.RequestId == Guid.Empty || request.MaxPlayers is < 2 or > 4)
            return Deny<MatchAuthorityResponse>("Invalid match request", ErrorCodes.ValidationError);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var room = await LockRoom(id, ct);
        if (room is null || room.HostUserId != host || room.ClosedAtUtc is not null)
            return Deny<MatchAuthorityResponse>("Room authority unavailable", ErrorCodes.MatchAuthorityForbidden);
        var prior = await db.MatchAuthorityBindings.AsNoTracking().SingleOrDefaultAsync(
            x => x.RoomSessionId == id && x.RoomMatchRequestId == request.RequestId, ct);
        if (prior is not null)
            return prior.MaxPlayers == request.MaxPlayers
                ? ServiceResult<MatchAuthorityResponse>.Success(ToMatch(prior))
                : Deny<MatchAuthorityResponse>("Request differs from existing match");
        if (room.LeaseExpiresAtUtc <= DateTime.UtcNow)
            return Deny<MatchAuthorityResponse>("Room lease expired", ErrorCodes.MatchLeaseExpired);
        if (room.CurrentMatchId is Guid currentId)
        {
            var current = await db.MatchAuthorityBindings.SingleAsync(x => x.MatchId == currentId, ct);
            if (room.CurrentMatchRequestId == request.RequestId)
                return current.MaxPlayers == request.MaxPlayers
                    ? ServiceResult<MatchAuthorityResponse>.Success(ToMatch(current))
                    : Deny<MatchAuthorityResponse>("Request differs from existing match");
            if (room.ActivatedAtUtc is null)
                return Deny<MatchAuthorityResponse>("First room publication is unresolved");
            if (!room.MatchPublished || current.Status != MatchAuthorityStatus.Ended)
                return Deny<MatchAuthorityResponse>("Current match publication or result is unresolved");
        }
        var now = DateTime.UtcNow;
        var match = new MatchAuthorityBinding { MatchId = Guid.NewGuid(), RoomSessionId = id, RoomMatchRequestId = request.RequestId,
            FusionSessionName = room.FusionSessionName, HostUserId = host, MaxPlayers = request.MaxPlayers,
            Status = MatchAuthorityStatus.Lobby, LeaseExpiresAtUtc = now.AddSeconds(settings?.Value.LeaseLifetimeSeconds ?? 45), CreatedAtUtc = now, UpdatedAtUtc = now };
        db.MatchAuthorityBindings.Add(match);
        room.CurrentMatchId = match.MatchId;
        room.CurrentMatchRequestId = request.RequestId;
        room.MatchPublished = false;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<MatchAuthorityResponse>.Success(ToMatch(match));
    }

    public async Task<ServiceResult<RoomSessionResponse>> ConfirmPublicationAsync(Guid host, Guid id, Guid matchId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var room = await LockRoom(id, ct);
        if (room is null || room.HostUserId != host || room.ClosedAtUtc is not null
            || room.ActivatedAtUtc is null || room.LeaseExpiresAtUtc <= DateTime.UtcNow || room.CurrentMatchId != matchId)
            return Deny<RoomSessionResponse>("Stale publication acknowledgement", ErrorCodes.MatchAuthorityForbidden);
        room.MatchPublished = true;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<RoomSessionResponse>.Success(Map(room));
    }

    public async Task<ServiceResult<RoomSessionResponse>> CloseAsync(Guid admin, Guid id, CloseRoomRequest request, CancellationToken ct)
    {
        var reason = request.Reason?.Trim() ?? "";
        if (request.ActionId == Guid.Empty || reason.Length is < 10 or > 500)
            return Deny<RoomSessionResponse>("Invalid close request", ErrorCodes.ValidationError);
        if (!await db.Users.AnyAsync(x => x.Id == admin && x.Role == UserRole.ADMIN && x.Status == UserStatus.ACTIVE, ct))
            return Deny<RoomSessionResponse>("Admin access required", ErrorCodes.MatchAuthorityForbidden);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var room = await LockRoom(id, ct);
        if (room is null) return Deny<RoomSessionResponse>("Room not found", ErrorCodes.NotFound);
        var action = await db.AdminModerationActions.SingleOrDefaultAsync(x => x.TargetId == id && x.Action == "CLOSE_ROOM", ct);
        if (action is not null)
            return action.Id == request.ActionId && action.AdminUserId == admin && action.Reason == reason
                ? ServiceResult<RoomSessionResponse>.Success(Map(room))
                : Deny<RoomSessionResponse>("Room already closed");
        var now = DateTime.UtcNow;
        room.ClosedAtUtc = now;
        room.CloseReason = reason;
        if (room.CurrentMatchId is Guid matchId)
        {
            var match = await db.MatchAuthorityBindings.SingleAsync(x => x.MatchId == matchId, ct);
            if (match.Status != MatchAuthorityStatus.Ended)
            {
                match.Status = MatchAuthorityStatus.Ended;
                match.EndedAtUtc = now;
                match.UpdatedAtUtc = now;
            }
        }
        db.AdminModerationActions.Add(new AdminModerationAction { Id = request.ActionId, TargetId = id,
            Action = "CLOSE_ROOM", AdminUserId = admin, Reason = reason, CreatedAtUtc = now });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<RoomSessionResponse>.Success(Map(room));
    }

    public async Task<ServiceResult<RoomSessionResponse>> GetAsync(Guid user, Guid id, CancellationToken ct)
    {
        var room = await db.RoomSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (room is null || room.HostUserId != user)
            return Deny<RoomSessionResponse>("Room unavailable", ErrorCodes.MatchAuthorityForbidden);
        return ServiceResult<RoomSessionResponse>.Success(Map(room));
    }

    public async Task<ServiceResult<RoomCloseStatusResponse>> GetCloseStatusAsync(Guid user, Guid id, CancellationToken ct)
    {
        var room = await db.RoomSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (room is null || user == Guid.Empty || room.HostUserId != user
            && !await db.MatchPlayerBindings.AsNoTracking().AnyAsync(player => player.UserId == user
                && db.MatchAuthorityBindings.Any(match => match.MatchId == player.MatchId
                    && match.RoomSessionId == id), ct))
            return Deny<RoomCloseStatusResponse>("Room unavailable", ErrorCodes.MatchAuthorityForbidden);
        var action = await db.AdminModerationActions.AsNoTracking().SingleOrDefaultAsync(
            x => x.TargetId == id && x.Action == "CLOSE_ROOM", ct);
        return ServiceResult<RoomCloseStatusResponse>.Success(
            new RoomCloseStatusResponse(action is not null, action?.Reason));
    }

    public async Task<ServiceResult<RoomCloseStatusResponse>> AckCloseAsync(Guid host, Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var room = await LockRoom(id, ct);
        if (room is null || room.HostUserId != host || room.ClosedAtUtc is null)
            return Deny<RoomCloseStatusResponse>("Room authority unavailable", ErrorCodes.MatchAuthorityForbidden);
        var action = await db.AdminModerationActions.SingleOrDefaultAsync(
            x => x.TargetId == id && x.Action == "CLOSE_ROOM", ct);
        if (action is null) return Deny<RoomCloseStatusResponse>("Admin close not found", ErrorCodes.NotFound);
        action.HostExecutedAtUtc ??= DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<RoomCloseStatusResponse>.Success(new RoomCloseStatusResponse(true, action.Reason));
    }

    public async Task<ServiceResult<RoomSessionResponse>> GetForMatchMemberAsync(Guid user, Guid matchId, CancellationToken ct)
    {
        var match = await db.MatchAuthorityBindings.AsNoTracking().SingleOrDefaultAsync(x => x.MatchId == matchId, ct);
        if (match?.RoomSessionId is not Guid id ||
            !(match.HostUserId == user || await db.MatchPlayerBindings.AsNoTracking().AnyAsync(x => x.MatchId == matchId && x.UserId == user, ct)))
            return Deny<RoomSessionResponse>("Room unavailable", ErrorCodes.MatchAuthorityForbidden);
        var room = await db.RoomSessions.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        return ServiceResult<RoomSessionResponse>.Success(Map(room));
    }

    public async Task<ServiceResult<IReadOnlyList<RoomSessionResponse>>> ListForAdminAsync(Guid admin, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == admin && x.Role == UserRole.ADMIN && x.Status == UserStatus.ACTIVE, ct))
            return Deny<IReadOnlyList<RoomSessionResponse>>("Admin access required", ErrorCodes.MatchAuthorityForbidden);
        var rooms = await db.RoomSessions.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc)
            .Take(100).ToArrayAsync(ct);
        return ServiceResult<IReadOnlyList<RoomSessionResponse>>.Success(rooms.Select(Map).ToArray());
    }

    public async Task<ServiceResult<RoomSessionResponse>> GetForAdminAsync(Guid admin, Guid id, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == admin && x.Role == UserRole.ADMIN && x.Status == UserStatus.ACTIVE, ct))
            return Deny<RoomSessionResponse>("Admin access required", ErrorCodes.MatchAuthorityForbidden);
        var room = await db.RoomSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (room is null) return Deny<RoomSessionResponse>("Room not found", ErrorCodes.NotFound);
        var action = await db.AdminModerationActions.AsNoTracking().SingleOrDefaultAsync(
            x => x.TargetId == id && x.Action == "CLOSE_ROOM", ct);
        return ServiceResult<RoomSessionResponse>.Success(Map(room) with { HostExecutedAtUtc = action?.HostExecutedAtUtc });
    }

    private Task<RoomSession?> LockRoom(Guid id, CancellationToken ct) => db.Database.IsNpgsql()
        ? db.RoomSessions.FromSqlInterpolated($"SELECT * FROM \"RoomSessions\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct)
        : db.RoomSessions.SingleOrDefaultAsync(x => x.Id == id, ct);

    private static RoomSessionResponse Map(RoomSession x) => new(x.Id, x.FusionSessionName, x.Region, x.ActualRegion,
        x.HostUserId, x.CreatedAtUtc, x.LeaseExpiresAtUtc, x.ActivatedAtUtc, x.CurrentMatchId, x.MatchPublished, x.ClosedAtUtc, x.CloseReason);

    private static MatchAuthorityResponse ToMatch(MatchAuthorityBinding x) => new() { MatchId = x.MatchId,
        FusionSessionName = x.FusionSessionName, HostUserId = x.HostUserId, MaxPlayers = x.MaxPlayers,
        Status = x.Status, LeaseExpiresAtUtc = x.LeaseExpiresAtUtc };
}
