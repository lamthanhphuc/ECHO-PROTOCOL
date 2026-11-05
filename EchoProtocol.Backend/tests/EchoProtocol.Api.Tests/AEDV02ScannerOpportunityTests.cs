using System.Text.Json;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Services;
using MongoDB.Bson;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed partial class ScenarioServiceTests
{
    [Theory, Trait("Category", "AEDV02")]
    [InlineData("core", "Success")]
    [InlineData("motion", "Success")]
    [InlineData("not-presented", "Censored")]
    [InlineData("empty", "NoOpportunity")]
    [InlineData("wrong-result", null)]
    [InlineData("missing-activation", null)]
    [InlineData("no-custody", null)]
    [InlineData("cross-match", null)]
    [InlineData("changed-census", null)]
    [InlineData("late-presentation", null)]
    [InlineData("cooldown-bypass", null)]
    [InlineData("missing-world-census", null)]
    public async Task V02_ScannerReplaysActualCensusActivationSelectionAndDelivery(string mode, string? expected)
    {
        await using var f = await ValidV02Fixture();
        var (baseline, request) = await V02WorldEvidence(f, geometry: true, pilot: true);
        var content = f.Db.AEDV02SceneContents.Single();
        var manifest = JsonSerializer.Deserialize<AEDV02SceneManifest>(content.ManifestJson)!;
        manifest.ScannerCoreRange = manifest.ScannerMotionRange = 100;
        manifest.ScannerMovingThreshold = .15; manifest.ScannerOcclusionMultiplier = .6; manifest.ScannerMaxTargets = 3;
        manifest.ScannerActiveSeconds = 20; manifest.ScannerCycleSeconds = 50;
        string json = JsonSerializer.Serialize(manifest), scene = AEDV02Plan.Hash(json);
        f.Db.AEDV02SceneContents.Add(new() { Fingerprint = scene, SceneVersion = manifest.SceneVersion, ManifestJson = json, RegisteredAtUtc = Now });
        await f.Db.SaveChangesAsync();
        var trace = baseline.ToList();
        foreach (var item in trace)
        {
            if (item.EventType != "AED_V02_WORLD_SAMPLE") continue;
            var world = JsonSerializer.Deserialize<AEDV02WorldSample>(item.ValueJson["data"]["world"].ToJson())!;
            world.SceneManifestFingerprint = scene;
            int second = (int)(world.Tick / 60 - 10);
            if (second >= 1 && mode != "no-custody")
            {
                var tool = world.Tools.Single(x => x.ToolId == 1); tool.Location = "Player"; tool.OwnerUserId = f.HostId; tool.MarkerId = string.Empty;
                world.Players.Single(x => x.UserId == f.HostId).HeldToolInstanceId = tool.InstanceId;
            }
            if (mode == "motion") world.Stalkers = [new AEDV02ThreatSource { NetworkId = "scanner-stalker", State = "PATROL", Zone = 1, X = 2, Z = 1 }];
            if (second >= 5)
                world.Scans = [new AEDV02ScanSource { MatchId = mode == "cross-match" ? Guid.NewGuid() : f.MatchId, UserId = f.HostId, Sequence = 1,
                    Occurrence = "player:owner-object:tool:FIELD_SCANNER:1", Tick = 900, PhaseName = "CORE_COLLECTION",
                    Mode = mode == "motion" ? "Motion" : "Core", Range = 100, MovingThreshold = .15, OcclusionMultiplier = .6,
                    MaxTargets = mode == "motion" ? 3 : 1, Presented = mode == "late-presentation" ? second >= 26 : second >= 6 && mode != "not-presented",
                    PresentedTick = mode == "late-presentation" ? second >= 26 ? 2160 : 0 : second >= 6 && mode != "not-presented" ? 960 : 0,
                    Candidates = mode == "empty" ? [] : [new AEDV02ScanCandidate { LocalId = 42,
                        Identity = mode == "motion" ? "scanner-stalker" : "core-target", Kind = mode == "motion" ? "Stalker" : "Core",
                        Available = true, Moving = true, Speed = 1, X = mode == "changed-census" && second >= 7 ? 3 : 2, Z = 1 }],
                    ResultIds = mode is "empty" or "wrong-result" ? [] : [42] }];
            if (world.Scans.Length != 0 && mode != "missing-world-census") world.Scannables = world.Scans[0].Candidates;
            item.ValueJson["data"]["world"] = BsonDocument.Parse(JsonSerializer.Serialize(world));
        }
        if (mode != "no-custody") trace.Add(V02Event(f.MatchId, "TEAM_TOOL_LIFECYCLE", 0, Now.AddSeconds(-29), "1.2", new BsonDocument {
            ["context"] = new BsonDocument { ["phase"] = "CORE_COLLECTION", ["authorityTick"] = 660 },
            ["data"] = new BsonDocument { ["action"] = "PICKUP", ["instanceId"] = "tool:1", ["toolId"] = 1, ["zone"] = 1, ["ownerUserId"] = f.HostId.ToString("D") } }));
        if (mode != "missing-activation") trace.Add(V02Event(f.MatchId, "TEAM_TOOL_USED", 0, Now.AddSeconds(-25), "1.1", new BsonDocument {
            ["context"] = new BsonDocument { ["phase"] = "CORE_COLLECTION", ["authorityTick"] = 900, ["occurrenceKey"] = "player:owner-object:tool:FIELD_SCANNER:1" },
            ["data"] = new BsonDocument { ["toolType"] = "FIELD_SCANNER" } }, f.HostId, "PLAYER_ACTIVATED_TOOL"));
        if (mode == "cooldown-bypass") trace.Add(V02Event(f.MatchId, "TEAM_TOOL_USED", 0, Now.AddSeconds(-28), "1.1", new BsonDocument {
            ["context"] = new BsonDocument { ["phase"] = "CORE_COLLECTION", ["authorityTick"] = 720, ["occurrenceKey"] = "player:owner-object:tool:FIELD_SCANNER:99" },
            ["data"] = new BsonDocument { ["toolType"] = "FIELD_SCANNER" } }, f.HostId, "PLAYER_ACTIVATED_TOOL"));
        var ordered = trace.OrderBy(x => x.Ts).ThenBy(x => x.EventType == "AED_V02_WORLD_SAMPLE" ? 1 : 0)
            .Select((x, i) => V02Event(f.MatchId, x.EventType, i + 1, x.Ts, x.SchemaVersion, x.ValueJson, x.UserId, x.ReasonCode));
        var accepted = await TelemetryServiceTests.IngestV02Trace(f.MatchId, ordered, Now, V02TelemetryAuthority(f), f.HostId);
        var result = await new AEDV02SourceVerifier(f.Db, new AcceptedEvidenceRepository(accepted), new FixedClock(Now))
            .VerifyAsync(f.MatchId, request with { SceneManifestFingerprint = scene });
        if (expected == null) { Assert.False(result.IsSuccess); Assert.Equal("HOLD_TOOL_SCANNER_LINEAGE_INVALID", result.Message); return; }
        Assert.True(result.IsSuccess, result.Message);
        var outcomes = result.Data!.ResolvedOpportunities!.Where(x => x.Axis == "Tool" && x.ToolId == 1).ToArray();
        if (expected == "NoOpportunity") Assert.Empty(outcomes);
        else { var outcome = Assert.Single(outcomes); Assert.Equal(expected, outcome.Resolution); Assert.Equal(f.HostId, outcome.UserId); }
    }
}
