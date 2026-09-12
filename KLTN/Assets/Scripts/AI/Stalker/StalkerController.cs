using System.Collections.Generic;
using EchoProtocol.AI.Common;
using EchoProtocol.AI.Common.Spatial;
using EchoProtocol.AI.Listener.Perception;
using EchoProtocol.AI.Stalker.Hearing;
using EchoProtocol.AI.Stalker.Spatial;
using EchoProtocol.AI.Stalker.Telemetry;
using EchoProtocol.Networking;
using Fusion;
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

    internal enum RoomSweepCurrentRoomResult
    {
        NotApplicable,
        DestinationSet,
        RoomCleared,
        Exhausted,
        Failed
    }

    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class StalkerController : MonoBehaviour
    {
        [SerializeField] private PatrolRoute patrolRoute;
        [SerializeField] private StalkerVisionSensor visionSensor;

        [Header("Patrol Mode")]
        [SerializeField] private StalkerPatrolMode patrolMode = StalkerPatrolMode.FixedWaypoint;

        [Header("Movement Speed")]
        [SerializeField, Min(0f)] private float patrolSpeed = 7f;
        [SerializeField, Min(0f)] private float chaseSpeed = 9f;

        [Header("Diagnostics")]
        [SerializeField] private bool enableDiagnostics;

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
        [SerializeField] private float searchRadius = 8f;

        [Header("Chase Navigation Defaults")]
        [SerializeField] private float chaseDestinationRefreshDistance = 0.5f;
        [SerializeField] private float chaseDestinationRefreshInterval = 0.5f;
        [SerializeField, Min(0f)] private float chasePredictionLeadSeconds = 0.2f;
        [SerializeField, Min(0f)] private float chasePredictionMaxDistance = 1.5f;

        [Header("Attack Spike Defaults")]
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private float attackWindup = 0.75f;
        [SerializeField] private float attackRecovery = 1f;
        [SerializeField] private float attackDamage = 100f;

        [Header("World Interaction")]
        [SerializeField, Min(0.1f)] private float worldInteractionDistance = 1.75f;
        [SerializeField, Min(0.01f)] private float stalkerDoorBreakDurationSeconds = 3f;
        [SerializeField, Min(0.01f)] private float worldInteractionProbeRadius = 0.35f;

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
        [SerializeField] private RegionGraphFallbackReason regionGraphFallbackReason;
        [SerializeField] private int canonicalCurrentRegionId;
        [SerializeField] private int canonicalObjectiveRegionId;
        [SerializeField] private int canonicalNextRegionId;
        [SerializeField] private long searchEpisodeId;
        [SerializeField] private int searchCandidateNodeId = -1;

        private const float TargetSelectionTieEpsilon = 0f;
        private const float TopologyPathSegmentSampleSpacing = 1f;
        private const int MaxTopologyPathSegmentSamples = 8;
        private const float EmergencyNavMeshRecoverySampleDistance = 2f;

        private readonly StalkerMemory _memory =
            new StalkerMemory();

        private readonly StalkerHearingMemory _hearingMemory =
            new StalkerHearingMemory();

        private readonly StalkerHearingSelector _hearingSelector =
            new StalkerHearingSelector();

        private readonly StalkerAttackController _attackController =
            new StalkerAttackController();

        private readonly StalkerBlackboard _blackboard =
            new StalkerBlackboard();
        private StalkerNavigationController _navigation;
        private NavMeshSpatialGraph _spatialPatrolGraph;
        [SerializeField] private RegionGraphAsset regionGraphAsset;
        private SpatialPatrolMemory _spatialPatrolMemory;
        private SpatialPatrolPlanner _spatialPatrolPlanner;
        private CoverageMemory _coverageMemory;
        private RegionGraph _regionGraph;
        private GlobalPatrolPlanner _globalPatrolPlanner;
        private LocalPatrolSelector _localPatrolSelector;
        private RoomSweepCoverageMemory _roomSweepCoverageMemory;
        private RoomSweepPlanner _roomSweepPlanner;
        private RoomSweepGlobalPlanner _roomSweepGlobalPlanner;
        private bool _hasPatrolVariationSeed;
        private int _patrolVariationSeed;
        private int _patrolNearOptimalHopSlack;
        private StalkerSearchPlanner _searchPlanner;
        private StalkerSearchContext _searchContext;
        private readonly StalkerWorldInteractionDriver _worldInteractionDriver = new StalkerWorldInteractionDriver();
        private int _currentPatrolIndex;
        private bool _spatialPatrolInitializationAttempted;
        private bool _dynamicPatrolFallbackActive;
        private bool _canonicalPatrolFallbackActive;
        private bool _roomSweepPatrolFallbackActive;
        private RegionId _currentRegionId = RegionId.Invalid;
        private RegionId _previousRegionId = RegionId.Invalid;
        private long _searchEpisodeSequence;
        private bool _hasLastChaseRequestedDestination;
        private Vector3 _lastChaseRequestedDestination;
        private float _chaseDestinationRefreshElapsed;
        private bool _hasPreviousChaseObservation;
        private Vector3 _previousChaseObservedPosition;
        private float _previousChaseObservationTimeSeconds;
        private bool _navigationRecoveryAttemptUsed;
        private StalkerNavigationObjectiveKey _navigationObjectiveKey;
        private readonly HashSet<int> _rejectedDynamicPatrolNodeIds = new HashSet<int>();
        private readonly HashSet<int> _rejectedCanonicalLocalNodeIds = new HashSet<int>();
        private readonly HashSet<RegionId> _rejectedCanonicalGlobalRegionIds = new HashSet<RegionId>();
        private readonly HashSet<int> _rejectedRoomSweepTransitNodeIds = new HashSet<int>();
        private readonly HashSet<RegionId> _rejectedRoomSweepGlobalRegionIds = new HashSet<RegionId>();
        private int _fixedPatrolFallbackFailureCount;
        private bool _searchCandidatePlanningExhausted;
        private bool _isSimulating;
        private float _currentSimulationDeltaSeconds;
        private double _currentSimulationSeconds;
        private AiSimulationStep _currentSimulationStep;
        private StalkerAttackTargetSnapshot? _currentAttackTargetSnapshot;
        private StalkerSearchEndedFact _lastCommittedSearchEndedFact;
        private SearchEpisodeId _lastCommittedSearchEpisodeId;
        private long _legacySimulationTick;
        private IReadOnlyList<StalkerTargetCandidate> _currentVisibleTargetCandidates;
        private IReadOnlyList<StalkerTargetStatus> _currentTargetStatuses;
        private IReadOnlyList<HearingObservation>
            _currentHearingObservations;
        private System.DateTime
            _currentHearingEvaluationTimeUtc;
        private bool _roomSweepSuppressLegacyGateLogged;
        private bool _roomSweepNavigationGateUsableLogged;
        private bool _roomSweepNavigationGateBlockedLogged;
        private bool _roomSweepSelfProbeScanActive;
        private int _roomSweepSelfProbeScanNodeId = -1;
        private RegionId _roomSweepSelfProbeScanRegionId = RegionId.Invalid;
        private float _roomSweepSelfProbeScanAccumulatedDegrees;
        private bool _roomSweepSelfProbeScanHasAgentRotationOwnership;
        private bool _roomSweepSelfProbeScanPreviousAgentUpdateRotation;
        private readonly HashSet<int> _roomSweepResidualDiagLoggedRegionIds = new HashSet<int>();

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
        public RegionGraphFallbackReason RegionGraphFallbackReason => regionGraphFallbackReason;
        public SearchEpisodeId ActiveSearchEpisodeId => _searchContext?.EpisodeId ?? SearchEpisodeId.Invalid;
        public PlayerId DetectionTargetId => _memory.DetectionTargetId;
        public PlayerId CurrentTargetId => _memory.CurrentTargetId;
        public StalkerAttackEpisodeId ActiveAttackEpisodeId => _attackController.ActiveEpisodeId;
        public PlayerId AttackTargetId => _attackController.AttackTargetId;
        public bool HitMomentResolved => _attackController.HitMomentResolved;
        public StalkerAttackOutcome AttackOutcome => _attackController.Outcome;
        public StalkerAttackResolutionResult AttackResolutionResult => _attackController.LastResolutionResult;
        public int AttackResolutionCount => _attackController.ResolutionCount;
        public StalkerAttackEpisode ActiveAttackEpisode => _attackController.ActiveEpisode;
        public bool HasCommittedAttackResolutionFact => _attackController.HasCommittedResolutionFact;
        public StalkerAttackResolvedFact LastCommittedAttackResolutionFact => _attackController.LastCommittedResolutionFact;
        public bool HasCommittedSearchEndedFact => _lastCommittedSearchEndedFact.IsValid;
        public StalkerSearchEndedFact LastCommittedSearchEndedFact => _lastCommittedSearchEndedFact;
        public StalkerSearchContext ActiveSearchContext => _searchContext;
        public bool HasLastKnownPosition => _memory.HasLastKnownPosition;
        public bool HasLastSeenDirection => _memory.HasLastSeenDirection;
        public Vector3 LastSeenDirection => _memory.LastSeenDirection;
        public bool HasTargetLastSeenTime => _memory.HasTargetLastSeenTime;
        public AiSimulationTime TargetLastSeenTime => _memory.TargetLastSeenTime;
        public int CurrentRegionIdValue => _currentRegionId.IsValid ? _currentRegionId.Value : canonicalCurrentRegionId;
        public int GlobalObjectiveRegionIdValue => canonicalObjectiveRegionId;
        public int SearchCandidateNodeId => searchCandidateNodeId;
        public bool FixedFallbackActive => _dynamicPatrolFallbackActive || _canonicalPatrolFallbackActive || _roomSweepPatrolFallbackActive;
        public NavigationFailureReason NavigationFailureReason => _navigation?.CurrentFailureReason ?? EchoProtocol.AI.Stalker.NavigationFailureReason.AgentUnavailable;
        public NavigationRecoveryReason RecoveryReason => _navigation?.CurrentRecoveryReason ?? NavigationRecoveryReason.None;
        public NavigationPathStatus NavigationPathStatus => _navigation?.GetPathStatus() ?? NavigationPathStatus.AgentUnavailable;
        public NavigationExecutionStatus NavigationExecutionStatus => _navigation?.GetExecutionStatus() ?? NavigationExecutionStatus.Failed;
        public StalkerWorldInteractionKind CurrentWorldInteractionKind => _worldInteractionDriver.CurrentKind;
        public float WorldInteractionProgress01 => _worldInteractionDriver.InteractionProgress01;
        public Component CurrentWorldInteractionBlocker => _worldInteractionDriver.CurrentBlocker;
        public IPlayerAttackConsequenceSink AttackConsequenceSink { get; set; }
        public bool SuppressLegacyUpdateSimulation { get; set; }

        public bool TryGetNavigationDestination(out Vector3 destination)
        {
            if (_navigation != null)
            {
                return _navigation.TryGetActiveDestination(out destination);
            }

            destination = default;
            return false;
        }

        public bool TrySetPatrolRegionEdgeOpen(int fromRegionId, int toRegionId, bool open)
        {
            if (fromRegionId <= 0 || toRegionId <= 0 || !EnsureCanonicalPatrolInitialized())
            {
                return false;
            }

            var from = new RegionId(fromRegionId);
            var to = new RegionId(toRegionId);
            var affectsCurrentRoute = !open && IsTopologyEdgeRelevantToCurrentNavigation(from, to);
            var changed = _regionGraph.TrySetEdgeOpen(from, to, open);
            if (!changed || open)
            {
                if (changed && open && currentState == StalkerState.SEARCH)
                {
                    _searchCandidatePlanningExhausted = false;
                }

                return changed;
            }

            if (affectsCurrentRoute)
            {
                HandleTopologyBlockedByDoor();
            }

            return true;
        }

        public void ConfigurePhase4AttackAcceptanceDiagnostics(
            float detectionMeterFullValue,
            float detectionFillRateValue,
            float attackRangeValue,
            float attackWindupValue,
            float attackRecoveryValue,
            IPlayerAttackConsequenceSink consequenceSink)
        {
            detectionMeterFull = Mathf.Max(0.0001f, detectionMeterFullValue);
            detectionFillRate = Mathf.Max(0f, detectionFillRateValue);
            attackRange = Mathf.Max(0f, attackRangeValue);
            attackWindup = Mathf.Max(0f, attackWindupValue);
            attackRecovery = Mathf.Max(0f, attackRecoveryValue);
            AttackConsequenceSink = consequenceSink;
        }

        public void ConfigurePatrolVariation(
            int variationSeed,
            int nearOptimalHopSlack = 1)
        {
            var clampedSlack =
                System.Math.Max(0, nearOptimalHopSlack);

            if (_hasPatrolVariationSeed
                && _patrolVariationSeed == variationSeed
                && _patrolNearOptimalHopSlack == clampedSlack)
            {
                return;
            }

            _hasPatrolVariationSeed = true;
            _patrolVariationSeed = variationSeed;
            _patrolNearOptimalHopSlack = clampedSlack;

            if (_roomSweepPlanner != null)
            {
                _roomSweepPlanner.ConfigureVariation(
                    _patrolVariationSeed);
            }

            if (_roomSweepCoverageMemory != null
                && _regionGraph != null)
            {
                _roomSweepGlobalPlanner =
                    new RoomSweepGlobalPlanner(
                        _regionGraph,
                        _roomSweepCoverageMemory,
                        _patrolVariationSeed,
                        _patrolNearOptimalHopSlack);
            }
        }

        private void Awake()
        {
            InitializeNavigation();
        }

        private void OnEnable()
        {
            InitializeNavigation();
            _hearingMemory.Reset();
        }

        private void Update()
        {
            LogRoomSweepSuppressLegacyGateOnce();
            if (SuppressLegacyUpdateSimulation)
            {
                return;
            }

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
            _currentSimulationStep = input.Step;
            _currentVisibleTargetCandidates =
                input.VisibleTargetCandidates;

            _currentTargetStatuses =
                input.TargetStatuses;

            _currentAttackTargetSnapshot =
                input.CurrentAttackTargetSnapshot;

            _currentHearingObservations =
                input.HearingObservations;

            _currentHearingEvaluationTimeUtc =
                input.HearingEvaluationTimeUtc;

            try
            {
                ApplyMovementSpeedForCurrentState();
                TickCurrentState();
                TryBeginHeardNoiseSearchFromCurrentFrame();
                TryUpdateHeardNoiseSearchFromCurrentFrame();
                _navigation?.TickProgress(CurrentSimulationDeltaSeconds);
                TickNavigationRecovery();
                TickNavigationFallback();
                return true;
            }
            finally
            {
                _currentVisibleTargetCandidates = null;
                _currentTargetStatuses = null;
                _currentAttackTargetSnapshot = null;
                _currentHearingObservations = null;
                _currentHearingEvaluationTimeUtc =
                    default;
                _currentSimulationStep = AiSimulationStep.Invalid;
                _currentSimulationDeltaSeconds = 0f;
                _currentSimulationSeconds = 0d;
                _isSimulating = false;
            }
        }

        private void TickCurrentState()
        {
            if (TickWorldInteraction())
            {
                return;
            }

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
            if (patrolMode == StalkerPatrolMode.RoomSweepSpatial)
            {
                TickRoomSweepSpatialPatrol();
                return;
            }

            if (patrolMode == StalkerPatrolMode.DynamicSpatial)
            {
                TickDynamicSpatialPatrol();
                return;
            }

            if (patrolMode == StalkerPatrolMode.ConfidenceSpatial)
            {
                TickConfidenceSpatialPatrol();
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

        private void TickConfidenceSpatialPatrol()
        {
            if (_canonicalPatrolFallbackActive)
            {
                TickFixedWaypointPatrol();
                return;
            }

            if (!CanUseNavigation())
            {
                _navigation?.ClearDestinationCache();
                return;
            }

            if (!EnsureCanonicalPatrolInitialized())
            {
                ActivateCanonicalPatrolFallback();
                return;
            }

            if (!_navigation.HasActiveDestination)
            {
                if (!SetCanonicalPatrolDestinationWithGlobalAlternates())
                {
                    ActivateCanonicalPatrolFallback();
                }

                return;
            }

            if (!_navigation.HasArrived())
            {
                return;
            }

            var agent = GetComponent<NavMeshAgent>();

            var nearestNodeId = -1;
            TryResolveNearestSpatialNode(transform.position, out nearestNodeId);

            var nearestRegionId = RegionId.Invalid;
            var destinationRegionId = RegionId.Invalid;

            if (_regionGraph != null)
            {
                _regionGraph.TryGetRegionForNode(nearestNodeId, out nearestRegionId);
                _regionGraph.TryGetRegionForNode(
                    _blackboard.DestinationSpatialNodeId,
                    out destinationRegionId);
            }

            var activeDestination = default(Vector3);
            _navigation.TryGetActiveDestination(out activeDestination);

            var corners = agent.path != null ? agent.path.corners : null;
            var pathEnd = corners != null && corners.Length > 0
                ? corners[corners.Length - 1]
                : transform.position;

            var pathEndToRequested =
                Vector3.Distance(pathEnd, activeDestination);

            LogDiagnosticWarning(
                $"[STK ARRIVAL DIAG] " +
                $"currentRegion={(_currentRegionId.IsValid ? _currentRegionId.Value : -1)} " +
                $"previousRegion={(_previousRegionId.IsValid ? _previousRegionId.Value : -1)} " +
                $"objectiveRegion={canonicalObjectiveRegionId} " +
                $"nextRegion={canonicalNextRegionId} " +
                $"currentNode={_blackboard.CurrentSpatialNodeId} " +
                $"destinationNode={_blackboard.DestinationSpatialNodeId} " +
                $"nearestNode={nearestNodeId} " +
                $"nearestRegion={(nearestRegionId.IsValid ? nearestRegionId.Value : -1)} " +
                $"destinationRegion={(destinationRegionId.IsValid ? destinationRegionId.Value : -1)} " +
                $"remaining={agent.remainingDistance:0.000} " +
                $"stopping={agent.stoppingDistance:0.000} " +
                $"pathEnd={pathEnd} " +
                $"pathEndToRequested={pathEndToRequested:0.000} " +
                $"worldDistance={Vector3.Distance(transform.position, activeDestination):0.000} " +
                $"position={transform.position} " +
                $"destination={activeDestination}");

            MarkCanonicalDestinationReached();
            if (!SetCanonicalPatrolDestinationWithGlobalAlternates())
            {
                ActivateCanonicalPatrolFallback();
            }
        }

        private void TickRoomSweepSpatialPatrol()
        {
            if (_roomSweepPatrolFallbackActive)
            {
                TickFixedWaypointPatrol();
                return;
            }

            LogRoomSweepNavigationGateOnce();
            if (!CanUseNavigation())
            {
                _navigation?.ClearDestinationCache();
                return;
            }

            if (!EnsureRoomSweepPatrolInitialized())
            {
                ActivateRoomSweepPatrolFallback();
                return;
            }

            if (_roomSweepSelfProbeScanActive)
            {
                TickRoomSweepSelfProbeScan();
                return;
            }

            if (_navigation.HasActiveDestination)
            {
                if (!_navigation.HasArrived())
                {
                    if (TryCancelCompletedActiveRoomSweepProbe())
                    {
                        if (!SetRoomSweepPatrolDestination())
                        {
                            ActivateRoomSweepPatrolFallback();
                        }
                    }

                    return;
                }

                MarkRoomSweepDestinationReached();
            }

            if (!SetRoomSweepPatrolDestination())
            {
                ActivateRoomSweepPatrolFallback();
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
            if (HasTypedTargetFrame && _memory.CurrentTargetId.IsValid)
            {
                _attackController.BeginAttack(true, _memory.CurrentTargetId, _currentSimulationStep);
            }

            StopAgentPath();
        }

        private void TickAttack()
        {
            if (HasTypedTargetFrame || _attackController.HasActiveEpisode)
            {
                TickAttackTyped();
                return;
            }

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

        private void TickAttackTyped()
        {
            if (!_attackController.HasActiveEpisode)
            {
                if (!_memory.CurrentTargetId.IsValid)
                {
                    lastAttackResult = StalkerAttackResult.Miss;
                    EnterRecover();
                    return;
                }

                _attackController.BeginAttack(true, _memory.CurrentTargetId, _currentSimulationStep);
            }

            var targetId = _attackController.AttackTargetId;
            if (targetId.IsValid
                && TryGetUniqueTargetStatus(targetId, out var status)
                && !status.Eligible)
            {
                _memory.ClearCurrentTarget();
            }

            _attackController.AdvanceWindup(CurrentSimulationDeltaSeconds);
            attackElapsedTime = _attackController.ActiveEpisode.WindupElapsedSeconds;
            if (attackElapsedTime < GetAttackWindup())
            {
                return;
            }

            ResolveAttackHitMomentTyped();
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
            PlayerDownState downState = currentTarget.GetComponentInParent<PlayerDownState>();
            if (downState != null)
            {
                downState.ApplyDamage(GetAttackDamage());
            }
        }

        private void ResolveAttackHitMomentTyped()
        {
            var snapshot = _currentAttackTargetSnapshot
                ?? StalkerAttackTargetSnapshot.Missing(_attackController.AttackTargetId);
            var result = _attackController.ResolveHitMoment(
                true,
                _attackController.ActiveEpisodeId,
                transform.position,
                GetAttackRange(),
                snapshot,
                AttackConsequenceSink,
                _currentSimulationStep);

            lastAttackResult = result == StalkerAttackResolutionResult.ResolvedHit
                ? StalkerAttackResult.Hit
                : StalkerAttackResult.Miss;
        }

        private void EnterRecover()
        {
            currentState = StalkerState.RECOVER;
            recoverElapsedTime = 0f;
            StopAgentPath();
        }

        private void TickRecover()
        {
            if (HasTypedTargetFrame || _memory.CurrentTargetId.IsValid || _attackController.HasActiveEpisode)
            {
                TickRecoverTyped();
                return;
            }

            recoverElapsedTime += CurrentSimulationDeltaSeconds;
            if (recoverElapsedTime < GetAttackRecovery())
            {
                return;
            }

            attackElapsedTime = 0f;
            recoverElapsedTime = 0f;
            _attackController.ClearActiveEpisode();

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

        private void TickRecoverTyped()
        {
            recoverElapsedTime += CurrentSimulationDeltaSeconds;

            var currentTargetId = _memory.CurrentTargetId;
            if (currentTargetId.IsValid
                && (!TryGetUniqueTargetStatus(currentTargetId, out var inRecoveryStatus) || !inRecoveryStatus.Eligible))
            {
                _memory.ClearCurrentTarget();
                currentTargetId = PlayerId.Invalid;
            }

            if (recoverElapsedTime < GetAttackRecovery())
            {
                return;
            }

            attackElapsedTime = 0f;
            recoverElapsedTime = 0f;
            _attackController.ClearActiveEpisode();

            if (currentTargetId.IsValid
                && TryGetUniqueTargetStatus(currentTargetId, out var status)
                && status.Eligible)
            {
                if (TryGetUniqueVisibleTargetCandidate(currentTargetId, out var candidate, out var hasDuplicate)
                    && !hasDuplicate
                    && candidate.Eligibility.Eligible
                    && _memory.TryAcceptCurrentTargetObservation(candidate.Observation))
                {
                    lastKnownPosition = _memory.LastKnownPosition;
                    ResetChaseDestinationTracking();
                    ResetNavigationRecoveryBudget();
                    currentState = StalkerState.CHASE;
                    SetChaseDestination(candidate.Observation.ObservedPosition);
                    return;
                }

                if (_memory.HasLastKnownPosition)
                {
                    EnterSearch();
                    return;
                }
            }

            ClearTargetContext();
            if (TryAcquireTypedDetectionTargetFromVisibleFrame())
            {
                return;
            }

            currentState = StalkerState.PATROL;
            SetCurrentPatrolDestination();
        }

        private void TryBeginHeardNoiseSearchFromCurrentFrame()
        {
            //
            // Hearing currently starts a new investigation only from PATROL.
            // Visual DETECT/CHASE/ATTACK/RECOVER and visual SEARCH keep priority.
            //
            if (currentState != StalkerState.PATROL
                || _worldInteractionDriver.HasActiveInteraction
                || _currentHearingObservations == null
                || _currentHearingObservations.Count == 0
                || _currentHearingEvaluationTimeUtc == default
                || _currentHearingEvaluationTimeUtc.Kind
                    != System.DateTimeKind.Utc)
            {
                return;
            }

            if (!_hearingSelector.TrySelectInitial(
                    _currentHearingObservations,
                    _currentHearingEvaluationTimeUtc,
                    out var selection)
                || !selection.HasObservation)
            {
                return;
            }

            EnterHeardNoiseSearch(
                selection.Observation);
        }

        private void TryUpdateHeardNoiseSearchFromCurrentFrame()
        {
            if (currentState != StalkerState.SEARCH
                || _searchContext == null
                || _searchContext.Source
                    != StalkerSearchSource.HeardNoise
                || !_hearingMemory.HasActiveNoiseInvestigation
                || _currentHearingObservations == null
                || _currentHearingObservations.Count == 0
                || _currentHearingEvaluationTimeUtc == default
                || _currentHearingEvaluationTimeUtc.Kind
                    != System.DateTimeKind.Utc)
            {
                return;
            }

            if (!_hearingSelector.TrySelectInvestigationUpdate(
                    _hearingMemory,
                    _currentHearingObservations,
                    _currentHearingEvaluationTimeUtc,
                    out var selection)
                || !selection.HasObservation)
            {
                return;
            }

            var previousPosition =
                _hearingMemory.InvestigationPosition;

            _hearingMemory.UpdateNoiseInvestigation(
                selection.Observation);

            UnityEngine.Debug.Log(
                $"[STK_HEARING][UPDATE_SEARCH] " +
                $"reason={selection.Reason} " +
                $"event={selection.Observation.NoiseEventId} " +
                $"position={selection.Observation.ObservedNoisePosition} " +
                $"effectiveIntensity={selection.Observation.EffectiveIntensity:F3}");

            var updatedPosition =
                _hearingMemory.InvestigationPosition;

            //
            // Do not manufacture visual player knowledge.
            // Only the hearing hypothesis and its navigation objective move.
            //
            if ((updatedPosition - previousPosition)
                    .sqrMagnitude
                <= 0.0001f)
            {
                return;
            }

            RestartHeardNoiseSearchAt(
                updatedPosition);
        }

        private void RestartHeardNoiseSearchAt(
            Vector3 noisePosition)
        {
            var direction =
                noisePosition - transform.position;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction =
                    transform.forward;
            }

            //
            // New hearing evidence changes the hypothesis,
            // therefore rebuild the SEARCH episode around
            // the new authoritative position.
            //
            ClearSearchRuntimeContext();

            searchElapsedTime = 0f;

            EnsureSearchContext(
                StalkerSearchSource.HeardNoise,
                noisePosition,
                direction);

            if (!TrySetSearchOriginDestination(
                    noisePosition))
            {
                TryPlanNextSearchCandidate();
            }
        }

        private void EnterHeardNoiseSearch(
            HearingObservation observation)
        {
            var noisePosition =
                observation.ObservedNoisePosition;

            var direction =
                noisePosition - transform.position;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction =
                    transform.forward;
            }

            ResetChaseDestinationTracking();
            ResetNavigationRecoveryBudget();

            //
            // Hearing knowledge remains completely separate from
            // StalkerMemory visual player knowledge.
            //
            _hearingMemory.BeginNoiseInvestigation(
                observation);

            UnityEngine.Debug.Log(
                $"[STK_HEARING][ENTER_SEARCH] " +
                $"event={observation.NoiseEventId} " +
                $"type={observation.NoiseType} " +
                $"position={observation.ObservedNoisePosition} " +
                $"effectiveIntensity={observation.EffectiveIntensity:F3}");

            //
            // Start a fresh SEARCH episode without modifying
            // lastKnownPosition or manufacturing a PlayerId.
            //
            ClearSearchRuntimeContext();

            currentState =
                StalkerState.SEARCH;

            searchElapsedTime = 0f;

            EnsureSearchContext(
                StalkerSearchSource.HeardNoise,
                noisePosition,
                direction);

            if (!TrySetSearchOriginDestination(
                    noisePosition))
            {
                TryPlanNextSearchCandidate();
            }
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
            EnsureSearchContext();
            if (HasTypedTargetFrame)
            {
                if (!TrySetSearchOriginDestination(_memory.LastKnownPosition))
                {
                    TryPlanNextSearchCandidate();
                }

                return;
            }

            if (!TrySetSearchOriginDestination(lastKnownPosition))
            {
                TryPlanNextSearchCandidate();
            }
        }

        private void TickSearch()
        {
            if (_searchContext != null
                && _searchContext.Source == StalkerSearchSource.HeardNoise)
            {
                TickHeardNoiseSearch();
                return;
            }

            if (HasTypedTargetFrame)
            {
                TickSearchTyped();
                return;
            }

            if (currentTarget == null)
            {
                CommitSearchEnded(
                    StalkerSearchTerminalOutcome
                        .CURRENT_TARGET_INVALID_NO_REPLACEMENT);

                ClearSearchContext();
                currentState = StalkerState.PATROL;
                StopAgentPath();
                SetCurrentPatrolDestination();
                return;
            }

            if (TryGetVisibleCurrentTargetObservation(
                    out var observedPosition))
            {
                lastKnownPosition = observedPosition;

                CommitSearchEnded(
                    StalkerSearchTerminalOutcome
                        .SAME_TARGET_REACQUIRED);

                ClearSearchRuntimeContext();
                ResetChaseDestinationTracking();
                ResetNavigationRecoveryBudget();

                currentState = StalkerState.CHASE;
                SetChaseDestination(observedPosition);
                return;
            }

            if (_navigation != null
                && _navigation.HasActiveDestination
                && _navigation.HasArrived())
            {
                MarkSearchCandidateReached();
            }

            if (_navigation == null
                || !_navigation.HasActiveDestination)
            {
                TryPlanNextSearchCandidateIfNotHolding();
            }

            searchElapsedTime +=
                CurrentSimulationDeltaSeconds;

            if (searchElapsedTime < GetSearchDuration())
            {
                return;
            }

            CommitSearchEnded(
                StalkerSearchTerminalOutcome.TIMEOUT);

            ClearSearchContext();
            currentState = StalkerState.PATROL;
            StopAgentPath();
            SetCurrentPatrolDestination();
        }

        private void TickHeardNoiseSearch()
        {
            //
            // HeardNoise SEARCH deliberately has no CurrentTargetId.
            // A player identity only becomes known through the visual
            // perception pipeline.
            //
            if (!_hearingMemory.HasActiveNoiseInvestigation)
            {
                ClearSearchRuntimeContext();

                currentState =
                    StalkerState.PATROL;

                StopAgentPath();
                SetCurrentPatrolDestination();
                return;
            }

            //
            // A real visible player converts the positional hearing
            // hypothesis into the normal visual DETECT pipeline.
            //
            if (TryAcquireDifferentVisibleTargetDuringSearch(
                    PlayerId.Invalid))
            {
                _hearingMemory.ClearNoiseInvestigation();
                return;
            }

            if (_navigation != null
                && _navigation.HasActiveDestination
                && _navigation.HasArrived())
            {
                MarkSearchCandidateReached();
            }

            if (_navigation == null
                || !_navigation.HasActiveDestination)
            {
                TryPlanNextSearchCandidateIfNotHolding();
            }

            searchElapsedTime +=
                CurrentSimulationDeltaSeconds;

            if (searchElapsedTime < GetSearchDuration())
            {
                return;
            }

            CommitSearchEnded(
                StalkerSearchTerminalOutcome.TIMEOUT);

            _hearingMemory.ClearNoiseInvestigation();
            ClearSearchContext();

            currentState =
                StalkerState.PATROL;

            StopAgentPath();
            SetCurrentPatrolDestination();
        }

        private void TickSearchTyped()
        {
            var currentTargetId = _memory.CurrentTargetId;
            if (!currentTargetId.IsValid)
            {
                CommitSearchTerminalAndInvalidateCurrentTarget(StalkerSearchTerminalOutcome.CURRENT_TARGET_INVALID_NO_REPLACEMENT);
                return;
            }

            if (!TryGetUniqueTargetStatus(currentTargetId, out var status) || !status.Eligible)
            {
                if (TryAcquireDifferentVisibleTargetDuringSearch(currentTargetId))
                {
                    return;
                }

                CommitSearchEnded(StalkerSearchTerminalOutcome.CURRENT_TARGET_INVALID_NO_REPLACEMENT);
                ClearSearchContext();
                if (currentState == StalkerState.SEARCH)
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
                    CommitSearchTerminalAndInvalidateCurrentTarget(StalkerSearchTerminalOutcome.CURRENT_TARGET_INVALID_NO_REPLACEMENT);
                    return;
                }

                var observation = candidate.Observation;
                if (!_memory.TryAcceptCurrentTargetObservation(observation))
                {
                    CommitSearchTerminalAndInvalidateCurrentTarget(StalkerSearchTerminalOutcome.CURRENT_TARGET_INVALID_NO_REPLACEMENT);
                    return;
                }

                lastKnownPosition = _memory.LastKnownPosition;
                CommitSearchEnded(StalkerSearchTerminalOutcome.SAME_TARGET_REACQUIRED);
                ClearSearchRuntimeContext();
                ResetChaseDestinationTracking();
                ResetNavigationRecoveryBudget();
                currentState = StalkerState.CHASE;
                SetChaseDestination(observation.ObservedPosition);
                return;
            }

            if (hasDuplicate || !_memory.HasLastKnownPosition)
            {
                CommitSearchTerminalAndInvalidateCurrentTarget(StalkerSearchTerminalOutcome.CURRENT_TARGET_INVALID_NO_REPLACEMENT);
                return;
            }

            if (TryAcquireDifferentVisibleTargetDuringSearch(currentTargetId))
            {
                return;
            }

            if (_navigation != null && _navigation.HasActiveDestination && _navigation.HasArrived())
            {
                MarkSearchCandidateReached();
            }

            if (_navigation == null || !_navigation.HasActiveDestination)
            {
                TryPlanNextSearchCandidateIfNotHolding();
            }

            searchElapsedTime += CurrentSimulationDeltaSeconds;
            if (searchElapsedTime < GetSearchDuration())
            {
                return;
            }

            CommitSearchEnded(StalkerSearchTerminalOutcome.TIMEOUT);
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

            if (!TryResolveReachableChaseDestination(
                    observedPosition,
                    out var chaseDestination))
            {
                return;
            }

            _chaseDestinationRefreshElapsed += CurrentSimulationDeltaSeconds;
            if (!ShouldRefreshChaseDestination(chaseDestination))
            {
                return;
            }

            var result = _navigation.RequestDestination(
                chaseDestination,
                NavigationRequestIntent.TrackMovingGoal);
            if (!result.IsAccepted)
            {
                return;
            }

            _lastChaseRequestedDestination = chaseDestination;
            _hasLastChaseRequestedDestination = true;
            _chaseDestinationRefreshElapsed = 0f;
            SetNavigationObjective(new StalkerNavigationObjectiveKey(
                StalkerNavigationObjectiveKind.ChaseTarget,
                -1,
                -1,
                _memory.CurrentTargetId.IsValid ? _memory.CurrentTargetId.Value : -1));
        }

        private Vector3 GetPredictedChaseDestination(
            Vector3 observedPosition)
        {
            var nowSeconds = CurrentSimulationTimeSeconds;

            if (!_hasPreviousChaseObservation)
            {
                _hasPreviousChaseObservation = true;
                _previousChaseObservedPosition = observedPosition;
                _previousChaseObservationTimeSeconds = nowSeconds;
                return observedPosition;
            }

            var deltaSeconds =
                nowSeconds - _previousChaseObservationTimeSeconds;

            var previousPosition =
                _previousChaseObservedPosition;

            _previousChaseObservedPosition = observedPosition;
            _previousChaseObservationTimeSeconds = nowSeconds;

            if (deltaSeconds <= 0.0001f)
            {
                return observedPosition;
            }

            var displacement =
                observedPosition - previousPosition;

            displacement.y = 0f;

            var velocity =
                displacement / deltaSeconds;

            var leadOffset =
                velocity * Mathf.Max(
                    0f,
                    chasePredictionLeadSeconds);

            var maxLeadDistance =
                Mathf.Max(
                    0f,
                    chasePredictionMaxDistance);

            if (leadOffset.sqrMagnitude
                > maxLeadDistance * maxLeadDistance)
            {
                leadOffset =
                    leadOffset.normalized * maxLeadDistance;
            }

            return observedPosition + leadOffset;
        }

        private bool TryResolveReachableChaseDestination(
            Vector3 observedPosition,
            out Vector3 destination)
        {
            var predictedDestination =
                GetPredictedChaseDestination(
                    observedPosition);

            if (_navigation.EvaluateDestination(
                    predictedDestination).IsComplete)
            {
                destination = predictedDestination;
                return true;
            }

            var agent = GetComponent<NavMeshAgent>();
            if (agent != null
                && agent.enabled
                && agent.isOnNavMesh)
            {
                var projectionRadius =
                    Mathf.Max(
                        0.01f,
                        chasePredictionMaxDistance);

                if (NavMesh.SamplePosition(
                        predictedDestination,
                        out var hit,
                        projectionRadius,
                        agent.areaMask)
                    && _navigation.EvaluateDestination(
                        hit.position).IsComplete)
                {
                    destination = hit.position;
                    return true;
                }
            }

            if (_navigation.EvaluateDestination(
                    observedPosition).IsComplete)
            {
                destination = observedPosition;
                return true;
            }

            destination = observedPosition;
            return false;
        }

        private void SetSearchDestination()
        {
            TrySetSearchOriginDestination(lastKnownPosition);
        }

        private void EnsureSearchContext()
        {
            if (_searchContext != null)
            {
                return;
            }

            var origin =
                _memory.HasLastKnownPosition
                    ? _memory.LastKnownPosition
                    : lastKnownPosition;

            var direction =
                _memory.HasLastSeenDirection
                    ? _memory.LastSeenDirection
                    : transform.forward;

            EnsureSearchContext(
                StalkerSearchSource.VisualTargetLoss,
                origin,
                direction);
        }

        private void EnsureSearchContext(
            StalkerSearchSource source,
            Vector3 origin,
            Vector3 direction)
        {
            if (_searchContext != null)
            {
                return;
            }

            var originRegionId =
                RegionId.Invalid;

            if (EnsureCanonicalPatrolInitialized()
                && TryResolveNearestSpatialNode(
                    origin,
                    out var originNodeId)
                && _regionGraph != null)
            {
                _regionGraph.TryGetRegionForNode(
                    originNodeId,
                    out originRegionId);
            }

            var startTime =
                new AiSimulationTime(
                    _legacySimulationTick < 0
                        ? 0
                        : _legacySimulationTick,
                    System.Math.Max(
                        0d,
                        _currentSimulationSeconds));

            _searchEpisodeSequence++;

            _searchContext =
                new StalkerSearchContext(
                    new SearchEpisodeId(
                        _searchEpisodeSequence),
                    source,
                    origin,
                    direction,
                    startTime,
                    originRegionId);

            searchEpisodeId =
                _searchContext.EpisodeId.Value;

            _searchCandidatePlanningExhausted =
                false;
        }

        private void MarkSearchCandidateReached()
        {
            if (_searchContext == null || searchCandidateNodeId < 0)
            {
                _navigation?.Stop();
                return;
            }

            _searchContext.RecordPhysicalCandidateArrival(searchCandidateNodeId);
            _coverageMemory?.RecordPhysicalNodeArrival(searchCandidateNodeId, CurrentSimulationTimeSeconds);
            searchCandidateNodeId = -1;
            _blackboard.DestinationSpatialNodeId = -1;
            _searchCandidatePlanningExhausted = false;
            _navigation?.Stop();
        }

        private bool TryPlanNextSearchCandidateIfNotHolding()
        {
            return !_searchCandidatePlanningExhausted && TryPlanNextSearchCandidate();
        }

        private bool TryPlanNextSearchCandidate()
        {
            EnsureSearchContext();
            if (_searchContext == null)
            {
                return false;
            }

            if (_searchPlanner == null)
            {
                EnsureCanonicalPatrolInitialized();
            }

            if (_searchPlanner == null || !TryResolveNearestSpatialNode(transform.position, out var currentNodeId))
            {
                _navigation?.Stop();
                _searchCandidatePlanningExhausted = true;
                return false;
            }

            var maxAttempts = _spatialPatrolGraph?.Nodes?.Count ?? 0;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (!_searchPlanner.TrySelectCandidate(
                        _searchContext,
                        searchRadius,
                        currentNodeId,
                        _blackboard.PreviousSpatialNodeId,
                        out var selection))
                {
                    _navigation?.Stop();
                    _searchCandidatePlanningExhausted = true;
                    return false;
                }

                var destinationStatus = TryRequestSearchDestination(selection.DestinationNode.Position);
                if (destinationStatus == NavigationEvaluationStatus.Complete)
                {
                    _searchContext.RecordCandidateAttempt(selection.DestinationNode.Id);
                    searchCandidateNodeId = selection.DestinationNode.Id;
                    _blackboard.DestinationSpatialNodeId = selection.DestinationNode.Id;
                    _searchCandidatePlanningExhausted = false;
                    SetNavigationObjective(new StalkerNavigationObjectiveKey(
                        StalkerNavigationObjectiveKind.SearchCandidate,
                        selection.DestinationNode.Id,
                        -1,
                        _memory.CurrentTargetId.IsValid ? _memory.CurrentTargetId.Value : -1));
                    return true;
                }

                if (!IsSearchCandidateSpecificDestinationFailure(destinationStatus))
                {
                    _navigation?.Stop();
                    return false;
                }

                _searchContext.RecordCandidateAttempt(selection.DestinationNode.Id);
            }

            _navigation?.Stop();
            _searchCandidatePlanningExhausted = true;
            return false;
        }

        private void CommitSearchEnded(StalkerSearchTerminalOutcome outcome)
        {
            if (_searchContext == null
                || !_searchContext.EpisodeId.IsValid
                || _lastCommittedSearchEpisodeId == _searchContext.EpisodeId)
            {
                return;
            }

            _lastCommittedSearchEndedFact = new StalkerSearchEndedFact(
                _searchContext.EpisodeId,
                outcome,
                _currentSimulationStep.Time.IsValid
                    ? _currentSimulationStep.Time
                    : new AiSimulationTime(_legacySimulationTick < 0 ? 0 : _legacySimulationTick, System.Math.Max(0d, _currentSimulationSeconds)));
            _lastCommittedSearchEpisodeId = _searchContext.EpisodeId;
        }

        private void CommitSearchTerminalAndInvalidateCurrentTarget(StalkerSearchTerminalOutcome outcome)
        {
            CommitSearchEnded(outcome);
            ClearSearchRuntimeContext();
            InvalidateCurrentTarget();
        }

        private void CommitSearchTerminalAndInvalidateDetectionTarget(StalkerSearchTerminalOutcome outcome)
        {
            CommitSearchEnded(outcome);
            ClearSearchRuntimeContext();
            InvalidateDetectionTarget();
        }

        private bool TryAcquireDifferentVisibleTargetDuringSearch(PlayerId currentTargetId)
        {
            if (_currentVisibleTargetCandidates == null)
            {
                return false;
            }

            var bestCandidate = default(StalkerTargetCandidate);
            var hasCandidate = false;
            for (var i = 0; i < _currentVisibleTargetCandidates.Count; i++)
            {
                var candidate = _currentVisibleTargetCandidates[i];
                if (candidate.Observation.PlayerId == currentTargetId || !candidate.Eligibility.Eligible)
                {
                    continue;
                }

                if (!hasCandidate
                    || IsHigherPrioritySearchReplacement(candidate.Observation, bestCandidate.Observation))
                {
                    bestCandidate = candidate;
                    hasCandidate = true;
                }
            }

            if (!hasCandidate)
            {
                return false;
            }

            _memory.SetDetectionTarget(bestCandidate.Observation.PlayerId);
            if (!_memory.TryAcceptDetectionTargetObservation(bestCandidate.Observation))
            {
                CommitSearchTerminalAndInvalidateDetectionTarget(StalkerSearchTerminalOutcome.CURRENT_TARGET_INVALID_NO_REPLACEMENT);
                return true;
            }

            CommitSearchEnded(StalkerSearchTerminalOutcome.NEW_ELIGIBLE_TARGET_OBSERVED);
            currentTarget = null;
            _memory.ClearCurrentTarget();
            ClearSearchRuntimeContext();
            detectionMeter = 0f;
            detectionTarget = null;
            currentState = StalkerState.DETECT;
            StopAgentPath();
            return true;
        }

        private static bool IsHigherPrioritySearchReplacement(
            VisionObservation candidate,
            VisionObservation currentBest)
        {
            if (candidate.Distance < currentBest.Distance - TargetSelectionTieEpsilon)
            {
                return true;
            }

            if (candidate.Distance > currentBest.Distance + TargetSelectionTieEpsilon)
            {
                return false;
            }

            return DeterministicTieBreak.ComparePrimaryThenStableKey(
                0,
                candidate.PlayerId,
                currentBest.PlayerId) < 0;
        }

        private bool TrySetSearchDestination(Vector3 rememberedPosition)
        {
            return TryRequestSearchDestination(rememberedPosition) == NavigationEvaluationStatus.Complete;
        }

        private NavigationEvaluationStatus TryRequestSearchDestination(Vector3 rememberedPosition)
        {
            if (!CanUseNavigation())
            {
                _navigation?.ClearDestinationCache();
                return _navigation == null
                    ? NavigationEvaluationStatus.AgentUnavailable
                    : NavigationEvaluationStatus.AgentNotOnNavMesh;
            }

            var evaluation = _navigation.EvaluateDestination(rememberedPosition);
            if (!evaluation.IsComplete)
            {
                return evaluation.Status;
            }

            return ToSearchDestinationRequestStatus(_navigation.RequestDestination(
                rememberedPosition,
                NavigationRequestIntent.NewGoal));
        }

        private static NavigationEvaluationStatus ToSearchDestinationRequestStatus(NavigationPlanResult result)
        {
            switch (result.Status)
            {
                case NavigationPlanStatus.Accepted:
                case NavigationPlanStatus.AlreadyActive:
                    return NavigationEvaluationStatus.Complete;
                case NavigationPlanStatus.AgentUnavailable:
                    return NavigationEvaluationStatus.AgentUnavailable;
                case NavigationPlanStatus.AgentNotOnNavMesh:
                    return NavigationEvaluationStatus.AgentNotOnNavMesh;
                case NavigationPlanStatus.DestinationRequestFailed:
                    return NavigationEvaluationStatus.DestinationInvalid;
                default:
                    return NavigationEvaluationStatus.Invalid;
            }
        }

        private bool TrySetSearchOriginDestination(Vector3 rememberedPosition)
        {
            if (!TrySetSearchDestination(rememberedPosition))
            {
                return false;
            }

            searchCandidateNodeId = -1;
            _blackboard.DestinationSpatialNodeId = -1;
            SetNavigationObjective(new StalkerNavigationObjectiveKey(
                StalkerNavigationObjectiveKind.SearchOriginLkp,
                -1,
                _searchContext != null && _searchContext.SearchOriginRegionId.IsValid
                    ? _searchContext.SearchOriginRegionId.Value
                    : -1,
                _memory.CurrentTargetId.IsValid ? _memory.CurrentTargetId.Value : -1));
            return true;
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
            _searchContext = null;
            searchEpisodeId = 0;
            searchCandidateNodeId = -1;
            _searchCandidatePlanningExhausted = false;
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

        private float GetAttackDamage()
        {
            return Mathf.Max(0f, attackDamage);
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
            CancelWorldInteraction("stop-agent-path");
            ClearRoomSweepSelfProbeScan();
            ResetChaseDestinationTracking();
            ClearNavigationObjective();
            ResetFixedPatrolFallbackState();
            _navigation?.Stop();
        }

        private void TickNavigationRecovery()
        {
            if (_navigation == null || !IsLocomotionState())
            {
                return;
            }

            var pathStatus = _navigation.GetPathStatus();
            var executionStatus = _navigation.GetExecutionStatus();
            if (executionStatus != NavigationExecutionStatus.Failed
                && executionStatus != NavigationExecutionStatus.NoProgress
                && executionStatus != NavigationExecutionStatus.Stuck)
            {
                return;
            }

            var failureReason = _navigation.CurrentFailureReason;
            if (failureReason == NavigationFailureReason.None)
            {
                failureReason = ResolveNavigationFailureReason(pathStatus, executionStatus);
            }

            if (failureReason == NavigationFailureReason.AgentNotOnNavMesh)
            {
                if (_navigation.TryReattachToNearestNavMesh(
                        EmergencyNavMeshRecoverySampleDistance))
                {
                    _navigation.RecordRecoveryReason(
                        NavigationRecoveryReason.EmergencyNavMeshRecovery);

                    if (TryGetCurrentNavigationRecoveryDestination(
                            out var destination))
                    {
                        _navigation.RequestDestination(
                            destination,
                            NavigationRequestIntent.RecoveryRepath);

                        _navigation.RecordRecoveryReason(
                            NavigationRecoveryReason.EmergencyNavMeshRecovery);
                    }
                }

                return;
            }

            LogDiagnosticWarning(
                $"[STK NAV DIAG] state={currentState} " +
                $"path={pathStatus} execution={executionStatus} failure={failureReason} " +
                $"currentNode={_blackboard.CurrentSpatialNodeId} " +
                $"destinationNode={_blackboard.DestinationSpatialNodeId} " +
                $"position={transform.position}");

            if (TryBeginWorldInteractionForCurrentNavigation(failureReason))
            {
                return;
            }

            if (currentState == StalkerState.SEARCH)
            {
                HandleSearchNavigationFailure(failureReason);
                return;
            }

            if (currentState == StalkerState.CHASE)
            {
                HandleChaseNavigationFailure(failureReason);
                return;
            }

            if (currentState == StalkerState.PATROL)
            {
                if (patrolMode == StalkerPatrolMode.RoomSweepSpatial
                    && _roomSweepPatrolFallbackActive
                    && _blackboard.DestinationSpatialNodeId < 0
                    && !_navigation.HasActiveDestination)
                {
                    return;
                }

                HandlePatrolNavigationFailure(failureReason);
            }
        }

        private bool TryIssueNavigationRecoveryRepath(NavigationRecoveryReason recoveryReason)
        {
            if (_navigationRecoveryAttemptUsed)
            {
                return false;
            }

            if (!TryGetCurrentNavigationRecoveryDestination(out var destination))
            {
                return false;
            }

            _navigation.RecordRecoveryReason(recoveryReason);
            _navigationRecoveryAttemptUsed = true;
            var result = _navigation.RequestDestination(destination, NavigationRequestIntent.RecoveryRepath);
            if (result.IsAccepted)
            {
                _navigation.RecordRecoveryReason(recoveryReason);
            }

            return result.IsAccepted;
        }

        private void HandlePatrolNavigationFailure(NavigationFailureReason failureReason)
        {
            if (patrolMode == StalkerPatrolMode.RoomSweepSpatial && !_roomSweepPatrolFallbackActive)
            {
                HandleRoomSweepNavigationFailure(failureReason);
                return;
            }

            if (patrolMode == StalkerPatrolMode.DynamicSpatial && !_dynamicPatrolFallbackActive)
            {
                HandleDynamicSpatialNavigationFailure(failureReason);
                return;
            }

            if (patrolMode == StalkerPatrolMode.ConfidenceSpatial && !_canonicalPatrolFallbackActive)
            {
                HandleConfidenceSpatialNavigationFailure(failureReason);
                return;
            }

            if (IsNavigationExecutionRetryableFailure(failureReason)
                && HasRecoveryBudgetForCurrentObjective()
                && TryIssueNavigationRecoveryRepath(ToSameObjectiveRecoveryReason(failureReason)))
            {
                return;
            }
        }

        private void HandleDynamicSpatialNavigationFailure(NavigationFailureReason failureReason)
        {
            if (IsNavigationAgentUnavailableFailure(failureReason))
            {
                return;
            }

            if (IsNavigationExecutionRetryableFailure(failureReason)
                && HasRecoveryBudgetForCurrentObjective()
                && TryIssueNavigationRecoveryRepath(ToSameObjectiveRecoveryReason(failureReason)))
            {
                return;
            }

            if (_blackboard.DestinationSpatialNodeId >= 0)
            {
                _rejectedDynamicPatrolNodeIds.Add(_blackboard.DestinationSpatialNodeId);
            }

            _navigation?.Stop();
            _navigation?.RecordRecoveryReason(NavigationRecoveryReason.AlternateLocalCandidate);
            ClearDynamicPatrolDestination();
            if (SetDynamicSpatialPatrolDestination())
            {
                _navigation?.RecordRecoveryReason(NavigationRecoveryReason.AlternateLocalCandidate);
                return;
            }

            ActivateDynamicPatrolFallback();
        }

        private void HandleConfidenceSpatialNavigationFailure(NavigationFailureReason failureReason)
        {
            if (IsNavigationAgentUnavailableFailure(failureReason))
            {
                return;
            }

            if (IsNavigationExecutionRetryableFailure(failureReason)
                && HasRecoveryBudgetForCurrentObjective()
                && TryIssueNavigationRecoveryRepath(ToSameObjectiveRecoveryReason(failureReason)))
            {
                return;
            }

            if (_blackboard.DestinationSpatialNodeId >= 0)
            {
                _rejectedCanonicalLocalNodeIds.Add(_blackboard.DestinationSpatialNodeId);
            }

            _navigation?.Stop();
            _navigation?.RecordRecoveryReason(NavigationRecoveryReason.AlternateLocalCandidate);
            ClearDynamicPatrolDestination();
            if (TrySetCanonicalPatrolDestinationWithGlobalAlternates(
                    ToGlobalInvalidationReason(failureReason),
                    out var recoveryReason))
            {
                _navigation?.RecordRecoveryReason(recoveryReason == NavigationRecoveryReason.None
                    ? NavigationRecoveryReason.AlternateLocalCandidate
                    : recoveryReason);
                return;
            }

            ActivateCanonicalPatrolFallback();
        }

        private void HandleRoomSweepNavigationFailure(NavigationFailureReason failureReason)
        {
            if (IsNavigationAgentUnavailableFailure(failureReason))
            {
                return;
            }

            if (IsNavigationExecutionRetryableFailure(failureReason)
                && HasRecoveryBudgetForCurrentObjective()
                && TryIssueNavigationRecoveryRepath(ToSameObjectiveRecoveryReason(failureReason)))
            {
                return;
            }

            var objectiveKind = _navigationObjectiveKey.Kind;
            var destinationNodeId = _blackboard.DestinationSpatialNodeId;
            _navigation?.Stop();

            if (objectiveKind == StalkerNavigationObjectiveKind.RoomSweepProbe)
            {
                if (destinationNodeId >= 0)
                {
                    RejectRoomSweepProbeWithDiagnostic(
                        destinationNodeId,
                        GetRoomSweepNavigationRejectionCategory(failureReason),
                        failureReason);
                }

                _navigation?.RecordRecoveryReason(NavigationRecoveryReason.AlternateLocalCandidate);
                ClearRoomSweepDestination();
                if (_roomSweepPlanner != null && _roomSweepPlanner.IsExhausted)
                {
                    ActivateRoomSweepPatrolFallback();
                    return;
                }

                if (SetRoomSweepPatrolDestination())
                {
                    _navigation?.RecordRecoveryReason(NavigationRecoveryReason.AlternateLocalCandidate);
                    return;
                }

                ActivateRoomSweepPatrolFallback();
                return;
            }

            if (objectiveKind == StalkerNavigationObjectiveKind.RoomSweepTransit)
            {
                if (destinationNodeId >= 0)
                {
                    _rejectedRoomSweepTransitNodeIds.Add(destinationNodeId);
                }

                _navigation?.RecordRecoveryReason(NavigationRecoveryReason.AlternateLocalCandidate);
                ClearRoomSweepDestination();
                if (TrySetRoomSweepTransitDestinationWithGlobalAlternates(
                        RoomSweepGlobalObjectiveInvalidationReason.NavigationRecoveryFailed,
                        out var recoveryReason))
                {
                    _navigation?.RecordRecoveryReason(recoveryReason == NavigationRecoveryReason.None
                        ? NavigationRecoveryReason.AlternateLocalCandidate
                        : recoveryReason);
                    return;
                }

                ActivateRoomSweepPatrolFallback();
                return;
            }

            ActivateRoomSweepPatrolFallback();
        }

        private void HandleSearchNavigationFailure(NavigationFailureReason failureReason)
        {
            if (IsSearchRetryableFailure(failureReason)
                && HasRecoveryBudgetForCurrentObjective()
                && TryIssueNavigationRecoveryRepath(ToSameObjectiveRecoveryReason(failureReason)))
            {
                return;
            }

            searchCandidateNodeId = -1;
            _blackboard.DestinationSpatialNodeId = -1;
            _navigation?.Stop();
            _navigation?.RecordRecoveryReason(NavigationRecoveryReason.AlternateLocalCandidate);
            _navigationObjectiveKey = StalkerNavigationObjectiveKey.None;
            ResetNavigationRecoveryBudget();
            _searchCandidatePlanningExhausted = false;
            if (!TryPlanNextSearchCandidate())
            {
                _navigation?.Stop();
                _navigation?.RecordRecoveryReason(NavigationRecoveryReason.None);
                return;
            }

            _navigation?.RecordRecoveryReason(NavigationRecoveryReason.AlternateLocalCandidate);
        }

        private void HandleChaseNavigationFailure(NavigationFailureReason failureReason)
        {
            if (IsChaseSameObjectiveRetryableFailure(failureReason)
                && HasRecoveryBudgetForCurrentObjective())
            {
                if (TryIssueNavigationRecoveryRepath(ToSameObjectiveRecoveryReason(failureReason)))
                {
                    return;
                }
            }

            if (IsChaseSameObjectiveRetryableFailure(failureReason)
                && !HasRecoveryBudgetForCurrentObjective())
            {
                EnterSearch();
            }
        }

        private void HandleTopologyBlockedByDoor()
        {
            _navigation?.RecordRecoveryReason(NavigationRecoveryReason.TopologyChangedRepath);
            switch (currentState)
            {
                case StalkerState.PATROL:
                    HandlePatrolNavigationFailure(NavigationFailureReason.DoorBlocked);
                    break;
                case StalkerState.SEARCH:
                    HandleSearchNavigationFailure(NavigationFailureReason.DoorBlocked);
                    break;
                case StalkerState.CHASE:
                    HandleChaseNavigationFailure(NavigationFailureReason.DoorBlocked);
                    break;
            }
        }

        private bool IsTopologyEdgeRelevantToCurrentNavigation(RegionId from, RegionId to)
        {
            if (_regionGraph == null || !IsLocomotionState())
            {
                return false;
            }

            if (!TryGetCurrentNavigationRegions(out var currentRegion, out var destinationRegion)
                || currentRegion == destinationRegion)
            {
                return false;
            }

            if (TryActiveNavigationPathUsesTopologyEdge(from, to))
            {
                return true;
            }

            var routeCursor = currentRegion;
            var maxHops = _regionGraph.Regions?.Count ?? 0;
            for (var hop = 0; hop < maxHops; hop++)
            {
                if (!_regionGraph.TryGetNextRegionOnRoute(routeCursor, destinationRegion, out var nextRegion)
                    || !nextRegion.IsValid)
                {
                    return false;
                }

                if ((routeCursor == from && nextRegion == to)
                    || (routeCursor == to && nextRegion == from))
                {
                    return true;
                }

                if (nextRegion == destinationRegion)
                {
                    return false;
                }

                routeCursor = nextRegion;
            }

            return false;
        }

        private bool TryGetCurrentNavigationRegions(out RegionId currentRegion, out RegionId destinationRegion)
        {
            currentRegion = RegionId.Invalid;
            destinationRegion = RegionId.Invalid;

            if (_spatialPatrolGraph == null
                || _regionGraph == null
                || !TryResolveNearestSpatialNode(transform.position, out var currentNodeId)
                || !_regionGraph.TryGetRegionForNode(currentNodeId, out currentRegion))
            {
                return false;
            }

            switch (currentState)
            {
                case StalkerState.CHASE:
                    return _hasLastChaseRequestedDestination
                        && TryResolveNearestSpatialNode(_lastChaseRequestedDestination, out var chaseDestinationNodeId)
                        && _regionGraph.TryGetRegionForNode(chaseDestinationNodeId, out destinationRegion);
                case StalkerState.SEARCH:
                    if (searchCandidateNodeId >= 0)
                    {
                        return _regionGraph.TryGetRegionForNode(searchCandidateNodeId, out destinationRegion);
                    }

                    return _searchContext != null
                        && TryResolveNearestSpatialNode(_searchContext.SearchOriginLKP, out var searchOriginNodeId)
                        && _regionGraph.TryGetRegionForNode(searchOriginNodeId, out destinationRegion);
                case StalkerState.PATROL:
                    return _blackboard.DestinationSpatialNodeId >= 0
                        && _regionGraph.TryGetRegionForNode(_blackboard.DestinationSpatialNodeId, out destinationRegion);
                default:
                    return false;
            }
        }

        private bool TryActiveNavigationPathUsesTopologyEdge(RegionId from, RegionId to)
        {
            if (_navigation == null
                || !_navigation.TryGetCurrentPathCorners(out var corners)
                || corners == null
                || corners.Length < 2)
            {
                return false;
            }

            return TryPathSegmentsUseTopologyEdge(corners, from, to);
        }

        private bool TryPathSegmentsUseTopologyEdge(IReadOnlyList<Vector3> corners, RegionId from, RegionId to)
        {
            if (corners == null || corners.Count < 2)
            {
                return false;
            }

            var previousRegion = RegionId.Invalid;
            for (var segmentIndex = 0; segmentIndex < corners.Count - 1; segmentIndex++)
            {
                var start = corners[segmentIndex];
                var end = corners[segmentIndex + 1];
                var segmentLength = Vector3.Distance(start, end);
                var sampleCount = Mathf.Clamp(
                    Mathf.CeilToInt(segmentLength / TopologyPathSegmentSampleSpacing),
                    1,
                    MaxTopologyPathSegmentSamples);

                for (var sample = 0; sample <= sampleCount; sample++)
                {
                    if (segmentIndex > 0 && sample == 0)
                    {
                        continue;
                    }

                    var point = Vector3.Lerp(start, end, (float)sample / sampleCount);
                    if (!TryResolveNearestSpatialNode(point, out var nodeId)
                        || !_regionGraph.TryGetRegionForNode(nodeId, out var regionId))
                    {
                        continue;
                    }

                    if (!previousRegion.IsValid)
                    {
                        previousRegion = regionId;
                        continue;
                    }

                    if (regionId == previousRegion)
                    {
                        continue;
                    }

                    if (!AreRegionsDirectlyAdjacent(previousRegion, regionId))
                    {
                        return false;
                    }

                    if (IsSameUndirectedRegionEdge(previousRegion, regionId, from, to))
                    {
                        return true;
                    }

                    previousRegion = regionId;
                }
            }

            return false;
        }

        private bool AreRegionsDirectlyAdjacent(RegionId a, RegionId b)
        {
            return _regionGraph != null
                && a.IsValid
                && b.IsValid
                && (_regionGraph.IsEdgeTraversable(a, b)
                    || _regionGraph.IsEdgeTraversable(b, a));
        }

        private static bool IsSameUndirectedRegionEdge(RegionId a, RegionId b, RegionId from, RegionId to)
        {
            return (a == from && b == to)
                || (a == to && b == from);
        }

        private static NavigationRecoveryReason ToSameObjectiveRecoveryReason(NavigationFailureReason failureReason)
        {
            switch (failureReason)
            {
                case NavigationFailureReason.DoorBlocked:
                    return NavigationRecoveryReason.TopologyChangedRepath;
                case NavigationFailureReason.PathStale:
                    return NavigationRecoveryReason.PathStaleRepath;
                default:
                    return NavigationRecoveryReason.RetryLogicalObjective;
            }
        }

        private static bool IsSearchRetryableFailure(NavigationFailureReason failureReason)
        {
            return IsNavigationExecutionRetryableFailure(failureReason);
        }

        private static bool IsSearchCandidateSpecificDestinationFailure(NavigationEvaluationStatus status)
        {
            return status == NavigationEvaluationStatus.DestinationInvalid
                || status == NavigationEvaluationStatus.Partial
                || status == NavigationEvaluationStatus.Invalid;
        }

        private static bool IsChaseSameObjectiveRetryableFailure(NavigationFailureReason failureReason)
        {
            return IsNavigationExecutionRetryableFailure(failureReason)
                || failureReason == NavigationFailureReason.PathPartial
                || failureReason == NavigationFailureReason.PathInvalid;
        }

        private static bool IsNavigationAgentUnavailableFailure(NavigationFailureReason failureReason)
        {
            return failureReason == NavigationFailureReason.AgentUnavailable
                || failureReason == NavigationFailureReason.AgentNotOnNavMesh;
        }

        private static bool IsNavigationExecutionRetryableFailure(NavigationFailureReason failureReason)
        {
            return failureReason == NavigationFailureReason.PathStale
                || failureReason == NavigationFailureReason.NoProgress
                || failureReason == NavigationFailureReason.Stuck
                || failureReason == NavigationFailureReason.PathPendingTimeout
                || failureReason == NavigationFailureReason.DoorBlocked;
        }

        private static NavigationFailureReason ResolveNavigationFailureReason(
            NavigationPathStatus pathStatus,
            NavigationExecutionStatus executionStatus)
        {
            switch (pathStatus)
            {
                case NavigationPathStatus.AgentUnavailable:
                    return NavigationFailureReason.AgentUnavailable;
                case NavigationPathStatus.AgentNotOnNavMesh:
                    return NavigationFailureReason.AgentNotOnNavMesh;
                case NavigationPathStatus.Partial:
                    return NavigationFailureReason.PathPartial;
                case NavigationPathStatus.Invalid:
                    return NavigationFailureReason.PathInvalid;
                case NavigationPathStatus.Stale:
                    return NavigationFailureReason.PathStale;
                case NavigationPathStatus.Pending:
                    return NavigationFailureReason.PathPendingTimeout;
            }

            return executionStatus == NavigationExecutionStatus.Stuck
                ? NavigationFailureReason.Stuck
                : NavigationFailureReason.NoProgress;
        }

        private static GlobalPatrolObjectiveInvalidationReason ToGlobalInvalidationReason(NavigationFailureReason failureReason)
        {
            return failureReason == NavigationFailureReason.DoorBlocked
                || failureReason == NavigationFailureReason.PathStale
                    ? GlobalPatrolObjectiveInvalidationReason.TopologyChanged
                    : GlobalPatrolObjectiveInvalidationReason.NavigationRecoveryFailed;
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
                    if (_searchContext != null && searchCandidateNodeId >= 0 && _spatialPatrolGraph != null && _spatialPatrolGraph.TryGetNode(searchCandidateNodeId, out var searchNode))
                    {
                        destination = searchNode.Position;
                        return true;
                    }

                    if (_searchContext != null)
                    {
                        destination = _searchContext.SearchOriginLKP;
                        return true;
                    }

                    return false;
                default:
                    return false;
            }
        }

        private bool TryGetCurrentPatrolRecoveryDestination(out Vector3 destination)
        {
            destination = default;
            if ((patrolMode == StalkerPatrolMode.DynamicSpatial
                    || patrolMode == StalkerPatrolMode.ConfidenceSpatial
                    || patrolMode == StalkerPatrolMode.RoomSweepSpatial)
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
            _hasPreviousChaseObservation = false;
            _previousChaseObservedPosition = default;
            _previousChaseObservationTimeSeconds = 0f;
        }

        private void ResetNavigationRecoveryBudget()
        {
            _navigationRecoveryAttemptUsed = false;
        }

        private void SetNavigationObjective(StalkerNavigationObjectiveKey objectiveKey)
        {
            if (!_navigationObjectiveKey.Equals(objectiveKey))
            {
                _navigationObjectiveKey = objectiveKey;
                ResetNavigationRecoveryBudget();
            }
        }

        private void ClearNavigationObjective()
        {
            _navigationObjectiveKey = StalkerNavigationObjectiveKey.None;
            ResetNavigationRecoveryBudget();
            _navigation?.RecordRecoveryReason(NavigationRecoveryReason.None);
        }

        private bool HasRecoveryBudgetForCurrentObjective()
        {
            return !_navigationRecoveryAttemptUsed;
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
            if (patrolMode == StalkerPatrolMode.RoomSweepSpatial)
            {
                ResetRoomSweepFallbackState();
                _roomSweepPlanner?.ClearRejectedProbes();

                if (SetRoomSweepPatrolDestination())
                {
                    return;
                }

                ActivateRoomSweepPatrolFallback();
                return;
            }

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

            if (patrolMode == StalkerPatrolMode.ConfidenceSpatial)
            {
                _canonicalPatrolFallbackActive = false;
                regionGraphFallbackReason = RegionGraphFallbackReason.None;

                if (SetCanonicalPatrolDestinationWithGlobalAlternates())
                {
                    return;
                }

                ActivateCanonicalPatrolFallback();
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
                SetNavigationObjective(new StalkerNavigationObjectiveKey(
                    StalkerNavigationObjectiveKind.FixedWaypoint,
                    pointIndex,
                    -1,
                    -1));
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

        private bool EnsureCanonicalPatrolInitialized()
        {
            if (_globalPatrolPlanner != null && _localPatrolSelector != null)
            {
                return true;
            }

            if (!EnsureSpatialGraphBuilt())
            {
                regionGraphFallbackReason = RegionGraphFallbackReason.MissingRegionGraph;
                return false;
            }

            if (regionGraphAsset == null)
            {
                regionGraphFallbackReason = RegionGraphFallbackReason.MissingRegionGraph;
                return false;
            }

            _regionGraph = regionGraphAsset.BuildRuntimeGraph();
            var validation = RegionGraph.Validate(_regionGraph, _spatialPatrolGraph);
            if (validation != RegionGraphValidationFailure.None)
            {
                regionGraphFallbackReason = validation == RegionGraphValidationFailure.SpatialGraphCompatibilityMismatch
                    ? RegionGraphFallbackReason.SpatialGraphCompatibilityMismatch
                    : RegionGraphFallbackReason.MalformedRegionGraph;
                _regionGraph = null;
                return false;
            }

            _coverageMemory = new CoverageMemory(_spatialPatrolGraph.NodeCount, _regionGraph);
            _spatialPatrolMemory = new SpatialPatrolMemory(_coverageMemory);
            _globalPatrolPlanner = new GlobalPatrolPlanner(_regionGraph, _coverageMemory);
            _localPatrolSelector = new LocalPatrolSelector(
                _spatialPatrolGraph,
                _regionGraph,
                _coverageMemory,
                candidateBfsDepth,
                HasCompletePathTo);
            _searchPlanner = new StalkerSearchPlanner(
                _spatialPatrolGraph,
                _regionGraph,
                _coverageMemory,
                EvaluateSearchPath);
            return true;
        }

        private bool EnsureRoomSweepPatrolInitialized()
        {
            if (_roomSweepCoverageMemory != null
                && _roomSweepPlanner != null
                && _roomSweepGlobalPlanner != null)
            {
                return true;
            }

            if (!EnsureCanonicalPatrolInitialized())
            {
                return false;
            }

            _roomSweepCoverageMemory = new RoomSweepCoverageMemory();
            _roomSweepPlanner = _hasPatrolVariationSeed
                ? new RoomSweepPlanner(
                    _spatialPatrolGraph,
                    _regionGraph,
                    _roomSweepCoverageMemory,
                    _patrolVariationSeed)
                : new RoomSweepPlanner(
                    _spatialPatrolGraph,
                    _regionGraph,
                    _roomSweepCoverageMemory);
            _roomSweepGlobalPlanner = _hasPatrolVariationSeed
                ? new RoomSweepGlobalPlanner(
                    _regionGraph,
                    _roomSweepCoverageMemory,
                    _patrolVariationSeed,
                    _patrolNearOptimalHopSlack)
                : new RoomSweepGlobalPlanner(
                    _regionGraph,
                    _roomSweepCoverageMemory);
            return true;
        }

        private bool EnsureSpatialGraphBuilt()
        {
            if (_spatialPatrolGraph != null && !_spatialPatrolGraph.IsEmpty)
            {
                return true;
            }

            _spatialPatrolGraph = NavMeshSpatialGraphBuilder.Build();
            return _spatialPatrolGraph != null && !_spatialPatrolGraph.IsEmpty;
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
                _rejectedDynamicPatrolNodeIds.Clear();
            }

            plannerRunCount++;
            if (!_spatialPatrolPlanner.TrySelectDestination(
                currentNodeId,
                _blackboard.PreviousSpatialNodeId,
                CurrentSimulationTimeSeconds,
                _rejectedDynamicPatrolNodeIds,
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

            SetNavigationObjective(new StalkerNavigationObjectiveKey(
                StalkerNavigationObjectiveKind.DynamicSpatialNode,
                plan.DestinationNode.Id,
                -1,
                -1));
            _blackboard.DestinationSpatialNodeId = plan.DestinationNode.Id;
            lastPatrolScore = plan.Score;
            candidateCount = plan.CandidateCount;
            SyncDynamicPatrolDebugFields();
            return true;
        }

        private bool SetCanonicalPatrolDestination()
        {
            if (!EnsureCanonicalPatrolInitialized())
            {
                return false;
            }

            if (!TryResolveNearestSpatialNode(transform.position, out var currentNodeId)
                || !_regionGraph.TryGetRegionForNode(currentNodeId, out var currentRegionId))
            {
                regionGraphFallbackReason = RegionGraphFallbackReason.InvalidNodeToRegionMap;
                ClearDynamicPatrolDestination();
                return false;
            }

            UpdateCurrentRegion(currentRegionId);
            _blackboard.CurrentSpatialNodeId = currentNodeId;

            if (!_globalPatrolPlanner.TryGetOrCreateObjective(
                    currentRegionId,
                    _previousRegionId,
                    _rejectedCanonicalGlobalRegionIds,
                    out var objective))
            {
                regionGraphFallbackReason = RegionGraphFallbackReason.NoReachableRegionObjective;
                return false;
            }

            if (!_localPatrolSelector.TrySelect(
                    currentNodeId,
                    _blackboard.PreviousSpatialNodeId,
                    objective.NextRegionId,
                    _rejectedCanonicalLocalNodeIds,
                    out var selection))
            {
                LogCanonicalLocalSelectionFailure(currentNodeId, objective);
                regionGraphFallbackReason = RegionGraphFallbackReason.NoCompleteLocalPath;
                return false;
            }

            var result = _navigation.RequestDestination(selection.DestinationNode.Position);
            if (!result.IsAccepted)
            {
                regionGraphFallbackReason = RegionGraphFallbackReason.NoCompleteLocalPath;
                return false;
            }

            SetNavigationObjective(new StalkerNavigationObjectiveKey(
                StalkerNavigationObjectiveKind.ConfidenceSpatialNode,
                selection.DestinationNode.Id,
                objective.TargetRegionId.Value,
                -1));
            _blackboard.DestinationSpatialNodeId = selection.DestinationNode.Id;
            lastPatrolScore = selection.Score;
            candidateCount = selection.CandidateCount;
            canonicalCurrentRegionId = currentRegionId.Value;
            canonicalObjectiveRegionId = objective.TargetRegionId.Value;
            canonicalNextRegionId = objective.NextRegionId.Value;
            regionGraphFallbackReason = RegionGraphFallbackReason.None;
            SyncDynamicPatrolDebugFields();
            return true;
        }

        private bool SetRoomSweepPatrolDestination()
        {
            if (!EnsureRoomSweepPatrolInitialized())
            {
                return false;
            }

            if (!ResolveCurrentRoomSweepLocation(out var currentNodeId, out var currentRegionId))
            {
                regionGraphFallbackReason = RegionGraphFallbackReason.InvalidNodeToRegionMap;
                ClearRoomSweepDestination();
                return false;
            }

            LogRoomSweepPlanningLocation(currentNodeId, currentRegionId);
            var currentRoomResult = TryBeginOrContinueCurrentRoomSweep(currentNodeId, currentRegionId);
            LogRoomSweepCurrentRoomResult(currentNodeId, currentRegionId, currentRoomResult);
            switch (currentRoomResult)
            {
                case RoomSweepCurrentRoomResult.DestinationSet:
                    return true;
                case RoomSweepCurrentRoomResult.NotApplicable:
                    return TrySetRoomSweepTransitDestinationWithGlobalAlternates(out _);
                case RoomSweepCurrentRoomResult.RoomCleared:
                    CompleteRoomSweepCurrentRoomObjective(currentRegionId);
                    return TrySetRoomSweepTransitDestinationWithGlobalAlternates(
                        RoomSweepGlobalObjectiveInvalidationReason.TargetCleared,
                        out _);
                case RoomSweepCurrentRoomResult.Exhausted:
                case RoomSweepCurrentRoomResult.Failed:
                default:
                    return false;
            }
        }

        private void LogRoomSweepSuppressLegacyGateOnce()
        {
            if (_roomSweepSuppressLegacyGateLogged)
            {
                return;
            }

            _roomSweepSuppressLegacyGateLogged = true;
            LogDiagnosticWarning(
                "[STK ROOM SWEEP GATE DIAG] update-suppress-legacy-gate "
                + $"SuppressLegacyUpdateSimulation={SuppressLegacyUpdateSimulation} "
                + $"enabled={enabled} "
                + $"gameObject.activeInHierarchy={(gameObject != null && gameObject.activeInHierarchy)} "
                + $"currentState={currentState} "
                + $"patrolMode={patrolMode}");
        }

        private void LogRoomSweepNavigationGateOnce()
        {
            var navigationUsable = CanUseNavigation();
            if (navigationUsable)
            {
                if (_roomSweepNavigationGateUsableLogged)
                {
                    return;
                }

                _roomSweepNavigationGateUsableLogged = true;
            }
            else
            {
                if (_roomSweepNavigationGateBlockedLogged)
                {
                    return;
                }

                _roomSweepNavigationGateBlockedLogged = true;
            }

            var agent = GetComponent<NavMeshAgent>();
            var agentExists = agent != null;
            LogDiagnosticWarning(
                "[STK ROOM SWEEP GATE DIAG] navigation-gate "
                + $"navigationUsable={navigationUsable} "
                + $"navigationNull={_navigation == null} "
                + $"navMeshAgentComponentExists={agentExists} "
                + $"agentEnabled={(agentExists && agent.enabled)} "
                + $"agentIsOnNavMesh={(agentExists && agent.isOnNavMesh)} "
                + $"agentIsActiveAndEnabled={(agentExists && agent.isActiveAndEnabled)} "
                + $"transformPosition={transform.position} "
                + $"currentState={currentState} "
                + $"patrolMode={patrolMode}");
        }

        private bool TryCancelCompletedActiveRoomSweepProbe()
        {
            if (!TryProcessActiveRoomSweepProbeVisualCoverage(out var clearedRoomRegionId))
            {
                return false;
            }

            CompleteRoomSweepCurrentRoomObjective(clearedRoomRegionId);
            _navigation?.Stop();
            ClearRoomSweepDestination();
            return true;
        }

        private bool TryProcessActiveRoomSweepProbeVisualCoverage(out RegionId clearedRoomRegionId)
        {
            clearedRoomRegionId = RegionId.Invalid;
            if (_navigationObjectiveKey.Kind != StalkerNavigationObjectiveKind.RoomSweepProbe
                || _roomSweepPlanner == null
                || !_roomSweepPlanner.CurrentRegionId.IsValid
                || !ResolveCurrentRoomSweepLocation(out var currentNodeId, out var currentRegionId)
                || _roomSweepPlanner.CurrentRegionId != currentRegionId
                || !IsRoomSweepRoomRegion(currentRegionId)
                || _roomSweepCoverageMemory.IsRegionCleared(currentRegionId))
            {
                return false;
            }

            UpdateRoomSweepVisualCoverage(currentRegionId);
            LogRoomSweepResidualProbesOnce(currentNodeId, currentRegionId);
            if (!_roomSweepPlanner.IsFullyObserved)
            {
                return false;
            }

            _roomSweepCoverageMemory.MarkRegionCleared(currentRegionId);
            _roomSweepPlanner.ClearRejectedProbes();
            clearedRoomRegionId = currentRegionId;
            return true;
        }

        private RoomSweepCurrentRoomResult TryBeginOrContinueCurrentRoomSweep(int currentNodeId, RegionId currentRegionId)
        {
            if (!IsRoomSweepRoomRegion(currentRegionId))
            {
                return RoomSweepCurrentRoomResult.NotApplicable;
            }

            if (_roomSweepCoverageMemory.IsRegionCleared(currentRegionId))
            {
                return RoomSweepCurrentRoomResult.NotApplicable;
            }

            if (_roomSweepPlanner.CurrentRegionId != currentRegionId
                && !_roomSweepPlanner.TryBeginRegion(currentRegionId))
            {
                return RoomSweepCurrentRoomResult.Failed;
            }

            UpdateRoomSweepVisualCoverage(currentRegionId);
            LogRoomSweepResidualProbesOnce(currentNodeId, currentRegionId);
            if (_roomSweepPlanner.IsFullyObserved)
            {
                _roomSweepCoverageMemory.MarkRegionCleared(currentRegionId);
                _roomSweepPlanner.ClearRejectedProbes();
                return RoomSweepCurrentRoomResult.RoomCleared;
            }

            if (TrySetRoomSweepProbeDestination(currentNodeId, currentRegionId))
            {
                return RoomSweepCurrentRoomResult.DestinationSet;
            }

            if (_roomSweepPlanner.IsExhausted)
            {
                return RoomSweepCurrentRoomResult.Exhausted;
            }

            regionGraphFallbackReason = RegionGraphFallbackReason.NoCompleteLocalPath;
            return RoomSweepCurrentRoomResult.Failed;
        }

        private void CompleteRoomSweepCurrentRoomObjective(RegionId currentRegionId)
        {
            if (_roomSweepGlobalPlanner == null)
            {
                return;
            }

            var objective = _roomSweepGlobalPlanner.CurrentObjective;
            if (objective.IsValid
                && (objective.TargetRoomRegionId == currentRegionId
                    || _roomSweepCoverageMemory.IsRegionCleared(objective.TargetRoomRegionId)))
            {
                _roomSweepGlobalPlanner.Invalidate(RoomSweepGlobalObjectiveInvalidationReason.TargetCleared);
            }

            _rejectedRoomSweepTransitNodeIds.Clear();
        }

        private bool TrySetRoomSweepProbeDestination(int currentNodeId, RegionId currentRoomRegionId)
        {
            var maxAttempts = _spatialPatrolGraph?.NodeCount ?? 0;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (!_roomSweepPlanner.TrySelectNextProbe(currentNodeId, out var probeNodeId)
                    || !_spatialPatrolGraph.TryGetNode(probeNodeId, out var probeNode))
                {
                    return false;
                }

                if (probeNodeId == currentNodeId)
                {
                    // Previous immediate self-probe rejection kept as reference:
                    // RejectRoomSweepProbeWithDiagnostic(
                    //     probeNodeId,
                    //     "SELF_PROBE",
                    //     NavigationFailureReason.None);
                    // continue;
                    BeginRoomSweepSelfProbeScan(probeNodeId, currentRoomRegionId);
                    UnityEngine.Debug.Log(
                        $"[STK_PATROL][LOCAL] " +
                        $"region={currentRoomRegionId.Value} " +
                        $"probeNode={probeNodeId} " +
                        $"seed={GetPatrolVariationDiagnosticValue()}");
                    return true;
                }

                var result = _navigation.RequestDestination(probeNode.Position);
                if (!result.IsAccepted)
                {
                    LogRoomSweepRequestDestinationResult(result.IsAccepted);
                    RejectRoomSweepProbeWithDiagnostic(
                        probeNodeId,
                        "REQUEST_DESTINATION_REJECTED",
                        _navigation.CurrentFailureReason);
                    regionGraphFallbackReason = RegionGraphFallbackReason.NoCompleteLocalPath;
                    continue;
                }

                LogRoomSweepProbeSelected(currentNodeId, probeNodeId, probeNode.Position);
                UnityEngine.Debug.Log(
                    $"[STK_PATROL][LOCAL] " +
                    $"region={currentRoomRegionId.Value} " +
                    $"probeNode={probeNodeId} " +
                    $"seed={GetPatrolVariationDiagnosticValue()}");

                SetNavigationObjective(new StalkerNavigationObjectiveKey(
                    StalkerNavigationObjectiveKind.RoomSweepProbe,
                    probeNodeId,
                    currentRoomRegionId.Value,
                    -1));
                _blackboard.DestinationSpatialNodeId = probeNodeId;
                lastPatrolScore = 0f;
                candidateCount = 1;
                canonicalCurrentRegionId = currentRoomRegionId.Value;
                canonicalObjectiveRegionId = currentRoomRegionId.Value;
                canonicalNextRegionId = currentRoomRegionId.Value;
                regionGraphFallbackReason = RegionGraphFallbackReason.None;
                SyncDynamicPatrolDebugFields();
                LogRoomSweepRequestDestinationResult(result.IsAccepted);
                return true;
            }

            return false;
        }

        private void BeginRoomSweepSelfProbeScan(int probeNodeId, RegionId roomRegionId)
        {
            ClearRoomSweepSelfProbeScan();

            _roomSweepSelfProbeScanActive = true;
            _roomSweepSelfProbeScanNodeId = probeNodeId;
            _roomSweepSelfProbeScanRegionId = roomRegionId;
            _roomSweepSelfProbeScanAccumulatedDegrees = 0f;

            var navAgent = GetComponent<NavMeshAgent>();
            if (navAgent != null)
            {
                _roomSweepSelfProbeScanHasAgentRotationOwnership = true;
                _roomSweepSelfProbeScanPreviousAgentUpdateRotation = navAgent.updateRotation;
                navAgent.updateRotation = false;
            }

            ClearRoomSweepDestination();
        }

        private void TickRoomSweepSelfProbeScan()
        {
            if (!_roomSweepSelfProbeScanActive)
            {
                return;
            }

            if (!ResolveCurrentRoomSweepLocation(out var currentNodeId, out var currentRegionId)
                || currentRegionId != _roomSweepSelfProbeScanRegionId
                || _roomSweepPlanner == null
                || !_roomSweepPlanner.HasActiveRegion
                || _roomSweepPlanner.CurrentRegionId != currentRegionId
                || !IsRoomSweepRoomRegion(currentRegionId)
                || _roomSweepCoverageMemory == null)
            {
                ClearRoomSweepSelfProbeScan();
                ClearRoomSweepDestination();
                return;
            }

            var navAgent = GetComponent<NavMeshAgent>();
            var deltaTime = Mathf.Max(0f, CurrentSimulationDeltaSeconds);
            var deltaDegrees = navAgent != null
                ? Mathf.Max(0f, navAgent.angularSpeed) * deltaTime
                : 0f;

            if (deltaDegrees > 0f)
            {
                var previousYaw = transform.eulerAngles.y;
                transform.Rotate(0f, deltaDegrees, 0f, Space.World);
                var currentYaw = transform.eulerAngles.y;
                var actualYawDelta = Mathf.Abs(Mathf.DeltaAngle(previousYaw, currentYaw));
                // Previous requested-angle accounting kept as reference:
                // _roomSweepSelfProbeScanAccumulatedDegrees += deltaDegrees;
                _roomSweepSelfProbeScanAccumulatedDegrees += actualYawDelta;
            }

            UpdateRoomSweepVisualCoverage(currentRegionId);
            LogRoomSweepResidualProbesOnce(currentNodeId, currentRegionId);

            if (_roomSweepCoverageMemory.IsObserved(currentRegionId, _roomSweepSelfProbeScanNodeId))
            {
                ClearRoomSweepSelfProbeScan();
                ClearRoomSweepDestination();
                return;
            }

            if (_roomSweepSelfProbeScanAccumulatedDegrees >= 360f)
            {
                var rejectedProbeNodeId = _roomSweepSelfProbeScanNodeId;
                ClearRoomSweepSelfProbeScan();
                ClearRoomSweepDestination();
                RejectRoomSweepProbeWithDiagnostic(
                    rejectedProbeNodeId,
                    "SELF_PROBE_FULL_SCAN_UNSEEN",
                    NavigationFailureReason.None);
            }
        }

        private void ClearRoomSweepSelfProbeScan()
        {
            if (_roomSweepSelfProbeScanHasAgentRotationOwnership)
            {
                var navAgent = GetComponent<NavMeshAgent>();
                if (navAgent != null)
                {
                    navAgent.updateRotation = _roomSweepSelfProbeScanPreviousAgentUpdateRotation;
                }
            }

            _roomSweepSelfProbeScanActive = false;
            _roomSweepSelfProbeScanNodeId = -1;
            _roomSweepSelfProbeScanRegionId = RegionId.Invalid;
            _roomSweepSelfProbeScanAccumulatedDegrees = 0f;
            _roomSweepSelfProbeScanHasAgentRotationOwnership = false;
            _roomSweepSelfProbeScanPreviousAgentUpdateRotation = false;
        }

        private bool TrySetRoomSweepTransitDestinationWithGlobalAlternates(out NavigationRecoveryReason recoveryReason)
        {
            return TrySetRoomSweepTransitDestinationWithGlobalAlternates(
                RoomSweepGlobalObjectiveInvalidationReason.NavigationRecoveryFailed,
                out recoveryReason);
        }

        private bool TrySetRoomSweepTransitDestinationWithGlobalAlternates(
            RoomSweepGlobalObjectiveInvalidationReason invalidationReason,
            out NavigationRecoveryReason recoveryReason)
        {
            recoveryReason = NavigationRecoveryReason.None;
            if (SetRoomSweepTransitDestination())
            {
                return true;
            }

            if (regionGraphFallbackReason != RegionGraphFallbackReason.NoCompleteLocalPath)
            {
                return false;
            }

            var maxGlobalAttempts = _regionGraph?.Regions?.Count ?? 0;
            for (var attempt = 0; attempt < maxGlobalAttempts; attempt++)
            {
                var objective = _roomSweepGlobalPlanner?.CurrentObjective ?? RoomSweepGlobalObjective.Invalid;
                if (!objective.TargetRoomRegionId.IsValid)
                {
                    return false;
                }

                _rejectedRoomSweepGlobalRegionIds.Add(objective.TargetRoomRegionId);
                _roomSweepGlobalPlanner?.Invalidate(invalidationReason);
                _rejectedRoomSweepTransitNodeIds.Clear();
                recoveryReason = NavigationRecoveryReason.AlternateGlobalObjective;

                if (SetRoomSweepTransitDestination())
                {
                    return true;
                }

                if (regionGraphFallbackReason != RegionGraphFallbackReason.NoCompleteLocalPath)
                {
                    return false;
                }
            }

            return false;
        }

        private bool SetRoomSweepTransitDestination()
        {
            if (!ResolveCurrentRoomSweepLocation(out var currentNodeId, out var currentRegionId))
            {
                regionGraphFallbackReason = RegionGraphFallbackReason.InvalidNodeToRegionMap;
                return false;
            }

            var previousObjective =
                _roomSweepGlobalPlanner.CurrentObjective;

            if (!_roomSweepGlobalPlanner.TryGetOrCreateObjective(
                    currentRegionId,
                    _rejectedRoomSweepGlobalRegionIds,
                    out var objective))
            {
                var currentRoomResult = _roomSweepGlobalPlanner.LastInvalidationReason == RoomSweepGlobalObjectiveInvalidationReason.TargetReached
                    ? TryBeginOrContinueCurrentRoomSweep(currentNodeId, currentRegionId)
                    : RoomSweepCurrentRoomResult.NotApplicable;
                if (currentRoomResult == RoomSweepCurrentRoomResult.DestinationSet
                    || currentRoomResult == RoomSweepCurrentRoomResult.RoomCleared)
                {
                    return true;
                }

                regionGraphFallbackReason = RegionGraphFallbackReason.NoReachableRegionObjective;
                return false;
            }

            var maxAttempts = _spatialPatrolGraph?.NodeCount ?? 0;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (!_localPatrolSelector.TrySelect(
                        currentNodeId,
                        _blackboard.PreviousSpatialNodeId,
                        objective.NextRegionId,
                        _rejectedRoomSweepTransitNodeIds,
                        out var selection))
                {
                    regionGraphFallbackReason = RegionGraphFallbackReason.NoCompleteLocalPath;
                    return false;
                }

                var result = _navigation.RequestDestination(selection.DestinationNode.Position);
                if (!result.IsAccepted)
                {
                    LogRoomSweepRequestDestinationResult(result.IsAccepted);
                    _rejectedRoomSweepTransitNodeIds.Add(selection.DestinationNode.Id);
                    regionGraphFallbackReason = RegionGraphFallbackReason.NoCompleteLocalPath;
                    continue;
                }

                if (!previousObjective.IsValid
                    || previousObjective.TargetRoomRegionId
                        != objective.TargetRoomRegionId
                    || previousObjective.NextRegionId
                        != objective.NextRegionId)
                {
                    UnityEngine.Debug.Log(
                        $"[STK_PATROL][GLOBAL] " +
                        $"currentRegion={currentRegionId.Value} " +
                        $"targetRoom={objective.TargetRoomRegionId.Value} " +
                        $"nextRegion={objective.NextRegionId.Value} " +
                        $"seed={GetPatrolVariationDiagnosticValue()}");
                }

                SetNavigationObjective(new StalkerNavigationObjectiveKey(
                    StalkerNavigationObjectiveKind.RoomSweepTransit,
                    selection.DestinationNode.Id,
                    objective.TargetRoomRegionId.Value,
                    -1));
                _blackboard.DestinationSpatialNodeId = selection.DestinationNode.Id;
                lastPatrolScore = selection.Score;
                candidateCount = selection.CandidateCount;
                canonicalCurrentRegionId = currentRegionId.Value;
                canonicalObjectiveRegionId = objective.TargetRoomRegionId.Value;
                canonicalNextRegionId = objective.NextRegionId.Value;
                regionGraphFallbackReason = RegionGraphFallbackReason.None;
                SyncDynamicPatrolDebugFields();
                LogRoomSweepRequestDestinationResult(result.IsAccepted);
                return true;
            }

            return false;
        }

        private void RejectRoomSweepProbeWithDiagnostic(
            int probeNodeId,
            string rejectionCategory,
            NavigationFailureReason navigationFailureReason)
        {
            if (_roomSweepPlanner == null)
            {
                return;
            }

            var rejectedProbeCountBefore = _roomSweepPlanner.RejectedProbeCount;
            var currentSpatialNodeId = -1;
            var currentRegionId = RegionId.Invalid;
            if (TryResolveNearestSpatialNode(transform.position, out currentSpatialNodeId)
                && _regionGraph != null)
            {
                _regionGraph.TryGetRegionForNode(currentSpatialNodeId, out currentRegionId);
            }

            var plannerCurrentRegionId = _roomSweepPlanner.CurrentRegionId;
            var probeAlreadyObserved = plannerCurrentRegionId.IsValid
                && _roomSweepCoverageMemory != null
                && _roomSweepCoverageMemory.IsObserved(plannerCurrentRegionId, probeNodeId);
            var rejectedProbe = _roomSweepPlanner.RejectProbe(probeNodeId);
            var rejectedProbeCountAfter = _roomSweepPlanner.RejectedProbeCount;

            LogDiagnosticWarning(
                "[STK ROOM SWEEP REJECT DIAG] probe-rejected "
                + $"probeNodeId={probeNodeId} "
                + $"rejectionCategory={rejectionCategory} "
                + $"currentSpatialNodeId={currentSpatialNodeId} "
                + $"currentRegionId={(currentRegionId.IsValid ? currentRegionId.Value : -1)} "
                + $"plannerCurrentRegionId={(plannerCurrentRegionId.IsValid ? plannerCurrentRegionId.Value : -1)} "
                + $"probeEqualsCurrent={probeNodeId == currentSpatialNodeId} "
                + $"probeAlreadyObserved={probeAlreadyObserved} "
                + $"rejectedProbeCountBefore={rejectedProbeCountBefore} "
                + $"rejectProbeResult={rejectedProbe} "
                + $"rejectedProbeCountAfter={rejectedProbeCountAfter} "
                + $"navigationFailureReason={navigationFailureReason} "
                + $"navigationPathStatus={(_navigation != null ? _navigation.GetPathStatus().ToString() : NavigationPathStatus.AgentUnavailable.ToString())} "
                + $"navigationExecutionStatus={(_navigation != null ? _navigation.GetExecutionStatus().ToString() : NavigationExecutionStatus.Failed.ToString())} "
                + $"destinationSpatialNodeId={_blackboard.DestinationSpatialNodeId} "
                + $"position={transform.position}");
        }

        private static string GetRoomSweepNavigationRejectionCategory(NavigationFailureReason failureReason)
        {
            switch (failureReason)
            {
                case NavigationFailureReason.NoProgress:
                    return "NO_PROGRESS";
                case NavigationFailureReason.PathPartial:
                    return "PATH_PARTIAL";
                case NavigationFailureReason.PathInvalid:
                    return "PATH_INVALID";
                default:
                    return "OTHER_NAV_FAILURE";
            }
        }

        private void LogRoomSweepPlanningLocation(int currentNodeId, RegionId currentRegionId)
        {
            var isRoom = IsRoomSweepRoomRegion(currentRegionId);
            GetRoomSweepCoverageSnapshot(
                currentRegionId,
                out var coverageKnown,
                out var isCleared,
                out var observed,
                out var total,
                out var remaining);
            LogDiagnosticWarning(
                "[STK ROOM SWEEP DIAG] planning-location "
                + $"currentSpatialNodeId={currentNodeId} "
                + $"currentRegionId={currentRegionId.Value} "
                + $"worldPosition={transform.position} "
                + $"isRoom={isRoom} "
                + $"coverageKnown={coverageKnown} "
                + $"roomCleared={isCleared} "
                + $"observed={observed} total={total} remaining={remaining}");
        }

        private void LogRoomSweepCurrentRoomResult(
            int currentNodeId,
            RegionId currentRegionId,
            RoomSweepCurrentRoomResult result)
        {
            GetRoomSweepCoverageSnapshot(
                currentRegionId,
                out _,
                out _,
                out var observed,
                out var total,
                out var remaining);
            LogDiagnosticWarning(
                "[STK ROOM SWEEP DIAG] current-room-result "
                + $"currentSpatialNodeId={currentNodeId} "
                + $"currentRegionId={currentRegionId.Value} "
                + $"result={result} "
                + $"plannerCurrentRegionId={(_roomSweepPlanner != null ? _roomSweepPlanner.CurrentRegionId.Value : -1)} "
                + $"plannerHasActiveRegion={(_roomSweepPlanner != null && _roomSweepPlanner.HasActiveRegion)} "
                + $"plannerIsFullyObserved={(_roomSweepPlanner != null && _roomSweepPlanner.IsFullyObserved)} "
                + $"plannerIsExhausted={(_roomSweepPlanner != null && _roomSweepPlanner.IsExhausted)} "
                + $"rejectedProbeCount={(_roomSweepPlanner?.RejectedProbeCount ?? 0)} "
                + $"observed={observed} total={total} remaining={remaining}");
        }

        private void LogRoomSweepResidualProbesOnce(int currentNodeId, RegionId currentRegionId)
        {
            if (_roomSweepPlanner == null
                || _roomSweepCoverageMemory == null
                || _spatialPatrolGraph == null
                || visionSensor == null
                || !_roomSweepPlanner.HasActiveRegion
                || _roomSweepPlanner.CurrentRegionId != currentRegionId
                || _roomSweepCoverageMemory.IsRegionCleared(currentRegionId))
            {
                return;
            }

            GetRoomSweepCoverageSnapshot(
                currentRegionId,
                out var coverageKnown,
                out _,
                out var observed,
                out var total,
                out var remaining);
            if (!coverageKnown
                || remaining <= 0
                || remaining > 50
                || _roomSweepResidualDiagLoggedRegionIds.Contains(currentRegionId.Value)
                || !_roomSweepCoverageMemory.TryGetUnobservedProbeNodeIds(currentRegionId, out var nodeIds))
            {
                return;
            }

            _roomSweepResidualDiagLoggedRegionIds.Add(currentRegionId.Value);
            var stalkerPosition = transform.position;
            LogDiagnosticWarning(
                "[STK ROOM SWEEP RESIDUAL DIAG] summary "
                + $"currentSpatialNodeId={currentNodeId} "
                + $"regionId={currentRegionId.Value} "
                + $"observed={observed} "
                + $"total={total} "
                + $"remaining={remaining} "
                + $"residualNodeCount={nodeIds.Count}");

            for (var i = 0; i < nodeIds.Count; i++)
            {
                var nodeId = nodeIds[i];
                if (!_spatialPatrolGraph.TryGetNode(nodeId, out var node))
                {
                    continue;
                }

                var observationPoint = visionSensor.GetObservationPointForGroundPoint(node.Position);
                var canSeePoint = visionSensor.CanSeePoint(observationPoint);
                LogDiagnosticWarning(
                    "[STK ROOM SWEEP RESIDUAL DIAG] probe "
                    + $"regionId={currentRegionId.Value} "
                    + $"spatialNodeId={nodeId} "
                    + $"worldPosition={node.Position} "
                    + $"groundProbePosition={node.Position} "
                    + $"observationPoint={observationPoint} "
                    + $"stalkerWorldPosition={stalkerPosition} "
                    + $"distanceFromStalker={Vector3.Distance(stalkerPosition, node.Position)} "
                    + $"deltaY={node.Position.y - stalkerPosition.y} "
                    + $"canSeePoint={canSeePoint}");
            }
        }

        private string GetPatrolVariationDiagnosticValue()
        {
            return _hasPatrolVariationSeed
                ? _patrolVariationSeed.ToString()
                : "LEGACY";
        }

        private void LogRoomSweepProbeSelected(int currentNodeId, int probeNodeId, Vector3 probePosition)
        {
            LogDiagnosticWarning(
                "[STK ROOM SWEEP DIAG] probe-selected "
                + $"currentSpatialNodeId={currentNodeId} "
                + $"selectedProbeNodeId={probeNodeId} "
                + $"selectedEqualsCurrent={probeNodeId == currentNodeId} "
                + $"selectedProbeWorldPosition={probePosition} "
                + $"distanceFromStalker={Vector3.Distance(transform.position, probePosition)}");
        }

        private void LogRoomSweepRequestDestinationResult(bool accepted)
        {
            var hasActiveDestination = _navigation != null && _navigation.HasActiveDestination;
            var hasArrived = hasActiveDestination && _navigation.HasArrived();
            LogDiagnosticWarning(
                "[STK ROOM SWEEP DIAG] request-destination-result "
                + $"accepted={accepted} "
                + $"destinationSpatialNodeId={_blackboard.DestinationSpatialNodeId} "
                + $"objectiveKind={_navigationObjectiveKey.Kind} "
                + $"hasActiveDestination={hasActiveDestination} "
                + $"hasArrived={hasArrived}");
        }

        private void LogRoomSweepDestinationReached(int destinationNodeId)
        {
            var resolvedCurrentNodeId = -1;
            var currentRegionId = RegionId.Invalid;
            if (TryResolveNearestSpatialNode(transform.position, out resolvedCurrentNodeId)
                && _regionGraph != null)
            {
                _regionGraph.TryGetRegionForNode(resolvedCurrentNodeId, out currentRegionId);
            }

            LogDiagnosticWarning(
                "[STK ROOM SWEEP DIAG] destination-reached "
                + $"objectiveKind={_navigationObjectiveKey.Kind} "
                + $"destinationNodeId={destinationNodeId} "
                + $"resolvedCurrentNodeId={resolvedCurrentNodeId} "
                + $"currentRegionId={currentRegionId.Value}");
        }

        private void LogRoomSweepFallbackActivated()
        {
            var currentNodeId = -1;
            var currentRegionId = RegionId.Invalid;
            if (TryResolveNearestSpatialNode(transform.position, out currentNodeId)
                && _regionGraph != null)
            {
                _regionGraph.TryGetRegionForNode(currentNodeId, out currentRegionId);
            }

            LogDiagnosticWarning(
                "[STK ROOM SWEEP DIAG] fallback-activated "
                + $"currentNode={currentNodeId} "
                + $"currentRegion={currentRegionId.Value} "
                + $"objectiveKind={_navigationObjectiveKey.Kind} "
                + $"destinationSpatialNodeId={_blackboard.DestinationSpatialNodeId}");
        }

        private void GetRoomSweepCoverageSnapshot(
            RegionId regionId,
            out bool coverageKnown,
            out bool isCleared,
            out int observed,
            out int total,
            out int remaining)
        {
            observed = _roomSweepCoverageMemory?.GetObservedCount(regionId) ?? 0;
            total = _roomSweepCoverageMemory?.GetTotalProbeCount(regionId) ?? 0;
            remaining = _roomSweepCoverageMemory?.GetRemainingProbeCount(regionId) ?? 0;
            isCleared = _roomSweepCoverageMemory != null && _roomSweepCoverageMemory.IsRegionCleared(regionId);
            coverageKnown = observed > 0 || total > 0 || remaining > 0 || isCleared;
        }

        private void LogCanonicalLocalSelectionFailure(
            int currentNodeId,
            GlobalPatrolObjective objective)
        {
            var currentRegion = RegionId.Invalid;
            var previousRegion = RegionId.Invalid;
            var previousNodeId = _blackboard.PreviousSpatialNodeId;
            if (_regionGraph != null)
            {
                _regionGraph.TryGetRegionForNode(currentNodeId, out currentRegion);
                if (previousNodeId >= 0)
                {
                    _regionGraph.TryGetRegionForNode(previousNodeId, out previousRegion);
                }
            }

            var totalNextRegionNodes = 0;
            var minHopToNextRegion = -1;
            var withinDepth = 0;
            var unrejectedWithinDepth = 0;
            var graph = _spatialPatrolGraph;
            if (graph != null && _regionGraph != null && currentNodeId >= 0 && currentNodeId < graph.NodeCount)
            {
                var distances = new int[graph.NodeCount];
                for (var i = 0; i < distances.Length; i++)
                {
                    distances[i] = -1;
                }

                var queue = new Queue<int>();
                distances[currentNodeId] = 0;
                queue.Enqueue(currentNodeId);
                while (queue.Count > 0)
                {
                    var nodeId = queue.Dequeue();
                    if (!graph.TryGetNode(nodeId, out var node))
                    {
                        continue;
                    }

                    for (var neighborIndex = 0; neighborIndex < node.NeighborIds.Count; neighborIndex++)
                    {
                        var neighborId = node.NeighborIds[neighborIndex];
                        if (neighborId < 0 || neighborId >= distances.Length || distances[neighborId] >= 0)
                        {
                            continue;
                        }

                        distances[neighborId] = distances[nodeId] + 1;
                        queue.Enqueue(neighborId);
                    }
                }

                for (var i = 0; i < graph.Nodes.Count; i++)
                {
                    var node = graph.Nodes[i];
                    if (!_regionGraph.TryGetRegionForNode(node.Id, out var nodeRegion)
                        || nodeRegion != objective.NextRegionId)
                    {
                        continue;
                    }

                    totalNextRegionNodes++;
                    var distance = node.Id >= 0 && node.Id < distances.Length ? distances[node.Id] : -1;
                    if (distance < 0)
                    {
                        continue;
                    }

                    if (minHopToNextRegion < 0 || distance < minHopToNextRegion)
                    {
                        minHopToNextRegion = distance;
                    }

                    if (distance <= candidateBfsDepth)
                    {
                        withinDepth++;
                        if (!_rejectedCanonicalLocalNodeIds.Contains(node.Id))
                        {
                            unrejectedWithinDepth++;
                        }
                    }
                }
            }

            var inferredPathRejectedWithinDepth = unrejectedWithinDepth;
            LogDiagnosticWarning(
                "[STK LOCAL SELECT DIAG] "
                + $"currentRegion={currentRegion.Value} previousRegion={previousRegion.Value} "
                + $"targetRegion={objective.TargetRegionId.Value} nextRegion={objective.NextRegionId.Value} "
                + $"currentNode={currentNodeId} previousNode={previousNodeId} "
                + $"bfsDepth={candidateBfsDepth} totalNextRegionNodes={totalNextRegionNodes} "
                + $"minHopToNextRegion={minHopToNextRegion} withinDepth={withinDepth} "
                + $"unrejectedWithinDepth={unrejectedWithinDepth} "
                + $"inferredPathRejectedWithinDepth={inferredPathRejectedWithinDepth} "
                + $"rejectedLocal={_rejectedCanonicalLocalNodeIds.Count}");
        }

        private bool SetCanonicalPatrolDestinationWithGlobalAlternates()
        {
            return TrySetCanonicalPatrolDestinationWithGlobalAlternates(out _);
        }

        private bool TrySetCanonicalPatrolDestinationWithGlobalAlternates(out NavigationRecoveryReason recoveryReason)
        {
            return TrySetCanonicalPatrolDestinationWithGlobalAlternates(
                GlobalPatrolObjectiveInvalidationReason.NavigationRecoveryFailed,
                out recoveryReason);
        }

        private bool TrySetCanonicalPatrolDestinationWithGlobalAlternates(
            GlobalPatrolObjectiveInvalidationReason invalidationReason,
            out NavigationRecoveryReason recoveryReason)
        {
            recoveryReason = NavigationRecoveryReason.None;
            if (SetCanonicalPatrolDestination())
            {
                return true;
            }

            if (regionGraphFallbackReason != RegionGraphFallbackReason.NoCompleteLocalPath)
            {
                return false;
            }

            var maxGlobalAttempts = _regionGraph?.Regions?.Count ?? 0;
            for (var attempt = 0; attempt < maxGlobalAttempts; attempt++)
            {
                var objective = _globalPatrolPlanner?.CurrentObjective ?? GlobalPatrolObjective.Invalid;
                if (!objective.TargetRegionId.IsValid)
                {
                    return false;
                }

                _rejectedCanonicalGlobalRegionIds.Add(objective.TargetRegionId);
                _globalPatrolPlanner?.Invalidate(invalidationReason);
                _rejectedCanonicalLocalNodeIds.Clear();
                recoveryReason = NavigationRecoveryReason.AlternateGlobalObjective;

                if (SetCanonicalPatrolDestination())
                {
                    return true;
                }

                if (regionGraphFallbackReason != RegionGraphFallbackReason.NoCompleteLocalPath)
                {
                    return false;
                }
            }

            return false;
        }

        private void MarkCanonicalDestinationReached()
        {
            var destinationNodeId = _blackboard.DestinationSpatialNodeId;
            if (destinationNodeId < 0)
            {
                return;
            }

            _blackboard.PreviousSpatialNodeId = _blackboard.CurrentSpatialNodeId;
            _blackboard.CurrentSpatialNodeId = destinationNodeId;
            _blackboard.DestinationSpatialNodeId = -1;
            _coverageMemory?.RecordPhysicalNodeArrival(destinationNodeId, CurrentSimulationTimeSeconds);
            _rejectedCanonicalLocalNodeIds.Clear();
            _rejectedCanonicalGlobalRegionIds.Clear();
            if (_regionGraph != null && _regionGraph.TryGetRegionForNode(destinationNodeId, out var regionId))
            {
                UpdateCurrentRegion(regionId);
            }

            SyncDynamicPatrolDebugFields();
        }

        private void MarkRoomSweepDestinationReached()
        {
            var destinationNodeId = _blackboard.DestinationSpatialNodeId;
            if (destinationNodeId < 0)
            {
                return;
            }

            LogRoomSweepDestinationReached(destinationNodeId);
            _blackboard.PreviousSpatialNodeId = _blackboard.CurrentSpatialNodeId;
            _blackboard.CurrentSpatialNodeId = destinationNodeId;
            _blackboard.DestinationSpatialNodeId = -1;
            _coverageMemory?.RecordPhysicalNodeArrival(destinationNodeId, CurrentSimulationTimeSeconds);
            if (_regionGraph != null && _regionGraph.TryGetRegionForNode(destinationNodeId, out var regionId))
            {
                UpdateCurrentRegion(regionId);
            }

            ClearNavigationObjective();
            SyncDynamicPatrolDebugFields();
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
            _rejectedDynamicPatrolNodeIds.Clear();
            ClearNavigationObjective();
            SyncDynamicPatrolDebugFields();
        }

        private void ClearDynamicPatrolDestination()
        {
            _blackboard.DestinationSpatialNodeId = -1;
            lastPatrolScore = 0f;
            candidateCount = 0;
            SyncDynamicPatrolDebugFields();
        }

        private void ClearRoomSweepDestination()
        {
            _blackboard.DestinationSpatialNodeId = -1;
            lastPatrolScore = 0f;
            candidateCount = 0;
            ClearNavigationObjective();
            SyncDynamicPatrolDebugFields();
        }

        private void ActivateDynamicPatrolFallback()
        {
            _dynamicPatrolFallbackActive = true;
            ClearDynamicPatrolDestination();
            TickFixedWaypointPatrol();
            _navigation?.RecordRecoveryReason(NavigationRecoveryReason.FixedPatrolFallback);
        }

        private void ActivateCanonicalPatrolFallback()
        {
            if (regionGraphFallbackReason == RegionGraphFallbackReason.None)
            {
                regionGraphFallbackReason = RegionGraphFallbackReason.MalformedRegionGraph;
            }

            _canonicalPatrolFallbackActive = true;
            _globalPatrolPlanner?.Invalidate(GlobalPatrolObjectiveInvalidationReason.NavigationRecoveryFailed);
            var recoveryReason = regionGraphFallbackReason == RegionGraphFallbackReason.SpatialGraphCompatibilityMismatch
                ? NavigationRecoveryReason.RegionGraphCompatibilityFallback
                : NavigationRecoveryReason.FixedPatrolFallback;
            ClearDynamicPatrolDestination();
            TickFixedWaypointPatrol();
            _navigation?.RecordRecoveryReason(recoveryReason);
        }

        private void ActivateRoomSweepPatrolFallback()
        {
            if (regionGraphFallbackReason == RegionGraphFallbackReason.None)
            {
                regionGraphFallbackReason = RegionGraphFallbackReason.MalformedRegionGraph;
            }

            LogRoomSweepFallbackActivated();
            _roomSweepPatrolFallbackActive = true;
            _roomSweepGlobalPlanner?.Invalidate(RoomSweepGlobalObjectiveInvalidationReason.NavigationRecoveryFailed);
            _rejectedRoomSweepTransitNodeIds.Clear();
            var recoveryReason = regionGraphFallbackReason == RegionGraphFallbackReason.SpatialGraphCompatibilityMismatch
                ? NavigationRecoveryReason.RegionGraphCompatibilityFallback
                : NavigationRecoveryReason.FixedPatrolFallback;
            _navigation?.Stop();
            ClearRoomSweepDestination();
            TickFixedWaypointPatrol();
            _navigation?.RecordRecoveryReason(recoveryReason);
        }

        private void ResetRoomSweepFallbackState()
        {
            _roomSweepPatrolFallbackActive = false;
            regionGraphFallbackReason = RegionGraphFallbackReason.None;
            _rejectedRoomSweepTransitNodeIds.Clear();
            _rejectedRoomSweepGlobalRegionIds.Clear();
        }

        private bool ResolveCurrentRoomSweepLocation(out int currentNodeId, out RegionId currentRegionId)
        {
            currentNodeId = -1;
            currentRegionId = RegionId.Invalid;
            if (!TryResolveNearestSpatialNode(transform.position, out currentNodeId)
                || _regionGraph == null
                || !_regionGraph.TryGetRegionForNode(currentNodeId, out currentRegionId))
            {
                return false;
            }

            _blackboard.CurrentSpatialNodeId = currentNodeId;
            UpdateCurrentRegion(currentRegionId);
            return true;
        }

        private bool IsRoomSweepRoomRegion(RegionId regionId)
        {
            return _regionGraph != null
                && _regionGraph.TryGetRegionSemanticMetadata(regionId, out var metadata)
                && metadata.Kind == RegionSemanticKind.Room;
        }

        private void UpdateRoomSweepVisualCoverage(RegionId roomRegionId)
        {
            if (visionSensor == null
                || _regionGraph == null
                || _spatialPatrolGraph == null
                || !_regionGraph.TryGetSpatialNodeIdsForRegion(roomRegionId, out var nodeIds))
            {
                return;
            }

            for (var i = 0; i < nodeIds.Count; i++)
            {
                var nodeId = nodeIds[i];
                if (!_spatialPatrolGraph.TryGetNode(nodeId, out var node))
                {
                    continue;
                }

                var observationPoint = visionSensor.GetObservationPointForGroundPoint(node.Position);
                if (visionSensor.CanSeePoint(observationPoint))
                {
                    _roomSweepCoverageMemory.MarkObserved(roomRegionId, nodeId);
                }
            }
        }

        private bool TryResolveNearestSpatialNode(Vector3 worldPosition, out int nodeId)
        {
            nodeId = -1;
            if (!EnsureSpatialGraphBuilt())
            {
                return false;
            }

            var bestSqrDistance = float.PositiveInfinity;
            var nodes = _spatialPatrolGraph.Nodes;
            for (var i = 0; i < nodes.Count; i++)
            {
                var sqrDistance = (nodes[i].Position - worldPosition).sqrMagnitude;
                if (sqrDistance >= bestSqrDistance)
                {
                    continue;
                }

                bestSqrDistance = sqrDistance;
                nodeId = nodes[i].Id;
            }

            return nodeId >= 0;
        }

        private void UpdateCurrentRegion(RegionId nextRegionId)
        {
            if (!nextRegionId.IsValid || _currentRegionId == nextRegionId)
            {
                return;
            }

            if (_currentRegionId.IsValid)
            {
                _previousRegionId = _currentRegionId;
            }

            _currentRegionId = nextRegionId;
            canonicalCurrentRegionId = nextRegionId.Value;
        }

        private bool HasCompletePathTo(Vector3 destination)
        {
            return _navigation != null && _navigation.EvaluateDestination(destination).IsComplete;
        }

        private NavigationEvaluationStatus EvaluateSearchPath(Vector3 destination)
        {
            return _navigation != null
                ? _navigation.EvaluateDestination(destination).Status
                : NavigationEvaluationStatus.AgentUnavailable;
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

        private void ApplyMovementSpeedForCurrentState()
        {
            var agent = GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                return;
            }

            agent.speed = currentState == StalkerState.CHASE
                ? Mathf.Max(0f, chaseSpeed)
                : Mathf.Max(0f, patrolSpeed);
        }

        private void LogDiagnosticWarning(string message)
        {
            if (enableDiagnostics)
            {
                UnityEngine.Debug.LogWarning(message);
            }
        }

        private void LogDiagnostic(string message)
        {
            if (enableDiagnostics)
            {
                UnityEngine.Debug.Log(message);
            }
        }

        private bool CanUseNavigation()
        {
            return _navigation != null && _navigation.IsUsable;
        }

        private bool TryBeginWorldInteractionForCurrentNavigation(NavigationFailureReason failureReason)
        {
            if (_worldInteractionDriver.HasActiveInteraction
                || !_navigationObjectiveKey.IsValid
                || !IsLocomotionState()
                || !CanRunWorldInteractionAuthority())
            {
                return false;
            }

            if (failureReason != NavigationFailureReason.DoorBlocked
                && !IsNavigationExecutionRetryableFailure(failureReason))
            {
                return false;
            }

            if (!TryFindCurrentWorldInteractionBlocker(out var blocker))
            {
                return false;
            }

            var result = _worldInteractionDriver.TryBegin(
                blocker,
                _navigationObjectiveKey,
                currentState,
                true,
                stalkerDoorBreakDurationSeconds);

            if (result == StalkerWorldInteractionStartResult.NotHandled
                || result == StalkerWorldInteractionStartResult.AuthorityRejected)
            {
                return false;
            }

            LogDiagnostic(
                $"[STK WORLD INTERACTION DIAG] blocker-detected result={result} " +
                $"kind={_worldInteractionDriver.CurrentKind} " +
                $"blocker={blocker.name} objectiveKind={_navigationObjectiveKey.Kind} " +
                $"destinationNode={_blackboard.DestinationSpatialNodeId}");

            _navigation?.Stop();
            if (result == StalkerWorldInteractionStartResult.Completed)
            {
                ResumeCurrentNavigationObjectiveAfterWorldInteraction("immediate-complete");
            }

            return true;
        }

        private bool TickWorldInteraction()
        {
            if (!_worldInteractionDriver.HasActiveInteraction)
            {
                return false;
            }

            if (!IsLocomotionState()
                || !_navigationObjectiveKey.Equals(_worldInteractionDriver.ObjectiveKey)
                || currentState != _worldInteractionDriver.State)
            {
                CancelWorldInteraction("objective-or-state-changed");
                return false;
            }

            if (!_worldInteractionDriver.Tick(
                    CurrentSimulationDeltaSeconds,
                    CanRunWorldInteractionAuthority(),
                    out var completed))
            {
                LogDiagnostic(
                    $"[STK WORLD INTERACTION DIAG] cancelled kind={_worldInteractionDriver.CurrentKind} " +
                    $"reason=driver-cancelled objectiveKind={_navigationObjectiveKey.Kind}");
                return false;
            }

            if (!completed)
            {
                return true;
            }

            ResumeCurrentNavigationObjectiveAfterWorldInteraction("completed");
            return true;
        }

        private void CancelWorldInteraction(string reason)
        {
            if (!_worldInteractionDriver.HasActiveInteraction)
            {
                return;
            }

            var kind = _worldInteractionDriver.CurrentKind;
            _worldInteractionDriver.Cancel();
            LogDiagnostic(
                $"[STK WORLD INTERACTION DIAG] cancelled kind={kind} reason={reason}");
        }

        private void ResumeCurrentNavigationObjectiveAfterWorldInteraction(string reason)
        {
            LogDiagnostic(
                $"[STK WORLD INTERACTION DIAG] resume-same-objective reason={reason} " +
                $"objectiveKind={_navigationObjectiveKey.Kind} destinationNode={_blackboard.DestinationSpatialNodeId}");

            if (!TryGetCurrentNavigationRecoveryDestination(out var destination))
            {
                return;
            }

            _navigation?.RequestDestination(destination, NavigationRequestIntent.NewGoal);
        }

        private bool TryFindCurrentWorldInteractionBlocker(out Component blocker)
        {
            blocker = null;
            var origin = transform.position + Vector3.up * 0.8f;
            if (!TryGetCurrentWorldInteractionDirection(origin, out var direction, out var maxDistance))
            {
                return false;
            }

            var hits = Physics.SphereCastAll(
                origin,
                Mathf.Max(0.01f, worldInteractionProbeRadius),
                direction,
                maxDistance,
                ~0,
                QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (var i = 0; i < hits.Length; i++)
            {
                var hitCollider = hits[i].collider;
                if (hitCollider == null
                    || hitCollider.isTrigger
                    || hitCollider.transform == transform
                    || hitCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                var jammer = hitCollider.GetComponentInParent<NetworkDoorJammer>();
                if (jammer != null && jammer.IsActive)
                {
                    blocker = jammer;
                    return true;
                }

                var door = hitCollider.GetComponentInParent<NetworkSlidingDoor>();
                if (door != null && door.BlocksTraversal)
                {
                    blocker = door;
                    return true;
                }

                return false;
            }

            return false;
        }

        private bool TryGetCurrentWorldInteractionDirection(
            Vector3 origin,
            out Vector3 direction,
            out float maxDistance)
        {
            direction = default;
            maxDistance = 0f;
            if (_navigation != null
                && _navigation.TryGetCurrentPathCorners(out var corners)
                && corners != null
                && corners.Length >= 2)
            {
                direction = corners[1] - origin;
            }
            else if (_navigation != null && _navigation.TryGetActiveDestination(out var destination))
            {
                direction = destination - origin;
            }
            else if (TryGetCurrentNavigationRecoveryDestination(out var recoveryDestination))
            {
                direction = recoveryDestination - origin;
            }
            else
            {
                return false;
            }

            var distance = direction.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                return false;
            }

            direction /= distance;
            maxDistance = Mathf.Min(distance, Mathf.Max(0.1f, worldInteractionDistance));
            return true;
        }

        private bool CanRunWorldInteractionAuthority()
        {
            var networkObject = GetComponent<NetworkObject>();
            return networkObject == null || !networkObject.IsValid || networkObject.HasStateAuthority;
        }
    }
}
