using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common;

namespace EchoProtocol.AI.Stalker
{
    public readonly struct StalkerTargetCandidate
    {
        public StalkerTargetCandidate(
            VisionObservation observation,
            StalkerTargetEligibilityResult eligibility)
        {
            Observation = observation;
            Eligibility = eligibility;
        }

        public VisionObservation Observation { get; }

        public StalkerTargetEligibilityResult Eligibility { get; }
    }

    public static class StalkerTargetSelector
    {
        public static bool TrySelectNearestEligibleVisible(
            IReadOnlyList<StalkerTargetCandidate> candidates,
            float distanceTieEpsilon,
            out VisionObservation selectedObservation)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (float.IsNaN(distanceTieEpsilon) || float.IsInfinity(distanceTieEpsilon) || distanceTieEpsilon < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(distanceTieEpsilon), distanceTieEpsilon, "Distance tie epsilon must be finite and non-negative.");
            }

            selectedObservation = default;
            var hasEligibleCandidate = false;
            var minDistance = 0f;

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (!candidate.Eligibility.Eligible)
                {
                    continue;
                }

                if (!hasEligibleCandidate || candidate.Observation.Distance < minDistance)
                {
                    minDistance = candidate.Observation.Distance;
                    hasEligibleCandidate = true;
                }
            }

            if (!hasEligibleCandidate)
            {
                return false;
            }

            var maxTieDistance = minDistance + distanceTieEpsilon;
            var hasSelected = false;

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (!candidate.Eligibility.Eligible || candidate.Observation.Distance > maxTieDistance)
                {
                    continue;
                }

                if (!hasSelected
                    || DeterministicTieBreak.ComparePrimaryThenStableKey(
                        0,
                        candidate.Observation.PlayerId,
                        selectedObservation.PlayerId) < 0)
                {
                    selectedObservation = candidate.Observation;
                    hasSelected = true;
                }
            }

            return hasSelected;
        }
    }
}
