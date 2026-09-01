using System.Text.Json;
using EchoProtocol.Api.DTOs.Telemetry;

namespace EchoProtocol.Api.Services;

internal static class TelemetryV11Validator
{
    private static readonly HashSet<string> TeamToolTypes =
        ["FIELD_SCANNER", "NOISE_MAKER", "FIRST_AID_KIT", "DOOR_JAMMER"];

    private static readonly Dictionary<string, string> NoiseReasonByType = new()
    {
        ["SPRINT"] = "PLAYER_SPRINT",
        ["INTERACTION"] = "OBJECT_INTERACTION",
        ["CORE_CARRY"] = "CORE_CARRY_MOVEMENT",
        ["CORE_DROP"] = "CORE_DROP",
        ["NOISE_MAKER"] = "NOISE_MAKER_USED"
    };

    private static readonly HashSet<string> ResearchEvents =
    [
        "MONSTER_INVESTIGATE_STARTED",
        "MONSTER_INVESTIGATE_RESOLVED",
        "MONSTER_ATTACK_RESOLVED",
        "MONSTER_SEARCH_ENDED",
        "WARDEN_TELEGRAPH_STARTED",
        "WARDEN_ROUTE_ACTION_APPLIED",
        "WARDEN_ROUTE_SAFETY_CHECKED",
        "WARDEN_ROUTE_ACTION_RELEASED"
    ];

    public static bool TryValidate(
        TelemetryEventRequest telemetryEvent,
        out long eventSequence,
        out string rejectReason)
    {
        eventSequence = 0;
        rejectReason = string.Empty;

        if (telemetryEvent.ValueJson.ValueKind != JsonValueKind.Object ||
            !TryObject(telemetryEvent.ValueJson, "context", out var context) ||
            !TryObject(telemetryEvent.ValueJson, "data", out var data))
        {
            return Reject("TELEMETRY_VALUE_JSON_INVALID", out rejectReason);
        }

        if (!TryPositiveInt64(context, "eventSequence", out eventSequence) ||
            !HasNullableNonNegativeInt64(context, "authorityTick") ||
            !HasText(context, "scenarioConfigVersion") ||
            !HasText(context, "policyVersion") ||
            !HasOneOf(context, "configSource", "FIXED", "ADAPTIVE"))
        {
            return Reject("TELEMETRY_COMMON_CONTEXT_INVALID", out rejectReason);
        }

        if (telemetryEvent.ReasonCode is not null &&
            (!IsUpperSnakeCase(telemetryEvent.ReasonCode) || telemetryEvent.ReasonCode.Length > 100))
        {
            return Reject("TELEMETRY_REASON_CODE_INVALID", out rejectReason);
        }

        if (ResearchEvents.Contains(telemetryEvent.EventType))
        {
            return Reject("TELEMETRY_RESEARCH_EVENT_NOT_ENABLED", out rejectReason);
        }

        return telemetryEvent.EventType switch
        {
            "MATCH_STARTED" => ValidateMatchStarted(telemetryEvent, context, data, eventSequence, out rejectReason),
            "MATCH_ENDED" => ValidateMatchEnded(telemetryEvent, context, data, out rejectReason),
            "PHASE_STARTED" => ValidatePhase(telemetryEvent, context, true, out rejectReason),
            "PHASE_COMPLETED" => ValidatePhase(telemetryEvent, context, false, out rejectReason),
            "SECURITY_HOLD_INTERRUPTED" => ValidateSystemPhaseEvent(
                telemetryEvent, context, "SECURITY_HOLD", null, out rejectReason),
            "CORE_PICKED_UP" => ValidateCoreEvent(
                telemetryEvent, context, data, "PLAYER_PICKUP", out rejectReason),
            "CORE_DROPPED" => ValidateCoreEvent(
                telemetryEvent, context, data, "PLAYER_DROP", out rejectReason),
            "CORE_PLACED" => ValidateCoreEvent(
                telemetryEvent, context, data, "CORE_OBJECTIVE_PLACED", out rejectReason),
            "PUZZLE_COMPLETED" => ValidateSystemPhaseEvent(
                telemetryEvent, context, "POWER_PUZZLE", null, out rejectReason),
            "PLAYER_DOWNED" => ValidatePlayerDowned(telemetryEvent, context, out rejectReason),
            "PLAYER_REVIVED" => ValidatePlayerRevived(telemetryEvent, context, data, out rejectReason),
            "PLAYER_ELIMINATED" => ValidatePlayerEvent(
                telemetryEvent, context, "REVIVE_LIMIT_REACHED", out rejectReason),
            "PLAYER_ESCAPED" => ValidateSystemPhaseForUser(
                telemetryEvent, context, "FINAL_HUNT", "EXIT_REACHED", out rejectReason),
            "TEAM_TOOL_USED" => ValidateTeamTool(telemetryEvent, context, data, out rejectReason),
            "HELP_PING_USED" => ValidatePlayerEvent(
                telemetryEvent, context, "PLAYER_REQUESTED_HELP", out rejectReason),
            "NOISE_EMITTED" => ValidateNoise(telemetryEvent, context, data, out rejectReason),
            _ => Reject("TELEMETRY_EVENT_TYPE_UNSUPPORTED", out rejectReason)
        };
    }

    private static bool ValidateMatchStarted(
        TelemetryEventRequest item,
        JsonElement context,
        JsonElement data,
        long sequence,
        out string reason)
    {
        if (item.UserId is not null || sequence != 1 || item.ReasonCode != "MATCH_READY" ||
            !TryInt32(context, "teamSize", out var teamSize) || teamSize is < 1 or > 4 ||
            !HasText(context, "buildVersion") ||
            !HasText(context, "mapContentVersion") ||
            !HasText(context, "contentWhitelistVersion") ||
            !HasBoolean(context, "researchCaptureEnabled") ||
            !HasText(data, "mapId"))
        {
            return Reject("TELEMETRY_MATCH_STARTED_INVALID", out reason);
        }

        var hasCondition = context.TryGetProperty("experimentCondition", out var condition);
        var hasProtocol = context.TryGetProperty("experimentProtocolVersion", out var protocol);
        if (hasCondition != hasProtocol ||
            (hasCondition &&
             (condition.ValueKind != JsonValueKind.String ||
              condition.GetString() is not ("FIXED" or "ADAPTIVE") ||
              protocol.ValueKind != JsonValueKind.String ||
              string.IsNullOrWhiteSpace(protocol.GetString()))))
        {
            return Reject("TELEMETRY_EXPERIMENT_PROVENANCE_INVALID", out reason);
        }

        reason = string.Empty;
        return true;
    }

    private static bool ValidateMatchEnded(
        TelemetryEventRequest item,
        JsonElement context,
        JsonElement data,
        out string reason)
    {
        if (item.UserId is not null ||
            !HasOneOf(context, "phase", "MATCH_END") ||
            item.ReasonCode is not ("TEAM_ESCAPED" or "TEAM_ELIMINATED" or "MATCH_ABORTED") ||
            !HasText(data, "outcome") ||
            !TryNonNegativeNumber(data, "durationSeconds") ||
            !TryInt32(data, "survivorCount", out var survivors) || survivors < 0)
        {
            return Reject("TELEMETRY_MATCH_ENDED_INVALID", out reason);
        }

        reason = string.Empty;
        return true;
    }

    private static bool ValidatePhase(
        TelemetryEventRequest item,
        JsonElement context,
        bool started,
        out string reason)
    {
        var validReason = started
            ? item.ReasonCode is null or "PREVIOUS_PHASE_COMPLETED"
            : item.ReasonCode is null or "OBJECTIVE_COMPLETED";
        if (item.UserId is not null || !HasText(context, "phase") || !validReason)
        {
            return Reject(started
                ? "TELEMETRY_PHASE_STARTED_INVALID"
                : "TELEMETRY_PHASE_COMPLETED_INVALID", out reason);
        }

        reason = string.Empty;
        return true;
    }

    private static bool ValidateSystemPhaseEvent(
        TelemetryEventRequest item,
        JsonElement context,
        string phase,
        string? expectedReason,
        out string reason)
    {
        if (item.UserId is not null || !HasOneOf(context, "phase", phase) ||
            item.ReasonCode != expectedReason)
        {
            return Reject("TELEMETRY_SYSTEM_EVENT_INVALID", out reason);
        }

        reason = string.Empty;
        return true;
    }

    private static bool ValidateCoreEvent(
        TelemetryEventRequest item,
        JsonElement context,
        JsonElement data,
        string expectedReason,
        out string reason)
    {
        if (item.UserId is null || !HasOneOf(context, "phase", "CORE_COLLECTION") ||
            item.ReasonCode != expectedReason || !HasText(data, "coreId") ||
            !HasOptionalPosition(context))
        {
            return Reject("TELEMETRY_CORE_EVENT_INVALID", out reason);
        }

        reason = string.Empty;
        return true;
    }

    private static bool ValidatePlayerDowned(
        TelemetryEventRequest item,
        JsonElement context,
        out string reason)
    {
        if (item.UserId is null || !HasText(context, "phase") || !HasOptionalPosition(context) ||
            !context.TryGetProperty("monsterType", out var monster) ||
            monster.ValueKind != JsonValueKind.String)
        {
            return Reject("TELEMETRY_PLAYER_DOWNED_INVALID", out reason);
        }

        var monsterType = monster.GetString();
        var matching = (monsterType == "STALKER" && item.ReasonCode == "STALKER_ATTACK") ||
                       (monsterType == "LISTENER" && item.ReasonCode == "LISTENER_ATTACK");
        if (!matching)
        {
            return Reject("TELEMETRY_PLAYER_DOWNED_REASON_MISMATCH", out reason);
        }

        reason = string.Empty;
        return true;
    }

    private static bool ValidatePlayerRevived(
        TelemetryEventRequest item,
        JsonElement context,
        JsonElement data,
        out string reason)
    {
        if (item.UserId is null || !HasText(context, "phase") ||
            item.ReasonCode != "TEAMMATE_REVIVE" ||
            !data.TryGetProperty("reviverPlayerId", out var reviver) ||
            reviver.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(reviver.GetString(), out var reviverId) || reviverId == Guid.Empty)
        {
            return Reject("TELEMETRY_PLAYER_REVIVED_INVALID", out reason);
        }

        reason = string.Empty;
        return true;
    }

    private static bool ValidatePlayerEvent(
        TelemetryEventRequest item,
        JsonElement context,
        string expectedReason,
        out string reason)
    {
        if (item.UserId is null || !HasText(context, "phase") || item.ReasonCode != expectedReason)
        {
            return Reject("TELEMETRY_PLAYER_EVENT_INVALID", out reason);
        }

        reason = string.Empty;
        return true;
    }

    private static bool ValidateSystemPhaseForUser(
        TelemetryEventRequest item,
        JsonElement context,
        string phase,
        string expectedReason,
        out string reason)
    {
        if (item.UserId is null || !HasOneOf(context, "phase", phase) || item.ReasonCode != expectedReason)
        {
            return Reject("TELEMETRY_PLAYER_EVENT_INVALID", out reason);
        }

        reason = string.Empty;
        return true;
    }

    private static bool ValidateTeamTool(
        TelemetryEventRequest item,
        JsonElement context,
        JsonElement data,
        out string reason)
    {
        if (item.UserId is null || !HasText(context, "phase") ||
            item.ReasonCode != "PLAYER_ACTIVATED_TOOL" ||
            !data.TryGetProperty("toolType", out var toolType) ||
            toolType.ValueKind != JsonValueKind.String ||
            !TeamToolTypes.Contains(toolType.GetString()!))
        {
            return Reject("TELEMETRY_TEAM_TOOL_INVALID", out reason);
        }

        reason = string.Empty;
        return true;
    }

    private static bool ValidateNoise(
        TelemetryEventRequest item,
        JsonElement context,
        JsonElement data,
        out string reason)
    {
        if (item.UserId is null || !HasText(context, "phase") || !HasRequiredPosition(context) ||
            !HasText(data, "noiseEventId") ||
            !data.TryGetProperty("noiseType", out var noiseTypeElement) ||
            noiseTypeElement.ValueKind != JsonValueKind.String ||
            !NoiseReasonByType.TryGetValue(noiseTypeElement.GetString()!, out var expectedReason) ||
            item.ReasonCode != expectedReason || !TryNonNegativeNumber(data, "loudness") ||
            (data.TryGetProperty("hearingRadius", out var radius) &&
             (radius.ValueKind != JsonValueKind.Number || radius.GetDouble() < 0)))
        {
            return Reject("TELEMETRY_NOISE_EVENT_INVALID", out reason);
        }

        reason = string.Empty;
        return true;
    }

    private static bool TryObject(JsonElement parent, string name, out JsonElement value) =>
        parent.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Object;

    private static bool HasText(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString());

    private static bool HasBoolean(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False;

    private static bool HasOneOf(JsonElement parent, string name, params string[] allowed) =>
        parent.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        allowed.Contains(value.GetString(), StringComparer.Ordinal);

    private static bool TryPositiveInt64(JsonElement parent, string name, out long result)
    {
        result = 0;
        return parent.TryGetProperty(name, out var value) &&
               value.ValueKind == JsonValueKind.Number &&
               value.TryGetInt64(out result) && result > 0;
    }

    private static bool TryInt32(JsonElement parent, string name, out int result)
    {
        result = 0;
        return parent.TryGetProperty(name, out var value) &&
               value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out result);
    }

    private static bool TryNonNegativeNumber(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && number >= 0;

    private static bool HasNullableNonNegativeInt64(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value))
        {
            return false;
        }

        return value.ValueKind == JsonValueKind.Null ||
               (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var tick) && tick >= 0);
    }

    private static bool HasOptionalPosition(JsonElement context) =>
        !context.TryGetProperty("position", out var position) || IsPosition(position);

    private static bool HasRequiredPosition(JsonElement context) =>
        context.TryGetProperty("position", out var position) && IsPosition(position);

    private static bool IsPosition(JsonElement position) =>
        position.ValueKind == JsonValueKind.Object &&
        HasNumber(position, "x") && HasNumber(position, "y") && HasNumber(position, "z");

    private static bool HasNumber(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number;

    private static bool IsUpperSnakeCase(string value)
    {
        foreach (var character in value)
        {
            if (!(character is >= 'A' and <= 'Z') &&
                !(character is >= '0' and <= '9') &&
                character != '_')
            {
                return false;
            }
        }

        return value.Length > 0;
    }

    private static bool Reject(string code, out string rejectReason)
    {
        rejectReason = code;
        return false;
    }
}
