using EchoProtocol.AI.Stalker.Networking;
using UnityEngine;
using UnityEngine.AI;

namespace EchoProtocol.AI.Stalker.Presentation
{
    [DisallowMultipleComponent]
    public sealed class StalkerAnimatorPresenter : MonoBehaviour
    {
        private enum PresentationTransient
        {
            None,
            RunStop,
            Turn,
            TurnLeft,
            TurnRight
        }

        private static readonly int StalkerStateHash = Animator.StringToHash("StalkerState");
        private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
        private static readonly int StateProgressHash = Animator.StringToHash("StateProgress");
        private static readonly int AttackProgressHash = Animator.StringToHash("AttackProgress");
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

        private static readonly int IdleStateHash = Animator.StringToHash("Base Layer.Idle");
        private static readonly int WalkStateHash = Animator.StringToHash("Base Layer.Walk");
        private static readonly int RunStateHash = Animator.StringToHash("Base Layer.Run");
        private static readonly int AlertStateHash = Animator.StringToHash("Base Layer.Alert");
        private static readonly int SearchStateHash = Animator.StringToHash("Base Layer.Search");
        private static readonly int AttackStateHash = Animator.StringToHash("Base Layer.Attack");
        private static readonly int RecoverStateHash = Animator.StringToHash("Base Layer.Recover");
        private static readonly int RunStopStateHash = Animator.StringToHash("Base Layer.RunStop");
        private static readonly int TurnStateHash = Animator.StringToHash("Base Layer.Turn");
        private static readonly int TurnLeftStateHash = Animator.StringToHash("Base Layer.TurnLeft");
        private static readonly int TurnRightStateHash = Animator.StringToHash("Base Layer.TurnRight");

        [SerializeField] private Animator animator;
        [SerializeField] private StalkerFusionRuntime fusionRuntime;
        [SerializeField] private StalkerController controller;
        [SerializeField] private NavMeshAgent navMeshAgent;

        [Header("Crossfade Durations")]
        [SerializeField, Min(0f)] private float defaultCrossfadeSeconds = 0.1f;
        [SerializeField, Min(0f)] private float idlePatrolCrossfadeSeconds = 0.15f;
        [SerializeField, Min(0f)] private float patrolDetectCrossfadeSeconds = 0.08f;
        [SerializeField, Min(0f)] private float detectChaseCrossfadeSeconds = 0.1f;
        [SerializeField, Min(0f)] private float chaseAttackCrossfadeSeconds = 0.05f;
        [SerializeField, Min(0f)] private float attackRecoverCrossfadeSeconds = 0.05f;
        [SerializeField, Min(0f)] private float recoverExitCrossfadeSeconds = 0.1f;
        [SerializeField, Min(0f)] private float chaseSearchCrossfadeSeconds = 0.15f;
        [SerializeField, Min(0f)] private float searchPatrolCrossfadeSeconds = 0.15f;
        [SerializeField, Min(0f)] private float searchChaseCrossfadeSeconds = 0.08f;
        [SerializeField, Min(0f)] private float runStopCrossfadeSeconds = 0.07f;
        [SerializeField, Min(0f)] private float turnCrossfadeSeconds = 0.1f;

        [Header("Progress Mapping")]
        [SerializeField, Range(0.05f, 0.95f)] private float attackHitPoseNormalizedTime = 0.45f;
        [SerializeField, Min(0.01f)] private float fallbackAttackWindupSeconds = 0.75f;
        [SerializeField, Min(0.01f)] private float fallbackRecoverSeconds = 1f;
        [SerializeField, Min(0f)] private float movingSpeedThreshold = 0.05f;
        [SerializeField, Min(0f)] private float moveSpeedDampSeconds = 0.08f;
        [SerializeField, Min(0f)] private float visualSpeedFloor = 0.03f;

        [Header("Presentation Transients")]
        [SerializeField, Min(0f)] private float runStopMinSpeed = 1.25f;
        [SerializeField, Min(0.01f)] private float runStopPresentationSeconds = 0.65f;
        [SerializeField, Min(0f)] private float turnMaxMoveSpeed = 0.1f;
        [SerializeField, Range(0f, 180f)] private float turnAngleThreshold = 35f;
        [SerializeField, Range(0f, 180f)] private float neutralTurnAngleThreshold = 165f;
        [SerializeField, Min(0.01f)] private float turnPresentationSeconds = 0.65f;
        [SerializeField, Min(0f)] private float turnCooldownSeconds = 0.35f;

        private bool _hasPresentedState;
        private StalkerState _presentedState;
        private int _presentedAnimatorStateHash;
        private StalkerAttackEpisodeId _presentedAttackEpisodeId;
        private bool _warnedMissingStateSource;
        private float _currentMoveSpeed;
        private float _previousMoveSpeed;
        private Vector3 _lastRootPosition;
        private bool _hasLastRootPosition;
        private StalkerState _previousSemanticState;
        private PresentationTransient _activeTransient;
        private float _transientEndsAt;
        private float _nextTurnAllowedAt;

        private void Awake()
        {
            ResolveDependencies();
            ConfigureAnimator();
        }

        private void OnEnable()
        {
            ResolveDependencies();
            ConfigureAnimator();
            ResetVisualMotionSampling();
            SynchronizeImmediate();
        }

        private void Start()
        {
            SynchronizeImmediate();
        }

        private void Update()
        {
            if (!ResolveDependencies() || !CanDriveAnimator())
            {
                return;
            }

            var presentation = ResolvePresentationState();
            ApplyParameters(presentation);
            ApplyState(presentation, immediate: false);
        }

        private bool ResolveDependencies()
        {
            if (fusionRuntime == null)
            {
                fusionRuntime = GetComponent<StalkerFusionRuntime>()
                    ?? GetComponentInParent<StalkerFusionRuntime>()
                    ?? GetComponentInChildren<StalkerFusionRuntime>(true);
            }

            if (controller == null)
            {
                controller = GetComponent<StalkerController>()
                    ?? GetComponentInParent<StalkerController>()
                    ?? GetComponentInChildren<StalkerController>(true);
            }

            if (navMeshAgent == null)
            {
                navMeshAgent = GetComponent<NavMeshAgent>()
                    ?? GetComponentInParent<NavMeshAgent>()
                    ?? GetComponentInChildren<NavMeshAgent>(true);
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
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

        private void ResetVisualMotionSampling()
        {
            _lastRootPosition = transform.position;
            _hasLastRootPosition = true;
            _currentMoveSpeed = 0f;
            _previousMoveSpeed = 0f;
        }

        private void SynchronizeImmediate()
        {
            if (!ResolveDependencies() || !CanDriveAnimator())
            {
                return;
            }

            var presentation = ResolvePresentationState();
            ApplyParameters(presentation);
            ApplyState(presentation, immediate: true);
            animator.Update(0f);
        }

        private StalkerNetworkPresentationState ResolvePresentationState()
        {
            if (fusionRuntime != null)
            {
                if (fusionRuntime.Object == null || !fusionRuntime.Object.IsValid)
                {
                    return BuildLocalPresentationState();
                }

                var presentation = fusionRuntime.GetReplicatedPresentationState();
                var semanticState = presentation.SemanticState;

                if (fusionRuntime.Object.HasStateAuthority && controller != null)
                {
                    semanticState = controller.CurrentState;
                }
                else if (fusionRuntime.ReplicatedState != semanticState
                    && !presentation.HasAttackEpisode)
                {
                    semanticState = fusionRuntime.ReplicatedState;
                }

                if (semanticState == presentation.SemanticState)
                {
                    return presentation;
                }

                return new StalkerNetworkPresentationState(
                    semanticState,
                    presentation.AttackEpisodeId,
                    presentation.AttackPhase,
                    presentation.AttackProgressSeconds,
                    presentation.AttackHitMomentResolved,
                    presentation.AttackOutcome,
                    presentation.AttackStartedTick,
                    presentation.AttackResolvedTick);
            }

            if (controller == null && !_warnedMissingStateSource)
            {
                _warnedMissingStateSource = true;
                UnityEngine.Debug.LogWarning(
                    "[StalkerAnimatorPresenter] No StalkerFusionRuntime or StalkerController found. " +
                    "Animator will stay in fallback Patrol presentation.",
                    this);
            }

            return new StalkerNetworkPresentationState(
                controller != null ? controller.CurrentState : StalkerState.PATROL,
                StalkerAttackEpisodeId.Invalid,
                StalkerNetworkAttackPhase.None,
                0f,
                false,
                StalkerAttackOutcome.None,
                -1L,
                -1L);
        }

        private StalkerNetworkPresentationState BuildLocalPresentationState()
        {
            if (controller != null
                && (controller.CurrentState == StalkerState.ATTACK || controller.CurrentState == StalkerState.RECOVER)
                && controller.ActiveAttackEpisode.EpisodeId.IsValid)
            {
                var activeEpisode = controller.ActiveAttackEpisode;
                var isRecovering = controller.CurrentState == StalkerState.RECOVER;
                return new StalkerNetworkPresentationState(
                    controller.CurrentState,
                    activeEpisode.EpisodeId,
                    isRecovering
                        ? StalkerNetworkAttackPhase.Recover
                        : activeEpisode.HitMomentResolved
                            ? StalkerNetworkAttackPhase.Resolved
                            : StalkerNetworkAttackPhase.Windup,
                    isRecovering ? controller.RecoverElapsedTime : activeEpisode.WindupElapsedSeconds,
                    activeEpisode.HitMomentResolved,
                    activeEpisode.Outcome,
                    activeEpisode.StartedTick,
                    activeEpisode.ResolutionTime.IsValid ? activeEpisode.ResolutionTime.Tick : -1L);
            }

            return new StalkerNetworkPresentationState(
                controller != null ? controller.CurrentState : StalkerState.PATROL,
                StalkerAttackEpisodeId.Invalid,
                StalkerNetworkAttackPhase.None,
                0f,
                false,
                StalkerAttackOutcome.None,
                -1L,
                -1L);
        }

        private void ApplyParameters(StalkerNetworkPresentationState presentation)
        {
            animator.SetInteger(StalkerStateHash, (int)presentation.SemanticState);

            var speed = ResolveMoveSpeed();
            _previousMoveSpeed = _currentMoveSpeed;
            _currentMoveSpeed = speed;
            animator.SetFloat(MoveSpeedHash, speed, moveSpeedDampSeconds, Time.deltaTime);
            animator.SetBool(IsMovingHash, speed > movingSpeedThreshold);

            var attackProgress = ResolveAttackNormalizedProgress(presentation);
            animator.SetFloat(AttackProgressHash, attackProgress);
            animator.SetFloat(StateProgressHash, ResolveStateNormalizedProgress(presentation, attackProgress));
        }

        private void ApplyState(StalkerNetworkPresentationState presentation, bool immediate)
        {
            bool stateChanged = !_hasPresentedState || _presentedState != presentation.SemanticState;
            bool newAttackEpisode = presentation.SemanticState == StalkerState.ATTACK
                && presentation.AttackEpisodeId.IsValid
                && _presentedAttackEpisodeId != presentation.AttackEpisodeId;
            var targetHash = ResolveAnimatorStateHash(presentation);
            bool animatorStateChanged = !_hasPresentedState || _presentedAnimatorStateHash != targetHash;

            if (!stateChanged && !newAttackEpisode && !animatorStateChanged)
            {
                return;
            }

            var normalizedTime = ResolveEntryNormalizedTime(presentation);
            if (immediate || !_hasPresentedState)
            {
                animator.Play(targetHash, 0, normalizedTime);
            }
            else
            {
                animator.CrossFadeInFixedTime(
                    targetHash,
                    ResolveCrossfadeDuration(_presentedState, presentation.SemanticState, targetHash),
                    0,
                    normalizedTime * ResolveClipDurationSeconds(targetHash, presentation.SemanticState));
            }

            _presentedState = presentation.SemanticState;
            _presentedAnimatorStateHash = targetHash;
            _presentedAttackEpisodeId = presentation.AttackEpisodeId;
            _previousSemanticState = presentation.SemanticState;
            _hasPresentedState = true;
        }

        private float ResolveMoveSpeed()
        {
            var speed = 0f;
            if (navMeshAgent != null && navMeshAgent.enabled)
            {
                speed = navMeshAgent.velocity.magnitude;
            }

            if (!_hasLastRootPosition)
            {
                ResetVisualMotionSampling();
                return speed;
            }

            var delta = transform.position - _lastRootPosition;
            delta.y = 0f;
            var visualSpeed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
            _lastRootPosition = transform.position;

            return Mathf.Max(speed, visualSpeed >= visualSpeedFloor ? visualSpeed : 0f);
        }

        private float ResolveStateNormalizedProgress(
            StalkerNetworkPresentationState presentation,
            float attackProgress)
        {
            if (presentation.SemanticState == StalkerState.ATTACK)
            {
                return attackProgress;
            }

            if (presentation.SemanticState == StalkerState.RECOVER)
            {
                return Mathf.Clamp01(presentation.AttackProgressSeconds / ResolveRecoverSeconds());
            }

            return 0f;
        }

        private float ResolveEntryNormalizedTime(StalkerNetworkPresentationState presentation)
        {
            return presentation.SemanticState switch
            {
                StalkerState.ATTACK => ResolveAttackNormalizedProgress(presentation),
                StalkerState.RECOVER => Mathf.Clamp01(presentation.AttackProgressSeconds / ResolveRecoverSeconds()),
                _ => 0f,
            };
        }

        private float ResolveAttackNormalizedProgress(StalkerNetworkPresentationState presentation)
        {
            if (presentation.SemanticState != StalkerState.ATTACK || !presentation.HasAttackEpisode)
            {
                return 0f;
            }

            var windup01 = Mathf.Clamp01(presentation.AttackProgressSeconds / ResolveAttackWindupSeconds());
            return Mathf.Clamp01(windup01 * attackHitPoseNormalizedTime);
        }

        private float ResolveAttackWindupSeconds()
        {
            return controller != null
                ? Mathf.Max(0.01f, controller.AttackWindupSeconds)
                : fallbackAttackWindupSeconds;
        }

        private float ResolveRecoverSeconds()
        {
            return controller != null
                ? Mathf.Max(0.01f, controller.AttackRecoverySeconds)
                : fallbackRecoverSeconds;
        }

        private int ResolveAnimatorStateHash(StalkerNetworkPresentationState presentation)
        {
            var state = presentation.SemanticState;
            if (_activeTransient != PresentationTransient.None)
            {
                if (CanContinueTransient(_activeTransient, state) && Time.time < _transientEndsAt)
                {
                    return ResolveTransientStateHash(_activeTransient);
                }

                _activeTransient = PresentationTransient.None;
            }

            if (ShouldPlayRunStop(state))
            {
                BeginTransient(PresentationTransient.RunStop, runStopPresentationSeconds);
                return RunStopStateHash;
            }

            if (TryBeginTurnTransient(state, out var turnHash))
            {
                return turnHash;
            }

            return ResolveBaseAnimatorStateHash(state);
        }

        private int ResolveBaseAnimatorStateHash(StalkerState state)
        {
            return state switch
            {
                StalkerState.PATROL => _currentMoveSpeed > movingSpeedThreshold ? WalkStateHash : IdleStateHash,
                StalkerState.DETECT => AlertStateHash,
                StalkerState.CHASE => RunStateHash,
                StalkerState.ATTACK => AttackStateHash,
                StalkerState.RECOVER => RecoverStateHash,
                StalkerState.SEARCH => SearchStateHash,
                _ => IdleStateHash,
            };
        }

        private bool ShouldPlayRunStop(StalkerState state)
        {
            return _hasPresentedState
                && _presentedAnimatorStateHash == RunStateHash
                && (_previousSemanticState == StalkerState.CHASE || _presentedState == StalkerState.CHASE)
                && (state == StalkerState.SEARCH || state == StalkerState.PATROL)
                && Mathf.Max(_currentMoveSpeed, _previousMoveSpeed) >= runStopMinSpeed;
        }

        private bool TryBeginTurnTransient(StalkerState state, out int stateHash)
        {
            stateHash = 0;
            if ((state != StalkerState.PATROL && state != StalkerState.SEARCH)
                || _currentMoveSpeed > turnMaxMoveSpeed
                || Time.time < _nextTurnAllowedAt
                || !TryResolveDesiredTurnAngle(out var signedAngle))
            {
                return false;
            }

            var absoluteAngle = Mathf.Abs(signedAngle);
            if (absoluteAngle < turnAngleThreshold)
            {
                return false;
            }

            var transient = absoluteAngle >= neutralTurnAngleThreshold
                ? PresentationTransient.Turn
                : signedAngle > 0f
                    ? PresentationTransient.TurnRight
                    : PresentationTransient.TurnLeft;

            BeginTransient(transient, turnPresentationSeconds);
            _nextTurnAllowedAt = Time.time + turnPresentationSeconds + turnCooldownSeconds;
            stateHash = ResolveTransientStateHash(transient);
            return true;
        }

        private bool TryResolveDesiredTurnAngle(out float signedAngle)
        {
            signedAngle = 0f;
            if (navMeshAgent == null || !navMeshAgent.enabled)
            {
                return false;
            }

            var desiredDirection = navMeshAgent.desiredVelocity;
            desiredDirection.y = 0f;
            if (desiredDirection.sqrMagnitude < 0.01f && navMeshAgent.hasPath)
            {
                desiredDirection = navMeshAgent.steeringTarget - transform.position;
                desiredDirection.y = 0f;
            }

            var forward = transform.forward;
            forward.y = 0f;
            if (desiredDirection.sqrMagnitude < 0.01f || forward.sqrMagnitude < 0.01f)
            {
                return false;
            }

            signedAngle = Vector3.SignedAngle(forward.normalized, desiredDirection.normalized, Vector3.up);
            return true;
        }

        private void BeginTransient(PresentationTransient transient, float durationSeconds)
        {
            _activeTransient = transient;
            _transientEndsAt = Time.time + Mathf.Max(0.01f, durationSeconds);
        }

        private static bool CanContinueTransient(PresentationTransient transient, StalkerState state)
        {
            return transient switch
            {
                PresentationTransient.RunStop => state == StalkerState.SEARCH || state == StalkerState.PATROL,
                PresentationTransient.Turn or PresentationTransient.TurnLeft or PresentationTransient.TurnRight =>
                    state == StalkerState.PATROL || state == StalkerState.SEARCH,
                _ => false,
            };
        }

        private static int ResolveTransientStateHash(PresentationTransient transient)
        {
            return transient switch
            {
                PresentationTransient.RunStop => RunStopStateHash,
                PresentationTransient.TurnLeft => TurnLeftStateHash,
                PresentationTransient.TurnRight => TurnRightStateHash,
                PresentationTransient.Turn => TurnStateHash,
                _ => IdleStateHash,
            };
        }

        private float ResolveCrossfadeDuration(StalkerState from, StalkerState to, int targetHash)
        {
            if (targetHash == RunStopStateHash)
                return runStopCrossfadeSeconds;
            if (targetHash == TurnStateHash || targetHash == TurnLeftStateHash || targetHash == TurnRightStateHash)
                return turnCrossfadeSeconds;
            if ((targetHash == IdleStateHash || targetHash == WalkStateHash)
                && from == StalkerState.PATROL && to == StalkerState.PATROL)
                return idlePatrolCrossfadeSeconds;

            if ((from == StalkerState.PATROL && to == StalkerState.DETECT)
                || (from == StalkerState.DETECT && to == StalkerState.PATROL))
                return patrolDetectCrossfadeSeconds;
            if (from == StalkerState.DETECT && to == StalkerState.CHASE)
                return detectChaseCrossfadeSeconds;
            if (from == StalkerState.CHASE && to == StalkerState.ATTACK)
                return chaseAttackCrossfadeSeconds;
            if (from == StalkerState.ATTACK && to == StalkerState.RECOVER)
                return attackRecoverCrossfadeSeconds;
            if (from == StalkerState.RECOVER)
                return recoverExitCrossfadeSeconds;
            if (from == StalkerState.CHASE && to == StalkerState.SEARCH)
                return chaseSearchCrossfadeSeconds;
            if (from == StalkerState.SEARCH && to == StalkerState.PATROL)
                return searchPatrolCrossfadeSeconds;
            if (from == StalkerState.SEARCH && to == StalkerState.CHASE)
                return searchChaseCrossfadeSeconds;
            if (IsIdlePatrolPair(from, to))
                return idlePatrolCrossfadeSeconds;

            return defaultCrossfadeSeconds;
        }

        private static float ResolveClipDurationSeconds(int targetHash, StalkerState state)
        {
            if (targetHash == RunStopStateHash)
                return 0.65f;
            if (targetHash == TurnStateHash || targetHash == TurnLeftStateHash || targetHash == TurnRightStateHash)
                return 0.65f;
            if (targetHash == IdleStateHash)
                return 2.8f;
            if (targetHash == WalkStateHash)
                return 1.3f;
            if (targetHash == RunStateHash)
                return 0.75f;
            if (targetHash == AlertStateHash)
                return 0.75f;
            if (targetHash == SearchStateHash)
                return 2.4f;
            if (targetHash == AttackStateHash)
                return 1.15f;
            if (targetHash == RecoverStateHash)
                return 0.75f;

            return state switch
            {
                StalkerState.DETECT => 0.75f,
                StalkerState.ATTACK => 1.15f,
                StalkerState.RECOVER => 0.75f,
                StalkerState.SEARCH => 2.4f,
                StalkerState.CHASE => 0.75f,
                StalkerState.PATROL => 1.3f,
                _ => 2.8f,
            };
        }

        private static bool IsIdlePatrolPair(StalkerState from, StalkerState to)
        {
            return (from == StalkerState.PATROL && to == default)
                || (from == default && to == StalkerState.PATROL);
        }

        private bool CanDriveAnimator()
        {
            return animator != null
                && animator.isInitialized
                && animator.runtimeAnimatorController != null;
        }
    }
}
