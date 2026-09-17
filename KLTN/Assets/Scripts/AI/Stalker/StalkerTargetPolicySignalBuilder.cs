using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common;
using UnityEngine;

namespace EchoProtocol.AI.Stalker
{
    public sealed class StalkerTargetPolicySignalBuilder
    {
        public const float DefaultFullIsolationDistance = 8f;
        public const float DefaultRecentDetectionWindowSeconds = 10f;
        public const float DefaultTargetHistoryWindowSeconds = 45f;

        private readonly float _fullIsolationDistance;
        private readonly float _recentDetectionWindowSeconds;
        private readonly float _targetHistoryWindowSeconds;

        public StalkerTargetPolicySignalBuilder()
            : this(
                DefaultFullIsolationDistance,
                DefaultRecentDetectionWindowSeconds,
                DefaultTargetHistoryWindowSeconds)
        {
        }

        public StalkerTargetPolicySignalBuilder(
            float fullIsolationDistance,
            float recentDetectionWindowSeconds,
            float targetHistoryWindowSeconds)
        {
            _fullIsolationDistance = ValidatePositiveFinite(
                fullIsolationDistance,
                nameof(fullIsolationDistance));
            _recentDetectionWindowSeconds = ValidatePositiveFinite(
                recentDetectionWindowSeconds,
                nameof(recentDetectionWindowSeconds));
            _targetHistoryWindowSeconds = ValidatePositiveFinite(
                targetHistoryWindowSeconds,
                nameof(targetHistoryWindowSeconds));
        }

        public void Build(
            IReadOnlyList<StalkerTargetCandidate> visibleCandidates,
            IReadOnlyList<PlayerId> visibleObjectiveCarrierIds,
            StalkerTargetPolicyContext context,
            StalkerTargetHistoryMemory history,
            List<StalkerTargetPolicyCandidate> results)
        {
            if (visibleCandidates == null)
            {
                throw new ArgumentNullException(nameof(visibleCandidates));
            }

            if (history == null)
            {
                throw new ArgumentNullException(nameof(history));
            }

            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            if (!context.SimulationTime.IsValid)
            {
                throw new ArgumentException(
                    "Signal building requires valid simulation time.",
                    nameof(context));
            }

            results.Clear();
            for (var i = 0; i < visibleCandidates.Count; i++)
            {
                var target = visibleCandidates[i];
                var signals = target.Eligibility.Eligible
                    ? new StalkerTargetPolicySignals(
                        ContainsPlayerId(
                            visibleObjectiveCarrierIds,
                            target.Observation.PlayerId),
                        CalculateVisibleIsolation01(
                            visibleCandidates,
                            i),
                        history.GetRecentDetection01(
                            target.Observation.PlayerId,
                            context.SimulationTime,
                            _recentDetectionWindowSeconds),
                        0f,
                        history.GetTargetHistory01(
                            target.Observation.PlayerId,
                            context.SimulationTime,
                            _targetHistoryWindowSeconds))
                    : StalkerTargetPolicySignals.None;

                results.Add(
                    new StalkerTargetPolicyCandidate(
                        target,
                        signals));
            }
        }

        private static bool ContainsPlayerId(
            IReadOnlyList<PlayerId> playerIds,
            PlayerId playerId)
        {
            if (playerIds == null)
            {
                return false;
            }

            for (var i = 0; i < playerIds.Count; i++)
            {
                if (playerIds[i] == playerId)
                {
                    return true;
                }
            }

            return false;
        }

        private float CalculateVisibleIsolation01(
            IReadOnlyList<StalkerTargetCandidate> visibleCandidates,
            int candidateIndex)
        {
            var candidate = visibleCandidates[candidateIndex];
            var hasOtherEligible = false;
            var nearestDistance = 0f;

            for (var i = 0; i < visibleCandidates.Count; i++)
            {
                if (i == candidateIndex
                    || !visibleCandidates[i].Eligibility.Eligible)
                {
                    continue;
                }

                var distance = Vector3.Distance(
                    candidate.Observation.ObservedPosition,
                    visibleCandidates[i].Observation.ObservedPosition);
                if (!hasOtherEligible || distance < nearestDistance)
                {
                    nearestDistance = distance;
                    hasOtherEligible = true;
                }
            }

            return hasOtherEligible
                ? Mathf.Clamp01(
                    nearestDistance / _fullIsolationDistance)
                : 1f;
        }

        private static float ValidatePositiveFinite(
            float value,
            string parameterName)
        {
            if (float.IsNaN(value)
                || float.IsInfinity(value)
                || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Signal normalization window must be finite and positive.");
            }

            return value;
        }
    }
}
