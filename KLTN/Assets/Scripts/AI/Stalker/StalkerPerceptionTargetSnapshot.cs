using EchoProtocol.AI.Common;
using UnityEngine;

namespace EchoProtocol.AI.Stalker
{
    public readonly struct StalkerPerceptionTargetSnapshot
    {
        public StalkerPerceptionTargetSnapshot(
            PlayerId playerId,
            Transform targetSample,
            Transform targetHierarchyRoot,
            StalkerTargetEligibilitySnapshot eligibilitySnapshot)
            : this(
                playerId,
                targetSample,
                targetHierarchyRoot,
                eligibilitySnapshot,
                false)
        {
        }

        public StalkerPerceptionTargetSnapshot(
            PlayerId playerId,
            Transform targetSample,
            Transform targetHierarchyRoot,
            StalkerTargetEligibilitySnapshot eligibilitySnapshot,
            bool isObjectiveCarrier)
        {
            PlayerId = playerId;
            TargetSample = targetSample;
            TargetHierarchyRoot = targetHierarchyRoot;
            EligibilitySnapshot = eligibilitySnapshot;
            IsObjectiveCarrier = isObjectiveCarrier;
        }

        public PlayerId PlayerId { get; }

        public Transform TargetSample { get; }

        public Transform TargetHierarchyRoot { get; }

        public StalkerTargetEligibilitySnapshot EligibilitySnapshot { get; }

        public bool IsObjectiveCarrier { get; }
    }
}
