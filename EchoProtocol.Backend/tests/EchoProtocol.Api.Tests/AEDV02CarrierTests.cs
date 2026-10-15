using System.Text.Json;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Data.Telemetry;
using EchoProtocol.Api.Services;
using MongoDB.Bson;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed partial class ScenarioServiceTests
{
    [Theory, Trait("Category", "AEDV02")]
    [InlineData("valid", null)]
    [InlineData("missing-steal", "HOLD_TOOL_CARRIER_CENSUS_MISMATCH")]
    [InlineData("unknown-minion", "HOLD_TOOL_CARRIER_SOURCE_INVALID")]
    [InlineData("wrong-drop", "HOLD_TOOL_CARRIER_SOURCE_INVALID")]
    [InlineData("missing-drop", "HOLD_TOOL_CARRIER_CENSUS_MISMATCH")]
    public async Task V02_ActualDropStealReleaseLineageIsIndependentlyReplayed(string mode, string? rejection)
    {
        await using var f = await ValidV02Fixture();
        var (world, request) = await V02WorldEvidence(f, geometry: true);
        var events = world.ToList(); var user = f.Db.MatchPlayerBindings.First(x => x.MatchId == f.MatchId).UserId;
        foreach (var item in events.Where(x => x.EventType == "AED_V02_WORLD_SAMPLE"))
        {
            var sample = JsonSerializer.Deserialize<AEDV02WorldSample>(item.ValueJson["data"]["world"].ToJson())!;
            sample.Minions = new[] { new AEDV02ThreatSource { NetworkId = "minion:1", State = "IDLE", Zone = 1 } };
            var aid = sample.Tools.Single(x => x.ToolId == 3);
            if (sample.Tick >= 900 && sample.Tick < 1200)
            { aid.Location = "Player"; aid.OwnerUserId = user; aid.MarkerId = string.Empty; sample.Players.Single(x => x.UserId == user).HeldToolInstanceId = aid.InstanceId; }
            if (sample.Tick >= 1200)
            { aid.InstanceId = "drop:aid"; aid.Origin = "PlayerDrop"; aid.MarkerId = string.Empty; aid.CarrierMinionId = sample.Tick < 1800 ? "minion:1" : string.Empty; }
            item.ValueJson["data"]["world"] = BsonDocument.Parse(JsonSerializer.Serialize(sample));
        }
        void Tool(string action, long tick, string id, string? parent = null)
        {
            var data = new BsonDocument { ["action"] = action, ["instanceId"] = id, ["toolId"] = 3, ["zone"] = 1,
                ["ownerUserId"] = user.ToString("D"), ["remainingCharges"] = BsonNull.Value };
            if (parent != null) data["parentInstanceId"] = parent;
            events.Add(V02Event(f.MatchId, "TEAM_TOOL_LIFECYCLE", 0, Now.AddSeconds((tick - 2400) / 60d), "1.2",
                new BsonDocument { ["context"] = new BsonDocument { ["phase"] = "CORE_COLLECTION", ["authorityTick"] = tick }, ["data"] = data }));
        }
        void Carrier(string action, long tick, string minion)
        {
            var source = new AEDV02ToolCarrierSource { ToolInstanceId = "drop:aid", MinionNetworkId = minion, Action = action, Tick = tick };
            events.Add(V02Event(f.MatchId, "AED_V02_TOOL_CARRIER", 0, Now.AddSeconds((tick - 2400) / 60d), "1.3",
                new BsonDocument { ["context"] = new BsonDocument { ["phase"] = "CORE_COLLECTION", ["authorityTick"] = tick },
                    ["data"] = new BsonDocument { ["carrier"] = BsonDocument.Parse(JsonSerializer.Serialize(source)) } }));
        }
        events.Add(V02Event(f.MatchId, "MINION_LIFECYCLE", 0, Now.AddSeconds(-31), "1.2", new BsonDocument {
            ["context"] = new BsonDocument { ["phase"] = "CORE_COLLECTION", ["authorityTick"] = 599 },
            ["data"] = new BsonDocument { ["networkInstanceId"] = "minion:1", ["zone"] = 1, ["state"] = "SPAWNED" } }));
        Tool("PICKUP", 900, "tool:3"); Tool("DROP", 1200, "drop:aid", "tool:3");
        if (mode != "missing-steal") Carrier("STEAL", 1200, mode == "unknown-minion" ? "phantom" : "minion:1");
        if (mode != "missing-drop") Carrier("DROP", 1800, mode == "wrong-drop" ? "phantom" : "minion:1");
        var ordered = events.OrderBy(x => x.Ts).ThenBy(x => x.EventType == "AED_V02_WORLD_SAMPLE" ? 1 : 0)
            .Select((x, i) => V02Event(f.MatchId, x.EventType, i + 1, x.Ts, x.SchemaVersion, x.ValueJson, x.UserId));
        var accepted = await TelemetryServiceTests.IngestV02Trace(f.MatchId, ordered, Now, V02TelemetryAuthority(f), f.HostId);
        var result = await new AEDV02SourceVerifier(f.Db, new AcceptedEvidenceRepository(accepted), new FixedClock(Now)).VerifyAsync(f.MatchId, request);
        if (rejection == null) { Assert.True(result.IsSuccess, result.Message); Assert.True(result.Data!.SafeWindow); return; }
        Assert.False(result.IsSuccess); Assert.Equal(rejection, result.Message); Assert.Empty(f.Db.AEDV02VerifiedSources);
    }
}
