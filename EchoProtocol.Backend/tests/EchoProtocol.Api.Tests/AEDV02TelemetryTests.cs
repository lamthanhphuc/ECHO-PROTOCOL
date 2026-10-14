using System.Text.Json;
using System.Text.Json.Nodes;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.DTOs.Telemetry;
using EchoProtocol.Api.Data.Telemetry;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed partial class TelemetryServiceTests
{
    [Theory, Trait("Category", "AEDV02")]
    [InlineData("WORLD")]
    [InlineData("EFFECT")]
    public async Task V02_Ingestion_AcceptsVersionedSourcesAndDeduplicatesReplay(string kind)
    {
        var repo = new FakeTelemetryEventRepository();
        var batch = CreateMatchStarted(researchCaptureEnabled: true);
        var match = batch.Events[0].MatchId;
        batch.Events.Add(CreatePhaseStarted(match, 2));
        batch.Events.Add(V02Telemetry(match, kind));
        var service = CreateService(repo);
        var caller = Guid.NewGuid();
        var accepted = await service.IngestBatchAsync(batch, caller, CancellationToken.None);
        Assert.True(accepted.IsSuccess, accepted.Message);
        Assert.True(accepted.Data!.Items[2].Status == TelemetryAckStatuses.Accepted, accepted.Data.Items[2].RejectReason);
        Assert.Single(repo.InsertedEvents, x => x.Id == batch.Events[2].Id);
        repo.Boundaries[match] = new TelemetryMatchBoundary(true, null);
        repo.ConflictResults[batch.Events[2].Id] = new TelemetryWriteItemResult(batch.Events[2].Id, TelemetryWriteStatus.DuplicateAlreadyAccepted);
        var replay = await service.IngestBatchAsync(new TelemetryBatchRequest { Events = [batch.Events[2]] }, caller, CancellationToken.None);
        Assert.Equal(TelemetryAckStatuses.DuplicateAlreadyAccepted, replay.Data!.Items[0].Status);
        Assert.Single(repo.InsertedEvents, x => x.Id == batch.Events[2].Id);
    }

    [Theory, Trait("Category", "AEDV02")]
    [InlineData("unknown-root")]
    [InlineData("unknown-context")]
    [InlineData("unknown-data")]
    [InlineData("unknown-source-field")]
    [InlineData("capture-disabled")]
    [InlineData("context-capture-disabled")]
    [InlineData("fixed-mode")]
    [InlineData("difficulty")]
    [InlineData("wrong-tick")]
    [InlineData("null-player")]
    [InlineData("invalid-door")]
    [InlineData("effect-future-tick")]
    public async Task V02_Ingestion_RejectsMalformedSources(string failure)
    {
        var repo = new FakeTelemetryEventRepository();
        var batch = CreateMatchStarted(researchCaptureEnabled: failure != "capture-disabled");
        var match = batch.Events[0].MatchId;
        batch.Events.Add(CreatePhaseStarted(match, 2));
        var item = V02Telemetry(match, failure == "effect-future-tick" ? "EFFECT" : "WORLD");
        var json = JsonNode.Parse(item.ValueJson.GetRawText())!;
        switch (failure)
        {
            case "unknown-root": json["verified"] = true; break;
            case "unknown-context": json["context"]!["verified"] = true; break;
            case "unknown-data": json["data"]!["verified"] = true; break;
            case "unknown-source-field": json["data"]!["world"]!["Verified"] = true; break;
            case "context-capture-disabled": json["context"]!["researchCaptureEnabled"] = false; break;
            case "fixed-mode": json["data"]!["world"]!["ResolutionMode"] = "Fixed"; break;
            case "difficulty": json["data"]!["world"]!["Difficulty"] = "Hard"; break;
            case "wrong-tick": json["data"]!["world"]!["Tick"] = 43; break;
            case "null-player": json["data"]!["world"]!["Players"]![0] = null; break;
            case "invalid-door": json["data"]!["world"]!["Doors"] = new JsonArray(new JsonObject { ["DoorId"] = "door", ["State"] = "TRUST_ME" }); break;
            case "effect-future-tick": json["data"]!["effect"]!["ObservedTick"] = 43; break;
        }
        item.ValueJson = JsonSerializer.SerializeToElement(json);
        batch.Events.Add(item);
        var result = await CreateService(repo).IngestBatchAsync(batch, Guid.NewGuid(), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(TelemetryAckStatuses.PermanentlyRejected, result.Data!.Items[2].Status);
        Assert.DoesNotContain(repo.InsertedEvents, x => x.Id == item.Id);
    }

    private static TelemetryEventRequest V02Telemetry(Guid match, string kind)
    {
        var item = CreateP8ToolEvent(match, "1.3");
        var root = JsonNode.Parse(item.ValueJson.GetRawText())!;
        if (kind == "WORLD")
        {
            item.EventType = "AED_V02_WORLD_SAMPLE";
            root["data"] = new JsonObject { ["world"] = JsonSerializer.SerializeToNode(new AEDV02WorldSample
            {
                ResolutionMode = "Adaptive", Difficulty = "Normal", Tick = 42, TickRate = 60,
                PhaseOrdinal = 1, PhaseName = "CORE_COLLECTION", LogicalPhase = 1, Zone = 1, PlanRevision = 1,
                PlanFingerprint = AEDV02Plan.Normal().Fingerprint(), RosterIdentity = AEDV02Plan.Hash("roster"), SceneManifestFingerprint = AEDV02Plan.Hash("scene"),
                Players = [new AEDV02PlayerSource { UserId = Guid.NewGuid(), LifeState = "ALIVE", HideSpotId = ulong.MaxValue.ToString() }]
            }) };
        }
        else
        {
            item.EventType = "AED_V02_ACTION_EFFECT";
            root["data"] = new JsonObject { ["effect"] = JsonSerializer.SerializeToNode(new AEDV02EffectSource
            {
                DecisionId = Guid.NewGuid(), PlanRevision = 1, PlanFingerprint = AEDV02Plan.Normal().Fingerprint(),
                Consumer = "StalkerFusionRuntime", Key = "ChaseSpeed", ObservedValue = 7.5, ObservedTick = 42, NetworkObjectIds = ["stalker:1"]
            }) };
        }
        item.ValueJson = JsonSerializer.SerializeToElement(root);
        return item;
    }
}
