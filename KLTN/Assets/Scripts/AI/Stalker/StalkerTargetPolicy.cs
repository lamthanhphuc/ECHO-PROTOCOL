using System;
using System.Collections.Generic;

namespace EchoProtocol.AI.Stalker
{
    public interface IStalkerTargetPolicy
    {
        bool TrySelectTarget(
            IReadOnlyList<StalkerTargetPolicyCandidate> candidates,
            StalkerTargetPolicyContext context,
            out VisionObservation selectedObservation);
    }

    public sealed class NearestEligibleVisibleTargetPolicy
        : IStalkerTargetPolicy
    {
        private readonly float _distanceTieEpsilon;

        // Compatibility projection used only by the legacy nearest policy.
        // Reused to avoid per-tick GC allocations.
        private readonly List<StalkerTargetCandidate> _selectorCandidates =
            new List<StalkerTargetCandidate>(4);

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

            _selectorCandidates.Clear();

            for (var i = 0; i < candidates.Count; i++)
            {
                _selectorCandidates.Add(candidates[i].Target);
            }

            // Phase 2A deliberately ignores adaptive signals.
            // This preserves the exact legacy nearest-target behavior.
            return StalkerTargetSelector.TrySelectNearestEligibleVisible(
                _selectorCandidates,
                _distanceTieEpsilon,
                out selectedObservation);
        }
    }
}
