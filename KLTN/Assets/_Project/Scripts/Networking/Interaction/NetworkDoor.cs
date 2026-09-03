using System;
using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking
{
    public enum NetworkDoorState
    {
        Closed = 0,
        Open = 1,
        Locked = 2,
    }

    /// <summary>Replicates door semantics; rotation and collision are derived presentation.</summary>
    public sealed class NetworkDoor : NetworkInteractable
    {
        public static event Action<NetworkDoor, NetworkDoorState> StateChanged;

        [SerializeField] private Transform _doorVisual;
        [SerializeField] private Collider _blockingCollider;
        [SerializeField] private Vector3 _closedEulerAngles;
        [SerializeField] private Vector3 _openEulerAngles = new Vector3(0f, 90f, 0f);
        [SerializeField] private bool _startsLocked;

        [Networked, OnChangedRender(nameof(ApplyReplicatedState))]
        public NetworkDoorState State { get; private set; }

        [Networked] public NetworkId MatchStateId { get; private set; }

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                State = _startsLocked ? NetworkDoorState.Locked : NetworkDoorState.Closed;
            }
            ApplyReplicatedState();
        }

        public void InitializeAuthoritative(NetworkId matchStateId)
        {
            if (!Object.HasStateAuthority || !matchStateId.IsValid) return;
            MatchStateId = matchStateId;
            State = NetworkDoorState.Locked;
            ApplyReplicatedState();
        }

        public bool SetLockedAuthoritative(bool locked)
        {
            if (!Object.HasStateAuthority) return false;
            if (TryGetMatchState(out var matchState) && matchState.IsEnded) return false;
            State = locked ? NetworkDoorState.Locked : NetworkDoorState.Closed;
            ApplyReplicatedState();
            return true;
        }

        protected override InteractionValidationResult ValidateCurrentState(in InteractionContext context)
        {
            if (TryGetMatchState(out var matchState))
            {
                return !matchState.IsEnded && matchState.CurrentPhase == NetworkMatchPhase.FinalHunt
                    ? InteractionValidationResult.Accepted
                    : InteractionValidationResult.InvalidTargetState;
            }

            return State == NetworkDoorState.Locked
                ? InteractionValidationResult.InvalidTargetState
                : InteractionValidationResult.Accepted;
        }

        protected override void ExecuteInteraction(in InteractionContext context)
        {
            if (TryGetMatchState(out var matchState))
            {
                if (matchState.TryEnterEscape(Object.Id, context.Player))
                {
                    State = NetworkDoorState.Open;
                    ApplyReplicatedState();
                    Debug.Log($"[NetworkDoor] {context.Player} opened escape door {Object.Id}.");
                }
                return;
            }

            State = State == NetworkDoorState.Open
                ? NetworkDoorState.Closed
                : NetworkDoorState.Open;
            ApplyReplicatedState();
            Debug.Log($"[NetworkDoor] {context.Player} changed door {Object.Id} to {State}.");
        }

        private bool TryGetMatchState(out NetworkMatchState matchState)
        {
            matchState = null;
            return MatchStateId.IsValid
                && Runner.TryFindObject(MatchStateId, out var matchObject)
                && matchObject != null
                && matchObject.TryGetComponent(out matchState);
        }

        private void ApplyReplicatedState()
        {
            var visual = _doorVisual != null ? _doorVisual : transform;
            visual.localRotation = Quaternion.Euler(
                State == NetworkDoorState.Open ? _openEulerAngles : _closedEulerAngles);
            var blockingCollider = _blockingCollider != null ? _blockingCollider : GetComponent<Collider>();
            if (blockingCollider != null) blockingCollider.enabled = State != NetworkDoorState.Open;
            StateChanged?.Invoke(this, State);
        }
    }
}
