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

    public static class NetworkPlayerLifeStateRules
    {
        public static bool CanDown(NetworkPlayerLifeStatus status) =>
            status == NetworkPlayerLifeStatus.Alive;

        public static bool CanRevive(NetworkPlayerLifeStatus status, int reviveCount, int maximumRevives) =>
            status == NetworkPlayerLifeStatus.Downed
            && reviveCount >= 0
            && maximumRevives >= 0
            && reviveCount < maximumRevives;

        public static bool CanEliminate(NetworkPlayerLifeStatus status, int reviveCount, int maximumRevives) =>
            status == NetworkPlayerLifeStatus.Downed
            && maximumRevives >= 0
            && reviveCount >= maximumRevives;

        public static bool CanEscape(NetworkPlayerLifeStatus status) =>
            status == NetworkPlayerLifeStatus.Alive;
    }

    /// <summary>Single State-Authority owner for player survival mutations.</summary>
    [DisallowMultipleComponent]
    public sealed class NetworkPlayerLifeState : NetworkBehaviour
    {
        public static event Action<NetworkPlayerLifeState> StateChanged;

        [SerializeField, Min(0)] private int _maximumRevives = 1;

        [Networked, OnChangedRender(nameof(HandleReplicatedStateChanged))]
        public NetworkPlayerLifeStatus Status { get; private set; }

        [Networked] public int DownCount { get; private set; }
        [Networked] public int ReviveCount { get; private set; }
        [Networked] public uint TransitionOrdinal { get; private set; }

        public bool CanBeRevived => NetworkPlayerLifeStateRules.CanRevive(
            Status,
            ReviveCount,
            _maximumRevives);

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                Status = NetworkPlayerLifeStatus.Alive;
                DownCount = 0;
                ReviveCount = 0;
                TransitionOrdinal = 0;
            }

            StateChanged?.Invoke(this);
        }

        public bool TryApplyMonsterDown(string monsterType, Vector3 hitPosition)
        {
            if (!Object.HasStateAuthority || !NetworkPlayerLifeStateRules.CanDown(Status))
            {
                return false;
            }

            Status = NetworkPlayerLifeStatus.Downed;
            DownCount++;
            AdvanceTransition();
            MatchAuthorityRuntime.Instance?.RecordPlayerDowned(
                Object.InputAuthority,
                BuildOccurrenceKey("down"),
                monsterType,
                DownCount,
                hitPosition);
            StateChanged?.Invoke(this);
            Debug.Log($"[LifeState] {Object.InputAuthority} Alive -> Downed by {monsterType}.");
            return true;
        }

        public bool TryRevive(PlayerRef reviver, bool usedFirstAidKit = false)
        {
            if (!Object.HasStateAuthority || !reviver.IsValid || !CanBeRevived)
            {
                return false;
            }

            Status = NetworkPlayerLifeStatus.Alive;
            ReviveCount++;
            AdvanceTransition();
            MatchAuthorityRuntime.Instance?.RecordPlayerRevived(
                Object.InputAuthority,
                reviver,
                BuildOccurrenceKey("revive"),
                ReviveCount,
                usedFirstAidKit);
            StateChanged?.Invoke(this);
            Debug.Log($"[LifeState] {Object.InputAuthority} revived by {reviver}.");
            return true;
        }

        public bool TryEliminateForReviveLimit()
        {
            if (!Object.HasStateAuthority
                || !NetworkPlayerLifeStateRules.CanEliminate(Status, ReviveCount, _maximumRevives))
            {
                return false;
            }

            Status = NetworkPlayerLifeStatus.Eliminated;
            AdvanceTransition();
            MatchAuthorityRuntime.Instance?.RecordPlayerEliminated(
                Object.InputAuthority,
                BuildOccurrenceKey("eliminate"),
                ReviveCount);
            StateChanged?.Invoke(this);
            Debug.Log($"[LifeState] {Object.InputAuthority} Downed -> Eliminated.");
            return true;
        }

        public bool TryEscape(bool rescuedTeammate = false)
        {
            if (!Object.HasStateAuthority || !NetworkPlayerLifeStateRules.CanEscape(Status))
            {
                return false;
            }

            Status = NetworkPlayerLifeStatus.Escaped;
            AdvanceTransition();
            MatchAuthorityRuntime.Instance?.RecordPlayerEscaped(
                Object.InputAuthority,
                BuildOccurrenceKey("escape"),
                rescuedTeammate);
            StateChanged?.Invoke(this);
            Debug.Log($"[LifeState] {Object.InputAuthority} Alive -> Escaped.");
            return true;
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

        private void HandleReplicatedStateChanged()
        {
            StateChanged?.Invoke(this);
        }
    }
}
