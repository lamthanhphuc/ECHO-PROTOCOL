using EchoProtocol.AI.Common;
using EchoProtocol.AI.Stalker.Networking;
using EchoProtocol.AI.Stalker.Spatial;
using EchoProtocol.AI.Stalker.Telemetry;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Debug
{
    public readonly struct StalkerAIDebugSnapshot
    {
        public StalkerAIDebugSnapshot(
            StalkerState state,
            PlayerId currentTargetId,
            PlayerId detectionTargetId,
            float detectionMeter,
            bool hasLastKnownPosition,
            Vector3 lastKnownPosition,
            bool hasLastSeenDirection,
            Vector3 lastSeenDirection,
            bool hasTargetLastSeenTime,
            AiSimulationTime targetLastSeenTime,
            int currentRegionId,
            int globalObjectiveRegionId,
            float lastPatrolScore,
            int plannerRunCount,
            int candidateCount,
            int previousSpatialNodeId,
            int currentSpatialNodeId,
            int destinationSpatialNodeId,
            bool hasNavigationDestination,
            Vector3 navigationDestination,
            NavigationPathStatus navigationPathStatus,
            bool pathPending,
            bool isPathStale,
            NavigationExecutionStatus navigationExecutionStatus,
            NavigationFailureReason navigationFailureReason,
            NavigationRecoveryReason navigationRecoveryReason,
            float searchElapsed,
            SearchEpisodeId searchEpisodeId,
            int searchCandidateNodeId,
            int searchCandidateAttemptCount,
            int searchVisitedNodeCount,
            Vector3 searchOriginLastKnownPosition,
            Vector3 searchOriginDirection,
            StalkerAttackEpisodeId activeAttackEpisodeId,
            StalkerNetworkAttackPhase activeAttackPhase,
            float activeAttackProgressSeconds,
            bool attackHitMomentResolved,
            StalkerAttackOutcome attackOutcome,
            StalkerAttackResolutionResult lastAttackResolutionResult,
            bool fixedFallbackActive,
            RegionGraphFallbackReason fixedFallbackReason,
            bool hasStateAuthority,
            int authoritativeSimulationCount,
            StalkerNetworkPresentationState replicatedPresentationState,
            bool hasCommittedAttackResolutionFact,
            StalkerAttackResolvedFact committedAttackResolutionFact,
            bool hasCommittedSearchEndedFact,
            StalkerSearchEndedFact committedSearchEndedFact)
        {
            State = state;
            CurrentTargetId = currentTargetId;
            DetectionTargetId = detectionTargetId;
            DetectionMeter = detectionMeter;
            HasLastKnownPosition = hasLastKnownPosition;
            LastKnownPosition = lastKnownPosition;
            HasLastSeenDirection = hasLastSeenDirection;
            LastSeenDirection = lastSeenDirection;
            HasTargetLastSeenTime = hasTargetLastSeenTime;
            TargetLastSeenTime = targetLastSeenTime;
            CurrentRegionId = currentRegionId;
            GlobalObjectiveRegionId = globalObjectiveRegionId;
            LastPatrolScore = lastPatrolScore;
            PlannerRunCount = plannerRunCount;
            CandidateCount = candidateCount;
            PreviousSpatialNodeId = previousSpatialNodeId;
            CurrentSpatialNodeId = currentSpatialNodeId;
            DestinationSpatialNodeId = destinationSpatialNodeId;
            HasNavigationDestination = hasNavigationDestination;
            NavigationDestination = navigationDestination;
            NavigationPathStatus = navigationPathStatus;
            PathPending = pathPending;
            IsPathStale = isPathStale;
            NavigationExecutionStatus = navigationExecutionStatus;
            NavigationFailureReason = navigationFailureReason;
            NavigationRecoveryReason = navigationRecoveryReason;
            SearchElapsed = searchElapsed;
            SearchEpisodeId = searchEpisodeId;
            SearchCandidateNodeId = searchCandidateNodeId;
            SearchCandidateAttemptCount = searchCandidateAttemptCount;
            SearchVisitedNodeCount = searchVisitedNodeCount;
            SearchOriginLastKnownPosition = searchOriginLastKnownPosition;
            SearchOriginDirection = searchOriginDirection;
            ActiveAttackEpisodeId = activeAttackEpisodeId;
            ActiveAttackPhase = activeAttackPhase;
            ActiveAttackProgressSeconds = activeAttackProgressSeconds;
            AttackHitMomentResolved = attackHitMomentResolved;
            AttackOutcome = attackOutcome;
            LastAttackResolutionResult = lastAttackResolutionResult;
            FixedFallbackActive = fixedFallbackActive;
            FixedFallbackReason = fixedFallbackReason;
            HasStateAuthority = hasStateAuthority;
            AuthoritativeSimulationCount = authoritativeSimulationCount;
            ReplicatedPresentationState = replicatedPresentationState;
            HasCommittedAttackResolutionFact = hasCommittedAttackResolutionFact;
            CommittedAttackResolutionFact = committedAttackResolutionFact;
            HasCommittedSearchEndedFact = hasCommittedSearchEndedFact;
            CommittedSearchEndedFact = committedSearchEndedFact;
        }

        public StalkerState State { get; }
        public PlayerId CurrentTargetId { get; }
        public PlayerId DetectionTargetId { get; }
        public float DetectionMeter { get; }
        public bool HasLastKnownPosition { get; }
        public Vector3 LastKnownPosition { get; }
        public bool HasLastSeenDirection { get; }
        public Vector3 LastSeenDirection { get; }
        public bool HasTargetLastSeenTime { get; }
        public AiSimulationTime TargetLastSeenTime { get; }
        public int CurrentRegionId { get; }
        public int GlobalObjectiveRegionId { get; }
        public float LastPatrolScore { get; }
        public int PlannerRunCount { get; }
        public int CandidateCount { get; }
        public int PreviousSpatialNodeId { get; }
        public int CurrentSpatialNodeId { get; }
        public int DestinationSpatialNodeId { get; }
        public bool HasNavigationDestination { get; }
        public Vector3 NavigationDestination { get; }
        public NavigationPathStatus NavigationPathStatus { get; }
        public bool PathPending { get; }
        public bool IsPathStale { get; }
        public NavigationExecutionStatus NavigationExecutionStatus { get; }
        public NavigationFailureReason NavigationFailureReason { get; }
        public NavigationRecoveryReason NavigationRecoveryReason { get; }
        public float SearchElapsed { get; }
        public SearchEpisodeId SearchEpisodeId { get; }
        public int SearchCandidateNodeId { get; }
        public int SearchCandidateAttemptCount { get; }
        public int SearchVisitedNodeCount { get; }
        public Vector3 SearchOriginLastKnownPosition { get; }
        public Vector3 SearchOriginDirection { get; }
        public StalkerAttackEpisodeId ActiveAttackEpisodeId { get; }
        public StalkerNetworkAttackPhase ActiveAttackPhase { get; }
        public float ActiveAttackProgressSeconds { get; }
        public bool AttackHitMomentResolved { get; }
        public StalkerAttackOutcome AttackOutcome { get; }
        public StalkerAttackResolutionResult LastAttackResolutionResult { get; }
        public bool FixedFallbackActive { get; }
        public RegionGraphFallbackReason FixedFallbackReason { get; }
        public bool HasStateAuthority { get; }
        public int AuthoritativeSimulationCount { get; }
        public StalkerNetworkPresentationState ReplicatedPresentationState { get; }
        public bool HasCommittedAttackResolutionFact { get; }
        public StalkerAttackResolvedFact CommittedAttackResolutionFact { get; }
        public bool HasCommittedSearchEndedFact { get; }
        public StalkerSearchEndedFact CommittedSearchEndedFact { get; }
    }
}
