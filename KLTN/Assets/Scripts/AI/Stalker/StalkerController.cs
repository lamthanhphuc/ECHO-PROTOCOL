using UnityEngine;
using UnityEngine.AI;

namespace EchoProtocol.AI.Stalker
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class StalkerController : MonoBehaviour
    {
        [SerializeField] private PatrolRoute patrolRoute;

        private NavMeshAgent _agent;
        private StalkerState _currentState = StalkerState.PATROL;
        private int _currentPatrolIndex;
        private Vector3? _activeDestination;

        public StalkerState CurrentState => _currentState;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
        }

        private void OnEnable()
        {
            if (_agent == null)
            {
                _agent = GetComponent<NavMeshAgent>();
            }

            if (_currentState == StalkerState.PATROL)
            {
                SetCurrentPatrolDestination();
            }
        }

        private void Update()
        {
            if (_currentState != StalkerState.PATROL)
            {
                return;
            }

            TickPatrol();
        }

        private void TickPatrol()
        {
            if (!CanUseAgent() || patrolRoute == null || patrolRoute.PointCount == 0)
            {
                return;
            }

            if (!_activeDestination.HasValue)
            {
                SetCurrentPatrolDestination();
                return;
            }

            if (_agent.pathPending)
            {
                return;
            }

            if (_agent.remainingDistance > _agent.stoppingDistance)
            {
                return;
            }

            AdvancePatrolDestination();
        }

        private void AdvancePatrolDestination()
        {
            _currentPatrolIndex++;
            SetCurrentPatrolDestination();
        }

        private void SetCurrentPatrolDestination()
        {
            if (!CanUseAgent() || patrolRoute == null)
            {
                _activeDestination = null;
                return;
            }

            if (!patrolRoute.TryGetNextValidPoint(_currentPatrolIndex, out var pointIndex, out var point))
            {
                _activeDestination = null;
                return;
            }

            _currentPatrolIndex = pointIndex;
            var destination = point.position;

            if (_activeDestination.HasValue && _activeDestination.Value == destination)
            {
                return;
            }

            if (_agent.SetDestination(destination))
            {
                _activeDestination = destination;
            }
        }

        private bool CanUseAgent()
        {
            return _agent != null
                && _agent.enabled
                && _agent.isOnNavMesh;
        }
    }
}
