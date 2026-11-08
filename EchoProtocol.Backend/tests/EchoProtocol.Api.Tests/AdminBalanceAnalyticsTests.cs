using System.Text.Json;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class AdminBalanceAnalyticsTests
{
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
            ApprovalStatus = "APPROVED", ApplyStatus = "APPLIED", ReceiptId = Guid.NewGuid()
        };
    }
}
