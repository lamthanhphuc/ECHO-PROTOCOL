using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common;

namespace EchoProtocol.AI.Stalker
{
    public static class StalkerPerceptionEvaluator
    {
        public static int CollectVisibleTargetCandidates(
            StalkerVisionSensor sensor,
            IReadOnlyList<StalkerPerceptionTargetSnapshot> targets,
            AiSimulationTime observedAt,
            List<StalkerTargetCandidate> results)
        {
            if (targets == null)
            {
                throw new ArgumentNullException(nameof(targets));
            }

            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();

            if (sensor == null || !observedAt.IsValid)
            {
                return 0;
            }

            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (!IsValidTarget(target))
                {
                    continue;
                }

                if (!sensor.TryEvaluateCandidate(
                        target.TargetSample,
                        target.TargetHierarchyRoot,
                        out var physicalObservation))
                {
                    continue;
                }

                var visionObservation = new VisionObservation(
                    target.PlayerId,
                    physicalObservation.ObservedPosition,
                    physicalObservation.ObservedDirection,
                    observedAt,
                    physicalObservation.Distance);
                var eligibility = StalkerTargetEligibility.Evaluate(target.EligibilitySnapshot);

                results.Add(new StalkerTargetCandidate(visionObservation, eligibility));
            }

            return results.Count;
        }

        private static bool IsValidTarget(StalkerPerceptionTargetSnapshot target)
        {
            if (!target.PlayerId.IsValid || target.TargetSample == null || target.TargetHierarchyRoot == null)
            {
                return false;
            }

            return target.TargetSample == target.TargetHierarchyRoot
                || target.TargetSample.IsChildOf(target.TargetHierarchyRoot);
        }
    }
}
