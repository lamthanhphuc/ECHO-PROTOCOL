using System;
using EchoProtocol.AI.Listener.Noise;
using Fusion;
using UnityEngine;
using EchoProtocol.Networking.Authority;

namespace EchoProtocol.Networking
{
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
        [SerializeField] private Vector3 _holderLocalPosition = new Vector3(0.35f, 0.9f, 0.45f);
        [SerializeField] private Vector3 _holderLocalEulerAngles;

        [Networked, OnChangedRender(nameof(ApplyReplicatedState))]
        public NetworkItemState State { get; private set; }

        [Networked, OnChangedRender(nameof(ApplyReplicatedState))]
        public PlayerRef Holder { get; private set; }

        [Networked] public uint TransitionOrdinal { get; private set; }

        [Networked] public NetworkId PlacedSectorId { get; private set; }

        [Networked] public int PlacementSlot { get; private set; }

        [Networked, OnChangedRender(nameof(ApplyReplicatedPose))]
        public Vector3 WorldPosition { get; private set; }

        [Networked, OnChangedRender(nameof(ApplyReplicatedPose))]
        public Quaternion WorldRotation { get; private set; }

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                State = NetworkItemState.Available;
                Holder = PlayerRef.None;
                TransitionOrdinal = 0;
                PlacedSectorId = default;
                PlacementSlot = -1;
                WorldPosition = transform.position;
                WorldRotation = transform.rotation;
            }
            ApplyReplicatedState();
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority || State != NetworkItemState.Carried) return;

            if (TryGetHolderPose(out var carriedPosition, out var carriedRotation))
            {
                // This is the authoritative recovery pose if the holder disconnects before the next tick.
                WorldPosition = carriedPosition;
                WorldRotation = carriedRotation;
                return;
            }

            var disconnectedHolder = Holder;
            Debug.Log($"[NetworkItem] Holder {disconnectedHolder} left; dropping item {Object.Id} at its last authoritative pose.");
            State = NetworkItemState.Dropped;
            Holder = PlayerRef.None;
            PlacedSectorId = default;
            PlacementSlot = -1;
            AdvanceTransition();
            ApplyReplicatedState();
            PublishTransition(disconnectedHolder);
        }

        public override void Render()
        {
            ApplyReplicatedPose();
        }

        protected override InteractionValidationResult ValidateCurrentState(in InteractionContext context)
        {
            return EnergyCoreAuthorityRules.CanPickup(
                    State,
                    Holder,
                    context.PlayerState != null,
                    context.PlayerState != null && context.PlayerState.CarriedCoreId.IsValid)
                ? InteractionValidationResult.Accepted
                : InteractionValidationResult.InvalidTargetState;
        }

        protected override void ExecuteInteraction(in InteractionContext context)
        {
            if (!context.PlayerState.TryBeginCarryingCore(Object.Id)) return;
            State = NetworkItemState.Carried;
            Holder = context.Player;
            PlacedSectorId = default;
            PlacementSlot = -1;
            AdvanceTransition();
            ApplyReplicatedState();
            PublishTransition(context.Player);
            Debug.Log($"[NetworkItem] {context.Player} picked up item {Object.Id}.");
        }

        public bool CanBeDroppedBy(PlayerRef actor)
        {
            return Object.HasStateAuthority
                && EnergyCoreAuthorityRules.CanDrop(State, Holder, actor)
                && TryGetCarrier(actor, out var playerState)
                && playerState.CarriedCoreId == Object.Id;
        }

        public bool TryDrop(PlayerRef actor, Vector3 position, Quaternion rotation)
        {
            if (!CanBeDroppedBy(actor) || !TryClearCarrier(actor))
            {
                return false;
            }

            State = NetworkItemState.Dropped;
            Holder = PlayerRef.None;
            PlacedSectorId = default;
            PlacementSlot = -1;
            WorldPosition = position;
            WorldRotation = rotation;
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

        public bool CanBePlacedBy(PlayerRef actor)
        {
            return Object.HasStateAuthority
                && EnergyCoreAuthorityRules.CanPlace(State, Holder, actor)
                && TryGetCarrier(actor, out var playerState)
                && playerState.CarriedCoreId == Object.Id;
        }

        public bool TryPlace(
            PlayerRef actor,
            NetworkId sectorId,
            int placementSlot,
            Vector3 position,
            Quaternion rotation)
        {
            if (!CanBePlacedBy(actor)
                || !sectorId.IsValid
                || placementSlot < 0
                || !Runner.TryFindObject(sectorId, out var sectorObject)
                || sectorObject == null
                || !sectorObject.HasStateAuthority
                || !sectorObject.TryGetComponent<NetworkSectorBox>(out _)
                || !TryClearCarrier(actor))
            {
                return false;
            }

            State = NetworkItemState.Placed;
            Holder = PlayerRef.None;
            PlacedSectorId = sectorId;
            PlacementSlot = placementSlot;
            WorldPosition = position;
            WorldRotation = rotation;
            AdvanceTransition();
            ApplyReplicatedState();
            PublishTransition(actor);
            Debug.Log($"[NetworkItem] {actor} placed item {Object.Id}.");
            return true;
        }

        private void ApplyReplicatedState()
        {
            // Semantic state drives presentation. Carried and Placed cores remain visible.
            if (_availableVisual != null) _availableVisual.enabled = true;
            if (_pickupCollider != null)
            {
                _pickupCollider.enabled = State == NetworkItemState.Available || State == NetworkItemState.Dropped;
            }
            ApplyReplicatedPose();
            StateChanged?.Invoke(this, State, Holder);
        }

        private void ApplyReplicatedPose()
        {
            if (State == NetworkItemState.Carried && TryGetHolderPose(out var position, out var rotation))
            {
                transform.SetPositionAndRotation(position, rotation);
                return;
            }

            transform.SetPositionAndRotation(WorldPosition, WorldRotation);
        }

        private bool TryGetHolderPose(out Vector3 position, out Quaternion rotation)
        {
            if (Holder.IsRealPlayer
                && Runner != null
                && IsActivePlayer(Holder)
                && Runner.TryGetPlayerObject(Holder, out var playerObject)
                && playerObject != null
                && playerObject.InputAuthority == Holder)
            {
                position = playerObject.transform.TransformPoint(_holderLocalPosition);
                rotation = playerObject.transform.rotation * Quaternion.Euler(_holderLocalEulerAngles);
                return true;
            }

            position = WorldPosition;
            rotation = WorldRotation;
            return false;
        }

        private bool IsActivePlayer(PlayerRef player)
        {
            foreach (var activePlayer in Runner.ActivePlayers)
            {
                if (activePlayer == player) return true;
            }
            return false;
        }

        private bool TryClearCarrier(PlayerRef actor)
        {
            return TryGetCarrier(actor, out var playerState) && playerState.TryClearCarriedCore(Object.Id);
        }

        private bool TryGetCarrier(PlayerRef actor, out LobbyPlayerState playerState)
        {
            playerState = null;
            return actor.IsRealPlayer
                && Runner.TryGetPlayerObject(actor, out var playerObject)
                && playerObject != null
                && playerObject.InputAuthority == actor
                && playerObject.TryGetComponent(out playerState);
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
