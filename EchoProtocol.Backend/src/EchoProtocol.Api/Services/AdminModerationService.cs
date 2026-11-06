using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Services;

public sealed class AdminModerationService(AppDbContext db)
{
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
