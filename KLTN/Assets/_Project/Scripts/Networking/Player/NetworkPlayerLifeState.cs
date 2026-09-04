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
        [SerializeField, Min(0)] private int _maximumRevives = 1;
        [SerializeField, Min(0.1f)] private float _reviveDurationSeconds = 2.5f;
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

        private PlayerDownState _legacyDownState;
        private PlayerReviveInteractable _legacyReviveInteractable;

        public bool CanBeRevived => NetworkPlayerLifeStateRules.CanRevive(
            Status,
            ReviveCount,
            _maximumRevives);

        public bool CanMove => NetworkPlayerLifeStateRules.CanMove(Status);
        public bool CanInitiateAction => NetworkPlayerLifeStateRules.CanInitiateAction(Status);
        public bool IsDowned => Status == NetworkPlayerLifeStatus.Downed;
        public bool IsReviveInProgress => IsDowned && Reviver.IsValid && ReviveTimer.IsRunning;
        public bool HasReviveProtection => Status == NetworkPlayerLifeStatus.Alive
                                           && ReviveProtectionRemaining > 0f;
        public bool IsMatchActive => Status == NetworkPlayerLifeStatus.Alive
                                     || IsDowned;
        public float MovementSpeedMultiplier => IsDowned ? _crawlSpeedMultiplier : 1f;
        public float BleedoutRemaining => Remaining(BleedoutTimer);
        public float ReviveProtectionRemaining => Remaining(ProtectionTimer);
        public float ReviveProgress01 => !IsReviveInProgress
            ? 0f
            : 1f - Mathf.Clamp01(Remaining(ReviveTimer) / _reviveDurationSeconds);

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
            }

            ApplyPresentation();
            StateChanged?.Invoke(this);
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;

            // Bleedout wins a same-tick race against revive completion.
            if (NetworkPlayerLifeStateRules.CanBleedOut(Status) && BleedoutTimer.Expired(Runner))
            {
                CommitEliminated(NetworkPlayerLifeTransitionCause.Bleedout, "BLEEDOUT");
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
            ReviveTimer = TickTimer.CreateFromSeconds(Runner, _reviveDurationSeconds);
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
            Reviver = PlayerRef.None;
            ReviveTimer = TickTimer.None;
            ProtectionTimer = TickTimer.None;
            BleedoutTimer = TickTimer.CreateFromSeconds(Runner, _bleedoutSeconds);
            IsCrawling = true;
            DownCount++;
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
                && Vector3.SqrMagnitude(reviverObject.transform.position - transform.position)
                   <= _reviveDistance * _reviveDistance;
        }

        private void CancelReviveAuthoritative(string reason)
        {
            var previousReviver = Reviver;
            Reviver = PlayerRef.None;
            ReviveTimer = TickTimer.None;
            IsCrawling = true;
            CommitStatus(NetworkPlayerLifeStatus.Downed, NetworkPlayerLifeTransitionCause.ReviveCancelled);
            Debug.Log($"[LifeState] Revive cancelled target={Object.InputAuthority}, reviver={previousReviver}, reason={reason}.");
        }

        private void CompleteReviveAuthoritative()
        {
            if (!IsReviveInProgress || !CanContinueRevive()) return;

            var completedReviver = Reviver;
            Reviver = PlayerRef.None;
            ReviveTimer = TickTimer.None;
            BleedoutTimer = TickTimer.None;
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
                false);
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
        }

        private void HandleReplicatedStateChanged()
        {
            ApplyPresentation();
            StateChanged?.Invoke(this);
        }
    }
}
