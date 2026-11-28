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
        Assert.Equal(2, incompatible.GetProperty("Total").GetInt32());
        Assert.Equal(1, incompatible.GetProperty("FixedMatches").GetInt32());
        Assert.Equal(1, incompatible.GetProperty("AdaptiveMatches").GetInt32());
        var separated = incompatible.GetProperty("cohorts").EnumerateArray()
            .ToDictionary(item => item.GetProperty("Group").GetString()!,
                item => item.GetProperty("RecentMatchIds")[0].GetGuid());
        Assert.Contains("FIXED", separated.Keys);
        Assert.Contains("ADAPTIVE_HOLD", separated.Keys);
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
        var incomplete = new AEDV02Decision
        {
            DecisionId = Guid.NewGuid(), MatchId = id, ApprovalStatus = "APPROVED", ApplyStatus = "APPLIED",
            ChangedKey = "ChaseSpeed", ReceiptId = Guid.NewGuid(), EffectEventId = Guid.NewGuid(),
            AppliedTick = 1
        };
        var emptyEvidence = new AEDV02Decision
        {
            DecisionId = Guid.NewGuid(), MatchId = id, ApprovalStatus = "APPROVED", ApplyStatus = "APPLIED",
            ChangedKey = "ChaseSpeed", ReceiptId = Guid.Empty, EffectEventId = Guid.Empty,
            AppliedTick = 1, AppliedAtUtc = DateTime.UtcNow
        };
        db.AEDV02Decisions.AddRange(incomplete, emptyEvidence);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var beforeRows = await db.AEDV02Decisions.AsNoTracking().OrderBy(x => x.DecisionId)
            .Select(x => new { x.DecisionId, x.ApplyStatus, x.ReceiptId, x.EffectEventId, x.AppliedTick, x.AppliedAtUtc })
            .ToArrayAsync();
        var payload = JsonSerializer.SerializeToElement((await new AdminBalanceAnalyticsService(db)
            .GetAsync(30, null, null, null, null, false, default)).Data);
        Assert.Equal(5, payload.GetProperty("Approved").GetInt32());
        Assert.Equal(1, payload.GetProperty("FailedEffect").GetInt32());
        Assert.Equal(0, payload.GetProperty("Applied").GetInt32());
        Assert.Equal("ADAPTIVE_UNVERIFIED", payload.GetProperty("cohorts")[0].GetProperty("Group").GetString());
        Assert.Equal(beforeRows, await db.AEDV02Decisions.AsNoTracking().OrderBy(x => x.DecisionId)
            .Select(x => new { x.DecisionId, x.ApplyStatus, x.ReceiptId, x.EffectEventId, x.AppliedTick, x.AppliedAtUtc })
            .ToArrayAsync());
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
    public async Task AnalyticsReadsDoNotMutateHistoricalMatchOrDecisionRecords()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);
        var matchId = Guid.NewGuid();
        var endedAt = DateTime.UtcNow;
        var match = new MatchResult
        {
            MatchId = matchId,
            DurationSeconds = 300,
            PlayerCount = 1,
            EndedAtUtc = endedAt,
            Outcome = EchoProtocol.Api.Enums.MatchOutcome.WIN,
            Match = new MatchAuthorityBinding
            {
                MatchId = matchId,
                ResolutionMode = "ADAPTIVE",
                MatchDataPolicy = "STANDARD",
                AdaptivePolicyConfigVersion = "AED_ADMIN_TEST"
            },
            Players = { new MatchResultPlayer { UserId = Guid.NewGuid(), Survived = true } }
        };
        var decision = new AEDV02Decision
        {
            DecisionId = Guid.NewGuid(),
            MatchId = matchId,
            ApprovalStatus = "HOLD",
            ApplyStatus = "NOT_APPLICABLE",
            ReasonCode = "HOLD_RULE",
            PolicyVersion = "AED-V02",
            CatalogVersion = "catalog-before"
        };
        db.MatchResults.Add(match);
        db.AEDV02Decisions.Add(decision);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = new AdminBalanceAnalyticsService(db);
        Assert.True((await service.GetAsync(30, null, null, null, null, false, default)).IsSuccess);
        Assert.True((await service.PlayerAsync(Guid.NewGuid(), 1, default)).IsSuccess);
        Assert.DoesNotContain(db.ChangeTracker.Entries(),
            entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);

        var storedMatch = await db.MatchResults.AsNoTracking().SingleAsync(item => item.MatchId == matchId);
        var storedBinding = await db.MatchAuthorityBindings.AsNoTracking()
            .SingleAsync(item => item.MatchId == matchId);
        var storedDecision = await db.AEDV02Decisions.AsNoTracking()
            .SingleAsync(item => item.DecisionId == decision.DecisionId);
        Assert.Equal(endedAt, storedMatch.EndedAtUtc);
        Assert.Equal(EchoProtocol.Api.Enums.MatchOutcome.WIN, storedMatch.Outcome);
        Assert.Equal("AED_ADMIN_TEST", storedBinding.AdaptivePolicyConfigVersion);
        Assert.Equal("HOLD", storedDecision.ApprovalStatus);
        Assert.Equal("catalog-before", storedDecision.CatalogVersion);
    }

    [Fact]
    public async Task CohortsSeparateVerifiedChangesNeutralReceiptsAndTestMatches()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        var cutoff = DateTime.UtcNow.AddDays(-30);
        var fixedMatch = AddResult("FIXED", "STANDARD", cutoff.AddDays(1), "Scene_A");
        var applied = AddResult("ADAPTIVE", "STANDARD", cutoff.AddDays(2), "Scene_A");
        var neutral = AddResult("ADAPTIVE", "STANDARD", cutoff.AddDays(3), "Scene_A");
        var trial = AddResult("FIXED", "TEST_ONLY", cutoff.AddDays(4), "Scene_A");
        var old = AddResult("ADAPTIVE", "STANDARD", cutoff.AddSeconds(-1), "Scene_A");
        var wrongMap = AddResult("FIXED", "STANDARD", cutoff.AddDays(5), "Scene_B");
        db.AEDV02Decisions.AddRange(Receipt(applied, "ChaseSpeed"), Receipt(neutral, ""), Receipt(trial, "ChaseSpeed"));
        var mismatched = Receipt(applied, "ChaseSpeed");
        mismatched.ReceiptId = Guid.Empty;
        mismatched.EffectEventId = Guid.NewGuid();
        db.AEDV02Decisions.Add(mismatched);
        await db.SaveChangesAsync();

        var service = new AdminBalanceAnalyticsService(db);
        var result = await service.GetAsync(30, "Scene_A", "Normal", 1, null, false, default);
        var payload = JsonSerializer.SerializeToElement(result.Data);
        Assert.True(result.IsSuccess);
        Assert.Equal(3, payload.GetProperty("Total").GetInt32());
        Assert.Equal(2, payload.GetProperty("Applied").GetInt32());
        var groups = payload.GetProperty("cohorts").EnumerateArray().ToDictionary(
            x => x.GetProperty("Group").GetString()!, x => x.GetProperty("RecentMatchIds")[0].GetGuid());
        Assert.Equal(fixedMatch, groups["FIXED"]);
        Assert.Equal(applied, groups["ADAPTIVE_APPLIED"]);
        Assert.Equal(neutral, groups["ADAPTIVE_UNVERIFIED"]);
        var gathered = groups.Values.ToHashSet();
        Assert.Equal(new[] { fixedMatch, applied, neutral }.ToHashSet(), gathered);
        Assert.DoesNotContain(old, gathered);
        Assert.DoesNotContain(wrongMap, gathered);
        Assert.DoesNotContain(trial, gathered);

        var testResult = await service.GetAsync(30, "Scene_A", "Normal", 1, null, true, default);
        var testPayload = JsonSerializer.SerializeToElement(testResult.Data);
        Assert.Equal(1, testPayload.GetProperty("Total").GetInt32());
        Assert.Equal(trial, Assert.Single(testPayload.GetProperty("cohorts").EnumerateArray()
            .SelectMany(item => item.GetProperty("RecentMatchIds").EnumerateArray())
            .Select(item => item.GetGuid())));

        Guid AddResult(string mode, string policy, DateTime endedAt, string map)
        {
            var id = Guid.NewGuid();
            db.MatchResults.Add(new MatchResult
            {
                MatchId = id, DurationSeconds = 300, PlayerCount = 1, EndedAtUtc = endedAt,
                Match = new MatchAuthorityBinding
                    { MatchId = id, ResolutionMode = mode, MatchDataPolicy = policy, MapId = map, Difficulty = "Normal" },
                Players = { new MatchResultPlayer { UserId = Guid.NewGuid(), Survived = true } }
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
