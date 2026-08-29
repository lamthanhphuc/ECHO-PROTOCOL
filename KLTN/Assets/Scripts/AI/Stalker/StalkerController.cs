using System.Collections.Generic;
using EchoProtocol.AI.Common;
using EchoProtocol.AI.Stalker.Spatial;
using UnityEngine;
using UnityEngine.AI;

namespace EchoProtocol.AI.Stalker
{
    public enum StalkerAttackResult
    {
        None,
        Hit,
        Miss
    }

    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class StalkerController : MonoBehaviour
    {
        [SerializeField] private PatrolRoute patrolRoute;
        [SerializeField] private StalkerVisionSensor visionSensor;

        [Header("Patrol Mode")]
        [SerializeField] private StalkerPatrolMode patrolMode = StalkerPatrolMode.FixedWaypoint;

        [Header("Dynamic Patrol Spike Defaults")]
        [SerializeField] private int candidateBfsDepth = 3;
        [SerializeField] private float stalenessHorizon = 15f;
        [SerializeField] private float stalenessWeight = 1f;
        [SerializeField] private float connectivityWeight = 0.15f;
        [SerializeField] private float immediateBacktrackPenalty = 0.75f;

        [Header("Detection Spike Defaults")]
        [SerializeField] private float detectionMeterFull = 1f;
        [SerializeField] private float detectionFillRate = 0.5f;
        [SerializeField] private float detectionDecayRate = 0.5f;

        [Header("Search Spike Defaults")]
        [SerializeField] private float searchDuration = 5f;

        [Header("Chase Navigation Defaults")]
        [SerializeField] private float chaseDestinationRefreshDistance = 0.5f;
        [SerializeField] private float chaseDestinationRefreshInterval = 0.5f;

        [Header("Attack Spike Defaults")]
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private float attackWindup = 0.75f;
        [SerializeField] private float attackRecovery = 1f;

        [Header("Debug Runtime")]
        [SerializeField] private StalkerState currentState = StalkerState.PATROL;
        [SerializeField] private float detectionMeter;
        [SerializeField] private Transform detectionTarget;
        [SerializeField] private Transform currentTarget;
        [SerializeField] private Vector3 lastKnownPosition;
        [SerializeField] private float searchElapsedTime;
        [SerializeField] private float attackElapsedTime;
        [SerializeField] private StalkerAttackResult lastAttackResult;
        [SerializeField] private float recoverElapsedTime;

        [Header("Dynamic Patrol Debug Runtime")]
        [SerializeField] private int dynamicCurrentSpatialNodeId = -1;
        [SerializeField] private int dynamicDestinationSpatialNodeId = -1;
        [SerializeField] private int dynamicPreviousSpatialNodeId = -1;
        [SerializeField] private float lastPatrolScore;
        [SerializeField] private int plannerRunCount;
        [SerializeField] private int candidateCount;

        private const float TargetSelectionTieEpsilon = 0f;

        private readonly StalkerMemory _memory = new StalkerMemory();
        private readonly StalkerBlackboard _blackboard = new StalkerBlackboard();
        private StalkerNavigationController _navigation;
        private NavMeshSpatialGraph _spatialPatrolGraph;
        private SpatialPatrolMemory _spatialPatrolMemory;
        private SpatialPatrolPlanner _spatialPatrolPlanner;
        private int _currentPatrolIndex;
        private bool _spatialPatrolInitializationAttempted;
        private bool _dynamicPatrolFallbackActive;
        private bool _hasLastChaseRequestedDestination;
        private Vector3 _lastChaseRequestedDestination;
        private float _chaseDestinationRefreshElapsed;
        private bool _navigationRecoveryAttemptUsed;
        private int _fixedPatrolFallbackFailureCount;
        private bool _isSimulating;
        private float _currentSimulationDeltaSeconds;
        private double _currentSimulationSeconds;
        private long _legacySimulationTick;
        private IReadOnlyList<StalkerTargetCandidate> _currentVisibleTargetCandidates;
        private IReadOnlyList<StalkerTargetStatus> _currentTargetStatuses;

        public StalkerPatrolMode PatrolMode => patrolMode;
        public StalkerState CurrentState => currentState;
        public float DetectionMeter => detectionMeter;
        public Transform DetectionTarget => detectionTarget;
        public Transform CurrentTarget => currentTarget;
        public Vector3 LastKnownPosition => lastKnownPosition;
        public float SearchElapsedTime => searchElapsedTime;
        public float AttackElapsedTime => attackElapsedTime;
        public StalkerAttackResult LastAttackResult => lastAttackResult;
        public float RecoverElapsedTime => recoverElapsedTime;
        public StalkerBlackboard Blackboard => _blackboard;
        public int DynamicCurrentSpatialNodeId => dynamicCurrentSpatialNodeId;
        public int DynamicDestinationSpatialNodeId => dynamicDestinationSpatialNodeId;
        public int DynamicPreviousSpatialNodeId => dynamicPreviousSpatialNodeId;
        public float LastPatrolScore => lastPatrolScore;
        public int PlannerRunCount => plannerRunCount;
        public int CandidateCount => candidateCount;

        private void Awake()
        {
            InitializeNavigation();
        }

        private void OnEnable()
        {
            InitializeNavigation();

            if (currentState == StalkerState.PATROL && patrolMode != StalkerPatrolMode.DynamicSpatial)
            {
                SetCurrentPatrolDestination();
            }
        }

        private void Update()
        {
            // Temporary migration facade for spike scenes until authoritative runtime drives Simulate.
            var legacyStep = new AiSimulationStep(
                new AiSimulationTime(_legacySimulationTick, Time.time),
                Time.deltaTime);
            _legacySimulationTick++;

            Simulate(new StalkerSimulationInput(
                legacyStep,
                null));
        }

        public bool Simulate(StalkerSimulationInput input)
        {
            if (!input.Step.IsValid || _isSimulating)
            {
                return false;
            }

            _isSimulating = true;
            _currentSimulationDeltaSeconds = input.Step.DeltaSeconds;
            _currentSimulationSeconds = input.Step.Time.Seconds;
            _currentVisibleTargetCandidates = input.VisibleTargetCandidates;
            _currentTargetStatuses = input.TargetStatuses;

            try
            {
                TickCurrentState();
                _navigation?.TickProgress(CurrentSimulationDeltaSeconds);
                TickNavigationRecovery();
                TickNavigationFallback();
                return true;
            }
            finally
            {
                _currentVisibleTargetCandidates = null;
                _currentTargetStatuses = null;
                _currentSimulationDeltaSeconds = 0f;
                _currentSimulationSeconds = 0d;
                _isSimulating = false;
            }
        }

        private void TickCurrentState()
        {
            switch (currentState)
            {
                case StalkerState.PATROL:
                    TickPatrol();
                    if (HasTypedTargetFrame)
                    {
                        TryAcquireTypedDetectionTargetFromVisibleFrame();
                    }
                    else
                    {
                        TryAcquireDetectionTargetFromPatrol();
                    }
                    break;
                case StalkerState.DETECT:
                    TickDetect();
                    break;
                case StalkerState.CHASE:
                    TickChase();
                    break;
                case StalkerState.ATTACK:
                    TickAttack();
                    break;
                case StalkerState.RECOVER:
                    TickRecover();
                    break;
                case StalkerState.SEARCH:
                    TickSearch();
                    break;
            }
        }

        private void TickPatrol()
        {
            if (patrolMode == StalkerPatrolMode.DynamicSpatial)
            {
                TickDynamicSpatialPatrol();
                return;
            }

            TickFixedWaypointPatrol();
        }

        private void TickFixedWaypointPatrol()
        {
            if (!CanUseNavigation() || patrolRoute == null || patrolRoute.PointCount == 0)
            {
                return;
            }

            if (!_navigation.HasActiveDestination)
            {
                SetCurrentFixedPatrolDestination();
                return;
            }

            if (!_navigation.HasArrived())
            {
                return;
            }

            AdvancePatrolDestination();
        }

        private void TickDynamicSpatialPatrol()
        {
            if (_dynamicPatrolFallbackActive)
            {
                TickFixedWaypointPatrol();
                return;
            }

            if (!CanUseNavigation())
            {
                _navigation?.ClearDestinationCache();
                return;
            }

            if (!EnsureSpatialPatrolInitialized())
            {
                ActivateDynamicPatrolFallback();
                return;
            }

            if (!_navigation.HasActiveDestination)
            {
                if (!SetDynamicSpatialPatrolDestination())
                {
                    ActivateDynamicPatrolFallback();
                }

                return;
            }

            if (!_navigation.HasArrived())
            {
                return;
            }

            MarkDynamicSpatialDestinationReached();
            if (!SetDynamicSpatialPatrolDestination())
            {
                ActivateDynamicPatrolFallback();
            }
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

        private bool TryAcquireTypedDetectionTargetFromVisibleFrame()
        {
            if (_currentVisibleTargetCandidates == null)
            {
                return false;
            }

            if (!StalkerTargetSelector.TrySelectNearestEligibleVisible(
                    _currentVisibleTargetCandidates,
                    TargetSelectionTieEpsilon,
                    out var selectedObservation))
            {
                return false;
            }

            _memory.SetDetectionTarget(selectedObservation.PlayerId);
            if (!_memory.TryAcceptDetectionTargetObservation(selectedObservation))
            {
                ClearDetectionContext();
                currentState = StalkerState.PATROL;
                SetCurrentPatrolDestination();
                return false;
            }

            detectionTarget = null;
            currentTarget = null;
            detectionMeter = 0f;
            currentState = StalkerState.DETECT;
            StopAgentPath();
            return true;
        }

        private void TickDetect()
        {
            if (HasTypedTargetFrame)
            {
                TickDetectTyped();
                return;
            }

            if (detectionTarget == null)
            {
                ClearDetectionContext();
                currentState = StalkerState.PATROL;
                SetCurrentPatrolDestination();
                return;
            }

            if (TryGetVisibleDetectionTargetObservation(out var observedPosition))
            {
                detectionMeter += GetDetectionFillRate() * CurrentSimulationDeltaSeconds;
                detectionMeter = ClampDetectionMeter(detectionMeter);

                if (detectionMeter >= GetDetectionMeterFull())
                {
                    PromoteDetectionTargetToCurrentTarget(observedPosition);
                }

                return;
            }

            detectionMeter -= GetDetectionDecayRate() * CurrentSimulationDeltaSeconds;
            detectionMeter = ClampDetectionMeter(detectionMeter);

            if (detectionMeter <= 0f)
            {
                ClearDetectionContext();
                currentState = StalkerState.PATROL;
                SetCurrentPatrolDestination();
            }
        }

        private void TickDetectTyped()
        {
            var detectionTargetId = _memory.DetectionTargetId;
            if (!detectionTargetId.IsValid)
            {
                InvalidateDetectionTarget();
                return;
            }

            if (!TryGetUniqueTargetStatus(detectionTargetId, out var status))
            {
                InvalidateDetectionTarget();
                return;
            }

            if (!status.Eligible)
            {
                ClearDetectionContext();
                if (!TryAcquireTypedDetectionTargetFromVisibleFrame())
                {
                    currentState = StalkerState.PATROL;
                    SetCurrentPatrolDestination();
                }

                return;
            }

            if (TryGetUniqueVisibleTargetCandidate(detectionTargetId, out var candidate, out var hasDuplicate))
            {
                if (!candidate.Eligibility.Eligible)
                {
                    InvalidateDetectionTarget();
                    return;
                }

                var observation = candidate.Observation;
                if (!_memory.TryAcceptDetectionTargetObservation(observation))
                {
                    InvalidateDetectionTarget();
                    return;
                }

                detectionMeter += GetDetectionFillRate() * CurrentSimulationDeltaSeconds;
                detectionMeter = ClampDetectionMeter(detectionMeter);
                _memory.SetDetectionMeter(detectionMeter);

                if (detectionMeter >= GetDetectionMeterFull())
                {
                    PromoteDetectionTargetToCurrentTarget(observation);
                }

                return;
            }

            if (hasDuplicate)
            {
                InvalidateDetectionTarget();
                return;
            }

            detectionMeter -= GetDetectionDecayRate() * CurrentSimulationDeltaSeconds;
            detectionMeter = ClampDetectionMeter(detectionMeter);
            _memory.SetDetectionMeter(detectionMeter);

            if (detectionMeter <= 0f)
            {
                InvalidateDetectionTarget();
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

        private void PromoteDetectionTargetToCurrentTarget(VisionObservation observation)
        {
            _memory.SetCurrentTarget(observation.PlayerId);
            _memory.TryAcceptCurrentTargetObservation(observation);
            _memory.ClearDetectionTarget();
            currentTarget = null;
            detectionTarget = null;
            detectionMeter = 0f;
            lastKnownPosition = _memory.LastKnownPosition;
            currentState = StalkerState.CHASE;
            StopAgentPath();
        }

        private void TickChase()
        {
            if (HasTypedTargetFrame)
            {
                TickChaseTyped();
                return;
            }

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
            if (IsWithinAttackRange(observedPosition))
            {
                EnterAttack();
                return;
            }

            SetChaseDestination(observedPosition);
        }

        private void TickChaseTyped()
        {
            var currentTargetId = _memory.CurrentTargetId;
            if (!currentTargetId.IsValid)
            {
                InvalidateCurrentTarget();
                return;
            }

            if (!TryGetUniqueTargetStatus(currentTargetId, out var status))
            {
                InvalidateCurrentTarget();
                return;
            }

            if (!status.Eligible)
            {
                ClearTargetContext();
                if (!TryAcquireTypedDetectionTargetFromVisibleFrame())
                {
                    currentState = StalkerState.PATROL;
                    SetCurrentPatrolDestination();
                }

                return;
            }

            if (TryGetUniqueVisibleTargetCandidate(currentTargetId, out var candidate, out var hasDuplicate))
            {
                if (!candidate.Eligibility.Eligible)
                {
                    InvalidateCurrentTarget();
                    return;
                }

                var observation = candidate.Observation;
                if (!_memory.TryAcceptCurrentTargetObservation(observation))
                {
                    InvalidateCurrentTarget();
                    return;
                }

                lastKnownPosition = _memory.LastKnownPosition;
                if (IsWithinAttackRange(observation.ObservedPosition))
                {
                    EnterAttack();
                    return;
                }

                SetChaseDestination(observation.ObservedPosition);
                return;
            }

            if (hasDuplicate)
            {
                InvalidateCurrentTarget();
                return;
            }

            EnterSearch();
        }

        private void EnterAttack()
        {
            currentState = StalkerState.ATTACK;
            attackElapsedTime = 0f;
            lastAttackResult = StalkerAttackResult.None;
            StopAgentPath();
        }

        private void TickAttack()
        {
            if (currentTarget == null)
            {
                lastAttackResult = StalkerAttackResult.Miss;
                EnterRecover();
                return;
            }

            attackElapsedTime += CurrentSimulationDeltaSeconds;
            if (attackElapsedTime < GetAttackWindup())
            {
                return;
            }

            ResolveAttackHitMoment();
            EnterRecover();
        }

        private void ResolveAttackHitMoment()
        {
            if (currentTarget == null || !IsWithinAttackRange(currentTarget.position))
            {
                lastAttackResult = StalkerAttackResult.Miss;
                return;
            }

            lastAttackResult = StalkerAttackResult.Hit;
        }

        private void EnterRecover()
        {
            currentState = StalkerState.RECOVER;
            recoverElapsedTime = 0f;
            StopAgentPath();
        }

        private void TickRecover()
        {
            recoverElapsedTime += CurrentSimulationDeltaSeconds;
            if (recoverElapsedTime < GetAttackRecovery())
            {
                return;
            }

            attackElapsedTime = 0f;
            recoverElapsedTime = 0f;

            if (currentTarget == null)
            {
                ClearTargetContext();
                currentState = StalkerState.PATROL;
                SetCurrentPatrolDestination();
                return;
            }

            if (TryGetVisibleCurrentTargetObservation(out var observedPosition))
            {
                lastKnownPosition = observedPosition;
                ResetChaseDestinationTracking();
                ResetNavigationRecoveryBudget();
                currentState = StalkerState.CHASE;
                SetChaseDestination(observedPosition);
                return;
            }

            EnterSearch();
        }

        private void EnterSearch()
        {
            ResetChaseDestinationTracking();
            ResetNavigationRecoveryBudget();
            if (HasTypedTargetFrame && !_memory.HasLastKnownPosition)
            {
                InvalidateCurrentTarget();
                return;
            }

            currentState = StalkerState.SEARCH;
            searchElapsedTime = 0f;
            if (HasTypedTargetFrame)
            {
                SetSearchDestination(_memory.LastKnownPosition);
                return;
            }

            SetSearchDestination();
        }

        private void TickSearch()
        {
            if (HasTypedTargetFrame)
            {
                TickSearchTyped();
                return;
            }

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
                ResetChaseDestinationTracking();
                ResetNavigationRecoveryBudget();
                currentState = StalkerState.CHASE;
                SetChaseDestination(observedPosition);
                return;
            }

            SetSearchDestination();

            searchElapsedTime += CurrentSimulationDeltaSeconds;
            if (searchElapsedTime < GetSearchDuration())
            {
                return;
            }

            ClearSearchContext();
            currentState = StalkerState.PATROL;
            StopAgentPath();
            SetCurrentPatrolDestination();
        }

        private void TickSearchTyped()
        {
            var currentTargetId = _memory.CurrentTargetId;
            if (!currentTargetId.IsValid)
            {
                InvalidateCurrentTarget();
                return;
            }

            if (!TryGetUniqueTargetStatus(currentTargetId, out var status) || !status.Eligible)
            {
                InvalidateCurrentTarget();
                return;
            }

            if (TryGetUniqueVisibleTargetCandidate(currentTargetId, out var candidate, out var hasDuplicate))
            {
                if (!candidate.Eligibility.Eligible)
                {
                    InvalidateCurrentTarget();
                    return;
                }

                var observation = candidate.Observation;
                if (!_memory.TryAcceptCurrentTargetObservation(observation))
                {
                    InvalidateCurrentTarget();
                    return;
                }

                lastKnownPosition = _memory.LastKnownPosition;
                ClearSearchRuntimeContext();
                ResetChaseDestinationTracking();
                ResetNavigationRecoveryBudget();
                currentState = StalkerState.CHASE;
                SetChaseDestination(observation.ObservedPosition);
                return;
            }

            if (hasDuplicate || !_memory.HasLastKnownPosition)
            {
                InvalidateCurrentTarget();
                return;
            }

            SetSearchDestination(_memory.LastKnownPosition);

            searchElapsedTime += CurrentSimulationDeltaSeconds;
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
            if (!CanUseNavigation())
            {
                _navigation?.ClearDestinationCache();
                ResetChaseDestinationTracking();
                return;
            }

            _chaseDestinationRefreshElapsed += CurrentSimulationDeltaSeconds;
            if (!ShouldRefreshChaseDestination(observedPosition))
            {
                return;
            }

            var result = _navigation.RequestDestination(
                observedPosition,
                NavigationRequestIntent.TrackMovingGoal);
            if (!result.IsAccepted)
            {
                return;
            }

            _lastChaseRequestedDestination = observedPosition;
            _hasLastChaseRequestedDestination = true;
            _chaseDestinationRefreshElapsed = 0f;
        }

        private void SetSearchDestination()
        {
            SetSearchDestination(lastKnownPosition);
        }

        private void SetSearchDestination(Vector3 rememberedPosition)
        {
            if (!CanUseNavigation())
            {
                _navigation?.ClearDestinationCache();
                return;
            }

            _navigation.TrySetDestination(rememberedPosition);
        }

        private bool IsWithinAttackRange(Vector3 targetPosition)
        {
            var delta = targetPosition - transform.position;
            return delta.sqrMagnitude <= GetAttackRange() * GetAttackRange();
        }

        private void ClearDetectionContext()
        {
            detectionTarget = null;
            detectionMeter = 0f;
            _memory.ClearDetectionTarget();
            if (!_memory.CurrentTargetId.IsValid)
            {
                _memory.ClearCurrentTarget();
            }
        }

        private void ClearTargetContext()
        {
            currentTarget = null;
            _memory.ClearCurrentTarget();
            ResetChaseDestinationTracking();
            ResetNavigationRecoveryBudget();
            ResetFixedPatrolFallbackState();
            ClearDetectionContext();
        }

        private void ClearSearchContext()
        {
            currentTarget = null;
            _memory.ClearCurrentTarget();
            ClearDetectionContext();
            ClearSearchRuntimeContext();
        }

        private void InvalidateDetectionTarget()
        {
            ClearDetectionContext();
            currentState = StalkerState.PATROL;
            SetCurrentPatrolDestination();
        }

        private void InvalidateCurrentTarget()
        {
            ClearTargetContext();
            currentState = StalkerState.PATROL;
            SetCurrentPatrolDestination();
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

        private float GetChaseDestinationRefreshDistance()
        {
            return Mathf.Max(0f, chaseDestinationRefreshDistance);
        }

        private float GetChaseDestinationRefreshInterval()
        {
            return Mathf.Max(0f, chaseDestinationRefreshInterval);
        }

        private float GetAttackRange()
        {
            return Mathf.Max(0f, attackRange);
        }

        private float GetAttackWindup()
        {
            return Mathf.Max(0f, attackWindup);
        }

        private float GetAttackRecovery()
        {
            return Mathf.Max(0f, attackRecovery);
        }

        private float CurrentSimulationDeltaSeconds => _currentSimulationDeltaSeconds;

        private float CurrentSimulationTimeSeconds => (float)_currentSimulationSeconds;

        private bool HasTypedTargetFrame => _currentVisibleTargetCandidates != null || _currentTargetStatuses != null;

        private bool TryGetUniqueTargetStatus(PlayerId playerId, out StalkerTargetEligibilityResult eligibility)
        {
            return StalkerTargetStatusLookup.TryGetUnique(_currentTargetStatuses, playerId, out eligibility);
        }

        private bool TryGetUniqueVisibleTargetCandidate(
            PlayerId playerId,
            out StalkerTargetCandidate candidate,
            out bool hasDuplicate)
        {
            return StalkerTargetCandidateLookup.TryGetUnique(
                _currentVisibleTargetCandidates,
                playerId,
                out candidate,
                out hasDuplicate);
        }

        private void StopAgentPath()
        {
            ResetChaseDestinationTracking();
            ResetNavigationRecoveryBudget();
            ResetFixedPatrolFallbackState();
            _navigation?.Stop();
        }

        private void TickNavigationRecovery()
        {
            if (_navigation == null || !IsLocomotionState())
            {
                return;
            }

            var executionStatus = _navigation.GetExecutionStatus();
            if (executionStatus == NavigationExecutionStatus.Stuck)
            {
                TryIssueNavigationRecoveryRepath();
                return;
            }

            if (executionStatus != NavigationExecutionStatus.Failed)
            {
                return;
            }

            if (_navigation.GetPathStatus() == NavigationPathStatus.Stale)
            {
                TryIssueNavigationRecoveryRepath();
            }
        }

        private void TryIssueNavigationRecoveryRepath()
        {
            if (_navigationRecoveryAttemptUsed)
            {
                return;
            }

            if (!TryGetCurrentNavigationRecoveryDestination(out var destination))
            {
                return;
            }

            _navigationRecoveryAttemptUsed = true;
            _navigation.RequestDestination(destination, NavigationRequestIntent.RecoveryRepath);
        }

        private void TickNavigationFallback()
        {
            if (_navigation == null
                || currentState != StalkerState.PATROL
                || patrolMode != StalkerPatrolMode.FixedWaypoint)
            {
                return;
            }

            if (patrolRoute == null || patrolRoute.PointCount == 0)
            {
                return;
            }

            var executionStatus = _navigation.GetExecutionStatus();
            var pathStatus = _navigation.GetPathStatus();
            var shouldFallback = pathStatus == NavigationPathStatus.Partial
                || (executionStatus == NavigationExecutionStatus.Stuck && _navigationRecoveryAttemptUsed)
                || (pathStatus == NavigationPathStatus.Stale && _navigationRecoveryAttemptUsed);

            if (!shouldFallback)
            {
                return;
            }

            AdvanceFixedPatrolFallbackDestination();
        }

        private bool IsLocomotionState()
        {
            return currentState == StalkerState.PATROL
                || currentState == StalkerState.CHASE
                || currentState == StalkerState.SEARCH;
        }

        private bool TryGetCurrentNavigationRecoveryDestination(out Vector3 destination)
        {
            destination = default;
            switch (currentState)
            {
                case StalkerState.PATROL:
                    return TryGetCurrentPatrolRecoveryDestination(out destination);
                case StalkerState.CHASE:
                    if (!_hasLastChaseRequestedDestination)
                    {
                        return false;
                    }

                    destination = _lastChaseRequestedDestination;
                    return true;
                case StalkerState.SEARCH:
                    destination = lastKnownPosition;
                    return currentTarget != null;
                default:
                    return false;
            }
        }

        private bool TryGetCurrentPatrolRecoveryDestination(out Vector3 destination)
        {
            destination = default;
            if (patrolMode == StalkerPatrolMode.DynamicSpatial
                && _spatialPatrolGraph != null
                && _spatialPatrolGraph.TryGetNode(_blackboard.DestinationSpatialNodeId, out var node))
            {
                destination = node.Position;
                return true;
            }

            if (patrolRoute == null || !patrolRoute.TryGetPoint(_currentPatrolIndex, out var point) || point == null)
            {
                return false;
            }

            destination = point.position;
            return true;
        }

        private bool ShouldRefreshChaseDestination(Vector3 observedPosition)
        {
            if (!_navigation.HasActiveDestination)
            {
                return true;
            }

            if (!_hasLastChaseRequestedDestination)
            {
                return true;
            }

            if (Vector3.Distance(_lastChaseRequestedDestination, observedPosition) >= GetChaseDestinationRefreshDistance())
            {
                return true;
            }

            return _chaseDestinationRefreshElapsed >= GetChaseDestinationRefreshInterval();
        }

        private void ResetChaseDestinationTracking()
        {
            _hasLastChaseRequestedDestination = false;
            _lastChaseRequestedDestination = default;
            _chaseDestinationRefreshElapsed = 0f;
        }

        private void ResetNavigationRecoveryBudget()
        {
            _navigationRecoveryAttemptUsed = false;
        }

        private void ResetFixedPatrolFallbackState()
        {
            _fixedPatrolFallbackFailureCount = 0;
        }

        private void AdvancePatrolDestination()
        {
            ResetFixedPatrolFallbackState();
            _currentPatrolIndex++;
            SetCurrentFixedPatrolDestination();
        }

        private void AdvanceFixedPatrolFallbackDestination()
        {
            if (patrolRoute == null)
            {
                return;
            }

            var pointCount = patrolRoute.PointCount;
            if (pointCount == 0 || _fixedPatrolFallbackFailureCount >= pointCount)
            {
                return;
            }

            _fixedPatrolFallbackFailureCount++;
            if (_fixedPatrolFallbackFailureCount >= pointCount)
            {
                return;
            }

            _currentPatrolIndex++;
            ResetNavigationRecoveryBudget();
            _navigation?.Stop();
        }

        private void SetCurrentPatrolDestination()
        {
            if (patrolMode == StalkerPatrolMode.DynamicSpatial)
            {
                _dynamicPatrolFallbackActive = false;

                if (SetDynamicSpatialPatrolDestination())
                {
                    return;
                }

                ActivateDynamicPatrolFallback();
                return;
            }

            SetCurrentFixedPatrolDestination();
        }

        private void SetCurrentFixedPatrolDestination()
        {
            if (!CanUseNavigation() || patrolRoute == null)
            {
                _navigation?.ClearDestinationCache();
                return;
            }

            if (patrolMode == StalkerPatrolMode.FixedWaypoint
                && patrolRoute.PointCount > 0
                && _fixedPatrolFallbackFailureCount >= patrolRoute.PointCount)
            {
                return;
            }

            if (!patrolRoute.TryGetNextValidPoint(_currentPatrolIndex, out var pointIndex, out var point))
            {
                _navigation.ClearDestinationCache();
                return;
            }

            _currentPatrolIndex = pointIndex;
            var destination = point.position;

            if (patrolMode == StalkerPatrolMode.FixedWaypoint)
            {
                var evaluation = _navigation.EvaluateDestination(destination);
                if (evaluation.Status == NavigationEvaluationStatus.Partial
                    || evaluation.Status == NavigationEvaluationStatus.Invalid
                    || evaluation.Status == NavigationEvaluationStatus.DestinationInvalid)
                {
                    AdvanceFixedPatrolFallbackDestination();
                    return;
                }

                if (!evaluation.IsComplete)
                {
                    return;
                }
            }

            if (_navigation.TrySetDestination(destination))
            {
                ResetNavigationRecoveryBudget();
            }
        }

        private bool EnsureSpatialPatrolInitialized()
        {
            if (_spatialPatrolPlanner != null)
            {
                return true;
            }

            if (_spatialPatrolInitializationAttempted)
            {
                return false;
            }

            _spatialPatrolInitializationAttempted = true;
            _spatialPatrolGraph = NavMeshSpatialGraphBuilder.Build();
            if (_spatialPatrolGraph == null || _spatialPatrolGraph.IsEmpty)
            {
                return false;
            }

            _spatialPatrolMemory = new SpatialPatrolMemory(_spatialPatrolGraph.NodeCount);
            _spatialPatrolPlanner = new SpatialPatrolPlanner(
                _spatialPatrolGraph,
                _spatialPatrolMemory,
                candidateBfsDepth,
                stalenessHorizon,
                stalenessWeight,
                connectivityWeight,
                immediateBacktrackPenalty);

            SyncDynamicPatrolDebugFields();
            return _spatialPatrolPlanner.CanPlan;
        }

        private bool SetDynamicSpatialPatrolDestination()
        {
            if (!EnsureSpatialPatrolInitialized())
            {
                return false;
            }

            if (!_spatialPatrolPlanner.TryResolveNearestNode(transform.position, out var currentNodeId))
            {
                ClearDynamicPatrolDestination();
                return false;
            }

            if (_blackboard.CurrentSpatialNodeId != currentNodeId)
            {
                if (_blackboard.CurrentSpatialNodeId >= 0)
                {
                    _blackboard.PreviousSpatialNodeId = _blackboard.CurrentSpatialNodeId;
                }

                _blackboard.CurrentSpatialNodeId = currentNodeId;
            }

            _spatialPatrolMemory.MarkVisited(currentNodeId, CurrentSimulationTimeSeconds);

            plannerRunCount++;
            if (!_spatialPatrolPlanner.TrySelectDestination(
                currentNodeId,
                _blackboard.PreviousSpatialNodeId,
                CurrentSimulationTimeSeconds,
                out var plan))
            {
                ClearDynamicPatrolDestination();
                return false;
            }

            if (!_navigation.TrySetDestination(plan.DestinationNode.Position))
            {
                ClearDynamicPatrolDestination();
                return false;
            }

            ResetNavigationRecoveryBudget();
            _blackboard.DestinationSpatialNodeId = plan.DestinationNode.Id;
            lastPatrolScore = plan.Score;
            candidateCount = plan.CandidateCount;
            SyncDynamicPatrolDebugFields();
            return true;
        }

        private void MarkDynamicSpatialDestinationReached()
        {
            var destinationNodeId = _blackboard.DestinationSpatialNodeId;
            if (destinationNodeId < 0)
            {
                return;
            }

            _blackboard.PreviousSpatialNodeId = _blackboard.CurrentSpatialNodeId;
            _blackboard.CurrentSpatialNodeId = destinationNodeId;
            _blackboard.DestinationSpatialNodeId = -1;
            _spatialPatrolMemory?.MarkVisited(destinationNodeId, CurrentSimulationTimeSeconds);
            SyncDynamicPatrolDebugFields();
        }

        private void ClearDynamicPatrolDestination()
        {
            _blackboard.DestinationSpatialNodeId = -1;
            lastPatrolScore = 0f;
            candidateCount = 0;
            SyncDynamicPatrolDebugFields();
        }

        private void ActivateDynamicPatrolFallback()
        {
            _dynamicPatrolFallbackActive = true;
            ClearDynamicPatrolDestination();
            TickFixedWaypointPatrol();
        }

        private void SyncDynamicPatrolDebugFields()
        {
            dynamicCurrentSpatialNodeId = _blackboard.CurrentSpatialNodeId;
            dynamicDestinationSpatialNodeId = _blackboard.DestinationSpatialNodeId;
            dynamicPreviousSpatialNodeId = _blackboard.PreviousSpatialNodeId;
        }

        private void InitializeNavigation()
        {
            if (_navigation == null)
            {
                _navigation = new StalkerNavigationController(GetComponent<NavMeshAgent>());
            }
        }

        private bool CanUseNavigation()
        {
            return _navigation != null && _navigation.IsUsable;
        }
    }
}
