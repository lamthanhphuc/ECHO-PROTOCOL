using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.MatchAuthority;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class MatchAuthorityServiceTests
{
    [Fact]
    public async Task LobbyReturnCannotExceedCapacityAndStartFreezesConnectedRoster()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var host = Guid.NewGuid(); var departed = Guid.NewGuid(); var replacement = Guid.NewGuid();
        var created = await service.CreateAsync(host, new CreateMatchAuthorityRequest
            { FusionSessionName = "capacity-return", MaxPlayers = 2 }, CancellationToken.None);
        var id = created.Data!.MatchId;
        await BindAsync(service, host, host, id, "capacity-return", 1);
        await BindAsync(service, host, departed, id, "capacity-return", 2);
        await service.MarkPlayerDisconnectedAsync(host, id, 2, departed, CancellationToken.None);
        await BindAsync(service, host, replacement, id, "capacity-return", 3);
        var proof = await service.IssueJoinProofAsync(departed, id, new IssueJoinProofRequest
            { FusionSessionName = "capacity-return", FusionActorNumber = 4 }, CancellationToken.None);
        var returned = await service.BindPlayerAsync(host, id, new BindMatchPlayerRequest
            { FusionActorNumber = 4, JoinProof = proof.Data!.Proof }, CancellationToken.None);
        Assert.False(returned.IsSuccess);
        Assert.Equal("MATCH_CAPACITY_REACHED", returned.ErrorCode);
        Assert.True((await service.StartAsync(host, id, CancellationToken.None)).IsSuccess);
        Assert.False((await db.MatchPlayerBindings.SingleAsync(p => p.UserId == departed)).Participated);
        Assert.Equal(2, await db.MatchPlayerBindings.CountAsync(p => p.Participated));
    }
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HostReadsOnlyBoundPlayersCurrentInventory()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var hostId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var ownedItem = Guid.NewGuid();
        var unrelatedItem = Guid.NewGuid();
        var match = await service.CreateAsync(hostId, new CreateMatchAuthorityRequest
        {
            FusionSessionName = "entitlement-room", MaxPlayers = 4
        }, CancellationToken.None);
        var matchId = match.Data!.MatchId;
        await BindAsync(service, hostId, playerId, matchId, "entitlement-room", 2);
        db.InventoryItems.AddRange(
            new InventoryItem { InventoryItemId = Guid.NewGuid(), UserId = playerId,
                ShopItemId = ownedItem, AcquiredAtUtc = Now.UtcDateTime },
            new InventoryItem { InventoryItemId = Guid.NewGuid(), UserId = otherId,
                ShopItemId = unrelatedItem, AcquiredAtUtc = Now.UtcDateTime });
        await db.SaveChangesAsync();

        var accepted = await service.GetPlayerEntitlementsAsync(hostId, matchId, 2, CancellationToken.None);
        Assert.True(accepted.IsSuccess);
        Assert.Equal(playerId, accepted.Data!.UserId);
        Assert.Equal(new[] { ownedItem }, accepted.Data.ShopItemIds);
        Assert.False((await service.GetPlayerEntitlementsAsync(otherId, matchId, 2, CancellationToken.None)).IsSuccess);
        Assert.False((await service.GetPlayerEntitlementsAsync(hostId, matchId, 3, CancellationToken.None)).IsSuccess);

        db.InventoryItems.Add(new InventoryItem { InventoryItemId = Guid.NewGuid(), UserId = playerId,
            ShopItemId = unrelatedItem, AcquiredAtUtc = Now.UtcDateTime });
        await db.SaveChangesAsync();
        var refreshed = await service.GetPlayerEntitlementsAsync(hostId, matchId, 2, CancellationToken.None);
        Assert.Contains(unrelatedItem, refreshed.Data!.ShopItemIds);

        await service.MarkPlayerDisconnectedAsync(hostId, matchId, 2, playerId, CancellationToken.None);
        Assert.False((await service.GetPlayerEntitlementsAsync(hostId, matchId, 2, CancellationToken.None)).IsSuccess);
    }

    [Fact]
    public async Task BoundHost_CanDelegateTelemetryForVerifiedPlayer()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var hostId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var created = await service.CreateAsync(hostId, new CreateMatchAuthorityRequest
        {
            FusionSessionName = "room-a",
            MaxPlayers = 4
        }, CancellationToken.None);
        Assert.True(created.IsSuccess);

        await BindAsync(service, hostId, hostId, created.Data!.MatchId, "room-a", 1);
        await BindAsync(service, hostId, playerId, created.Data.MatchId, "room-a", 2);

        var started = await service.StartAsync(hostId, created.Data.MatchId, CancellationToken.None);
        var delegated = await service.CanSubmitTelemetryAsync(
            hostId, created.Data.MatchId, playerId, CancellationToken.None);

        Assert.True(started.IsSuccess);
        Assert.True(delegated);
        Assert.True(await service.CanSubmitSystemTelemetryAsync(
            hostId, created.Data.MatchId, CancellationToken.None));
    }

    [Fact]
    public async Task SingleBoundHost_CanStartMatch()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var hostId = Guid.NewGuid();
        var created = await service.CreateAsync(hostId, new CreateMatchAuthorityRequest
        {
            FusionSessionName = "solo-room",
            MaxPlayers = 4
        }, CancellationToken.None);
        Assert.True(created.IsSuccess);

        await BindAsync(service, hostId, hostId, created.Data!.MatchId, "solo-room", 1);

        var started = await service.StartAsync(hostId, created.Data.MatchId, CancellationToken.None);

        Assert.True(started.IsSuccess);
        Assert.Equal(MatchAuthorityStatus.InMatch, started.Data!.Status);
        Assert.Equal(
            Now.UtcDateTime,
            (await db.MatchAuthorityBindings.SingleAsync()).StartedAtUtc);
    }

    [Fact]
    public async Task StartContextIsValidatedAndCannotChangeAfterStart()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var hostId = Guid.NewGuid();
        var created = await service.CreateAsync(hostId, new CreateMatchAuthorityRequest
        {
            FusionSessionName = "leaderboard-context-room",
            MaxPlayers = 2
        }, CancellationToken.None);
        var matchId = created.Data!.MatchId;
        await BindAsync(service, hostId, hostId, matchId, "leaderboard-context-room", 1);

        var incomplete = await service.StartAsync(hostId, matchId, CancellationToken.None,
            new StartMatchAuthorityRequest { MapId = "SciFi" });
        Assert.False(incomplete.IsSuccess);
        Assert.Equal(MatchAuthorityStatus.Lobby,
            (await db.MatchAuthorityBindings.SingleAsync()).Status);

        var started = await service.StartAsync(hostId, matchId, CancellationToken.None,
            new StartMatchAuthorityRequest { MapId = "SciFi", Difficulty = "normal" });
        Assert.True(started.IsSuccess);
        Assert.Equal("SciFi", started.Data!.MapId);
        Assert.Equal("NORMAL", started.Data.Difficulty);

        var changed = await service.StartAsync(hostId, matchId, CancellationToken.None,
            new StartMatchAuthorityRequest { MapId = "Scene_A", Difficulty = "HARD" });
        Assert.False(changed.IsSuccess);
        Assert.Equal("MATCH_SESSION_CONFLICT", changed.ErrorCode);
        Assert.Equal("SciFi", (await db.MatchAuthorityBindings.SingleAsync()).MapId);
    }

    [Fact]
    public async Task UnboundOrNonHostUser_CannotDelegateTelemetry()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var hostId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var created = await service.CreateAsync(hostId, new CreateMatchAuthorityRequest
        {
            FusionSessionName = "room-a",
            MaxPlayers = 2
        }, CancellationToken.None);

        Assert.False(await service.CanSubmitTelemetryAsync(
            hostId, created.Data!.MatchId, playerId, CancellationToken.None));
        Assert.False(await service.CanSubmitTelemetryAsync(
            Guid.NewGuid(), created.Data.MatchId, playerId, CancellationToken.None));
        Assert.False(await service.CanSubmitSystemTelemetryAsync(
            Guid.NewGuid(), created.Data.MatchId, CancellationToken.None));
    }

    [Fact]
    public async Task BindPlayer_ProofForDifferentActor_IsRejected()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var hostId = Guid.NewGuid();
        var created = await service.CreateAsync(hostId, new CreateMatchAuthorityRequest
        {
            FusionSessionName = "room-a",
            MaxPlayers = 4
        }, CancellationToken.None);
        var proof = await service.IssueJoinProofAsync(Guid.NewGuid(), created.Data!.MatchId,
            new IssueJoinProofRequest { FusionActorNumber = 2, FusionSessionName = "room-a" },
            CancellationToken.None);

        var binding = await service.BindPlayerAsync(hostId, created.Data.MatchId,
            new BindMatchPlayerRequest { FusionActorNumber = 3, JoinProof = proof.Data!.Proof },
            CancellationToken.None);

        Assert.False(binding.IsSuccess);
        Assert.Equal("JOIN_PROOF_INVALID", binding.ErrorCode);
    }

    [Fact]
    public async Task ActorBeyondRoomCapacity_ProofBindsWithoutExpandingMaxPlayers()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var hostId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var thirdUserId = Guid.NewGuid();
        var created = await service.CreateAsync(hostId, new CreateMatchAuthorityRequest
        {
            FusionSessionName = "rejoin-actor-room",
            MaxPlayers = 2
        }, CancellationToken.None);
        Assert.True(created.IsSuccess);

        var matchId = created.Data!.MatchId;
        await BindAsync(service, hostId, hostId, matchId, "rejoin-actor-room", 1);
        await BindAsync(service, hostId, playerId, matchId, "rejoin-actor-room", 8);

        var bound = await db.MatchPlayerBindings.SingleAsync(item =>
            item.MatchId == matchId && item.UserId == playerId);
        Assert.Equal(8, bound.FusionActorNumber);
        Assert.Equal(2, await db.MatchPlayerBindings.CountAsync(item =>
            item.MatchId == matchId && item.DisconnectedAtUtc == null));

        // Actor 11 is valid as an identifier, but a third player must still
        // be rejected because only two concurrent players are permitted.
        var extraProof = await service.IssueJoinProofAsync(
            thirdUserId, matchId, new IssueJoinProofRequest
            {
                FusionActorNumber = 11,
                FusionSessionName = "rejoin-actor-room"
            }, CancellationToken.None);
        Assert.True(extraProof.IsSuccess);

        var excessBinding = await service.BindPlayerAsync(
            hostId, matchId, new BindMatchPlayerRequest
            {
                FusionActorNumber = 11,
                JoinProof = extraProof.Data!.Proof
            }, CancellationToken.None);
        Assert.False(excessBinding.IsSuccess);
        Assert.Equal("MATCH_CAPACITY_REACHED", excessBinding.ErrorCode);
    }

    [Fact]
    public async Task InMatch_OnlyExistingRosterMemberGetsReconnectProof()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var hostId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var created = await service.CreateAsync(hostId, new CreateMatchAuthorityRequest
        {
            FusionSessionName = "reconnect-room", MaxPlayers = 4
        }, CancellationToken.None);
        var matchId = created.Data!.MatchId;
        await BindAsync(service, hostId, hostId, matchId, "reconnect-room", 1);
        await BindAsync(service, hostId, playerId, matchId, "reconnect-room", 2);
        Assert.True((await service.StartAsync(hostId, matchId, CancellationToken.None)).IsSuccess);

        var request = new IssueJoinProofRequest
        {
            FusionSessionName = "reconnect-room", FusionActorNumber = 2
        };
        Assert.True((await service.IssueJoinProofAsync(playerId, matchId, request, CancellationToken.None)).IsSuccess);
        Assert.False((await service.IssueJoinProofAsync(Guid.NewGuid(), matchId, request, CancellationToken.None)).IsSuccess);

        var boundPlayer = await db.MatchPlayerBindings.SingleAsync(item => item.UserId == playerId);
        boundPlayer.DisconnectedAtUtc = Now.UtcDateTime;
        await db.SaveChangesAsync();
        var reconnectProof = await service.IssueJoinProofAsync(playerId, matchId, request, CancellationToken.None);
        Assert.True(reconnectProof.IsSuccess);
        Assert.False((await service.IssueJoinProofAsync(Guid.NewGuid(), matchId, request, CancellationToken.None)).IsSuccess);
        Assert.True((await service.BindPlayerAsync(hostId, matchId,
            new BindMatchPlayerRequest { FusionActorNumber = 2, JoinProof = reconnectProof.Data!.Proof },
            CancellationToken.None)).IsSuccess);
        Assert.Null((await db.MatchPlayerBindings.SingleAsync(item => item.UserId == playerId)).DisconnectedAtUtc);
    }

    [Fact]
    public async Task SameRosterUser_RebindMovesActorWithoutCreatingDuplicate()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var hostId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        var created = await service.CreateAsync(
            hostId,
            new CreateMatchAuthorityRequest
            {
                FusionSessionName = "duplicate-user-room",
                MaxPlayers = 4
            },
            CancellationToken.None);

        Assert.True(created.IsSuccess);

        var matchId = created.Data!.MatchId;

        await BindAsync(
            service,
            hostId,
            playerId,
            matchId,
            "duplicate-user-room",
            1);

        var secondProof = await service.IssueJoinProofAsync(
            playerId,
            matchId,
            new IssueJoinProofRequest
            {
                FusionActorNumber = 2,
                FusionSessionName = "duplicate-user-room"
            },
            CancellationToken.None);

        Assert.True(secondProof.IsSuccess);

        var secondBind = await service.BindPlayerAsync(
            hostId,
            matchId,
            new BindMatchPlayerRequest
            {
                FusionActorNumber = 2,
                JoinProof = secondProof.Data!.Proof
            },
            CancellationToken.None);

        Assert.True(secondBind.IsSuccess);
        Assert.Equal(2, secondBind.Data!.FusionActorNumber);
        Assert.Equal(
            1,
            await db.MatchPlayerBindings.CountAsync(item =>
                item.MatchId == matchId
                && item.UserId == playerId));
    }

    [Fact]
    public async Task ActivePlayer_CannotJoinAnotherActiveMatch()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var hostA = Guid.NewGuid();
        var hostB = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        var matchA = await service.CreateAsync(
            hostA,
            new CreateMatchAuthorityRequest
            {
                FusionSessionName = "active-room-a",
                MaxPlayers = 4
            },
            CancellationToken.None);

        var matchB = await service.CreateAsync(
            hostB,
            new CreateMatchAuthorityRequest
            {
                FusionSessionName = "active-room-b",
                MaxPlayers = 4
            },
            CancellationToken.None);

        Assert.True(matchA.IsSuccess);
        Assert.True(matchB.IsSuccess);

        await BindAsync(
            service,
            hostA,
            playerId,
            matchA.Data!.MatchId,
            "active-room-a",
            2);

        var proof = await service.IssueJoinProofAsync(
            playerId,
            matchB.Data!.MatchId,
            new IssueJoinProofRequest
            {
                FusionActorNumber = 3,
                FusionSessionName = "active-room-b"
            },
            CancellationToken.None);

        Assert.False(proof.IsSuccess);
        Assert.Equal("ACCOUNT_ALREADY_IN_ACTIVE_MATCH", proof.ErrorCode);
    }

    [Fact]
    public async Task DisconnectedPlayer_CanJoinAnotherActiveMatch()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var hostA = Guid.NewGuid();
        var hostB = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        var matchA = await service.CreateAsync(
            hostA,
            new CreateMatchAuthorityRequest
            {
                FusionSessionName = "leave-room-a",
                MaxPlayers = 4
            },
            CancellationToken.None);

        var matchB = await service.CreateAsync(
            hostB,
            new CreateMatchAuthorityRequest
            {
                FusionSessionName = "leave-room-b",
                MaxPlayers = 4
            },
            CancellationToken.None);

        Assert.True(matchA.IsSuccess);
        Assert.True(matchB.IsSuccess);

        await BindAsync(
            service,
            hostA,
            playerId,
            matchA.Data!.MatchId,
            "leave-room-a",
            2);

        var disconnected = await service.MarkPlayerDisconnectedAsync(
            hostA,
            matchA.Data.MatchId,
            2,
            playerId,
            CancellationToken.None);

        Assert.True(disconnected.IsSuccess);

        var proof = await service.IssueJoinProofAsync(
            playerId,
            matchB.Data!.MatchId,
            new IssueJoinProofRequest
            {
                FusionActorNumber = 3,
                FusionSessionName = "leave-room-b"
            },
            CancellationToken.None);

        Assert.True(proof.IsSuccess);

        var binding = await service.BindPlayerAsync(
            hostB,
            matchB.Data.MatchId,
            new BindMatchPlayerRequest
            {
                FusionActorNumber = 3,
                JoinProof = proof.Data!.Proof
            },
            CancellationToken.None);

        Assert.True(binding.IsSuccess);
    }

    [Fact]
    public async Task ConnectedRosterMember_CannotTakeOverWhileCurrentHostLeaseIsActive()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var hostId = Guid.NewGuid();
        var nextHostId = Guid.NewGuid();

        var created = await service.CreateAsync(
            hostId,
            new CreateMatchAuthorityRequest
            {
                FusionSessionName = "migration-room",
                MaxPlayers = 4
            },
            CancellationToken.None);

        Assert.True(created.IsSuccess);
        var matchId = created.Data!.MatchId;

        await BindAsync(service, hostId, hostId, matchId, "migration-room", 1);
        await BindAsync(service, hostId, nextHostId, matchId, "migration-room", 2);

        Assert.True((await service.StartAsync(
            hostId, matchId, CancellationToken.None)).IsSuccess);

        var takeover = await service.TakeoverAsync(
            nextHostId,
            matchId,
            new TakeoverMatchAuthorityRequest
            {
                FusionSessionName = "migration-room",
                FusionActorNumber = 2
            },
            CancellationToken.None);

        Assert.False(takeover.IsSuccess);
        Assert.Equal(
            "MATCH_AUTHORITY_TAKEOVER_NOT_READY",
            takeover.ErrorCode);

        var unchanged =
            await db.MatchAuthorityBindings
                .SingleAsync(item => item.MatchId == matchId);

        Assert.Equal(hostId, unchanged.HostUserId);
        Assert.Equal(
            Now.UtcDateTime.AddSeconds(45),
            unchanged.LeaseExpiresAtUtc);
    }

    [Fact]
    public async Task ConnectedRosterMember_CanTakeOverExpiredHostAuthority()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var hostId = Guid.NewGuid();
        var nextHostId = Guid.NewGuid();

        var created = await service.CreateAsync(
            hostId,
            new CreateMatchAuthorityRequest
            {
                FusionSessionName = "migration-room",
                MaxPlayers = 4
            },
            CancellationToken.None);

        Assert.True(created.IsSuccess);
        var matchId = created.Data!.MatchId;

        await BindAsync(service, hostId, hostId, matchId, "migration-room", 1);
        await BindAsync(service, hostId, nextHostId, matchId, "migration-room", 2);

        Assert.True((await service.StartAsync(
            hostId, matchId, CancellationToken.None)).IsSuccess);

        var authority =
            await db.MatchAuthorityBindings
                .SingleAsync(item => item.MatchId == matchId);

        authority.LeaseExpiresAtUtc =
            Now.UtcDateTime.AddSeconds(-1);

        await db.SaveChangesAsync();
        var takeover = await service.TakeoverAsync(
            nextHostId,
            matchId,
            new TakeoverMatchAuthorityRequest
            {
                FusionSessionName = "migration-room",
                FusionActorNumber = 2
            },
            CancellationToken.None);

        Assert.True(takeover.IsSuccess);
        Assert.Equal(nextHostId, takeover.Data!.HostUserId);
        Assert.Contains(takeover.Data.Players, player => player.UserId == hostId && player.Disconnected);
        Assert.Contains(takeover.Data.Players, player => player.UserId == nextHostId && !player.Disconnected);
        Assert.Equal(
            Now.UtcDateTime.AddSeconds(45),
            takeover.Data.LeaseExpiresAtUtc);

        var oldHostLease = await service.RenewLeaseAsync(
            hostId, matchId, CancellationToken.None);
        Assert.False(oldHostLease.IsSuccess);
        Assert.Equal(
            "MATCH_AUTHORITY_FORBIDDEN",
            oldHostLease.ErrorCode);

        Assert.True((await service.RenewLeaseAsync(
            nextHostId, matchId, CancellationToken.None)).IsSuccess);

        Assert.False(await service.CanSubmitSystemTelemetryAsync(
            hostId, matchId, CancellationToken.None));
        Assert.True(await service.CanSubmitSystemTelemetryAsync(
            nextHostId, matchId, CancellationToken.None));

        var oldHostBinding = await db.MatchPlayerBindings
            .SingleAsync(item => item.UserId == hostId);

        Assert.NotNull(oldHostBinding.DisconnectedAtUtc);
    }

    [Fact]
    public async Task UnboundUser_CannotTakeOverHostAuthority()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var hostId = Guid.NewGuid();

        var created = await service.CreateAsync(
            hostId,
            new CreateMatchAuthorityRequest
            {
                FusionSessionName = "migration-room",
                MaxPlayers = 4
            },
            CancellationToken.None);

        Assert.True(created.IsSuccess);
        var matchId = created.Data!.MatchId;

        await BindAsync(service, hostId, hostId, matchId, "migration-room", 1);

        var takeover = await service.TakeoverAsync(
            Guid.NewGuid(),
            matchId,
            new TakeoverMatchAuthorityRequest
            {
                FusionSessionName = "migration-room",
                FusionActorNumber = 2
            },
            CancellationToken.None);

        Assert.False(takeover.IsSuccess);
        Assert.Equal(
            "MATCH_AUTHORITY_FORBIDDEN",
            takeover.ErrorCode);
        Assert.Equal(
            hostId,
            (await db.MatchAuthorityBindings.SingleAsync()).HostUserId);
    }

    [Fact]
    public async Task Takeover_AllowsBoundRosterMemberToRemapActorAfterMigration()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var hostId = Guid.NewGuid();
        var nextHostId = Guid.NewGuid();

        var created = await service.CreateAsync(
            hostId,
            new CreateMatchAuthorityRequest
            {
                FusionSessionName = "migration-room",
                MaxPlayers = 4
            },
            CancellationToken.None);

        Assert.True(created.IsSuccess);

        var matchId = created.Data!.MatchId;

        await BindAsync(
            service,
            hostId,
            hostId,
            matchId,
            "migration-room",
            1);

        await BindAsync(
            service,
            hostId,
            nextHostId,
            matchId,
            "migration-room",
            2);

        var authority =
            await db.MatchAuthorityBindings
                .SingleAsync();

        authority.LeaseExpiresAtUtc =
            Now.UtcDateTime.AddSeconds(-1);

        await db.SaveChangesAsync();

        var takeover =
            await service.TakeoverAsync(
                nextHostId,
                matchId,
                new TakeoverMatchAuthorityRequest
                {
                    FusionSessionName =
                        "migration-room",

                    FusionActorNumber = 1
                },
                CancellationToken.None);

        Assert.True(takeover.IsSuccess);
        Assert.Equal(
            nextHostId,
            takeover.Data!.HostUserId);

        var migratedHostBinding =
            await db.MatchPlayerBindings
                .SingleAsync(item =>
                    item.UserId == nextHostId);

        Assert.Equal(
            1,
            migratedHostBinding.FusionActorNumber);

        var proof =
            await service.IssueJoinProofAsync(
                nextHostId,
                matchId,
                new IssueJoinProofRequest
                {
                    FusionActorNumber = 1,
                    FusionSessionName =
                        "migration-room"
                },
                CancellationToken.None);

        Assert.True(proof.IsSuccess);

        var rebound =
            await service.BindPlayerAsync(
                nextHostId,
                matchId,
                new BindMatchPlayerRequest
                {
                    FusionActorNumber = 1,
                    JoinProof = proof.Data!.Proof
                },
                CancellationToken.None);

        Assert.True(rebound.IsSuccess);
        Assert.Equal(
            1,
            rebound.Data!.FusionActorNumber);

        var oldHost =
            await db.MatchPlayerBindings
                .SingleAsync(item =>
                    item.UserId == hostId);

        var newHost =
            await db.MatchPlayerBindings
                .SingleAsync(item =>
                    item.UserId == nextHostId);

        Assert.NotNull(
            oldHost.DisconnectedAtUtc);

        Assert.Null(
            newHost.DisconnectedAtUtc);

        Assert.Equal(
            1,
            newHost.FusionActorNumber);
    }

    [Fact]
    public async Task Takeover_AtomicallyReusesDepartedHostActor_WithRelationalUniqueIndex()
    {
        await using var connection =
            new SqliteConnection(
                "Data Source=:memory:;Foreign Keys=True");

        await connection.OpenAsync();

        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

        await using var db =
            new AppDbContext(options);

        await db.Database.EnsureCreatedAsync();

        var hostId = Guid.NewGuid();
        var nextHostId = Guid.NewGuid();

        db.Users.AddRange(
            CreateUser(hostId, "migration-host"),
            CreateUser(nextHostId, "migration-next-host"));

        await db.SaveChangesAsync();

        var service = CreateService(db);
        var created = await service.CreateAsync(
            hostId,
            new CreateMatchAuthorityRequest
            {
                FusionSessionName = "actor-reuse-room",
                MaxPlayers = 4
            },
            CancellationToken.None);

        Assert.True(created.IsSuccess);
        var matchId = created.Data!.MatchId;

        await BindAsync(
            service, hostId, hostId,
            matchId, "actor-reuse-room", 1);

        await BindAsync(
            service, hostId, nextHostId,
            matchId, "actor-reuse-room", 2);

        var authority =
            await db.MatchAuthorityBindings
                .SingleAsync();

        authority.LeaseExpiresAtUtc =
            Now.UtcDateTime.AddSeconds(-1);

        await db.SaveChangesAsync();

        var takeover = await service.TakeoverAsync(
            nextHostId,
            matchId,
            new TakeoverMatchAuthorityRequest
            {
                FusionSessionName = "actor-reuse-room",
                FusionActorNumber = 1
            },
            CancellationToken.None);

        Assert.True(takeover.IsSuccess);

        var oldHost = await db.MatchPlayerBindings
            .SingleAsync(item => item.UserId == hostId);
        var newHost = await db.MatchPlayerBindings
            .SingleAsync(item => item.UserId == nextHostId);

        Assert.NotNull(oldHost.DisconnectedAtUtc);
        Assert.Equal(1, newHost.FusionActorNumber);
        Assert.Null(newHost.DisconnectedAtUtc);
    }

    [Fact]
    public async Task Takeover_IsIdempotentForCurrentHost()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var hostId = Guid.NewGuid();

        var created = await service.CreateAsync(
            hostId,
            new CreateMatchAuthorityRequest
            {
                FusionSessionName = "migration-room",
                MaxPlayers = 4
            },
            CancellationToken.None);

        var matchId = created.Data!.MatchId;
        await BindAsync(service, hostId, hostId, matchId, "migration-room", 1);

        var takeover = await service.TakeoverAsync(
            hostId,
            matchId,
            new TakeoverMatchAuthorityRequest
            {
                FusionSessionName = "migration-room",
                FusionActorNumber = 1
            },
            CancellationToken.None);

        Assert.True(takeover.IsSuccess);
        Assert.Equal(hostId, takeover.Data!.HostUserId);

        var binding = await db.MatchPlayerBindings
            .SingleAsync(item => item.UserId == hostId);

        Assert.Null(binding.DisconnectedAtUtc);
    }

    [Fact]
    public async Task ExpiredCurrentHost_CannotResurrectAuthorityThroughTakeover()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var hostId = Guid.NewGuid();

        var created = await service.CreateAsync(
            hostId,
            new CreateMatchAuthorityRequest
            {
                FusionSessionName = "migration-room",
                MaxPlayers = 4
            },
            CancellationToken.None);

        var matchId = created.Data!.MatchId;
        await BindAsync(service, hostId, hostId, matchId, "migration-room", 1);

        var authority =
            await db.MatchAuthorityBindings
                .SingleAsync(item => item.MatchId == matchId);

        authority.LeaseExpiresAtUtc =
            Now.UtcDateTime.AddSeconds(-1);

        await db.SaveChangesAsync();
        var takeover = await service.TakeoverAsync(
            hostId,
            matchId,
            new TakeoverMatchAuthorityRequest
            {
                FusionSessionName = "migration-room",
                FusionActorNumber = 1
            },
            CancellationToken.None);

        Assert.False(takeover.IsSuccess);
        Assert.Equal(
            "MATCH_LEASE_EXPIRED",
            takeover.ErrorCode);
    }

    [Fact]
    public async Task HealthyHost_CanRelinquishWithoutEndingMatch()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var hostId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        var created = await service.CreateAsync(
            hostId,
            new CreateMatchAuthorityRequest
            {
                FusionSessionName = "relinquish-room",
                MaxPlayers = 4
            },
            CancellationToken.None);

        Assert.True(created.IsSuccess);
        var matchId = created.Data!.MatchId;

        await BindAsync(
            service, hostId, hostId,
            matchId, "relinquish-room", 1);

        await BindAsync(
            service, hostId, playerId,
            matchId, "relinquish-room", 2);

        var started = await service.StartAsync(
            hostId, matchId, CancellationToken.None);

        Assert.True(started.IsSuccess);

        var relinquished = await service.RelinquishAsync(
            hostId, matchId, CancellationToken.None);

        Assert.True(relinquished.IsSuccess);
        Assert.Equal(
            MatchAuthorityStatus.InMatch,
            relinquished.Data!.Status);
        Assert.Equal(
            Now.UtcDateTime,
            relinquished.Data.LeaseExpiresAtUtc);

        var authority =
            await db.MatchAuthorityBindings
                .Include(item => item.Players)
                .SingleAsync(item => item.MatchId == matchId);

        Assert.Equal(
            MatchAuthorityStatus.InMatch,
            authority.Status);
        Assert.Null(authority.EndedAtUtc);
        Assert.Equal(
            Now.UtcDateTime,
            authority.LeaseExpiresAtUtc);

        var oldHost =
            authority.Players.Single(
                item => item.UserId == hostId);

        Assert.NotNull(oldHost.DisconnectedAtUtc);
        Assert.Equal(
            Now.UtcDateTime,
            oldHost.DisconnectedAtUtc!.Value);
    }

    [Fact]
    public async Task RelinquishedAuthority_CanBeTakenOverImmediately()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var hostId = Guid.NewGuid();
        var promotedHostId = Guid.NewGuid();

        var created = await service.CreateAsync(
            hostId,
            new CreateMatchAuthorityRequest
            {
                FusionSessionName = "relinquish-takeover-room",
                MaxPlayers = 4
            },
            CancellationToken.None);

        Assert.True(created.IsSuccess);
        var matchId = created.Data!.MatchId;

        await BindAsync(
            service, hostId, hostId,
            matchId, "relinquish-takeover-room", 1);

        await BindAsync(
            service, hostId, promotedHostId,
            matchId, "relinquish-takeover-room", 2);

        var started = await service.StartAsync(
            hostId, matchId, CancellationToken.None);

        Assert.True(started.IsSuccess);

        var relinquished = await service.RelinquishAsync(
            hostId, matchId, CancellationToken.None);

        Assert.True(relinquished.IsSuccess);

        var takeover = await service.TakeoverAsync(
            promotedHostId,
            matchId,
            new TakeoverMatchAuthorityRequest
            {
                FusionSessionName = "relinquish-takeover-room",
                FusionActorNumber = 2
            },
            CancellationToken.None);

        Assert.True(takeover.IsSuccess);
        Assert.Equal(
            promotedHostId,
            takeover.Data!.HostUserId);
        Assert.Equal(
            MatchAuthorityStatus.InMatch,
            takeover.Data.Status);
        Assert.True(
            takeover.Data.LeaseExpiresAtUtc
            > Now.UtcDateTime);

        var authority =
            await db.MatchAuthorityBindings
                .Include(item => item.Players)
                .SingleAsync(item => item.MatchId == matchId);

        Assert.Equal(promotedHostId, authority.HostUserId);
        Assert.Null(authority.EndedAtUtc);

        var oldHost =
            authority.Players.Single(
                item => item.UserId == hostId);

        var promotedHost =
            authority.Players.Single(
                item => item.UserId == promotedHostId);

        Assert.NotNull(oldHost.DisconnectedAtUtc);
        Assert.Null(promotedHost.DisconnectedAtUtc);
    }

    [Fact]
    public async Task NonHost_CannotRelinquishHostAuthority()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var hostId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        var created = await service.CreateAsync(
            hostId,
            new CreateMatchAuthorityRequest
            {
                FusionSessionName = "relinquish-forbidden-room",
                MaxPlayers = 4
            },
            CancellationToken.None);

        Assert.True(created.IsSuccess);
        var matchId = created.Data!.MatchId;

        await BindAsync(
            service, hostId, hostId,
            matchId, "relinquish-forbidden-room", 1);

        await BindAsync(
            service, hostId, playerId,
            matchId, "relinquish-forbidden-room", 2);

        var result = await service.RelinquishAsync(
            playerId, matchId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "MATCH_AUTHORITY_FORBIDDEN",
            result.ErrorCode);

        var authority =
            await db.MatchAuthorityBindings
                .SingleAsync(item => item.MatchId == matchId);

        Assert.Equal(hostId, authority.HostUserId);
        Assert.True(
            authority.LeaseExpiresAtUtc
            > Now.UtcDateTime);
    }

    [Fact]
    public async Task ExpiredHost_CannotRelinquishAuthority()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var hostId = Guid.NewGuid();

        var created = await service.CreateAsync(
            hostId,
            new CreateMatchAuthorityRequest
            {
                FusionSessionName = "relinquish-expired-room",
                MaxPlayers = 4
            },
            CancellationToken.None);

        Assert.True(created.IsSuccess);
        var matchId = created.Data!.MatchId;

        await BindAsync(
            service, hostId, hostId,
            matchId, "relinquish-expired-room", 1);

        var authority =
            await db.MatchAuthorityBindings
                .SingleAsync(item => item.MatchId == matchId);

        authority.LeaseExpiresAtUtc =
            Now.UtcDateTime.AddSeconds(-1);

        await db.SaveChangesAsync();

        var result = await service.RelinquishAsync(
            hostId, matchId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "MATCH_LEASE_EXPIRED",
            result.ErrorCode);
    }

    [Fact]
    public async Task Disconnect_RejectsUnexpectedIdentity_WithoutMutatingRoster()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var hostId = Guid.NewGuid();
        var originalUserId = Guid.NewGuid();
        var match = await service.CreateAsync(hostId,
            new CreateMatchAuthorityRequest
            {
                FusionSessionName = "disconnect-identity-room",
                MaxPlayers = 4
            }, CancellationToken.None);

        Assert.True(match.IsSuccess);
        await BindAsync(service, hostId, originalUserId,
            match.Data!.MatchId, "disconnect-identity-room", 2);

        var result = await service.MarkPlayerDisconnectedAsync(
            hostId, match.Data.MatchId, 2, Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("MATCH_PLAYER_BINDING_CONFLICT", result.ErrorCode);
        Assert.Null((await db.MatchPlayerBindings.SingleAsync()).DisconnectedAtUtc);
    }

    [Fact]
    public async Task Disconnect_StaleIdentityCannotDisconnectReusedActor_Relational()
    {
        await using var connection = new SqliteConnection(
            "Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var hostId = Guid.NewGuid();
        var departedUserId = Guid.NewGuid();
        var survivingUserId = Guid.NewGuid();
        db.Users.AddRange(
            CreateUser(hostId, "disconnect-actor-host"),
            CreateUser(departedUserId, "disconnect-actor-departed"),
            CreateUser(survivingUserId, "disconnect-actor-survivor"));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var created = await service.CreateAsync(hostId,
            new CreateMatchAuthorityRequest
            {
                FusionSessionName = "disconnect-actor-reuse",
                MaxPlayers = 4
            }, CancellationToken.None);
        Assert.True(created.IsSuccess);
        var matchId = created.Data!.MatchId;

        await BindAsync(service, hostId, hostId, matchId, "disconnect-actor-reuse", 1);
        await BindAsync(service, hostId, departedUserId, matchId, "disconnect-actor-reuse", 2);

        var leave = await service.MarkPlayerDisconnectedAsync(
            hostId, matchId, 2, departedUserId, CancellationToken.None);
        Assert.True(leave.IsSuccess);

        // A different account is legitimately assigned the same actor number.
        await BindAsync(service, hostId, survivingUserId, matchId,
            "disconnect-actor-reuse", 2);

        var stale = await service.MarkPlayerDisconnectedAsync(
            hostId, matchId, 2, departedUserId, CancellationToken.None);
        Assert.False(stale.IsSuccess);
        Assert.Equal("MATCH_PLAYER_BINDING_CONFLICT", stale.ErrorCode);

        var survivor = await db.MatchPlayerBindings
            .SingleAsync(item => item.UserId == survivingUserId);
        Assert.Null(survivor.DisconnectedAtUtc);

        var actual = await service.MarkPlayerDisconnectedAsync(
            hostId, matchId, 2, survivingUserId, CancellationToken.None);
        Assert.True(actual.IsSuccess);
        Assert.NotNull(survivor.DisconnectedAtUtc);
    }

    private static async Task BindAsync(
        MatchAuthorityService service,
        Guid hostId,
        Guid playerId,
        Guid matchId,
        string sessionName,
        int actorNumber)
    {
        var proof = await service.IssueJoinProofAsync(playerId, matchId,
            new IssueJoinProofRequest
            {
                FusionActorNumber = actorNumber,
                FusionSessionName = sessionName
            }, CancellationToken.None);
        var bound = await service.BindPlayerAsync(hostId, matchId,
            new BindMatchPlayerRequest
            {
                FusionActorNumber = actorNumber,
                JoinProof = proof.Data!.Proof
            }, CancellationToken.None);
        Assert.True(bound.IsSuccess);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options);
    }

    private static MatchAuthorityService CreateService(AppDbContext db)
    {
        var time = new FixedTimeProvider(Now);
        var settings = Options.Create(new MatchAuthoritySettings
        {
            ProofSigningKey = "test-proof-key-that-is-at-least-32-bytes",
            JoinProofLifetimeSeconds = 120,
            LeaseLifetimeSeconds = 45,
            TelemetryDelegationRetentionHours = 24
        });
        return new MatchAuthorityService(
            db,
            new MatchJoinProofService(settings, time),
            settings,
            time);
    }

    private static User CreateUser(
        Guid userId,
        string username)
    {
        return new User
        {
            Id = userId,
            Email = $"{username}@echo.invalid",
            Username = username,
            PasswordHash = "test-hash",
            Role = UserRole.PLAYER,
            Status = UserStatus.ACTIVE,
            CreatedAt = Now.UtcDateTime,
            UpdatedAt = Now.UtcDateTime
        };
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
