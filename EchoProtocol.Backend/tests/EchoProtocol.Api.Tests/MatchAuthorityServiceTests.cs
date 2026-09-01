using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.MatchAuthority;
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
