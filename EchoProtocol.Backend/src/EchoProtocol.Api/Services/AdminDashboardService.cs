using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Admin;
using EchoProtocol.Api.Enums;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Services;

public sealed class AdminDashboardService(AppDbContext db)
{
    public async Task<AdminDashboardResponse> GetAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var activeRooms = db.RoomSessions.AsNoTracking().Where(room => room.ClosedAtUtc == null
            && room.ActivatedAtUtc != null && room.LeaseExpiresAtUtc > now);
        var activeMatches = db.MatchAuthorityBindings.AsNoTracking().Where(match =>
            (match.Status == MatchAuthorityStatus.Lobby || match.Status == MatchAuthorityStatus.InMatch)
            && match.LeaseExpiresAtUtc > now
            && (match.RoomSessionId == null || activeRooms.Any(room => room.Id == match.RoomSessionId
                && room.CurrentMatchId == match.MatchId && room.MatchPublished)));

        var onlinePlayers = await db.MatchPlayerBindings.AsNoTracking()
            .Where(player => player.DisconnectedAtUtc == null && player.KickedAtUtc == null
                && activeMatches.Any(match => match.MatchId == player.MatchId))
            .Select(player => player.UserId).Distinct().CountAsync(ct);
        var activeRoomCount = await activeRooms.CountAsync(ct);
        var activeMatchCount = await activeMatches.CountAsync(match => match.Status == MatchAuthorityStatus.InMatch, ct);
        var registeredPlayers = await db.Users.AsNoTracking().CountAsync(user => user.Role == UserRole.PLAYER, ct);
        var recentRooms = await db.RoomSessions.AsNoTracking()
            .OrderByDescending(room => room.CreatedAtUtc).ThenByDescending(room => room.Id).Take(5)
            .Select(room => new AdminDashboardRoom(room.Id, room.FusionSessionName,
                db.Users.Where(user => user.Id == room.HostUserId).Select(user => user.Username).FirstOrDefault() ?? "",
                room.CurrentMatchId, room.CreatedAtUtc, room.LeaseExpiresAtUtc, room.ActivatedAtUtc, room.ClosedAtUtc))
            .ToArrayAsync(ct);
        return new AdminDashboardResponse(now, onlinePlayers, activeRoomCount, activeMatchCount,
            registeredPlayers, recentRooms);
    }
}
