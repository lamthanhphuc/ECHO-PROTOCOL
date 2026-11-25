using System.Text.Json;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class AdminBalanceAnalyticsTests
{
    [Theory]
    [InlineData("hash", "INCOMPATIBLE")]
    [InlineData("map", "INCOMPATIBLE")]
    [InlineData("difficulty", "INCOMPATIBLE")]
    [InlineData("team", "MISSING_METADATA")]
    [InlineData("version", "INCOMPATIBLE")]
    [InlineData("build", "INCOMPATIBLE")]
    [InlineData("missingHash", "MISSING_METADATA")]
    public async Task PolicyComparisonIncludesHoldAndRejectsIncompatibleOrMissingMetadata(string difference, string expected)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        void Add(string mode, string hash, bool hold)
        {
            var id = Guid.NewGuid();
            db.MatchResults.Add(new MatchResult
            {
                MatchId = id, DurationSeconds = 300, PlayerCount = 1, EndedAtUtc = DateTime.UtcNow,
                Match = new MatchAuthorityBinding
                {
                    MatchId = id, ResolutionMode = mode, MatchDataPolicy = "STANDARD",
                    MapId = "Map1", Difficulty = "Normal", BalanceSource = "REMOTE_FIXED",
                    BalanceVersion = "v1", BalanceHash = hash, BalanceBuildVersion = "build1",
                    BalanceProtocolVersion = "RFB-1", AdaptivePolicyConfigVersion = mode == "ADAPTIVE" ? "policy1" : null
                },
                Players = { new MatchResultPlayer { UserId = Guid.NewGuid(), Survived = true } }
            });
            if (hold) db.AEDV02Decisions.Add(new AEDV02Decision
            {
                DecisionId = Guid.NewGuid(), MatchId = id, ApprovalStatus = "HOLD", ApplyStatus = "NOT_APPLICABLE",
                ReasonCode = "HOLD_RULE", PolicyVersion = "AED-V02", CatalogVersion = "catalog1",
                SceneManifestFingerprint = "manifest1"
            });
        }
        Add("FIXED", new string('a', 64), false);
        Add("ADAPTIVE", new string('a', 64), true);
        await db.SaveChangesAsync();
        var service = new AdminBalanceAnalyticsService(db);
        var compatible = JsonSerializer.SerializeToElement((await service.GetAsync(30, null, null, null, null, false, default)).Data);
        Assert.Equal("COMPARABLE_DESCRIPTIVE", compatible.GetProperty("qualification").GetString());
        Assert.Equal(1, compatible.GetProperty("AdaptiveMatches").GetInt32());
        var adaptive = db.MatchResults.Single(x => x.Match!.ResolutionMode == "ADAPTIVE");
        switch (difference)
        {
            case "hash": adaptive.Match.BalanceHash = new string('b', 64); break;
            case "map": adaptive.Match.MapId = "Map2"; break;
            case "difficulty": adaptive.Match.Difficulty = "Hard"; break;
            case "team": adaptive.PlayerCount = 2; break;
            case "version": adaptive.Match.BalanceVersion = "v2"; break;
            case "build": adaptive.Match.BalanceBuildVersion = "build2"; break;
            case "missingHash": adaptive.Match.BalanceHash = null; break;
        }
        await db.SaveChangesAsync();
        var incompatible = JsonSerializer.SerializeToElement((await service.GetAsync(30, null, null, null, null, false, default)).Data);
        Assert.Equal(expected, incompatible.GetProperty("qualification").GetString());
    }

    [Fact]
    public async Task EmptyAndSingleGroupAreNotComparable()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        var service = new AdminBalanceAnalyticsService(db);
        Assert.Equal("NO_DATA", JsonSerializer.SerializeToElement((await service.GetAsync(30, null, null, null, null, false, default)).Data)
            .GetProperty("qualification").GetString());
        var id = Guid.NewGuid();
        db.MatchResults.Add(new MatchResult { MatchId = id, EndedAtUtc = DateTime.UtcNow,
            Match = new MatchAuthorityBinding { MatchId = id, ResolutionMode = "FIXED" } });
        await db.SaveChangesAsync();
        Assert.Equal("ONLY_FIXED", JsonSerializer.SerializeToElement((await service.GetAsync(30, null, null, null, null, false, default)).Data)
            .GetProperty("qualification").GetString());
    }

    [Fact]
    public async Task PendingFailedAndIncompleteReceiptsDoNotCountAsApplied()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        var id = Guid.NewGuid();
        db.MatchResults.Add(new MatchResult { MatchId = id, EndedAtUtc = DateTime.UtcNow,
            Match = new MatchAuthorityBinding { MatchId = id, ResolutionMode = "ADAPTIVE" } });
        foreach (var status in new[] { "PENDING", "FAILED_EFFECT", "APPLIED" })
            db.AEDV02Decisions.Add(new AEDV02Decision { DecisionId = Guid.NewGuid(), MatchId = id,
                ApprovalStatus = "APPROVED", ApplyStatus = status, ChangedKey = "ChaseSpeed", ReceiptId = Guid.NewGuid() });
        await db.SaveChangesAsync();
        var payload = JsonSerializer.SerializeToElement((await new AdminBalanceAnalyticsService(db)
            .GetAsync(30, null, null, null, null, false, default)).Data);
        Assert.Equal(3, payload.GetProperty("Approved").GetInt32());
        Assert.Equal(1, payload.GetProperty("FailedEffect").GetInt32());
        Assert.Equal(0, payload.GetProperty("Applied").GetInt32());
        Assert.Equal("ADAPTIVE_UNVERIFIED", payload.GetProperty("cohorts")[0].GetProperty("Group").GetString());
    }

    [Fact]
    public async Task FixedFallbackIsSeparateFromHoldAndStaysInAdaptivePolicyCount()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        var id = Guid.NewGuid();
        db.MatchResults.Add(new MatchResult { MatchId = id, EndedAtUtc = DateTime.UtcNow,
            Match = new MatchAuthorityBinding { MatchId = id, ResolutionMode = "ADAPTIVE" } });
        db.ScenarioDecisions.Add(new ScenarioDecision { DecisionId = Guid.NewGuid(), MatchId = id,
            ResolutionMode = EchoProtocol.Api.Enums.ScenarioResolutionMode.Adaptive,
            IsCurrent = true, UsedFixedFallback = true, FallbackReasonCode = "HOLD_RULE" });
        await db.SaveChangesAsync();
        var payload = JsonSerializer.SerializeToElement((await new AdminBalanceAnalyticsService(db)
            .GetAsync(30, null, null, null, null, false, default)).Data);
        Assert.Equal(1, payload.GetProperty("AdaptiveMatches").GetInt32());
        Assert.Equal("ADAPTIVE_FALLBACK", payload.GetProperty("cohorts")[0].GetProperty("Group").GetString());
    }

    [Fact]
    public async Task TruncatedSampleCannotBeQualifiedForComparison()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        for (var index = 0; index < 1001; index++)
        {
            var id = Guid.NewGuid();
            db.MatchResults.Add(new MatchResult { MatchId = id, EndedAtUtc = DateTime.UtcNow,
                Match = new MatchAuthorityBinding { MatchId = id, ResolutionMode = "FIXED" } });
        }
        await db.SaveChangesAsync();
        var payload = JsonSerializer.SerializeToElement((await new AdminBalanceAnalyticsService(db)
            .GetAsync(30, null, null, null, null, false, default)).Data);
        Assert.Equal(1000, payload.GetProperty("Total").GetInt32());
        Assert.True(payload.GetProperty("truncated").GetBoolean());
        Assert.Equal("TRUNCATED", payload.GetProperty("qualification").GetString());
    }

    [Fact]
    public async Task CohortsSeparateVerifiedChangesNeutralReceiptsAndTestMatches()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        var fixedMatch = AddResult("FIXED", "STANDARD");
        var applied = AddResult("ADAPTIVE", "STANDARD");
        var neutral = AddResult("ADAPTIVE", "STANDARD");
        var trial = AddResult("FIXED", "TEST_ONLY");
        db.AEDV02Decisions.AddRange(Receipt(applied, "ChaseSpeed"), Receipt(neutral, ""), Receipt(trial, "ChaseSpeed"));
        await db.SaveChangesAsync();

        var service = new AdminBalanceAnalyticsService(db);
        var result = await service.GetAsync(30, null, null, null, null, false, default);
        var payload = JsonSerializer.SerializeToElement(result.Data);
        Assert.True(result.IsSuccess);
        Assert.Equal(3, payload.GetProperty("Total").GetInt32());
        Assert.Equal(2, payload.GetProperty("Applied").GetInt32());
        var groups = payload.GetProperty("cohorts").EnumerateArray().ToDictionary(
            x => x.GetProperty("Group").GetString()!, x => x.GetProperty("RecentMatchIds")[0].GetGuid());
        Assert.Equal(fixedMatch, groups["FIXED"]);
        Assert.Equal(applied, groups["ADAPTIVE_APPLIED"]);
        Assert.Equal(neutral, groups["ADAPTIVE_UNVERIFIED"]);

        var testResult = await service.GetAsync(30, null, null, null, null, true, default);
        Assert.Equal(1, JsonSerializer.SerializeToElement(testResult.Data).GetProperty("Total").GetInt32());

        Guid AddResult(string mode, string policy)
        {
            var id = Guid.NewGuid();
            db.MatchResults.Add(new MatchResult
            {
                MatchId = id, DurationSeconds = 300, PlayerCount = 1, EndedAtUtc = DateTime.UtcNow,
                Match = new MatchAuthorityBinding { MatchId = id, ResolutionMode = mode, MatchDataPolicy = policy }
            });
            return id;
        }

        static AEDV02Decision Receipt(Guid matchId, string key) => new()
        {
            DecisionId = Guid.NewGuid(), MatchId = matchId, ChangedKey = key,
            ApprovalStatus = "APPROVED", ApplyStatus = "APPLIED", ReceiptId = Guid.NewGuid(),
            EffectEventId = Guid.NewGuid(), AppliedTick = 10, AppliedAtUtc = DateTime.UtcNow
        };
    }
}
