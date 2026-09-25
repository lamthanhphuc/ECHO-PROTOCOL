using EchoProtocol.AI.Stalker.Networking;
using EchoProtocol.AI.Stalker.Special;
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

        private static readonly int DeathStateHash =
            Animator.StringToHash(
                "Base Layer.Death");

        [Header("Dependencies")]
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
            _hasPresented = false;
            _playedBiteEpisodeId = StalkerAttackEpisodeId.Invalid;
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

            var semanticChanged =
                !_hasPresented
                || _presentedSemanticState
                    != presentation.SemanticState;

            ApplyAudioPresentation(
                presentation,
                moving,
                semanticChanged);

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

            animator.speed =
                ResolvePlaybackSpeed(
                    presentation);

            if (!forceRefresh
                && !semanticChanged
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
                && semanticChanged;

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

            _hasPresented = true;
        }

        private void ApplyAudioPresentation(
            StalkerNetworkPresentationState presentation,
            bool moving,
            bool semanticChanged)
        {
            if (audioController == null)
            {
                return;
            }

            audioController.SetMoving(moving);

            if (semanticChanged)
            {
                var wasPursuing =
                    _hasPresented
                    && IsPursuing(_presentedSemanticState);
                var pursuing = IsPursuing(presentation.SemanticState);

                if (pursuing && !wasPursuing)
                {
                    audioController.EnterChase();
                }
                else if (!pursuing && wasPursuing)
                {
                    audioController.ExitChase();
                }

                switch (presentation.SemanticState)
                {
                    case StalkerState.PATROL:
                        audioController.EnterIdle();
                        break;
                    case StalkerState.DETECT:
                        audioController.BeginDetectAudioEntry();
                        audioController.EnterIdle();
                        audioController.PlayDetect();
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

            if (presentation.AttackHitMomentResolved
                && presentation.AttackEpisodeId.IsValid
                && _playedBiteEpisodeId
                    != presentation.AttackEpisodeId)
            {
                audioController.PlayBite();
                _playedBiteEpisodeId = presentation.AttackEpisodeId;
            }
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
                    CrouchStateHash,

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

            if (navMeshAgent != null
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
