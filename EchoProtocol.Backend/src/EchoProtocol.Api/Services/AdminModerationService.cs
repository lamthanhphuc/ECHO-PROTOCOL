using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.MatchAuthority;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Services;

public sealed class AdminModerationService(AppDbContext db)
{
    public async Task<ServiceResult<IReadOnlyList<KickDecisionResponse>>> GetKicksForHostAsync(
        Guid hostUserId, Guid matchId, CancellationToken ct)
    {
        var match = await db.MatchAuthorityBindings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.MatchId == matchId, ct);
        if (match is null || match.HostUserId != hostUserId
            || match.Status is not (MatchAuthorityStatus.Lobby or MatchAuthorityStatus.InMatch)
            || match.LeaseExpiresAtUtc <= DateTime.UtcNow)
            return ServiceResult<IReadOnlyList<KickDecisionResponse>>.Failure("Host authority required", ErrorCodes.MatchAuthorityForbidden);
        var decisions = await db.AdminModerationActions.AsNoTracking()
            .Where(item => item.TargetId == matchId && item.Action == "KICK_PLAYER" && item.TargetUserId != null)
            .OrderBy(item => item.CreatedAtUtc)
            .Select(item => new KickDecisionResponse(item.Id, item.TargetUserId!.Value, item.Reason, item.CreatedAtUtc, item.HostExecutedAtUtc))
            .ToArrayAsync(ct);
        return ServiceResult<IReadOnlyList<KickDecisionResponse>>.Success(decisions);
    }

    public async Task<ServiceResult<KickStatusResponse>> GetKickStatusAsync(Guid userId, Guid matchId, CancellationToken ct)
    {
        if (userId == Guid.Empty || matchId == Guid.Empty)
            return ServiceResult<KickStatusResponse>.Failure("Invalid identity", ErrorCodes.ValidationError);
        var action = await db.AdminModerationActions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.TargetId == matchId && item.TargetUserId == userId && item.Action == "KICK_PLAYER", ct);
        return ServiceResult<KickStatusResponse>.Success(action is null
            ? new KickStatusResponse(false, null, null, null)
            : new KickStatusResponse(true, action.Id, action.Reason, action.CreatedAtUtc));
    }

    public async Task<ServiceResult<KickDecisionResponse>> AckKickAsync(Guid hostUserId, Guid matchId, Guid actionId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var match = db.Database.IsNpgsql()
            ? await db.MatchAuthorityBindings.FromSqlInterpolated($"SELECT * FROM \"MatchAuthorityBindings\" WHERE \"MatchId\" = {matchId} FOR UPDATE").SingleOrDefaultAsync(ct)
            : await db.MatchAuthorityBindings.SingleOrDefaultAsync(item => item.MatchId == matchId, ct);
        var now = DateTime.UtcNow;
        if (match is null || match.HostUserId != hostUserId
            || !(match.Status is (MatchAuthorityStatus.Lobby or MatchAuthorityStatus.InMatch) && match.LeaseExpiresAtUtc > now
                || match.Status == MatchAuthorityStatus.Ended && match.EndedAtUtc >= now.AddMinutes(-10)))
            return ServiceResult<KickDecisionResponse>.Failure("Host authority required", ErrorCodes.MatchAuthorityForbidden);
        var action = await db.AdminModerationActions.SingleOrDefaultAsync(
            item => item.Id == actionId && item.TargetId == matchId && item.Action == "KICK_PLAYER" && item.TargetUserId != null, ct);
        if (action is null)
            return ServiceResult<KickDecisionResponse>.Failure("Kick decision not found", ErrorCodes.NotFound);
        if (action.HostExecutedAtUtc is null)
        {
            action.HostExecutedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return ServiceResult<KickDecisionResponse>.Success(new(action.Id, action.TargetUserId!.Value, action.Reason, action.CreatedAtUtc, action.HostExecutedAtUtc));
    }

    public async Task<ServiceResult<AdminModerationAction>> KickPlayerAsync(
        Guid adminId, Guid matchId, Guid targetUserId, Guid actionId, string reason, CancellationToken ct)
    {
        reason = reason?.Trim() ?? "";
        if (adminId == Guid.Empty || matchId == Guid.Empty || targetUserId == Guid.Empty
            || actionId == Guid.Empty || reason.Length is < 10 or > 500)
            return ServiceResult<AdminModerationAction>.Failure("Invalid kick request", ErrorCodes.ValidationError);
        if (!await db.Users.AnyAsync(user => user.Id == adminId && user.Role == UserRole.ADMIN && user.Status == UserStatus.ACTIVE, ct))
            return ServiceResult<AdminModerationAction>.Failure("Admin access required", ErrorCodes.MatchAuthorityForbidden);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var match = db.Database.IsNpgsql()
            ? await db.MatchAuthorityBindings.FromSqlInterpolated($"SELECT * FROM \"MatchAuthorityBindings\" WHERE \"MatchId\" = {matchId} FOR UPDATE").SingleOrDefaultAsync(ct)
            : await db.MatchAuthorityBindings.SingleOrDefaultAsync(item => item.MatchId == matchId, ct);
        if (match is null)
            return ServiceResult<AdminModerationAction>.Failure("Match not found", ErrorCodes.NotFound);

        var existing = await db.AdminModerationActions.SingleOrDefaultAsync(
            item => item.TargetId == matchId && item.TargetUserId == targetUserId && item.Action == "KICK_PLAYER", ct);
        if (existing is not null)
            return existing.Id == actionId && existing.AdminUserId == adminId && existing.Reason == reason
                ? ServiceResult<AdminModerationAction>.Success(existing)
                : ServiceResult<AdminModerationAction>.Failure("Player already kicked", ErrorCodes.Conflict);
        if (match.Status is not (MatchAuthorityStatus.Lobby or MatchAuthorityStatus.InMatch)
            || match.LeaseExpiresAtUtc <= DateTime.UtcNow
            || match.RoomSessionId is Guid roomId && await db.RoomSessions.AnyAsync(room => room.Id == roomId && room.ClosedAtUtc != null, ct))
            return ServiceResult<AdminModerationAction>.Failure("Room is not active", ErrorCodes.Conflict);

        var player = await db.MatchPlayerBindings.SingleOrDefaultAsync(
            item => item.MatchId == matchId && item.UserId == targetUserId
                && (match.Status == MatchAuthorityStatus.Lobby && item.DisconnectedAtUtc == null
                    || match.Status == MatchAuthorityStatus.InMatch && item.Participated), ct);
        if (player is null)
            return ServiceResult<AdminModerationAction>.Failure("Player is not connected in the current roster", ErrorCodes.NotFound);
        if (match.HostUserId == targetUserId && !await db.MatchPlayerBindings.AnyAsync(
                item => item.MatchId == matchId && item.UserId != targetUserId
                    && item.DisconnectedAtUtc == null && item.KickedAtUtc == null, ct))
            return ServiceResult<AdminModerationAction>.Failure("No eligible player remains to receive Host authority", ErrorCodes.Conflict);
        if (match.HostUserId != targetUserId
            && await db.MatchPlayerBindings.AnyAsync(item => item.MatchId == matchId && item.UserId == match.HostUserId && item.KickedAtUtc != null, ct)
            && !await db.MatchPlayerBindings.AnyAsync(item => item.MatchId == matchId && item.UserId != targetUserId
                && item.UserId != match.HostUserId && item.DisconnectedAtUtc == null && item.KickedAtUtc == null, ct))
            return ServiceResult<AdminModerationAction>.Failure("No eligible player remains to receive Host authority", ErrorCodes.Conflict);

        var now = DateTime.UtcNow;
        player.KickedAtUtc = now;
        var entry = new AdminModerationAction
        {
            Id = actionId, TargetId = matchId, TargetUserId = targetUserId,
            Action = "KICK_PLAYER", AdminUserId = adminId, Reason = reason, CreatedAtUtc = now
        };
        db.AdminModerationActions.Add(entry);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult<AdminModerationAction>.Success(entry);
    }

    public async Task<ServiceResult<AdminModerationAction>> ApplyAsync(
        Guid adminId, Guid targetId, string action, string reason, CancellationToken ct)
    {
        reason = reason?.Trim() ?? "";
        if (adminId == Guid.Empty || targetId == Guid.Empty || reason.Length is < 10 or > 500)
            return ServiceResult<AdminModerationAction>.Failure("Reason must contain 10 to 500 characters", ErrorCodes.ValidationError);
        if (action is not ("FLAG_MATCH" or "CLEAR_MATCH" or "STOP_MATCH" or "FLAG_USER" or "CLEAR_USER" or "LOCK_USER" or "UNLOCK_USER"))
            return ServiceResult<AdminModerationAction>.Failure("Invalid moderation action", ErrorCodes.ValidationError);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;
        if (action.EndsWith("_USER", StringComparison.Ordinal))
        {
            var user = db.Database.IsNpgsql()
                ? await db.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {targetId} FOR UPDATE").SingleOrDefaultAsync(ct)
                : await db.Users.SingleOrDefaultAsync(item => item.Id == targetId, ct);
            if (user is null) return ServiceResult<AdminModerationAction>.Failure("User not found", ErrorCodes.NotFound);
            if (user.Role != UserRole.PLAYER || user.Id == adminId)
                return ServiceResult<AdminModerationAction>.Failure("Only player accounts can be moderated", ErrorCodes.ValidationError);
            if (action is "LOCK_USER" or "UNLOCK_USER")
            {
                var requestedStatus = action == "LOCK_USER" ? UserStatus.LOCKED : UserStatus.ACTIVE;
                if (user.Status == requestedStatus)
                    return ServiceResult<AdminModerationAction>.Failure("Account already has this status", ErrorCodes.Conflict);
                user.Status = requestedStatus;
                user.UpdatedAt = now;
                if (requestedStatus == UserStatus.LOCKED)
                    await db.RefreshSessions.Where(item => item.UserId == targetId && item.RevokedAtUtc == null)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.RevokedAtUtc, (DateTime?)now), ct);
            }
            else if (!await CanChangeFlagAsync(targetId, action, ct))
                return ServiceResult<AdminModerationAction>.Failure("Account already has this review status", ErrorCodes.Conflict);
        }
        else
        {
            var match = db.Database.IsNpgsql()
                ? await db.MatchAuthorityBindings.FromSqlInterpolated($"SELECT * FROM \"MatchAuthorityBindings\" WHERE \"MatchId\" = {targetId} FOR UPDATE").SingleOrDefaultAsync(ct)
                : await db.MatchAuthorityBindings.SingleOrDefaultAsync(item => item.MatchId == targetId, ct);
            if (match is null) return ServiceResult<AdminModerationAction>.Failure("Match not found", ErrorCodes.NotFound);
            if (action == "STOP_MATCH")
            {
                if (match.Status == MatchAuthorityStatus.Ended)
                    return ServiceResult<AdminModerationAction>.Failure("Match already ended", ErrorCodes.Conflict);
                match.Status = MatchAuthorityStatus.Ended;
                match.EndedAtUtc = now;
                match.UpdatedAtUtc = now;
                match.LeaseExpiresAtUtc = now;
            }
            else
            {
                if (!await CanChangeFlagAsync(targetId, action, ct))
                    return ServiceResult<AdminModerationAction>.Failure("Match already has this review status", ErrorCodes.Conflict);
            }
        }

        var entry = new AdminModerationAction
        {
            Id = Guid.NewGuid(), TargetId = targetId, Action = action,
            AdminUserId = adminId, Reason = reason, CreatedAtUtc = now
        };
        db.AdminModerationActions.Add(entry);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult<AdminModerationAction>.Success(entry);
    }

    private async Task<bool> CanChangeFlagAsync(Guid targetId, string action, CancellationToken ct)
    {
        var isUser = action.EndsWith("_USER", StringComparison.Ordinal);
        var flag = isUser ? "FLAG_USER" : "FLAG_MATCH";
        var clear = isUser ? "CLEAR_USER" : "CLEAR_MATCH";
        var lastFlag = await db.AdminModerationActions.AsNoTracking()
            .Where(item => item.TargetId == targetId && (item.Action == flag || item.Action == clear))
            .OrderByDescending(item => item.CreatedAtUtc).ThenByDescending(item => item.Id)
            .Select(item => item.Action).FirstOrDefaultAsync(ct);
        return (action == flag) != (lastFlag == flag);
    }

    public async Task<ServiceResult<IReadOnlyList<AdminModerationAction>>> HistoryAsync(Guid targetId, CancellationToken ct)
    {
        var entries = await db.AdminModerationActions.AsNoTracking()
            .Where(item => item.TargetId == targetId)
            .OrderByDescending(item => item.CreatedAtUtc).ThenByDescending(item => item.Id)
            .Take(50).ToArrayAsync(ct);
        return ServiceResult<IReadOnlyList<AdminModerationAction>>.Success(entries);
    }
}
