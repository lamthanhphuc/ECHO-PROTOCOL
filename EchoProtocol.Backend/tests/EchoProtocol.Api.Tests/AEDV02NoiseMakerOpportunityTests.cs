using System.Text.Json;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Services;
using MongoDB.Bson;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed partial class ScenarioServiceTests
{
    [Theory, Trait("Category", "AEDV02")]
    [InlineData("diverted", "Success")]
    [InlineData("not-heard", "Censored")]
    [InlineData("stationary", "Censored")]
    [InlineData("wrong-actor", null)]
    [InlineData("missing-activation", null)]
    [InlineData("missing-charge-use", null)]
    [InlineData("wrong-goal", null)]
    [InlineData("off-mesh-goal", null)]
    public async Task V02_NoiseMakerOutcomeRequiresRealActivationChargeNoiseGoalAndMovement(string mode, string? outcome)
    {
        await using var f = await ValidV02Fixture();
        var (baseline, request) = await V02WorldEvidence(f, geometry: true, pilot: true);
        var trace = baseline.ToList();
        string NoiseId(int pulse) => f.MatchId.ToString("N") + ":noise:team-tool:owner-object:noise-maker:1:NOISE_MAKER:" + pulse;
        foreach (var item in trace)
        {
            if (item.EventType == "TEAM_TOOL_LIFECYCLE" && item.ValueJson["data"]["toolId"] == 2)
                item.ValueJson["data"]["remainingCharges"] = 2;
            if (item.EventType != "AED_V02_WORLD_SAMPLE") continue;
            var world = JsonSerializer.Deserialize<AEDV02WorldSample>(item.ValueJson["data"]["world"].ToJson())!;
            int second = (int)(world.Tick / 60 - 10);
            var tool = world.Tools.Single(x => x.ToolId == 2); tool.RemainingCharges = second < 5 ? 2 : 1;
            if (second >= 1)
            {
                tool.Location = "Player"; tool.OwnerUserId = f.HostId; tool.MarkerId = string.Empty;
                world.Players.Single(x => x.UserId == f.HostId).HeldToolInstanceId = tool.InstanceId;
            }
            bool chasing = second is >= 1 and <= 4;
            var stalker = new AEDV02ThreatSource { NetworkId = "stalker", Zone = 1, State = chasing ? "CHASE" : "SEARCH",
                TargetUserId = chasing ? f.HostId : null, EpisodeId = chasing ? "pre-tool-chase" : string.Empty, X = 1, Z = 1 };
            if (second is >= 6 and <= 9 && mode != "not-heard")
            {
                stalker.InvestigationNoiseId = NoiseId(second < 8 ? 1 : 2);
                stalker.DestinationX = mode == "wrong-goal" ? 9 : mode == "off-mesh-goal" ? 100 : 5;
                stalker.DestinationZ = 1;
                stalker.X = mode == "stationary" ? 1 : second - 5;
            }
            world.Stalkers = [stalker];
            item.ValueJson["data"]["world"] = BsonDocument.Parse(JsonSerializer.Serialize(world));
        }
        void Add(string type, int second, string schema, BsonDocument data, Guid? user = null, string? reason = null, BsonDocument? extra = null)
        {
            var context = new BsonDocument { ["phase"] = "CORE_COLLECTION", ["authorityTick"] = 600 + second * 60 };
            if (extra != null) context.Merge(extra);
            trace.Add(V02Event(f.MatchId, type, 0, Now.AddSeconds(second - 30), schema,
                new BsonDocument { ["context"] = context, ["data"] = data }, user, reason));
        }
        Add("TEAM_TOOL_LIFECYCLE", 1, "1.2", new BsonDocument { ["action"] = "PICKUP", ["instanceId"] = "tool:2", ["toolId"] = 2,
            ["zone"] = 1, ["ownerUserId"] = f.HostId.ToString("D"), ["remainingCharges"] = 2 });
        if (mode != "missing-activation") Add("TEAM_TOOL_USED", 5, "1.1", new BsonDocument { ["toolType"] = "NOISE_MAKER" },
            f.HostId, "PLAYER_ACTIVATED_TOOL", new BsonDocument { ["occurrenceKey"] = "player:owner-object:tool:1" });
        if (mode != "missing-charge-use") Add("TEAM_TOOL_LIFECYCLE", 5, "1.2", new BsonDocument { ["action"] = "USE", ["instanceId"] = "tool:2",
            ["toolId"] = 2, ["zone"] = 1, ["ownerUserId"] = f.HostId.ToString("D"), ["remainingCharges"] = 1 });
        for (int pulse = 1; pulse <= 8; pulse++)
            Add("NOISE_EMITTED", 4 + pulse * 2, "1.1", new BsonDocument { ["noiseEventId"] = NoiseId(pulse), ["noiseType"] = "NOISE_MAKER",
                ["loudness"] = 1d, ["hearingRadius"] = 100d }, mode == "wrong-actor" ? f.PlayerId : f.HostId, "NOISE_MAKER_USED",
                new BsonDocument { ["position"] = new BsonDocument { ["x"] = mode == "off-mesh-goal" ? 100d : 5d, ["y"] = 0d, ["z"] = 1d } });
        var ordered = trace.OrderBy(x => x.Ts).ThenBy(x => x.EventType == "AED_V02_WORLD_SAMPLE" ? 2 : x.EventType == "TEAM_TOOL_USED" ? 0 : 1)
            .Select((x, index) => V02Event(f.MatchId, x.EventType, index + 1, x.Ts, x.SchemaVersion, x.ValueJson, x.UserId, x.ReasonCode));
        var accepted = await TelemetryServiceTests.IngestV02Trace(f.MatchId, ordered, Now, V02TelemetryAuthority(f), f.HostId);
        var result = await new AEDV02SourceVerifier(f.Db, new AcceptedEvidenceRepository(accepted), new FixedClock(Now)).VerifyAsync(f.MatchId, request);
        if (outcome == null)
        {
            Assert.False(result.IsSuccess); Assert.Equal("HOLD_TOOL_NOISE_LINEAGE_INVALID", result.Message); return;
        }
        Assert.True(result.IsSuccess, result.Message);
        var opportunity = Assert.Single(result.Data!.ResolvedOpportunities!.Where(x => x.Axis == "Tool" && x.ToolId == 2));
        Assert.Equal(f.HostId, opportunity.UserId); Assert.Equal(outcome, opportunity.Resolution);
        Assert.Equal("Tool:2:stalker:pre-tool-chase", opportunity.Id);
    }
}
