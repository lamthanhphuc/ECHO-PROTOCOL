using UnityEngine;
using UnityEngine.AI;

namespace EchoProtocol.AI.Stalker
{
    public sealed class StalkerNavigationController
    {
        private readonly NavMeshAgent _agent;
        private Vector3? _activeDestination;

        public StalkerNavigationController(NavMeshAgent agent)
        {
            _agent = agent;
        }

        public bool IsUsable => _agent != null
            && _agent.enabled
            && _agent.isOnNavMesh;

        // This is the controller's accepted destination cache, not proof of a complete Unity path.
        public bool HasActiveDestination => _activeDestination.HasValue;

        public bool HasArrived()
        {
            return IsUsable
                && !_agent.pathPending
                && _agent.remainingDistance <= _agent.stoppingDistance;
        }

        public void ClearDestinationCache()
        {
            _activeDestination = null;
        }

        public void Stop()
        {
            if (IsUsable)
            {
                _agent.ResetPath();
            }

            ClearDestinationCache();
        }

        public NavigationPlanResult RequestDestination(Vector3 destination)
        {
            if (_agent == null || !_agent.enabled)
            {
                ClearDestinationCache();
                return new NavigationPlanResult(NavigationPlanStatus.AgentUnavailable, destination);
            }

            if (!_agent.isOnNavMesh)
            {
                ClearDestinationCache();
                return new NavigationPlanResult(NavigationPlanStatus.AgentNotOnNavMesh, destination);
            }

            if (_activeDestination.HasValue && _activeDestination.Value == destination)
            {
                return new NavigationPlanResult(NavigationPlanStatus.AlreadyActive, destination);
            }

            // SetDestination accepts a request; it does not prove the resulting path is complete.
            if (!_agent.SetDestination(destination))
            {
                ClearDestinationCache();
                return new NavigationPlanResult(NavigationPlanStatus.DestinationRequestFailed, destination);
            }

            _activeDestination = destination;
            return new NavigationPlanResult(NavigationPlanStatus.Accepted, destination);
        }

        public bool TrySetDestination(Vector3 destination)
        {
            return RequestDestination(destination).IsAccepted;
        }

        public NavigationExecutionStatus GetExecutionStatus()
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
            {
                return NavigationExecutionStatus.Failed;
            }

            if (!HasActiveDestination)
            {
                return NavigationExecutionStatus.Idle;
            }

            if (_agent.pathPending)
            {
                return NavigationExecutionStatus.RepathPending;
            }

            if (HasArrived())
            {
                return NavigationExecutionStatus.Arrived;
            }

            return NavigationExecutionStatus.Moving;
        }
    }
}
