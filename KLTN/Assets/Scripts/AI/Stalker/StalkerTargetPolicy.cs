using System;
using System.Collections.Generic;

namespace EchoProtocol.AI.Stalker
{
    public interface IStalkerTargetPolicy
    {
        bool TrySelectTarget(
            IReadOnlyList<StalkerTargetCandidate> candidates,
            out VisionObservation selectedObservation);
    }

    public sealed class NearestEligibleVisibleTargetPolicy : IStalkerTargetPolicy
    {
        private readonly float _distanceTieEpsilon;

        public NearestEligibleVisibleTargetPolicy(
            float distanceTieEpsilon)
        {
            if (float.IsNaN(distanceTieEpsilon)
                || float.IsInfinity(distanceTieEpsilon)
                || distanceTieEpsilon < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(distanceTieEpsilon),
                    distanceTieEpsilon,
                    "Distance tie epsilon must be finite and non-negative.");
            }

            _distanceTieEpsilon = distanceTieEpsilon;
        }

        public bool TrySelectTarget(
            IReadOnlyList<StalkerTargetCandidate> candidates,
            out VisionObservation selectedObservation)
        {
            return StalkerTargetSelector.TrySelectNearestEligibleVisible(
                candidates,
                _distanceTieEpsilon,
                out selectedObservation);
        }
    }
}
