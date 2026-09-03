using System;
using UnityEngine;

namespace EchoProtocol.AI.Listener.Noise
{
    public enum RuntimeNoiseType
    {
        SPRINT,
        INTERACTION,
        CORE_CARRY,
        CORE_DROP,
        NOISE_MAKER
    }

    public enum RuntimeNoiseEmissionMode
    {
        DiscreteAction,
        RecurringMovement
    }

    public enum RuntimeNoiseAcceptStatus
    {
        Accepted,
        Duplicate,
        Rejected
    }

    public enum NoiseValidationRejectReason
    {
        None,
        NotStateAuthority,
        UnknownNoiseType,
        InvalidDefinition,
        InvalidPosition,
        InvalidLoudness,
        InvalidHearingRadius,
        InvalidExpiry,
        DuplicateEmission,
        SourceActionRejected
    }

    public enum NoiseSystemDiagnosticReason
    {
        None,
        CapacityEvicted,
        DedupRetentionInvariantViolation,
        SubsystemUnavailable
    }

    public readonly struct RuntimeNoiseSystemDiagnostic
    {
        public RuntimeNoiseSystemDiagnostic(NoiseSystemDiagnosticReason reason, string details)
        {
            Reason = reason;
            Details = details ?? string.Empty;
        }

        public NoiseSystemDiagnosticReason Reason { get; }
        public string Details { get; }
    }

    public readonly struct RuntimeNoiseSourceOccurrenceKey
    {
        public RuntimeNoiseSourceOccurrenceKey(string streamKey, long sequence)
        {
            StreamKey = streamKey ?? string.Empty;
            Sequence = sequence;
        }

        public string StreamKey { get; }
        public long Sequence { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(StreamKey) && Sequence > 0;
        public string Value => $"{StreamKey}:{Sequence}";
        public override string ToString() => Value;

        public static RuntimeNoiseSourceOccurrenceKey ForMovement(
            string playerObjectId,
            RuntimeNoiseType noiseType,
            long authoritativeTick)
        {
            return new RuntimeNoiseSourceOccurrenceKey(
                $"movement:{playerObjectId}",
                authoritativeTick);
        }

        public static RuntimeNoiseSourceOccurrenceKey ForInteraction(
            string playerObjectId,
            uint commandSequence)
        {
            return new RuntimeNoiseSourceOccurrenceKey(
                $"interaction:{playerObjectId}",
                commandSequence);
        }

        public static RuntimeNoiseSourceOccurrenceKey ForTeamTool(
            string playerObjectId,
            string toolType,
            uint commandSequence)
        {
            return new RuntimeNoiseSourceOccurrenceKey(
                $"team-tool:{playerObjectId}:{toolType}",
                commandSequence);
        }

        public static RuntimeNoiseSourceOccurrenceKey ForCoreDrop(
            string coreObjectId,
            uint transitionOrdinal)
        {
            return new RuntimeNoiseSourceOccurrenceKey(
                $"core-drop:{coreObjectId}",
                transitionOrdinal);
        }
    }

    public readonly struct RuntimeNoiseEventOrderKey : IComparable<RuntimeNoiseEventOrderKey>
    {
        public RuntimeNoiseEventOrderKey(long authoritativeTick, ulong ordinal)
        {
            AuthoritativeTick = authoritativeTick;
            Ordinal = ordinal;
        }

        public long AuthoritativeTick { get; }
        public ulong Ordinal { get; }

        public int CompareTo(RuntimeNoiseEventOrderKey other)
        {
            var tickComparison = AuthoritativeTick.CompareTo(other.AuthoritativeTick);
            return tickComparison != 0 ? tickComparison : Ordinal.CompareTo(other.Ordinal);
        }
    }

    public sealed class RuntimeNoiseDefinition
    {
        public RuntimeNoiseDefinition(
            RuntimeNoiseType noiseType,
            double baseLoudness,
            double hearingRadius,
            TimeSpan lifetime,
            RuntimeNoiseEmissionMode emissionMode)
        {
            if (!IsFinite(baseLoudness) || baseLoudness < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(baseLoudness));
            }

            if (!IsFinite(hearingRadius) || hearingRadius <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(hearingRadius));
            }

            if (lifetime <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(lifetime));
            }

            NoiseType = noiseType;
            BaseLoudness = baseLoudness;
            HearingRadius = hearingRadius;
            Lifetime = lifetime;
            EmissionMode = emissionMode;
        }

        public RuntimeNoiseType NoiseType { get; }
        public double BaseLoudness { get; }
        public double HearingRadius { get; }
        public TimeSpan Lifetime { get; }
        public RuntimeNoiseEmissionMode EmissionMode { get; }

        internal static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    public readonly struct RuntimeNoiseEmissionRequest
    {
        public RuntimeNoiseEmissionRequest(
            RuntimeNoiseSourceOccurrenceKey sourceOccurrenceKey,
            RuntimeNoiseType noiseType,
            Vector3 worldPosition,
            DateTime emittedAtUtc,
            long authoritativeTick,
            string sourcePlayerId = null,
            string sourceEntityId = null)
        {
            SourceOccurrenceKey = sourceOccurrenceKey;
            NoiseType = noiseType;
            WorldPosition = worldPosition;
            EmittedAtUtc = emittedAtUtc;
            AuthoritativeTick = authoritativeTick;
            SourcePlayerId = sourcePlayerId;
            SourceEntityId = sourceEntityId;
        }

        public RuntimeNoiseSourceOccurrenceKey SourceOccurrenceKey { get; }
        public RuntimeNoiseType NoiseType { get; }
        public Vector3 WorldPosition { get; }
        public DateTime EmittedAtUtc { get; }
        public long AuthoritativeTick { get; }
        internal string SourcePlayerId { get; }
        internal string SourceEntityId { get; }
    }

    public readonly struct RuntimeNoiseEvent
    {
        internal RuntimeNoiseEvent(
            string noiseEventId,
            RuntimeNoiseEventOrderKey eventOrderKey,
            RuntimeNoiseType noiseType,
            Vector3 worldPosition,
            DateTime emittedAtUtc,
            double loudness,
            double hearingRadius,
            DateTime expiresAtUtc,
            RuntimeNoiseSourceOccurrenceKey sourceOccurrenceKey,
            string sourcePlayerId,
            string sourceEntityId)
        {
            NoiseEventId = noiseEventId ?? string.Empty;
            EventOrderKey = eventOrderKey;
            NoiseType = noiseType;
            WorldPosition = worldPosition;
            EmittedAtUtc = emittedAtUtc;
            Loudness = loudness;
            HearingRadius = hearingRadius;
            ExpiresAtUtc = expiresAtUtc;
            _sourceOccurrenceKey = sourceOccurrenceKey;
            _sourcePlayerId = sourcePlayerId;
            _sourceEntityId = sourceEntityId;
        }

        public string NoiseEventId { get; }
        public RuntimeNoiseEventOrderKey EventOrderKey { get; }
        public RuntimeNoiseType NoiseType { get; }
        public Vector3 WorldPosition { get; }
        public DateTime EmittedAtUtc { get; }
        public double Loudness { get; }
        public double HearingRadius { get; }
        public DateTime ExpiresAtUtc { get; }
        private readonly RuntimeNoiseSourceOccurrenceKey _sourceOccurrenceKey;
        private readonly string _sourcePlayerId;
        private readonly string _sourceEntityId;

        public bool IsExpiredAt(DateTime nowUtc)
        {
            if (nowUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("Runtime noise time must be UTC.", nameof(nowUtc));
            }

            return nowUtc >= ExpiresAtUtc;
        }
    }
}
