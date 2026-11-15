using EchoProtocol.Api.Data;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class AdminDashboardTests
{
    [Fact]
    public async Task CountsOnlyCurrentLiveRoomRosterAndLabelsStaleRoomsSeparately()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var now = DateTime.UtcNow;
        var host = Guid.NewGuid();
        var connected = Guid.NewGuid();
        var kicked = Guid.NewGuid();
        var stale = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var staleRoomId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var staleMatchId = Guid.NewGuid();
        db.Users.AddRange(new[] { host, connected, kicked, stale }.Select((id, index) => new User
        {
            Id = id, Username = $"dashboard-{index}", Email = $"dashboard-{index}@test.invalid",
            PasswordHash = "hash", Role = UserRole.PLAYER, Status = UserStatus.ACTIVE
        }));
        db.RoomSessions.AddRange(
            new RoomSession { Id = roomId, CreateRequestId = Guid.NewGuid(), FusionSessionName = "live-room",
                Region = "asia", HostUserId = host, CreatedAtUtc = now.AddMinutes(-2), ActivatedAtUtc = now.AddMinutes(-1),
                LeaseExpiresAtUtc = now.AddMinutes(1), CurrentMatchId = matchId, MatchPublished = true },
            new RoomSession { Id = staleRoomId, CreateRequestId = Guid.NewGuid(), FusionSessionName = "stale-room",
                Region = "asia", HostUserId = stale, CreatedAtUtc = now.AddMinutes(-3), ActivatedAtUtc = now.AddMinutes(-2),
                LeaseExpiresAtUtc = now.AddMinutes(-1), CurrentMatchId = staleMatchId, MatchPublished = true });
        db.MatchAuthorityBindings.AddRange(
            new MatchAuthorityBinding { MatchId = matchId, RoomSessionId = roomId, FusionSessionName = "live-room", HostUserId = host,
                MaxPlayers = 4, Status = MatchAuthorityStatus.InMatch, CreatedAtUtc = now, UpdatedAtUtc = now,
                LeaseExpiresAtUtc = now.AddMinutes(1) },
            new MatchAuthorityBinding { MatchId = staleMatchId, RoomSessionId = staleRoomId, FusionSessionName = "stale-room", HostUserId = stale,
                MaxPlayers = 4, Status = MatchAuthorityStatus.InMatch, CreatedAtUtc = now, UpdatedAtUtc = now,
                LeaseExpiresAtUtc = now.AddMinutes(1) });
        db.MatchPlayerBindings.AddRange(new[]
        {
            new MatchPlayerBinding { Id = Guid.NewGuid(), MatchId = matchId, UserId = host, FusionActorNumber = 1, JoinProofId = Guid.NewGuid(), BoundAtUtc = now, LastSeenAtUtc = now },
            new MatchPlayerBinding { Id = Guid.NewGuid(), MatchId = matchId, UserId = connected, FusionActorNumber = 2, JoinProofId = Guid.NewGuid(), BoundAtUtc = now, LastSeenAtUtc = now },
            new MatchPlayerBinding { Id = Guid.NewGuid(), MatchId = matchId, UserId = kicked, FusionActorNumber = 3, JoinProofId = Guid.NewGuid(), BoundAtUtc = now, LastSeenAtUtc = now, KickedAtUtc = now },
            new MatchPlayerBinding { Id = Guid.NewGuid(), MatchId = staleMatchId, UserId = stale, FusionActorNumber = 1, JoinProofId = Guid.NewGuid(), BoundAtUtc = now, LastSeenAtUtc = now }
        });
        await db.SaveChangesAsync();

        var result = await new AdminDashboardService(db).GetAsync(default);
        Assert.Equal(2, result.OnlinePlayersEstimate);
        Assert.Equal(1, result.ActiveRooms);
        Assert.Equal(1, result.ActiveMatches);
        Assert.Equal(4, result.RegisteredPlayers);
        Assert.Equal(2, result.RecentRooms.Count);
        Assert.Equal("live-room", result.RecentRooms[0].FusionSessionName);
    }
}
