using System.Collections.Generic;
using EchoProtocol.AI.Common;
using EchoProtocol.AI.Stalker.Telemetry;
using EchoProtocol.Networking;
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
    public sealed class StalkerFusionRuntime : NetworkBehaviour
    {
        [SerializeField] private StalkerController controller;
        [SerializeField] private StalkerVisionSensor visionSensor;
        [SerializeField] private FusionPlayerLifecycle lifecycle;

        [Header("Authoritative Combat")]
        [SerializeField, Min(1)] private int attackDamage = 25;
        [SerializeField, Min(0.1f)] private float maximumDamageDistance = 2f;

        [Header("Replicated Presentation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string animatorStateParameter = "MonsterState";

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
        private readonly StalkerPresentationDriver _presentationDriver = new StalkerPresentationDriver();
        private bool _networkSimulationOwned;
        private AiSimulationStep _lastAuthoritativeStep;
        private StalkerNetworkLifeStateConsequenceSink _productionConsequenceSink;
        private StalkerProductionTelemetryProducer _productionTelemetryProducer;
        private StalkerNetworkPresentationState _lastAuthoritativePresentationState;

        [Networked] public int ReplicatedSemanticState { get; private set; }
        [Networked] public long ReplicatedAttackEpisodeId { get; private set; }
        [Networked] public int ReplicatedAttackPhase { get; private set; }
        [Networked] public float ReplicatedAttackProgressSeconds { get; private set; }
        [Networked] public NetworkBool ReplicatedAttackHitMomentResolved { get; private set; }
        [Networked] public int ReplicatedAttackOutcome { get; private set; }
        [Networked] public long ReplicatedAttackStartedTick { get; private set; }
        [Networked] public long ReplicatedAttackResolvedTick { get; private set; }
        private NavMeshAgent _navigationAgent;
        private StalkerAttackResult _previousAttackResult;
        private int _animatorStateParameterHash;
        private bool _networkPrefabGuard;

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
            ResolveLocalDependencies();
            _animatorStateParameterHash = Animator.StringToHash(animatorStateParameter);
            _networkPrefabGuard = GetComponent<NetworkObject>() != null;
            if (_networkPrefabGuard)
            {
                SetLegacySimulationSuppressed(true);
                SetDecisionComponentsEnabled(false);
            }
        }

        private void OnEnable()
        {
            ResolveLocalDependencies();
            ApplyOwnedLegacySuppression();
        }

        private void OnDisable()
        {
            SetLegacySimulationSuppressed(_networkSimulationOwned || _networkPrefabGuard);
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

            var input = new StalkerSimulationInput(
                step,
                _visibleCandidates,
                _targetStatuses,
                BuildCurrentAttackTargetSnapshot(controller.CurrentTargetId));

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
                ReplicatedAttackResolvedTick);
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
                var isDowned = playerObject.TryGetComponent<NetworkPlayerHealth>(out var health) && health.IsDowned;
                var eligibilitySnapshot = new StalkerTargetEligibilitySnapshot(
                    isGameplayPlayer,
                    true,
                    isDowned,
                    false,
                    false);
                _targetStatuses.Add(new StalkerTargetStatus(
                    playerId,
                    StalkerTargetEligibility.Evaluate(eligibilitySnapshot)));
                _perceptionSnapshots.Add(new StalkerPerceptionTargetSnapshot(
                    playerId,
                    playerObject.transform,
                    playerObject.transform,
                    eligibilitySnapshot));
            }

            return true;
        }

        private PlayerId CreateRunnerPlayerId(PlayerRef player)
        {
            var actorId = Runner.GetPlayerActorId(player) ?? player.PlayerId;
            return actorId >= 0 ? new PlayerId(actorId + 1) : PlayerId.Invalid;
        }

        private void ResolveAuthoritativeAttack()
        {
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

            if (!playerObject.TryGetComponent<NetworkPlayerHealth>(out var health)
                || !health.TryApplyAuthoritativeDamage(Object, attackDamage))
            {
                UnityEngine.Debug.LogWarning(
                    $"[StalkerFusion] Rejected attack damage against {TargetPlayer}: health state unavailable.");
                return false;
            }

            AttackSequence++;
            UnityEngine.Debug.Log(
                $"[StalkerFusion] Attack committed target={TargetPlayer}, sequence={AttackSequence}, " +
                $"damage={attackDamage}.");
            return true;
        }

        private void ResolveLocalDependencies()
        {
            if (controller == null)
            {
                controller = GetComponent<StalkerController>();
            }

            if (visionSensor == null)
            {
                visionSensor = GetComponent<StalkerVisionSensor>();
            }

            if (_navigationAgent == null)
            {
                _navigationAgent = GetComponent<NavMeshAgent>();
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
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
                    lifecycle.IdentityRegistry);
            if (controller.AttackConsequenceSink == null
                || controller.AttackConsequenceSink is StalkerDiagnosticAttackConsequenceSink)
            {
                controller.AttackConsequenceSink = _productionConsequenceSink;
            }
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
            if (_networkSimulationOwned || _networkPrefabGuard)
            {
                SetLegacySimulationSuppressed(true);
            }
        }

        private void ClearFrameBuffers()
        {
            _perceptionSnapshots.Clear();
            _targetStatuses.Clear();
            _visibleCandidates.Clear();
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
                    -1L);
            }

            return new StalkerNetworkPresentationState(
                controller.CurrentState,
                activeEpisode.EpisodeId,
                ResolveAttackPhase(controller.CurrentState, activeEpisode),
                ResolveAttackProgressSeconds(controller.CurrentState, activeEpisode),
                activeEpisode.HitMomentResolved,
                activeEpisode.Outcome,
                activeEpisode.StartedAt.IsValid ? activeEpisode.StartedAt.Tick : -1L,
                activeEpisode.ResolutionTime.IsValid ? activeEpisode.ResolutionTime.Tick : -1L);
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
            UnityEngine.Debug.Log(
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
            if (animator != null && _animatorStateParameterHash != 0)
            {
                animator.SetInteger(_animatorStateParameterHash, (int)ReplicatedState);
            }
        }
    }
}
