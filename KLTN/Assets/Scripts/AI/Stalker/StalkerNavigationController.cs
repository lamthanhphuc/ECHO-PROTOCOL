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

        public bool TrySetDestination(Vector3 destination)
        {
            if (!IsUsable)
            {
                ClearDestinationCache();
                return false;
            }

            if (_activeDestination.HasValue && _activeDestination.Value == destination)
            {
                return true;
            }

            if (!_agent.SetDestination(destination))
            {
                return false;
            }

            _activeDestination = destination;
            return true;
        }
    }
}
