using System;
using EchoProtocol.AI.Common;

namespace EchoProtocol.AI.Stalker
{
    public readonly struct StalkerTargetPolicySignals
    {
        public StalkerTargetPolicySignals(
            bool isObjectiveCarrier,
            float visibleIsolation01,
            float recentDetection01,
            float confirmedNoisyBehavior01,
            float targetHistory01)
        {
            IsObjectiveCarrier = isObjectiveCarrier;
            VisibleIsolation01 = ValidateNormalized(
                visibleIsolation01,
                nameof(visibleIsolation01));
            RecentDetection01 = ValidateNormalized(
                recentDetection01,
                nameof(recentDetection01));
            ConfirmedNoisyBehavior01 = ValidateNormalized(
                confirmedNoisyBehavior01,
                nameof(confirmedNoisyBehavior01));
            TargetHistory01 = ValidateNormalized(
                targetHistory01,
                nameof(targetHistory01));
        }

        public bool IsObjectiveCarrier { get; }

        public float VisibleIsolation01 { get; }

        public float RecentDetection01 { get; }

        public float ConfirmedNoisyBehavior01 { get; }

        public float TargetHistory01 { get; }

        public static StalkerTargetPolicySignals None =>
            new StalkerTargetPolicySignals(
                false,
                0f,
                0f,
                0f,
                0f);

        private static float ValidateNormalized(
            float value,
            string parameterName)
        {
            if (float.IsNaN(value)
                || float.IsInfinity(value)
                || value < 0f
                || value > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Target-policy signal must be finite and within [0, 1].");
            }

            return value;
        }
    }

    public readonly struct StalkerTargetPolicyCandidate
    {
        public StalkerTargetPolicyCandidate(
            StalkerTargetCandidate target,
            StalkerTargetPolicySignals signals)
        {
            Target = target;
            Signals = signals;
        }

        public StalkerTargetCandidate Target { get; }

        public StalkerTargetPolicySignals Signals { get; }

        public PlayerId PlayerId => Target.Observation.PlayerId;

        public float Distance => Target.Observation.Distance;

        public bool Eligible => Target.Eligibility.Eligible;
    }

    public readonly struct StalkerTargetPolicyContext
    {
        public StalkerTargetPolicyContext(
            AiSimulationTime simulationTime,
            PlayerId currentTargetId)
        {
            if (!simulationTime.IsValid)
            {
                throw new ArgumentException(
                    "Target-policy context requires valid simulation time.",
                    nameof(simulationTime));
            }

            SimulationTime = simulationTime;
            CurrentTargetId = currentTargetId;
        }

        public AiSimulationTime SimulationTime { get; }

        public PlayerId CurrentTargetId { get; }
    }
}
