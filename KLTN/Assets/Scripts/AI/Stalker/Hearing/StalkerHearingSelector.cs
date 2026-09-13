using System;
using System.Collections.Generic;
using EchoProtocol.AI.Listener.Perception;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Hearing
{
    public enum StalkerHearingSelectionReason
    {
        None,
        InitialInvestigation,
        RelatedSupport,
        StrongerUnrelatedInterrupt
    }

    public readonly struct StalkerHearingSelection
    {
        public StalkerHearingSelection(
            HearingObservation observation,
            StalkerHearingSelectionReason reason)
        {
            Observation = observation;
            Reason = reason;
        }

        public HearingObservation Observation { get; }

        public StalkerHearingSelectionReason Reason { get; }

        public bool HasObservation =>
            !string.IsNullOrWhiteSpace(
                Observation.NoiseEventId);
    }

    /// <summary>
    /// Deterministically selects hearing observations for Stalker.
    ///
    /// It deliberately knows nothing about PlayerId. Hearing remains
    /// a positional hypothesis until Stalker's visual system confirms
    /// an actual player.
    /// </summary>
    public sealed class StalkerHearingSelector
    {
        public const float DefaultHypothesisMergeRadius = 2.5f;
        public const double DefaultInterruptMargin = 0.2d;

        private readonly float _hypothesisMergeRadius;
        private readonly double _interruptMargin;

        private readonly List<HearingObservation> _scratch =
            new List<HearingObservation>();

        private readonly HashSet<string> _seenNoiseEventIds =
            new HashSet<string>(StringComparer.Ordinal);

        public StalkerHearingSelector(
            float hypothesisMergeRadius =
                DefaultHypothesisMergeRadius,
            double interruptMargin =
                DefaultInterruptMargin)
        {
            if (hypothesisMergeRadius < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(hypothesisMergeRadius));
            }

            if (double.IsNaN(interruptMargin)
                || double.IsInfinity(interruptMargin)
                || interruptMargin < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(interruptMargin));
            }

            _hypothesisMergeRadius =
                hypothesisMergeRadius;

            _interruptMargin =
                interruptMargin;
        }

        public bool TrySelectInitial(
            IReadOnlyList<HearingObservation> observations,
            DateTime nowUtc,
            out StalkerHearingSelection selection)
        {
            ValidateNow(nowUtc);

            selection = default;

            BuildRankedCandidates(
                observations,
                nowUtc);

            if (_scratch.Count == 0)
            {
                return false;
            }

            selection =
                new StalkerHearingSelection(
                    _scratch[0],
                    StalkerHearingSelectionReason
                        .InitialInvestigation);

            return true;
        }

        public bool TrySelectInvestigationUpdate(
            StalkerHearingMemory memory,
            IReadOnlyList<HearingObservation> observations,
            DateTime nowUtc,
            out StalkerHearingSelection selection)
        {
            if (memory == null)
            {
                throw new ArgumentNullException(
                    nameof(memory));
            }

            ValidateNow(nowUtc);

            selection = default;

            if (!memory.HasActiveNoiseInvestigation)
            {
                return TrySelectInitial(
                    observations,
                    nowUtc,
                    out selection);
            }

            BuildRankedCandidates(
                observations,
                nowUtc);

            //
            // Priority 1:
            // A sufficiently stronger unrelated sound may redirect
            // the current investigation.
            //
            for (var i = 0; i < _scratch.Count; i++)
            {
                var candidate = _scratch[i];

                if (IsRelated(
                        memory.InvestigationPosition,
                        candidate.ObservedNoisePosition))
                {
                    continue;
                }

                if (candidate.EffectiveIntensity
                    < memory.CommittedEffectiveIntensity
                    + _interruptMargin)
                {
                    continue;
                }

                selection =
                    new StalkerHearingSelection(
                        candidate,
                        StalkerHearingSelectionReason
                            .StrongerUnrelatedInterrupt);

                return true;
            }

            //
            // Priority 2:
            // A sound near the existing hypothesis supports and
            // refines the current investigation position.
            //
            for (var i = 0; i < _scratch.Count; i++)
            {
                var candidate = _scratch[i];

                if (!IsRelated(
                        memory.InvestigationPosition,
                        candidate.ObservedNoisePosition))
                {
                    continue;
                }

                selection =
                    new StalkerHearingSelection(
                        candidate,
                        StalkerHearingSelectionReason
                            .RelatedSupport);

                return true;
            }

            return false;
        }

        private void BuildRankedCandidates(
            IReadOnlyList<HearingObservation> observations,
            DateTime nowUtc)
        {
            _scratch.Clear();
            _seenNoiseEventIds.Clear();

            if (observations == null)
            {
                return;
            }

            for (var i = 0;
                 i < observations.Count;
                 i++)
            {
                var observation =
                    observations[i];

                if (string.IsNullOrWhiteSpace(
                        observation.NoiseEventId))
                {
                    continue;
                }

                if (observation.IsExpiredAt(nowUtc))
                {
                    continue;
                }

                if (!_seenNoiseEventIds.Add(
                        observation.NoiseEventId))
                {
                    continue;
                }

                _scratch.Add(observation);
            }

            _scratch.Sort(
                HearingObservationComparer.Instance);
        }

        private bool IsRelated(
            Vector3 currentPosition,
            Vector3 candidatePosition)
        {
            return Vector3.Distance(
                       currentPosition,
                       candidatePosition)
                   <= _hypothesisMergeRadius;
        }

        private static void ValidateNow(
            DateTime nowUtc)
        {
            if (nowUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Hearing selection time must be UTC.",
                    nameof(nowUtc));
            }
        }
    }
}
