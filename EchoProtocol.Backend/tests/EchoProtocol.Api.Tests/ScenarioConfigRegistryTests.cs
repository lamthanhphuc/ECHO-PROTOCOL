using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class ScenarioConfigRegistryTests
{
    private const string Compatibility = "TEST_UNITY_CONTRACT_V1";

    [Fact, Trait("Category", "M4Scenario")]
    public void ValidConfig_PassesCanonicalAndApprovedContentValidation()
    {
        var config = Config("candidate", ScenarioConfigSource.Adaptive);
        var result = ScenarioConfigValidator.Validate(config, Content(), Compatibility);
        Assert.True(result.IsValid);
        Assert.Empty(result.ReasonCodes);
    }

    [Fact, Trait("Category", "M4Scenario")]
    public void VersionMismatch_IsRejected()
    {
        var config = Config("candidate", ScenarioConfigSource.Adaptive);
        config.SchemaVersion = "9.9";
        var result = ScenarioConfigValidator.Validate(config, Content(), Compatibility);
        Assert.False(result.IsValid);
        Assert.Contains("SCENARIO_SCHEMA_VERSION_UNSUPPORTED", result.ReasonCodes);
    }

    [Fact, Trait("Category", "M4Scenario")]
    public void UnknownOrDisabledContent_IsRejected()
    {
        var config = Config("candidate", ScenarioConfigSource.Adaptive);
        var unknown = ScenarioConfigValidator.Validate(config, Content().Where(x => x.ContentType != ScenarioContentType.Map).ToArray(), Compatibility);
        var disabledContent = Content();
        disabledContent.Single(x => x.ContentType == ScenarioContentType.Map).IsActive = false;
        var disabled = ScenarioConfigValidator.Validate(config, disabledContent, Compatibility);
        Assert.Contains("SCENARIO_CONTENT_UNKNOWN", unknown.ReasonCodes);
        Assert.Contains("SCENARIO_CONTENT_DISABLED_OR_UNAPPROVED", disabled.ReasonCodes);
    }

    [Fact, Trait("Category", "M4Scenario")]
    public void InvalidNumericValue_IsRejectedWithoutRepair()
    {
        var config = Config("candidate", ScenarioConfigSource.Adaptive);
        config.ChaseSpeed = double.NaN;
        var result = ScenarioConfigValidator.Validate(config, Content(), Compatibility);
        Assert.False(result.IsValid);
        Assert.Contains("SCENARIO_VALUE_OUT_OF_RANGE", result.ReasonCodes);
    }

    [Fact, Trait("Category", "M4Scenario")]
    public async Task InvalidRequestedConfig_UsesSameDeterministicApprovedFixedFallback()
    {
        await using var fixture = await Fixture.CreateAsync(includeFallback: true);
        var registry = new ScenarioConfigRegistry(fixture.Db);
        var first = await registry.ResolveWithFixedFallbackAsync("missing", "missing", Compatibility);
        var second = await registry.ResolveWithFixedFallbackAsync("missing", "missing", Compatibility);
        Assert.True(first.IsSuccess);
        Assert.True(first.Data!.UsedFixedFallback);
        Assert.Equal("fixed", first.Data.Config.ScenarioConfigId);
        Assert.Equal(first.Data.Config.ScenarioConfigVersion, second.Data!.Config.ScenarioConfigVersion);
    }

    [Fact, Trait("Category", "M4Scenario")]
    public async Task MissingProductionFallback_ReturnsExplicitConfigurationError()
    {
        await using var fixture = await Fixture.CreateAsync(includeFallback: false);
        var result = await new ScenarioConfigRegistry(fixture.Db)
            .ResolveWithFixedFallbackAsync("missing", "missing", Compatibility);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ScenarioFixedFallbackNotConfigured, result.ErrorCode);
    }

    [Fact, Trait("Category", "M4Scenario")]
    public void ResolutionContract_CannotAcceptCurrentMatchTeamProfileOrRawTelemetry()
    {
        var method = typeof(EchoProtocol.Api.Services.Interfaces.IScenarioConfigRegistry)
            .GetMethod("ResolveWithFixedFallbackAsync")!;
        Assert.DoesNotContain(method.GetParameters(), parameter =>
            parameter.ParameterType == typeof(TeamProfile)
            || parameter.ParameterType.Name.Contains("Telemetry", StringComparison.Ordinal));
    }

    private static ScenarioConfigDefinition Config(string id, ScenarioConfigSource source) => new()
    {
        ScenarioConfigId = id, ScenarioConfigVersion = $"{id}-v1", SchemaVersion = "1.1",
        PolicyVersion = ScenarioConfigValidator.SupportedPolicyVersion, ConfigSource = source,
        MapId = "test-map", MonsterType = "test-monster", ObjectiveSpawnSetId = "test-objectives",
        SupportItemBudget = 1, DetectionFillRate = 1, DetectionDecayRate = 1,
        ChaseSpeed = 1, SearchDuration = 1, RouteModifier = "test-route",
        EscapeDoorTimerSeconds = 45, FallbackConfigId = "fixed", FallbackConfigVersion = "fixed-v1",
        ContentWhitelistVersion = "test-whitelist-v1", UnityCompatibilityVersion = Compatibility,
        IsActive = true, IsProductionApproved = true, Provenance = "AUTOMATED_TEST_FIXTURE",
        CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };

    private static ScenarioContentDefinition[] Content() =>
    [
        Entry(ScenarioContentType.Map, "test-map"), Entry(ScenarioContentType.Monster, "test-monster"),
        Entry(ScenarioContentType.ObjectiveSpawnSet, "test-objectives"), Entry(ScenarioContentType.RouteModifier, "test-route")
    ];

    private static ScenarioContentDefinition Entry(ScenarioContentType type, string id) => new()
    {
        Id = Guid.NewGuid(), ContentType = type, ContentId = id,
        ContentWhitelistVersion = "test-whitelist-v1", UnityCompatibilityVersion = Compatibility,
        IsActive = true, IsProductionApproved = true, Provenance = "AUTOMATED_TEST_FIXTURE",
        CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        public AppDbContext Db { get; }
        private Fixture(SqliteConnection connection, AppDbContext db) { this.connection = connection; Db = db; }
        public static async Task<Fixture> CreateAsync(bool includeFallback)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            db.ScenarioContentDefinitions.AddRange(Content());
            if (includeFallback)
            {
                var fallback = Config("fixed", ScenarioConfigSource.Fixed);
                fallback.IsFixedFallback = true;
                db.ScenarioConfigs.Add(fallback);
            }
            await db.SaveChangesAsync();
            return new(connection, db);
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
