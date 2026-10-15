using System.Text.Json;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Services;
using MongoDB.Bson;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed partial class ScenarioServiceTests
{
    [Fact, Trait("Category", "AEDV02")]
    public async Task V02_BakedSciFiManifest_ReconstructsSourceFromActualMeshAndToolWhitelist()
    {
        await using var f = await ValidV02Fixture();
        var (events, request) = await V02WorldEvidence(f);
        var json = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Content/AEDV02/Rev02SceneManifest.json"));
        var manifest = JsonSerializer.Deserialize<AEDV02SceneManifest>(json)!;
        Assert.True(manifest.IsValid()); Assert.True(manifest.NavCells.Length > 1000);
        Assert.Contains(manifest.NavLinks, x => x.DoorIds.Length > 0);
        await AEDV02SceneRegistry.RegisterPilotAsync(f.Db, json, Now);
        await AEDV02SceneRegistry.RegisterPilotAsync(f.Db, json, Now);
        var fingerprint = AEDV02Plan.Hash(json);
        Assert.Single(f.Db.AEDV02SceneContents, x => x.Fingerprint == fingerprint);
        var doors = manifest.DoorIds.Select(x => new AEDV02DoorSource { DoorId = x, State = "OPEN" }).ToArray();
        var entry = manifest.Markers.Single(x => x.Id == "Z1_ENTRY");
        var selected = new[] { 1, 2, 3, 4, 6 }.Select(id => manifest.Markers.First(x => x.Zone == 1 && x.Kind == "TOOL" && x.AllowedToolIds.Contains(id) &&
            manifest.HasReachableGeometry([entry.X, entry.Y, entry.Z], [x.X, x.Y, x.Z], doors))).ToArray();
        foreach (var item in events.Where(x => x.EventType == "AED_V02_WORLD_SAMPLE"))
        {
            var world = JsonSerializer.Deserialize<AEDV02WorldSample>(item.ValueJson["data"]["world"].ToJson())!;
            world.SceneManifestFingerprint = fingerprint; world.Doors = doors;
            for (int i = 0; i < world.Tools.Length; i++)
            { world.Tools[i].MarkerId = selected[i].Id; world.Tools[i].X = selected[i].X; world.Tools[i].Y = selected[i].Y; world.Tools[i].Z = selected[i].Z; }
            item.ValueJson["data"]["world"] = BsonDocument.Parse(JsonSerializer.Serialize(world));
        }
        var verifier = new AEDV02SourceVerifier(f.Db, new AcceptedEvidenceRepository(events), new FixedClock(Now));
        var source = await verifier.VerifyAsync(f.MatchId, request with { SceneManifestFingerprint = fingerprint });
        Assert.True(source.IsSuccess, source.Message);
        Assert.Equal(fingerprint, source.Data!.SceneManifestFingerprint);
        var last = events.Last();
        var corrupt = JsonSerializer.Deserialize<AEDV02WorldSample>(last.ValueJson["data"]["world"].ToJson())!;
        corrupt.Tools[0].Y += 20;
        last.ValueJson["data"]["world"] = BsonDocument.Parse(JsonSerializer.Serialize(corrupt));
        Assert.Equal("HOLD_SUPPLY_ROUTE_INVALID", (await verifier.VerifyAsync(f.MatchId, request with { SceneManifestFingerprint = fingerprint })).Message);
    }

    [Theory, Trait("Category", "AEDV02")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task V02_AuthoredZoneEntry_HasRealMeshRoutesForFiveRequiredTypes(int zone)
    {
        var json = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Content/AEDV02/Rev02SceneManifest.json"));
        var manifest = JsonSerializer.Deserialize<AEDV02SceneManifest>(json)!;
        var entry = manifest.Markers.Single(x => x.Id == "Z" + zone + "_ENTRY");
        var doors = manifest.DoorIds.Select(x => new AEDV02DoorSource { DoorId = x, State = "OPEN" }).ToArray();
        Assert.Contains(manifest.NavCells, x => x.Contains([entry.X, entry.Y, entry.Z]));
        foreach (int tool in new[] { 1, 2, 3, 4, 6 })
            Assert.Contains(manifest.Markers, x => x.Zone == zone && x.Kind == "TOOL" && x.AllowedToolIds.Contains(tool) &&
                manifest.HasReachableGeometry([entry.X, entry.Y, entry.Z], [x.X, x.Y, x.Z], doors));
        var first = manifest.Markers.First(x => x.Zone == zone && x.Kind == "TOOL" &&
            manifest.HasReachableGeometry([entry.X, entry.Y, entry.Z], [x.X, x.Y, x.Z], doors));
        Assert.False(manifest.TryReachableGeometry([entry.X, entry.Y, entry.Z], [first.X, first.Y, first.Z], Array.Empty<AEDV02DoorSource>(), out _));
    }
}
