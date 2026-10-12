using EchoProtocol.Api.Data.Telemetry;
using MongoDB.Bson;

namespace EchoProtocol.Api.Services;

public static class AEDPhase8LifecycleReconcilerV1
{
    private sealed record Tool(int ToolId, int Zone, string Location, Guid? Owner, int? Charges, long Tick);

    public static bool TryVerify(IEnumerable<TelemetryEventDocument> accepted, Guid matchId, long completedSequence,
        IReadOnlySet<Guid> roster, out string reason)
    {
        reason = "P8_SOURCE_LIFECYCLE_INVALID";
        if (accepted == null || matchId == Guid.Empty || completedSequence <= 0 || roster == null || roster.Count == 0) return false;
        var events = accepted.Where(x => x.MatchId == matchId && x.EventSequence <= completedSequence)
            .OrderBy(x => x.EventSequence).ToArray();
        if (events.Length == 0 || events[^1].EventSequence != completedSequence || events[^1].EventType != "PHASE_COMPLETED") return false;
        var tools = new Dictionary<string, Tool>(StringComparer.Ordinal);
        var held = new Dictionary<Guid, string>();
        var minions = new Dictionary<string, (int Zone, long Tick, bool Alive)>(StringComparer.Ordinal);
        var sequences = new HashSet<long>();
        var occurrences = new HashSet<string>(StringComparer.Ordinal);
        string phase = null;
        foreach (var item in events)
        {
            if (item.EventSequence <= 0 || !sequences.Add(item.EventSequence)) return false;
            if (item.EventType == "PHASE_STARTED")
            {
                if (phase != null || !ReadContextText(item, "phase", out var startedPhase)) return false;
                phase = startedPhase; continue;
            }
            if (item.EventType == "PHASE_COMPLETED")
            {
                if (phase == null || !ReadContextText(item, "phase", out var endedPhase) || endedPhase != phase) return false;
                phase = null; continue;
            }
            if (item.EventType is not ("TEAM_TOOL_LIFECYCLE" or "MINION_LIFECYCLE")) continue;
            if (phase == null || item.SchemaVersion != "1.2" || item.UserId.HasValue ||
                !ReadContextText(item, "phase", out var eventPhase) || eventPhase != phase ||
                !ReadContextText(item, "occurrenceKey", out var occurrence) || !occurrences.Add(occurrence) ||
                !ReadContextTick(item, out var tick) || !ReadData(item, out var data)) return false;
            if (item.EventType == "TEAM_TOOL_LIFECYCLE")
            {
                if (!ReplayTool(data, tick, roster, tools, held)) return false;
            }
            else if (!ReplayMinion(data, tick, minions)) return false;
        }
        if (phase != null) return false;
        reason = string.Empty;
        return true;
    }

    private static bool ReplayTool(BsonDocument data, long tick, IReadOnlySet<Guid> roster,
        Dictionary<string, Tool> tools, Dictionary<Guid, string> held)
    {
        if (!Text(data, "instanceId", out var id) || !Text(data, "action", out var action) ||
            !Integer(data, "toolId", out var toolId) || toolId is not (1 or 2 or 3 or 4 or 6) ||
            !Integer(data, "zone", out var zone) || zone > 3 || !Charges(data, out var charges)) return false;
        if (action == "SPAWN")
        {
            if (tools.ContainsKey(id) || charges.HasValue || data.Contains("ownerUserId") || data.Contains("parentInstanceId")) return false;
            if (data.Contains("origin") && (!Text(data, "origin", out var origin) || origin is not ("WorldSpawn" or "ScenePlaced"))) return false;
            tools.Add(id, new Tool(toolId, zone, "World", null, null, tick)); return true;
        }
        if (action == "LOADOUT")
        {
            if (zone != 0 || !Text(data, "origin", out var origin) || origin != "LobbyLoadout" || data.Contains("parentInstanceId") ||
                !Owner(data, roster, out var owner) || tools.ContainsKey(id) || held.ContainsKey(owner) || !ValidInitialCharges(toolId, charges)) return false;
            tools.Add(id, new Tool(toolId, zone, "Player", owner, charges, tick)); held.Add(owner, id); return true;
        }
        if (action == "DROP")
        {
            if (data.Contains("origin") || !Text(data, "parentInstanceId", out var parentId) || id == parentId || tools.ContainsKey(id) ||
                !Owner(data, roster, out var owner) || !held.TryGetValue(owner, out var heldId) || heldId != parentId ||
                !tools.TryGetValue(parentId, out var parent) || parent.Location != "Player" || parent.Owner != owner ||
                parent.ToolId != toolId || parent.Zone != zone || parent.Charges != charges || tick <= parent.Tick) return false;
            tools[parentId] = parent with { Location = "Transferred", Owner = null, Tick = tick }; held.Remove(owner);
            tools.Add(id, new Tool(toolId, zone, "World", null, charges, tick)); return true;
        }
        if (!tools.TryGetValue(id, out var previous) || previous.ToolId != toolId || previous.Zone != zone ||
            data.Contains("origin") || data.Contains("parentInstanceId") || tick <= previous.Tick || !Owner(data, roster, out var actor)) return false;
        if (action == "PICKUP")
        {
            if (previous.Location != "World" || held.ContainsKey(actor) || !ValidInitialCharges(toolId, charges) ||
                (previous.Charges.HasValue && previous.Charges != charges)) return false;
            tools[id] = previous with { Location = "Player", Owner = actor, Charges = charges, Tick = tick }; held.Add(actor, id); return true;
        }
        if (action is not ("USE" or "CONSUME") || previous.Location != "Player" || previous.Owner != actor ||
            !held.TryGetValue(actor, out var heldTool) || heldTool != id) return false;
        if (toolId is 2 or 4)
        {
            if (!previous.Charges.HasValue || !charges.HasValue || previous.Charges.Value - charges.Value != 1 ||
                (action == "USE" && charges.Value == 0) || (action == "CONSUME" && charges.Value != 0)) return false;
        }
        else if (action != "CONSUME" || charges != 0 || previous.Charges.HasValue) return false;
        var consumed = action == "CONSUME";
        tools[id] = previous with { Location = consumed ? "Consumed" : "Player", Owner = actor, Charges = charges, Tick = tick };
        if (consumed) held.Remove(actor);
        return true;
    }

    private static bool ReplayMinion(BsonDocument data, long tick, Dictionary<string, (int Zone, long Tick, bool Alive)> minions)
    {
        if (!Text(data, "networkInstanceId", out var id) || !Text(data, "state", out var state) || !Integer(data, "zone", out var zone) || zone > 3) return false;
        if (state == "SPAWNED") { if (minions.ContainsKey(id)) return false; minions.Add(id, (zone, tick, true)); return true; }
        if (state != "DESPAWNED" || !minions.TryGetValue(id, out var previous) || !previous.Alive || previous.Zone != zone || tick <= previous.Tick) return false;
        minions[id] = (zone, tick, false); return true;
    }

    private static bool ValidInitialCharges(int toolId, int? charges) => toolId is 2 or 4 ? charges is >= 1 and <= 3 : !charges.HasValue;
    private static bool Owner(BsonDocument data, IReadOnlySet<Guid> roster, out Guid owner)
    {
        owner = Guid.Empty;
        return Text(data, "ownerUserId", out var value) && Guid.TryParse(value, out owner) && owner != Guid.Empty && roster.Contains(owner);
    }
    private static bool Charges(BsonDocument data, out int? charges)
    {
        charges = null;
        if (!data.TryGetValue("remainingCharges", out var raw) || raw.IsBsonNull) return true;
        if (!raw.IsInt32 || raw.AsInt32 < 0) return false; charges = raw.AsInt32; return true;
    }
    private static bool Integer(BsonDocument data, string key, out int number)
    {
        number = 0; return data.TryGetValue(key, out var raw) && raw.IsInt32 && raw.AsInt32 >= 0 && (number = raw.AsInt32) >= 0;
    }
    private static bool Text(BsonDocument data, string key, out string value)
    {
        value = null; return data.TryGetValue(key, out var raw) && raw.IsString && !string.IsNullOrWhiteSpace(value = raw.AsString);
    }
    private static bool ReadData(TelemetryEventDocument item, out BsonDocument data)
    {
        data = null; return item.ValueJson.TryGetValue("data", out var raw) && raw.IsBsonDocument && (data = raw.AsBsonDocument) != null;
    }
    private static bool ReadContextText(TelemetryEventDocument item, string key, out string value)
    {
        value = null; return item.ValueJson.TryGetValue("context", out var raw) && raw.IsBsonDocument && Text(raw.AsBsonDocument, key, out value);
    }
    private static bool ReadContextTick(TelemetryEventDocument item, out long tick)
    {
        tick = -1;
        if (!item.ValueJson.TryGetValue("context", out var context) || !context.IsBsonDocument || !context.AsBsonDocument.TryGetValue("authorityTick", out var raw)) return false;
        if (raw.IsInt64) tick = raw.AsInt64; else if (raw.IsInt32) tick = raw.AsInt32;
        return tick >= 0;
    }
}
