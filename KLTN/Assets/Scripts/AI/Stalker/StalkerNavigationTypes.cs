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
