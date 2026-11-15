using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.MatchAuthority;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class RoomSessionTests
{
    [Fact]
    public async Task RoomSurvivesMatchesAndCloseRevokesItsCurrentMatch()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var host = Guid.NewGuid();
        var admin = Guid.NewGuid();
        db.Users.AddRange(new User { Id = host, Username = "room-host", Email = "room-host@test.invalid", PasswordHash = "hash", Role = UserRole.PLAYER, Status = UserStatus.ACTIVE },
            new User { Id = admin, Username = "room-admin", Email = "room-admin@test.invalid", PasswordHash = "hash", Role = UserRole.ADMIN, Status = UserStatus.ACTIVE });
        await db.SaveChangesAsync();
        var rooms = new RoomSessionService(db);
        var create = new CreateRoomRequest(Guid.NewGuid(), "room-1", "asia");
        var first = await rooms.CreateAsync(host, create, default);
        Assert.True(first.IsSuccess);
        Assert.Equal(first.Data!.RoomInstanceId, (await rooms.CreateAsync(host, create, default)).Data!.RoomInstanceId);
        Assert.False((await rooms.CreateAsync(Guid.NewGuid(), new CreateRoomRequest(Guid.NewGuid(), "room-1", "asia"), default)).IsSuccess);
        var roomId = first.Data.RoomInstanceId;
        Assert.Single((await rooms.ListForAdminAsync(admin, default)).Data!);
        Assert.False((await rooms.ListForAdminAsync(host, default)).IsSuccess);
        Assert.Equal(roomId, (await rooms.GetForAdminAsync(admin, roomId, default)).Data!.RoomInstanceId);
        var requestA = new PrepareRoomMatchRequest(Guid.NewGuid(), 4);
        var matchA = (await rooms.PrepareMatchAsync(host, roomId, requestA, default)).Data!.MatchId;
        Assert.Equal(matchA, (await rooms.PrepareMatchAsync(host, roomId, requestA, default)).Data!.MatchId);
        Assert.False((await rooms.PrepareMatchAsync(host, roomId, new PrepareRoomMatchRequest(Guid.NewGuid(), 4), default)).IsSuccess);
        Assert.False((await rooms.ConfirmPublicationAsync(host, roomId, matchA, default)).IsSuccess);
        Assert.True((await rooms.ActivateAsync(host, roomId, new ActivateRoomRequest("asia"), default)).IsSuccess);
        Assert.True((await rooms.ConfirmPublicationAsync(host, roomId, matchA, default)).IsSuccess);
        var ended = await db.MatchAuthorityBindings.SingleAsync(x => x.MatchId == matchA);
        ended.Status = MatchAuthorityStatus.Ended;
        await db.SaveChangesAsync();
        var matchB = (await rooms.PrepareMatchAsync(host, roomId, new PrepareRoomMatchRequest(Guid.NewGuid(), 4), default)).Data!.MatchId;
        Assert.NotEqual(matchA, matchB);
        Assert.Equal(matchA, (await rooms.PrepareMatchAsync(host, roomId, requestA, default)).Data!.MatchId);
        Assert.False((await rooms.ConfirmPublicationAsync(host, roomId, matchA, default)).IsSuccess);
        var close = new CloseRoomRequest(Guid.NewGuid(), "Admin closed this room");
        Assert.True((await rooms.CloseAsync(admin, roomId, close, default)).IsSuccess);
        Assert.True((await rooms.GetCloseStatusAsync(host, roomId, default)).Data!.Closed);
        Assert.False((await rooms.GetCloseStatusAsync(Guid.NewGuid(), roomId, default)).IsSuccess);
        Assert.False((await rooms.AckCloseAsync(Guid.NewGuid(), roomId, default)).IsSuccess);
        Assert.True((await rooms.AckCloseAsync(host, roomId, default)).IsSuccess);
        Assert.True((await rooms.AckCloseAsync(host, roomId, default)).IsSuccess);
        Assert.NotNull((await rooms.GetForAdminAsync(admin, roomId, default)).Data!.HostExecutedAtUtc);
        Assert.True((await rooms.CloseAsync(admin, roomId, close, default)).IsSuccess);
        Assert.False((await rooms.ActivateAsync(host, roomId, null, default)).IsSuccess);
        Assert.False((await rooms.PrepareMatchAsync(host, roomId, new PrepareRoomMatchRequest(Guid.NewGuid(), 4), default)).IsSuccess);
        Assert.False((await rooms.ConfirmPublicationAsync(host, roomId, matchB, default)).IsSuccess);
        Assert.Equal(MatchAuthorityStatus.Ended, (await db.MatchAuthorityBindings.SingleAsync(x => x.MatchId == matchB)).Status);
    }

    [Fact]
    public async Task ExpiredRoomLeaseReleasesNameAndHostReservation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var host = Guid.NewGuid();
        var rooms = new RoomSessionService(db);
        var first = (await rooms.CreateAsync(host, new CreateRoomRequest(Guid.NewGuid(), "room-reuse", "auto"), default)).Data!;
        await db.RoomSessions.Where(x => x.Id == first.RoomInstanceId)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.LeaseExpiresAtUtc, DateTime.UtcNow.AddSeconds(-1)));
        var second = await rooms.CreateAsync(host, new CreateRoomRequest(Guid.NewGuid(), "room-reuse", "auto"), default);
        Assert.True(second.IsSuccess);
        Assert.NotEqual(first.RoomInstanceId, second.Data!.RoomInstanceId);
        Assert.NotNull((await db.RoomSessions.AsNoTracking().SingleAsync(x => x.Id == first.RoomInstanceId)).ClosedAtUtc);
    }

    [Fact]
    public async Task HostReleaseClosesRoomWithoutAdminAction()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var host = Guid.NewGuid();
        var rooms = new RoomSessionService(db);
        var first = (await rooms.CreateAsync(host, new CreateRoomRequest(Guid.NewGuid(), "host-left", "auto"), default)).Data!;
        Assert.False((await rooms.ReleaseAsync(Guid.NewGuid(), first.RoomInstanceId, default)).IsSuccess);
        Assert.True((await rooms.ReleaseAsync(host, first.RoomInstanceId, default)).IsSuccess);
        Assert.False((await rooms.GetCloseStatusAsync(host, first.RoomInstanceId, default)).Data!.Closed);
        Assert.True((await rooms.ReleaseAsync(host, first.RoomInstanceId, default)).IsSuccess);
        Assert.True((await rooms.CreateAsync(host, new CreateRoomRequest(Guid.NewGuid(), "host-left", "auto"), default)).IsSuccess);
        Assert.Empty(db.AdminModerationActions);
    }
}
