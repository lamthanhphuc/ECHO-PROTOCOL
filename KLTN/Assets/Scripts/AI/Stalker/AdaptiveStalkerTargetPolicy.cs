using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common;
using EchoProtocol.AI.Common.AED;

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
            => Select(candidates, null, out selectedObservation);

        public static bool TrySelectV02Target(IReadOnlyList<StalkerTargetPolicyCandidate> candidates,
            IReadOnlyDictionary<PlayerId, int> tiers, out VisionObservation selectedObservation)
            => Select(candidates, tiers, out selectedObservation);

        private static float V02Score(StalkerTargetPolicyCandidate candidate, IReadOnlyDictionary<PlayerId, int> tiers) =>
            CalculateScore(candidate) + (tiers != null && tiers.TryGetValue(candidate.PlayerId, out int tier) ? (float)AEDV02PilotPolicy.TargetAdjustment(tier) : 0f);

        private static bool Select(IReadOnlyList<StalkerTargetPolicyCandidate> candidates,
            IReadOnlyDictionary<PlayerId, int> tiers, out VisionObservation selectedObservation)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));

            selectedObservation = default;
            var bestScore = 0f;
            var hasEligibleCandidate = false;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (!candidate.Eligible || !candidate.PlayerId.IsValid) continue;
                var score = V02Score(candidate, tiers);
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
                    || V02Score(candidate, tiers) < minimumTieScore)
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
