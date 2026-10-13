using EchoProtocol.Api.Data.Telemetry;
using EchoProtocol.Api.DTOs.Scenarios;
using EchoProtocol.Api.Services;
using MongoDB.Bson;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class AEDPhase8LifecycleReconcilerTests
{
    [Fact, Trait("Category", "AEDv2")]
    public void ValidLifecycle_ReconcilesAllToolAndMinionTransitions()
    {
        var events = Build(out var matchId, out var owner);
        Assert.True(AEDPhase8LifecycleReconcilerV1.TryVerify(events, matchId, 11,
            new HashSet<Guid> { owner }, out var reason));
        Assert.Equal(string.Empty, reason);
    }

    [Theory, Trait("Category", "AEDv2")]
    [InlineData("missing-parent")]
    [InlineData("wrong-owner")]
    [InlineData("skipped-charge")]
    [InlineData("wrong-minion-zone")]
    public void InvalidLifecycle_IsRejected(string corruption)
    {
        var events = Build(out var matchId, out var owner);
        switch (corruption)
        {
            case "missing-parent": Data(events[4])["parentInstanceId"] = "missing"; break;
            case "wrong-owner": Data(events[2])["ownerUserId"] = Guid.NewGuid().ToString("D"); break;
            case "skipped-charge": Data(events[3])["remainingCharges"] = 1; break;
            case "wrong-minion-zone": Data(events[9])["zone"] = 3; break;
        }
        Assert.False(AEDPhase8LifecycleReconcilerV1.TryVerify(events, matchId, 11,
            new HashSet<Guid> { owner }, out var reason));
        Assert.Equal("P8_SOURCE_LIFECYCLE_INVALID", reason);
    }

    [Fact, Trait("Category", "AEDv2")]
    public void MatchingSidecars_AcceptsRepeatedToolInstanceAcrossActions()
    {
        var events = Build(out var matchId, out var owner);
        var (supply, minions) = Sidecars(owner);
        Assert.True(AEDPhase8LifecycleReconcilerV1.TryMatchPhaseSidecars(events, matchId, 1, 11, supply, minions, out var reason));
        Assert.Equal(string.Empty, reason);
    }

    [Theory, Trait("Category", "AEDv2")]
    [InlineData("missing-supply")]
    [InlineData("extra-supply")]
    [InlineData("duplicate-occurrence")]
    [InlineData("wrong-origin")]
    [InlineData("wrong-location")]
    [InlineData("wrong-owner")]
    [InlineData("wrong-charges")]
    [InlineData("wrong-tick")]
    [InlineData("wrong-minion-state")]
    [InlineData("missing-minion")]
    [InlineData("wrong-minion-tick")]
    public void CorruptedSidecars_AreRejected(string corruption)
    {
        var events = Build(out var matchId, out var owner);
        var (supply, minions) = Sidecars(owner);
        switch (corruption)
        {
            case "missing-supply": supply.RemoveAt(2); break;
            case "extra-supply": supply.Add(supply[0] with { SourceOccurrenceKey = "forged-extra" }); break;
            case "duplicate-occurrence": supply[2] = supply[2] with { SourceOccurrenceKey = supply[1].SourceOccurrenceKey }; break;
            case "wrong-origin": supply[2] = supply[2] with { Origin = "ScenePlaced" }; break;
            case "wrong-location": supply[3] = supply[3] with { Location = "Player" }; break;
            case "wrong-owner": supply[2] = supply[2] with { OwnerUserId = Guid.NewGuid() }; break;
            case "wrong-charges": supply[3] = supply[3] with { RemainingCharges = 1 }; break;
            case "wrong-tick": supply[4] = supply[4] with { SourceTick = 999 }; break;
            case "wrong-minion-state": minions[1] = minions[1] with { State = "SPAWNED" }; break;
            case "missing-minion": minions.RemoveAt(0); break;
            case "wrong-minion-tick": minions[1] = minions[1] with { SourceTick = 91 }; break;
        }
        Assert.False(AEDPhase8LifecycleReconcilerV1.TryMatchPhaseSidecars(events, matchId, 1, 11, supply, minions, out var reason));
        Assert.NotEqual(string.Empty, reason);
    }

    private static (List<AEDPhase8SupplyEvidenceV1> Supply, List<AEDPhase8MinionEvidenceV1> Minions) Sidecars(Guid owner)
    {
        var supply = new List<AEDPhase8SupplyEvidenceV1>
        {
            new("tool-a", 2, 1, "WorldSpawn", "World", null, null, true, 10, "spawn"),
            new("tool-a", 2, 1, "WorldSpawn", "Player", owner, 3, true, 20, "pickup-a"),
            new("tool-a", 2, 1, "WorldSpawn", "Player", owner, 2, true, 30, "use-a"),
            new("tool-b", 2, 1, "PlayerDrop", "World", null, 2, true, 40, "drop"),
            new("tool-b", 2, 1, "PlayerDrop", "Player", owner, 2, true, 50, "pickup-b"),
            new("tool-b", 2, 1, "PlayerDrop", "Player", owner, 1, true, 60, "use-b"),
            new("tool-b", 2, 1, "PlayerDrop", "Consumed", owner, 0, false, 70, "consume")
        };
        var minions = new List<AEDPhase8MinionEvidenceV1>
        {
            new("minion-1", 2, "SPAWNED", 80, "minion-spawn"),
            new("minion-1", 2, "DESPAWNED", 90, "minion-despawn")
        };
        return (supply, minions);
    }

    private static List<TelemetryEventDocument> Build(out Guid matchId, out Guid owner)
    {
        matchId = Guid.NewGuid(); owner = Guid.NewGuid();
        return new()
        {
            Event(matchId, "PHASE_STARTED", 1, 0, "CORE_COLLECTION", "start", new()),
            Event(matchId, "TEAM_TOOL_LIFECYCLE", 2, 10, "CORE_COLLECTION", "spawn", Tool("SPAWN", "tool-a", 2, 1, null, null, null)),
            Event(matchId, "TEAM_TOOL_LIFECYCLE", 3, 20, "CORE_COLLECTION", "pickup-a", Tool("PICKUP", "tool-a", 2, 1, owner, 3, null)),
            Event(matchId, "TEAM_TOOL_LIFECYCLE", 4, 30, "CORE_COLLECTION", "use-a", Tool("USE", "tool-a", 2, 1, owner, 2, null)),
            Event(matchId, "TEAM_TOOL_LIFECYCLE", 5, 40, "CORE_COLLECTION", "drop", Tool("DROP", "tool-b", 2, 1, owner, 2, "tool-a")),
            Event(matchId, "TEAM_TOOL_LIFECYCLE", 6, 50, "CORE_COLLECTION", "pickup-b", Tool("PICKUP", "tool-b", 2, 1, owner, 2, null)),
            Event(matchId, "TEAM_TOOL_LIFECYCLE", 7, 60, "CORE_COLLECTION", "use-b", Tool("USE", "tool-b", 2, 1, owner, 1, null)),
            Event(matchId, "TEAM_TOOL_LIFECYCLE", 8, 70, "CORE_COLLECTION", "consume", Tool("CONSUME", "tool-b", 2, 1, owner, 0, null)),
            Event(matchId, "MINION_LIFECYCLE", 9, 80, "CORE_COLLECTION", "minion-spawn", Minion("SPAWNED", "minion-1", 2)),
            Event(matchId, "MINION_LIFECYCLE", 10, 90, "CORE_COLLECTION", "minion-despawn", Minion("DESPAWNED", "minion-1", 2)),
            Event(matchId, "PHASE_COMPLETED", 11, 100, "CORE_COLLECTION", "end", new())
        };
    }

    private static BsonDocument Tool(
        string action,
        string id,
        int toolId,
        int zone,
        Guid? owner,
        int? charges,
        string? parent)
    {
        var data = new BsonDocument { ["action"] = action, ["instanceId"] = id, ["toolId"] = toolId, ["zone"] = zone };
        if (owner.HasValue) data["ownerUserId"] = owner.Value.ToString("D");
        if (charges.HasValue) data["remainingCharges"] = charges.Value;
        if (parent != null) data["parentInstanceId"] = parent;
        return data;
    }
    private static BsonDocument Minion(string state, string id, int zone) => new() { ["networkInstanceId"] = id, ["state"] = state, ["zone"] = zone };
    private static BsonDocument Data(TelemetryEventDocument item) => item.ValueJson["data"].AsBsonDocument;
    private static TelemetryEventDocument Event(Guid matchId, string type, long sequence, long tick, string phase, string occurrence, BsonDocument data) => new()
    {
        Id = Guid.NewGuid(), MatchId = matchId, EventType = type, EventSequence = sequence,
        SchemaVersion = type is "TEAM_TOOL_LIFECYCLE" or "MINION_LIFECYCLE" ? "1.2" : "1.1",
        ValueJson = new BsonDocument { ["context"] = new BsonDocument { ["phase"] = phase, ["authorityTick"] = tick, ["occurrenceKey"] = occurrence }, ["data"] = data }
    };
}
