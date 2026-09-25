using System;
using System.Collections.Generic;

namespace EchoProtocol.AI.Stalker
{
    public readonly struct StalkerTargetScoreBreakdown
    {
        public StalkerTargetScoreBreakdown(
            float distanceContribution,
            float isolationContribution,
            float objectiveCarrierContribution,
            float recentDetectionContribution,
            float confirmedNoiseContribution,
            float targetHistoryContribution)
        {
            DistanceContribution = distanceContribution;
            IsolationContribution = isolationContribution;
            ObjectiveCarrierContribution = objectiveCarrierContribution;
            RecentDetectionContribution = recentDetectionContribution;
            ConfirmedNoiseContribution = confirmedNoiseContribution;
            TargetHistoryContribution = targetHistoryContribution;
            TotalScore = distanceContribution
                + isolationContribution
                + objectiveCarrierContribution
                + recentDetectionContribution
                + confirmedNoiseContribution
                + targetHistoryContribution;
        }

        public float DistanceContribution { get; }

        public float IsolationContribution { get; }

        public float ObjectiveCarrierContribution { get; }

        public float RecentDetectionContribution { get; }

        public float ConfirmedNoiseContribution { get; }

        public float TargetHistoryContribution { get; }

        public float TotalScore { get; }
    }

    public sealed class AdaptiveStalkerTargetPolicy
        : IStalkerTargetPolicy
    {
        private readonly StalkerTargetPolicyWeights _weights;

        public AdaptiveStalkerTargetPolicy()
            : this(StalkerTargetPolicyWeights.Default)
        {
        }

        public AdaptiveStalkerTargetPolicy(
            StalkerTargetPolicyWeights weights)
        {
            _weights = weights;
        }

        public StalkerTargetPolicyWeights Weights => _weights;

        public StalkerTargetScoreBreakdown CalculateScore(
            StalkerTargetPolicyCandidate candidate)
        {
            var distanceScore01 = 1f / (1f + candidate.Distance);
            var signals = candidate.Signals;

            return new StalkerTargetScoreBreakdown(
                _weights.DistanceWeight * distanceScore01,
                _weights.IsolationWeight * signals.VisibleIsolation01,
                _weights.ObjectiveCarrierWeight
                    * (signals.IsObjectiveCarrier ? 1f : 0f),
                _weights.RecentDetectionWeight
                    * signals.RecentDetection01,
                _weights.ConfirmedNoiseWeight
                    * signals.ConfirmedNoisyBehavior01,
                -_weights.TargetHistoryWeight
                    * signals.TargetHistory01);
        }

        public bool TrySelectTarget(
            IReadOnlyList<StalkerTargetPolicyCandidate> candidates,
            StalkerTargetPolicyContext context,
            out VisionObservation selectedObservation)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (!context.SimulationTime.IsValid)
            {
                throw new ArgumentException(
                    "Target-policy context requires valid simulation time.",
                    nameof(context));
            }

            selectedObservation = default;
            var bestScore = 0f;
            var hasEligibleCandidate = false;

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (!candidate.Eligible || !candidate.PlayerId.IsValid)
                {
                    continue;
                }

                var score = CalculateScore(candidate).TotalScore;
                if (!hasEligibleCandidate || score > bestScore)
                {
                    bestScore = score;
                    hasEligibleCandidate = true;
                }
            }

            if (!hasEligibleCandidate)
            {
                return false;
            }

            var minimumTieScore = bestScore - _weights.ScoreTieEpsilon;
            var hasSelected = false;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (!candidate.Eligible
                    || !candidate.PlayerId.IsValid
                    || CalculateScore(candidate).TotalScore < minimumTieScore)
                {
                    continue;
                }

                if (!hasSelected
                    || candidate.PlayerId.CompareTo(
                        selectedObservation.PlayerId) < 0)
                {
                    selectedObservation = candidate.Target.Observation;
                    hasSelected = true;
                }
            }

            return hasSelected;
        }
    }
}
