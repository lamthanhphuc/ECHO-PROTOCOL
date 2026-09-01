using System;
using System.Collections.Generic;

namespace EchoProtocol.Telemetry
{
    public static class TelemetrySchemaVersions
    {
        public const string LegacyV1 = "1.0";
        public const string CurrentV11 = "1.1";
    }

    public static class TelemetryEventTypes
    {
        public const string MatchStarted = "MATCH_STARTED";
        public const string MatchEnded = "MATCH_ENDED";
        public const string PhaseStarted = "PHASE_STARTED";
        public const string PhaseCompleted = "PHASE_COMPLETED";
        public const string CorePickedUp = "CORE_PICKED_UP";
        public const string CoreDropped = "CORE_DROPPED";
        public const string CorePlaced = "CORE_PLACED";
        public const string PuzzleCompleted = "PUZZLE_COMPLETED";
        public const string SecurityHoldInterrupted = "SECURITY_HOLD_INTERRUPTED";
        public const string PlayerDowned = "PLAYER_DOWNED";
        public const string PlayerRevived = "PLAYER_REVIVED";
        public const string PlayerEliminated = "PLAYER_ELIMINATED";
        public const string PlayerEscaped = "PLAYER_ESCAPED";
        public const string TeamToolUsed = "TEAM_TOOL_USED";
        public const string HelpPingUsed = "HELP_PING_USED";
        public const string NoiseEmitted = "NOISE_EMITTED";

        public const string MonsterInvestigateStarted = "MONSTER_INVESTIGATE_STARTED";
        public const string MonsterInvestigateResolved = "MONSTER_INVESTIGATE_RESOLVED";
        public const string MonsterAttackResolved = "MONSTER_ATTACK_RESOLVED";
        public const string MonsterSearchEnded = "MONSTER_SEARCH_ENDED";
        public const string WardenTelegraphStarted = "WARDEN_TELEGRAPH_STARTED";
        public const string WardenRouteActionApplied = "WARDEN_ROUTE_ACTION_APPLIED";
        public const string WardenRouteSafetyChecked = "WARDEN_ROUTE_SAFETY_CHECKED";
        public const string WardenRouteActionReleased = "WARDEN_ROUTE_ACTION_RELEASED";
    }

    public enum TelemetryEventStatus
    {
        ActiveProduction,
        ResearchCapture
    }

    public enum TelemetryConfigSource
    {
        Fixed,
        Adaptive
    }

    public enum TelemetryAckStatus
    {
        Accepted,
        DuplicateAlreadyAccepted,
        PermanentlyRejected,
        TransientFailure
    }

    public enum TelemetryBufferFailureReason
    {
        None,
        BufferCapacityExceeded,
        RetryExhausted,
        SerializationFailed
    }

    public enum TelemetryStreamCompleteness
    {
        Complete,
        Incomplete,
        Invalid,
        Unknown
    }

    public static class TelemetryEventCatalog
    {
        private static readonly HashSet<string> ProductionEvents = new HashSet<string>(StringComparer.Ordinal)
        {
            TelemetryEventTypes.MatchStarted,
            TelemetryEventTypes.MatchEnded,
            TelemetryEventTypes.PhaseStarted,
            TelemetryEventTypes.PhaseCompleted,
            TelemetryEventTypes.CorePickedUp,
            TelemetryEventTypes.CoreDropped,
            TelemetryEventTypes.CorePlaced,
            TelemetryEventTypes.PuzzleCompleted,
            TelemetryEventTypes.SecurityHoldInterrupted,
            TelemetryEventTypes.PlayerDowned,
            TelemetryEventTypes.PlayerRevived,
            TelemetryEventTypes.PlayerEliminated,
            TelemetryEventTypes.PlayerEscaped,
            TelemetryEventTypes.TeamToolUsed,
            TelemetryEventTypes.HelpPingUsed,
            TelemetryEventTypes.NoiseEmitted
        };

        private static readonly HashSet<string> ResearchEvents = new HashSet<string>(StringComparer.Ordinal)
        {
            TelemetryEventTypes.MonsterInvestigateStarted,
            TelemetryEventTypes.MonsterInvestigateResolved,
            TelemetryEventTypes.MonsterAttackResolved,
            TelemetryEventTypes.MonsterSearchEnded,
            TelemetryEventTypes.WardenTelegraphStarted,
            TelemetryEventTypes.WardenRouteActionApplied,
            TelemetryEventTypes.WardenRouteSafetyChecked,
            TelemetryEventTypes.WardenRouteActionReleased
        };

        public static bool TryGetStatus(string eventType, out TelemetryEventStatus status)
        {
            if (ProductionEvents.Contains(eventType))
            {
                status = TelemetryEventStatus.ActiveProduction;
                return true;
            }

            if (ResearchEvents.Contains(eventType))
            {
                status = TelemetryEventStatus.ResearchCapture;
                return true;
            }

            status = default;
            return false;
        }
    }

    public sealed class TelemetryEvent
    {
        internal TelemetryEvent(
            Guid id,
            Guid matchId,
            Guid? userId,
            string eventType,
            DateTime timestampUtc,
            string contextJson,
            string dataJson,
            string reasonCode,
            string schemaVersion)
        {
            Id = id;
            MatchId = matchId;
            UserId = userId;
            EventType = eventType;
            TimestampUtc = timestampUtc;
            ContextJson = contextJson;
            DataJson = dataJson;
            ReasonCode = reasonCode;
            SchemaVersion = schemaVersion;
        }

        public Guid Id { get; }
        public Guid MatchId { get; }
        public Guid? UserId { get; }
        public string EventType { get; }
        public DateTime TimestampUtc { get; }
        public string ContextJson { get; }
        public string DataJson { get; }
        public string ReasonCode { get; }
        public string SchemaVersion { get; }
    }

    public sealed class TelemetryProvenanceSnapshot
    {
        public TelemetryProvenanceSnapshot(
            string scenarioConfigVersion,
            string policyVersion,
            TelemetryConfigSource configSource,
            bool researchCaptureEnabled)
        {
            if (string.IsNullOrWhiteSpace(scenarioConfigVersion))
            {
                throw new ArgumentException("Scenario config version is required.", nameof(scenarioConfigVersion));
            }

            if (string.IsNullOrWhiteSpace(policyVersion))
            {
                throw new ArgumentException("Policy version is required.", nameof(policyVersion));
            }

            ScenarioConfigVersion = scenarioConfigVersion;
            PolicyVersion = policyVersion;
            ConfigSource = configSource;
            ResearchCaptureEnabled = researchCaptureEnabled;
        }

        public string ScenarioConfigVersion { get; }
        public string PolicyVersion { get; }
        public TelemetryConfigSource ConfigSource { get; }
        public bool ResearchCaptureEnabled { get; }
    }

    public interface ITelemetryAuthorityContext
    {
        bool HasStateAuthority { get; }
        bool TryGetMatchId(out Guid matchId);
        long? AuthorityTick { get; }
    }

    public interface ITelemetryProvenanceProvider
    {
        TelemetryProvenanceSnapshot Capture();
    }

    public interface ITelemetryLocalLog
    {
        void Append(string category, Guid? eventId, string detailsJson);
    }

    public sealed class NullTelemetryLocalLog : ITelemetryLocalLog
    {
        public void Append(string category, Guid? eventId, string detailsJson)
        {
        }
    }
}
