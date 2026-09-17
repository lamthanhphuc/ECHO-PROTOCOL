using System;
using EchoProtocol.Networking.Authority;
using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking
{
    public enum NetworkPlayerLifeStatus
    {
        Alive = 0,
        Downed = 1,
        Eliminated = 2,
        Escaped = 3,
        Caught = 4,
    }

    public enum NetworkPlayerLifeTransitionCause
    {
        None = 0,
        Damage = 1,
        ReviveStarted = 2,
        ReviveCancelled = 3,
        ReviveCompleted = 4,
        Bleedout = 5,
        ReviveLimit = 6,
        Escaped = 7,
        ProtectionExpired = 8,
        GhostCatch = 9,
    }

    public static class NetworkPlayerLifeStateRules
    {
        public static bool CanReceiveDamage(NetworkPlayerLifeStatus status, bool hasReviveProtection) =>
            status == NetworkPlayerLifeStatus.Alive && !hasReviveProtection;

        public static bool CanDown(NetworkPlayerLifeStatus status) =>
            status == NetworkPlayerLifeStatus.Alive;

        public static bool CanStartRevive(
            NetworkPlayerLifeStatus targetStatus,
            NetworkPlayerLifeStatus reviverStatus,
            bool samePlayer,
            bool reviveInProgress,
            int reviveCount,
            int maximumRevives) =>
            targetStatus == NetworkPlayerLifeStatus.Downed
            && reviverStatus == NetworkPlayerLifeStatus.Alive
            && !samePlayer
            && !reviveInProgress
            && reviveCount >= 0
            && maximumRevives >= 0
            && reviveCount < maximumRevives;

        public static bool CanRevive(NetworkPlayerLifeStatus status, int reviveCount, int maximumRevives) =>
            status == NetworkPlayerLifeStatus.Downed
            && reviveCount >= 0
            && maximumRevives >= 0
            && reviveCount < maximumRevives;

        public static bool CanEliminate(NetworkPlayerLifeStatus status, int reviveCount, int maximumRevives) =>
            status == NetworkPlayerLifeStatus.Downed
            && maximumRevives >= 0
            && reviveCount >= maximumRevives;

        public static bool CanBleedOut(NetworkPlayerLifeStatus status) =>
            status == NetworkPlayerLifeStatus.Downed;

        public static bool CanEscape(NetworkPlayerLifeStatus status) =>
            status == NetworkPlayerLifeStatus.Alive;

        public static bool CanMove(NetworkPlayerLifeStatus status) =>
            status == NetworkPlayerLifeStatus.Alive
            || status == NetworkPlayerLifeStatus.Downed;

        public static bool CanInitiateAction(NetworkPlayerLifeStatus status) =>
            status == NetworkPlayerLifeStatus.Alive;
    }

    /// <summary>Single State-Authority owner for health, down, revive, protection and elimination.</summary>
    [DisallowMultipleComponent]
    public sealed class NetworkPlayerLifeState : NetworkBehaviour
    {
        public static event Action<NetworkPlayerLifeState> StateChanged;

        [Header("Health / Down")]
        [SerializeField, Min(1f)] private float _maximumHealth = 100f;
        [SerializeField, Min(0.1f)] private float _bleedoutSeconds = 45f;
        [SerializeField, Range(0.05f, 1f)] private float _crawlSpeedMultiplier = 0.32f;

        [Header("Revive")]
        [SerializeField, Min(0)] private int _maximumRevives = 2;
        [SerializeField, Min(0.1f)] private float _reviveDurationSeconds = 3f;
        [SerializeField, Min(0.1f)] private float _reviveDistance = 3f;
        [SerializeField, Min(1f)] private float _revivedHealth = 35f;
        [SerializeField, Min(0f)] private float _reviveProtectionSeconds = 3f;

        [Networked, OnChangedRender(nameof(HandleReplicatedStateChanged))]
        public NetworkPlayerLifeStatus Status { get; private set; }

        [Networked, OnChangedRender(nameof(HandleReplicatedStateChanged))]
        public float Health { get; private set; }

        [Networked, OnChangedRender(nameof(HandleReplicatedStateChanged))]
        public PlayerRef Reviver { get; private set; }

        [Networked, OnChangedRender(nameof(HandleReplicatedStateChanged))]
        public NetworkBool IsCrawling { get; private set; }

        [Networked] public int DownCount { get; private set; }
        [Networked] public int ReviveCount { get; private set; }
        [Networked] public uint TransitionOrdinal { get; private set; }
        [Networked] public NetworkPlayerLifeTransitionCause LastTransitionCause { get; private set; }
        [Networked] private TickTimer BleedoutTimer { get; set; }
        [Networked] private TickTimer ReviveTimer { get; set; }
        [Networked] private TickTimer ProtectionTimer { get; set; }
        [Networked] private float ActiveReviveDurationSeconds { get; set; }
        [Networked] private NetworkBool ActiveReviveUsedFirstAidKit { get; set; }
        [Networked] private float PausedBleedoutRemainingSeconds { get; set; }

        private PlayerDownState _legacyDownState;
        [Networked] public NetworkId CaughtByGhostId { get; private set; }
        [Networked] public float CatchDuration { get; private set; }
        [Networked] private TickTimer CatchTimer { get; set; }
        [Networked] private NetworkBool CatchEndsInDeath { get; set; }

        public bool IsCaught => Object != null && Object.IsValid && Status == NetworkPlayerLifeStatus.Caught;
        public bool IsEliminated => Object != null && Object.IsValid &&
            (Status == NetworkPlayerLifeStatus.Eliminated || Status == NetworkPlayerLifeStatus.Escaped);
        public float CatchRemaining => Remaining(CatchTimer);
        private PlayerReviveInteractable _legacyReviveInteractable;
        private Renderer[] _presentationRenderers;
        private Collider[] _presentationColliders;
        private Light[] _presentationLights;
        private bool[] _presentationRendererDefaults;
        private bool[] _presentationColliderDefaults;
        private bool[] _presentationLightDefaults;
        private int _presentationRendererCount;
        private int _presentationColliderCount;
        private int _presentationLightCount;
        private bool _presentationHidden;
        private Transform _characterVisualRoot;
        private bool _characterVisualRootDefaultActive;
        private bool _hasCharacterVisualRootDefault;

        public bool CanBeRevived => (Object != null && Object.IsValid) && NetworkPlayerLifeStateRules.CanRevive(
            Status,
            ReviveCount,
            _maximumRevives);

        public bool CanMove => (Object == null || !Object.IsValid) || NetworkPlayerLifeStateRules.CanMove(Status);
        public bool CanInitiateAction => (Object == null || !Object.IsValid) || NetworkPlayerLifeStateRules.CanInitiateAction(Status);
        public bool IsDowned => (Object != null && Object.IsValid) && Status == NetworkPlayerLifeStatus.Downed;
        public bool IsReviveInProgress => IsDowned && Reviver.IsValid && ReviveTimer.IsRunning;
        public bool HasReviveProtection => (Object != null && Object.IsValid)
                                           && Status == NetworkPlayerLifeStatus.Alive
                                           && ReviveProtectionRemaining > 0f;
        public bool IsMatchActive => (Object == null || !Object.IsValid)
                                     || Status == NetworkPlayerLifeStatus.Alive
                                     || IsCaught
                                     || IsDowned;
        public float MovementSpeedMultiplier => IsDowned ? _crawlSpeedMultiplier : 1f;
        public float BleedoutRemaining => IsReviveInProgress
            ? Mathf.Max(0f, PausedBleedoutRemainingSeconds)
            : Remaining(BleedoutTimer);
        public float ReviveProtectionRemaining => Remaining(ProtectionTimer);
        public float ReviveProgress01 => !IsReviveInProgress
            ? 0f
            : 1f - Mathf.Clamp01(Remaining(ReviveTimer) /
                Mathf.Max(0.1f, ActiveReviveDurationSeconds));

        public override void Spawned()
        {
            EnsureLegacyPresentationComponents();
            if (Object.HasStateAuthority)
            {
                Status = NetworkPlayerLifeStatus.Alive;
                Health = _maximumHealth;
                Reviver = PlayerRef.None;
                IsCrawling = false;
                DownCount = 0;
                ReviveCount = 0;
                TransitionOrdinal = 0;
                LastTransitionCause = NetworkPlayerLifeTransitionCause.None;
                BleedoutTimer = TickTimer.None;
                ReviveTimer = TickTimer.None;
                ProtectionTimer = TickTimer.None;
                PausedBleedoutRemainingSeconds = 0f;
                CatchTimer = TickTimer.None;
                CaughtByGhostId = default;
                CatchDuration = 0f;
                CatchEndsInDeath = false;
                ClearReviveSnapshot();
            }

            ApplyPresentation();
            StateChanged?.Invoke(this);
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;

            if (IsCaught)
            {
                if (CatchTimer.Expired(Runner))
                {
                    CatchTimer = TickTimer.None;
                    if (CatchEndsInDeath)
                        CommitEliminated(NetworkPlayerLifeTransitionCause.GhostCatch, "GHOST_CATCH");
                    else
                        CommitDown("STALKER", transform.position);
                }
                return;
            }

            if (IsReviveInProgress)
            {
                if (!CanContinueRevive())
                {
                    CancelReviveAuthoritative("reviver/target validation failed");
                    return;
                }

                if (ReviveTimer.Expired(Runner))
                {
                    CompleteReviveAuthoritative();
                }
                return;
            }

            if (NetworkPlayerLifeStateRules.CanBleedOut(Status) && BleedoutTimer.Expired(Runner))
            {
                CommitEliminated(NetworkPlayerLifeTransitionCause.Bleedout, "BLEEDOUT");
                return;
            }

            if (Status == NetworkPlayerLifeStatus.Alive && ProtectionTimer.Expired(Runner))
            {
                ProtectionTimer = TickTimer.None;
                CommitStatus(NetworkPlayerLifeStatus.Alive, NetworkPlayerLifeTransitionCause.ProtectionExpired);
            }
        }

        public override void Render()
        {
            ApplyPresentation();
        }

        public void ResetForMatchAuthoritative()
        {
            if (Object == null || !Object.IsValid || !Object.HasStateAuthority) return;
            Status = NetworkPlayerLifeStatus.Alive;
            Health = _maximumHealth;
            Reviver = PlayerRef.None;
            IsCrawling = false;
            DownCount = 0;
            ReviveCount = 0;
            TransitionOrdinal = 0;
            LastTransitionCause = NetworkPlayerLifeTransitionCause.None;
            BleedoutTimer = TickTimer.None;
            ReviveTimer = TickTimer.None;
            ProtectionTimer = TickTimer.None;
            PausedBleedoutRemainingSeconds = 0f;
            CatchTimer = TickTimer.None;
            CaughtByGhostId = default;
            CatchDuration = 0f;
            CatchEndsInDeath = false;
            ClearReviveSnapshot();
            ApplyPresentation();
            StateChanged?.Invoke(this);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            GetComponent<PlayerJumpscareController>()?.StopJumpscare();
        }

        public bool TryApplyAuthoritativeDamage(float damage, string sourceType, Vector3 hitPosition)
        {
            if (!Object.HasStateAuthority
                || damage <= 0f
                || !NetworkPlayerLifeStateRules.CanReceiveDamage(Status, HasReviveProtection))
            {
                return false;
            }

            Health = Mathf.Max(0f, Health - damage);
            if (Health <= 0f)
            {
                CommitDown(sourceType, hitPosition);
            }
            else
            {
                HandleReplicatedStateChanged();
            }

            return true;
        }

        public bool TryHeal(float amount)
        {
            if (!Object.HasStateAuthority || Status != NetworkPlayerLifeStatus.Alive || Health >= _maximumHealth)
            {
                return false;
            }

            Health = Mathf.Min(Health + amount, _maximumHealth);
            HandleReplicatedStateChanged();
            return true;
        }

        public bool TryApplyMonsterDown(string monsterType, Vector3 hitPosition)
        {
            if (!Object.HasStateAuthority
                || HasReviveProtection
                || !NetworkPlayerLifeStateRules.CanDown(Status))
            {
                return false;
            }

            Health = 0f;
            CommitDown(monsterType, hitPosition);
            return true;
        }

        // Called only by an authoritative ghost's validated hit; no client RPC.
        public bool TryCatchAuthoritative(NetworkObject ghost, float range, float duration, bool endsInDeath)
        {
            if (Object == null || !Object.IsValid || !Object.HasStateAuthority
                || ghost == null || !ghost.IsValid || !ghost.HasStateAuthority || ghost.Runner != Runner
                || !NetworkPlayerLifeStateRules.CanReceiveDamage(Status, HasReviveProtection)
                || !float.IsFinite(range) || range <= 0f || !float.IsFinite(duration) || duration < 0.2f
                || (ghost.transform.position - transform.position).sqrMagnitude > range * range)
                return false;

            ClearSurvivalTimers();
            CaughtByGhostId = ghost.Id;
            CatchDuration = duration;
            CatchEndsInDeath = endsInDeath;
            CatchTimer = TickTimer.CreateFromSeconds(Runner, duration);
            IsCrawling = false;
            CommitStatus(NetworkPlayerLifeStatus.Caught, NetworkPlayerLifeTransitionCause.GhostCatch);
            return true;
        }

        public bool TryStartRevive(PlayerRef reviver)
        {
            if (!Object.HasStateAuthority
                || !TryResolvePlayerLifeState(reviver, out var reviverObject, out var reviverLifeState)
                || !NetworkPlayerLifeStateRules.CanStartRevive(
                    Status,
                    reviverLifeState.Status,
                    reviver == Object.InputAuthority,
                    IsReviveInProgress,
                    ReviveCount,
                    _maximumRevives)
                || Vector3.SqrMagnitude(reviverObject.transform.position - transform.position)
                    > _reviveDistance * _reviveDistance)
            {
                return false;
            }

            Reviver = reviver;
            var reviverState = reviverObject.GetComponent<LobbyPlayerState>();
            var reviverInteractor = reviverObject.GetComponent<NetworkPlayerInteractor>();
            if (reviverInteractor == null || !reviverInteractor.CanStartFirstAidReviveAuthoritative(reviverState))
            {
                return false;
            }

            ActiveReviveUsedFirstAidKit = true;
            ActiveReviveDurationSeconds = _reviveDurationSeconds;
            PausedBleedoutRemainingSeconds = BleedoutRemaining;
            BleedoutTimer = TickTimer.None;
            ReviveTimer = TickTimer.CreateFromSeconds(Runner, ActiveReviveDurationSeconds);
            CommitStatus(NetworkPlayerLifeStatus.Downed, NetworkPlayerLifeTransitionCause.ReviveStarted);
            Debug.Log($"[LifeState] {reviver} started reviving {Object.InputAuthority}.");
            return true;
        }

        public bool TryCancelRevive(PlayerRef reviver)
        {
            if (!Object.HasStateAuthority
                || !IsReviveInProgress
                || Reviver != reviver)
            {
                return false;
            }

            CancelReviveAuthoritative("explicit gameplay interruption");
            return true;
        }

        public bool TryRevive(PlayerRef reviver, bool usedFirstAidKit = false)
        {
            // Compatibility entry point: requesting a revive starts authoritative progress.
            return TryStartRevive(reviver);
        }

        public bool TryEliminateForReviveLimit()
        {
            if (!Object.HasStateAuthority
                || !NetworkPlayerLifeStateRules.CanEliminate(Status, ReviveCount, _maximumRevives))
            {
                return false;
            }

            return CommitEliminated(NetworkPlayerLifeTransitionCause.ReviveLimit, "REVIVE_LIMIT_REACHED");
        }

        public bool ForceEliminateAuthoritative(NetworkPlayerLifeTransitionCause cause, string reason)
        {
            if (Object == null || !Object.IsValid || !Object.HasStateAuthority)
            {
                return false;
            }

            return CommitEliminated(cause, reason);
        }

        public bool TryEscape(bool rescuedTeammate = false)
        {
            if (!Object.HasStateAuthority || !NetworkPlayerLifeStateRules.CanEscape(Status))
            {
                return false;
            }

            ClearSurvivalTimers();
            Health = Mathf.Max(Health, 1f);
            CommitStatus(NetworkPlayerLifeStatus.Escaped, NetworkPlayerLifeTransitionCause.Escaped);
            MatchAuthorityRuntime.Instance?.RecordPlayerEscaped(
                Object.InputAuthority,
                BuildOccurrenceKey("escape"),
                rescuedTeammate);
            Debug.Log($"[LifeState] {Object.InputAuthority} -> Escaped.");
            return true;
        }

        private void CommitDown(string sourceType, Vector3 hitPosition)
        {
            var nextDownCount = DownCount + 1;
            if (nextDownCount >= 3)
            {
                Reviver = PlayerRef.None;
                ReviveTimer = TickTimer.None;
                ClearReviveSnapshot();
                ProtectionTimer = TickTimer.None;
                PausedBleedoutRemainingSeconds = 0f;
                BleedoutTimer = TickTimer.None;
                IsCrawling = false;
                DownCount = nextDownCount;
                GetComponent<NetworkPlayerInteractor>()?.DropHeldItemsAuthoritative(Object.InputAuthority);
                CommitEliminated(NetworkPlayerLifeTransitionCause.ReviveLimit, "THIRD_DOWN");
                return;
            }

            Reviver = PlayerRef.None;
            ReviveTimer = TickTimer.None;
            ClearReviveSnapshot();
            ProtectionTimer = TickTimer.None;
            PausedBleedoutRemainingSeconds = 0f;
            BleedoutTimer = TickTimer.CreateFromSeconds(Runner, _bleedoutSeconds);
            IsCrawling = true;
            DownCount = nextDownCount;
            GetComponent<NetworkPlayerInteractor>()?.DropHeldItemsAuthoritative(Object.InputAuthority);
            CommitStatus(NetworkPlayerLifeStatus.Downed, NetworkPlayerLifeTransitionCause.Damage);
            MatchAuthorityRuntime.Instance?.RecordPlayerDowned(
                Object.InputAuthority,
                BuildOccurrenceKey("down"),
                sourceType,
                DownCount,
                hitPosition);
            Debug.Log($"[LifeState] {Object.InputAuthority} Alive -> Downed by {sourceType}.");
        }

        private bool CanContinueRevive()
        {
            return IsReviveInProgress
                && Reviver.IsValid
                && TryResolvePlayerLifeState(Reviver, out var reviverObject, out var reviverLifeState)
                && NetworkPlayerLifeStateRules.CanInitiateAction(reviverLifeState.Status)
                && ReviverStillHasUsableFirstAidKit(reviverObject)
                && Vector3.SqrMagnitude(reviverObject.transform.position - transform.position)
                   <= _reviveDistance * _reviveDistance;
        }

        private static bool ReviverStillHasUsableFirstAidKit(NetworkObject reviverObject)
        {
            if (reviverObject == null
                || !reviverObject.TryGetComponent<LobbyPlayerState>(out var reviverState)
                || !reviverObject.TryGetComponent<NetworkPlayerInteractor>(out var reviverInteractor))
            {
                return false;
            }

            return reviverInteractor.CanStartFirstAidReviveAuthoritative(reviverState);
        }

        private void CancelReviveAuthoritative(string reason)
        {
            var previousReviver = Reviver;
            Reviver = PlayerRef.None;
            ReviveTimer = TickTimer.None;
            ClearReviveSnapshot();
            IsCrawling = true;
            if (PausedBleedoutRemainingSeconds > 0f)
            {
                BleedoutTimer = TickTimer.CreateFromSeconds(Runner, PausedBleedoutRemainingSeconds);
            }
            else
            {
                BleedoutTimer = TickTimer.None;
            }
            PausedBleedoutRemainingSeconds = 0f;
            CommitStatus(NetworkPlayerLifeStatus.Downed, NetworkPlayerLifeTransitionCause.ReviveCancelled);
            if (!BleedoutTimer.IsRunning)
            {
                CommitEliminated(NetworkPlayerLifeTransitionCause.Bleedout, "BLEEDOUT_AFTER_REVIVE_CANCEL");
            }
            Debug.Log($"[LifeState] Revive cancelled target={Object.InputAuthority}, reviver={previousReviver}, reason={reason}.");
        }

        private void CompleteReviveAuthoritative()
        {
            if (!IsReviveInProgress || !CanContinueRevive()) return;

            var completedReviver = Reviver;
            var usedFirstAidKit = ActiveReviveUsedFirstAidKit;
            if (!TryResolvePlayerLifeState(completedReviver, out var reviverObject, out _)
                || !reviverObject.TryGetComponent<LobbyPlayerState>(out var reviverState)
                || !reviverObject.TryGetComponent<NetworkPlayerInteractor>(out var reviverInteractor)
                || !reviverInteractor.ConsumeFirstAidReviveAuthoritative(reviverState))
            {
                CancelReviveAuthoritative("first aid kit missing before completion");
                return;
            }

            Reviver = PlayerRef.None;
            ReviveTimer = TickTimer.None;
            ClearReviveSnapshot();
            BleedoutTimer = TickTimer.None;
            PausedBleedoutRemainingSeconds = 0f;
            IsCrawling = false;
            Health = Mathf.Clamp(_revivedHealth, 1f, _maximumHealth);
            ReviveCount++;
            ProtectionTimer = _reviveProtectionSeconds > 0f
                ? TickTimer.CreateFromSeconds(Runner, _reviveProtectionSeconds)
                : TickTimer.None;
            CommitStatus(NetworkPlayerLifeStatus.Alive, NetworkPlayerLifeTransitionCause.ReviveCompleted);
            MatchAuthorityRuntime.Instance?.RecordPlayerRevived(
                Object.InputAuthority,
                completedReviver,
                BuildOccurrenceKey("revive"),
                ReviveCount,
                usedFirstAidKit);
            Debug.Log($"[LifeState] {Object.InputAuthority} revived by {completedReviver}; protection={_reviveProtectionSeconds:0.##}s.");
        }

        private bool CommitEliminated(NetworkPlayerLifeTransitionCause cause, string reason)
        {
            if (Status == NetworkPlayerLifeStatus.Eliminated
                || Status == NetworkPlayerLifeStatus.Escaped)
            {
                return false;
            }

            Health = 0f;
            IsCrawling = false;
            ClearSurvivalTimers();
            CommitStatus(NetworkPlayerLifeStatus.Eliminated, cause);
            MatchAuthorityRuntime.Instance?.RecordPlayerEliminated(
                Object.InputAuthority,
                BuildOccurrenceKey("eliminate"),
                ReviveCount);
            Debug.Log($"[LifeState] {Object.InputAuthority} -> Eliminated reason={reason}.");
            return true;
        }

        private void ClearSurvivalTimers()
        {
            Reviver = PlayerRef.None;
            BleedoutTimer = TickTimer.None;
            ReviveTimer = TickTimer.None;
            ProtectionTimer = TickTimer.None;
            PausedBleedoutRemainingSeconds = 0f;
            ClearReviveSnapshot();
        }

        private void ClearReviveSnapshot()
        {
            ActiveReviveDurationSeconds = 0f;
            ActiveReviveUsedFirstAidKit = false;
        }

        private bool TryResolvePlayerLifeState(
            PlayerRef player,
            out NetworkObject playerObject,
            out NetworkPlayerLifeState lifeState)
        {
            playerObject = null;
            lifeState = null;
            return player.IsValid
                && Runner.TryGetPlayerObject(player, out playerObject)
                && playerObject != null
                && playerObject.InputAuthority == player
                && playerObject.TryGetComponent(out lifeState);
        }

        private float Remaining(TickTimer timer)
        {
            return Runner == null ? 0f : Mathf.Max(0f, timer.RemainingTime(Runner) ?? 0f);
        }

        private void CommitStatus(NetworkPlayerLifeStatus status, NetworkPlayerLifeTransitionCause cause)
        {
            Status = status;
            LastTransitionCause = cause;
            AdvanceTransition();
            HandleReplicatedStateChanged();
        }

        private void AdvanceTransition()
        {
            TransitionOrdinal++;
            if (TransitionOrdinal == 0) TransitionOrdinal = 1;
        }

        private string BuildOccurrenceKey(string transition)
        {
            return $"player:{Object.Id}:{transition}:{TransitionOrdinal}";
        }

        private void EnsureLegacyPresentationComponents()
        {
            if (GetComponent<PlayerJumpscareController>() == null)
                gameObject.AddComponent<PlayerJumpscareController>();
            if (GetComponent<PlayerSpectateController>() == null)
                gameObject.AddComponent<PlayerSpectateController>();
            _legacyDownState = GetComponent<PlayerDownState>();
            if (_legacyDownState == null)
            {
                _legacyDownState = gameObject.AddComponent<PlayerDownState>();
            }
            _legacyDownState.SetNetworkAuthorityPresentationOnly(true);

            _legacyReviveInteractable = GetComponent<PlayerReviveInteractable>();
            if (_legacyReviveInteractable == null)
            {
                _legacyReviveInteractable = gameObject.AddComponent<PlayerReviveInteractable>();
            }
            _legacyReviveInteractable.SetNetworkAuthorityPresentationOnly(true);
        }

        private void ApplyPresentation()
        {
            if (_legacyDownState == null || _legacyReviveInteractable == null)
            {
                EnsureLegacyPresentationComponents();
            }

            var presentationState = Status switch
            {
                NetworkPlayerLifeStatus.Caught => PlayerLifeState.Caught,
                NetworkPlayerLifeStatus.Downed => PlayerLifeState.Downed,
                NetworkPlayerLifeStatus.Eliminated when Object.HasInputAuthority => PlayerLifeState.Spectating,
                NetworkPlayerLifeStatus.Eliminated => PlayerLifeState.Eliminated,
                NetworkPlayerLifeStatus.Escaped => PlayerLifeState.Spectating,
                _ => PlayerLifeState.Active,
            };

            _legacyDownState.ApplyAuthoritativeSnapshot(
                presentationState,
                Health,
                BleedoutRemaining,
                ReviveProtectionRemaining,
                Object.HasInputAuthority);

            GameObject reviverObject = null;
            if (Reviver.IsValid && Runner.TryGetPlayerObject(Reviver, out var resolvedReviver))
            {
                reviverObject = resolvedReviver.gameObject;
            }
            _legacyReviveInteractable.ApplyAuthoritativeSnapshot(
                IsReviveInProgress,
                reviverObject,
                ReviveProgress01,
                LastTransitionCause == NetworkPlayerLifeTransitionCause.ReviveCompleted);

            ApplyEliminatedPresentation(Status == NetworkPlayerLifeStatus.Eliminated
                                        || Status == NetworkPlayerLifeStatus.Escaped);
        }

        private void ApplyEliminatedPresentation(bool hidden)
        {
            if (hidden)
            {
                HideCurrentPresentationComponents();
                _presentationHidden = true;
                return;
            }

            if (!_presentationHidden
                && _presentationRenderers != null
                && _presentationColliders != null)
            {
                return;
            }

            _presentationHidden = hidden;
            EnsurePresentationVisibilityCache();

            for (int i = 0; i < _presentationRenderers.Length; i++)
            {
                if (_presentationRenderers[i] != null)
                {
                    _presentationRenderers[i].enabled = hidden ? false : _presentationRendererDefaults[i];
                }
            }

            for (int i = 0; i < _presentationColliders.Length; i++)
            {
                if (_presentationColliders[i] != null)
                {
                    _presentationColliders[i].enabled = hidden ? false : _presentationColliderDefaults[i];
                }
            }

            for (int i = 0; i < _presentationLights.Length; i++)
            {
                if (_presentationLights[i] != null)
                {
                    _presentationLights[i].enabled = hidden ? false : _presentationLightDefaults[i];
                }
            }

            ApplyCharacterVisualRootVisibility(!hidden);
        }

        private void HideCurrentPresentationComponents()
        {
            EnsurePresentationVisibilityCache();
            ApplyCharacterVisualRootVisibility(false);

            var renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length != _presentationRendererCount)
            {
                _presentationRenderers = null;
                EnsurePresentationVisibilityCache();
                renderers = _presentationRenderers;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].enabled)
                {
                    renderers[i].enabled = false;
                }
            }

            var colliders = GetComponentsInChildren<Collider>(true);
            if (colliders.Length != _presentationColliderCount)
            {
                _presentationColliders = null;
                EnsurePresentationVisibilityCache();
                colliders = _presentationColliders;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].enabled)
                {
                    colliders[i].enabled = false;
                }
            }

            var lights = GetComponentsInChildren<Light>(true);
            if (lights.Length != _presentationLightCount)
            {
                _presentationLights = null;
                EnsurePresentationVisibilityCache();
                lights = _presentationLights;
            }

            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].enabled)
                {
                    lights[i].enabled = false;
                }
            }
        }

        private void EnsurePresentationVisibilityCache()
        {
            if (_presentationRenderers == null)
            {
                _presentationRenderers = GetComponentsInChildren<Renderer>(true);
                _presentationRendererCount = _presentationRenderers.Length;
                _presentationRendererDefaults = new bool[_presentationRenderers.Length];
                for (int i = 0; i < _presentationRenderers.Length; i++)
                {
                    _presentationRendererDefaults[i] = _presentationRenderers[i] != null
                        && _presentationRenderers[i].enabled;
                }
            }

            if (_presentationColliders == null)
            {
                _presentationColliders = GetComponentsInChildren<Collider>(true);
                _presentationColliderCount = _presentationColliders.Length;
                _presentationColliderDefaults = new bool[_presentationColliders.Length];
                for (int i = 0; i < _presentationColliders.Length; i++)
                {
                    _presentationColliderDefaults[i] = _presentationColliders[i] != null
                        && _presentationColliders[i].enabled;
                }
            }

            if (_presentationLights == null)
            {
                _presentationLights = GetComponentsInChildren<Light>(true);
                _presentationLightCount = _presentationLights.Length;
                _presentationLightDefaults = new bool[_presentationLights.Length];
                for (int i = 0; i < _presentationLights.Length; i++)
                {
                    _presentationLightDefaults[i] = _presentationLights[i] != null
                        && _presentationLights[i].enabled;
                }
            }

            if (!_hasCharacterVisualRootDefault)
            {
                _characterVisualRoot = transform.Find("CharacterVisual");
                if (_characterVisualRoot != null)
                {
                    _characterVisualRootDefaultActive = _characterVisualRoot.gameObject.activeSelf;
                }

                _hasCharacterVisualRootDefault = true;
            }
        }

        private void ApplyCharacterVisualRootVisibility(bool visible)
        {
            if (!_hasCharacterVisualRootDefault)
            {
                EnsurePresentationVisibilityCache();
            }

            if (_characterVisualRoot == null)
            {
                return;
            }

            bool targetActive = visible && _characterVisualRootDefaultActive;
            if (_characterVisualRoot.gameObject.activeSelf != targetActive)
            {
                _characterVisualRoot.gameObject.SetActive(targetActive);
            }
        }

        private void HandleReplicatedStateChanged()
        {
            ApplyPresentation();
            StateChanged?.Invoke(this);
        }
    }
}
