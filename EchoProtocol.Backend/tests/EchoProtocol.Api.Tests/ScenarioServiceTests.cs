using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Scenarios;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class ScenarioServiceTests
{
    private static readonly DateTime Now = new(2026, 9, 21, 14, 0, 0, DateTimeKind.Utc);

    [Fact, Trait("Category", "M4ScenarioService")]
    public async Task AuthorizedHost_CommitsFixedDecisionWithoutClaimingUnityApplied()
    {
        await using var f = await Fixture.CreateAsync();
        var result = await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, Request());
        Assert.True(result.IsSuccess, $"{result.ErrorCode}: {result.Message}");
        Assert.Equal("FIXED_CONFIG_ISSUED", result.Data!.ResolutionResult);
        Assert.Equal("PENDING", result.Data.UnityApplyStatus);
        Assert.Single(await f.Db.ScenarioDecisions.ToArrayAsync());
        Assert.Empty(await f.Db.ScenarioApplyReceipts.ToArrayAsync());
    }

    [Fact, Trait("Category", "M4ScenarioService")]
    public async Task NonHost_IsRejectedWithoutPersistence()
    {
        await using var f = await Fixture.CreateAsync();
        var result = await f.Service.ResolvePreMatchAsync(f.PlayerId, f.MatchId, Request());
        Assert.Equal(ErrorCodes.MatchAuthorityForbidden, result.ErrorCode);
        Assert.Empty(await f.Db.ScenarioDecisions.ToArrayAsync());
    }

    [Fact, Trait("Category", "M4ScenarioService")]
    public async Task Snapshot_IsImmutableAndPreservesColdStartUnavailableAndDeferred()
    {
        await using var f = await Fixture.CreateAsync(includePlayerProfile: false);
        var built = await f.Builder.BuildPreMatchAsync(f.MatchId);
        Assert.Equal(AdaptiveSnapshotValidity.Partial, built.Snapshot.Validity);
        Assert.Contains("PROFILE_MISSING", built.ReasonCodes);
        var missing = built.Snapshot.Players.Single(x => x.UserId == f.PlayerId);
        Assert.Equal("UNAVAILABLE", missing.SurvivalStatus);
        Assert.Contains("\"status\":\"DEFERRED\"", missing.DeferredDimensionsJson);
        var frozen = built.Snapshot.SnapshotContentFingerprint;
        f.Db.PlayerAIProfiles.Single(x => x.UserId == f.HostId).SurvivalScore = 10;
        await f.Db.SaveChangesAsync();
        Assert.Equal(frozen, built.Snapshot.SnapshotContentFingerprint);
    }

    [Fact, Trait("Category", "M4ScenarioService")]
    public async Task RosterChange_SupersedesOldUnappliedDecisionWithNewIdentity()
    {
        await using var f = await Fixture.CreateAsync();
        var firstRequest = Request();
        var first = await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, firstRequest);
        await f.AddRosterPlayerAsync();
        var staleRetry = await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, firstRequest);
        var second = await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, Request());
        Assert.Equal(ErrorCodes.ScenarioDecisionStaleRoster, staleRetry.ErrorCode);
        Assert.True(second.IsSuccess);
        Assert.NotEqual(first.Data!.RosterIdentity, second.Data!.RosterIdentity);
        Assert.Single(await f.Db.ScenarioDecisions.Where(x => x.IsCurrent).ToArrayAsync());
    }

    [Fact, Trait("Category", "M4ScenarioService")]
    public async Task AdaptiveWithoutApprovedPolicy_UsesDeterministicFixedFallback()
    {
        await using var f = await Fixture.CreateAsync();
        var request = Request(); request.ResolutionMode = ScenarioResolutionMode.Adaptive;
        var result = await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, request);
        Assert.True(result.IsSuccess, $"{result.ErrorCode}: {result.Message}");
        Assert.True(result.Data!.UsedFixedFallback);
        Assert.Equal("POLICY_CONFIG_INVALID", result.Data.FallbackReasonCode);
        Assert.Equal("NOT_EVALUATED", result.Data.CandidateValidationStatus);
        Assert.Equal("FIXED", result.Data.Config.ConfigSource);
    }

    [Fact, Trait("Category", "M4ScenarioService")]
    public async Task MissingOrIncompatibleProductionFallback_ReturnsConfigurationError()
    {
        await using var f = await Fixture.CreateAsync(includeFallback: false);
        var missing = await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, Request());
        var incompatibleRequest = Request(); incompatibleRequest.UnityCompatibilityVersion = "OTHER";
        var incompatible = await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, incompatibleRequest);
        Assert.Equal(ErrorCodes.ScenarioFixedFallbackNotConfigured, missing.ErrorCode);
        Assert.Equal(ErrorCodes.ScenarioFixedFallbackNotConfigured, incompatible.ErrorCode);
    }

    [Fact, Trait("Category", "M4ScenarioService")]
    public async Task IdenticalRetryReplaysAndConflictingRetryIsRejected()
    {
        await using var f = await Fixture.CreateAsync();
        var request = Request();
        var initial = await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, request);
        Assert.True(initial.IsSuccess, $"{initial.ErrorCode}: {initial.Message}");
        var replay = await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, request);
        request.ResolutionMode = ScenarioResolutionMode.Adaptive;
        var conflict = await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, request);
        Assert.True(replay.Data!.IsReplay);
        Assert.Equal(ErrorCodes.ScenarioDecisionIdentityConflict, conflict.ErrorCode);
        Assert.Single(await f.Db.ScenarioDecisions.ToArrayAsync());
    }

    [Fact, Trait("Category", "M4ScenarioService")]
    public async Task NewDecisionAfterMatchStart_IsRejected()
    {
        await using var f = await Fixture.CreateAsync();
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.InMatch;
        f.Db.MatchAuthorityBindings.Single().StartedAtUtc = Now;
        await f.Db.SaveChangesAsync();
        var result = await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, Request());
        Assert.Equal(ErrorCodes.ScenarioDecisionWindowClosed, result.ErrorCode);
    }

    [Fact, Trait("Category", "M4ScenarioService")]
    public async Task ApplyConfirmation_IsSeparateIdempotentAndRejectsConflictingConfig()
    {
        await using var f = await Fixture.CreateAsync();
        var decision = (await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, Request())).Data!;
        var apply = new ConfirmScenarioAppliedRequest
        { ScenarioConfigId = decision.Config.ScenarioConfigId, ScenarioConfigVersion = decision.Config.ScenarioConfigVersion,
          ScenarioConfigFingerprint = decision.Config.ContentFingerprint };
        var first = await f.Service.ConfirmAppliedAsync(f.HostId, f.MatchId, decision.DecisionId, apply);
        var replay = await f.Service.ConfirmAppliedAsync(f.HostId, f.MatchId, decision.DecisionId, apply);
        apply.ScenarioConfigVersion = "conflict";
        var conflict = await f.Service.ConfirmAppliedAsync(f.HostId, f.MatchId, decision.DecisionId, apply);
        Assert.Equal("APPLIED", first.Data!.UnityApplyStatus);
        Assert.True(replay.Data!.IsReplay);
        Assert.Equal(ErrorCodes.ScenarioApplyConflict, conflict.ErrorCode);
        Assert.Single(await f.Db.ScenarioApplyReceipts.ToArrayAsync());
    }

    [Fact, Trait("Category", "M4ScenarioService")]
    public async Task PersistFailure_RollsBackSnapshotAndDecision()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_decision BEFORE INSERT ON ScenarioDecisions BEGIN SELECT RAISE(ABORT, 'test failure'); END;");
        var result = await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, Request());
        Assert.False(result.IsSuccess);
        Assert.Empty(await f.Db.AdaptiveInputSnapshots.ToArrayAsync());
        Assert.Empty(await f.Db.ScenarioDecisions.ToArrayAsync());
    }

    private static ResolveScenarioRequest Request() => new()
    { DecisionId = Guid.NewGuid(), ResolutionMode = ScenarioResolutionMode.Fixed, UnityCompatibilityVersion = "TEST_UNITY_V1" };

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        public AppDbContext Db { get; }
        public Guid HostId { get; } = Guid.NewGuid(); public Guid PlayerId { get; } = Guid.NewGuid();
        public Guid MatchId { get; } = Guid.NewGuid();
        public AdaptiveInputSnapshotBuilder Builder { get; }
        public ScenarioService Service { get; }
        private readonly TimeProvider clock = new FixedClock(Now);
        private Fixture(SqliteConnection c, AppDbContext db)
        {
            connection = c; Db = db; Builder = new(db, clock);
            Service = new(db, new ScenarioConfigRegistry(db), Builder, clock);
        }
        public static async Task<Fixture> CreateAsync(bool includeFallback = true, bool includePlayerProfile = true)
        {
            var c = new SqliteConnection("Data Source=:memory:"); await c.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(c).Options);
            await db.Database.EnsureCreatedAsync(); var f = new Fixture(c, db); f.Seed(includeFallback, includePlayerProfile);
            await db.SaveChangesAsync(); return f;
        }
        private void Seed(bool fallback, bool playerProfile)
        {
            Db.Users.AddRange(User(HostId), User(PlayerId));
            Db.MatchAuthorityBindings.Add(new MatchAuthorityBinding
            { MatchId = MatchId, FusionSessionName = "scenario-test", HostUserId = HostId, MaxPlayers = 4,
              Status = MatchAuthorityStatus.Lobby, LeaseExpiresAtUtc = Now.AddMinutes(5), CreatedAtUtc = Now, UpdatedAtUtc = Now,
              Players = [Binding(HostId, 1), Binding(PlayerId, 2)] });
            Db.PlayerAIProfiles.Add(Profile(HostId, ProfileDimensionStatus.Active, 2));
            if (playerProfile) Db.PlayerAIProfiles.Add(Profile(PlayerId, ProfileDimensionStatus.ColdStart, 0));
            if (fallback) { Db.ScenarioContentDefinitions.AddRange(Content()); Db.ScenarioConfigs.Add(Config()); }
        }
        public async Task AddRosterPlayerAsync()
        {
            var id = Guid.NewGuid(); Db.Users.Add(User(id)); Db.MatchPlayerBindings.Add(Binding(id, 3)); await Db.SaveChangesAsync();
        }
        private MatchPlayerBinding Binding(Guid id, int actor) => new()
        { Id = Guid.NewGuid(), MatchId = MatchId, UserId = id, FusionActorNumber = actor, JoinProofId = Guid.NewGuid(), BoundAtUtc = Now, LastSeenAtUtc = Now };
        private static User User(Guid id) => new() { Id=id, Email=$"{id:N}@test.local", Username=$"u{id:N}", PasswordHash="hash", Role=UserRole.PLAYER, Status=UserStatus.ACTIVE, CreatedAt=Now, UpdatedAt=Now };
        private static PlayerAIProfile Profile(Guid id, ProfileDimensionStatus status, int count) => new()
        { UserId=id, ProfileLineageId=Guid.NewGuid(), ProfileRevision=1, ProfileFormulaVersion="PROFILE_FORMULA_V1_1",
          MatchScoreFormulaVersion="TEST_SCORE_V1", NormalizationConfigVersion="TEST_NORM_V1", ProfileNoiseFilterVersion="TEST_FILTER_V1",
          AlphaConfigVersion="TEST_ALPHA_V1", SurvivalScore=status==ProfileDimensionStatus.Active?75:50, SurvivalStatus=status,
          SurvivalSampleCount=count, NoiseScore=status==ProfileDimensionStatus.Active?70:50, NoiseStatus=status, NoiseSampleCount=count,
          CreatedAtUtc=Now, UpdatedAtUtc=Now };
        private static ScenarioConfigDefinition Config() => new()
        { ScenarioConfigId="FIXED_BASELINE_V1", ScenarioConfigVersion="FIXED_BASELINE_TEST_V1", SchemaVersion="1.1",
          PolicyVersion=ScenarioConfigValidator.SupportedPolicyVersion, ConfigSource=ScenarioConfigSource.Fixed,
          MapId="map", MonsterType="STALKER", ObjectiveSpawnSetId="objectives", SupportItemBudget=0,
          DetectionFillRate=1, DetectionDecayRate=1, ChaseSpeed=1, SearchDuration=1, RouteModifier="route",
          EscapeDoorTimerSeconds=45, FallbackConfigId="FIXED_BASELINE_V1", FallbackConfigVersion="FIXED_BASELINE_TEST_V1",
          ContentWhitelistVersion="TEST_WHITELIST", UnityCompatibilityVersion="TEST_UNITY_V1", IsActive=true,
          IsProductionApproved=true, IsFixedFallback=true, Provenance="AUTOMATED_TEST_FIXTURE", CreatedAtUtc=Now, UpdatedAtUtc=Now };
        private static ScenarioContentDefinition[] Content() =>
        [Entry(ScenarioContentType.Map,"map"),Entry(ScenarioContentType.Monster,"STALKER"),Entry(ScenarioContentType.ObjectiveSpawnSet,"objectives"),Entry(ScenarioContentType.RouteModifier,"route")];
        private static ScenarioContentDefinition Entry(ScenarioContentType t,string id)=>new()
        {Id=Guid.NewGuid(),ContentType=t,ContentId=id,ContentWhitelistVersion="TEST_WHITELIST",UnityCompatibilityVersion="TEST_UNITY_V1",IsActive=true,IsProductionApproved=true,Provenance="AUTOMATED_TEST_FIXTURE",CreatedAtUtc=Now,UpdatedAtUtc=Now};
        public async ValueTask DisposeAsync(){await Db.DisposeAsync();await connection.DisposeAsync();}
    }
    private sealed class FixedClock(DateTime utc) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(utc, TimeSpan.Zero); }
}
