using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoProtocol.AI.Listener.Noise
{
    public sealed class RuntimeNoiseSystem
    {
        public const int DefaultActiveCapacity = 64;
        public const int DefaultDedupCapacity = 256;

        private readonly RuntimeNoiseCatalog _catalog;
        private readonly int _activeCapacity;
        private readonly int _dedupCapacity;
        private readonly List<RuntimeNoiseEvent> _activeEvents = new List<RuntimeNoiseEvent>();
        private readonly Dictionary<string, long> _watermarkByStream =
            new Dictionary<string, long>(StringComparer.Ordinal);
        private ulong _nextOrdinal;

        public RuntimeNoiseSystem(
            RuntimeNoiseCatalog catalog = null,
            int activeCapacity = DefaultActiveCapacity,
            int dedupCapacity = DefaultDedupCapacity)
        {
            if (activeCapacity < 1) throw new ArgumentOutOfRangeException(nameof(activeCapacity));
            if (dedupCapacity < 1) throw new ArgumentOutOfRangeException(nameof(dedupCapacity));

            _catalog = catalog ?? RuntimeNoiseCatalog.CreateDefault();
            _activeCapacity = activeCapacity;
            _dedupCapacity = dedupCapacity;
        }

        public event Action<RuntimeNoiseEvent> RuntimeNoiseAccepted;
        public event Action<RuntimeNoiseSystemDiagnostic> DiagnosticEmitted;

        public int ActiveCount => _activeEvents.Count;
        public int DedupCount => _watermarkByStream.Count;
        public int ActiveCapacity => _activeCapacity;
        public int DedupCapacity => _dedupCapacity;
        public NoiseValidationRejectReason LastRejectReason { get; private set; }
        public NoiseSystemDiagnosticReason LastDiagnosticReason { get; private set; }

        public RuntimeNoiseAcceptStatus TryAccept(
            Guid matchId,
            RuntimeNoiseEmissionRequest request,
            out RuntimeNoiseEvent noiseEvent)
        {
            noiseEvent = default;
            LastRejectReason = NoiseValidationRejectReason.None;
            LastDiagnosticReason = NoiseSystemDiagnosticReason.None;
            if (matchId == Guid.Empty
                || !request.SourceOccurrenceKey.IsValid)
            {
                LastRejectReason = NoiseValidationRejectReason.SourceActionRejected;
                return RuntimeNoiseAcceptStatus.Rejected;
            }

            if (!_catalog.TryGetDefinition(request.NoiseType, out var definition))
            {
                LastRejectReason = NoiseValidationRejectReason.UnknownNoiseType;
                return RuntimeNoiseAcceptStatus.Rejected;
            }

            if (!RuntimeNoiseDefinition.IsFinite(definition.BaseLoudness) || definition.BaseLoudness < 0d)
            {
                LastRejectReason = NoiseValidationRejectReason.InvalidLoudness;
                return RuntimeNoiseAcceptStatus.Rejected;
            }

            if (!RuntimeNoiseDefinition.IsFinite(definition.HearingRadius) || definition.HearingRadius <= 0d)
            {
                LastRejectReason = NoiseValidationRejectReason.InvalidHearingRadius;
                return RuntimeNoiseAcceptStatus.Rejected;
            }

            if (definition.Lifetime <= TimeSpan.Zero)
            {
                LastRejectReason = NoiseValidationRejectReason.InvalidExpiry;
                return RuntimeNoiseAcceptStatus.Rejected;
            }

            if (request.EmittedAtUtc.Kind != DateTimeKind.Utc
                || !IsFinite(request.WorldPosition.x)
                || !IsFinite(request.WorldPosition.y)
                || !IsFinite(request.WorldPosition.z))
            {
                LastRejectReason = NoiseValidationRejectReason.InvalidPosition;
                return RuntimeNoiseAcceptStatus.Rejected;
            }

            Expire(request.EmittedAtUtc);

            if (_watermarkByStream.TryGetValue(
                    request.SourceOccurrenceKey.StreamKey,
                    out var retainedWatermark)
                && request.SourceOccurrenceKey.Sequence <= retainedWatermark)
            {
                LastRejectReason = NoiseValidationRejectReason.DuplicateEmission;
                return RuntimeNoiseAcceptStatus.Duplicate;
            }

            if (!_watermarkByStream.ContainsKey(request.SourceOccurrenceKey.StreamKey)
                && _watermarkByStream.Count >= _dedupCapacity)
            {
                EmitDiagnostic(
                    NoiseSystemDiagnosticReason.DedupRetentionInvariantViolation,
                    request.SourceOccurrenceKey.StreamKey);
                LastRejectReason = NoiseValidationRejectReason.SourceActionRejected;
                return RuntimeNoiseAcceptStatus.Rejected;
            }

            if (_activeEvents.Count >= _activeCapacity)
            {
                EvictOldestActive();
            }

            _nextOrdinal++;
            if (_nextOrdinal == 0) _nextOrdinal = 1;
            noiseEvent = new RuntimeNoiseEvent(
                BuildNoiseEventId(matchId, request.SourceOccurrenceKey.Value),
                new RuntimeNoiseEventOrderKey(request.AuthoritativeTick, _nextOrdinal),
                request.NoiseType,
                request.WorldPosition,
                request.EmittedAtUtc,
                definition.BaseLoudness,
                definition.HearingRadius,
                request.EmittedAtUtc + definition.Lifetime,
                request.SourceOccurrenceKey,
                request.SourcePlayerId,
                request.SourceEntityId);

            _activeEvents.Add(noiseEvent);
            _watermarkByStream[request.SourceOccurrenceKey.StreamKey] = request.SourceOccurrenceKey.Sequence;
            RuntimeNoiseAccepted?.Invoke(noiseEvent);
            return RuntimeNoiseAcceptStatus.Accepted;
        }

        public void Expire(DateTime nowUtc)
        {
            if (nowUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("Runtime noise expiry time must be UTC.", nameof(nowUtc));
            }

            for (var index = _activeEvents.Count - 1; index >= 0; index--)
            {
                if (_activeEvents[index].IsExpiredAt(nowUtc))
                {
                    _activeEvents.RemoveAt(index);
                }
            }
        }

        public IReadOnlyList<RuntimeNoiseEvent> GetActiveEvents(DateTime nowUtc)
        {
            Expire(nowUtc);
            return _activeEvents.ToArray();
        }

        public void ResetForMatch()
        {
            _activeEvents.Clear();
            _watermarkByStream.Clear();
            _nextOrdinal = 0;
            LastRejectReason = NoiseValidationRejectReason.None;
            LastDiagnosticReason = NoiseSystemDiagnosticReason.None;
        }

        private void EvictOldestActive()
        {
            var oldestIndex = 0;
            for (var index = 1; index < _activeEvents.Count; index++)
            {
                if (_activeEvents[index].EventOrderKey.CompareTo(_activeEvents[oldestIndex].EventOrderKey) < 0)
                {
                    oldestIndex = index;
                }
            }

            _activeEvents.RemoveAt(oldestIndex);
            EmitDiagnostic(NoiseSystemDiagnosticReason.CapacityEvicted, "active");
        }

        private void EmitDiagnostic(NoiseSystemDiagnosticReason reason, string details)
        {
            LastDiagnosticReason = reason;
            DiagnosticEmitted?.Invoke(new RuntimeNoiseSystemDiagnostic(reason, details));
        }

        private static string BuildNoiseEventId(Guid matchId, string sourceKey)
        {
            return $"{matchId:N}:noise:{sourceKey}";
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
