using System;
using EchoProtocol.AI.Listener.Noise;
using Fusion;
using UnityEngine;
using EchoProtocol.Networking.Authority;

namespace EchoProtocol.Networking
{
    public enum NetworkItemState
    {
        Available = 0,
        PickedUp = 1,
        Dropped = 2,
        Placed = 3,
    }

    public readonly struct NetworkItemTransition
    {
        public NetworkItemTransition(
            NetworkPickupItem item,
            NetworkItemState state,
            PlayerRef actor,
            uint ordinal,
            Vector3 position)
        {
            Item = item;
            State = state;
            Actor = actor;
            Ordinal = ordinal;
            Position = position;
        }

        public NetworkPickupItem Item { get; }
        public NetworkItemState State { get; }
        public PlayerRef Actor { get; }
        public uint Ordinal { get; }
        public Vector3 Position { get; }
    }

    /// <summary>Replicates availability and holder; visibility/collision are derived presentation.</summary>
    public sealed class NetworkPickupItem : NetworkInteractable
    {
        public static event Action<NetworkPickupItem, NetworkItemState, PlayerRef> StateChanged;
        public static event Action<NetworkItemTransition> AuthoritativeStateCommitted;

        [SerializeField] private Renderer _availableVisual;
        [SerializeField] private Collider _pickupCollider;

        [Networked, OnChangedRender(nameof(ApplyReplicatedState))]
        public NetworkItemState State { get; private set; }

        [Networked, OnChangedRender(nameof(ApplyReplicatedState))]
        public PlayerRef Holder { get; private set; }

        [Networked] public uint TransitionOrdinal { get; private set; }

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                State = NetworkItemState.Available;
                Holder = PlayerRef.None;
                TransitionOrdinal = 0;
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
            return (State == NetworkItemState.Available || State == NetworkItemState.Dropped)
                && !Holder.IsValid
                && context.PlayerState != null
                && !context.PlayerState.CarriedCoreId.IsValid
                ? InteractionValidationResult.Accepted
                : InteractionValidationResult.InvalidTargetState;
        }

        protected override void ExecuteInteraction(in InteractionContext context)
        {
            if (!context.PlayerState.TryBeginCarryingCore(Object.Id)) return;
            State = NetworkItemState.PickedUp;
            Holder = context.Player;
            AdvanceTransition();
            ApplyReplicatedState();
            PublishTransition(context.Player);
            Debug.Log($"[NetworkItem] {context.Player} picked up item {Object.Id}.");
        }

        public bool TryDrop(PlayerRef actor, Vector3 position)
        {
            if (!Object.HasStateAuthority || State != NetworkItemState.PickedUp || Holder != actor
                || !TryClearCarrier(actor))
            {
                return false;
            }

            State = NetworkItemState.Dropped;
            Holder = PlayerRef.None;
            transform.position = position;
            AdvanceTransition();
            ApplyReplicatedState();
            PublishTransition(actor);
            HostRuntimeNoiseService.EnsureExists(MatchAuthorityRuntime.Instance)
                .TryAccept(
                    actor,
                    RuntimeNoiseType.CORE_DROP,
                    RuntimeNoiseSourceOccurrenceKey.ForCoreDrop(Object.Id.ToString(), TransitionOrdinal),
                    position,
                    out _);
            Debug.Log($"[NetworkItem] {actor} dropped item {Object.Id}.");
            return true;
        }

        public bool TryPlace(PlayerRef actor, Vector3 position)
        {
            if (!Object.HasStateAuthority || State != NetworkItemState.PickedUp || Holder != actor
                || !TryClearCarrier(actor))
            {
                return false;
            }

            State = NetworkItemState.Placed;
            Holder = PlayerRef.None;
            transform.position = position;
            AdvanceTransition();
            ApplyReplicatedState();
            PublishTransition(actor);
            Debug.Log($"[NetworkItem] {actor} placed item {Object.Id}.");
            return true;
        }

        private void ApplyReplicatedState()
        {
            var available = State == NetworkItemState.Available || State == NetworkItemState.Dropped;
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

        private bool TryClearCarrier(PlayerRef actor)
        {
            return Runner.TryGetPlayerObject(actor, out var playerObject)
                && playerObject.TryGetComponent<LobbyPlayerState>(out var playerState)
                && playerState.TryClearCarriedCore(Object.Id);
        }

        private void AdvanceTransition()
        {
            TransitionOrdinal++;
            if (TransitionOrdinal == 0) TransitionOrdinal = 1;
        }

        private void PublishTransition(PlayerRef actor)
        {
            AuthoritativeStateCommitted?.Invoke(new NetworkItemTransition(
                this,
                State,
                actor,
                TransitionOrdinal,
                transform.position));
        }
    }
}
