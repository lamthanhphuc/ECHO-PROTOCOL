using EchoProtocol.AI.Common;
using EchoProtocol.AI.Stalker.Networking;
using EchoProtocol.AI.Stalker.Telemetry;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Debug
{
    public static class StalkerDebugProvider
    {
        public static StalkerAIDebugSnapshot CreateHostSnapshot(
            StalkerController controller,
            StalkerFusionRuntime runtime = null)
        {
            if (controller == null)
            {
                return default;
            }

            var searchContext = controller.ActiveSearchContext;
            var navigationDestination = default(Vector3);
            var hasNavigationDestination = controller.TryGetNavigationDestination(out navigationDestination);
            var replicatedState = runtime != null && runtime.Object != null
                ? runtime.GetReplicatedPresentationState()
                : runtime != null
                    ? runtime.LastAuthoritativePresentationState
                    : default;
            var pathStatus = controller.NavigationPathStatus;
            var activeAttackPhase = controller.CurrentState == StalkerState.RECOVER
                ? StalkerNetworkAttackPhase.Recover
                : controller.ActiveAttackEpisodeId.IsValid && controller.CurrentState == StalkerState.ATTACK
                    ? StalkerNetworkAttackPhase.Windup
                    : StalkerNetworkAttackPhase.None;
            var activeAttackProgress = controller.CurrentState == StalkerState.RECOVER
                ? controller.RecoverElapsedTime
                : controller.ActiveAttackEpisode.WindupElapsedSeconds;

            return new StalkerAIDebugSnapshot(
                controller.CurrentState,
                controller.CurrentTargetId,
                controller.DetectionTargetId,
                controller.DetectionMeter,
                controller.HasLastKnownPosition,
                controller.LastKnownPosition,
                controller.HasLastSeenDirection,
                controller.LastSeenDirection,
                controller.HasTargetLastSeenTime,
                controller.TargetLastSeenTime,
                controller.CurrentRegionIdValue,
                controller.GlobalObjectiveRegionIdValue,
                controller.LastPatrolScore,
                controller.PlannerRunCount,
                controller.CandidateCount,
                controller.DynamicPreviousSpatialNodeId,
                controller.DynamicCurrentSpatialNodeId,
                controller.DynamicDestinationSpatialNodeId,
                hasNavigationDestination,
                navigationDestination,
                pathStatus,
                pathStatus == NavigationPathStatus.Pending,
                pathStatus == NavigationPathStatus.Stale,
                controller.NavigationExecutionStatus,
                controller.NavigationFailureReason,
                controller.RecoveryReason,
                controller.SearchElapsedTime,
                controller.ActiveSearchEpisodeId,
                controller.SearchCandidateNodeId,
                searchContext != null ? searchContext.CandidateAttemptCount : 0,
                searchContext != null ? searchContext.VisitedSearchNodeCount : 0,
                searchContext != null ? searchContext.SearchOriginLKP : default,
                searchContext != null ? searchContext.SearchOriginDirection : default,
                controller.ActiveAttackEpisodeId,
                activeAttackPhase,
                activeAttackProgress,
                controller.HitMomentResolved,
                controller.AttackOutcome,
                controller.AttackResolutionResult,
                controller.FixedFallbackActive,
                controller.RegionGraphFallbackReason,
                runtime != null && runtime.HasStateAuthorityForDebug,
                runtime != null ? runtime.AuthoritativeSimulationCount : 0,
                replicatedState,
                controller.HasCommittedAttackResolutionFact,
                controller.LastCommittedAttackResolutionFact,
                controller.HasCommittedSearchEndedFact,
                controller.LastCommittedSearchEndedFact);
        }
    }
}
