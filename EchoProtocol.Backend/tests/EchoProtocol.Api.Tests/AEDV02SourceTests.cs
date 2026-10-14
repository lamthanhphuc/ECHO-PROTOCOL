using System.Text.Json;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Data.Telemetry;
using EchoProtocol.Api.DTOs.Scenarios;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using MongoDB.Bson;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed partial class ScenarioServiceTests
{
    [Fact, Trait("Category", "AEDV02")]
    public async Task V02_RealLifecycleAndWorldSamples_ReconstructVerifiedZeroMinionCensus()
    {
        await using var f = await ValidAEDv2Fixture();
        var (events, request) = await V02WorldEvidence(f);
        var verifier = new AEDV02SourceVerifier(f.Db, new AcceptedEvidenceRepository(events), new FixedClock(Now));
        var result = await verifier.VerifyAsync(f.MatchId, request);
        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.Data!.SafeWindow);
        Assert.Equal(2, result.Data.PlayerPressure.Count);
        Assert.All(result.Data.PlayerPressure.Values, x => Assert.Equal(0, x));
        Assert.Single(f.Db.AEDV02VerifiedSources);
        var replay = await verifier.VerifyAsync(f.MatchId, request);
        Assert.Equal(result.Data.Fingerprint, replay.Data!.Fingerprint);
        Assert.Single(f.Db.AEDV02VerifiedSources);
    }

    [Theory, Trait("Category", "AEDV02")]
    [InlineData("phantom-minion", "HOLD_MINION_CENSUS_MISMATCH")]
    [InlineData("missing-tool", "HOLD_TOOL_CENSUS_MISMATCH")]
    [InlineData("locked-door", "HOLD_SUPPLY_ROUTE_INVALID")]
    [InlineData("forged-owner", "HOLD_TOOL_CENSUS_MISMATCH")]
    [InlineData("forged-life", "HOLD_PLAYER_LIFECYCLE_MISMATCH")]
    [InlineData("gap", "HOLD_SOURCE_SEQUENCE_GAP")]
    [InlineData("debug", "HOLD_INSUFFICIENT_EVIDENCE")]
    [InlineData("stale", "HOLD_STALE_SOURCE")]
    public async Task V02_RealVerifier_RejectsCorruptIncompleteAndStaleSources(string failure, string reason)
    {
        await using var f = await ValidAEDv2Fixture();
        var (events, request) = await V02WorldEvidence(f);
        var sampleEvent = events.Last();
        var sample = JsonSerializer.Deserialize<AEDV02WorldSample>(sampleEvent.ValueJson["data"]["world"].ToJson())!;
        switch (failure)
        {
            case "phantom-minion": sample.Minions = new[] { new AEDV02ThreatSource { NetworkId = "phantom", Zone = 1, State = "IDLE" } }; break;
            case "missing-tool": sample.Tools = sample.Tools.Skip(1).ToArray(); break;
            case "locked-door": sample.Doors[0].State = "LOCKED"; break;
            case "forged-owner": sample.Tools[0].OwnerUserId = sample.Players[0].UserId; break;
            case "forged-life": sample.Players[0].LifeState = "DOWNED"; break;
            case "debug": sample.EvidenceExclusion = "DEBUG_SKIP_ZONE3"; break;
        }
        sampleEvent.ValueJson["data"]["world"] = BsonDocument.Parse(JsonSerializer.Serialize(sample));
        if (failure == "gap") events = events.Where(x => x.EventSequence != 20).ToArray();
        var verifier = new AEDV02SourceVerifier(f.Db, new AcceptedEvidenceRepository(events), new FixedClock(failure == "stale" ? Now.AddSeconds(11) : Now));
        var result = await verifier.VerifyAsync(f.MatchId, request);
        Assert.False(result.IsSuccess);
        Assert.Equal(reason, result.Message);
        Assert.Empty(f.Db.AEDV02VerifiedSources);
    }

    private static async Task<(TelemetryEventDocument[] Events, AEDV02SourceRequest Request)> V02WorldEvidence(Fixture f)
    {
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.InMatch;
        var users = f.Db.MatchPlayerBindings.Where(x => x.MatchId == f.MatchId).Select(x => x.UserId).ToArray();
        var roster = AEDV02Plan.Hash(f.MatchId.ToString("D") + "|" + string.Join(',', users.Order().Select(x => x.ToString("D"))));
        var ids = new[] { 1, 2, 3, 4, 6 };
        var manifest = new AEDV02SceneManifest { SceneVersion = "TEST_SCENE_REV02",
            Markers = new[] { new AEDV02SceneMarker { Id = "Z1_ENTRY", Zone = 1, Kind = "ENTRY" } }
                .Concat(ids.Select(x => new AEDV02SceneMarker { Id = "marker:" + x, Zone = 1, Kind = "TOOL", AllowedToolIds = new[] { x } })).ToArray(),
            Edges = ids.Select(x => new AEDV02RouteEdge { Id = "edge:" + x, From = "Z1_ENTRY", To = "marker:" + x, PathMeters = 10, DoorId = "door" }).ToArray() };
        var json = JsonSerializer.Serialize(manifest); var fingerprint = AEDV02Plan.Hash(json);
        f.Db.AEDV02SceneContents.Add(new AEDV02SceneContent { Fingerprint = fingerprint, SceneVersion = manifest.SceneVersion, ManifestJson = json, RegisteredAtUtc = Now });
        await f.Db.SaveChangesAsync();
        var events = new List<TelemetryEventDocument> { V02Event(f.MatchId, "PHASE_STARTED", 1, Now.AddSeconds(-60), "1.1",
            new BsonDocument { ["context"] = new BsonDocument { ["phase"] = "CORE_COLLECTION" }, ["data"] = new BsonDocument() }) };
        foreach (var id in ids)
            events.Add(V02Event(f.MatchId, "TEAM_TOOL_LIFECYCLE", events.Count + 1, Now.AddSeconds(-31), "1.2",
                new BsonDocument { ["context"] = new BsonDocument { ["phase"] = "CORE_COLLECTION", ["authorityTick"] = 599, ["occurrenceKey"] = "spawn:" + id },
                    ["data"] = new BsonDocument { ["action"] = "SPAWN", ["instanceId"] = "tool:" + id, ["toolId"] = id, ["zone"] = 1, ["origin"] = "WorldSpawn" } }));
        for (int i = 0; i <= 30; i++)
        {
            var sample = new AEDV02WorldSample { ResolutionMode = "Adaptive", Difficulty = "Normal", Tick = 600 + i * 60, TickRate = 60, PhaseOrdinal = 1, PhaseName = "CORE_COLLECTION", LogicalPhase = 1, Zone = 1,
                PlanRevision = 1, PlanFingerprint = AEDV02Plan.Normal().Fingerprint(), RosterIdentity = roster, SceneManifestFingerprint = fingerprint,
                Players = users.Select(x => new AEDV02PlayerSource { UserId = x, LifeState = "ALIVE" }).ToArray(),
                Tools = ids.Select(x => new AEDV02ToolSource { InstanceId = "tool:" + x, ToolId = x, Zone = 1, Origin = "WorldSpawn", Location = "World", MarkerId = "marker:" + x, RouteEdgeIds = new[] { "edge:" + x } }).ToArray(),
                Doors = new[] { new AEDV02DoorSource { DoorId = "door", State = "CLOSED_UNLOCKED" } } };
            var value = JsonSerializer.SerializeToElement(new { context = new { authorityTick = sample.Tick, phase = sample.PhaseName, occurrenceKey = "world:" + sample.Tick }, data = new { world = sample } });
            events.Add(V02Event(f.MatchId, "AED_V02_WORLD_SAMPLE", events.Count + 1, Now.AddSeconds(i - 30), "1.3", BsonDocument.Parse(value.GetRawText())));
        }
        return (events.ToArray(), new AEDV02SourceRequest(1, 600, 2400, fingerprint));
    }
    private static TelemetryEventDocument V02Event(Guid matchId, string type, long sequence, DateTime ts, string version, BsonDocument value, Guid? userId = null)
    {
        value["context"]["eventSequence"] = sequence;
        return new TelemetryEventDocument { Id = Guid.NewGuid(), MatchId = matchId, UserId = userId, EventType = type,
            EventSequence = sequence, Ts = ts, SchemaVersion = version,
            SemanticFingerprint = AEDV02Plan.Hash(value.ToJson()), IngestedAt = Now, ValueJson = value };
    }
}
