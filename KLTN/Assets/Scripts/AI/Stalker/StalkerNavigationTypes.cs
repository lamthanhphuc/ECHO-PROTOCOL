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
}
