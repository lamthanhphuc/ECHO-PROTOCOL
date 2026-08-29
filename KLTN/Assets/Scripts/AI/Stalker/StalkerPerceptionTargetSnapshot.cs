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
        {
            PlayerId = playerId;
            TargetSample = targetSample;
            TargetHierarchyRoot = targetHierarchyRoot;
            EligibilitySnapshot = eligibilitySnapshot;
        }

        public PlayerId PlayerId { get; }

        public Transform TargetSample { get; }

        public Transform TargetHierarchyRoot { get; }

        public StalkerTargetEligibilitySnapshot EligibilitySnapshot { get; }
    }
}
