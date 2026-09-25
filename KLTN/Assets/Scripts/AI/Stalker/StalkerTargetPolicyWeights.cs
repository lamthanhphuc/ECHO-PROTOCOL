using System;

namespace EchoProtocol.AI.Stalker
{
    public readonly struct StalkerTargetPolicyWeights
    {
        public StalkerTargetPolicyWeights(
            float distanceWeight,
            float isolationWeight,
            float objectiveCarrierWeight,
            float recentDetectionWeight,
            float confirmedNoiseWeight,
            float targetHistoryWeight,
            float scoreTieEpsilon)
        {
            DistanceWeight = ValidateWeight(
                distanceWeight,
                nameof(distanceWeight));
            IsolationWeight = ValidateWeight(
                isolationWeight,
                nameof(isolationWeight));
            ObjectiveCarrierWeight = ValidateWeight(
                objectiveCarrierWeight,
                nameof(objectiveCarrierWeight));
            RecentDetectionWeight = ValidateWeight(
                recentDetectionWeight,
                nameof(recentDetectionWeight));
            ConfirmedNoiseWeight = ValidateWeight(
                confirmedNoiseWeight,
                nameof(confirmedNoiseWeight));
            TargetHistoryWeight = ValidateWeight(
                targetHistoryWeight,
                nameof(targetHistoryWeight));
            ScoreTieEpsilon = ValidateWeight(
                scoreTieEpsilon,
                nameof(scoreTieEpsilon));

            var maximumScore = (double)DistanceWeight
                + IsolationWeight
                + ObjectiveCarrierWeight
                + RecentDetectionWeight
                + ConfirmedNoiseWeight
                + TargetHistoryWeight;
            if (maximumScore > float.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(targetHistoryWeight),
                    targetHistoryWeight,
                    "Combined target-policy weights must produce a finite score.");
            }
        }

        public float DistanceWeight { get; }

        public float IsolationWeight { get; }

        public float ObjectiveCarrierWeight { get; }

        public float RecentDetectionWeight { get; }

        public float ConfirmedNoiseWeight { get; }

        public float TargetHistoryWeight { get; }

        public float ScoreTieEpsilon { get; }

        public static StalkerTargetPolicyWeights Default =>
            new StalkerTargetPolicyWeights(
                1.00f,
                0.65f,
                0.90f,
                0.60f,
                0f,
                0.35f,
                0.0001f);

        private static float ValidateWeight(
            float value,
            string parameterName)
        {
            if (float.IsNaN(value)
                || float.IsInfinity(value)
                || value < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Target-policy weight must be finite and non-negative.");
            }

            return value;
        }
    }
}
