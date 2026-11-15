using EchoProtocol.Api.Data;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.DTOs.MatchAuthority;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class AdminModerationTests
{
    [Fact]
    public async Task LobbyKickCanTargetHostOnlyWithSuccessorAndBlocksFutureRoomProof()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var admin = Guid.NewGuid();
        var host = Guid.NewGuid();
        var successor = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.Users.AddRange(
            new User { Id = admin, Username = "admin-room", Email = "admin-room@test.invalid", PasswordHash = "hash", Role = UserRole.ADMIN, Status = UserStatus.ACTIVE },
            new User { Id = host, Username = "host-room", Email = "host-room@test.invalid", PasswordHash = "hash", Role = UserRole.PLAYER, Status = UserStatus.ACTIVE },
            new User { Id = successor, Username = "successor-room", Email = "successor-room@test.invalid", PasswordHash = "hash", Role = UserRole.PLAYER, Status = UserStatus.ACTIVE });
        db.RoomSessions.Add(new RoomSession { Id = roomId, CreateRequestId = Guid.NewGuid(), FusionSessionName = "room-kick", Region = "auto", HostUserId = host,
            CreatedAtUtc = now, ActivatedAtUtc = now, LeaseExpiresAtUtc = now.AddMinutes(1), CurrentMatchId = matchId, MatchPublished = true });
        db.MatchAuthorityBindings.Add(new MatchAuthorityBinding { MatchId = matchId, RoomSessionId = roomId, HostUserId = host,
            FusionSessionName = "room-kick", MaxPlayers = 4, Status = MatchAuthorityStatus.Lobby,
            CreatedAtUtc = now, UpdatedAtUtc = now, LeaseExpiresAtUtc = now.AddMinutes(1) });
        db.MatchPlayerBindings.AddRange(
            new MatchPlayerBinding { Id = Guid.NewGuid(), MatchId = matchId, UserId = host, FusionActorNumber = 1, JoinProofId = Guid.NewGuid(), BoundAtUtc = now, LastSeenAtUtc = now },
            new MatchPlayerBinding { Id = Guid.NewGuid(), MatchId = matchId, UserId = successor, FusionActorNumber = 2, JoinProofId = Guid.NewGuid(), BoundAtUtc = now, LastSeenAtUtc = now });
        await db.SaveChangesAsync();
        var moderation = new AdminModerationService(db);
        var settings = Options.Create(new MatchAuthoritySettings { ProofSigningKey = "test-proof-key-that-is-at-least-32-bytes", JoinProofLifetimeSeconds = 120 });
        var authority = new MatchAuthorityService(db, new MatchJoinProofService(settings, TimeProvider.System), settings, TimeProvider.System);

        Assert.True((await moderation.KickPlayerAsync(admin, matchId, host, Guid.NewGuid(), "Host removed by administrator", default)).IsSuccess);
        Assert.Single((await moderation.GetKicksForHostAsync(host, matchId, default)).Data!);
        Assert.False((await moderation.KickPlayerAsync(admin, matchId, successor, Guid.NewGuid(), "Remove remaining player", default)).IsSuccess);
        Assert.False((await authority.IssueJoinProofAsync(host, matchId,
            new IssueJoinProofRequest { FusionSessionName = "room-kick", FusionActorNumber = 1 }, default)).IsSuccess);
        Assert.True((await authority.RelinquishAsync(host, matchId, default)).IsSuccess);
        var takeover = await authority.TakeoverAsync(successor, matchId,
            new TakeoverMatchAuthorityRequest { FusionSessionName = "room-kick", FusionActorNumber = 2 }, default);
        Assert.True(takeover.IsSuccess);
        Assert.True((await new RoomSessionService(db).SyncHostAfterTakeoverAsync(successor, roomId, default)).IsSuccess);
        Assert.Equal(successor, db.RoomSessions.Single().HostUserId);
        var nextMatchId = Guid.NewGuid();
        db.MatchAuthorityBindings.Add(new MatchAuthorityBinding { MatchId = nextMatchId, RoomSessionId = roomId, HostUserId = successor,
            FusionSessionName = "room-kick", MaxPlayers = 4, Status = MatchAuthorityStatus.Lobby,
            CreatedAtUtc = now, UpdatedAtUtc = now, LeaseExpiresAtUtc = now.AddMinutes(1) });
        db.RoomSessions.Single().CurrentMatchId = nextMatchId;
        await db.SaveChangesAsync();
        Assert.False((await authority.IssueJoinProofAsync(host, nextMatchId,
            new IssueJoinProofRequest { FusionSessionName = "room-kick", FusionActorNumber = 1 }, default)).IsSuccess);
    }

    [Fact]
    public async Task KickRevokesDisconnectedRosterMemberAndPreviouslyIssuedProof()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var adminId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var actionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.Users.AddRange(
            new User { Id = adminId, Username = "admin-kick", Email = "admin-kick@test.invalid", PasswordHash = "hash", Role = UserRole.ADMIN, Status = UserStatus.ACTIVE },
            new User { Id = hostId, Username = "host-kick", Email = "host-kick@test.invalid", PasswordHash = "hash", Role = UserRole.PLAYER, Status = UserStatus.ACTIVE },
            new User { Id = playerId, Username = "player-kick", Email = "player-kick@test.invalid", PasswordHash = "hash", Role = UserRole.PLAYER, Status = UserStatus.ACTIVE });
        db.MatchAuthorityBindings.Add(new MatchAuthorityBinding
        {
            MatchId = matchId, HostUserId = hostId, FusionSessionName = "kick-test", MaxPlayers = 4,
            Status = MatchAuthorityStatus.InMatch, LeaseExpiresAtUtc = now.AddMinutes(1),
            CreatedAtUtc = now, UpdatedAtUtc = now, StartedAtUtc = now
        });
        db.MatchPlayerBindings.Add(new MatchPlayerBinding
        {
            Id = Guid.NewGuid(), MatchId = matchId, UserId = playerId, FusionActorNumber = 2,
            JoinProofId = Guid.NewGuid(), Participated = true, BoundAtUtc = now,
            LastSeenAtUtc = now, DisconnectedAtUtc = now
        });
        await db.SaveChangesAsync();
        var settings = Options.Create(new MatchAuthoritySettings
        {
            ProofSigningKey = "test-proof-key-that-is-at-least-32-bytes", JoinProofLifetimeSeconds = 120
        });
        var authority = new MatchAuthorityService(db, new MatchJoinProofService(settings, TimeProvider.System), settings, TimeProvider.System);
        var oldProof = await authority.IssueJoinProofAsync(playerId, matchId,
            new IssueJoinProofRequest { FusionSessionName = "kick-test", FusionActorNumber = 3 }, default);
        Assert.True(oldProof.IsSuccess);

        var moderation = new AdminModerationService(db);
        Assert.False((await moderation.KickPlayerAsync(playerId, matchId, hostId, Guid.NewGuid(), "Unauthorized attempt", default)).IsSuccess);
        Assert.False((await moderation.KickPlayerAsync(adminId, matchId, hostId, Guid.NewGuid(), "Host cannot be kicked", default)).IsSuccess);
        var kick = await moderation.KickPlayerAsync(adminId, matchId, playerId, actionId, "Disruptive behavior in match", default);
        Assert.True(kick.IsSuccess);
        Assert.Single((await moderation.GetKicksForHostAsync(hostId, matchId, default)).Data!);
        var self = await moderation.GetKickStatusAsync(playerId, matchId, default);
        Assert.True(self.Data!.Kicked);
        Assert.Equal(actionId, self.Data.ActionId);
        Assert.False((await moderation.GetKickStatusAsync(hostId, matchId, default)).Data!.Kicked);
        Assert.False((await moderation.AckKickAsync(playerId, matchId, actionId, default)).IsSuccess);
        var ack = await moderation.AckKickAsync(hostId, matchId, actionId, default);
        Assert.True(ack.IsSuccess);
        Assert.NotNull(ack.Data!.HostExecutedAtUtc);
        Assert.Equal(ack.Data.HostExecutedAtUtc, (await moderation.AckKickAsync(hostId, matchId, actionId, default)).Data!.HostExecutedAtUtc);
        var kickedMatch = await db.MatchAuthorityBindings.SingleAsync();
        kickedMatch.Status = MatchAuthorityStatus.Ended;
        kickedMatch.EndedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        Assert.True((await moderation.AckKickAsync(hostId, matchId, actionId, default)).IsSuccess);
        Assert.False((await moderation.GetKicksForHostAsync(playerId, matchId, default)).IsSuccess);
        Assert.Equal(actionId, (await moderation.KickPlayerAsync(adminId, matchId, playerId, actionId, "Disruptive behavior in match", default)).Data!.Id);
        Assert.False((await moderation.KickPlayerAsync(adminId, matchId, playerId, Guid.NewGuid(), "Disruptive behavior in match", default)).IsSuccess);
        Assert.NotNull((await db.MatchPlayerBindings.SingleAsync()).DisconnectedAtUtc);
        Assert.NotNull((await db.MatchPlayerBindings.SingleAsync()).KickedAtUtc);
        Assert.False((await authority.IssueJoinProofAsync(playerId, matchId,
            new IssueJoinProofRequest { FusionSessionName = "kick-test", FusionActorNumber = 3 }, default)).IsSuccess);
        Assert.False((await authority.BindPlayerAsync(hostId, matchId,
            new BindMatchPlayerRequest { FusionActorNumber = 3, JoinProof = oldProof.Data!.Proof }, default)).IsSuccess);
        Assert.Single(await db.AdminModerationActions.Where(item => item.Action == "KICK_PLAYER").ToArrayAsync());
    }

    [Fact]
    public async Task AdminCanFlagStopAndLockWithAuditButCannotLockAnotherAdmin()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var adminId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        db.Users.AddRange(
            new User { Id = adminId, Username = "admin", Email = "admin@test.invalid", PasswordHash = "hash", Role = UserRole.ADMIN, Status = UserStatus.ACTIVE },
            new User { Id = playerId, Username = "player", Email = "player@test.invalid", PasswordHash = "hash", Role = UserRole.PLAYER, Status = UserStatus.ACTIVE });
        db.MatchAuthorityBindings.Add(new MatchAuthorityBinding
        {
            MatchId = matchId, HostUserId = playerId, FusionSessionName = "moderation-test", MaxPlayers = 4,
            Status = MatchAuthorityStatus.InMatch, LeaseExpiresAtUtc = DateTime.UtcNow.AddMinutes(1),
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow, StartedAtUtc = DateTime.UtcNow
        });
        db.RefreshSessions.Add(new RefreshSession
        {
            Id = Guid.NewGuid(), UserId = playerId, FamilyId = Guid.NewGuid(), TokenHash = "hash",
            CreatedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddDays(1), ConcurrencyToken = Guid.NewGuid()
        });
        await db.SaveChangesAsync();
        var moderation = new AdminModerationService(db);

        Assert.True((await moderation.ApplyAsync(adminId, matchId, "FLAG_MATCH", "Review unusual behavior", default)).IsSuccess);
        Assert.True((await moderation.ApplyAsync(adminId, playerId, "FLAG_USER", "Review player activity", default)).IsSuccess);
        Assert.True((await moderation.ApplyAsync(adminId, matchId, "STOP_MATCH", "Stop suspicious room", default)).IsSuccess);
        Assert.Equal(MatchAuthorityStatus.Ended, (await db.MatchAuthorityBindings.SingleAsync()).Status);
        Assert.False((await moderation.ApplyAsync(adminId, adminId, "LOCK_USER", "Cannot lock admin", default)).IsSuccess);
        Assert.True((await moderation.ApplyAsync(adminId, playerId, "LOCK_USER", "Account under review", default)).IsSuccess);
        Assert.Equal(UserStatus.LOCKED, (await db.Users.SingleAsync(item => item.Id == playerId)).Status);
        Assert.NotNull((await db.RefreshSessions.AsNoTracking().SingleAsync()).RevokedAtUtc);
        Assert.Equal(2, (await moderation.HistoryAsync(matchId, default)).Data!.Count);
        Assert.True((await moderation.ApplyAsync(adminId, matchId, "CLEAR_MATCH", "Review completed safely", default)).IsSuccess);
        Assert.True((await moderation.ApplyAsync(adminId, playerId, "CLEAR_USER", "No suspicious activity", default)).IsSuccess);
        Assert.True((await moderation.ApplyAsync(adminId, playerId, "UNLOCK_USER", "Account review complete", default)).IsSuccess);
        Assert.Equal(UserStatus.ACTIVE, (await db.Users.SingleAsync(item => item.Id == playerId)).Status);
    }
}
