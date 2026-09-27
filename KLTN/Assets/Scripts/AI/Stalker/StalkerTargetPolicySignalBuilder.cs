using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common;
using UnityEngine;

namespace EchoProtocol.AI.Stalker
{
    public readonly struct StalkerTargetCandidate
    {
        public StalkerTargetCandidate(VisionObservation observation, StalkerTargetEligibilityResult eligibility)
        {
            Observation = observation;
            Eligibility = eligibility;
        }

        public VisionObservation Observation { get; }
        public StalkerTargetEligibilityResult Eligibility { get; }
    }

    public readonly struct StalkerTargetPolicySignals
    {
        public StalkerTargetPolicySignals(
            bool isObjectiveCarrier, float visibleIsolation01,
            float recentDetection01, float targetHistory01)
        {
            IsObjectiveCarrier = isObjectiveCarrier;
            VisibleIsolation01 = ValidateNormalized(visibleIsolation01, nameof(visibleIsolation01));
            RecentDetection01 = ValidateNormalized(recentDetection01, nameof(recentDetection01));
            TargetHistory01 = ValidateNormalized(targetHistory01, nameof(targetHistory01));
        }

        public bool IsObjectiveCarrier { get; }
        public float VisibleIsolation01 { get; }
        public float RecentDetection01 { get; }
        public float TargetHistory01 { get; }

        public static StalkerTargetPolicySignals None =>
            new StalkerTargetPolicySignals(false, 0f, 0f, 0f);

        private static float ValidateNormalized(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 1f)
                throw new ArgumentOutOfRangeException(
                    parameterName, value, "Target-policy signal must be finite and within [0, 1].");
            return value;
        }
    }

    public readonly struct StalkerTargetPolicyCandidate
    {
        public StalkerTargetPolicyCandidate(StalkerTargetCandidate target, StalkerTargetPolicySignals signals)
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

    public static class StalkerTargetPolicySignalBuilder
    {
        private const float FullIsolationDistance = 8f;
        private const float RecentDetectionWindowSeconds = 10f;
        private const float TargetHistoryWindowSeconds = 45f;

        public static void Build(
            IReadOnlyList<StalkerTargetCandidate> visibleCandidates,
            IReadOnlyList<PlayerId> visibleObjectiveCarrierIds,
            AiSimulationTime simulationTime,
            StalkerTargetHistoryMemory history,
            List<StalkerTargetPolicyCandidate> results)
        {
            if (visibleCandidates == null) throw new ArgumentNullException(nameof(visibleCandidates));
            if (history == null) throw new ArgumentNullException(nameof(history));
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (!simulationTime.IsValid)
                throw new ArgumentException(
                    "Signal building requires valid simulation time.", nameof(simulationTime));

            results.Clear();
            for (var i = 0; i < visibleCandidates.Count; i++)
            {
                var target = visibleCandidates[i];
                var signals = target.Eligibility.Eligible
                    ? new StalkerTargetPolicySignals(
                        ContainsPlayerId(visibleObjectiveCarrierIds, target.Observation.PlayerId),
                        CalculateVisibleIsolation01(visibleCandidates, i),
                        history.GetRecentDetection01(
                            target.Observation.PlayerId, simulationTime, RecentDetectionWindowSeconds),
                        history.GetTargetHistory01(
                            target.Observation.PlayerId, simulationTime, TargetHistoryWindowSeconds))
                    : StalkerTargetPolicySignals.None;
                results.Add(new StalkerTargetPolicyCandidate(target, signals));
            }
        }

        private static bool ContainsPlayerId(IReadOnlyList<PlayerId> playerIds, PlayerId playerId)
        {
            if (playerIds == null) return false;
            for (var i = 0; i < playerIds.Count; i++)
                if (playerIds[i] == playerId) return true;
            return false;
        }

        private static float CalculateVisibleIsolation01(
            IReadOnlyList<StalkerTargetCandidate> visibleCandidates, int candidateIndex)
        {
            var candidate = visibleCandidates[candidateIndex];
            var hasOtherEligible = false;
            var nearestDistance = 0f;
            for (var i = 0; i < visibleCandidates.Count; i++)
            {
                if (i == candidateIndex || !visibleCandidates[i].Eligibility.Eligible) continue;
                var distance = Vector3.Distance(
                    candidate.Observation.ObservedPosition,
                    visibleCandidates[i].Observation.ObservedPosition);
                if (!hasOtherEligible || distance < nearestDistance)
                {
                    nearestDistance = distance;
                    hasOtherEligible = true;
                }
            }

            return hasOtherEligible ? Mathf.Clamp01(nearestDistance / FullIsolationDistance) : 1f;
        }
    }
}
