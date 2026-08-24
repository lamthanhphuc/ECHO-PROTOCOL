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

        [Header("Debug Runtime")]
        [SerializeField] private StalkerState currentState = StalkerState.PATROL;
        [SerializeField] private float detectionMeter;
        [SerializeField] private Transform detectionTarget;
        [SerializeField] private Transform currentTarget;

        private NavMeshAgent _agent;
        private int _currentPatrolIndex;
        private Vector3? _activeDestination;

        public StalkerState CurrentState => currentState;
        public float DetectionMeter => detectionMeter;
        public Transform DetectionTarget => detectionTarget;
        public Transform CurrentTarget => currentTarget;

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

            if (IsDetectionTargetVisible())
            {
                detectionMeter += GetDetectionFillRate() * Time.deltaTime;
                detectionMeter = ClampDetectionMeter(detectionMeter);

                if (detectionMeter >= GetDetectionMeterFull())
                {
                    PromoteDetectionTargetToCurrentTarget();
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

            if (!visionSensor.TryGetVisibleCandidate(out _))
            {
                return false;
            }

            visibleCandidate = visionSensor.Candidate;
            return visibleCandidate != null;
        }

        private bool IsDetectionTargetVisible()
        {
            if (visionSensor == null || visionSensor.Candidate != detectionTarget)
            {
                return false;
            }

            return visionSensor.TryGetVisibleCandidate(out _);
        }

        private void PromoteDetectionTargetToCurrentTarget()
        {
            currentTarget = detectionTarget;
            detectionTarget = null;
            detectionMeter = 0f;
            currentState = StalkerState.CHASE;
            StopAgentPath();
        }

        private void ClearDetectionContext()
        {
            detectionTarget = null;
            detectionMeter = 0f;
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
