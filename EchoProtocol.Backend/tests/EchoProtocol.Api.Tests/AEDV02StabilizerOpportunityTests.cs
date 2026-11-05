using System.Text.Json;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Services;
using MongoDB.Bson;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed partial class ScenarioServiceTests
{
    [Theory, Trait("Category", "AEDV02")]
    [InlineData("protected-carrier", "Success")]
    [InlineData("empty-room", "NoOpportunity")]
    [InlineData("unobserved-onset", "Censored")]
    [InlineData("missing-owner-tool", "HOLD_TOOL_STABILIZER_LINEAGE_INVALID")]
    [InlineData("buff-outside-radius", "HOLD_TOOL_STABILIZER_LINEAGE_INVALID")]
    [InlineData("missing-buff", "HOLD_TOOL_STABILIZER_LINEAGE_INVALID")]
    [InlineData("missing-core-event", "HOLD_TOOL_STABILIZER_LINEAGE_INVALID")]
    [InlineData("wrong-core-user", "HOLD_TOOL_STABILIZER_LINEAGE_INVALID")]
    [InlineData("timer-too-long", "HOLD_TOOL_STABILIZER_LINEAGE_INVALID")]
    [InlineData("unsupported-tuning", "HOLD_TOOL_STABILIZER_SOURCE_UNSUPPORTED")]
    public async Task V02_StabilizerOutcomeNeedsActualTimerOwnedToolBuffGeometryAndCoreLineage(string mode, string expected)
    {
        await using var f = await ValidV02Fixture();
        var (baseline, source) = await V02WorldEvidence(f, geometry: true, pilot: true);
        var content = f.Db.AEDV02SceneContents.Single();
        var manifest = JsonSerializer.Deserialize<AEDV02SceneManifest>(content.ManifestJson)!;
        manifest.CoreStabilizerRadius = mode == "unsupported-tuning" ? 0 : 10;
        manifest.CoreStabilizerDurationSeconds = 15;
        string json = JsonSerializer.Serialize(manifest), scene = AEDV02Plan.Hash(json);
        f.Db.AEDV02SceneContents.Add(new() { Fingerprint = scene, SceneVersion = manifest.SceneVersion, ManifestJson = json, RegisteredAtUtc = Now });
        await f.Db.SaveChangesAsync();
        var trace = baseline.ToList();
        foreach (var item in trace)
        {
            if (mode == "unobserved-onset" && item.EventType == "TEAM_TOOL_LIFECYCLE")
            { item.ValueJson["context"]["authorityTick"] = 598; }
            if (item.EventType != "AED_V02_WORLD_SAMPLE") continue;
            var world = JsonSerializer.Deserialize<AEDV02WorldSample>(item.ValueJson["data"]["world"].ToJson())!;
            world.SceneManifestFingerprint = scene;
            int second = (int)(world.Tick / 60 - 10);
            int start = mode == "unobserved-onset" ? 0 : 5;
            bool active = second >= start && second < start + 15;
            var owner = world.Players.Single(x => x.UserId == f.HostId);
            if ((second >= 1 || mode == "unobserved-onset") && mode != "missing-owner-tool")
            {
                var tool = world.Tools.Single(x => x.ToolId == 6); tool.Location = "Player"; tool.OwnerUserId = f.HostId; tool.MarkerId = string.Empty;
                owner.HeldToolInstanceId = tool.InstanceId;
            }
            if (active)
            {
                owner.StabilizerUntilTick = mode == "timer-too-long" ? 3600 : 600 + (start + 15) * 60;
                owner.StabilizerActivationOrdinal = 1; owner.StabilizerBuffed = true;
            }
            var carrier = world.Players.Single(x => x.UserId == f.PlayerId);
            carrier.X = mode == "buff-outside-radius" ? 20 : 1; carrier.Z = 1;
            carrier.StabilizerBuffed = active && mode != "missing-buff";
            if (second >= 2 && mode != "empty-room") { carrier.ObjectiveUnitId = "core:protected-core"; carrier.ObjectiveState = "OPERATING"; }
            item.ValueJson["data"]["world"] = BsonDocument.Parse(JsonSerializer.Serialize(world));
        }
        if (mode != "missing-owner-tool") trace.Add(V02Event(f.MatchId, "TEAM_TOOL_LIFECYCLE", 0, Now.AddSeconds(mode == "unobserved-onset" ? -31 : -29), "1.2", new BsonDocument {
            ["context"] = new BsonDocument { ["phase"] = "CORE_COLLECTION", ["authorityTick"] = mode == "unobserved-onset" ? 599 : 660 },
            ["data"] = new BsonDocument { ["action"] = "PICKUP", ["instanceId"] = "tool:6", ["toolId"] = 6, ["zone"] = 1, ["ownerUserId"] = f.HostId.ToString("D") } }));
        if (mode is not ("empty-room" or "missing-core-event")) trace.Add(V02Event(f.MatchId, "CORE_PICKED_UP", 0, Now.AddSeconds(-28), "1.1", new BsonDocument {
            ["context"] = new BsonDocument { ["phase"] = "CORE_COLLECTION", ["authorityTick"] = 720 },
            ["data"] = new BsonDocument { ["coreId"] = "protected-core" } }, mode == "wrong-core-user" ? f.HostId : f.PlayerId, "PLAYER_PICKUP"));
        var ordered = trace.OrderBy(x => x.Ts).ThenBy(x => x.EventType == "AED_V02_WORLD_SAMPLE" ? 1 : 0)
            .Select((x, index) => V02Event(f.MatchId, x.EventType, index + 1, x.Ts, x.SchemaVersion, x.ValueJson, x.UserId, x.ReasonCode));
        var accepted = await TelemetryServiceTests.IngestV02Trace(f.MatchId, ordered, Now, V02TelemetryAuthority(f), f.HostId);
        var result = await new AEDV02SourceVerifier(f.Db, new AcceptedEvidenceRepository(accepted), new FixedClock(Now))
            .VerifyAsync(f.MatchId, source with { SceneManifestFingerprint = scene });
        if (expected.StartsWith("HOLD_", StringComparison.Ordinal)) { Assert.False(result.IsSuccess); Assert.Equal(expected, result.Message); return; }
        Assert.True(result.IsSuccess, result.Message);
        var outcomes = result.Data!.ResolvedOpportunities!.Where(x => x.Axis == "Tool" && x.ToolId == 6).ToArray();
        if (expected == "NoOpportunity") { Assert.Empty(outcomes); return; }
        var outcome = Assert.Single(outcomes); Assert.Equal(f.HostId, outcome.UserId); Assert.Equal(expected, outcome.Resolution);
    }
}
