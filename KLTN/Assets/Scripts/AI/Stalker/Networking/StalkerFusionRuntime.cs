using System;
using System.Collections.Generic;
using EchoProtocol.AI.AED;
using EchoProtocol.AI.Common;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.AI.Listener.Noise;
using EchoProtocol.AI.Listener.Perception;
using EchoProtocol.AI.Stalker.Presentation;
using EchoProtocol.AI.Stalker.Special;
using EchoProtocol.AI.Stalker.Spatial.Strategic;
using EchoProtocol.AI.Stalker.Telemetry;
using EchoProtocol.Diagnostics;
using EchoProtocol.Networking;
using EchoProtocol.Networking.Authority;
using EchoProtocol.Player;
using Fusion;
using UnityEngine;
using UnityEngine.AI;
namespace EchoProtocol.AI.Stalker.Networking
{
    using Debug = UnityEngine.Debug;

    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StalkerController))]
    [RequireComponent(typeof(StalkerVisionSensor))]
    [RequireComponent(typeof(StalkerSpecialEncounterRuntime))]
    public sealed class StalkerFusionRuntime : NetworkBehaviour
    {
        [SerializeField] private StalkerController controller;
        [SerializeField] private StalkerVisionSensor visionSensor;
        [SerializeField] private StalkerSpecialEncounterRuntime specialEncounterRuntime;
        [SerializeField] private FusionPlayerLifecycle lifecycle;
        [SerializeField, Min(0)] private int patrolNearOptimalHopSlack = 1;

        [Header("Authoritative Hearing")]
        [SerializeField] private Transform hearingOrigin;
        [SerializeField] private LayerMask acousticBlockerMask = ~0;
        [SerializeField, Min(0f)] private double hearingThreshold = 0.1d;
        [SerializeField, Range(0.01f, 0.99f)] private float closedDoorMultiplier = 0.5f;
        [SerializeField, Range(0.01f, 0.99f)] private float wallMultiplier = 0.25f;

        [Header("Runtime Diagnostics")]
        [SerializeField, Min(0.1f)]
        private float hearingDiagnosticIntervalSeconds = 0.5f;

        [Header("Authoritative Combat")]
        [SerializeField, Min(1)] private int attackDamage = 25;
        [SerializeField, Min(0.1f)] private float maximumDamageDistance = 2f;
        [SerializeField] private bool catchEndsInDeath;
        [SerializeField, Min(0.2f)] private float jumpscareSeconds = 2f;
        [SerializeField, Min(0f)] private float catchCooldownSeconds = 2f;
        [Networked] private TickTimer CatchCooldown { get; set; }

        public bool TryCatchPlayer(NetworkPlayerLifeState player)
        {
            if (Object == null || !Object.IsValid || !Object.HasStateAuthority
                || controller == null || controller.CurrentState != StalkerState.ATTACK
                || !CatchCooldown.ExpiredOrNotRunning(Runner) || player == null)
                return false;
            if (!player.TryCatchAuthoritative(Object, maximumDamageDistance, jumpscareSeconds, catchEndsInDeath))
                return false;
            CatchCooldown = TickTimer.CreateFromSeconds(Runner, Mathf.Max(jumpscareSeconds, catchCooldownSeconds));
            AttackSequence++;
            return true;
        }

        [Networked, OnChangedRender(nameof(ApplyReplicatedPresentation))]
        public StalkerState ReplicatedState { get; private set; }

        [Networked, OnChangedRender(nameof(ApplyReplicatedPresentation))]
        public PlayerRef TargetPlayer { get; private set; }

        [Networked, OnChangedRender(nameof(ApplyReplicatedPresentation))]
        public uint AttackSequence { get; private set; }

        private readonly StalkerFusionTargetFrameBuilder _frameBuilder = new StalkerFusionTargetFrameBuilder();
        private readonly StalkerTelemetryAdapter _telemetryAdapter = new StalkerTelemetryAdapter();
        private readonly List<StalkerPerceptionTargetSnapshot> _perceptionSnapshots =
            new List<StalkerPerceptionTargetSnapshot>();
        private readonly List<StalkerTargetStatus> _targetStatuses =
            new List<StalkerTargetStatus>();
        private readonly List<StalkerTargetCandidate> _visibleCandidates =
            new List<StalkerTargetCandidate>();
        private readonly List<PlayerId> _visibleObjectiveCarrierIds =
            new List<PlayerId>();
        private readonly List<HearingObservation> _hearingObservations =
            new List<HearingObservation>();
        private readonly List<RuntimeNoiseEvent> _activeNoiseEvents =
            new List<RuntimeNoiseEvent>();
        private readonly List<PlayerRuntimeIdentity> _strategicPlayers =
            new List<PlayerRuntimeIdentity>();
        private readonly Dictionary<ActivityRoomKey, StrategicOccupancyAccumulator>
            _strategicOccupancyByRoom =
                new Dictionary<ActivityRoomKey, StrategicOccupancyAccumulator>();
        private readonly List<ActivityRoomOccupancy> _strategicOccupancy =
            new List<ActivityRoomOccupancy>();
        private readonly List<StrategicNoisePulse> _strategicNoisePulses =
            new List<StrategicNoisePulse>();
        private readonly StalkerPresentationDriver _presentationDriver = new StalkerPresentationDriver();
        private bool _networkSimulationOwned;
        private AiSimulationStep _lastAuthoritativeStep;
        private StalkerNetworkLifeStateConsequenceSink _productionConsequenceSink;
        private StalkerProductionTelemetryProducer _productionTelemetryProducer;
        private StalkerNetworkPresentationState _lastAuthoritativePresentationState;
        private float _nextHearingDiagnosticTime;

        [Networked] public int ReplicatedSemanticState { get; private set; }
        [Networked] public long ReplicatedAttackEpisodeId { get; private set; }
        [Networked] public int ReplicatedAttackPhase { get; private set; }
        [Networked] public float ReplicatedAttackProgressSeconds { get; private set; }
        [Networked] public NetworkBool ReplicatedAttackHitMomentResolved { get; private set; }
        [Networked] public int ReplicatedAttackOutcome { get; private set; }
        [Networked] public long ReplicatedAttackStartedTick { get; private set; }
        [Networked] public long ReplicatedAttackResolvedTick { get; private set; }
        [Networked] public int ReplicatedPresentationActionValue { get; private set; }
        [Networked] public int ReplicatedPresentationActionOrdinal { get; private set; }
        [Networked] public float ReplicatedPresentationActionProgress01 { get; private set; }
        [Networked] public int ReplicatedSpecialPhaseValue { get; private set; }
        [Networked] public uint ReplicatedSpecialSequenceOrdinal { get; private set; }
        [Networked] public float ReplicatedSpecialPhaseElapsed { get; private set; }
        [Networked] public NetworkBool ReplicatedSpecialVisible { get; private set; }
        private ListenerHearingSensor _hearingSensor;
        private HostRuntimeNoiseService _runtimeNoiseService;
        private Guid _boundHearingMatchId;
        private Guid _boundPatrolMatchId;
        private Guid _boundStrategicPatrolMatchId;
        private double _nextStrategicOccupancySampleSeconds;
        private Guid _boundScenarioMatchId;
        private string _boundScenarioConfigFingerprint;
        private NavMeshAgent _navigationAgent;
        private StalkerAttackResult _previousAttackResult;
        private bool _networkPrefabGuard;

        private struct StrategicOccupancyAccumulator
        {
            public int ActiveCount;
            public int HiddenCount;
        }

        public int AuthoritativeSimulationCount { get; private set; }
        public bool HasLastAuthoritativeStep => _lastAuthoritativeStep.IsValid;
        public AiSimulationStep LastAuthoritativeStep => _lastAuthoritativeStep;
        public StalkerNetworkPresentationState LastAuthoritativePresentationState => _lastAuthoritativePresentationState;
        public StalkerPresentationDriver PresentationDriver => _presentationDriver;
        public bool HasStateAuthorityForDebug => Object != null && Object.HasStateAuthority;
        public IStalkerTelemetryProducer TelemetryProducer { get; set; }
        public StalkerTelemetryMonsterIdentity TelemetryMonsterIdentity { get; set; }
        public int TelemetryTerminalOccurrenceCount => _telemetryAdapter.TerminalOccurrenceCount;
        public StalkerTelemetryPublishResult LastAttackTelemetryPublishResult { get; private set; }
        public StalkerTelemetryPublishResult LastSearchTelemetryPublishResult { get; private set; }

        private void Awake()
        {
            if (acousticBlockerMask.value == 0)
            {
                Debug.LogWarning(
                    "[StalkerFusion] acousticBlockerMask is empty; " +
                    "authoritative hearing cannot classify wall/door occlusion.",
                    this);
            }

            ResolveLocalDependencies();
            _networkPrefabGuard =
                GetComponent<NetworkObject>() != null;
        }

        private void OnEnable()
        {
            ResolveLocalDependencies();
            ApplyOwnedLegacySuppression();
        }

        private void OnDisable()
        {
            SetLegacySimulationSuppressed(_networkSimulationOwned);
        }

        public override void Spawned()
        {
            _networkSimulationOwned = true;
            ResolveLocalDependencies();
            ResolveLifecycle();
            BindProductionConsequenceSink();
            BindProductionTelemetryProducer();
            _telemetryAdapter.ResetForOwnerLifecycle();
            SetLegacySimulationSuppressed(true);
            ConfigureAuthorityOnlyComponents();

            if (Object != null && Object.HasStateAuthority)
            {
                ReplicatedState = controller != null ? controller.CurrentState : StalkerState.PATROL;
                TargetPlayer = PlayerRef.None;
                CatchCooldown = TickTimer.None;
            }

            if (Object != null) ApplyReplicatedPresentation();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _networkSimulationOwned = false;
            lifecycle = null;
            _productionConsequenceSink = null;
            if (ReferenceEquals(TelemetryProducer, _productionTelemetryProducer))
            {
                TelemetryProducer = null;
            }

            _productionTelemetryProducer = null;
            TelemetryMonsterIdentity = default;
            _lastAuthoritativeStep = AiSimulationStep.Invalid;
            _lastAuthoritativePresentationState = default;
            _telemetryAdapter.ResetForOwnerLifecycle();
            LastAttackTelemetryPublishResult = StalkerTelemetryPublishResult.RetryableFailure;
            LastSearchTelemetryPublishResult = StalkerTelemetryPublishResult.RetryableFailure;
            _previousAttackResult = StalkerAttackResult.None;
            _hearingSensor?.EndMatch();
            _runtimeNoiseService = null;
            _boundHearingMatchId = Guid.Empty;
            _boundPatrolMatchId = Guid.Empty;
            controller?.ResetStrategicPatrolRuntime();
            _boundStrategicPatrolMatchId = Guid.Empty;
            _nextStrategicOccupancySampleSeconds = 0d;
            _strategicPlayers.Clear();
            _strategicOccupancyByRoom.Clear();
            _strategicOccupancy.Clear();
            _strategicNoisePulses.Clear();
            controller?.ClearScenarioMonsterParameters();
            _boundScenarioMatchId =
                Guid.Empty;
            _boundScenarioConfigFingerprint =
                null;
            _hearingObservations.Clear();
            SetLegacySimulationSuppressed(false);
        }

        public override void FixedUpdateNetwork()
        {
            ResolveLocalDependencies();

            if (!CanRunAuthoritativeSimulation())
            {
                ClearFrameBuffers();
                return;
            }

            if (!FusionAiSimulationStepAdapter.TryCreate(Runner, out var step))
            {
                ClearFrameBuffers();
                return;
            }

            BindPatrolVariationFromMatchAuthority();
            BindScenarioConfigFromRegistry();
            _lastAuthoritativeStep = step;
            if (!RunAuthoritativePipeline(step))
            {
                return;
            }

            PublishAuthoritativeState();
            ResolveAuthoritativeAttack();
        }

        public override void Render()
        {
            if (Object != null && !Object.HasStateAuthority)
            {
                ConsumeReplicatedPresentationState();
            }
        }

        private bool CanRunAuthoritativeSimulation()
        {
            if (Runner == null
                || !Runner.IsRunning
                || !Runner.IsServer
                || Object == null
                || !Object.HasStateAuthority
                || controller == null
                || visionSensor == null)
            {
                return false;
            }

            var runnerLifecycle = Runner.GetComponent<FusionPlayerLifecycle>();
            if (lifecycle != runnerLifecycle)
            {
                lifecycle = runnerLifecycle;
                BindProductionConsequenceSink();
                BindProductionTelemetryProducer();
            }

            return lifecycle != null;
        }

        private static int DerivePatrolVariationSeed(Guid matchId)
        {
            var text = matchId.ToString("N");

            unchecked
            {
                uint hash = 2166136261u;

                for (var i = 0; i < text.Length; i++)
                {
                    hash ^= text[i];
                    hash *= 16777619u;
                }

                return (int)hash;
            }
        }

        private void BindPatrolVariationFromMatchAuthority()
        {
            if (controller == null)
            {
                return;
            }

            var authority =
                MatchAuthorityRuntime.Instance;

            if (authority == null
                || !authority.HasStateAuthority
                || !authority.TryGetMatchId(
                    out var matchId)
                || matchId == Guid.Empty)
            {
                return;
            }

            if (_boundPatrolMatchId != matchId)
            {
                var seed =
                    DerivePatrolVariationSeed(
                        matchId);

                controller.ConfigurePatrolVariation(
                    seed,
                    patrolNearOptimalHopSlack);

                RuntimeLog.Log(
                    RuntimeLogCategory.StalkerPatrol,
                    $"[STK_PATROL][BIND] " +
                    $"match={matchId:D} " +
                    $"seed={seed} " +
                    $"slack={patrolNearOptimalHopSlack}");

                _boundPatrolMatchId =
                    matchId;
            }

            if (controller.SmartPatrolEnabled
                && _boundStrategicPatrolMatchId
                    != matchId
                && controller.BeginStrategicPatrolMatch(
                    matchId))
            {
                _boundStrategicPatrolMatchId =
                    matchId;

                _nextStrategicOccupancySampleSeconds =
                    0d;
            }
        }

        private void BindScenarioConfigFromRegistry()
        {
            if (controller == null)
            {
                return;
            }

            var authority =
                MatchAuthorityRuntime.Instance;

            if (authority == null
                || !authority.HasStateAuthority
                || !authority.TryGetMatchId(
                    out var matchId)
                || matchId == Guid.Empty)
            {
                ClearScenarioConfigBinding();
                return;
            }

            if (!ScenarioConfigRuntimeRegistry
                    .TryGetAppliedConfig(
                        matchId,
                        out var config)
                || config == null)
            {
                ClearScenarioConfigBinding();
                return;
            }

            var fingerprint =
                ScenarioConfigFingerprint.Compute(
                    config);

            if (_boundScenarioMatchId == matchId
                && string.Equals(
                    _boundScenarioConfigFingerprint,
                    fingerprint,
                    StringComparison.Ordinal))
            {
                return;
            }

            controller.ApplyScenarioMonsterParameters(
                config.MonsterParameters);

            _boundScenarioMatchId =
                matchId;

            _boundScenarioConfigFingerprint =
                fingerprint;

            RuntimeLog.Log(
                RuntimeLogCategory.StalkerAed,
                $"[STK_AED][BIND] " +
                $"match={matchId:D} " +
                $"config={config.ScenarioConfigVersion} " +
                $"source={config.ConfigSource} " +
                $"chase={config.MonsterParameters.ChaseSpeed:0.###}");
        }

        private void ClearScenarioConfigBinding()
        {
            if (_boundScenarioMatchId == Guid.Empty
                && string.IsNullOrEmpty(
                    _boundScenarioConfigFingerprint))
            {
                return;
            }

            controller?.ClearScenarioMonsterParameters();

            _boundScenarioMatchId =
                Guid.Empty;

            _boundScenarioConfigFingerprint =
                null;
        }

        private bool RunAuthoritativePipeline(AiSimulationStep step)
        {
            if (!step.IsValid || controller == null || visionSensor == null)
            {
                ClearFrameBuffers();
                return false;
            }

            var frameBuilt = lifecycle != null
                ? _frameBuilder.TryBuild(
                    lifecycle,
                    controller.DetectionTargetId,
                    controller.CurrentTargetId,
                    _perceptionSnapshots,
                    _targetStatuses)
                : TryBuildRunnerPlayerFrame();
            if (!frameBuilt)
            {
                ClearFrameBuffers();
                return false;
            }

            StalkerPerceptionEvaluator.CollectVisibleTargetCandidates(
                visionSensor,
                _perceptionSnapshots,
                step.Time,
                _visibleCandidates);
            CollectVisibleObjectiveCarrierIds();

            var hearingEvaluationTimeUtc =
                DateTime.UtcNow;

            BuildAuthoritativeHearingFrame(
                hearingEvaluationTimeUtc);

            BuildAndApplyStrategicPatrolFrame(
                step,
                hearingEvaluationTimeUtc);

            //
            // Special Encounter must evaluate BEFORE the normal FSM
            // simulation.
            //
            // This is critical for the ATTACK -> RECOVER handoff:
            //
            // Bite causes Player Down
            //      ↓
            // Stalker enters RECOVER
            //      ↓
            // Special runtime sees RECOVER here
            //      ↓
            // Special acquires controller override
            //      ↓
            // controller.Simulate() below cannot advance RECOVER
            // into normal CHASE / SEARCH / PATROL first.
            //
            // Perception frames have already been built above, so Vision,
            // Hearing and target-status information remain fresh.
            //
            specialEncounterRuntime?.TickAuthoritative(
                Runner,
                lifecycle,
                step);

            if (specialEncounterRuntime != null
                && specialEncounterRuntime.IsActive)
            {
                controller.BeginSpecialEncounterOverride();
            }

            var input =
                new StalkerSimulationInput(
                    step,
                    _visibleCandidates,
                    _targetStatuses,
                    BuildCurrentAttackTargetSnapshot(
                        controller.CurrentTargetId),
                    _hearingObservations,
                    hearingEvaluationTimeUtc,
                    _visibleObjectiveCarrierIds);

            if (!controller.Simulate(input))
            {
                return false;
            }

            AuthoritativeSimulationCount++;

            PublishReplicatedPresentationState();

            PublishCommittedTelemetryFacts();

            return true;
        }

        public StalkerNetworkPresentationState GetReplicatedPresentationState()
        {
            return new StalkerNetworkPresentationState(
                ToReplicatedSemanticState(ReplicatedSemanticState),
                ReplicatedAttackEpisodeId > 0L
                    ? new StalkerAttackEpisodeId(ReplicatedAttackEpisodeId)
                    : StalkerAttackEpisodeId.Invalid,
                ToReplicatedAttackPhase(ReplicatedAttackPhase),
                ReplicatedAttackProgressSeconds,
                ReplicatedAttackHitMomentResolved,
                ToReplicatedAttackOutcome(ReplicatedAttackOutcome),
                ReplicatedAttackStartedTick,
                ReplicatedAttackResolvedTick,
                ToReplicatedPresentationAction(ReplicatedPresentationActionValue),
                ReplicatedPresentationActionOrdinal,
                ReplicatedPresentationActionProgress01,
                ToReplicatedSpecialPhase(ReplicatedSpecialPhaseValue),
                ReplicatedSpecialSequenceOrdinal,
                ReplicatedSpecialPhaseElapsed,
                ReplicatedSpecialVisible);
        }

        public StalkerPresentationConsumeResult ConsumeReplicatedPresentationState()
        {
            return _presentationDriver.Consume(GetReplicatedPresentationState());
        }

        private void PublishAuthoritativeState()
        {
            ReplicatedState = controller.CurrentState;
            TargetPlayer = ResolveReplicatedTarget();
        }

        private PlayerRef ResolveReplicatedTarget()
        {
            var playerId = controller.CurrentTargetId.IsValid
                ? controller.CurrentTargetId
                : controller.DetectionTargetId;
            if (!playerId.IsValid) return PlayerRef.None;
            if (lifecycle != null && lifecycle.IdentityRegistry.TryGetPlayerRef(playerId, out var lifecyclePlayer))
            {
                return lifecyclePlayer;
            }

            foreach (var player in Runner.ActivePlayers)
            {
                if (CreateRunnerPlayerId(player) == playerId) return player;
            }
            return PlayerRef.None;
        }

        private bool TryBuildRunnerPlayerFrame()
        {
            _perceptionSnapshots.Clear();
            _targetStatuses.Clear();

            foreach (var player in Runner.ActivePlayers)
            {
                if (!Runner.TryGetPlayerObject(player, out var playerObject) || playerObject == null) continue;

                var playerId = CreateRunnerPlayerId(player);
                if (!playerId.IsValid) continue;
                var isGameplayPlayer = !playerObject.TryGetComponent<LobbyPlayerState>(out var lobbyState)
                    || lobbyState.IsGameplayPlayer;
                var isDowned = (playerObject.TryGetComponent<NetworkPlayerLifeState>(out var lifeState) && lifeState.IsDowned)
                    || (playerObject.TryGetComponent<NetworkPlayerHealth>(out var health) && health.IsDowned);
                var isEliminated = lifeState != null && lifeState.IsEliminated;
                var isHidden = (playerObject.TryGetComponent<PlayerHidingController>(out var hiding) && hiding.IsHidden)
                    || (playerObject.TryGetComponent<NetworkPlayerMovement>(out var netMove) && netMove.IsHidden);
                var eligibilitySnapshot = new StalkerTargetEligibilitySnapshot(
                    isGameplayPlayer,
                    true,
                    isDowned,
                    isEliminated,
                    isHidden || (lifeState != null && lifeState.IsCaught));
                _targetStatuses.Add(new StalkerTargetStatus(
                    playerId,
                    StalkerTargetEligibility.Evaluate(eligibilitySnapshot),
                    isHidden));
                _perceptionSnapshots.Add(new StalkerPerceptionTargetSnapshot(
                    playerId,
                    playerObject.transform,
                    playerObject.transform,
                    eligibilitySnapshot,
                    lobbyState != null
                        && lobbyState.CarriedCoreId.IsValid));
            }

            return true;
        }

        private void BuildAuthoritativeHearingFrame(
            DateTime heardAtUtc)
        {
            _hearingObservations.Clear();
            _activeNoiseEvents.Clear();

            if (_hearingSensor == null)
            {
                return;
            }

            if (heardAtUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Authoritative hearing frame time must be UTC.",
                    nameof(heardAtUtc));
            }

            var authority =
                MatchAuthorityRuntime.Instance;

            if (authority == null
                || !authority.HasStateAuthority
                || !authority.TryGetMatchId(
                    out var matchId))
            {
                if (_boundHearingMatchId != Guid.Empty)
                {
                    _hearingSensor.EndMatch();
                    _boundHearingMatchId =
                        Guid.Empty;
                }

                _runtimeNoiseService = null;
                return;
            }

            //
            // HostRuntimeNoiseService is shared by the whole match.
            // Stalker only reads its authoritative snapshots.
            //
            _runtimeNoiseService =
                HostRuntimeNoiseService.EnsureExists(
                    authority);

            if (_boundHearingMatchId != matchId)
            {
                _hearingSensor.BeginMatch(
                    matchId);

                //
                // Idempotent for the same match. This also makes
                // late-spawned Stalkers safe if the service binding
                // happened before the monster existed.
                //
                _runtimeNoiseService.BeginMatch(
                    matchId);

                _boundHearingMatchId =
                    matchId;
            }

            var activeEvents =
                _runtimeNoiseService.GetActiveEvents(
                    heardAtUtc);

            for (var i = 0;
                 i < activeEvents.Count;
                 i++)
            {
                _activeNoiseEvents.Add(
                    activeEvents[i]);
            }

            //
            // ListenerHearingSensor owns an ordinal watermark,
            // therefore events must always be evaluated in
            // authoritative publication order.
            //
            _activeNoiseEvents.Sort(
                (left, right) =>
                    left.EventOrderKey.CompareTo(
                        right.EventOrderKey));

            var origin =
                hearingOrigin != null
                    ? hearingOrigin.position
                    : transform.position;

            for (var i = 0;
                 i < _activeNoiseEvents.Count;
                 i++)
            {
                var noiseEvent =
                    _activeNoiseEvents[i];

                var heard =
                    _hearingSensor.TryEvaluate(
                        noiseEvent,
                        origin,
                        heardAtUtc,
                        out var observation,
                        out var rejectReason);

                if (heard)
                {
                    _hearingObservations.Add(
                        observation);
                }

                if (RuntimeLog.IsEnabled(
                        RuntimeLogCategory.StalkerHearing)
                    && _hearingSensor.LastEvaluationStatus
                        == ListenerHearingEvaluationStatus.Evaluated)
                {
                    RuntimeLog.Log(
                        RuntimeLogCategory.StalkerHearing,
                        $"[STK_HEARING][EVAL] " +
                        $"type={noiseEvent.NoiseType} " +
                        $"distance={Vector3.Distance(origin, noiseEvent.WorldPosition):F2} " +
                        $"radius={noiseEvent.HearingRadius:F2} " +
                        $"loudness={noiseEvent.Loudness:F3} " +
                        $"result={(heard ? "HEARD" : "REJECT")} " +
                        $"reject={rejectReason} " +
                        $"noisePos={noiseEvent.WorldPosition} " +
                        $"origin={origin}",
                        this);
                }
            }

            LogHearingFrameDiagnostic(
                origin,
                heardAtUtc);
        }

        private void BuildAndApplyStrategicPatrolFrame(
            AiSimulationStep step,
            DateTime nowUtc)
        {
            if (!step.IsValid
                || nowUtc.Kind != DateTimeKind.Utc
                || controller == null
                || !controller.SmartPatrolEnabled
                || lifecycle == null
                || Runner == null
                || !Runner.IsServer
                || Object == null
                || !Object.IsValid
                || !Object.HasStateAuthority
                || _boundStrategicPatrolMatchId == Guid.Empty)
            {
                return;
            }

            // Exact player/noise world positions are consumed only here to resolve
            // logical ActivityRoomKey. They are not retained in the strategic frame.
            var sampleInterval = Math.Max(
                0.1d,
                controller.SmartPatrolOccupancySampleIntervalSeconds);
            var hasOccupancySample =
                step.Time.Seconds >= _nextStrategicOccupancySampleSeconds;

            if (hasOccupancySample)
            {
                _nextStrategicOccupancySampleSeconds =
                    step.Time.Seconds + sampleInterval;
                _strategicPlayers.Clear();
                _strategicOccupancyByRoom.Clear();
                _strategicOccupancy.Clear();
                lifecycle.EntityRegistry.CollectActiveEntities(
                    _strategicPlayers);

                for (var i = 0; i < _strategicPlayers.Count; i++)
                {
                    var identity = _strategicPlayers[i];
                    if (identity == null || !identity.IsBound)
                    {
                        continue;
                    }

                    var lobbyState = identity.GetComponent<LobbyPlayerState>();
                    if (lobbyState != null
                        && lobbyState.Object != null
                        && lobbyState.Object.IsValid
                        && !lobbyState.IsGameplayPlayer)
                    {
                        continue;
                    }

                    var lifeState =
                        identity.GetComponent<NetworkPlayerLifeState>();

                    if (lifeState == null
                        || lifeState.Object == null
                        || !lifeState.Object.IsValid
                        || lifeState.Status != NetworkPlayerLifeStatus.Alive)
                    {
                        continue;
                    }

                    if (!controller.TryResolveStrategicActivityRoom(
                            identity.EntityRoot.position,
                            out var room))
                    {
                        continue;
                    }

                    var movement = identity.GetComponent<NetworkPlayerMovement>();
                    var hidden = movement != null && movement.IsHidden;
                    var hiding = identity.GetComponent<PlayerHidingController>();
                    hidden |= hiding != null && hiding.IsHidden;

                    _strategicOccupancyByRoom.TryGetValue(
                        room,
                        out var accumulator);
                    accumulator.ActiveCount++;
                    if (hidden)
                    {
                        accumulator.HiddenCount++;
                    }

                    _strategicOccupancyByRoom[room] = accumulator;
                }

                foreach (var pair in _strategicOccupancyByRoom)
                {
                    _strategicOccupancy.Add(new ActivityRoomOccupancy(
                        pair.Key,
                        pair.Value.ActiveCount,
                        pair.Value.HiddenCount));
                }

                _strategicOccupancy.Sort(
                    (left, right) => left.Room.CompareTo(right.Room));
            }

            _strategicNoisePulses.Clear();
            for (var i = 0; i < _activeNoiseEvents.Count; i++)
            {
                var noise = _activeNoiseEvents[i];
                if (!controller.TryResolveStrategicActivityRoom(
                        noise.WorldPosition,
                        out var room))
                {
                    continue;
                }

                _strategicNoisePulses.Add(new StrategicNoisePulse(
                    noise.NoiseEventId,
                    room,
                    Mathf.Clamp01((float)noise.Loudness),
                    noise.NoiseType == RuntimeNoiseType.CORE_INSERT));
            }

            var legalLastKnownRoom = ActivityRoomKey.Invalid;
            var hasLegalLastKnownRoom =
                controller.HasLastKnownPosition
                && controller.CurrentState == StalkerState.SEARCH
                && controller.TryResolveStrategicActivityRoom(
                    controller.LastKnownPosition,
                    out legalLastKnownRoom);

            var frame = new StalkerStrategicWorldFrame(
                step.Time.Seconds,
                hasOccupancySample,
                _strategicOccupancy,
                _strategicNoisePulses,
                hasLegalLastKnownRoom,
                hasLegalLastKnownRoom
                    ? legalLastKnownRoom
                    : ActivityRoomKey.Invalid);

            controller.ApplyStrategicWorldFrame(frame);
        }

        private void LogHearingFrameDiagnostic(
            Vector3 origin,
            DateTime heardAtUtc)
        {
            if (_activeNoiseEvents.Count <= 0)
            {
                _nextHearingDiagnosticTime = 0f;
                return;
            }

            if (!RuntimeLog.IsEnabled(
                    RuntimeLogCategory.StalkerHearing))
            {
                return;
            }

            var now = Time.unscaledTime;

            if (now < _nextHearingDiagnosticTime)
            {
                return;
            }

            RuntimeLog.Log(
                RuntimeLogCategory.StalkerHearing,
                $"[STK_HEARING][FRAME] " +
                $"activeNoise={_activeNoiseEvents.Count} " +
                $"heard={_hearingObservations.Count} " +
                $"origin={origin} " +
                $"utc={heardAtUtc:O}");

            _nextHearingDiagnosticTime =
                now
                + Mathf.Max(
                    0.1f,
                    hearingDiagnosticIntervalSeconds);
        }

        private PlayerId CreateRunnerPlayerId(PlayerRef player)
        {
            var actorId = Runner.GetPlayerActorId(player) ?? player.PlayerId;
            return actorId >= 0 ? new PlayerId(actorId + 1) : PlayerId.Invalid;
        }

        private void ResolveAuthoritativeAttack()
        {
            // The production sink already commits the hit at the validated attack moment.
            if (controller.AttackConsequenceSink == _productionConsequenceSink && _productionConsequenceSink != null)
                return;
            var attackResult = controller.LastAttackResult;
            if (attackResult == StalkerAttackResult.Hit
                && _previousAttackResult != StalkerAttackResult.Hit)
            {
                TryApplyAuthoritativeAttackDamage();
            }

            _previousAttackResult = attackResult;
        }

        private bool TryApplyAuthoritativeAttackDamage()
        {
            if (!Object.HasStateAuthority
                || !TargetPlayer.IsRealPlayer
                || !Runner.TryGetPlayerObject(TargetPlayer, out var playerObject)
                || playerObject.InputAuthority != TargetPlayer)
            {
                UnityEngine.Debug.LogWarning("[StalkerFusion] Rejected attack damage: authoritative target is unavailable.");
                return false;
            }

            var delta = playerObject.transform.position - transform.position;
            if (delta.sqrMagnitude > maximumDamageDistance * maximumDamageDistance)
            {
                UnityEngine.Debug.LogWarning(
                    $"[StalkerFusion] Rejected attack damage against {TargetPlayer}: target left range.");
                return false;
            }

            bool applied = false;
            if (playerObject.TryGetComponent<NetworkPlayerLifeState>(out var lifeState))
            {
                return TryCatchPlayer(lifeState);
            }

            if (playerObject.TryGetComponent<NetworkPlayerHealth>(out var health))
            {
                applied |= health.TryApplyAuthoritativeDamage(Object, attackDamage);
            }

            if (!applied)
            {
                UnityEngine.Debug.LogWarning(
                    $"[StalkerFusion] Rejected attack damage against {TargetPlayer}: health/life state unavailable.");
                return false;
            }

            AttackSequence++;
            RuntimeLog.Log(
                RuntimeLogCategory.StalkerCombat,
                $"[StalkerFusion] Attack committed target={TargetPlayer}, sequence={AttackSequence}, " +
                $"damage={attackDamage}.");
            return true;
        }

        private void ResolveLocalDependencies()
        {
            if (controller == null)
            {
                controller =
                    GetComponent<StalkerController>();
            }

            if (visionSensor == null)
            {
                visionSensor =
                    GetComponent<StalkerVisionSensor>();
            }

            if (_hearingSensor == null)
            {
                _hearingSensor =
                    new ListenerHearingSensor(
                        new UnityListenerOcclusionResolver(
                            acousticBlockerMask,
                            transform,
                            triggerInteraction:
                                QueryTriggerInteraction.Ignore),
                        new ListenerHearingPolicy(
                            hearingThreshold,
                            closedDoorMultiplier,
                            wallMultiplier));
            }

            if (_navigationAgent == null)
            {
                _navigationAgent =
                    GetComponent<NavMeshAgent>();
            }

            if (specialEncounterRuntime == null)
            {
                specialEncounterRuntime =
                    GetComponent<
                        StalkerSpecialEncounterRuntime>();
            }

            if (specialEncounterRuntime == null)
            {
                specialEncounterRuntime =
                    gameObject.AddComponent<
                        StalkerSpecialEncounterRuntime>();
            }
        }

        private void ResolveLifecycle()
        {
            var runner = Runner;
            if (runner == null)
            {
                lifecycle = null;
                return;
            }

            lifecycle = runner.GetComponent<FusionPlayerLifecycle>();
            BindProductionConsequenceSink();
            BindProductionTelemetryProducer();
        }

        private void BindProductionConsequenceSink()
        {
            if (controller == null
                || lifecycle == null
                || Runner == null
                || !Runner.IsServer
                || Object == null
                || !Object.HasStateAuthority)
            {
                return;
            }

            _productionConsequenceSink ??=
                new StalkerNetworkLifeStateConsequenceSink(
                    Runner,
                    lifecycle.IdentityRegistry,
                    OnPlayerDownedByStalkerAuthoritative,
                    OnStalkerHitAppliedAuthoritative);
            if (controller.AttackConsequenceSink == null
                || controller.AttackConsequenceSink is StalkerDiagnosticAttackConsequenceSink)
            {
                controller.AttackConsequenceSink = _productionConsequenceSink;
            }
        }

        private void OnStalkerHitAppliedAuthoritative(
            PlayerId playerId)
        {
            if (Object == null
                || !Object.HasStateAuthority
                || !playerId.IsValid
                || lifecycle == null
                || lifecycle.IdentityRegistry == null
                || !lifecycle.IdentityRegistry.TryGetPlayerRef(
                    playerId,
                    out var targetPlayer)
                || !targetPlayer.IsRealPlayer)
            {
                return;
            }

            RPC_PlayStalkerBite(
                targetPlayer);
        }

        [Rpc(
            RpcSources.StateAuthority,
            RpcTargets.All)]
        private void RPC_PlayStalkerBite(
            [RpcTarget] PlayerRef targetPlayer)
        {
            if (Runner == null
                || !Runner.IsRunning
                || !targetPlayer.IsRealPlayer
                || !Runner.TryGetPlayerObject(
                    targetPlayer,
                    out var playerObject)
                || playerObject == null)
            {
                return;
            }

            var jumpscare =
                playerObject.GetComponent<
                    PlayerJumpscareController>();

            var stalkerPresenter =
                GetComponent<
                    StalkerAnimatorPresenter>();

            if (jumpscare == null
                || stalkerPresenter == null)
            {
                return;
            }

            jumpscare.PlayStalkerBite(
                stalkerPresenter);
        }

        private void OnPlayerDownedByStalkerAuthoritative(
            StalkerDownedPlayerFact fact)
        {
            if (Object == null
                || !Object.HasStateAuthority
                || !fact.IsValid)
            {
                return;
            }

            if (specialEncounterRuntime == null)
            {
                ResolveLocalDependencies();
            }

            if (specialEncounterRuntime == null)
            {
                RuntimeLog.Log(
                    RuntimeLogCategory.StalkerCombat,
                    "[STK_SPECIAL][ARM_FAILED] " +
                    "runtime-missing");

                return;
            }

            specialEncounterRuntime.Arm(fact);
        }

        private void BindProductionTelemetryProducer()
        {
            if (Runner == null
                || !Runner.IsServer
                || Object == null
                || !Object.HasStateAuthority)
            {
                return;
            }

            var monsterIdentity = ResolveTelemetryMonsterIdentity();
            if (!monsterIdentity.IsValid)
            {
                return;
            }

            TelemetryMonsterIdentity = monsterIdentity;
            if (TelemetryProducer != null && !ReferenceEquals(TelemetryProducer, _productionTelemetryProducer))
            {
                return;
            }

            _productionTelemetryProducer ??= new StalkerProductionTelemetryProducer();
            TelemetryProducer = _productionTelemetryProducer;
        }

        private StalkerTelemetryMonsterIdentity ResolveTelemetryMonsterIdentity()
        {
            return Object == null
                ? default
                : new StalkerTelemetryMonsterIdentity(Object.Id.ToString());
        }

        private void SetLegacySimulationSuppressed(bool suppressed)
        {
            if (controller != null)
            {
                controller.SuppressLegacyUpdateSimulation = suppressed;
            }
        }

        private void ApplyOwnedLegacySuppression()
        {
            if (_networkSimulationOwned)
            {
                SetLegacySimulationSuppressed(true);
            }
        }

        private void ClearFrameBuffers()
        {
            _perceptionSnapshots.Clear();
            _targetStatuses.Clear();
            _visibleCandidates.Clear();
            _visibleObjectiveCarrierIds.Clear();
            _hearingObservations.Clear();
            _activeNoiseEvents.Clear();
        }

        private void CollectVisibleObjectiveCarrierIds()
        {
            _visibleObjectiveCarrierIds.Clear();

            for (var i = 0; i < _visibleCandidates.Count; i++)
            {
                var playerId =
                    _visibleCandidates[i].Observation.PlayerId;

                for (var j = 0; j < _perceptionSnapshots.Count; j++)
                {
                    var snapshot = _perceptionSnapshots[j];
                    if (snapshot.PlayerId == playerId
                        && snapshot.IsObjectiveCarrier)
                    {
                        _visibleObjectiveCarrierIds.Add(playerId);
                        break;
                    }
                }
            }
        }

        private void PublishReplicatedPresentationState()
        {
            if (controller == null)
            {
                return;
            }

            _lastAuthoritativePresentationState = BuildCurrentPresentationState();

            if (Object == null || !Object.HasStateAuthority)
            {
                return;
            }

            ReplicatedSemanticState = (int)_lastAuthoritativePresentationState.SemanticState;
            ReplicatedAttackEpisodeId = _lastAuthoritativePresentationState.AttackEpisodeId.IsValid
                ? _lastAuthoritativePresentationState.AttackEpisodeId.Value
                : 0L;
            ReplicatedAttackPhase = (int)_lastAuthoritativePresentationState.AttackPhase;
            ReplicatedAttackProgressSeconds = _lastAuthoritativePresentationState.AttackProgressSeconds;
            ReplicatedAttackHitMomentResolved = _lastAuthoritativePresentationState.AttackHitMomentResolved;
            ReplicatedAttackOutcome = (int)_lastAuthoritativePresentationState.AttackOutcome;
            ReplicatedAttackStartedTick = _lastAuthoritativePresentationState.AttackStartedTick;
            ReplicatedAttackResolvedTick = _lastAuthoritativePresentationState.AttackResolvedTick;
            ReplicatedPresentationActionValue = (int)_lastAuthoritativePresentationState.PresentationAction;
            ReplicatedPresentationActionOrdinal = _lastAuthoritativePresentationState.PresentationActionOrdinal;
            ReplicatedPresentationActionProgress01 = _lastAuthoritativePresentationState.PresentationActionProgress01;
            ReplicatedSpecialPhaseValue = (int)_lastAuthoritativePresentationState.SpecialPhase;
            ReplicatedSpecialSequenceOrdinal = _lastAuthoritativePresentationState.SpecialSequenceOrdinal;
            ReplicatedSpecialPhaseElapsed = _lastAuthoritativePresentationState.SpecialPhaseElapsed;
            ReplicatedSpecialVisible = _lastAuthoritativePresentationState.PresentationVisible;
        }

        private void PublishCommittedTelemetryFacts()
        {
            if (controller == null
                || Runner == null
                || !Runner.IsServer
                || Object == null
                || !Object.HasStateAuthority
                || !TelemetryMonsterIdentity.IsValid
                || TelemetryProducer == null)
            {
                return;
            }

            if (controller.HasCommittedAttackResolutionFact)
            {
                LastAttackTelemetryPublishResult = _telemetryAdapter.TryPublishAttackResolved(
                    TelemetryMonsterIdentity,
                    controller.LastCommittedAttackResolutionFact,
                    TelemetryProducer);
            }

            if (controller.HasCommittedSearchEndedFact)
            {
                LastSearchTelemetryPublishResult = _telemetryAdapter.TryPublishSearchEnded(
                    TelemetryMonsterIdentity,
                    controller.LastCommittedSearchEndedFact,
                    TelemetryProducer);
            }
        }

        private StalkerNetworkPresentationState BuildCurrentPresentationState()
        {
            var activeEpisode = controller.ActiveAttackEpisode;
            var currentAttackActive = activeEpisode.EpisodeId.IsValid
                && (controller.CurrentState == StalkerState.ATTACK || controller.CurrentState == StalkerState.RECOVER);
            var action = ResolvePresentationAction(out var actionProgress);
            var specialPhase = specialEncounterRuntime != null
                ? specialEncounterRuntime.Phase
                : StalkerSpecialEncounterPhase.None;
            var specialSequence = specialEncounterRuntime != null
                ? specialEncounterRuntime.SequenceOrdinal
                : 0u;
            var specialElapsed = specialEncounterRuntime != null
                ? specialEncounterRuntime.PhaseElapsed
                : 0f;
            var visible = specialEncounterRuntime == null || specialEncounterRuntime.PresentationVisible;

            if (!currentAttackActive)
            {
                return new StalkerNetworkPresentationState(
                    controller.CurrentState,
                    StalkerAttackEpisodeId.Invalid,
                    StalkerNetworkAttackPhase.None,
                    0f,
                    false,
                    StalkerAttackOutcome.None,
                    -1L,
                    -1L,
                    action,
                    ResolvePresentationActionOrdinal(action, specialSequence),
                    actionProgress,
                    specialPhase,
                    specialSequence,
                    specialElapsed,
                    visible);
            }

            return new StalkerNetworkPresentationState(
                controller.CurrentState,
                activeEpisode.EpisodeId,
                ResolveAttackPhase(controller.CurrentState, activeEpisode),
                ResolveAttackProgressSeconds(controller.CurrentState, activeEpisode),
                activeEpisode.HitMomentResolved,
                activeEpisode.Outcome,
                activeEpisode.StartedAt.IsValid ? activeEpisode.StartedAt.Tick : -1L,
                activeEpisode.ResolutionTime.IsValid ? activeEpisode.ResolutionTime.Tick : -1L,
                action,
                ResolvePresentationActionOrdinal(action, specialSequence),
                actionProgress,
                specialPhase,
                specialSequence,
                specialElapsed,
                visible);
        }

        private StalkerPresentationAction ResolvePresentationAction(out float progress01)
        {
            progress01 = 0f;
            if (specialEncounterRuntime != null && specialEncounterRuntime.IsActive)
            {
                progress01 = specialEncounterRuntime.PhaseProgress01;
                switch (specialEncounterRuntime.Phase)
                {
                    case StalkerSpecialEncounterPhase.Sniff:
                        return StalkerPresentationAction.SpecialSniff;
                    case StalkerSpecialEncounterPhase.JumpOut:
                        return StalkerPresentationAction.SpecialJumpOut;
                    case StalkerSpecialEncounterPhase.JumpIn:
                        return StalkerPresentationAction.SpecialJumpIn;
                    case StalkerSpecialEncounterPhase.ReactionLock:
                        return StalkerPresentationAction.SpecialReactionRoar;
                    default:
                        return StalkerPresentationAction.None;
                }
            }

            if (controller.CurrentWorldInteractionKind == StalkerWorldInteractionKind.BreakingDoor
                || controller.CurrentWorldInteractionKind == StalkerWorldInteractionKind.BreakingJammer)
            {
                progress01 = controller.WorldInteractionProgress01;
                return StalkerPresentationAction.DoorPunch;
            }

            if (controller.IsSearchLkpSniffActive)
            {
                progress01 = controller.SearchLkpSniffProgress01;
                return StalkerPresentationAction.SearchSniff;
            }

            return StalkerPresentationAction.None;
        }

        private int ResolvePresentationActionOrdinal(
            StalkerPresentationAction action,
            uint specialSequence)
        {
            if (action == StalkerPresentationAction.SearchSniff)
            {
                return controller.SearchLkpSniffOrdinal;
            }

            if (action == StalkerPresentationAction.None)
            {
                return 0;
            }

            return specialSequence > int.MaxValue ? int.MaxValue : (int)specialSequence;
        }

        private static StalkerNetworkAttackPhase ResolveAttackPhase(
            StalkerState state,
            StalkerAttackEpisode episode)
        {
            if (!episode.EpisodeId.IsValid)
            {
                return StalkerNetworkAttackPhase.None;
            }

            if (state == StalkerState.RECOVER)
            {
                return StalkerNetworkAttackPhase.Recover;
            }

            if (state != StalkerState.ATTACK)
            {
                return StalkerNetworkAttackPhase.None;
            }

            if (episode.HitMomentResolved)
            {
                return StalkerNetworkAttackPhase.Resolved;
            }

            return StalkerNetworkAttackPhase.Windup;
        }

        private float ResolveAttackProgressSeconds(
            StalkerState state,
            StalkerAttackEpisode episode)
        {
            if (state == StalkerState.RECOVER)
            {
                return controller.RecoverElapsedTime;
            }

            return episode.WindupElapsedSeconds;
        }

        private static StalkerState ToReplicatedSemanticState(int value)
        {
            return System.Enum.IsDefined(typeof(StalkerState), value)
                ? (StalkerState)value
                : StalkerState.PATROL;
        }

        private static StalkerNetworkAttackPhase ToReplicatedAttackPhase(int value)
        {
            return System.Enum.IsDefined(typeof(StalkerNetworkAttackPhase), value)
                ? (StalkerNetworkAttackPhase)value
                : StalkerNetworkAttackPhase.None;
        }

        private static StalkerAttackOutcome ToReplicatedAttackOutcome(int value)
        {
            return System.Enum.IsDefined(typeof(StalkerAttackOutcome), value)
                ? (StalkerAttackOutcome)value
                : StalkerAttackOutcome.None;
        }

        private static StalkerPresentationAction ToReplicatedPresentationAction(int value)
        {
            return System.Enum.IsDefined(typeof(StalkerPresentationAction), value)
                ? (StalkerPresentationAction)value
                : StalkerPresentationAction.None;
        }

        private static StalkerSpecialEncounterPhase ToReplicatedSpecialPhase(int value)
        {
            return System.Enum.IsDefined(typeof(StalkerSpecialEncounterPhase), value)
                ? (StalkerSpecialEncounterPhase)value
                : StalkerSpecialEncounterPhase.None;
        }

        private StalkerAttackTargetSnapshot? BuildCurrentAttackTargetSnapshot(PlayerId currentTargetId)
        {
            if (!currentTargetId.IsValid)
            {
                return null;
            }

            for (var i = 0; i < _targetStatuses.Count; i++)
            {
                var status = _targetStatuses[i];
                if (status.PlayerId != currentTargetId)
                {
                    continue;
                }

                if (!status.Eligibility.Eligible)
                {
                    return StalkerAttackTargetSnapshot.Missing(currentTargetId);
                }

                for (var j = 0; j < _perceptionSnapshots.Count; j++)
                {
                    var snapshot = _perceptionSnapshots[j];
                    if (snapshot.PlayerId == currentTargetId && snapshot.TargetHierarchyRoot != null)
                    {
                        return new StalkerAttackTargetSnapshot(
                            currentTargetId,
                            true,
                            snapshot.TargetHierarchyRoot.position,
                            controller.AttackConsequenceSink != null);
                    }
                }

                return StalkerAttackTargetSnapshot.Missing(currentTargetId);
            }

            return StalkerAttackTargetSnapshot.Missing(currentTargetId);
        }

        private void ConfigureAuthorityOnlyComponents()
        {
            var isAuthority = Object != null && Object.HasStateAuthority;
            SetDecisionComponentsEnabled(isAuthority);
            RuntimeLog.Log(
                RuntimeLogCategory.StalkerLifecycle,
                $"[StalkerFusion] Spawned authority={isAuthority}; " +
                $"NavMesh/vision decision systems enabled={isAuthority}.");
        }

        private void SetDecisionComponentsEnabled(bool enabled)
        {
            if (_navigationAgent != null) _navigationAgent.enabled = enabled;
            if (visionSensor != null) visionSensor.enabled = enabled;
        }

        private void ApplyReplicatedPresentation()
        {
            //
            // Replicated presentation data is consumed by
            // StalkerAnimatorPresenter.
            //
            // Animator writes intentionally live in the presenter so there is
            // exactly one presentation owner.
            //
        }
    }
}
