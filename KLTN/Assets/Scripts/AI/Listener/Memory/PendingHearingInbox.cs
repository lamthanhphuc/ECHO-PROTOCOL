using System;
using System.Collections.Generic;
using EchoProtocol.AI.Listener.Perception;

namespace EchoProtocol.AI.Listener.Memory
{
    public enum PendingHearingDiagnosticReason
    {
        ExpiredBeforeCommit,
        CapacityEvicted,
        ConsumedByStatePolicy
    }

    public readonly struct PendingHearingDiagnostic
    {
        public PendingHearingDiagnostic(PendingHearingDiagnosticReason reason, string noiseEventId)
        {
            Reason = reason;
            NoiseEventId = noiseEventId ?? string.Empty;
        }

        public PendingHearingDiagnosticReason Reason { get; }
        public string NoiseEventId { get; }
    }

    public sealed class PendingHearingInbox
    {
        public const int DefaultCapacity = 8;

        private readonly int _capacity;
        private readonly List<HearingObservation> _observations = new List<HearingObservation>();
        private readonly HashSet<string> _noiseEventIds = new HashSet<string>(StringComparer.Ordinal);

        public PendingHearingInbox(int capacity = DefaultCapacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
        }

        public int Capacity => _capacity;
        public int Count => _observations.Count;
        public IReadOnlyList<HearingObservation> Observations => _observations.ToArray();
        public PendingHearingDiagnosticReason? LastDiagnosticReason { get; private set; }

        public event Action<PendingHearingDiagnostic> DiagnosticEmitted;

        public bool TryAdd(HearingObservation observation, DateTime nowUtc)
        {
            if (nowUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("Pending hearing time must be UTC.", nameof(nowUtc));
            }

            RemoveExpired(nowUtc);
            if (string.IsNullOrWhiteSpace(observation.NoiseEventId)
                || _noiseEventIds.Contains(observation.NoiseEventId))
            {
                return false;
            }

            if (observation.IsExpiredAt(nowUtc))
            {
                EmitDiagnostic(PendingHearingDiagnosticReason.ExpiredBeforeCommit, observation.NoiseEventId);
                return false;
            }

            _observations.Add(observation);
            _noiseEventIds.Add(observation.NoiseEventId);
            EnforceCapacity();
            return _noiseEventIds.Contains(observation.NoiseEventId);
        }

        public int RemoveExpired(DateTime nowUtc)
        {
            if (nowUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("Pending hearing time must be UTC.", nameof(nowUtc));
            }

            var removed = 0;
            for (var index = _observations.Count - 1; index >= 0; index--)
            {
                if (!_observations[index].IsExpiredAt(nowUtc))
                {
                    continue;
                }

                _noiseEventIds.Remove(_observations[index].NoiseEventId);
                EmitDiagnostic(
                    PendingHearingDiagnosticReason.ExpiredBeforeCommit,
                    _observations[index].NoiseEventId);
                _observations.RemoveAt(index);
                removed++;
            }

            return removed;
        }

        public bool TryTakeBest(DateTime nowUtc, out HearingObservation observation)
        {
            RemoveExpired(nowUtc);
            observation = default;
            if (_observations.Count == 0)
            {
                return false;
            }

            var bestIndex = 0;
            for (var index = 1; index < _observations.Count; index++)
            {
                if (HearingObservationComparer.Instance.Compare(
                        _observations[index],
                        _observations[bestIndex]) < 0)
                {
                    bestIndex = index;
                }
            }

            observation = _observations[bestIndex];
            _noiseEventIds.Remove(observation.NoiseEventId);
            _observations.RemoveAt(bestIndex);
            return true;
        }

        public void Clear()
        {
            _observations.Clear();
            _noiseEventIds.Clear();
            LastDiagnosticReason = null;
        }

        private void EnforceCapacity()
        {
            while (_observations.Count > _capacity)
            {
                var worstIndex = 0;
                for (var index = 1; index < _observations.Count; index++)
                {
                    if (HearingObservationComparer.Instance.Compare(
                            _observations[index],
                            _observations[worstIndex]) > 0)
                    {
                        worstIndex = index;
                    }
                }

                _noiseEventIds.Remove(_observations[worstIndex].NoiseEventId);
                EmitDiagnostic(
                    PendingHearingDiagnosticReason.CapacityEvicted,
                    _observations[worstIndex].NoiseEventId);
                _observations.RemoveAt(worstIndex);
            }
        }

        private void EmitDiagnostic(PendingHearingDiagnosticReason reason, string noiseEventId)
        {
            LastDiagnosticReason = reason;
            DiagnosticEmitted?.Invoke(new PendingHearingDiagnostic(reason, noiseEventId));
        }
    }
}
