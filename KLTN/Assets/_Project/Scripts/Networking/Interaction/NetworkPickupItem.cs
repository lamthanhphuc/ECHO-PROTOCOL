using System;
using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking
{
    public enum NetworkItemState
    {
        Available = 0,
        PickedUp = 1,
    }

    /// <summary>Replicates availability and holder; visibility/collision are derived presentation.</summary>
    public sealed class NetworkPickupItem : NetworkInteractable
    {
        public static event Action<NetworkPickupItem, NetworkItemState, PlayerRef> StateChanged;

        [SerializeField] private Renderer _availableVisual;
        [SerializeField] private Collider _pickupCollider;

        [Networked, OnChangedRender(nameof(ApplyReplicatedState))]
        public NetworkItemState State { get; private set; }

        [Networked, OnChangedRender(nameof(ApplyReplicatedState))]
        public PlayerRef Holder { get; private set; }

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                State = NetworkItemState.Available;
                Holder = PlayerRef.None;
            }
            ApplyReplicatedState();
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority || State != NetworkItemState.PickedUp || IsHolderConnected()) return;

            Debug.Log($"[NetworkItem] Holder {Holder} left; returning item {Object.Id} to Available.");
            State = NetworkItemState.Available;
            Holder = PlayerRef.None;
            ApplyReplicatedState();
        }

        protected override InteractionValidationResult ValidateCurrentState(in InteractionContext context)
        {
            return State == NetworkItemState.Available && !Holder.IsValid
                ? InteractionValidationResult.Accepted
                : InteractionValidationResult.InvalidTargetState;
        }

        protected override void ExecuteInteraction(in InteractionContext context)
        {
            State = NetworkItemState.PickedUp;
            Holder = context.Player;
            ApplyReplicatedState();
            Debug.Log($"[NetworkItem] {context.Player} picked up item {Object.Id}.");
        }

        private void ApplyReplicatedState()
        {
            var available = State == NetworkItemState.Available;
            if (_availableVisual != null) _availableVisual.enabled = available;
            if (_pickupCollider != null) _pickupCollider.enabled = available;
            StateChanged?.Invoke(this, State, Holder);
        }

        private bool IsHolderConnected()
        {
            foreach (var player in Runner.ActivePlayers)
            {
                if (player == Holder) return true;
            }
            return false;
        }
    }
}
