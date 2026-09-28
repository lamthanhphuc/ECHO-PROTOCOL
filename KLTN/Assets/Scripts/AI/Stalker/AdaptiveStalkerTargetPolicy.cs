using System;
using System.Collections.Generic;

namespace EchoProtocol.AI.Stalker
{
    public static class AdaptiveStalkerTargetPolicy
    {
        private const float DistanceWeight = 1.00f;
        private const float IsolationWeight = 0.65f;
        private const float ObjectiveCarrierWeight = 0.90f;
        private const float RecentDetectionWeight = 0.60f;
        private const float TargetHistoryWeight = 0.35f;
        private const float ScoreTieEpsilon = 0.0001f;

        private static float CalculateScore(StalkerTargetPolicyCandidate candidate)
        {
            var signals = candidate.Signals;
            var distanceScore = 1f / (1f + candidate.Distance);
            return DistanceWeight * distanceScore
                + IsolationWeight * signals.VisibleIsolation01
                + ObjectiveCarrierWeight * (signals.IsObjectiveCarrier ? 1f : 0f)
                + RecentDetectionWeight * signals.RecentDetection01
                - TargetHistoryWeight * signals.TargetHistory01;
        }

        public static bool TrySelectTarget(
            IReadOnlyList<StalkerTargetPolicyCandidate> candidates,
            out VisionObservation selectedObservation)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));

            selectedObservation = default;
            var bestScore = 0f;
            var hasEligibleCandidate = false;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (!candidate.Eligible || !candidate.PlayerId.IsValid) continue;
                var score = CalculateScore(candidate);
                if (!hasEligibleCandidate || score > bestScore)
                {
                    bestScore = score;
                    hasEligibleCandidate = true;
                }
            }

            if (!hasEligibleCandidate) return false;

            var minimumTieScore = bestScore - ScoreTieEpsilon;
            var hasSelected = false;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (!candidate.Eligible
                    || !candidate.PlayerId.IsValid
                    || CalculateScore(candidate) < minimumTieScore)
                    continue;

                if (!hasSelected
                    || candidate.PlayerId.CompareTo(selectedObservation.PlayerId) < 0)
                {
                    selectedObservation = candidate.Target.Observation;
                    hasSelected = true;
                }
            }

            return hasSelected;
        }
    }
}
