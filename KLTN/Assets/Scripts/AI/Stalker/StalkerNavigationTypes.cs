using UnityEngine;

namespace EchoProtocol.AI.Stalker
{
    public enum NavigationPlanStatus
    {
        Accepted,
        AlreadyActive,
        AgentUnavailable,
        AgentNotOnNavMesh,
        DestinationRequestFailed
    }

    public enum NavigationRequestIntent
    {
        NewGoal,
        TrackMovingGoal,
        RecoveryRepath
    }

    public enum NavigationFailureReason
    {
        None,
        AgentUnavailable,
        AgentNotOnNavMesh,
        DestinationInvalid,
        PathPendingTimeout,
        PathPartial,
        PathInvalid,
        PathStale,
        DoorBlocked,
        NoProgress,
        Stuck
    }

    public enum NavigationRecoveryReason
    {
        None,
        PathStaleRepath,
        TopologyChangedRepath,
        RetryLogicalObjective,
        AlternateLocalCandidate,
        AlternateGlobalObjective,
        RegionGraphCompatibilityFallback,
        FixedPatrolFallback,
        EmergencyNavMeshRecovery
    }

    public enum StalkerNavigationObjectiveKind
    {
        None,
        FixedWaypoint,
        DynamicSpatialNode,
        ConfidenceSpatialNode,
        SearchCandidate,
        ChaseTarget
    }

    public readonly struct StalkerNavigationObjectiveKey
    {
        public StalkerNavigationObjectiveKey(
            StalkerNavigationObjectiveKind kind,
            int localNodeId,
            int globalRegionId,
            int targetPlayerId)
        {
            Kind = kind;
            LocalNodeId = localNodeId;
            GlobalRegionId = globalRegionId;
            TargetPlayerId = targetPlayerId;
        }

        public static StalkerNavigationObjectiveKey None => default;

        public StalkerNavigationObjectiveKind Kind { get; }

        public int LocalNodeId { get; }

        public int GlobalRegionId { get; }

        public int TargetPlayerId { get; }

        public bool IsValid => Kind != StalkerNavigationObjectiveKind.None;

        public bool Equals(StalkerNavigationObjectiveKey other)
        {
            return Kind == other.Kind
                && LocalNodeId == other.LocalNodeId
                && GlobalRegionId == other.GlobalRegionId
                && TargetPlayerId == other.TargetPlayerId;
        }
    }

    public readonly struct NavigationPlanResult
    {
        public NavigationPlanResult(NavigationPlanStatus status, Vector3 requestedDestination)
        {
            Status = status;
            RequestedDestination = requestedDestination;
        }

        public NavigationPlanStatus Status { get; }

        public Vector3 RequestedDestination { get; }

        public bool IsAccepted => Status == NavigationPlanStatus.Accepted
            || Status == NavigationPlanStatus.AlreadyActive;
    }

    public enum NavigationEvaluationStatus
    {
        Complete,
        Partial,
        Invalid,
        DestinationInvalid,
        AgentUnavailable,
        AgentNotOnNavMesh
    }

    public readonly struct NavigationEvaluationResult
    {
        public NavigationEvaluationResult(NavigationEvaluationStatus status, Vector3 requestedDestination)
        {
            Status = status;
            RequestedDestination = requestedDestination;
        }

        public NavigationEvaluationStatus Status { get; }

        public Vector3 RequestedDestination { get; }

        public bool IsComplete => Status == NavigationEvaluationStatus.Complete;
    }

    public enum NavigationExecutionStatus
    {
        Idle,
        Moving,
        Arrived,
        RepathPending,
        NoProgress,
        Stuck,
        Failed
    }

    public enum NavigationPathStatus
    {
        NoDestination,
        Pending,
        Complete,
        Partial,
        Invalid,
        Stale,
        AgentUnavailable,
        AgentNotOnNavMesh
    }
}
