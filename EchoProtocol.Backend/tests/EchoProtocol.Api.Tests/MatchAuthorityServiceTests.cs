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
