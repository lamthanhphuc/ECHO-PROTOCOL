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
    public async Task LastPlayerDisconnectStopsRoomWithoutResult()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var host = Guid.NewGuid();
        var created = await service.CreateAsync(host,
            new CreateMatchAuthorityRequest { FusionSessionName = "last-disconnect", MaxPlayers = 4 },
            CancellationToken.None);
        var matchId = created.Data!.MatchId;
        await BindAsync(service, host, host, matchId, "last-disconnect", 1);
        Assert.True((await service.StartAsync(host, matchId, CancellationToken.None)).IsSuccess);

        Assert.True((await service.MarkPlayerDisconnectedAsync(host, matchId, 1, host, CancellationToken.None)).IsSuccess);
        var match = await db.MatchAuthorityBindings.SingleAsync(item => item.MatchId == matchId);
        Assert.Equal(MatchAuthorityStatus.Ended, match.Status);
        Assert.Equal(Now.UtcDateTime, match.EndedAtUtc);
        Assert.False(await db.MatchResults.AnyAsync(item => item.MatchId == matchId));
    }

    [Fact]
    public async Task LastHostRelinquishStopsRoomWithoutResult()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var host = Guid.NewGuid();
        var created = await service.CreateAsync(host,
            new CreateMatchAuthorityRequest { FusionSessionName = "last-relinquish", MaxPlayers = 4 },
            CancellationToken.None);
        var matchId = created.Data!.MatchId;
        await BindAsync(service, host, host, matchId, "last-relinquish", 1);
        Assert.True((await service.StartAsync(host, matchId, CancellationToken.None)).IsSuccess);

        var result = await service.RelinquishAsync(host, matchId, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(MatchAuthorityStatus.Ended, result.Data!.Status);
        Assert.Equal(Now.UtcDateTime, (await db.MatchAuthorityBindings.SingleAsync(item => item.MatchId == matchId)).EndedAtUtc);
        Assert.False(await db.MatchResults.AnyAsync(item => item.MatchId == matchId));
    }

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
        var binding = await db.MatchAuthorityBindings.SingleAsync();
        var preMatch = new AEDV02Decision { DecisionId = Guid.NewGuid(), MatchId = created.Data.MatchId, HostUserId = hostId,
            DecisionPoint = "PRE_MATCH", ApprovalStatus = "APPROVED", ApplyStatus = "PENDING", ExpiresAtUtc = Now.UtcDateTime.AddSeconds(1) };
        var inMatch = new AEDV02Decision { DecisionId = Guid.NewGuid(), MatchId = created.Data.MatchId, HostUserId = hostId,
            DecisionPoint = "SAFE_WINDOW", ApprovalStatus = "APPROVED", ApplyStatus = "PENDING", ExpiresAtUtc = Now.UtcDateTime.AddSeconds(1) };
        db.AEDV02Decisions.AddRange(preMatch, inMatch);
        await db.SaveChangesAsync();
        binding.LeaseExpiresAtUtc = Now.UtcDateTime.AddSeconds(-1);
        Assert.False((await service.StartAsync(hostId, created.Data.MatchId, CancellationToken.None)).IsSuccess);
        Assert.Equal(Now.UtcDateTime.AddSeconds(1), preMatch.ExpiresAtUtc);
        binding.LeaseExpiresAtUtc = Now.UtcDateTime.AddSeconds(1);

        var started = await service.StartAsync(hostId, created.Data.MatchId, CancellationToken.None);

        Assert.True(started.IsSuccess);
        Assert.Equal(MatchAuthorityStatus.InMatch, started.Data!.Status);
        Assert.Equal(
            Now.UtcDateTime,
            (await db.MatchAuthorityBindings.SingleAsync()).StartedAtUtc);
        Assert.Equal(Now.UtcDateTime.AddSeconds(90), started.Data.LeaseExpiresAtUtc);
        Assert.Equal(started.Data.LeaseExpiresAtUtc, preMatch.ExpiresAtUtc);
        Assert.Equal(Now.UtcDateTime.AddSeconds(1), inMatch.ExpiresAtUtc);
        var renewed = await service.RenewLeaseAsync(hostId, created.Data.MatchId, CancellationToken.None);
        Assert.True(renewed.IsSuccess);
        Assert.Equal(started.Data.LeaseExpiresAtUtc, renewed.Data!.LeaseExpiresAtUtc);
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
    public async Task LegacyStartWithoutBodyStillWorks()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var host = Guid.NewGuid();
        var matchId = await CreateBoundHostMatch(service, host, "legacy-no-body");
        var result = await service.StartAsync(host, matchId, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("STANDARD", result.Data!.MatchDataPolicy);
        Assert.Null(result.Data.ResolutionMode);
        Assert.Null(result.Data.BalanceVersion);
    }

    [Fact]
    public async Task LegacyStartWithMapAndDifficultyStillWorks()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var host = Guid.NewGuid();
        var matchId = await CreateBoundHostMatch(service, host, "legacy-context");
        var result = await service.StartAsync(host, matchId, CancellationToken.None,
            new StartMatchAuthorityRequest { MapId = "SciFi", Difficulty = "normal" });
        Assert.True(result.IsSuccess);
        Assert.Equal("SciFi", result.Data!.MapId);
        Assert.Equal("NORMAL", result.Data.Difficulty);
        Assert.Equal("STANDARD", result.Data.MatchDataPolicy);
    }

    [Fact]
    public Task V2LocalFixedStartsStandardMatch() => AssertV2LocalStartsStandardMatch("FIXED");

    [Fact]
    public Task V2LocalAdaptiveStartsStandardMatch() => AssertV2LocalStartsStandardMatch("ADAPTIVE");

    private static async Task AssertV2LocalStartsStandardMatch(string mode)
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var host = Guid.NewGuid();
        var matchId = await CreateBoundHostMatch(service, host, $"local-{mode}");
        var result = await service.StartV2Async(host, matchId, V2Request(mode: mode), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(mode, result.Data!.ResolutionMode);
        Assert.Equal("LOCAL", result.Data.BalanceSource);
        Assert.Equal("STANDARD", result.Data.MatchDataPolicy);
    }

    [Fact]
    public async Task V2RemoteFixedStartsTestOnlyMatch()
    {
        await using var db = CreateDb();
        var service = CreateService(db, remoteEnabled: true);
        SeedBalance(db);
        var host = Guid.NewGuid();
        var matchId = await CreateBoundHostMatch(service, host, "remote-start");
        var result = await service.StartV2Async(host, matchId, V2Request("REMOTE_FIXED"), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("FIXED", result.Data!.ResolutionMode);
        Assert.Equal("REMOTE_FIXED", result.Data.BalanceSource);
        Assert.Equal("STANDARD", result.Data.MatchDataPolicy);
        Assert.Equal("TEST-SCIFI-NORMAL-001", result.Data.BalanceVersion);
    }

    [Fact]
    public async Task V2RemoteFixedDisabledIsRejected()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        SeedBalance(db);
        var host = Guid.NewGuid();
        var matchId = await CreateBoundHostMatch(service, host, "remote-disabled");
        var result = await service.StartV2Async(host, matchId, V2Request("REMOTE_FIXED"), CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal("BALANCE_DISABLED", result.ErrorCode);
    }

    [Theory]
    [InlineData(false, true, "LOCAL")]
    [InlineData(true, false, "LOCAL")]
    [InlineData(true, true, "REMOTE_FIXED")]
    public async Task V2AutoFixedLocksAvailableSource(bool remoteEnabled, bool hasActiveVersion, string expectedSource)
    {
        await using var db = CreateDb();
        var service = CreateService(db, remoteEnabled: remoteEnabled);
        var version = hasActiveVersion ? SeedBalance(db) : null;
        var host = Guid.NewGuid();
        var matchId = await CreateBoundHostMatch(service, host, "auto-fixed");
        var request = V2Request("AUTO");
        var result = await service.StartV2Async(host, matchId, request, CancellationToken.None);
        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(expectedSource, result.Data!.BalanceSource);
        Assert.Equal(expectedSource == "REMOTE_FIXED" ? version!.Version : null, result.Data.BalanceVersion);

        if (version is not null)
        {
            version.IsActive = false;
            await db.SaveChangesAsync();
        }
        var retry = await service.StartV2Async(host, matchId, request, CancellationToken.None);
        Assert.True(retry.IsSuccess, retry.Message);
        Assert.Equal(expectedSource, retry.Data!.BalanceSource);
        Assert.Equal(result.Data.BalanceHash, retry.Data.BalanceHash);
    }

    [Fact]
    public async Task AutomaticBalanceIsPinnedInLobbyBeforeAnActiveVersionChanges()
    {
        await using var db = CreateDb();
        var service = CreateService(db, remoteEnabled: true);
        var version = SeedBalance(db);
        var host = Guid.NewGuid();
        var matchId = await CreateBoundHostMatch(service, host, "automatic-lock");
        var request = V2Request("AUTO");
        request.PrepareBalance = true;
        request.GameVersion = "0.1.0";
        var prepared = await service.StartV2Async(host, matchId, request, CancellationToken.None);
        Assert.True(prepared.IsSuccess, prepared.Message);
        Assert.Equal(MatchAuthorityStatus.Lobby, prepared.Data!.Status);
        Assert.Equal(version.Version, prepared.Data.BalanceVersion);

        version.IsActive = false;
        await db.SaveChangesAsync();
        var retry = await service.StartV2Async(host, matchId, request, CancellationToken.None);
        Assert.Equal(version.ContentHash, retry.Data!.BalanceHash);
        request.PrepareBalance = false;
        var started = await service.StartV2Async(host, matchId, request, CancellationToken.None);
        Assert.True(started.IsSuccess, started.Message);
        Assert.Equal(MatchAuthorityStatus.InMatch, started.Data!.Status);
        Assert.Equal(version.ContentHash, started.Data.BalanceHash);
        var locked = await service.GetLockedBalanceAsync(host, matchId, CancellationToken.None);
        Assert.True(locked.IsSuccess, locked.Message);
        Assert.Equal(version.Version, locked.Data!.BalanceVersion);
    }

    [Theory]
    [InlineData(false, true, "ADAPTIVE")]
    [InlineData(true, false, "FIXED")]
    [InlineData(true, true, "ADAPTIVE")]
    public async Task PreparedRfb2PreservesTestOnlyAndRequiresRelativeAdaptiveSupport(bool verified, bool supportsRelative, string mode)
    {
        await using var db = CreateDb();
        var service = CreateService(db, remoteEnabled: true, new AEDV02PilotSettings
        {
            Enabled = true, ChaseSpeedVerifiedCompatibilityVersions = verified ? ["0.1.0"] : []
        });
        db.AdaptiveConfigurationVersions.Add(new AdaptiveConfigurationVersion
        {
            Version = "AED_ADMIN_TEST", Compatibility = "0.1.0", Revision = 1,
            ConfigurationJson = """{"Enabled":true}"""
        });
        var version = SeedBalance(db);
        version.SnapshotJson = RemoteFixedBalanceTests.ValidSnapshot.Replace("RFB-1", "RFB-2").TrimEnd()[..^1] +
            ""","gameplay":[],"content":{"gameVersion":"0.1.0","mode":"EXPEDITION","monster":"STALKER","route":"DEFAULT_ROUTE"}}""";
        version.ContentHash = RemoteFixedBalanceContract.ComputeHash(version.SnapshotJson);
        await db.SaveChangesAsync();
        var host = Guid.NewGuid();
        var matchId = await CreateBoundHostMatch(service, host, "automatic-rfb2");
        var request = V2Request("AUTO");
        request.PrepareBalance = true;
        request.SupportsGameplayTuning = true;
        request.SupportsRelativeAdaptiveV02 = supportsRelative;
        request.GameVersion = "different-build";
        Assert.False((await service.StartV2Async(host, matchId, request, CancellationToken.None)).IsSuccess);
        request.GameVersion = "0.1.0";
        var prepared = await service.StartV2Async(host, matchId, request, CancellationToken.None);
        Assert.True(prepared.IsSuccess, prepared.Message);
        Assert.Equal(mode, prepared.Data!.ResolutionMode);
        Assert.Equal("TEST_ONLY", prepared.Data.MatchDataPolicy);
        var pinnedPolicy = mode == "ADAPTIVE" ? "AED_ADMIN_TEST" : null;
        Assert.Equal(pinnedPolicy, db.MatchAuthorityBindings.Single(x => x.MatchId == matchId).AdaptivePolicyConfigVersion);
        db.AdaptiveConfigurationVersions.Add(new AdaptiveConfigurationVersion
        {
            Version = "AED_ADMIN_TEST_DISABLED", Compatibility = "0.1.0", Revision = 2,
            ConfigurationJson = """{"Enabled":false}"""
        });
        await db.SaveChangesAsync();
        var replay = await service.StartV2Async(host, matchId, request, CancellationToken.None);
        Assert.True(replay.IsSuccess, replay.Message);
        Assert.Equal(mode, replay.Data!.ResolutionMode);
        Assert.Equal(pinnedPolicy, db.MatchAuthorityBindings.Single(x => x.MatchId == matchId).AdaptivePolicyConfigVersion);
        request.PrepareBalance = false;
        request.ResolutionMode = mode;
        var started = await service.StartV2Async(host, matchId, request, CancellationToken.None);
        Assert.True(started.IsSuccess, started.Message);
        Assert.Equal("TEST_ONLY", started.Data!.MatchDataPolicy);
    }

    [Theory]
    [InlineData(7.5, 7, 6.75)]
    [InlineData(10, 7, 9)]
    [InlineData(10, 7.5, 10)]
    [InlineData(10, 8, 11)]
    public void AutomaticChaseSpeedUsesLockedBasis(double basis, double tier, double expected)
    {
        Assert.True(EchoProtocol.AI.Common.AED.AEDV02Plan.TryResolveChaseSpeed(6.5, basis, tier, out var speed));
        Assert.Equal(expected, speed);
    }

    [Theory]
    [InlineData(9.5, 10, 7)]
    [InlineData(6.5, 24, 8)]
    public void AutomaticChaseSpeedRejectsUnsafeTier(double patrol, double basis, double tier)
    {
        Assert.False(EchoProtocol.AI.Common.AED.AEDV02Plan.TryResolveChaseSpeed(patrol, basis, tier, out var speed));
        Assert.Equal(basis, speed);
    }

    [Fact]
    public async Task V2RemoteWithoutActiveVersionIsRejected()
    {
        await using var db = CreateDb();
        var service = CreateService(db, remoteEnabled: true);
        var host = Guid.NewGuid();
        var matchId = await CreateBoundHostMatch(service, host, "remote-unavailable");
        var result = await service.StartV2Async(host, matchId, V2Request("REMOTE_FIXED"), CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal("BALANCE_VERSION_UNAVAILABLE", result.ErrorCode);
    }

    [Theory]
    [InlineData(0, 4, 1, 3.6)]
    [InlineData(1, 18, 28, 19.8)]
    [InlineData(2, 1.5, .85, 1.35)]
    [InlineData(3, 10, 3, 11)]
    [InlineData(4, 10, 7, 9)]
    [InlineData(5, 600, 360, 540)]
    [InlineData(6, 3, 0, 2)]
    [InlineData(7, 3, 1, 2)]
    [InlineData(8, 2, 1, 3)]
    public void AutomaticAdaptiveUsesLockedBaseline(int key, double baseline, double tier, double expected)
    {
        Assert.True(EchoProtocol.AI.Common.AED.AEDV02Plan.TryResolveRelative(
            (EchoProtocol.AI.Common.AED.AEDV02Key)key, 6.5, baseline, tier, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData(0, 1, 4)]
    [InlineData(2, 1, 6)]
    [InlineData(5, -1, 1)]
    [InlineData(7, -1, 3)]
    [InlineData(7, 0, 7)]
    public void RelativeOptionalBudgetPreservesOtherZoneAllocations(int baseline, int step, int expected)
    {
        Assert.True(EchoProtocol.AI.Common.AED.AEDV02Plan.TryResolveOptionalMask(baseline, step, out var mask));
        Assert.Equal(expected, mask);
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(7, 1)]
    [InlineData(1, 2)]
    public void RelativeOptionalBudgetHoldsAtBounds(int baseline, int step)
    {
        Assert.False(EchoProtocol.AI.Common.AED.AEDV02Plan.TryResolveOptionalMask(baseline, step, out var mask));
        Assert.Equal(baseline, mask);
    }


    [Theory]
    [InlineData("REMOTE_FIXED")]
    [InlineData("AUTO")]
    public async Task V2RemoteInvalidHashIsRejected(string source)
    {
        await using var db = CreateDb();
        var service = CreateService(db, remoteEnabled: true);
        var version = SeedBalance(db);
        version.ContentHash = new string('0', 64);
        await db.SaveChangesAsync();
        var host = Guid.NewGuid();
        var matchId = await CreateBoundHostMatch(service, host, "remote-bad-hash");
        var result = await service.StartV2Async(host, matchId, V2Request(source), CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal("BALANCE_SNAPSHOT_INVALID", result.ErrorCode);
    }

    [Fact]
    public Task V2LegacyRemoteOnSceneBIsRejected() => AssertV2InvalidContext("Scene_B", "FIXED", "REMOTE_FIXED");

    [Fact]
    public async Task V2SceneBRequiresMatchingUnityBuildBeforeLockingContent()
    {
        await using var db = CreateDb();
        var service = CreateService(db, remoteEnabled: true);
        var version = SeedBalance(db);
        version.MapId = "Scene_B";
        version.SnapshotJson = RemoteFixedBalanceTests.ValidSnapshot.Replace("RFB-1", "RFB-2").TrimEnd()[..^1] +
            ""","gameplay":[],"content":{"gameVersion":"0.1.0","mode":"PURGE","monster":"LIZARD","route":"SCENE_B_AUTHORED"}}""";
        version.ContentHash = RemoteFixedBalanceContract.ComputeHash(version.SnapshotJson);
        await db.SaveChangesAsync();
        var host = Guid.NewGuid();
        var matchId = await CreateBoundHostMatch(service, host, "purge-content");
        var request = V2Request("REMOTE_FIXED", map: "Scene_B");
        request.SupportsGameplayTuning = true;
        request.GameVersion = "wrong-build";
        Assert.False((await service.StartV2Async(host, matchId, request, CancellationToken.None)).IsSuccess);
        request.GameVersion = "0.1.0";
        var accepted = await service.StartV2Async(host, matchId, request, CancellationToken.None);
        Assert.True(accepted.IsSuccess, accepted.Message);
        Assert.Equal("TEST_ONLY", accepted.Data!.MatchDataPolicy);
    }

    [Fact]
    public Task V2AdaptiveRemoteCombinationIsRejected() => AssertV2InvalidContext("SciFi", "ADAPTIVE", "REMOTE_FIXED");

    private static async Task AssertV2InvalidContext(string map, string mode, string source)
    {
        await using var db = CreateDb();
        var service = CreateService(db, remoteEnabled: true);
        var host = Guid.NewGuid();
        var matchId = await CreateBoundHostMatch(service, host, $"invalid-{Guid.NewGuid():N}");
        var result = await service.StartV2Async(host, matchId,
            V2Request(source, map: map, mode: mode), CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    [Fact]
    public async Task V2RetrySameContextReturnsSameVersion()
    {
        await using var db = CreateDb();
        var service = CreateService(db, remoteEnabled: true);
        SeedBalance(db);
        var host = Guid.NewGuid();
        var matchId = await CreateBoundHostMatch(service, host, "remote-retry");
        var first = await service.StartV2Async(host, matchId, V2Request("REMOTE_FIXED"), CancellationToken.None);
        var retry = await service.StartV2Async(host, matchId, V2Request("REMOTE_FIXED"), CancellationToken.None);
        Assert.True(retry.IsSuccess);
        Assert.Equal(first.Data!.BalanceVersion, retry.Data!.BalanceVersion);
        Assert.Single(await db.RemoteFixedBalanceVersions.ToArrayAsync());
    }

    [Fact]
    public async Task V2RetryAfterActiveVersionChangesReturnsLockedVersion()
    {
        await using var db = CreateDb();
        var service = CreateService(db, remoteEnabled: true);
        var firstVersion = SeedBalance(db);
        var host = Guid.NewGuid();
        var matchId = await CreateBoundHostMatch(service, host, "remote-version-change");
        var started = await service.StartV2Async(host, matchId, V2Request("REMOTE_FIXED"), CancellationToken.None);
        firstVersion.IsActive = false;
        var json = RemoteFixedBalanceTests.ValidSnapshot.Replace("\"maxHealth\": 100", "\"maxHealth\": 101");
        db.RemoteFixedBalanceVersions.Add(new RemoteFixedBalanceVersion
        {
            Version = "TEST-SCIFI-NORMAL-002", MapId = "SciFi", Difficulty = "NORMAL",
            ProtocolVersion = RemoteFixedBalanceContract.ProtocolVersion, SnapshotJson = json,
            ContentHash = RemoteFixedBalanceContract.ComputeHash(json), IsActive = true, CreatedAtUtc = Now.UtcDateTime
        });
        await db.SaveChangesAsync();
        var retry = await service.StartV2Async(host, matchId, V2Request("REMOTE_FIXED"), CancellationToken.None);
        Assert.Equal(started.Data!.BalanceVersion, retry.Data!.BalanceVersion);
        Assert.Equal(started.Data.BalanceHash, retry.Data!.BalanceHash);
    }

    [Fact]
    public Task V2RetryDifferentModeIsRejected() => AssertV2RetryConflict(V2Request(mode: "ADAPTIVE"));

    [Fact]
    public Task V2RetryDifferentProtocolIsRejected() => AssertV2RetryConflict(V2Request(protocol: "RFB-2"));

    private static async Task AssertV2RetryConflict(StartMatchV2Request changedRequest)
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var host = Guid.NewGuid();
        var matchId = await CreateBoundHostMatch(service, host, "local-retry-conflict");
        await service.StartV2Async(host, matchId, V2Request(), CancellationToken.None);
        var retry = await service.StartV2Async(host, matchId, changedRequest, CancellationToken.None);
        Assert.False(retry.IsSuccess);
        Assert.Equal("MATCH_SESSION_CONFLICT", retry.ErrorCode);
    }

    [Fact]
    public async Task V2RetryDifferentDifficultyIsRejected()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var host = Guid.NewGuid();
        var matchId = await CreateBoundHostMatch(service, host, "local-difficulty-conflict");
        await service.StartV2Async(host, matchId, V2Request(), CancellationToken.None);
        var retry = await service.StartV2Async(host, matchId, V2Request(difficulty: "HARD"), CancellationToken.None);
        Assert.Equal("MATCH_SESSION_CONFLICT", retry.ErrorCode);
    }

    [Fact]
    public async Task LegacyRetryOnV2MatchIsRejected()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var host = Guid.NewGuid();
        var firstId = await CreateBoundHostMatch(service, host, "legacy-v2-conflict");
        await service.StartV2Async(host, firstId, V2Request(), CancellationToken.None);
        var legacyRetry = await service.StartAsync(host, firstId, CancellationToken.None);
        Assert.Equal("MATCH_SESSION_CONFLICT", legacyRetry.ErrorCode);

    }

    [Fact]
    public async Task V2RetryOnLegacyMatchIsRejected()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var host = Guid.NewGuid();
        var secondId = await CreateBoundHostMatch(service, host, "v2-legacy-conflict");
        await service.StartAsync(host, secondId, CancellationToken.None);
        var v2Retry = await service.StartV2Async(host, secondId, V2Request(), CancellationToken.None);
        Assert.Equal("MATCH_SESSION_CONFLICT", v2Retry.ErrorCode);
    }

    [Fact]
    public async Task UnauthorizedUserCannotFetchBalance()
    {
        await using var db = CreateDb();
        var service = CreateService(db, remoteEnabled: true);
        var version = SeedBalance(db);
        var host = Guid.NewGuid();
        var player = Guid.NewGuid();
        var matchId = await CreateBoundHostMatch(service, host, "balance-fetch");
        await BindAsync(service, host, player, matchId, "balance-fetch", 2);
        await service.StartV2Async(host, matchId, V2Request("REMOTE_FIXED"), CancellationToken.None);
        var denied = await service.GetLockedBalanceAsync(Guid.NewGuid(), matchId, CancellationToken.None);
        Assert.Equal("MATCH_AUTHORITY_FORBIDDEN", denied.ErrorCode);
    }

    [Fact]
    public async Task AuthorizedPlayerCanFetchLockedBalance()
    {
        await using var db = CreateDb();
        var service = CreateService(db, remoteEnabled: true);
        var version = SeedBalance(db);
        var host = Guid.NewGuid();
        var player = Guid.NewGuid();
        var matchId = await CreateBoundHostMatch(service, host, "authorized-balance-fetch");
        await BindAsync(service, host, player, matchId, "authorized-balance-fetch", 2);
        await service.StartV2Async(host, matchId, V2Request("REMOTE_FIXED"), CancellationToken.None);
        var response = await service.GetLockedBalanceAsync(player, matchId, CancellationToken.None);
        Assert.True(response.IsSuccess);
        Assert.Equal(version.ContentHash, response.Data!.BalanceHash);
        Assert.Equal(version.SnapshotJson, System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(response.Data.SnapshotPayloadBase64)));
    }

    [Fact]
    public async Task SnapshotTamperingIsDetected()
    {
        await using var db = CreateDb();
        var service = CreateService(db, remoteEnabled: true);
        var version = SeedBalance(db);
        var host = Guid.NewGuid();
        var matchId = await CreateBoundHostMatch(service, host, "balance-tamper");
        await service.StartV2Async(host, matchId, V2Request("REMOTE_FIXED"), CancellationToken.None);
        version.SnapshotJson = version.SnapshotJson.Replace("\"maxHealth\": 100", "\"maxHealth\": 101");
        await db.SaveChangesAsync();
        var response = await service.GetLockedBalanceAsync(host, matchId, CancellationToken.None);
        Assert.Equal("BALANCE_SNAPSHOT_INVALID", response.ErrorCode);
    }

    [Fact]
    public async Task RejectedStartDoesNotFreezeRoster()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var host = Guid.NewGuid();
        var matchId = await CreateBoundHostMatch(service, host, "rejected-start-roster");
        var result = await service.StartV2Async(host, matchId, V2Request("REMOTE_FIXED"), CancellationToken.None);
        var match = await db.MatchAuthorityBindings.SingleAsync();
        Assert.False(result.IsSuccess);
        Assert.Equal(MatchAuthorityStatus.Lobby, match.Status);
        Assert.False((await db.MatchPlayerBindings.SingleAsync()).Participated);
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
        Assert.True((await service.MarkPlayerDisconnectedAsync(hostId, matchId, 2, playerId, CancellationToken.None)).IsSuccess);
        Assert.True(boundPlayer.HadGameplayDisconnect);
        var reconnectProof = await service.IssueJoinProofAsync(playerId, matchId, request, CancellationToken.None);
        Assert.True(reconnectProof.IsSuccess);
        Assert.False((await service.IssueJoinProofAsync(Guid.NewGuid(), matchId, request, CancellationToken.None)).IsSuccess);
        Assert.True((await service.BindPlayerAsync(hostId, matchId,
            new BindMatchPlayerRequest { FusionActorNumber = 2, JoinProof = reconnectProof.Data!.Proof },
            CancellationToken.None)).IsSuccess);
        Assert.Null((await db.MatchPlayerBindings.SingleAsync(item => item.UserId == playerId)).DisconnectedAtUtc);
        Assert.True(boundPlayer.HadGameplayDisconnect);
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
            Now.UtcDateTime.AddSeconds(90),
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

    private static MatchAuthorityService CreateService(AppDbContext db, bool remoteEnabled = false, AEDV02PilotSettings? pilot = null)
    {
        var time = new FixedTimeProvider(Now);
        var settings = Options.Create(new MatchAuthoritySettings
        {
            ProofSigningKey = "test-proof-key-that-is-at-least-32-bytes",
            JoinProofLifetimeSeconds = 120,
            LeaseLifetimeSeconds = 45,
            TelemetryDelegationRetentionHours = 24,
            EnableRemoteFixed = remoteEnabled,
            EnableRemoteFixedGameplayTest = remoteEnabled
        });
        return new MatchAuthorityService(
            db,
            new MatchJoinProofService(settings, time),
            settings,
            time,
            pilot == null ? null : Options.Create(pilot));
    }

    private static RemoteFixedBalanceVersion SeedBalance(AppDbContext db)
    {
        var json = RemoteFixedBalanceTests.ValidSnapshot;
        var version = new RemoteFixedBalanceVersion
        {
            Version = "TEST-SCIFI-NORMAL-001", MapId = "SciFi", Difficulty = "NORMAL",
            ProtocolVersion = RemoteFixedBalanceContract.ProtocolVersion, SnapshotJson = json,
            ContentHash = RemoteFixedBalanceContract.ComputeHash(json), IsActive = true, CreatedAtUtc = Now.UtcDateTime
        };
        db.RemoteFixedBalanceVersions.Add(version);
        db.SaveChanges();
        return version;
    }

    private static StartMatchV2Request V2Request(
        string source = "LOCAL", string map = "SciFi", string mode = "FIXED",
        string protocol = "RFB-1", string difficulty = "NORMAL") => new()
    {
        MapId = map, Difficulty = difficulty, ResolutionMode = mode,
        BalanceSource = source, BalanceProtocolVersion = protocol
    };

    private static async Task<Guid> CreateBoundHostMatch(
        MatchAuthorityService service, Guid hostId, string sessionName)
    {
        var created = await service.CreateAsync(hostId,
            new CreateMatchAuthorityRequest { FusionSessionName = sessionName, MaxPlayers = 4 }, CancellationToken.None);
        Assert.True(created.IsSuccess);
        await BindAsync(service, hostId, hostId, created.Data!.MatchId, sessionName, 1);
        return created.Data.MatchId;
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
