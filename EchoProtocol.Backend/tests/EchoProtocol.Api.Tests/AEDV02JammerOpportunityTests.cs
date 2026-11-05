using System.Text.Json;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Services;
using MongoDB.Bson;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed partial class ScenarioServiceTests
{
    [Theory, Trait("Category", "AEDV02")]
    [InlineData("blocked", "Success")]
    [InlineData("unused", "Censored")]
    [InlineData("stationary-progress", "Censored")]
    [InlineData("wrong-target", "Censored")]
    [InlineData("missing-activation", null)]
    [InlineData("missing-charge", null)]
    [InlineData("changed-deployer", null)]
    [InlineData("prior-phase", "Success")]
    public async Task V02_JammerRequiresOwnedChargeAndActualThreatDelay(string mode, string? expected)
    {
        await using var f = await ValidV02Fixture();
        var (baseline, request) = await V02WorldEvidence(f, geometry: true, pilot: true);
        var content = f.Db.AEDV02SceneContents.Single();
        var manifest = JsonSerializer.Deserialize<AEDV02SceneManifest>(content.ManifestJson)!;
        manifest.JammerBreakSeconds = 3;
        string json = JsonSerializer.Serialize(manifest), scene = AEDV02Plan.Hash(json);
        f.Db.AEDV02SceneContents.Add(new() { Fingerprint = scene, SceneVersion = manifest.SceneVersion, ManifestJson = json, RegisteredAtUtc = Now });
        await f.Db.SaveChangesAsync();
        var trace = baseline.ToList();
        foreach (var item in trace)
        {
            if (item.EventType == "TEAM_TOOL_LIFECYCLE" && item.ValueJson["data"]["toolId"] == 4)
                item.ValueJson["data"]["remainingCharges"] = 2;
            if (item.EventType != "AED_V02_WORLD_SAMPLE") continue;
            var world = JsonSerializer.Deserialize<AEDV02WorldSample>(item.ValueJson["data"]["world"].ToJson())!;
            world.SceneManifestFingerprint = scene;
            int second = (int)(world.Tick / 60 - 10);
            var tool = world.Tools.Single(x => x.ToolId == 4); tool.RemainingCharges = second < 5 ? 2 : 1;
            if (second >= 1)
            {
                tool.Location = "Player"; tool.OwnerUserId = f.HostId; tool.MarkerId = string.Empty;
                world.Players.Single(x => x.UserId == f.HostId).HeldToolInstanceId = tool.InstanceId;
            }
            bool chase = second >= 1;
            var stalker = new AEDV02ThreatSource { NetworkId = "stalker", Zone = 1, State = chase ? "CHASE" : "PATROL",
                TargetUserId = chase ? (second >= 6 && mode == "wrong-target" ? f.PlayerId : f.HostId) : null,
                EpisodeId = chase ? "jammer-chase" : string.Empty, X = 1, Z = 1 };
            if (second >= 5)
                world.Jammers = [new AEDV02JammerSource { MatchId = f.MatchId, PhaseName = "CORE_COLLECTION", NetworkId = "jammer", UserId = mode == "changed-deployer" && second >= 7 ? f.PlayerId : f.HostId,
                    Occurrence = "player:owner-object:tool:1", DeploymentTick = 900, Active = second < 9, ColliderBlocking = second < 9,
                    BreakSeconds = 3, X = 2, Z = 1 }];
            if (second is >= 6 and <= 8 && mode != "unused")
            { stalker.JammerBlockerId = "jammer"; stalker.JammerProgress = mode == "stationary-progress" ? 0 : (second - 6) / 3d; }
            world.Stalkers = [stalker];
            item.ValueJson["data"]["world"] = BsonDocument.Parse(JsonSerializer.Serialize(world));
        }
        void Add(string type, int second, string schema, BsonDocument data, Guid? user = null, string? reason = null, string? occurrence = null)
        {
            var context = new BsonDocument { ["phase"] = "CORE_COLLECTION", ["authorityTick"] = 600 + second * 60 };
            if (occurrence != null) context["occurrenceKey"] = occurrence;
            trace.Add(V02Event(f.MatchId, type, 0, Now.AddSeconds(second - 30), schema,
                new BsonDocument { ["context"] = context, ["data"] = data }, user, reason));
        }
        Add("TEAM_TOOL_LIFECYCLE", 1, "1.2", new BsonDocument { ["action"] = "PICKUP", ["instanceId"] = "tool:4", ["toolId"] = 4,
            ["zone"] = 1, ["ownerUserId"] = f.HostId.ToString("D"), ["remainingCharges"] = 2 });
        if (mode != "missing-activation") Add("TEAM_TOOL_USED", 5, "1.1", new BsonDocument { ["toolType"] = "DOOR_JAMMER" },
            f.HostId, "PLAYER_ACTIVATED_TOOL", "player:owner-object:tool:1");
        if (mode != "missing-charge") Add("TEAM_TOOL_LIFECYCLE", 5, "1.2", new BsonDocument { ["action"] = "USE", ["instanceId"] = "tool:4",
            ["toolId"] = 4, ["zone"] = 1, ["ownerUserId"] = f.HostId.ToString("D"), ["remainingCharges"] = 1 });
        var ordered = trace.OrderBy(x => x.Ts).ThenBy(x => x.EventType == "AED_V02_WORLD_SAMPLE" ? 2 : x.EventType == "TEAM_TOOL_USED" ? 0 : 1)
            .Select((x, i) => V02Event(f.MatchId, x.EventType, i + 1, x.Ts, x.SchemaVersion, x.ValueJson, x.UserId, x.ReasonCode));
        var accepted = await TelemetryServiceTests.IngestV02Trace(f.MatchId, ordered, Now, V02TelemetryAuthority(f), f.HostId);
        var result = await new AEDV02SourceVerifier(f.Db, new AcceptedEvidenceRepository(accepted), new FixedClock(Now))
            .VerifyAsync(f.MatchId, request with { SceneManifestFingerprint = scene });
        if (expected == null) { Assert.False(result.IsSuccess); Assert.Equal("HOLD_TOOL_JAMMER_LINEAGE_INVALID", result.Message); return; }
        Assert.True(result.IsSuccess, result.Message);
        var opportunity = Assert.Single(result.Data!.ResolvedOpportunities!.Where(x => x.Axis == "Tool" && x.ToolId == 4));
        Assert.Equal(expected, opportunity.Resolution); Assert.Equal(f.HostId, opportunity.UserId);
        Assert.Equal("Tool:4:stalker:jammer-chase", opportunity.Id);
        if (mode == "prior-phase")
        {
            var later = accepted.Where(x => x.EventType == "AED_V02_WORLD_SAMPLE").TakeLast(2).Select((item, index) =>
            {
                var value = item.ValueJson.DeepClone().AsBsonDocument;
                var world = JsonSerializer.Deserialize<AEDV02WorldSample>(value["data"]["world"].ToJson())!;
                world.Tick = 2460 + index * 60; world.PhaseOrdinal = 2; world.PhaseName = "ZONE_2_OBJECTIVE"; world.LogicalPhase = world.Zone = 2;
                value["context"]["phase"] = world.PhaseName; value["context"]["authorityTick"] = world.Tick;
                value["data"]["world"] = BsonDocument.Parse(JsonSerializer.Serialize(world));
                return V02Event(f.MatchId, "AED_V02_WORLD_SAMPLE", accepted.Max(x => x.EventSequence) + index + 1,
                    Now.AddSeconds(index + 1), "1.3", value);
            }).ToArray();
            var nextAccepted = await TelemetryServiceTests.IngestV02Trace(f.MatchId, accepted.Concat(later), Now.AddSeconds(2), V02TelemetryAuthority(f), f.HostId);
            var current = nextAccepted.Where(x => x.EventSequence > accepted.Max(e => e.EventSequence))
                .Select(x => (Event: x, Sample: JsonSerializer.Deserialize<AEDV02WorldSample>(x.ValueJson["data"]["world"].ToJson())!)).ToList();
            var arguments = new object?[] { nextAccepted, current, manifest, null };
            var resolver = typeof(AEDV02SourceVerifier).GetMethod("TryResolveJammerOpportunities", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
            Assert.True((bool)resolver.Invoke(null, arguments)!);
            Assert.Empty((AEDV02ResolvedOpportunity[])arguments[3]!); // Existing deployment is valid census, not another phase's skill sample.
        }
    }
}
