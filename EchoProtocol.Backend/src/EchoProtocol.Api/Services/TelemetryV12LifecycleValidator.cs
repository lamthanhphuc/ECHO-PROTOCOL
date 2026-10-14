using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using EchoProtocol.Api.DTOs.Telemetry;

namespace EchoProtocol.Api.Services;

internal static class TelemetryV12LifecycleValidator
{
    private static readonly HashSet<string> ContextFields =
    [
        "eventSequence", "authorityTick", "scenarioConfigVersion", "policyVersion",
        "configSource", "occurrenceKey", "phase", "researchCaptureEnabled"
    ];

    private static readonly HashSet<string> ToolFields =
        ["instanceId", "parentInstanceId", "toolId", "zone", "action", "ownerUserId", "remainingCharges", "origin"];

    private static readonly HashSet<string> MinionFields =
    ["networkInstanceId", "zone", "state"];

    public static bool IsLifecycleEvent(string eventType) =>
        eventType is "TEAM_TOOL_LIFECYCLE" or "MINION_LIFECYCLE";

    public static bool TryValidate(
        TelemetryEventRequest item,
        bool? researchCaptureAllowed,
        out TelemetryValidationResult result,
        out string rejectReason)
    {
        result = new(0, false, null, false);
        rejectReason = "P8_TELEMETRY_V12_INVALID";

        if (!IsLifecycleEvent(item.EventType))
        {
            rejectReason = "TELEMETRY_EVENT_TYPE_UNSUPPORTED";
            return false;
        }

        if (researchCaptureAllowed != true)
        {
            rejectReason = "TELEMETRY_RESEARCH_CAPTURE_NOT_ENABLED";
            return false;
        }

        if (item.UserId.HasValue || item.ReasonCode != null ||
            item.ValueJson.ValueKind != JsonValueKind.Object ||
            !Object(item.ValueJson, "context", out var context) ||
            !Object(item.ValueJson, "data", out var data) ||
            !Fields(item.ValueJson, ["context", "data"]) ||
            !Fields(context, ContextFields) ||
            !PositiveLong(context, "eventSequence", out var sequence) ||
            !NonNegativeLong(context, "authorityTick") ||
            !Text(context, "scenarioConfigVersion") || !Text(context, "policyVersion") ||
            !Text(context, "occurrenceKey") || !Text(context, "phase") ||
            !IsTrue(context, "researchCaptureEnabled") ||
            !StringIn(context, "configSource", "FIXED", "ADAPTIVE"))
            return false;

        var valid = item.EventType switch
        {
            "TEAM_TOOL_LIFECYCLE" => ValidTool(data),
            "MINION_LIFECYCLE" => ValidMinion(data),
            _ => false
        };

        if (!valid) return false;

        result = new(sequence, false, null, false);
        rejectReason = string.Empty;
        return true;
    }

    private static bool ValidTool(JsonElement data)
    {
        if (!Fields(data, ToolFields) || !Text(data, "instanceId") ||
            !Int(data, "toolId", out var toolId) || toolId is not (1 or 2 or 3 or 4 or 6) ||
            !Int(data, "zone", out var zone) || zone is < 0 or > 3 ||
            !Text(data, "action")) return false;

        var action = data.GetProperty("action").GetString();
        if (action is not ("SPAWN" or "PICKUP" or "DROP" or "USE" or "CONSUME" or "LOADOUT")) return false;

        if (data.TryGetProperty("remainingCharges", out var charges) &&
            charges.ValueKind != JsonValueKind.Null &&
            (charges.ValueKind != JsonValueKind.Number || !charges.TryGetInt32(out var count) || count < 0))
            return false;

        if (action == "LOADOUT")
        {
            if (zone != 0 ||
                !StringIn(data, "origin", "LobbyLoadout") ||
                data.TryGetProperty("parentInstanceId", out _) ||
                !Text(data, "ownerUserId") ||
                !Guid.TryParse(data.GetProperty("ownerUserId").GetString(), out var loadoutOwner) ||
                loadoutOwner == Guid.Empty)
                return false;

            if (toolId is 2 or 4)
                return Int(data, "remainingCharges", out var loadoutCharges) &&
                       loadoutCharges is >= 1 and <= 3;

            return !data.TryGetProperty("remainingCharges", out _);
        }

        if (action == "SPAWN")
        {
            if (data.TryGetProperty("remainingCharges", out var spawnCharges) && spawnCharges.ValueKind != JsonValueKind.Null &&
                (toolId is not (2 or 4) || !spawnCharges.TryGetInt32(out var spawnUses) || spawnUses is < 1 or > 3)) return false;
            return !data.TryGetProperty("origin", out _) ||
                   StringIn(data, "origin", "WorldSpawn", "ScenePlaced");
        }

        if (data.TryGetProperty("origin", out _)) return false;

        if (action is "USE" or "CONSUME")
        {
            if (data.TryGetProperty("parentInstanceId", out _) || !Text(data, "ownerUserId") ||
                !Guid.TryParse(data.GetProperty("ownerUserId").GetString(), out var useOwner) ||
                useOwner == Guid.Empty || !Int(data, "remainingCharges", out var uses)) return false;
            return action == "CONSUME" ? uses == 0 : uses > 0 && toolId is 2 or 4;
        }

        if (action == "DROP")
            return Text(data, "parentInstanceId") && Text(data, "ownerUserId") &&
                   data.GetProperty("parentInstanceId").GetString() != data.GetProperty("instanceId").GetString() &&
                   Guid.TryParse(data.GetProperty("ownerUserId").GetString(), out var dropOwner) &&
                   dropOwner != Guid.Empty;

        if (data.TryGetProperty("parentInstanceId", out _)) return false;

        if (action == "PICKUP")
            return Text(data, "ownerUserId") && Guid.TryParse(
                data.GetProperty("ownerUserId").GetString(), out var owner) && owner != Guid.Empty;

        return !data.TryGetProperty("ownerUserId", out var spawnOwner) ||
               spawnOwner.ValueKind == JsonValueKind.Null;
    }

    private static bool ValidMinion(JsonElement data) =>
        Fields(data, MinionFields) && Text(data, "networkInstanceId") &&
        Int(data, "zone", out var zone) && zone is >= 0 and <= 3 &&
        StringIn(data, "state", "SPAWNED", "DESPAWNED");

    private static bool Object(JsonElement parent, string key, out JsonElement value)
    {
        value = default;
        return parent.TryGetProperty(key, out value) && value.ValueKind == JsonValueKind.Object;
    }

    private static bool Fields(JsonElement value, IEnumerable<string> allowed) =>
        value.ValueKind == JsonValueKind.Object && value.EnumerateObject().All(x => allowed.Contains(x.Name));

    private static bool Text(JsonElement value, string key) =>
        value.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(property.GetString());

    private static bool Int(JsonElement value, string key, out int result)
    {
        result = default;
        return value.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.Number &&
               property.TryGetInt32(out result);
    }

    private static bool PositiveLong(JsonElement value, string key, out long result)
    {
        result = default;
        return value.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.Number &&
               property.TryGetInt64(out result) && result > 0;
    }

    private static bool NonNegativeLong(JsonElement value, string key) =>
        value.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.Number &&
        property.TryGetInt64(out var number) && number >= 0;

    private static bool IsTrue(JsonElement value, string key) =>
        value.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.True;

    private static bool StringIn(JsonElement value, string key, params string[] values) =>
        Text(value, key) && values.Contains(value.GetProperty(key).GetString(), StringComparer.Ordinal);
}
