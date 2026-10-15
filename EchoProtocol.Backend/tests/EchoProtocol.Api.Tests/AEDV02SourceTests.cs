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
    public async Task V02_ScenePlacedSupplyDoesNotSubstituteRequiredRuntimeSpawnBudget()
    {
        await using var f = await ValidV02Fixture();
        var (events, request) = await V02WorldEvidence(f);
        foreach (var item in events)
        {
            if (item.EventType == "TEAM_TOOL_LIFECYCLE") item.ValueJson["data"]["origin"] = "ScenePlaced";
            if (item.EventType == "AED_V02_WORLD_SAMPLE")
                foreach (var tool in item.ValueJson["data"]["world"]["Tools"].AsBsonArray) tool["Origin"] = "ScenePlaced";
        }
        var accepted = await TelemetryServiceTests.IngestV02Trace(f.MatchId, events, Now, V02TelemetryAuthority(f), f.HostId);
        var result = await new AEDV02SourceVerifier(f.Db, new AcceptedEvidenceRepository(accepted), new FixedClock(Now)).VerifyAsync(f.MatchId, request);
        Assert.False(result.IsSuccess); Assert.Equal("HOLD_REQUIRED_SUPPLY_RECEIPTS_MISSING", result.Message);
    }

    [Fact, Trait("Category", "AEDV02")]
    public async Task V02_UnissuedPlanRevisionCannotBecomeCanonicalSource()
    {
        await using var f = await ValidV02Fixture();
        var (events, request) = await V02WorldEvidence(f, planRevision: 99);
        var result = await new AEDV02SourceVerifier(f.Db, new AcceptedEvidenceRepository(events), new FixedClock(Now)).VerifyAsync(f.MatchId, request);
        Assert.False(result.IsSuccess); Assert.Equal("HOLD_PLAN_LINEAGE_MISMATCH", result.Message);
    }
    [Fact, Trait("Category", "AEDV02")]
    public async Task V02_RealLifecycleAndWorldSamples_ReconstructVerifiedZeroMinionCensus()
    {
        await using var f = await ValidV02Fixture();
        var (events, request) = await V02WorldEvidence(f);
        var verifier = new AEDV02SourceVerifier(f.Db, new AcceptedEvidenceRepository(events), new FixedClock(Now));
        var result = await verifier.VerifyAsync(f.MatchId, request);
        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.Data!.SafeWindow);
        Assert.False(result.Data.PressureComplete);
        Assert.Contains("ObjectiveThreatInterruption", result.Data.MissingChannels);
        Assert.Contains("ResourceNeedOutsideRevive", result.Data.MissingChannels);
        Assert.Equal(2, result.Data.PlayerPressure.Count);
        Assert.All(result.Data.PlayerPressure.Values, x => Assert.Equal(0, x));
        Assert.Single(f.Db.AEDV02VerifiedSources);
        var replay = await verifier.VerifyAsync(f.MatchId, request);
        Assert.Equal(result.Data.Fingerprint, replay.Data!.Fingerprint);
        Assert.Single(f.Db.AEDV02VerifiedSources);
    }

    [Fact, Trait("Category", "AEDV02")]
    public async Task V02_CaughtPlayerRetainsSourceCoverageAndClosesSafeWindow()
    {
        await using var f = await ValidV02Fixture();
        var (events, request) = await V02WorldEvidence(f);
        var world = events.Last().ValueJson["data"]["world"].AsBsonDocument;
        world["Players"][0]["IsCaught"] = true;
        var result = await new AEDV02SourceVerifier(f.Db, new AcceptedEvidenceRepository(events), new FixedClock(Now)).VerifyAsync(f.MatchId, request);
        Assert.True(result.IsSuccess, result.Message);
        Assert.False(result.Data!.SafeWindow);
    }

    [Theory, Trait("Category", "AEDV02")]
    [InlineData(2)]
    [InlineData(4)]
    public async Task V02_KnownSpawnChargesSurviveIngestionAndIndependentReplay(int toolId)
    {
        await using var f = await ValidV02Fixture();
        var (events, request) = await V02WorldEvidence(f);
        events.Single(x => x.EventType == "TEAM_TOOL_LIFECYCLE" && x.ValueJson["data"]["toolId"] == toolId).ValueJson["data"]["remainingCharges"] = 3;
        foreach (var item in events.Where(x => x.EventType == "AED_V02_WORLD_SAMPLE"))
            foreach (var tool in item.ValueJson["data"]["world"]["Tools"].AsBsonArray.Where(x => x["ToolId"] == toolId)) tool["RemainingCharges"] = 3;
        events = await TelemetryServiceTests.IngestV02Trace(f.MatchId, events, Now, V02TelemetryAuthority(f), f.HostId);
        var result = await new AEDV02SourceVerifier(f.Db, new AcceptedEvidenceRepository(events), new FixedClock(Now)).VerifyAsync(f.MatchId, request);
        Assert.True(result.IsSuccess, result.Message);
    }

    [Fact, Trait("Category", "AEDV02")]
    public async Task V02_CanonicalReviveWithoutConsumedFirstAidCannotCertifyLifecycle()
    {
        await using var f = await ValidV02Fixture();
        var (world, request) = await V02WorldEvidence(f, geometry: true);
        var trace = V02PressureTrace(f, world).Where(x => x.EventType != "TEAM_TOOL_LIFECYCLE" || x.ValueJson["data"]["action"] != "CONSUME")
            .Select((x, i) => V02Event(f.MatchId, x.EventType, i + 1, x.Ts, x.SchemaVersion, x.ValueJson, x.UserId)).ToArray();
        var accepted = await TelemetryServiceTests.IngestV02Trace(f.MatchId, trace, Now, V02TelemetryAuthority(f), f.HostId);
        var result = await new AEDV02SourceVerifier(f.Db, new AcceptedEvidenceRepository(accepted), new FixedClock(Now)).VerifyAsync(f.MatchId, request);
        Assert.False(result.IsSuccess); Assert.Equal("HOLD_REVIVE_TOOL_SOURCE_MISMATCH", result.Message);
        Assert.Empty(f.Db.AEDV02VerifiedSources);
    }

    [Fact, Trait("Category", "AEDV02")]
    public async Task V02_PostDownAttackStateRetainsCoverageWithoutInventingAnotherEpisode()
    {
        await using var f = await ValidV02Fixture();
        var (world, request) = await V02WorldEvidence(f, geometry: true);
        var user = f.Db.MatchPlayerBindings.First(x => x.MatchId == f.MatchId).UserId;
        var events = world.ToList();
        foreach (var item in events.Where(x => x.EventType == "AED_V02_WORLD_SAMPLE"))
        {
            var sample = JsonSerializer.Deserialize<AEDV02WorldSample>(item.ValueJson["data"]["world"].ToJson())!;
            bool down = sample.Tick >= 1200;
            sample.Stalkers[0].State = down ? "ATTACK" : "CHASE"; sample.Stalkers[0].TargetUserId = user;
            sample.Stalkers[0].EpisodeId = down ? string.Empty : "actual:chase";
            sample.Players.Single(x => x.UserId == user).LifeState = down ? "DOWNED" : "ALIVE";
            item.ValueJson["data"]["world"] = BsonDocument.Parse(JsonSerializer.Serialize(sample));
        }
        events.Add(V02Event(f.MatchId, "PLAYER_DOWNED", 0, Now.AddSeconds(-20), "1.1", new BsonDocument {
            ["context"] = new BsonDocument { ["phase"] = "CORE_COLLECTION", ["authorityTick"] = 1200 }, ["data"] = new BsonDocument() }, user));
        var ordered = events.OrderBy(x => x.Ts).ThenBy(x => x.EventType == "AED_V02_WORLD_SAMPLE" ? 1 : 0)
            .Select((x, i) => V02Event(f.MatchId, x.EventType, i + 1, x.Ts, x.SchemaVersion, x.ValueJson, x.UserId));
        var accepted = await TelemetryServiceTests.IngestV02Trace(f.MatchId, ordered, Now, V02TelemetryAuthority(f), f.HostId);
        var result = await new AEDV02SourceVerifier(f.Db, new AcceptedEvidenceRepository(accepted), new FixedClock(Now)).VerifyAsync(f.MatchId, request);
        Assert.True(result.IsSuccess, result.Message); Assert.False(result.Data!.SafeWindow);
        Assert.InRange(result.Data.PressureComponents[user][0], .01, .17);
        Assert.True(result.Data.PressureComponents[user][2] > 0);
        Assert.Contains(user, result.Data.RecentDownUsers);
    }

    [Theory, Trait("Category", "AEDV02")]
    [InlineData("OPEN", 0d)]
    [InlineData("LOCKED", .5)]
    public async Task V02_ReviveScarcityUsesCanonicalDownAndRealReachability(string doorState, double scarcity)
    {
        await using var f = await ValidV02Fixture();
        var (original, request) = await V02WorldEvidence(f);
        var content = f.Db.AEDV02SceneContents.Single();
        var manifest = JsonSerializer.Deserialize<AEDV02SceneManifest>(content.ManifestJson)!;
        manifest.NavCells = new[] {
            new AEDV02NavCell { Id = 0, A = new[] { 0d, 0, 0 }, B = new[] { 10d, 0, 0 }, C = new[] { 0d, 0, 10 } },
            new AEDV02NavCell { Id = 1, A = new[] { 10d, 0, 0 }, B = new[] { 10d, 0, 10 }, C = new[] { 0d, 0, 10 } }
        };
        manifest.NavLinks = new[] { new AEDV02NavLink { From = 0, To = 1, DoorIds = new[] { "door" } }, new AEDV02NavLink { From = 1, To = 0, DoorIds = new[] { "door" } } };
        var json = JsonSerializer.Serialize(manifest); var fingerprint = AEDV02Plan.Hash(json);
        f.Db.AEDV02SceneContents.Remove(content);
        f.Db.AEDV02SceneContents.Add(new AEDV02SceneContent { Fingerprint = fingerprint, ManifestJson = json, SceneVersion = manifest.SceneVersion, RegisteredAtUtc = Now });
        await f.Db.SaveChangesAsync(); request = request with { SceneManifestFingerprint = fingerprint };
        var user = f.Db.MatchPlayerBindings.First(x => x.MatchId == f.MatchId).UserId;
        var events = original.ToList();
        foreach (var item in events.Where(x => x.EventType == "AED_V02_WORLD_SAMPLE"))
        {
            var sample = JsonSerializer.Deserialize<AEDV02WorldSample>(item.ValueJson["data"]["world"].ToJson())!;
            sample.SceneManifestFingerprint = fingerprint; sample.Doors[0].State = doorState;
            foreach (var player in sample.Players) { player.X = 1; player.Z = 1; }
            var firstAid = sample.Tools.Single(x => x.ToolId == 3); firstAid.X = 9; firstAid.Z = 9;
            if (sample.Tick >= 1200) sample.Players.Single(x => x.UserId == user).LifeState = "DOWNED";
            item.ValueJson["data"]["world"] = BsonDocument.Parse(JsonSerializer.Serialize(sample));
        }
        events.Add(V02Event(f.MatchId, "PLAYER_DOWNED", 0, Now.AddSeconds(-20), "1.1", new BsonDocument {
            ["context"] = new BsonDocument { ["phase"] = "CORE_COLLECTION", ["authorityTick"] = 1200 }, ["data"] = new BsonDocument() }, user));
        var ordered = events.OrderBy(x => x.Ts).ThenBy(x => x.EventType == "AED_V02_WORLD_SAMPLE" ? 1 : 0)
            .Select((x, i) => V02Event(f.MatchId, x.EventType, i + 1, x.Ts, x.SchemaVersion, x.ValueJson, x.UserId)).ToArray();
        var result = await new AEDV02SourceVerifier(f.Db, new AcceptedEvidenceRepository(ordered), new FixedClock(Now)).VerifyAsync(f.MatchId, request);
        Assert.True(result.IsSuccess, result.Message);
        Assert.False(result.Data!.SafeWindow);
        Assert.Equal(scarcity, result.Data.PressureComponents[user][3], 6);
        Assert.True(result.Data.PressureComponents[user][2] > 0);
        Assert.Equal(0, result.Data.PressureComponents.Single(x => x.Key != user).Value[3]);
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
    [InlineData("missing-stalker", "HOLD_STALKER_CENSUS_MISSING")]
    public async Task V02_RealVerifier_RejectsCorruptIncompleteAndStaleSources(string failure, string reason)
    {
        await using var f = await ValidV02Fixture();
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
            case "missing-stalker": sample.Stalkers = Array.Empty<AEDV02ThreatSource>(); break;
        }
        sampleEvent.ValueJson["data"]["world"] = BsonDocument.Parse(JsonSerializer.Serialize(sample));
        if (failure == "gap") events = events.Where(x => x.EventSequence != 20).ToArray();
        var verifier = new AEDV02SourceVerifier(f.Db, new AcceptedEvidenceRepository(events), new FixedClock(failure == "stale" ? Now.AddSeconds(11) : Now));
        var result = await verifier.VerifyAsync(f.MatchId, request);
        Assert.False(result.IsSuccess);
        Assert.Equal(reason, result.Message);
        Assert.Empty(f.Db.AEDV02VerifiedSources);
    }

    private static async Task<(TelemetryEventDocument[] Events, AEDV02SourceRequest Request)> V02WorldEvidence(Fixture f, bool geometry = false, int planRevision = 0)
    {
        f.Db.MatchAuthorityBindings.Single(x => x.MatchId == f.MatchId).Status = MatchAuthorityStatus.InMatch;
        var users = f.Db.MatchPlayerBindings.Where(x => x.MatchId == f.MatchId).Select(x => x.UserId).ToArray();
        var roster = AEDV02Plan.Hash(f.MatchId.ToString("D") + "|" + string.Join(',', users.Order().Select(x => x.ToString("D"))));
        var ids = new[] { 1, 2, 3, 4, 6 };
        var manifest = new AEDV02SceneManifest { SceneVersion = "TEST_SCENE_REV02",
            Markers = new[] { new AEDV02SceneMarker { Id = "Z1_ENTRY", Zone = 1, Kind = "ENTRY" } }
                .Concat(ids.Select(x => new AEDV02SceneMarker { Id = "marker:" + x, Zone = 1, Kind = "TOOL", AllowedToolIds = new[] { x } })).ToArray(),
            Edges = ids.Select(x => new AEDV02RouteEdge { Id = "edge:" + x, From = "Z1_ENTRY", To = "marker:" + x, PathMeters = 10, DoorId = "door" }).ToArray() };
        if (geometry) manifest.NavCells = new[] { new AEDV02NavCell { Id = 0, A = new[] { 0d, 0, 0 }, B = new[] { 10d, 0, 0 }, C = new[] { 0d, 0, 10 } } };
        var json = JsonSerializer.Serialize(manifest); var fingerprint = AEDV02Plan.Hash(json);
        if (!f.Db.AEDV02SceneContents.Any(x => x.Fingerprint == fingerprint))
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
                PlanRevision = planRevision, PlanFingerprint = AEDV02Plan.Normal().Fingerprint(), RosterIdentity = roster, SceneManifestFingerprint = fingerprint,
                Players = users.Select(x => new AEDV02PlayerSource { UserId = x, LifeState = "ALIVE" }).ToArray(),
                Stalkers = new[] { new AEDV02ThreatSource { NetworkId = "stalker", State = "PATROL", Zone = 1,
                    ConsumerValues = Enumerable.Range(0, 6).Select(index => AEDV02Plan.Normal().Get((AEDV02Key)index)).ToArray() } },
                Tools = ids.Select(x => new AEDV02ToolSource { InstanceId = "tool:" + x, ToolId = x, Zone = 1, Origin = "WorldSpawn", Location = "World", MarkerId = "marker:" + x, RouteEdgeIds = new[] { "edge:" + x } }).ToArray(),
                Doors = new[] { new AEDV02DoorSource { DoorId = "door", State = "CLOSED_UNLOCKED" } } };
            var value = JsonSerializer.SerializeToElement(new { context = new { authorityTick = sample.Tick, phase = sample.PhaseName, occurrenceKey = "world:" + sample.Tick }, data = new { world = sample } });
            events.Add(V02Event(f.MatchId, "AED_V02_WORLD_SAMPLE", events.Count + 1, Now.AddSeconds(i - 30), "1.3", BsonDocument.Parse(value.GetRawText())));
        }
        return (await TelemetryServiceTests.IngestV02Trace(f.MatchId, events, Now, V02TelemetryAuthority(f), f.HostId), new AEDV02SourceRequest(1, 600, 2400, fingerprint));
    }
    private static MatchAuthorityService V02TelemetryAuthority(Fixture f) => new(f.Db, null!,
        Microsoft.Extensions.Options.Options.Create(new EchoProtocol.Api.Configurations.MatchAuthoritySettings()), new FixedClock(Now));
    private static TelemetryEventDocument V02Event(Guid matchId, string type, long sequence, DateTime ts, string version, BsonDocument value, Guid? userId = null, string? reasonCode = null)
    {
        value["context"]["eventSequence"] = sequence;
        var context = value["context"].AsBsonDocument;
        context["authorityTick"] = context.GetValue("authorityTick", 0);
        context["scenarioConfigVersion"] = "FIXED_BASELINE_V1";
        context["policyVersion"] = AEDV02PilotPolicy.Version;
        context["configSource"] = "FIXED";
        context["occurrenceKey"] = context.GetValue("occurrenceKey", type + ":" + Guid.NewGuid().ToString("N"));
        if (version is "1.2" or "1.3") context["researchCaptureEnabled"] = true;
        if (type == "PLAYER_DOWNED") context["monsterType"] = "STALKER";
        return new TelemetryEventDocument { Id = Guid.NewGuid(), MatchId = matchId, UserId = userId, EventType = type,
            ReasonCode = reasonCode ?? (type == "MATCH_STARTED" ? "MATCH_READY" : type == "MATCH_ENDED" ? "TEAM_ESCAPED" : type == "PLAYER_DOWNED" ? "STALKER_ATTACK" : type == "PLAYER_REVIVED" ? "TEAMMATE_REVIVE" : null),
            EventSequence = sequence, Ts = ts, SchemaVersion = version,
            SemanticFingerprint = AEDV02Plan.Hash(value.ToJson()), IngestedAt = Now, ValueJson = value };
    }
}
