using EchoProtocol.AI.Stalker.Networking;
using EchoProtocol.AI.Stalker.Special;
using EchoProtocol.Networking;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

namespace EchoProtocol.AI.Stalker.Presentation
{
    [DisallowMultipleComponent]
    public sealed class StalkerAnimatorPresenter : MonoBehaviour
    {
        private static readonly int Idle1StateHash =
            Animator.StringToHash(
                "Base Layer.Idle1");

        private static readonly int Walk1StateHash =
            Animator.StringToHash(
                "Base Layer.Walk1");

        private static readonly int Walk2StateHash =
            Animator.StringToHash(
                "Base Layer.Walk2");

        private static readonly int CrouchStateHash =
            Animator.StringToHash(
                "Base Layer.Crouch");

        private static readonly int RoarStateHash =
            Animator.StringToHash(
                "Base Layer.Roar");

        private static readonly int BiteStateHash =
            Animator.StringToHash(
                "Base Layer.Bite");

        private static readonly int SniffStateHash =
            Animator.StringToHash(
                "Base Layer.Sniff");

        private static readonly int PunchStateHash =
            Animator.StringToHash(
                "Base Layer.Punch");

        private static readonly int JumpOutStateHash =
            Animator.StringToHash(
                "Base Layer.JumpOut");

        private static readonly int JumpInStateHash =
            Animator.StringToHash(
                "Base Layer.JumpIn");

        private static readonly int PurgeHitStateHash =
            Animator.StringToHash("Base Layer.PurgeHit");

        private static readonly int PurgeStunnedStateHash =
            Animator.StringToHash("Base Layer.PurgeStunned");

        private static readonly int PurgeUpperHitStateHash =
            Animator.StringToHash("Purge Upper.PurgeUpperHit");
        private static readonly int PurgeUpperBiteStateHash =
            Animator.StringToHash("Purge Upper.PurgeUpperBite");
        private static readonly int PurgeMoveRateHash =
            Animator.StringToHash("PurgeMoveRate");

        [Header("Dependencies")]
        [SerializeField]
        private GameObject biteJumpscarePrefab;

        public GameObject BiteJumpscarePrefab => biteJumpscarePrefab;

        [SerializeField]
        private Animator animator;

        [SerializeField]
        private StalkerFusionRuntime fusionRuntime;

        [SerializeField]
        private StalkerController controller;

        [SerializeField]
        private NavMeshAgent navMeshAgent;

        [SerializeField]
        private StalkerAudioController audioController;

        [SerializeField]
        private StalkerAnimationAudioEvents animationAudioEvents;

        [SerializeField]
        private Renderer[] controlledRenderers;

        [Header("Crossfade")]
        [SerializeField, Min(0f)]
        private float defaultCrossfadeSeconds = 0.10f;

        [Header("Movement Presentation")]
        [SerializeField, Min(0f)]
        private float movingSpeedThreshold = 0.05f;

        [SerializeField, Min(0.01f)]
        private float patrolPlaybackSpeed = 1f;

        [SerializeField, Min(0.01f)]
        private float chasePlaybackSpeed = 1f;

        [SerializeField, Min(0.01f)]
        private float searchPlaybackSpeed = 1f;

        [SerializeField, Min(0f)]
        private float visualSpeedFloor = 0.03f;

        private bool _hasPresented;

        private StalkerState _presentedSemanticState;

        private StalkerPresentationAction
            _presentedAction =
                StalkerPresentationAction.None;

        private int _presentedActionOrdinal;

        private StalkerAttackEpisodeId
            _presentedAttackEpisodeId =
                StalkerAttackEpisodeId.Invalid;

        private StalkerAttackEpisodeId
            _playedBiteEpisodeId =
                StalkerAttackEpisodeId.Invalid;

        private int _presentedAnimatorStateHash;
        private int _presentedPurgeUpperStateHash;
        private bool _hasPurgeMoveRateParameter;
        private StalkerAttackEpisodeId _presentedPurgeUpperAttackEpisodeId = StalkerAttackEpisodeId.Invalid;
        private PurgeBossMood? _lastPurgeMood;
        private PlayerRef _presentedDetectNetworkTarget = PlayerRef.None;
        private int _presentedDetectPlayerId;
        private bool _detectRoarAuthorized;

        private bool _lastPresentationVisible = true;

        private bool _localVisibilitySuppressed;

        private Vector3 _lastRootPosition;

        private bool _hasLastRootPosition;

        private void Awake()
        {
            ResolveDependencies();
            ConfigureAnimator();
            ResetMotionSampling();
        }

        private void OnEnable()
        {
            ResolveDependencies();
            ConfigureAnimator();
            ResetMotionSampling();

            SynchronizeImmediate();
        }

        private void Start()
        {
            SynchronizeImmediate();
        }

        private void OnDisable()
        {
            audioController?.StopAllLoops();
            if (animator != null)
            {
                int upperLayer = animator.GetLayerIndex("Purge Upper");
                if (upperLayer >= 0) animator.SetLayerWeight(upperLayer, 0f);
            }
            _hasPresented = false;
            _presentedPurgeUpperStateHash = 0;
            _presentedPurgeUpperAttackEpisodeId = StalkerAttackEpisodeId.Invalid;
            _playedBiteEpisodeId = StalkerAttackEpisodeId.Invalid;
            _presentedDetectNetworkTarget = PlayerRef.None;
            _presentedDetectPlayerId = 0;
            _detectRoarAuthorized = false;
            _lastPurgeMood = null;
        }

        private void Update()
        {
            if (!ResolveDependencies()
                || !CanDriveAnimator())
            {
                return;
            }

            var presentation =
                ResolvePresentationState();

            var moveSpeed =
                ResolveMoveSpeed();

            ApplyVisibility(
                presentation.PresentationVisible);

            //
            // Continue sampling transform movement while hidden.
            // This prevents the hidden network teleport from
            // being interpreted as huge locomotion speed when
            // JumpIn becomes visible.
            //
            if (!presentation.PresentationVisible)
            {
                audioController?.StopAllLoops();
                int upperLayer = animator.GetLayerIndex("Purge Upper");
                if (upperLayer >= 0) animator.SetLayerWeight(upperLayer, 0f);
                _presentedPurgeUpperStateHash = 0;
                _hasPresented = false;
                _lastPresentationVisible = false;
                return;
            }

            var forceRefresh =
                !_lastPresentationVisible;

            _lastPresentationVisible = true;

            ApplyPresentation(
                presentation,
                moveSpeed,
                forceRefresh,
                immediate: false);
        }

        private bool ResolveDependencies()
        {
            if (fusionRuntime == null)
            {
                fusionRuntime =
                    GetComponent<StalkerFusionRuntime>()
                    ?? GetComponentInParent<
                        StalkerFusionRuntime>()
                    ?? GetComponentInChildren<
                        StalkerFusionRuntime>(
                            true);
            }

            if (controller == null)
            {
                controller =
                    GetComponent<StalkerController>()
                    ?? GetComponentInParent<
                        StalkerController>()
                    ?? GetComponentInChildren<
                        StalkerController>(
                            true);
            }

            if (navMeshAgent == null)
            {
                navMeshAgent =
                    GetComponent<NavMeshAgent>()
                    ?? GetComponentInParent<
                        NavMeshAgent>()
                    ?? GetComponentInChildren<
                        NavMeshAgent>(
                            true);
            }

            if (animator == null)
            {
                animator =
                    GetComponentInChildren<
                        Animator>(
                            true);
            }

            if (audioController == null)
            {
                audioController =
                    GetComponent<StalkerAudioController>()
                    ?? GetComponentInParent<StalkerAudioController>()
                    ?? GetComponentInChildren<StalkerAudioController>(true);
            }

            if (animationAudioEvents == null && animator != null)
            {
                animationAudioEvents =
                    animator.GetComponent<StalkerAnimationAudioEvents>()
                    ?? animator.gameObject.AddComponent<StalkerAnimationAudioEvents>();
            }

            animationAudioEvents?.Bind(audioController);

            if (controlledRenderers == null
                || controlledRenderers.Length == 0)
            {
                controlledRenderers =
                    GetComponentsInChildren<
                        Renderer>(
                            true);
            }

            return animator != null;
        }

        private void ConfigureAnimator()
        {
            if (animator == null)
            {
                return;
            }

            animator.applyRootMotion = false;
            _hasPurgeMoveRateParameter = false;
            for (int i = 0; i < animator.parameterCount; i++)
                if (animator.GetParameter(i).nameHash == PurgeMoveRateHash)
                {
                    _hasPurgeMoveRateParameter = true;
                    break;
                }
        }

        private void ResetMotionSampling()
        {
            _lastRootPosition =
                transform.position;

            _hasLastRootPosition = true;
        }

        private void SynchronizeImmediate()
        {
            if (!ResolveDependencies()
                || !CanDriveAnimator())
            {
                return;
            }

            var presentation =
                ResolvePresentationState();

            var moveSpeed =
                ResolveMoveSpeed();

            ApplyVisibility(
                presentation.PresentationVisible);

            _lastPresentationVisible =
                presentation.PresentationVisible;

            if (!presentation.PresentationVisible)
            {
                audioController?.StopAllLoops();
                _hasPresented = false;
                return;
            }

            ApplyPresentation(
                presentation,
                moveSpeed,
                forceRefresh: true,
                immediate: true);

            animator.Update(0f);
        }

        private StalkerNetworkPresentationState
            ResolvePresentationState()
        {
            if (fusionRuntime != null)
            {
                if (fusionRuntime.Object == null
                    || !fusionRuntime.Object.IsValid)
                {
                    return BuildLocalPresentationState();
                }

                var presentation =
                    fusionRuntime
                        .GetReplicatedPresentationState();

                var semanticState =
                    presentation.SemanticState;

                if (fusionRuntime.Object.HasStateAuthority
                    && controller != null)
                {
                    semanticState =
                        controller.CurrentState;
                }
                else if (
                    fusionRuntime.ReplicatedState
                        != semanticState
                    && !presentation.HasAttackEpisode)
                {
                    semanticState =
                        fusionRuntime.ReplicatedState;
                }

                if (semanticState
                    == presentation.SemanticState)
                {
                    return presentation;
                }

                return CopyWithSemanticState(
                    presentation,
                    semanticState);
            }

            return BuildLocalPresentationState();
        }

        private static StalkerNetworkPresentationState
            CopyWithSemanticState(
                StalkerNetworkPresentationState source,
                StalkerState semanticState)
        {
            return new StalkerNetworkPresentationState(
                semanticState,
                source.AttackEpisodeId,
                source.AttackPhase,
                source.AttackProgressSeconds,
                source.AttackHitMomentResolved,
                source.AttackOutcome,
                source.AttackStartedTick,
                source.AttackResolvedTick,
                source.PresentationAction,
                source.PresentationActionOrdinal,
                source.PresentationActionProgress01,
                source.SpecialPhase,
                source.SpecialSequenceOrdinal,
                source.SpecialPhaseElapsed,
                source.PresentationVisible);
        }

        private StalkerNetworkPresentationState
            BuildLocalPresentationState()
        {
            if (controller != null
                && (controller.CurrentState
                        == StalkerState.ATTACK
                    || controller.CurrentState
                        == StalkerState.RECOVER)
                && controller
                    .ActiveAttackEpisode
                    .EpisodeId
                    .IsValid)
            {
                var episode =
                    controller.ActiveAttackEpisode;

                var recovering =
                    controller.CurrentState
                    == StalkerState.RECOVER;

                return new StalkerNetworkPresentationState(
                    controller.CurrentState,
                    episode.EpisodeId,
                    recovering
                        ? StalkerNetworkAttackPhase.Recover
                        : episode.HitMomentResolved
                            ? StalkerNetworkAttackPhase.Resolved
                            : StalkerNetworkAttackPhase.Windup,
                    recovering
                        ? controller.RecoverElapsedTime
                        : episode.WindupElapsedSeconds,
                    episode.HitMomentResolved,
                    episode.Outcome,
                    episode.StartedAt.IsValid
                        ? episode.StartedAt.Tick
                        : -1L,
                    episode.ResolutionTime.IsValid
                        ? episode.ResolutionTime.Tick
                        : -1L);
            }

            return new StalkerNetworkPresentationState(
                controller != null
                    ? controller.CurrentState
                    : StalkerState.PATROL,
                StalkerAttackEpisodeId.Invalid,
                StalkerNetworkAttackPhase.None,
                0f,
                false,
                StalkerAttackOutcome.None,
                -1L,
                -1L);
        }

        private void ApplyPresentation(
            StalkerNetworkPresentationState presentation,
            float moveSpeed,
            bool forceRefresh,
            bool immediate)
        {
            var moving =
                moveSpeed > movingSpeedThreshold;

            var targetHash =
                ResolveAnimatorStateHash(
                    presentation,
                    moving);

            var purgeMood = ResolvePurgeMood();
            if (purgeMood.HasValue)
            {
                targetHash = ResolvePurgeAnimatorStateHash(
                    purgeMood.Value, presentation, moving, targetHash);
            }
            if (purgeMood == PurgeBossMood.Calm
                && NetworkMatchState.Instance != null
                && NetworkMatchState.Instance.PurgeRageTier == 0
                && presentation.SemanticState == StalkerState.CHASE
                && targetHash == Walk2StateHash)
                targetHash = Walk1StateHash;
            // Walk2 remains the lower-body source under Hit/Bite. Match its stride
            // to actual travel, especially while valid UV contact cuts speed by 90%.
            if (_hasPurgeMoveRateParameter)
                animator.SetFloat(PurgeMoveRateHash, purgeMood.HasValue
                    ? Mathf.Clamp(moveSpeed / 6.6f, .18f, 1.4f) : 1f);
            ApplyPurgeUpperBody(presentation, purgeMood, moving, forceRefresh);

            var semanticChanged =
                !_hasPresented
                || _presentedSemanticState
                    != presentation.SemanticState;

            var detectTargetChanged = HasDetectTargetChanged(presentation);

            ApplyAudioPresentation(
                presentation,
                moving,
                semanticChanged,
                detectTargetChanged);

            if (purgeMood == PurgeBossMood.Warning
                && _lastPurgeMood != PurgeBossMood.Warning)
            {
                audioController?.BeginWarningRoarEntry();
            }
            _lastPurgeMood = purgeMood;

            if (_detectRoarAuthorized && presentation.SemanticState == StalkerState.DETECT
                && purgeMood != PurgeBossMood.Warning
                && _presentedAnimatorStateHash == RoarStateHash
                && animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 0.98f)
                _detectRoarAuthorized = false;

            if (presentation.SemanticState == StalkerState.DETECT
                && targetHash == RoarStateHash && !_detectRoarAuthorized)
                targetHash = moving ? Walk1StateHash : Idle1StateHash;

            var actionChanged =
                !_hasPresented
                || _presentedAction
                    != presentation.PresentationAction
                || _presentedActionOrdinal
                    != presentation
                        .PresentationActionOrdinal;

            var attackEpisodeChanged =
                presentation.SemanticState
                    == StalkerState.ATTACK
                && presentation
                    .AttackEpisodeId
                    .IsValid
                && _presentedAttackEpisodeId
                    != presentation.AttackEpisodeId;

            var animatorStateChanged =
                !_hasPresented
                || _presentedAnimatorStateHash
                    != targetHash;

            animator.speed = targetHash == PurgeHitStateHash
                || targetHash == PurgeStunnedStateHash
                || purgeMood == PurgeBossMood.Warning
                    ? 1f : ResolvePlaybackSpeed(presentation);

            bool continuousPurgeReaction = targetHash == PurgeHitStateHash
                || targetHash == PurgeStunnedStateHash;
            if (!forceRefresh
                && !animatorStateChanged
                && continuousPurgeReaction)
            {
                _presentedSemanticState = presentation.SemanticState;
                _presentedAction = presentation.PresentationAction;
                _presentedActionOrdinal = presentation.PresentationActionOrdinal;
                _presentedAttackEpisodeId = presentation.AttackEpisodeId;
                CapturePresentedDetectTarget(presentation);
                _hasPresented = true;
                return;
            }

            if (!forceRefresh
                && !semanticChanged
                && !detectTargetChanged
                && !actionChanged
                && !attackEpisodeChanged
                && !animatorStateChanged)
            {
                return;
            }

            var normalizedStart =
                ResolveNormalizedStart(
                    presentation);

            var enteringDetect =
                presentation.SemanticState == StalkerState.DETECT
                && (semanticChanged || detectTargetChanged);

            if (immediate
                || !_hasPresented
                || forceRefresh
                || enteringDetect)
            {
                animator.Play(
                    targetHash,
                    0,
                    normalizedStart);
            }
            else
            {
                animator.CrossFade(
                    targetHash,
                    Mathf.Max(
                        0f,
                        defaultCrossfadeSeconds),
                    0,
                    normalizedStart);
            }

            _presentedSemanticState =
                presentation.SemanticState;

            _presentedAction =
                presentation.PresentationAction;

            _presentedActionOrdinal =
                presentation
                    .PresentationActionOrdinal;

            _presentedAttackEpisodeId =
                presentation.AttackEpisodeId;

            _presentedAnimatorStateHash =
                targetHash;

            CapturePresentedDetectTarget(presentation);

            _hasPresented = true;
        }

        private PurgeBossMood? ResolvePurgeMood()
        {
            var match = NetworkMatchState.Instance;
            if (match == null || fusionRuntime == null
                || fusionRuntime.Object == null || !fusionRuntime.Object.IsValid
                || match.PurgeBossId != fusionRuntime.Object.Id)
                return null;
            return match.PurgeMood;
        }

        private int ResolvePurgeAnimatorStateHash(
            PurgeBossMood mood,
            StalkerNetworkPresentationState presentation,
            bool moving,
            int normalHash)
        {
            if (mood == PurgeBossMood.Warning)
            {
                return RoarStateHash;
            }
            if (mood == PurgeBossMood.Staggered)
            {
                return PurgeStunnedStateHash;
            }

            var match = NetworkMatchState.Instance;
            if ((mood == PurgeBossMood.Hunting || mood == PurgeBossMood.Calm) && match != null
                && match.PurgeUvContact)
            {
                return moving ? Walk2StateHash : PurgeHitStateHash;
            }

            if (presentation.SemanticState == StalkerState.ATTACK && moving)
                return Walk2StateHash;

            if (mood == PurgeBossMood.Calm
                && presentation.SemanticState == StalkerState.SEARCH)
                return moving ? Walk1StateHash : CrouchStateHash;
            return normalHash;
        }

        private void ApplyPurgeUpperBody(StalkerNetworkPresentationState presentation,
            PurgeBossMood? mood, bool moving, bool forceRefresh)
        {
            int layer = animator.GetLayerIndex("Purge Upper");
            if (layer < 0) return;
            int target = 0;
            var match = NetworkMatchState.Instance;
            if (mood.HasValue && moving && mood != PurgeBossMood.Staggered
                && mood != PurgeBossMood.Warning)
            {
                if (match != null && match.PurgeUvContact)
                    target = PurgeUpperHitStateHash;
                else if (presentation.SemanticState == StalkerState.ATTACK)
                    target = PurgeUpperBiteStateHash;
            }
            bool newAttack = target == PurgeUpperBiteStateHash
                && presentation.AttackEpisodeId.IsValid
                && _presentedPurgeUpperAttackEpisodeId != presentation.AttackEpisodeId;
            if (target != 0 && (forceRefresh || target != _presentedPurgeUpperStateHash || newAttack))
            {
                animator.Play(target, layer, target == PurgeUpperBiteStateHash
                    ? ResolveNormalizedStart(presentation) : 0f);
            }
            animator.SetLayerWeight(layer, Mathf.MoveTowards(animator.GetLayerWeight(layer),
                target == 0 ? 0f : 1f, Time.deltaTime * 9f));
            _presentedPurgeUpperStateHash = target;
            _presentedPurgeUpperAttackEpisodeId = target == PurgeUpperBiteStateHash
                ? presentation.AttackEpisodeId : StalkerAttackEpisodeId.Invalid;
        }

        private void ApplyAudioPresentation(
            StalkerNetworkPresentationState presentation,
            bool moving,
            bool semanticChanged,
            bool detectTargetChanged)
        {
            if (audioController == null)
            {
                return;
            }

            audioController.SetMoving(moving);

            var purgeMood = ResolvePurgeMood();
            var pursuing = IsPursuing(presentation.SemanticState)
                && (!purgeMood.HasValue || purgeMood == PurgeBossMood.Hunting);
            var isLocalTarget = IsTargetingLocalPlayer();

            audioController.UpdateChasePresentation(pursuing, isLocalTarget);

            if (presentation.SemanticState != StalkerState.DETECT
                && purgeMood != PurgeBossMood.Warning)
            {
                _detectRoarAuthorized = false;
                audioController.StopDetectVoice();
            }

            if (semanticChanged)
            {
                switch (presentation.SemanticState)
                {
                    case StalkerState.PATROL:
                        audioController.EnterIdle();
                        break;
                    case StalkerState.DETECT:
                        _detectRoarAuthorized = purgeMood != PurgeBossMood.Warning
                            && audioController.BeginDetectAudioEntry(ResolveDetectTargetKey());
                        audioController.EnterIdle();
                        if (!purgeMood.HasValue && _detectRoarAuthorized) audioController.PlayDetect();
                        break;
                    case StalkerState.ATTACK:
                        audioController.BeginAttackAudioEpisode();
                        break;
                    case StalkerState.SEARCH:
                        audioController.EnterIdle();
                        audioController.PlaySearch();
                        break;
                }
            }

            else if (presentation.SemanticState == StalkerState.DETECT
                     && detectTargetChanged)
            {
                _detectRoarAuthorized = purgeMood != PurgeBossMood.Warning
                    && audioController.BeginDetectAudioEntry(ResolveDetectTargetKey());
                audioController.EnterIdle();
                if (!purgeMood.HasValue && _detectRoarAuthorized) audioController.PlayDetectForNewTarget();
            }

            if (presentation.AttackHitMomentResolved
                && presentation.AttackEpisodeId.IsValid
                && _playedBiteEpisodeId
                    != presentation.AttackEpisodeId)
            {
                audioController.PlayBite();
                _playedBiteEpisodeId = presentation.AttackEpisodeId;
            }
        }

        private bool HasDetectTargetChanged(
            StalkerNetworkPresentationState presentation)
        {
            if (!_hasPresented
                || presentation.SemanticState != StalkerState.DETECT)
            {
                return false;
            }

            if (fusionRuntime != null
                && fusionRuntime.Object != null
                && fusionRuntime.Object.IsValid
                && fusionRuntime.Runner != null
                && fusionRuntime.Runner.IsRunning)
            {
                return fusionRuntime.TargetPlayer != _presentedDetectNetworkTarget;
            }

            return controller != null
                && controller.DetectionTargetId.Value != _presentedDetectPlayerId;
        }

        private int ResolveDetectTargetKey()
        {
            if (fusionRuntime != null && fusionRuntime.Object != null
                && fusionRuntime.Object.IsValid && fusionRuntime.Runner != null
                && fusionRuntime.Runner.IsRunning && fusionRuntime.TargetPlayer.IsRealPlayer)
                return fusionRuntime.TargetPlayer.RawEncoded;
            return controller != null ? controller.DetectionTargetId.Value : 0;
        }

        private void CapturePresentedDetectTarget(
            StalkerNetworkPresentationState presentation)
        {
            if (presentation.SemanticState != StalkerState.DETECT)
            {
                _presentedDetectNetworkTarget = PlayerRef.None;
                _presentedDetectPlayerId = 0;
                return;
            }

            if (fusionRuntime != null
                && fusionRuntime.Object != null
                && fusionRuntime.Object.IsValid
                && fusionRuntime.Runner != null
                && fusionRuntime.Runner.IsRunning)
            {
                _presentedDetectNetworkTarget = fusionRuntime.TargetPlayer;
            }
            else
            {
                _presentedDetectNetworkTarget = PlayerRef.None;
            }

            _presentedDetectPlayerId = controller != null
                ? controller.DetectionTargetId.Value
                : 0;
        }

        private bool IsTargetingLocalPlayer()
        {
            if (fusionRuntime != null && fusionRuntime.Object != null && fusionRuntime.Runner != null && fusionRuntime.Runner.IsRunning)
            {
                var targetPlayer = fusionRuntime.TargetPlayer;
                if (targetPlayer.IsRealPlayer)
                {
                    return targetPlayer == fusionRuntime.Runner.LocalPlayer;
                }

                if (fusionRuntime.Object.HasStateAuthority && controller != null)
                {
                    return IsControllerTargetLocal();
                }

                return false;
            }

            if (controller != null)
            {
                return IsControllerTargetLocal();
            }

            return true;
        }

        private bool IsControllerTargetLocal()
        {
            if (controller == null)
            {
                return false;
            }

            var target = controller.CurrentTarget != null ? controller.CurrentTarget : controller.DetectionTarget;
            if (target == null)
            {
                return false;
            }

            if (target.gameObject.TryGetComponent<NetworkObject>(out var netObj) && netObj.IsValid)
            {
                return netObj.HasInputAuthority;
            }

            return true;
        }

        private static bool IsPursuing(StalkerState state)
        {
            return state == StalkerState.CHASE
                || state == StalkerState.ATTACK
                || state == StalkerState.RECOVER;
        }

        private static int ResolveAnimatorStateHash(
            StalkerNetworkPresentationState presentation,
            bool moving)
        {
            //
            // Semantic DETECT must always own presentation.
            //
            // This prevents a stale SearchSniff / previous presentation
            // action from masking the DETECT Roar when SEARCH reacquires
            // a visible player.
            //
            if (presentation.SemanticState == StalkerState.DETECT)
            {
                return RoarStateHash;
            }

            switch (presentation.PresentationAction)
            {
                case StalkerPresentationAction
                    .SpecialSniff:

                    return SniffStateHash;

                case StalkerPresentationAction
                    .SpecialJumpOut:

                    return JumpOutStateHash;

                case StalkerPresentationAction
                    .SpecialJumpIn:

                    return JumpInStateHash;

                case StalkerPresentationAction
                    .SpecialReactionRoar:

                    return RoarStateHash;

                case StalkerPresentationAction
                    .DoorPunch:

                    return PunchStateHash;

            case StalkerPresentationAction
                .SearchSniff:

                return SniffStateHash;
        }

        //
        // During Special Approach the semantic FSM is intentionally
        // still RECOVER because the six-state FSM is frozen.
        //
        // Presentation must therefore use the replicated Special
        // phase rather than RECOVER, otherwise the Stalker would
        // slide toward the Downed Player while playing Idle1.
        //
        if (presentation.SpecialPhase
            == StalkerSpecialEncounterPhase
                .ApproachDownedPlayer)
        {
            return Walk1StateHash;
        }

        return presentation.SemanticState switch
        {
            StalkerState.PATROL =>
                moving
                        ? Walk1StateHash
                        : Idle1StateHash,

                StalkerState.DETECT =>
                    RoarStateHash,

                StalkerState.CHASE =>
                    Walk2StateHash,

                StalkerState.ATTACK =>
                    BiteStateHash,

                StalkerState.SEARCH =>
                    moving
                        ? Walk2StateHash
                        : CrouchStateHash,

                StalkerState.RECOVER =>
                    presentation.HasAttackEpisode
                    && presentation.AttackOutcome
                        == StalkerAttackOutcome.Hit
                        ? BiteStateHash
                        : Idle1StateHash,

                _ =>
                    Idle1StateHash
            };
        }

        private float ResolvePlaybackSpeed(
            StalkerNetworkPresentationState presentation)
        {
            if (presentation.PresentationAction
                != StalkerPresentationAction.None)
        {
            return 1f;
        }

        if (presentation.SpecialPhase
            == StalkerSpecialEncounterPhase
                .ApproachDownedPlayer)
        {
            return Mathf.Max(
                0.01f,
                patrolPlaybackSpeed);
        }

        return presentation.SemanticState switch
        {
            StalkerState.PATROL =>
                Mathf.Max(
                    0.01f,
                    patrolPlaybackSpeed),

            StalkerState.CHASE =>
                Mathf.Max(
                    0.01f,
                    chasePlaybackSpeed),

            StalkerState.SEARCH =>
                Mathf.Max(
                    0.01f,
                    searchPlaybackSpeed),

            _ => 1f
        };
        }

        private float ResolveNormalizedStart(
            StalkerNetworkPresentationState presentation)
        {
            if (presentation.PresentationAction
                != StalkerPresentationAction.None)
            {
                return Mathf.Clamp01(
                    presentation
                        .PresentationActionProgress01);
            }

            if (presentation.SemanticState
                    == StalkerState.ATTACK
                && presentation.HasAttackEpisode)
            {
                var duration =
                    controller != null
                        ? Mathf.Max(
                            0.01f,
                            controller.AttackWindupSeconds)
                        : 0.75f;

                return Mathf.Clamp01(
                    presentation
                        .AttackProgressSeconds
                    / duration);
            }

            return 0f;
        }

        private float ResolveMoveSpeed()
        {
            var agentSpeed = 0f;

            if (controller != null && controller.HasAuthoritativeLocomotion)
            {
                agentSpeed = controller.AuthoritativeMoveSpeed;
            }
            else if (navMeshAgent != null
                && navMeshAgent.enabled)
            {
                agentSpeed =
                    navMeshAgent.velocity.magnitude;
            }

            if (!_hasLastRootPosition)
            {
                ResetMotionSampling();
                return agentSpeed;
            }

            var delta =
                transform.position
                - _lastRootPosition;

            delta.y = 0f;

            var visualSpeed =
                Time.deltaTime > 0f
                    ? delta.magnitude
                        / Time.deltaTime
                    : 0f;

            _lastRootPosition =
                transform.position;

            if (visualSpeed
                < visualSpeedFloor)
            {
                visualSpeed = 0f;
            }

            return Mathf.Max(
                agentSpeed,
                visualSpeed);
        }

        public void SetLocalVisibilitySuppressed(
            bool suppressed)
        {
            _localVisibilitySuppressed =
                suppressed;

            ApplyVisibility(
                _lastPresentationVisible);
        }

        private void ApplyVisibility(
            bool visible)
        {
            if (controlledRenderers == null)
            {
                return;
            }

            for (var i = 0;
                 i < controlledRenderers.Length;
                 i++)
            {
                var current =
                    controlledRenderers[i];

                if (current != null)
                {
                    current.enabled =
                        visible
                        && !_localVisibilitySuppressed;
                }
            }
        }

        private bool CanDriveAnimator()
        {
            return animator != null
                && animator.isInitialized
                && animator
                    .runtimeAnimatorController
                    != null;
        }
    }
}
