using UnityEngine;
using UnityEngine.AI;

namespace EchoProtocol.AI.Stalker
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class StalkerController : MonoBehaviour
    {
        [SerializeField] private PatrolRoute patrolRoute;
        [SerializeField] private StalkerVisionSensor visionSensor;

        [Header("Detection Spike Defaults")]
        [SerializeField] private float detectionMeterFull = 1f;
        [SerializeField] private float detectionFillRate = 0.5f;
        [SerializeField] private float detectionDecayRate = 0.5f;

        [Header("Search Spike Defaults")]
        [SerializeField] private float searchDuration = 5f;

        [Header("Debug Runtime")]
        [SerializeField] private StalkerState currentState = StalkerState.PATROL;
        [SerializeField] private float detectionMeter;
        [SerializeField] private Transform detectionTarget;
        [SerializeField] private Transform currentTarget;
        [SerializeField] private Vector3 lastKnownPosition;
        [SerializeField] private float searchElapsedTime;

        private NavMeshAgent _agent;
        private int _currentPatrolIndex;
        private Vector3? _activeDestination;

        public StalkerState CurrentState => currentState;
        public float DetectionMeter => detectionMeter;
        public Transform DetectionTarget => detectionTarget;
        public Transform CurrentTarget => currentTarget;
        public Vector3 LastKnownPosition => lastKnownPosition;
        public float SearchElapsedTime => searchElapsedTime;

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

            if (currentState == StalkerState.PATROL)
            {
                SetCurrentPatrolDestination();
            }
        }

        private void Update()
        {
            switch (currentState)
            {
                case StalkerState.PATROL:
                    TickPatrol();
                    TryAcquireDetectionTargetFromPatrol();
                    break;
                case StalkerState.DETECT:
                    TickDetect();
                    break;
                case StalkerState.CHASE:
                    TickChase();
                    break;
                case StalkerState.SEARCH:
                    TickSearch();
                    break;
            }
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

        private void TryAcquireDetectionTargetFromPatrol()
        {
            if (!TryGetVisibleSpikeCandidate(out var visibleCandidate))
            {
                return;
            }

            detectionTarget = visibleCandidate;
            detectionMeter = 0f;
            currentState = StalkerState.DETECT;
            StopAgentPath();
        }

        private void TickDetect()
        {
            if (detectionTarget == null)
            {
                ClearDetectionContext();
                currentState = StalkerState.PATROL;
                SetCurrentPatrolDestination();
                return;
            }

            if (TryGetVisibleDetectionTargetObservation(out var observedPosition))
            {
                detectionMeter += GetDetectionFillRate() * Time.deltaTime;
                detectionMeter = ClampDetectionMeter(detectionMeter);

                if (detectionMeter >= GetDetectionMeterFull())
                {
                    PromoteDetectionTargetToCurrentTarget(observedPosition);
                }

                return;
            }

            detectionMeter -= GetDetectionDecayRate() * Time.deltaTime;
            detectionMeter = ClampDetectionMeter(detectionMeter);

            if (detectionMeter <= 0f)
            {
                ClearDetectionContext();
                currentState = StalkerState.PATROL;
                SetCurrentPatrolDestination();
            }
        }

        private bool TryGetVisibleSpikeCandidate(out Transform visibleCandidate)
        {
            visibleCandidate = null;

            if (visionSensor == null || visionSensor.Candidate == null)
            {
                return false;
            }

            if (!visionSensor.RefreshVisibility())
            {
                return false;
            }

            visibleCandidate = visionSensor.Candidate;
            return visibleCandidate != null;
        }

        private bool TryGetVisibleDetectionTargetObservation(out Vector3 observedPosition)
        {
            observedPosition = default;

            if (visionSensor == null || visionSensor.Candidate != detectionTarget)
            {
                return false;
            }

            if (!visionSensor.RefreshVisibility())
            {
                return false;
            }

            observedPosition = visionSensor.LastObservedPosition;
            return true;
        }

        private void PromoteDetectionTargetToCurrentTarget(Vector3 observedPosition)
        {
            currentTarget = detectionTarget;
            lastKnownPosition = observedPosition;
            detectionTarget = null;
            detectionMeter = 0f;
            currentState = StalkerState.CHASE;
            StopAgentPath();
        }

        private void TickChase()
        {
            if (currentTarget == null)
            {
                ClearTargetContext();
                currentState = StalkerState.PATROL;
                SetCurrentPatrolDestination();
                return;
            }

            if (!TryGetVisibleCurrentTargetObservation(out var observedPosition))
            {
                EnterSearch();
                return;
            }

            lastKnownPosition = observedPosition;
            SetChaseDestination(observedPosition);
        }

        private void EnterSearch()
        {
            currentState = StalkerState.SEARCH;
            searchElapsedTime = 0f;
            SetSearchDestination();
        }

        private void TickSearch()
        {
            if (currentTarget == null)
            {
                ClearSearchContext();
                currentState = StalkerState.PATROL;
                StopAgentPath();
                SetCurrentPatrolDestination();
                return;
            }

            if (TryGetVisibleCurrentTargetObservation(out var observedPosition))
            {
                lastKnownPosition = observedPosition;
                ClearSearchRuntimeContext();
                currentState = StalkerState.CHASE;
                SetChaseDestination(observedPosition);
                return;
            }

            SetSearchDestination();

            searchElapsedTime += Time.deltaTime;
            if (searchElapsedTime < GetSearchDuration())
            {
                return;
            }

            ClearSearchContext();
            currentState = StalkerState.PATROL;
            StopAgentPath();
            SetCurrentPatrolDestination();
        }

        private bool TryGetVisibleCurrentTargetObservation(out Vector3 observedPosition)
        {
            observedPosition = default;

            if (visionSensor == null || visionSensor.Candidate != currentTarget)
            {
                return false;
            }

            if (!visionSensor.RefreshVisibility())
            {
                return false;
            }

            observedPosition = visionSensor.LastObservedPosition;
            return true;
        }

        private void SetChaseDestination(Vector3 observedPosition)
        {
            if (!CanUseAgent())
            {
                _activeDestination = null;
                return;
            }

            if (_activeDestination.HasValue && _activeDestination.Value == observedPosition)
            {
                return;
            }

            if (_agent.SetDestination(observedPosition))
            {
                _activeDestination = observedPosition;
            }
        }

        private void SetSearchDestination()
        {
            if (!CanUseAgent())
            {
                _activeDestination = null;
                return;
            }

            if (_activeDestination.HasValue && _activeDestination.Value == lastKnownPosition)
            {
                return;
            }

            if (_agent.SetDestination(lastKnownPosition))
            {
                _activeDestination = lastKnownPosition;
            }
        }

        private void ClearDetectionContext()
        {
            detectionTarget = null;
            detectionMeter = 0f;
        }

        private void ClearTargetContext()
        {
            currentTarget = null;
            ClearDetectionContext();
        }

        private void ClearSearchContext()
        {
            currentTarget = null;
            ClearDetectionContext();
            ClearSearchRuntimeContext();
        }

        private void ClearSearchRuntimeContext()
        {
            searchElapsedTime = 0f;
        }

        private float ClampDetectionMeter(float value)
        {
            return Mathf.Clamp(value, 0f, GetDetectionMeterFull());
        }

        private float GetDetectionMeterFull()
        {
            return Mathf.Max(0.0001f, detectionMeterFull);
        }

        private float GetDetectionFillRate()
        {
            return Mathf.Max(0f, detectionFillRate);
        }

        private float GetDetectionDecayRate()
        {
            return Mathf.Max(0f, detectionDecayRate);
        }

        private float GetSearchDuration()
        {
            return Mathf.Max(0f, searchDuration);
        }

        private void StopAgentPath()
        {
            if (CanUseAgent())
            {
                _agent.ResetPath();
            }

            _activeDestination = null;
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
