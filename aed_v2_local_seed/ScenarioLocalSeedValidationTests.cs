// TEMPORARY local-only test file. Place in:
// EchoProtocol.Backend/tests/EchoProtocol.Api.Tests/ScenarioLocalSeedValidationTests.cs
// Remove after testing so regular CI tests do not depend on local credentials/database.
using System;
using System.Linq;
using System.Threading.Tasks;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class ScenarioLocalSeedValidationTests
{
    private const string ConfigId = "FIXED_BASELINE_V1";
    private const string UnityVersion = "0.1.0";
    private const string Whitelist = "M2-WHITELIST-1";

    private static AppDbContext OpenLocalDb()
    {
        var connection = Environment.GetEnvironmentVariable("AED_LOCAL_DB_CONNECTION")
            ?? throw new InvalidOperationException("Set AED_LOCAL_DB_CONNECTION to a LOCAL PostgreSQL connection string");
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        if (!string.Equals(parsed.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            && parsed.Host != "127.0.0.1")
            throw new InvalidOperationException("Only localhost/127.0.0.1 is permitted for this local-only test");
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection).Options);
    }

    [Fact, Trait("Category", "AEDLocalSeed")]
    public async Task StagedCandidatePassesScenarioConfigValidator()
    {
        await using var db = OpenLocalDb();
        var config = await db.ScenarioConfigs.AsNoTracking().SingleAsync(x =>
            x.ScenarioConfigId == ConfigId && x.ScenarioConfigVersion == ConfigId);
        var contents = await db.ScenarioContentDefinitions.AsNoTracking()
            .Where(x => x.UnityCompatibilityVersion == UnityVersion
                        && x.ContentWhitelistVersion == Whitelist).ToArrayAsync();
        Assert.False(config.IsActive);
        Assert.False(config.IsProductionApproved);
        Assert.Equal(4, contents.Length);
        Assert.All(contents, item => { Assert.False(item.IsActive); Assert.False(item.IsProductionApproved); });
        Assert.Equal("1.1", config.SchemaVersion);
        Assert.Equal(ScenarioConfigValidator.SupportedPolicyVersion, config.PolicyVersion);
        Assert.Equal("M2-MAP-1", config.MapId);
        Assert.Equal("STALKER", config.MonsterType);
        Assert.Equal("DEFAULT_OBJECTIVES", config.ObjectiveSpawnSetId);
        Assert.Equal("DEFAULT_ROUTE", config.RouteModifier);
        Assert.Equal(0, config.SupportItemBudget);
        Assert.Equal(0.5, config.DetectionFillRate);
        Assert.Equal(1.0, config.DetectionDecayRate);
        Assert.Equal(9.0, config.ChaseSpeed);
        Assert.Equal(5.0, config.SearchDuration);
        Assert.Equal(45.0, config.EscapeDoorTimerSeconds);
        Assert.True(config.IsFixedFallback);
        Assert.Equal(config.ScenarioConfigId, config.FallbackConfigId);
        Assert.Equal(config.ScenarioConfigVersion, config.FallbackConfigVersion);
        // Clone via AsNoTracking and set approval in memory ONLY. Nothing is saved.
        config.IsActive = config.IsProductionApproved = true;
        foreach (var item in contents) item.IsActive = item.IsProductionApproved = true;
        var result = ScenarioConfigValidator.Validate(config, contents, UnityVersion);
        Assert.True(result.IsValid, string.Join(", ", result.ReasonCodes));
    }

    [Fact, Trait("Category", "AEDLocalSeed")]
    public async Task ApprovedFallbackResolvesThroughProductionRegistry()
    {
        await using var db = OpenLocalDb();
        var result = await new ScenarioConfigRegistry(db)
            .GetProductionFixedFallbackAsync(UnityVersion);
        Assert.True(result.IsSuccess, $"{result.ErrorCode}: {result.Message}");
        Assert.Equal(ConfigId, result.Data!.ScenarioConfigId);
        Assert.Equal(ConfigId, result.Data.ScenarioConfigVersion);
    }
}
