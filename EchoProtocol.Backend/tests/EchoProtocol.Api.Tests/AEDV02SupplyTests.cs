using System.Text.Json;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data.Telemetry;
using EchoProtocol.Api.DTOs.Scenarios;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed partial class ScenarioServiceTests
{
    [Theory, Trait("Category", "AEDV02")]
    [InlineData("valid")]
    [InlineData("missing-source")]
    [InlineData("missing-required")]
    [InlineData("unregistered-marker")]
    [InlineData("forged-initial-budget")]
    [InlineData("forged-materialized")]
    public async Task V02_IssuedSupportSurvivesCompositePreMatchAndReceiptRequiresRealSupply(string mutation)
    {
        await using var f = await ValidAEDv2Fixture();
        var scenario = (await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, AdaptiveRequest())).Data!;
        Assert.Equal(1, scenario.Config.SupportItemBudget);
        var (original, sourceRequest) = await V02WorldEvidence(f);
        var content = f.Db.AEDV02SceneContents.Single();
        var manifest = JsonSerializer.Deserialize<AEDV02SceneManifest>(content.ManifestJson)!;
        var ids = new[] { 1, 2, 3, 4, 6 };
        manifest.Markers = manifest.Markers.Concat(Enumerable.Range(2, 2).SelectMany(zone => ids.Select(id =>
            new AEDV02SceneMarker { Id = $"z{zone}:tool:{id}", Zone = zone, Kind = "TOOL", AllowedToolIds = [id] })))
            .Append(new AEDV02SceneMarker { Id = "optional-marker", Zone = 1, Kind = "TOOL", AllowedToolIds = [3], X = 3 }).ToArray();
        manifest.Edges = manifest.Edges.Append(new AEDV02RouteEdge { Id = "optional-edge", From = "Z1_ENTRY", To = "optional-marker", PathMeters = 3 }).ToArray();
        f.Db.AEDV02SceneContents.Remove(content);
        var json = JsonSerializer.Serialize(manifest); var scene = AEDV02Plan.Hash(json);
        f.Db.AEDV02SceneContents.Add(new() { Fingerprint = scene, ManifestJson = json, SceneVersion = manifest.SceneVersion, RegisteredAtUtc = Now });
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.Lobby; await f.Db.SaveChangesAsync();
        var plan = AEDV02Plan.Normal().With(AEDV02Key.OptionalToolBudget, 1).With(AEDV02Key.CabinetAmbushEnabled, 1);
        var events = new List<TelemetryEventDocument>(); var repository = new MutableV02Repository(events); var clock = new MutableV02Clock(Now);
        var verifier = new AEDV02SourceVerifier(f.Db, repository, clock);
        var service = new AEDV02DecisionService(f.Db, verifier, repository, clock,
            Options.Create(new AEDV02PilotSettings { Enabled = true, CabinetAmbushEnabled = true }));
        var approval = await service.SubmitAsync(f.HostId, f.MatchId, V02Request(f, scenario, scene, plan, 0, null, 0));
        Assert.True(approval.IsSuccess, approval.Message); Assert.Equal("APPROVED", approval.Data!.ApprovalStatus);
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.InMatch; await f.Db.SaveChangesAsync();
        foreach (var item in original.Where(x => x.EventType != "AED_V02_WORLD_SAMPLE"))
            events.Add(V02Event(f.MatchId, item.EventType, events.Count + 1, Now.AddSeconds(item.EventType == "PHASE_STARTED" ? 1 : 2),
                item.SchemaVersion, item.ValueJson.DeepClone().AsBsonDocument));
        events.Add(V02Event(f.MatchId, "TEAM_TOOL_LIFECYCLE", events.Count + 1, Now.AddSeconds(2), "1.2", new BsonDocument {
            ["context"] = new BsonDocument { ["phase"] = "CORE_COLLECTION", ["authorityTick"] = 599 },
            ["data"] = new BsonDocument { ["action"] = "SPAWN", ["instanceId"] = "optional:1", ["toolId"] = 3, ["zone"] = 1, ["origin"] = "WorldSpawn" } }));
        var effect = V02Effect(f, approval.Data, "AEDPolicyEnvelope", AEDV02Key.CabinetAmbushEnabled, 600, events.Count + 1, Now.AddSeconds(3));
        events.Add(effect);
        int ordinal = 0;
        foreach (var item in original.Where(x => x.EventType == "AED_V02_WORLD_SAMPLE"))
        {
            var world = JsonSerializer.Deserialize<AEDV02WorldSample>(item.ValueJson["data"]["world"].ToJson())!;
            world.PlanRevision = 1; world.PlanFingerprint = plan.Fingerprint(); world.SceneManifestFingerprint = scene;
            world.Tools = world.Tools.Append(new AEDV02ToolSource { InstanceId = "optional:1", ToolId = 3, Zone = 1,
                Origin = "WorldSpawn", Location = "World", MarkerId = "optional-marker", RouteEdgeIds = ["optional-edge"], X = 3 }).ToArray();
            world.Supply = new AEDV02SupplySource { InitialOptionalBudget = 1, OptionalBudget = 1, OptionalMaterialized = 1, SpawnedZones = [1],
                Reservations = manifest.Markers.Where(x => x.Zone > 1 && x.Kind == "TOOL").Select(x => new AEDV02SupplyReservation
                { Zone = x.Zone, ToolId = x.AllowedToolIds.Single(), MarkerId = x.Id }).ToArray() };
            switch (mutation)
            {
                case "missing-source": world.Supply = null; break;
                case "missing-required": world.Supply.Reservations[0].ToolId = 6; break;
                case "unregistered-marker": world.Supply.Reservations[0].MarkerId = "not-registered"; break;
                case "forged-initial-budget": world.Supply.InitialOptionalBudget = 2; break;
                case "forged-materialized": world.Supply.OptionalMaterialized = 0; break;
            }
            events.Add(V02Event(f.MatchId, "AED_V02_WORLD_SAMPLE", events.Count + 1, Now.AddSeconds(3 + ordinal++), "1.3",
                BsonDocument.Parse(JsonSerializer.Serialize(new { context = new { phase = "CORE_COLLECTION", authorityTick = world.Tick }, data = new { world } }))));
        }
        clock.UtcNow = Now.AddSeconds(33);
        var accepted = await TelemetryServiceTests.IngestV02Trace(f.MatchId, events, clock.UtcNow, V02TelemetryAuthority(f), f.HostId);
        events.Clear(); events.AddRange(accepted);
        var receipt = new AEDV02ReceiptRequest(Guid.NewGuid(), effect.Id, 1, plan.Fingerprint(), 600);
        var confirmed = await service.ConfirmAsync(f.HostId, f.MatchId, approval.Data.DecisionId, receipt);
        if (mutation != "valid")
        { Assert.False(confirmed.IsSuccess); Assert.Equal("HOLD_SUPPLY_RESERVATION_SOURCE_REQUIRED", confirmed.Message); return; }
        Assert.True(confirmed.IsSuccess, confirmed.Message); Assert.Equal("APPLIED", confirmed.Data!.ApplyStatus);
        Assert.True((await service.ConfirmAsync(f.HostId, f.MatchId, approval.Data.DecisionId, receipt)).Data!.IsReplay);
        var source = await verifier.VerifyAsync(f.MatchId, sourceRequest with { SceneManifestFingerprint = scene });
        Assert.True(source.IsSuccess, source.Message); Assert.False(source.Data!.PressureComplete);
        Assert.Equal(1, source.Data.Supply!.InitialOptionalBudget); Assert.Equal(1, source.Data.Supply.OptionalMaterialized);
        Assert.Equal(0, source.Data.Zone3MinionPopulation);
        Assert.All(events.Where(x => x.EventType == "AED_V02_WORLD_SAMPLE"), x =>
            Assert.Equal(6, JsonSerializer.Deserialize<AEDV02WorldSample>(x.ValueJson["data"]["world"].ToJson())!.Tools.Length));
    }

    [Theory, Trait("Category", "AEDV02")]
    [InlineData("discard-support", "HOLD_PREMATCH_SUPPLY_MISMATCH")]
    [InlineData("invent-support", "HOLD_PREMATCH_SUPPLY_MISMATCH")]
    [InlineData("changed-candidate", "HOLD_PREMATCH_LEGACY_TUNING_UNMAPPED")]
    public async Task V02_PreMatchCannotOverwriteOrInventIssuedLegacyCandidate(string mutation, string expected)
    {
        await using var f = await ValidAEDv2Fixture();
        var scenario = (await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, AdaptiveRequest())).Data!;
        var (events, source) = await V02WorldEvidence(f);
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.Lobby;
        if (mutation == "changed-candidate") f.Db.ScenarioConfigs.Single(x => !x.IsFixedFallback).ChaseSpeed = 10;
        await f.Db.SaveChangesAsync();
        var repository = new AcceptedEvidenceRepository(events);
        var service = new AEDV02DecisionService(f.Db, new AEDV02SourceVerifier(f.Db, repository, new FixedClock(Now)), repository,
            new FixedClock(Now), Options.Create(new AEDV02PilotSettings { Enabled = true }));
        var plan = AEDV02Plan.Normal().With(AEDV02Key.OptionalToolBudget, mutation == "discard-support" ? 0 : mutation == "invent-support" ? 2 : 1);
        var result = await service.SubmitAsync(f.HostId, f.MatchId, V02Request(f, scenario, source.SceneManifestFingerprint, plan, 0, null, 0));
        Assert.True(result.IsSuccess, result.Message); Assert.Equal("HOLD", result.Data!.ApprovalStatus); Assert.Equal(expected, result.Data.ReasonCode);
    }
}
