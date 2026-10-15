using System.Text.Json;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Data.Telemetry;
using EchoProtocol.Api.Services;
using MongoDB.Bson;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed partial class ScenarioServiceTests
{
    [Theory, Trait("Category", "AEDV02")]
    [InlineData("valid", null)]
    [InlineData("marker", "HOLD_CABINET_CLUE_SOURCE_INVALID")]
    [InlineData("range", "HOLD_CABINET_CLUE_SOURCE_INVALID")]
    [InlineData("remote-cabinet", "HOLD_CABINET_CLUE_SOURCE_INVALID")]
    [InlineData("unseen-entry", "HOLD_CABINET_CLUE_LINEAGE_MISMATCH")]
    [InlineData("wrong-spot", "HOLD_CABINET_CLUE_LINEAGE_MISMATCH")]
    [InlineData("observer", "HOLD_CABINET_CLUE_LINEAGE_MISMATCH")]
    public async Task V02_CabinetClueUsesRealIngestionAndIndependentScenePlayerThreatLineage(string mutation, string? rejection)
    {
        await using var f = await ValidV02Fixture();
        var (trace, request) = await V02WorldEvidence(f);
        var content = f.Db.AEDV02SceneContents.Single(x => x.Fingerprint == request.SceneManifestFingerprint);
        var manifest = JsonSerializer.Deserialize<AEDV02SceneManifest>(content.ManifestJson)!;
        var markerId = AEDV02Plan.Hash("cabinet:10");
        manifest.MaximumVisionRange = 10;
        manifest.MaximumCabinetInteractionDistance = 3;
        manifest.Markers = [.. manifest.Markers, new AEDV02SceneMarker { Id = markerId, Kind = "CABINET", Zone = 1, HideSpotId = "10", InteractionBounds = [-1d, -1, -1, 1, 1, 1] }];
        var json = JsonSerializer.Serialize(manifest); var fingerprint = AEDV02Plan.Hash(json);
        f.Db.AEDV02SceneContents.Add(new AEDV02SceneContent { Fingerprint = fingerprint, SceneVersion = manifest.SceneVersion, ManifestJson = json, RegisteredAtUtc = Now });
        await f.Db.SaveChangesAsync();
        var user = JsonSerializer.Deserialize<AEDV02WorldSample>(trace.Last().ValueJson["data"]["world"].ToJson())!.Players[0].UserId;
        foreach (var item in trace.Where(x => x.EventType == "AED_V02_WORLD_SAMPLE"))
        {
            var sample = JsonSerializer.Deserialize<AEDV02WorldSample>(item.ValueJson["data"]["world"].ToJson())!;
            sample.SceneManifestFingerprint = fingerprint;
            sample.Players.Single(x => x.UserId == user).HideSpotId = mutation == "unseen-entry" || sample.Tick >= 660 ? (mutation == "wrong-spot" ? "11" : "10") : "0";
            item.ValueJson["data"]["world"] = BsonDocument.Parse(JsonSerializer.Serialize(sample));
        }
        var clue = new AEDV02CabinetClueSource { UserId = user, StalkerNetworkId = mutation == "observer" ? "unspawned:stalker" : "stalker",
            HideSpotId = "10", SceneMarkerId = mutation == "marker" ? AEDV02Plan.Hash("unknown") : markerId, SceneManifestFingerprint = fingerprint,
            Tick = 601, OriginY = mutation == "range" ? 20 : 1, ObservedDistance = mutation == "range" ? 20 : 1,
            PlayerX = mutation == "remote-cabinet" ? 50 : 0 };
        var observation = V02Event(f.MatchId, "AED_V02_CABINET_CLUE", 0, Now.AddSeconds(-30).AddSeconds(1d / 60), "1.3",
            new BsonDocument { ["context"] = new BsonDocument { ["phase"] = "CORE_COLLECTION", ["authorityTick"] = 601, ["occurrenceKey"] = "seen:10" },
                ["data"] = new BsonDocument { ["clue"] = BsonDocument.Parse(JsonSerializer.Serialize(clue)) } });
        var ordered = trace.Append(observation).OrderBy(x => x.Ts).Select((x, i) => new TelemetryEventDocument
        { Id = x.Id, MatchId = x.MatchId, UserId = x.UserId, EventType = x.EventType, Ts = x.Ts, EventSequence = i + 1,
            ValueJson = x.ValueJson, SchemaVersion = x.SchemaVersion, ReasonCode = x.ReasonCode }).ToArray();
        var accepted = await TelemetryServiceTests.IngestV02Trace(f.MatchId, ordered, Now, V02TelemetryAuthority(f), f.HostId);
        var result = await new AEDV02SourceVerifier(f.Db, new AcceptedEvidenceRepository(accepted), new FixedClock(Now))
            .VerifyAsync(f.MatchId, request with { SceneManifestFingerprint = fingerprint });
        if (rejection != null) { Assert.False(result.IsSuccess); Assert.Equal(rejection, result.Message); return; }
        Assert.True(result.IsSuccess, result.Message); Assert.Contains(observation.Id, result.Data!.CabinetClueEventIds!);
        Assert.False(result.Data.PressureComplete);
        Assert.Equal("Unsupported", result.Data.TeamPressureStatus);
    }
}
