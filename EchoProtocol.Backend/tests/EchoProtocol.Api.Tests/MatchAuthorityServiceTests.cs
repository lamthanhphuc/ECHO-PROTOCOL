using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.MatchAuthority;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class MatchAuthorityServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

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
    public async Task InMatch_OnlyDisconnectedRosterMemberGetsReconnectProof()
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
        Assert.False((await service.IssueJoinProofAsync(playerId, matchId, request, CancellationToken.None)).IsSuccess);
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
    public async Task SameUser_CannotBindTwoActorsInSameMatch()
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

        Assert.False(secondBind.IsSuccess);
        Assert.Equal("MATCH_PLAYER_BINDING_CONFLICT", secondBind.ErrorCode);
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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
