using System.Text.Json;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Services;
using MongoDB.Bson;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed partial class ScenarioServiceTests
{
    [Theory, Trait("Category", "AEDV02")]
    [InlineData("resolved", "Success")]
    [InlineData("slow", "Failure")]
    [InlineData("unobserved-onset", "Censored")]
    [InlineData("track-during-recovery", "Censored")]
    [InlineData("missing-impact", null)]
    [InlineData("wrong-actor", null)]
    public async Task V02_MinionOpportunityUsesAcceptedCausalConsequencesAndActualSafeRecovery(string mode, string? resolution)
    {
        await using var f = await ValidV02Fixture();
        var (baseline, request) = await V02WorldEvidence(f, geometry: true);
        var user = f.HostId;
        var trace = baseline.ToList();
        foreach (var item in trace.Where(x => x.EventType == "AED_V02_WORLD_SAMPLE"))
        {
            var world = JsonSerializer.Deserialize<AEDV02WorldSample>(item.ValueJson["data"]["world"].ToJson())!;
            world.OpportunityContractVersion = AEDV02PilotOpportunities.Version;
            int second = (int)(world.Tick / 60 - 10);
            bool active = second is >= 1 and <= 10 || mode == "unobserved-onset" && second == 0;
            bool tracking = mode == "track-during-recovery" && second is >= 11 and <= 20;
            world.Minions = [new AEDV02ThreatSource { NetworkId = "minion:1", Zone = 1,
                State = active ? "HARASS" : tracking ? "TRACK" : "FLEE",
                EpisodeId = active || tracking ? "minion-episode" : string.Empty, TargetUserId = active || tracking ? user : null }];
            if (second >= 10 && mode is "slow" or "missing-impact" or "wrong-actor")
                world.Players.Single(x => x.UserId == user).SlowUntilTick = 1320;
            item.ValueJson["data"]["world"] = BsonDocument.Parse(JsonSerializer.Serialize(world));
        }
        trace.Add(V02Event(f.MatchId, "MINION_LIFECYCLE", 0, Now.AddSeconds(-31), "1.2", new BsonDocument {
            ["context"] = new BsonDocument { ["phase"] = "CORE_COLLECTION", ["authorityTick"] = 599 },
            ["data"] = new BsonDocument { ["networkInstanceId"] = "minion:1", ["zone"] = 1, ["state"] = "SPAWNED" } }));
        if (mode is "slow" or "wrong-actor")
        {
            var impact = new AEDV02CausalImpact { UserId = user, EntityId = mode == "wrong-actor" ? "phantom" : "minion:1",
                Kind = "MINION_SLOW", Tick = 1200, UntilTick = 1320 };
            trace.Add(V02Event(f.MatchId, "AED_V02_CAUSAL_IMPACT", 0, Now.AddSeconds(-20), "1.3", new BsonDocument {
                ["context"] = new BsonDocument { ["phase"] = "CORE_COLLECTION", ["authorityTick"] = 1200 },
                ["data"] = new BsonDocument { ["impact"] = BsonDocument.Parse(JsonSerializer.Serialize(impact)) } }));
        }
        var ordered = trace.OrderBy(x => x.Ts).ThenBy(x => x.EventType == "AED_V02_WORLD_SAMPLE" ? 1 : 0)
            .Select((x, ordinal) => V02Event(f.MatchId, x.EventType, ordinal + 1, x.Ts, x.SchemaVersion, x.ValueJson, x.UserId));
        var accepted = await TelemetryServiceTests.IngestV02Trace(f.MatchId, ordered, Now, V02TelemetryAuthority(f), f.HostId);
        var result = await new AEDV02SourceVerifier(f.Db, new AcceptedEvidenceRepository(accepted), new FixedClock(Now)).VerifyAsync(f.MatchId, request);
        if (resolution == null)
        {
            Assert.False(result.IsSuccess);
            Assert.Equal("HOLD_CAUSAL_IMPACT_LINEAGE_INVALID", result.Message);
            return;
        }
        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.Data!.PressureComplete);
        var opportunity = Assert.Single(result.Data.ResolvedOpportunities!);
        Assert.Equal("Minion", opportunity.Axis);
        Assert.Equal(user, opportunity.UserId);
        Assert.Equal(resolution, opportunity.Resolution);
        if (resolution == "Failure") Assert.Equal(1200, opportunity.EndTick);
    }
}
