using System.Globalization;
using System.Text.Json;
using EchoProtocol.Api.DTOs.Telemetry;

namespace EchoProtocol.Api.Services;

internal sealed record TelemetryValidationResult(
    long EventSequence,
    bool IsMatchStarted,
    bool? ResearchCaptureEnabled,
    bool IsMatchEnded);

internal static class TelemetrySchemaDispatcher
{
    public static bool IsResearchCaptureEvent(TelemetryEventRequest telemetryEvent) =>
        telemetryEvent.SchemaVersion == "1.1" &&
        TelemetryV11Validator.IsResearchCaptureEvent(telemetryEvent.EventType);

    public static bool TryValidate(
        TelemetryEventRequest telemetryEvent,
        bool? researchCaptureAllowed,
        out TelemetryValidationResult result,
        out string rejectReason)
    {
        return telemetryEvent.SchemaVersion switch
        {
            "1.1" => TelemetryV11Validator.TryValidate(
                telemetryEvent, researchCaptureAllowed, out result, out rejectReason),
            "1.0" => Reject("TELEMETRY_LEGACY_V10_UNSUPPORTED", out result, out rejectReason),
            _ => Reject("TELEMETRY_SCHEMA_UNSUPPORTED", out result, out rejectReason)
        };
    }

    private static bool Reject(
        string code,
        out TelemetryValidationResult result,
        out string rejectReason)
    {
        result = new TelemetryValidationResult(0, false, null, false);
        rejectReason = code;
        return false;
    }
}

internal static class TelemetryV11Validator
{
    private static readonly string[] UtcTimestampFormats =
    [
        "yyyy-MM-dd'T'HH:mm:ss'Z'",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"
    ];

    private static readonly HashSet<string> EmptyFields = [];
    private static readonly HashSet<string> CommonContextFields =
    [
        "eventSequence",
        "authorityTick",
        "scenarioConfigVersion",
        "policyVersion",
        "configSource"
    ];

    private static readonly HashSet<string> MatchStartedOptionalContextFields =
    [
        "experimentCondition",
        "experimentProtocolVersion",
        "testRunId",
        "experimentRunId",
        "parameterRegistryVersion",
        "fixedBaselineId"
    ];

    private static readonly HashSet<string> PositionFields = ["x", "y", "z"];
    private static readonly HashSet<string> TeamToolTypes =
        ["FIELD_SCANNER", "NOISE_MAKER", "FIRST_AID_KIT", "DOOR_JAMMER"];
    private static readonly HashSet<string> ReservedEvents =
    [
        "PUZZLE_FAILED",
        "SECURITY_HOLD_STARTED",
        "SECURITY_HOLD_COMPLETED",
        "FINAL_HUNT_STARTED",
        "PLAYER_RESCUED",
        "MONSTER_TARGET_ACQUIRED",
        "MONSTER_TARGET_LOST"
    ];
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

    public static bool IsResearchCaptureEvent(string eventType) =>
        ResearchEvents.Contains(eventType);

    private static readonly Dictionary<string, string> NoiseReasonByType = new()
    {
        ["SPRINT"] = "PLAYER_SPRINT",
        ["INTERACTION"] = "OBJECT_INTERACTION",
        ["CORE_CARRY"] = "CORE_CARRY_MOVEMENT",
        ["CORE_DROP"] = "CORE_DROP",
        ["NOISE_MAKER"] = "NOISE_MAKER_USED"
    };

    private static readonly HashSet<string> ListenerInvestigationSelectionReasons =
    [
        "INITIAL_HIGHEST_AUDIBILITY",
        "NEXT_REACHABLE_CANDIDATE",
        "PENDING_OBSERVATION_SELECTED",
        "INTERRUPTED_BY_STRONGER_NOISE",
        "CHASE_INTERRUPTED_BY_NOISE"
    ];
    private static readonly HashSet<string> ListenerInvestigationOutcomes =
    [
        "PLAYER_CONFIRMED",
        "FALSE_INVESTIGATION",
        "INTERRUPTED_BY_HIGHER_PRIORITY_NOISE",
        "NAVIGATION_FAILED",
        "CANCELLED_BY_MATCH_END",
        "CANCELLED_BY_LISTENER_DISABLE"
    ];
    private static readonly HashSet<string> AttackOutcomes = ["HIT", "MISS"];
    private static readonly HashSet<string> SearchOutcomes =
    [
        "SAME_TARGET_REACQUIRED",
        "NEW_ELIGIBLE_TARGET_OBSERVED",
        "TIMEOUT",
        "CURRENT_TARGET_INVALID_NO_REPLACEMENT"
    ];
    private static readonly HashSet<string> WardenTelegraphSelectionReasons =
    [
        "HIGHEST_PRESSURE_FRESH_DOOR",
        "HIGHEST_PRESSURE_AFTER_HISTORY_EXHAUSTED",
        "STABLE_TIE_BREAK"
    ];
    private static readonly HashSet<string> WardenSafetyCheckTypes =
        ["POST_APPLY", "ACTIVE_LOCK_REVALIDATION"];
    private static readonly HashSet<string> WardenSafetyStatuses = ["VALID", "REJECTED"];
    private static readonly HashSet<string> WardenSafetyReasons =
    [
        "GRAPH_REVISION_CHANGED",
        "OBJECTIVE_UNKNOWN",
        "REQUIRED_ORIGIN_MISSING",
        "REQUIRED_DESTINATION_MISSING",
        "OBJECTIVE_UNREACHABLE",
        "EXIT_UNREACHABLE",
        "NO_LEGAL_ROUTE",
        "DOOR_STATE_CONFLICT",
        "DOORWAY_OCCUPIED"
    ];
    private static readonly HashSet<string> WardenFailSafeReasons =
    [
        "POST_APPLY_OBJECTIVE_UNREACHABLE",
        "POST_APPLY_EXIT_UNREACHABLE",
        "POST_APPLY_NO_LEGAL_ROUTE",
        "ACTIVE_LOCK_INVALID_AFTER_OBJECTIVE_CHANGE",
        "ACTIVE_LOCK_INVALID_AFTER_SCENARIO_CHANGE",
        "GRAPH_INVALID_WHILE_APPLIED"
    ];

    public static bool TryValidate(
        TelemetryEventRequest telemetryEvent,
        bool? researchCaptureAllowed,
        out TelemetryValidationResult result,
        out string rejectReason)
    {
        result = new TelemetryValidationResult(0, false, null, false);
        rejectReason = string.Empty;

        if (!TryValueObjects(telemetryEvent, out var context, out var data, out rejectReason) ||
            !TryCommonContext(context, out var eventSequence, out rejectReason) ||
            !ValidateReasonShape(telemetryEvent, out rejectReason))
        {
            return false;
        }

        if (ReservedEvents.Contains(telemetryEvent.EventType))
        {
            return Reject("TELEMETRY_RESERVED_EVENT_NOT_EMITTED", out rejectReason);
        }

        if (ResearchEvents.Contains(telemetryEvent.EventType) &&
            (researchCaptureAllowed != true || !HasBooleanValue(context, "researchCaptureEnabled", true)))
        {
            return Reject("TELEMETRY_RESEARCH_CAPTURE_NOT_ENABLED", out rejectReason);
        }

        var valid = telemetryEvent.EventType switch
        {
            "MATCH_STARTED" => ValidateMatchStarted(
                telemetryEvent, context, data, eventSequence, out result, out rejectReason),
            "MATCH_ENDED" => ValidateMatchEnded(
                telemetryEvent, context, data, eventSequence, out result, out rejectReason),
            "PHASE_STARTED" => ValidatePhase(telemetryEvent, context, data, true, out rejectReason),
            "PHASE_COMPLETED" => ValidatePhase(telemetryEvent, context, data, false, out rejectReason),
            "SECURITY_HOLD_INTERRUPTED" => ValidateSystemPhaseEvent(
                telemetryEvent, context, data, "SECURITY_HOLD", null, out rejectReason),
            "CORE_PICKED_UP" => ValidateCoreEvent(
                telemetryEvent, context, data, "PLAYER_PICKUP", out rejectReason),
            "CORE_DROPPED" => ValidateCoreEvent(
                telemetryEvent, context, data, "PLAYER_DROP", out rejectReason),
            "CORE_PLACED" => ValidateCoreEvent(
                telemetryEvent, context, data, "CORE_OBJECTIVE_PLACED", out rejectReason),
            "PUZZLE_COMPLETED" => ValidateSystemPhaseEvent(
                telemetryEvent, context, data, "POWER_PUZZLE", null, out rejectReason),
            "PLAYER_DOWNED" => ValidatePlayerDowned(telemetryEvent, context, data, out rejectReason),
            "PLAYER_REVIVED" => ValidatePlayerRevived(telemetryEvent, context, data, out rejectReason),
            "PLAYER_ELIMINATED" => ValidatePlayerEliminated(telemetryEvent, context, data, out rejectReason),
            "PLAYER_ESCAPED" => ValidatePlayerEscaped(telemetryEvent, context, data, out rejectReason),
            "TEAM_TOOL_USED" => ValidateTeamTool(telemetryEvent, context, data, out rejectReason),
            "HELP_PING_USED" => ValidateHelpPing(telemetryEvent, context, data, out rejectReason),
            "NOISE_EMITTED" => ValidateNoise(telemetryEvent, context, data, out rejectReason),
            "MONSTER_INVESTIGATE_STARTED" => ValidateInvestigateStarted(
                telemetryEvent, context, data, out rejectReason),
            "MONSTER_INVESTIGATE_RESOLVED" => ValidateInvestigateResolved(
                telemetryEvent, context, data, out rejectReason),
            "MONSTER_ATTACK_RESOLVED" => ValidateMonsterAttackResolved(
                telemetryEvent, context, data, out rejectReason),
            "MONSTER_SEARCH_ENDED" => ValidateMonsterSearchEnded(
                telemetryEvent, context, data, out rejectReason),
            "WARDEN_TELEGRAPH_STARTED" => ValidateWardenTelegraph(
                telemetryEvent, context, data, out rejectReason),
            "WARDEN_ROUTE_ACTION_APPLIED" => ValidateWardenApplied(
                telemetryEvent, context, data, out rejectReason),
            "WARDEN_ROUTE_SAFETY_CHECKED" => ValidateWardenSafetyChecked(
                telemetryEvent, context, data, out rejectReason),
            "WARDEN_ROUTE_ACTION_RELEASED" => ValidateWardenReleased(
                telemetryEvent, context, data, out rejectReason),
            _ => Reject("TELEMETRY_EVENT_TYPE_UNSUPPORTED", out rejectReason)
        };

        if (!valid)
        {
            return false;
        }

        result = result with { EventSequence = eventSequence };
        return true;
    }

    private static bool ValidateMatchStarted(
        TelemetryEventRequest item,
        JsonElement context,
        JsonElement data,
        long sequence,
        out TelemetryValidationResult result,
        out string reason)
    {
        result = new TelemetryValidationResult(sequence, false, null, false);
        if (!RequireNoFields(context, CommonContextFields
                .Concat(["teamSize", "buildVersion", "mapContentVersion", "contentWhitelistVersion", "researchCaptureEnabled"])
                .Concat(MatchStartedOptionalContextFields), out reason) ||
            !RequireNoFields(data, ["mapId"], out reason) ||
            item.UserId is not null ||
            sequence != 1 ||
            item.ReasonCode != "MATCH_READY" ||
            !TryInt32(context, "teamSize", out var teamSize) ||
            teamSize is < 1 or > 4 ||
            !HasText(context, "buildVersion") ||
            !HasText(context, "mapContentVersion") ||
            !HasText(context, "contentWhitelistVersion") ||
            !HasBoolean(context, "researchCaptureEnabled", out var researchEnabled) ||
            !HasText(data, "mapId") ||
            !NoPosition(context, out reason))
        {
            return Reject(string.IsNullOrEmpty(reason) ? "TELEMETRY_MATCH_STARTED_INVALID" : reason, out reason);
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

        result = new TelemetryValidationResult(sequence, true, researchEnabled, false);
        reason = string.Empty;
        return true;
    }

    private static bool ValidateMatchEnded(
        TelemetryEventRequest item,
        JsonElement context,
        JsonElement data,
        long sequence,
        out TelemetryValidationResult result,
        out string reason)
    {
        result = new TelemetryValidationResult(sequence, false, null, false);
        if (!RequireContext(context, ["phase"], false, out reason) ||
            !RequireNoFields(data, ["outcome", "durationSeconds", "survivorCount"], out reason) ||
            item.UserId is not null ||
            !HasOneOf(context, "phase", ["MATCH_END"], out reason, "TELEMETRY_MATCH_ENDED_INVALID") ||
            item.ReasonCode is not ("TEAM_ESCAPED" or "TEAM_ELIMINATED" or "MATCH_ABORTED") ||
            !HasOneOf(data, "outcome", TelemetryV11SemanticRegistries.MatchOutcomes, out reason, "TELEMETRY_INVALID_ENUM_TOKEN") ||
            !TryNonNegativeNumber(data, "durationSeconds") ||
            !TryInt32(data, "survivorCount", out var survivors) ||
            survivors < 0)
        {
            return Reject(string.IsNullOrEmpty(reason) ? "TELEMETRY_MATCH_ENDED_INVALID" : reason, out reason);
        }

        result = new TelemetryValidationResult(sequence, false, null, true);
        reason = string.Empty;
        return true;
    }

    private static bool ValidatePhase(
        TelemetryEventRequest item,
        JsonElement context,
        JsonElement data,
        bool started,
        out string reason)
    {
        var validReason = started
            ? item.ReasonCode is null or "PREVIOUS_PHASE_COMPLETED"
            : item.ReasonCode is null or "OBJECTIVE_COMPLETED";
        var dataFields = started ? EmptyFields : new HashSet<string>(["durationSeconds"]);
        if (!RequireContext(context, ["phase"], false, out reason) ||
            !RequireNoFields(data, dataFields, out reason) ||
            item.UserId is not null ||
            !HasText(context, "phase") ||
            !validReason ||
            (data.TryGetProperty("durationSeconds", out var duration) &&
             (duration.ValueKind != JsonValueKind.Number ||
              !duration.TryGetDouble(out var durationValue) ||
              durationValue < 0)))
        {
            return Reject(
                string.IsNullOrEmpty(reason)
                    ? started ? "TELEMETRY_PHASE_STARTED_INVALID" : "TELEMETRY_PHASE_COMPLETED_INVALID"
                    : reason,
                out reason);
        }

        reason = string.Empty;
        return true;
    }

    private static bool ValidateSystemPhaseEvent(
        TelemetryEventRequest item,
        JsonElement context,
        JsonElement data,
        string phase,
        string? expectedReason,
        out string reason)
    {
        if (!RequireContext(context, ["phase"], false, out reason) ||
            !RequireNoFields(data, EmptyFields, out reason) ||
            item.UserId is not null ||
            !HasOneOf(context, "phase", [phase], out reason, "TELEMETRY_SYSTEM_EVENT_INVALID") ||
            item.ReasonCode != expectedReason)
        {
            return Reject(string.IsNullOrEmpty(reason) ? "TELEMETRY_SYSTEM_EVENT_INVALID" : reason, out reason);
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
        if (!RequireContext(context, ["phase"], true, out reason) ||
            !RequireNoFields(data, ["coreId"], out reason) ||
            item.UserId is null ||
            !HasOneOf(context, "phase", ["CORE_COLLECTION"], out reason, "TELEMETRY_CORE_EVENT_INVALID") ||
            item.ReasonCode != expectedReason ||
            !HasText(data, "coreId") ||
            !HasOptionalPosition(context, out reason))
        {
            return Reject(string.IsNullOrEmpty(reason) ? "TELEMETRY_CORE_EVENT_INVALID" : reason, out reason);
        }

        reason = string.Empty;
        return true;
    }

    private static bool ValidatePlayerDowned(
        TelemetryEventRequest item,
        JsonElement context,
        JsonElement data,
        out string reason)
    {
        if (!RequireContext(context, ["phase", "monsterType"], true, out reason) ||
            !RequireNoFields(data, ["downCount"], out reason) ||
            item.UserId is null ||
            !HasText(context, "phase") ||
            !HasOneOf(context, "monsterType", ["STALKER", "LISTENER"], out reason, "TELEMETRY_INVALID_ENUM_TOKEN") ||
            !HasOptionalPosition(context, out reason) ||
            (data.TryGetProperty("downCount", out var downCount) &&
             (downCount.ValueKind != JsonValueKind.Number ||
              !downCount.TryGetInt32(out var count) ||
              count < 0)))
        {
            return Reject(string.IsNullOrEmpty(reason) ? "TELEMETRY_PLAYER_DOWNED_INVALID" : reason, out reason);
        }

        var monsterType = context.GetProperty("monsterType").GetString();
        var matching = (monsterType == "STALKER" && item.ReasonCode == "STALKER_ATTACK") ||
                       (monsterType == "LISTENER" && item.ReasonCode == "LISTENER_ATTACK");
        return matching ? Ok(out reason) : Reject("TELEMETRY_PLAYER_DOWNED_REASON_MISMATCH", out reason);
    }

    private static bool ValidatePlayerRevived(
        TelemetryEventRequest item,
        JsonElement context,
        JsonElement data,
        out string reason)
    {
        if (!RequireContext(context, ["phase"], false, out reason) ||
            !RequireNoFields(data, ["reviverPlayerId", "reviveCount", "usedFirstAidKit"], out reason) ||
            item.UserId is null ||
            !HasText(context, "phase") ||
            item.ReasonCode != "TEAMMATE_REVIVE" ||
            !HasNonEmptyGuidText(data, "reviverPlayerId") ||
            (data.TryGetProperty("reviveCount", out var reviveCount) &&
             (reviveCount.ValueKind != JsonValueKind.Number ||
              !reviveCount.TryGetInt32(out var count) ||
              count < 0)) ||
            (data.TryGetProperty("usedFirstAidKit", out var usedKit) &&
             usedKit.ValueKind is not (JsonValueKind.True or JsonValueKind.False)))
        {
            return Reject(string.IsNullOrEmpty(reason) ? "TELEMETRY_PLAYER_REVIVED_INVALID" : reason, out reason);
        }

        reason = string.Empty;
        return true;
    }

    private static bool ValidatePlayerEliminated(
        TelemetryEventRequest item,
        JsonElement context,
        JsonElement data,
        out string reason)
    {
        if (!RequireContext(context, ["phase"], false, out reason) ||
            !RequireNoFields(data, ["reviveCount"], out reason) ||
            item.UserId is null ||
            !HasText(context, "phase") ||
            item.ReasonCode != "REVIVE_LIMIT_REACHED" ||
            (data.TryGetProperty("reviveCount", out var reviveCount) &&
             (reviveCount.ValueKind != JsonValueKind.Number ||
              !reviveCount.TryGetInt32(out var count) ||
              count < 0)))
        {
            return Reject(string.IsNullOrEmpty(reason) ? "TELEMETRY_PLAYER_EVENT_INVALID" : reason, out reason);
        }

        reason = string.Empty;
        return true;
    }

    private static bool ValidatePlayerEscaped(
        TelemetryEventRequest item,
        JsonElement context,
        JsonElement data,
        out string reason)
    {
        if (!RequireContext(context, ["phase"], false, out reason) ||
            !RequireNoFields(data, ["rescuedTeammate"], out reason) ||
            item.UserId is null ||
            !HasOneOf(context, "phase", ["FINAL_HUNT"], out reason, "TELEMETRY_PLAYER_EVENT_INVALID") ||
            item.ReasonCode != "EXIT_REACHED" ||
            (data.TryGetProperty("rescuedTeammate", out var rescued) &&
             rescued.ValueKind is not (JsonValueKind.True or JsonValueKind.False)))
        {
            return Reject(string.IsNullOrEmpty(reason) ? "TELEMETRY_PLAYER_EVENT_INVALID" : reason, out reason);
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
        if (!RequireContext(context, ["phase"], false, out reason) ||
            !RequireNoFields(data, ["toolType", "targetId"], out reason) ||
            item.UserId is null ||
            !HasText(context, "phase") ||
            item.ReasonCode != "PLAYER_ACTIVATED_TOOL" ||
            !HasOneOf(data, "toolType", TeamToolTypes, out reason, "TELEMETRY_INVALID_ENUM_TOKEN") ||
            (data.TryGetProperty("targetId", out var targetId) &&
             targetId.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)))
        {
            return Reject(string.IsNullOrEmpty(reason) ? "TELEMETRY_TEAM_TOOL_INVALID" : reason, out reason);
        }

        reason = string.Empty;
        return true;
    }

    private static bool ValidateHelpPing(
        TelemetryEventRequest item,
        JsonElement context,
        JsonElement data,
        out string reason)
    {
        if (!RequireContext(context, ["phase"], true, out reason) ||
            !RequireNoFields(data, EmptyFields, out reason) ||
            item.UserId is null ||
            !HasText(context, "phase") ||
            item.ReasonCode != "PLAYER_REQUESTED_HELP" ||
            !HasOptionalPosition(context, out reason))
        {
            return Reject(string.IsNullOrEmpty(reason) ? "TELEMETRY_PLAYER_EVENT_INVALID" : reason, out reason);
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
        if (!RequireContext(context, ["phase"], true, out reason) ||
            !RequireNoFields(data, ["noiseEventId", "noiseType", "loudness", "hearingRadius"], out reason) ||
            item.UserId is null ||
            !HasText(context, "phase") ||
            !HasRequiredPosition(context, out reason) ||
            !HasText(data, "noiseEventId") ||
            !data.TryGetProperty("noiseType", out var noiseTypeElement) ||
            noiseTypeElement.ValueKind != JsonValueKind.String)
        {
            return Reject(string.IsNullOrEmpty(reason) ? "TELEMETRY_NOISE_EVENT_INVALID" : reason, out reason);
        }

        if (!NoiseReasonByType.TryGetValue(noiseTypeElement.GetString()!, out var expectedReason))
        {
            return Reject("TELEMETRY_INVALID_ENUM_TOKEN", out reason);
        }

        if (item.ReasonCode != expectedReason ||
            !TryNonNegativeNumber(data, "loudness") ||
            (data.TryGetProperty("hearingRadius", out var radius) &&
             (radius.ValueKind != JsonValueKind.Number ||
              !radius.TryGetDouble(out var radiusValue) ||
              radiusValue < 0)))
        {
            return Reject(string.IsNullOrEmpty(reason) ? "TELEMETRY_NOISE_EVENT_INVALID" : reason, out reason);
        }

        reason = string.Empty;
        return true;
    }

    private static bool ValidateInvestigateStarted(
        TelemetryEventRequest item,
        JsonElement context,
        JsonElement data,
        out string reason)
    {
        if (!ValidateResearchMonster(
                item,
                context,
                data,
                ["phase", "monsterType", "monsterId", "researchCaptureEnabled"],
                ["investigationEpisodeId", "noiseEventId", "noiseType", "heardAt", "selectionReason"],
                out reason) ||
            !HasOneOf(context, "monsterType", ["LISTENER"], out reason, "TELEMETRY_INVALID_ENUM_TOKEN") ||
            !HasText(data, "investigationEpisodeId") ||
            !HasText(data, "noiseEventId") ||
            !HasUtcTimestampText(data, "heardAt", out reason) ||
            !HasOneOf(data, "noiseType", NoiseReasonByType.Keys, out reason, "TELEMETRY_INVALID_ENUM_TOKEN") ||
            !HasOneOf(data, "selectionReason", ListenerInvestigationSelectionReasons, out reason, "TELEMETRY_INVALID_ENUM_TOKEN"))
        {
            return Reject(string.IsNullOrEmpty(reason) ? "TELEMETRY_RESEARCH_EVENT_INVALID" : reason, out reason);
        }

        reason = string.Empty;
        return true;
    }

    private static bool ValidateInvestigateResolved(
        TelemetryEventRequest item,
        JsonElement context,
        JsonElement data,
        out string reason)
    {
        if (!ValidateResearchMonster(
                item,
                context,
                data,
                ["phase", "monsterType", "monsterId", "researchCaptureEnabled"],
                ["investigationEpisodeId", "outcome"],
                out reason) ||
            !HasOneOf(context, "monsterType", ["LISTENER"], out reason, "TELEMETRY_INVALID_ENUM_TOKEN") ||
            !HasText(data, "investigationEpisodeId") ||
            !HasOneOf(data, "outcome", ListenerInvestigationOutcomes, out reason, "TELEMETRY_INVALID_ENUM_TOKEN"))
        {
            return Reject(string.IsNullOrEmpty(reason) ? "TELEMETRY_RESEARCH_EVENT_INVALID" : reason, out reason);
        }

        reason = string.Empty;
        return true;
    }

    private static bool ValidateMonsterAttackResolved(
        TelemetryEventRequest item,
        JsonElement context,
        JsonElement data,
        out string reason)
    {
        if (!ValidateResearchMonster(
                item,
                context,
                data,
                ["phase", "monsterType", "monsterId", "researchCaptureEnabled"],
                ["attackEpisodeId", "outcome"],
                out reason) ||
            !HasOneOf(context, "monsterType", ["STALKER", "LISTENER"], out reason, "TELEMETRY_INVALID_ENUM_TOKEN") ||
            !HasText(data, "attackEpisodeId") ||
            !HasOneOf(data, "outcome", AttackOutcomes, out reason, "TELEMETRY_INVALID_ENUM_TOKEN"))
        {
            return Reject(string.IsNullOrEmpty(reason) ? "TELEMETRY_RESEARCH_EVENT_INVALID" : reason, out reason);
        }

        reason = string.Empty;
        return true;
    }

    private static bool ValidateMonsterSearchEnded(
        TelemetryEventRequest item,
        JsonElement context,
        JsonElement data,
        out string reason)
    {
        if (!ValidateResearchMonster(
                item,
                context,
                data,
                ["phase", "monsterType", "monsterId", "researchCaptureEnabled"],
                ["searchEpisodeId", "outcome"],
                out reason) ||
            !HasOneOf(context, "monsterType", ["STALKER"], out reason, "TELEMETRY_INVALID_ENUM_TOKEN") ||
            !HasText(data, "searchEpisodeId") ||
            !HasOneOf(data, "outcome", SearchOutcomes, out reason, "TELEMETRY_INVALID_ENUM_TOKEN"))
        {
            return Reject(string.IsNullOrEmpty(reason) ? "TELEMETRY_RESEARCH_EVENT_INVALID" : reason, out reason);
        }

        reason = string.Empty;
        return true;
    }

    private static bool ValidateWardenTelegraph(
        TelemetryEventRequest item,
        JsonElement context,
        JsonElement data,
        out string reason)
    {
        if (!ValidateWardenBase(item, context, data,
                ["wardenActionId", "doorId", "routeFootprintIdentity", "selectionReason"], out reason) ||
            !HasOneOf(data, "selectionReason", WardenTelegraphSelectionReasons, out reason, "TELEMETRY_INVALID_ENUM_TOKEN"))
        {
            return false;
        }

        return Ok(out reason);
    }

    private static bool ValidateWardenApplied(
        TelemetryEventRequest item,
        JsonElement context,
        JsonElement data,
        out string reason)
    {
        if (!ValidateWardenBase(item, context, data,
                ["wardenActionId", "doorId", "routeFootprintIdentity", "routePressure", "preMeanShortestRouteCost", "postMeanShortestRouteCost", "safetyStatus"], out reason) ||
            !TryPositiveNumber(data, "routePressure") ||
            !TryNonNegativeNumber(data, "preMeanShortestRouteCost") ||
            !TryNonNegativeNumber(data, "postMeanShortestRouteCost") ||
            !HasOneOf(data, "safetyStatus", ["VALID"], out reason, "TELEMETRY_INVALID_ENUM_TOKEN"))
        {
            return Reject(string.IsNullOrEmpty(reason) ? "TELEMETRY_WARDEN_EVENT_INVALID" : reason, out reason);
        }

        return Ok(out reason);
    }

    private static bool ValidateWardenSafetyChecked(
        TelemetryEventRequest item,
        JsonElement context,
        JsonElement data,
        out string reason)
    {
        if (!ValidateWardenBase(item, context, data,
                ["wardenActionId", "safetyCheckId", "checkType", "objectiveReachable", "safetyStatus", "safetyReason"], out reason) ||
            !HasText(data, "safetyCheckId") ||
            !HasOneOf(data, "checkType", WardenSafetyCheckTypes, out reason, "TELEMETRY_INVALID_ENUM_TOKEN") ||
            !HasBoolean(data, "objectiveReachable", out _) ||
            !HasOneOf(data, "safetyStatus", WardenSafetyStatuses, out reason, "TELEMETRY_INVALID_ENUM_TOKEN"))
        {
            return Reject(string.IsNullOrEmpty(reason) ? "TELEMETRY_WARDEN_EVENT_INVALID" : reason, out reason);
        }

        var status = data.GetProperty("safetyStatus").GetString();
        var hasReason = data.TryGetProperty("safetyReason", out var safetyReason);
        if (status == "VALID")
        {
            return hasReason
                ? Reject("TELEMETRY_FORBIDDEN_FIELD", out reason)
                : Ok(out reason);
        }

        return hasReason &&
               safetyReason.ValueKind == JsonValueKind.String &&
               WardenSafetyReasons.Contains(safetyReason.GetString()!)
            ? Ok(out reason)
            : Reject("TELEMETRY_WARDEN_EVENT_INVALID", out reason);
    }

    private static bool ValidateWardenReleased(
        TelemetryEventRequest item,
        JsonElement context,
        JsonElement data,
        out string reason)
    {
        if (!ValidateWardenBase(item, context, data,
                ["wardenActionId", "doorId", "routeFootprintIdentity", "releaseReason", "failSafeReason"], out reason) ||
            !HasOneOf(data, "releaseReason", ["EXPIRED", "FAIL_SAFE"], out reason, "TELEMETRY_INVALID_ENUM_TOKEN"))
        {
            return false;
        }

        var releaseReason = data.GetProperty("releaseReason").GetString();
        var hasFailSafe = data.TryGetProperty("failSafeReason", out var failSafeReason);
        if (releaseReason == "EXPIRED")
        {
            return hasFailSafe
                ? Reject("TELEMETRY_FORBIDDEN_FIELD", out reason)
                : Ok(out reason);
        }

        return hasFailSafe &&
               failSafeReason.ValueKind == JsonValueKind.String &&
               WardenFailSafeReasons.Contains(failSafeReason.GetString()!)
            ? Ok(out reason)
            : Reject("TELEMETRY_WARDEN_EVENT_INVALID", out reason);
    }

    private static bool ValidateWardenBase(
        TelemetryEventRequest item,
        JsonElement context,
        JsonElement data,
        IEnumerable<string> dataFields,
        out string reason)
    {
        if (!ValidateResearchMonster(
                item,
                context,
                data,
                ["phase", "researchCaptureEnabled"],
                dataFields,
                out reason) ||
            !HasText(data, "wardenActionId") ||
            (data.TryGetProperty("doorId", out _) && !HasText(data, "doorId")) ||
            (data.TryGetProperty("routeFootprintIdentity", out _) && !HasText(data, "routeFootprintIdentity")))
        {
            return Reject(string.IsNullOrEmpty(reason) ? "TELEMETRY_WARDEN_EVENT_INVALID" : reason, out reason);
        }

        return Ok(out reason);
    }

    private static bool ValidateResearchMonster(
        TelemetryEventRequest item,
        JsonElement context,
        JsonElement data,
        IEnumerable<string> contextFields,
        IEnumerable<string> dataFields,
        out string reason)
    {
        if (!RequireContext(context, contextFields, false, out reason) ||
            !RequireNoFields(data, dataFields, out reason) ||
            item.UserId is not null ||
            item.ReasonCode is not null ||
            !HasText(context, "phase") ||
            !NoPosition(context, out reason))
        {
            return Reject(string.IsNullOrEmpty(reason) ? "TELEMETRY_RESEARCH_EVENT_INVALID" : reason, out reason);
        }

        reason = string.Empty;
        return true;
    }

    private static bool TryValueObjects(
        TelemetryEventRequest item,
        out JsonElement context,
        out JsonElement data,
        out string reason)
    {
        context = default;
        data = default;
        reason = string.Empty;
        if (item.ValueJson.ValueKind != JsonValueKind.Object ||
            !RequireNoFields(item.ValueJson, ["context", "data"], out reason) ||
            !TryObject(item.ValueJson, "context", out context) ||
            !TryObject(item.ValueJson, "data", out data))
        {
            reason = string.IsNullOrEmpty(reason) ? "TELEMETRY_VALUE_JSON_INVALID" : reason;
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool TryCommonContext(JsonElement context, out long eventSequence, out string reason)
    {
        eventSequence = 0;
        if (!TryPositiveInt64(context, "eventSequence", out eventSequence) ||
            !HasNullableNonNegativeInt64(context, "authorityTick") ||
            !HasText(context, "scenarioConfigVersion") ||
            !HasText(context, "policyVersion") ||
            !HasOneOf(context, "configSource", ["FIXED", "ADAPTIVE"], out reason, "TELEMETRY_COMMON_CONTEXT_INVALID"))
        {
            reason = "TELEMETRY_COMMON_CONTEXT_INVALID";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool ValidateReasonShape(TelemetryEventRequest item, out string reason)
    {
        if (item.ReasonCode is not null &&
            (!IsUpperSnakeCase(item.ReasonCode) || item.ReasonCode.Length > 100))
        {
            return Reject("TELEMETRY_REASON_CODE_INVALID", out reason);
        }

        reason = string.Empty;
        return true;
    }

    private static bool RequireContext(
        JsonElement context,
        IEnumerable<string> eventFields,
        bool allowPosition,
        out string reason)
    {
        var allowed = CommonContextFields.Concat(eventFields);
        if (allowPosition)
        {
            allowed = allowed.Concat(["position"]);
        }

        return RequireNoFields(context, allowed, out reason);
    }

    private static bool RequireNoFields(JsonElement element, IEnumerable<string> allowed, out string reason)
    {
        var allowedSet = allowed.ToHashSet(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!allowedSet.Contains(property.Name))
            {
                reason = "TELEMETRY_UNKNOWN_FIELD";
                return false;
            }
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

    private static bool HasNonEmptyGuidText(JsonElement parent, string name) =>
        HasText(parent, name) &&
        Guid.TryParse(parent.GetProperty(name).GetString(), out var parsed) &&
        parsed != Guid.Empty;

    private static bool HasUtcTimestampText(JsonElement parent, string name, out string reason)
    {
        reason = string.Empty;
        if (!HasText(parent, name))
        {
            reason = "TELEMETRY_TIMESTAMP_INVALID";
            return false;
        }

        var value = parent.GetProperty(name).GetString();
        if (value is null ||
            !value.EndsWith("Z", StringComparison.Ordinal) ||
            !DateTimeOffset.TryParseExact(
                value,
                UtcTimestampFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out _))
        {
            reason = "TELEMETRY_TIMESTAMP_NOT_UTC";
            return false;
        }

        return true;
    }

    private static bool HasBoolean(JsonElement parent, string name, out bool result)
    {
        result = false;
        if (!parent.TryGetProperty(name, out var value))
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.True)
        {
            result = true;
            return true;
        }

        return value.ValueKind == JsonValueKind.False;
    }

    private static bool HasBooleanValue(JsonElement parent, string name, bool expected) =>
        HasBoolean(parent, name, out var actual) && actual == expected;

    private static bool HasOneOf(
        JsonElement parent,
        string name,
        IEnumerable<string> allowed,
        out string reason,
        string invalidReason)
    {
        if (parent.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.String &&
            allowed.Contains(value.GetString(), StringComparer.Ordinal))
        {
            reason = string.Empty;
            return true;
        }

        reason = invalidReason;
        return false;
    }

    private static bool TryPositiveInt64(JsonElement parent, string name, out long result)
    {
        result = 0;
        return parent.TryGetProperty(name, out var value) &&
               value.ValueKind == JsonValueKind.Number &&
               value.TryGetInt64(out result) &&
               result > 0;
    }

    private static bool TryInt32(JsonElement parent, string name, out int result)
    {
        result = 0;
        return parent.TryGetProperty(name, out var value) &&
               value.ValueKind == JsonValueKind.Number &&
               value.TryGetInt32(out result);
    }

    private static bool TryNonNegativeNumber(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out var number) &&
        number >= 0;

    private static bool TryPositiveNumber(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out var number) &&
        number > 0;

    private static bool HasNullableNonNegativeInt64(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value))
        {
            return false;
        }

        return value.ValueKind == JsonValueKind.Null ||
               (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var tick) && tick >= 0);
    }

    private static bool HasOptionalPosition(JsonElement context, out string reason)
    {
        if (!context.TryGetProperty("position", out var position))
        {
            reason = string.Empty;
            return true;
        }

        return IsPosition(position, out reason);
    }

    private static bool HasRequiredPosition(JsonElement context, out string reason)
    {
        if (!context.TryGetProperty("position", out var position))
        {
            reason = "TELEMETRY_POSITION_INVALID";
            return false;
        }

        return IsPosition(position, out reason);
    }

    private static bool NoPosition(JsonElement context, out string reason)
    {
        if (context.TryGetProperty("position", out _))
        {
            reason = "TELEMETRY_FORBIDDEN_FIELD";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool IsPosition(JsonElement position, out string reason)
    {
        reason = string.Empty;
        if (position.ValueKind != JsonValueKind.Object ||
            !RequireNoFields(position, PositionFields, out reason) ||
            !HasNumber(position, "x") ||
            !HasNumber(position, "y") ||
            !HasNumber(position, "z"))
        {
            reason = string.IsNullOrEmpty(reason) ? "TELEMETRY_POSITION_INVALID" : reason;
            return false;
        }

        reason = string.Empty;
        return true;
    }

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

    private static bool Ok(out string reason)
    {
        reason = string.Empty;
        return true;
    }

    private static bool Reject(string code, out string rejectReason)
    {
        rejectReason = code;
        return false;
    }
}

internal static class TelemetryV11SemanticRegistries
{
    // Owned by current gameplay/MatchTelemetryAdapter emission and canonical v1.1 match-end contract.
    public static readonly HashSet<string> MatchOutcomes = ["SUCCESS", "FAILURE", "ABORTED"];
}
