using EchoProtocol.Api.Data.Telemetry;
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

    private static BsonDocument Tool(string action, string id, int toolId, int zone, Guid? owner, int? charges, string parent)
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
