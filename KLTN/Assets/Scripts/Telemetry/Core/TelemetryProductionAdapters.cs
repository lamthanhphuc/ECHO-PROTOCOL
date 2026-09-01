using System;
using System.Collections.Generic;

namespace EchoProtocol.Telemetry
{
    public readonly struct TelemetryPositionSnapshot
    {
        public TelemetryPositionSnapshot(double x, double y, double z)
        {
            if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z))
            {
                throw new ArgumentOutOfRangeException(nameof(x), "Telemetry position must be finite.");
            }

            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        internal TelemetryJsonObject ToJson()
        {
            return new TelemetryJsonObject()
                .AddNumber("x", X)
                .AddNumber("y", Y)
                .AddNumber("z", Z);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    /// <summary>
    /// Typed boundary called only after authoritative Match/Phase state has committed.
    /// It does not poll gameplay state or infer transitions.
    /// </summary>
    public sealed class MatchTelemetryAdapter
    {
        private readonly TelemetryEmitter _emitter;

        public MatchTelemetryAdapter(TelemetryEmitter emitter)
        {
            _emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
        }

        public bool EmitMatchStarted(
            string occurrenceKey,
            DateTime occurredAtUtc,
            string mapId,
            int teamSize,
            string buildVersion,
            string mapContentVersion,
            string contentWhitelistVersion,
            bool researchCaptureEnabled,
            out TelemetryEvent telemetryEvent,
            out TelemetryBufferFailureReason failureReason,
            string experimentCondition = null,
            string experimentProtocolVersion = null)
        {
            RequireText(mapId, nameof(mapId));
            RequireText(buildVersion, nameof(buildVersion));
            RequireText(mapContentVersion, nameof(mapContentVersion));
            RequireText(contentWhitelistVersion, nameof(contentWhitelistVersion));
            if (teamSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(teamSize));
            }

            if ((experimentCondition == null) != (experimentProtocolVersion == null))
            {
                throw new ArgumentException("Experiment condition and protocol version must be supplied together.");
            }

            if (experimentCondition != null
                && experimentCondition != "FIXED"
                && experimentCondition != "ADAPTIVE")
            {
                throw new ArgumentException("Experiment condition must be FIXED or ADAPTIVE.", nameof(experimentCondition));
            }

            var context = new TelemetryJsonObject()
                .AddInteger("teamSize", teamSize)
                .AddString("buildVersion", buildVersion)
                .AddString("mapContentVersion", mapContentVersion)
                .AddString("contentWhitelistVersion", contentWhitelistVersion)
                .AddBoolean("researchCaptureEnabled", researchCaptureEnabled);
            if (experimentCondition != null)
            {
                context
                    .AddString("experimentCondition", experimentCondition)
                    .AddString("experimentProtocolVersion", experimentProtocolVersion);
            }

            return Emit(
                occurrenceKey,
                TelemetryEventTypes.MatchStarted,
                null,
                "MATCH_READY",
                occurredAtUtc,
                context,
                new TelemetryJsonObject().AddString("mapId", mapId),
                out telemetryEvent,
                out failureReason);
        }

        public bool EmitMatchEnded(
            string occurrenceKey,
            DateTime occurredAtUtc,
            string outcome,
            double durationSeconds,
            int survivorCount,
            string reasonCode,
            out TelemetryEvent telemetryEvent,
            out TelemetryBufferFailureReason failureReason)
        {
            RequireText(outcome, nameof(outcome));
            RequireOneOf(reasonCode, nameof(reasonCode), "TEAM_ESCAPED", "TEAM_ELIMINATED", "MATCH_ABORTED");
            if (durationSeconds < 0 || double.IsNaN(durationSeconds) || double.IsInfinity(durationSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(durationSeconds));
            }

            if (survivorCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(survivorCount));
            }

            return Emit(
                occurrenceKey,
                TelemetryEventTypes.MatchEnded,
                null,
                reasonCode,
                occurredAtUtc,
                PhaseContext("MATCH_END"),
                new TelemetryJsonObject()
                    .AddString("outcome", outcome)
                    .AddNumber("durationSeconds", durationSeconds)
                    .AddInteger("survivorCount", survivorCount),
                out telemetryEvent,
                out failureReason);
        }

        public bool EmitPhaseStarted(
            string occurrenceKey,
            DateTime occurredAtUtc,
            string phase,
            out TelemetryEvent telemetryEvent,
            out TelemetryBufferFailureReason failureReason,
            string reasonCode = null)
        {
            if (reasonCode != null && reasonCode != "PREVIOUS_PHASE_COMPLETED")
            {
                throw new ArgumentException("Unsupported PHASE_STARTED reason.", nameof(reasonCode));
            }

            return Emit(occurrenceKey, TelemetryEventTypes.PhaseStarted, null, reasonCode, occurredAtUtc,
                PhaseContext(phase), new TelemetryJsonObject(), out telemetryEvent, out failureReason);
        }

        public bool EmitPhaseCompleted(
            string occurrenceKey,
            DateTime occurredAtUtc,
            string phase,
            out TelemetryEvent telemetryEvent,
            out TelemetryBufferFailureReason failureReason,
            double? durationSeconds = null,
            string reasonCode = null)
        {
            if (reasonCode != null && reasonCode != "OBJECTIVE_COMPLETED")
            {
                throw new ArgumentException("Unsupported PHASE_COMPLETED reason.", nameof(reasonCode));
            }

            var data = new TelemetryJsonObject();
            if (durationSeconds.HasValue)
            {
                if (durationSeconds.Value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(durationSeconds));
                }

                data.AddNumber("durationSeconds", durationSeconds.Value);
            }

            return Emit(occurrenceKey, TelemetryEventTypes.PhaseCompleted, null, reasonCode, occurredAtUtc,
                PhaseContext(phase), data, out telemetryEvent, out failureReason);
        }

        private bool Emit(
            string occurrenceKey,
            string eventType,
            Guid? userId,
            string reasonCode,
            DateTime occurredAtUtc,
            TelemetryJsonObject context,
            TelemetryJsonObject data,
            out TelemetryEvent telemetryEvent,
            out TelemetryBufferFailureReason failureReason)
        {
            return _emitter.TryEmit(new TelemetryEmissionRequest
            {
                SourceOccurrenceKey = occurrenceKey,
                EventType = eventType,
                UserId = userId,
                ReasonCode = reasonCode,
                OccurredAtUtc = occurredAtUtc,
                Context = context,
                Data = data
            }, out telemetryEvent, out failureReason);
        }

        internal static TelemetryJsonObject PhaseContext(string phase)
        {
            RequireText(phase, nameof(phase));
            return new TelemetryJsonObject().AddString("phase", phase);
        }

        internal static void RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A non-empty value is required.", parameterName);
            }
        }

        internal static void RequireUser(Guid userId, string parameterName)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("A non-empty user ID is required.", parameterName);
            }
        }

        internal static void RequireOneOf(string value, string parameterName, params string[] allowed)
        {
            for (var index = 0; index < allowed.Length; index++)
            {
                if (value == allowed[index])
                {
                    return;
                }
            }

            throw new ArgumentException("Unsupported canonical value: " + value, parameterName);
        }
    }

    public sealed class ObjectiveTelemetryAdapter
    {
        private readonly TelemetryEmitter _emitter;

        public ObjectiveTelemetryAdapter(TelemetryEmitter emitter)
        {
            _emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
        }

        public bool EmitCoreTransition(
            string occurrenceKey,
            DateTime occurredAtUtc,
            string eventType,
            Guid actingUserId,
            string coreId,
            out TelemetryEvent telemetryEvent,
            out TelemetryBufferFailureReason failureReason,
            TelemetryPositionSnapshot? position = null)
        {
            MatchTelemetryAdapter.RequireUser(actingUserId, nameof(actingUserId));
            MatchTelemetryAdapter.RequireText(coreId, nameof(coreId));
            string reasonCode;
            switch (eventType)
            {
                case TelemetryEventTypes.CorePickedUp: reasonCode = "PLAYER_PICKUP"; break;
                case TelemetryEventTypes.CoreDropped: reasonCode = "PLAYER_DROP"; break;
                case TelemetryEventTypes.CorePlaced: reasonCode = "CORE_OBJECTIVE_PLACED"; break;
                default: throw new ArgumentException("Event is not a canonical Core transition.", nameof(eventType));
            }

            var context = MatchTelemetryAdapter.PhaseContext("CORE_COLLECTION");
            if (position.HasValue)
            {
                context.AddObject("position", position.Value.ToJson());
            }

            return Emit(occurrenceKey, eventType, actingUserId, reasonCode, occurredAtUtc, context,
                new TelemetryJsonObject().AddString("coreId", coreId), out telemetryEvent, out failureReason);
        }

        public bool EmitPuzzleCompleted(
            string occurrenceKey,
            DateTime occurredAtUtc,
            out TelemetryEvent telemetryEvent,
            out TelemetryBufferFailureReason failureReason)
        {
            return Emit(occurrenceKey, TelemetryEventTypes.PuzzleCompleted, null, null, occurredAtUtc,
                MatchTelemetryAdapter.PhaseContext("POWER_PUZZLE"), new TelemetryJsonObject(),
                out telemetryEvent, out failureReason);
        }

        public bool EmitSecurityHoldInterrupted(
            string occurrenceKey,
            DateTime occurredAtUtc,
            out TelemetryEvent telemetryEvent,
            out TelemetryBufferFailureReason failureReason)
        {
            return Emit(occurrenceKey, TelemetryEventTypes.SecurityHoldInterrupted, null, null, occurredAtUtc,
                MatchTelemetryAdapter.PhaseContext("SECURITY_HOLD"), new TelemetryJsonObject(),
                out telemetryEvent, out failureReason);
        }

        private bool Emit(string key, string type, Guid? userId, string reason, DateTime at,
            TelemetryJsonObject context, TelemetryJsonObject data, out TelemetryEvent evt,
            out TelemetryBufferFailureReason failure)
        {
            return _emitter.TryEmit(new TelemetryEmissionRequest
            {
                SourceOccurrenceKey = key,
                EventType = type,
                UserId = userId,
                ReasonCode = reason,
                OccurredAtUtc = at,
                Context = context,
                Data = data
            }, out evt, out failure);
        }
    }

    public sealed class PlayerTelemetryAdapter
    {
        private static readonly HashSet<string> ToolTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "FIELD_SCANNER", "NOISE_MAKER", "FIRST_AID_KIT", "DOOR_JAMMER"
        };

        private readonly TelemetryEmitter _emitter;

        public PlayerTelemetryAdapter(TelemetryEmitter emitter)
        {
            _emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
        }

        public bool EmitPlayerDowned(string key, DateTime at, Guid userId, string phase, string monsterType,
            string reasonCode, out TelemetryEvent evt, out TelemetryBufferFailureReason failure,
            int? downCount = null, TelemetryPositionSnapshot? position = null)
        {
            MatchTelemetryAdapter.RequireUser(userId, nameof(userId));
            MatchTelemetryAdapter.RequireOneOf(reasonCode, nameof(reasonCode), "STALKER_ATTACK", "LISTENER_ATTACK");
            MatchTelemetryAdapter.RequireOneOf(monsterType, nameof(monsterType), "STALKER", "LISTENER");
            if ((reasonCode == "STALKER_ATTACK") != (monsterType == "STALKER"))
            {
                throw new ArgumentException("Monster type must match the Down reason.", nameof(monsterType));
            }

            var context = MatchTelemetryAdapter.PhaseContext(phase).AddString("monsterType", monsterType);
            if (position.HasValue) context.AddObject("position", position.Value.ToJson());
            var data = new TelemetryJsonObject();
            if (downCount.HasValue)
            {
                if (downCount.Value < 1) throw new ArgumentOutOfRangeException(nameof(downCount));
                data.AddInteger("downCount", downCount.Value);
            }

            return Emit(key, TelemetryEventTypes.PlayerDowned, userId, reasonCode, at, context, data, out evt, out failure);
        }

        public bool EmitPlayerRevived(string key, DateTime at, Guid revivedUserId, Guid reviverUserId,
            string phase, out TelemetryEvent evt, out TelemetryBufferFailureReason failure,
            int? reviveCount = null, bool? usedFirstAidKit = null)
        {
            MatchTelemetryAdapter.RequireUser(revivedUserId, nameof(revivedUserId));
            MatchTelemetryAdapter.RequireUser(reviverUserId, nameof(reviverUserId));
            var data = new TelemetryJsonObject().AddString("reviverPlayerId", reviverUserId.ToString("D"));
            if (reviveCount.HasValue)
            {
                if (reviveCount.Value < 1) throw new ArgumentOutOfRangeException(nameof(reviveCount));
                data.AddInteger("reviveCount", reviveCount.Value);
            }
            if (usedFirstAidKit.HasValue) data.AddBoolean("usedFirstAidKit", usedFirstAidKit.Value);
            return Emit(key, TelemetryEventTypes.PlayerRevived, revivedUserId, "TEAMMATE_REVIVE", at,
                MatchTelemetryAdapter.PhaseContext(phase), data, out evt, out failure);
        }

        public bool EmitPlayerEliminated(string key, DateTime at, Guid userId, string phase,
            out TelemetryEvent evt, out TelemetryBufferFailureReason failure, int? reviveCount = null)
        {
            MatchTelemetryAdapter.RequireUser(userId, nameof(userId));
            var data = new TelemetryJsonObject();
            if (reviveCount.HasValue)
            {
                if (reviveCount.Value < 0) throw new ArgumentOutOfRangeException(nameof(reviveCount));
                data.AddInteger("reviveCount", reviveCount.Value);
            }
            return Emit(key, TelemetryEventTypes.PlayerEliminated, userId, "REVIVE_LIMIT_REACHED", at,
                MatchTelemetryAdapter.PhaseContext(phase), data, out evt, out failure);
        }

        public bool EmitPlayerEscaped(string key, DateTime at, Guid userId,
            out TelemetryEvent evt, out TelemetryBufferFailureReason failure, bool? rescuedTeammate = null)
        {
            MatchTelemetryAdapter.RequireUser(userId, nameof(userId));
            var data = new TelemetryJsonObject();
            if (rescuedTeammate.HasValue) data.AddBoolean("rescuedTeammate", rescuedTeammate.Value);
            return Emit(key, TelemetryEventTypes.PlayerEscaped, userId, "EXIT_REACHED", at,
                MatchTelemetryAdapter.PhaseContext("FINAL_HUNT"), data, out evt, out failure);
        }

        public bool EmitTeamToolUsed(string key, DateTime at, Guid userId, string phase, string toolType,
            out TelemetryEvent evt, out TelemetryBufferFailureReason failure, string targetId = null)
        {
            MatchTelemetryAdapter.RequireUser(userId, nameof(userId));
            if (!ToolTypes.Contains(toolType)) throw new ArgumentException("Unknown canonical Team Tool.", nameof(toolType));
            var data = new TelemetryJsonObject().AddString("toolType", toolType);
            if (targetId != null) data.AddString("targetId", targetId);
            return Emit(key, TelemetryEventTypes.TeamToolUsed, userId, "PLAYER_ACTIVATED_TOOL", at,
                MatchTelemetryAdapter.PhaseContext(phase), data, out evt, out failure);
        }

        public bool EmitHelpPingUsed(string key, DateTime at, Guid userId, string phase,
            out TelemetryEvent evt, out TelemetryBufferFailureReason failure,
            TelemetryPositionSnapshot? legalPosition = null)
        {
            MatchTelemetryAdapter.RequireUser(userId, nameof(userId));
            var context = MatchTelemetryAdapter.PhaseContext(phase);
            if (legalPosition.HasValue) context.AddObject("position", legalPosition.Value.ToJson());
            return Emit(key, TelemetryEventTypes.HelpPingUsed, userId, "PLAYER_REQUESTED_HELP", at,
                context, new TelemetryJsonObject(), out evt, out failure);
        }

        private bool Emit(string key, string type, Guid userId, string reason, DateTime at,
            TelemetryJsonObject context, TelemetryJsonObject data, out TelemetryEvent evt,
            out TelemetryBufferFailureReason failure)
        {
            return _emitter.TryEmit(new TelemetryEmissionRequest
            {
                SourceOccurrenceKey = key,
                EventType = type,
                UserId = userId,
                ReasonCode = reason,
                OccurredAtUtc = at,
                Context = context,
                Data = data
            }, out evt, out failure);
        }
    }

    public sealed class NoiseTelemetryAdapter
    {
        private static readonly Dictionary<string, string> ReasonByType =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "SPRINT", "PLAYER_SPRINT" },
                { "INTERACTION", "OBJECT_INTERACTION" },
                { "CORE_CARRY", "CORE_CARRY_MOVEMENT" },
                { "CORE_DROP", "CORE_DROP" },
                { "NOISE_MAKER", "NOISE_MAKER_USED" }
            };

        private readonly TelemetryEmitter _emitter;

        public NoiseTelemetryAdapter(TelemetryEmitter emitter)
        {
            _emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
        }

        public bool EmitAcceptedRuntimeNoise(string noiseEventId, DateTime emittedAtUtc, Guid actingUserId,
            string phase, string noiseType, double loudness, TelemetryPositionSnapshot position,
            out TelemetryEvent telemetryEvent, out TelemetryBufferFailureReason failureReason,
            double? hearingRadius = null)
        {
            MatchTelemetryAdapter.RequireText(noiseEventId, nameof(noiseEventId));
            MatchTelemetryAdapter.RequireUser(actingUserId, nameof(actingUserId));
            if (!ReasonByType.TryGetValue(noiseType, out var reasonCode))
            {
                throw new ArgumentException("Unknown canonical Runtime Noise type.", nameof(noiseType));
            }
            if (loudness < 0 || double.IsNaN(loudness) || double.IsInfinity(loudness))
            {
                throw new ArgumentOutOfRangeException(nameof(loudness));
            }
            if (hearingRadius.HasValue && hearingRadius.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(hearingRadius));
            }

            var data = new TelemetryJsonObject()
                .AddString("noiseEventId", noiseEventId)
                .AddString("noiseType", noiseType)
                .AddNumber("loudness", loudness);
            if (hearingRadius.HasValue) data.AddNumber("hearingRadius", hearingRadius.Value);

            return _emitter.TryEmit(new TelemetryEmissionRequest
            {
                SourceOccurrenceKey = "noise|" + noiseEventId,
                EventType = TelemetryEventTypes.NoiseEmitted,
                UserId = actingUserId,
                ReasonCode = reasonCode,
                OccurredAtUtc = emittedAtUtc,
                Context = MatchTelemetryAdapter.PhaseContext(phase).AddObject("position", position.ToJson()),
                Data = data
            }, out telemetryEvent, out failureReason);
        }
    }

    public sealed class MonsterTelemetryAdapter
    {
        private readonly TelemetryEmitter _emitter;

        public MonsterTelemetryAdapter(TelemetryEmitter emitter)
        {
            _emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
        }

        public bool EmitAttackResolved(
            string occurrenceKey,
            DateTime occurredAtUtc,
            string phase,
            string monsterType,
            string monsterId,
            string attackEpisodeId,
            string outcome,
            out TelemetryEvent telemetryEvent,
            out TelemetryBufferFailureReason failureReason)
        {
            MatchTelemetryAdapter.RequireOneOf(monsterType, nameof(monsterType), "STALKER", "LISTENER");
            MatchTelemetryAdapter.RequireOneOf(outcome, nameof(outcome), "HIT", "MISS");
            MatchTelemetryAdapter.RequireText(monsterId, nameof(monsterId));
            MatchTelemetryAdapter.RequireText(attackEpisodeId, nameof(attackEpisodeId));
            return Emit(
                occurrenceKey,
                TelemetryEventTypes.MonsterAttackResolved,
                occurredAtUtc,
                MatchTelemetryAdapter.PhaseContext(phase)
                    .AddString("monsterType", monsterType)
                    .AddString("monsterId", monsterId),
                new TelemetryJsonObject()
                    .AddString("attackEpisodeId", attackEpisodeId)
                    .AddString("outcome", outcome),
                out telemetryEvent,
                out failureReason);
        }

        public bool EmitStalkerSearchEnded(
            string occurrenceKey,
            DateTime occurredAtUtc,
            string phase,
            string monsterId,
            string searchEpisodeId,
            string outcome,
            out TelemetryEvent telemetryEvent,
            out TelemetryBufferFailureReason failureReason)
        {
            MatchTelemetryAdapter.RequireOneOf(
                outcome,
                nameof(outcome),
                "SAME_TARGET_REACQUIRED",
                "NEW_ELIGIBLE_TARGET_OBSERVED",
                "TIMEOUT",
                "CURRENT_TARGET_INVALID_NO_REPLACEMENT");
            MatchTelemetryAdapter.RequireText(monsterId, nameof(monsterId));
            MatchTelemetryAdapter.RequireText(searchEpisodeId, nameof(searchEpisodeId));
            return Emit(
                occurrenceKey,
                TelemetryEventTypes.MonsterSearchEnded,
                occurredAtUtc,
                MatchTelemetryAdapter.PhaseContext(phase)
                    .AddString("monsterType", "STALKER")
                    .AddString("monsterId", monsterId),
                new TelemetryJsonObject()
                    .AddString("searchEpisodeId", searchEpisodeId)
                    .AddString("outcome", outcome),
                out telemetryEvent,
                out failureReason);
        }

        private bool Emit(
            string key,
            string type,
            DateTime at,
            TelemetryJsonObject context,
            TelemetryJsonObject data,
            out TelemetryEvent telemetryEvent,
            out TelemetryBufferFailureReason failureReason)
        {
            return _emitter.TryEmit(new TelemetryEmissionRequest
            {
                SourceOccurrenceKey = key,
                EventType = type,
                UserId = null,
                ReasonCode = null,
                OccurredAtUtc = at,
                Context = context,
                Data = data
            }, out telemetryEvent, out failureReason);
        }
    }
}
