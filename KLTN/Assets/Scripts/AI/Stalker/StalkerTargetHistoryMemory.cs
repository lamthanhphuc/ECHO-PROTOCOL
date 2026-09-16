using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common;

namespace EchoProtocol.AI.Stalker
{
    public sealed class StalkerTargetHistoryMemory
    {
        public const int DefaultCapacity = 4;

        private readonly Entry[] _entries;
        private int _count;

        public StalkerTargetHistoryMemory()
            : this(DefaultCapacity)
        {
        }

        public StalkerTargetHistoryMemory(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(capacity),
                    capacity,
                    "Target-history capacity must be positive.");
            }

            _entries = new Entry[capacity];
        }

        public int Count => _count;

        public void Reset()
        {
            Array.Clear(
                _entries,
                0,
                _entries.Length);

            _count = 0;
        }

        public void RecordVisibleFrame(
            IReadOnlyList<StalkerTargetCandidate> candidates)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];

                // Target history may only learn from a currently legal,
                // eligible visual observation. An ineligible candidate must
                // not contribute identity/history knowledge.
                if (!candidate.Eligibility.Eligible)
                {
                    continue;
                }

                var observation = candidate.Observation;
                if (!observation.PlayerId.IsValid
                    || !observation.ObservedAt.IsValid)
                {
                    continue;
                }

                RecordVisibleObservation(
                    observation.PlayerId,
                    observation.ObservedAt);
            }
        }

        public void RecordVisibleObservation(
            PlayerId playerId,
            AiSimulationTime observedAt)
        {
            ValidateEvent(playerId, observedAt);
            var index = GetOrCreateEntry(playerId, observedAt);
            var entry = _entries[index];

            if (!entry.HasLastSeen
                || observedAt.CompareTo(entry.LastSeenAt) >= 0)
            {
                entry.LastSeenAt = observedAt;
                entry.HasLastSeen = true;
            }

            entry.LastTouchedAt = LaterOf(
                entry.LastTouchedAt,
                observedAt);
            _entries[index] = entry;
        }

        public void RecordTargetAcquired(
            PlayerId playerId,
            AiSimulationTime acquiredAt)
        {
            ValidateEvent(playerId, acquiredAt);
            var index = GetOrCreateEntry(playerId, acquiredAt);
            var entry = _entries[index];

            if (!entry.HasLastAcquired
                || acquiredAt.CompareTo(entry.LastAcquiredAt) >= 0)
            {
                entry.LastAcquiredAt = acquiredAt;
                entry.HasLastAcquired = true;
            }

            entry.LastTouchedAt = LaterOf(
                entry.LastTouchedAt,
                acquiredAt);
            _entries[index] = entry;
        }

        public float GetRecentDetection01(
            PlayerId playerId,
            AiSimulationTime now,
            float decayWindowSeconds)
        {
            ValidateQuery(playerId, now, decayWindowSeconds);
            var index = FindEntry(playerId);
            return index < 0 || !_entries[index].HasLastSeen
                ? 0f
                : CalculateDecay01(
                    _entries[index].LastSeenAt,
                    now,
                    decayWindowSeconds);
        }

        public float GetTargetHistory01(
            PlayerId playerId,
            AiSimulationTime now,
            float decayWindowSeconds)
        {
            ValidateQuery(playerId, now, decayWindowSeconds);
            var index = FindEntry(playerId);
            return index < 0 || !_entries[index].HasLastAcquired
                ? 0f
                : CalculateDecay01(
                    _entries[index].LastAcquiredAt,
                    now,
                    decayWindowSeconds);
        }

        private int GetOrCreateEntry(
            PlayerId playerId,
            AiSimulationTime eventTime)
        {
            var existingIndex = FindEntry(playerId);
            if (existingIndex >= 0)
            {
                return existingIndex;
            }

            if (_count < _entries.Length)
            {
                var newIndex = _count;
                _count++;
                _entries[newIndex] = new Entry
                {
                    PlayerId = playerId,
                    LastTouchedAt = eventTime
                };
                return newIndex;
            }

            var replacementIndex = 0;
            for (var i = 1; i < _entries.Length; i++)
            {
                var comparison = _entries[i].LastTouchedAt.CompareTo(
                    _entries[replacementIndex].LastTouchedAt);
                if (comparison < 0
                    || (comparison == 0
                        && _entries[i].PlayerId.CompareTo(
                            _entries[replacementIndex].PlayerId) > 0))
                {
                    replacementIndex = i;
                }
            }

            _entries[replacementIndex] = new Entry
            {
                PlayerId = playerId,
                LastTouchedAt = eventTime
            };
            return replacementIndex;
        }

        private int FindEntry(PlayerId playerId)
        {
            for (var i = 0; i < _count; i++)
            {
                if (_entries[i].PlayerId == playerId)
                {
                    return i;
                }
            }

            return -1;
        }

        private static float CalculateDecay01(
            AiSimulationTime eventTime,
            AiSimulationTime now,
            float decayWindowSeconds)
        {
            var ageSeconds = Math.Max(
                0d,
                now.Seconds - eventTime.Seconds);
            if (ageSeconds >= decayWindowSeconds)
            {
                return 0f;
            }

            return 1f - (float)(ageSeconds / decayWindowSeconds);
        }

        private static void ValidateEvent(
            PlayerId playerId,
            AiSimulationTime eventTime)
        {
            if (!playerId.IsValid)
            {
                throw new ArgumentException(
                    "Target history requires a valid player id.",
                    nameof(playerId));
            }

            if (!eventTime.IsValid)
            {
                throw new ArgumentException(
                    "Target history requires valid simulation time.",
                    nameof(eventTime));
            }
        }

        private static void ValidateQuery(
            PlayerId playerId,
            AiSimulationTime now,
            float decayWindowSeconds)
        {
            ValidateEvent(playerId, now);
            if (float.IsNaN(decayWindowSeconds)
                || float.IsInfinity(decayWindowSeconds)
                || decayWindowSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(decayWindowSeconds),
                    decayWindowSeconds,
                    "History decay window must be finite and positive.");
            }
        }

        private static AiSimulationTime LaterOf(
            AiSimulationTime left,
            AiSimulationTime right)
        {
            return !left.IsValid || right.CompareTo(left) > 0
                ? right
                : left;
        }

        private struct Entry
        {
            public PlayerId PlayerId;
            public AiSimulationTime LastSeenAt;
            public bool HasLastSeen;
            public AiSimulationTime LastAcquiredAt;
            public bool HasLastAcquired;
            public AiSimulationTime LastTouchedAt;
        }
    }
}
