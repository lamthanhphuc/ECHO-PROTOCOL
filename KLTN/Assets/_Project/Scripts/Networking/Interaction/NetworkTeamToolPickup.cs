using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking
{
    [DisallowMultipleComponent]
    public sealed class NetworkTeamToolPickup : NetworkInteractable
    {
        [SerializeField, Range(1, 6)] private int _toolId = 2;
        [SerializeField] private string _toolDisplayName = "Team Tool";

        [Networked, OnChangedRender(nameof(OnConsumedChanged))] private NetworkBool IsConsumed { get; set; }
        [Networked, OnChangedRender(nameof(ApplyReplicatedPose))] public Vector3 WorldPosition { get; private set; }
        [Networked, OnChangedRender(nameof(ApplyReplicatedPose))] public Quaternion WorldRotation { get; private set; }

        private bool _pendingDespawn;

        public int ToolId => _toolId;

        public override string InteractionPrompt =>
            string.IsNullOrWhiteSpace(_toolDisplayName)
                ? "Pick Up Team Tool"
                : $"Pick Up {_toolDisplayName}";

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                IsConsumed = false;
                _pendingDespawn = false;
                WorldPosition = transform.position;
                WorldRotation = transform.rotation;
            }

            ApplyReplicatedPose();
            SetVisualsAndCollidersActive(!IsConsumed);
        }

        private void ApplyReplicatedPose()
        {
            if (WorldPosition != Vector3.zero)
            {
                transform.SetPositionAndRotation(WorldPosition, WorldRotation);
            }
        }

        private void OnConsumedChanged()
        {
            SetVisualsAndCollidersActive(!IsConsumed);
        }

        private void SetVisualsAndCollidersActive(bool active)
        {
            foreach (var c in GetComponentsInChildren<Collider>(true))
            {
                c.enabled = active;
            }

            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = active;
            }
        }

        protected override InteractionValidationResult ValidateCurrentState(
            in InteractionContext context)
        {
            if (IsConsumed || _toolId < 1 || _toolId > 6)
            {
                return InteractionValidationResult.InvalidTargetState;
            }

            var playerState = context.PlayerState;
            if (playerState == null || !playerState.IsGameplayPlayer)
            {
                return InteractionValidationResult.InvalidRequester;
            }

            return playerState.ToolId == 0
                ? InteractionValidationResult.Accepted
                : InteractionValidationResult.InvalidTargetState;
        }

        protected override void ExecuteInteraction(in InteractionContext context)
        {
            if (IsConsumed || !Object.HasStateAuthority)
            {
                return;
            }

            var playerState = context.PlayerState;
            if (playerState == null
                || !playerState.IsGameplayPlayer
                || playerState.ToolId != 0
                || _toolId < 1
                || _toolId > 6)
            {
                return;
            }

            IsConsumed = true;
            SetVisualsAndCollidersActive(false);
            playerState.SetGameplayToolId(_toolId);
            _pendingDespawn = false;
        }

        public override void FixedUpdateNetwork()
        {
            if (IsConsumed)
            {
                foreach (var c in GetComponentsInChildren<Collider>(true))
                {
                    if (c.enabled) c.enabled = false;
                }
            }

            _pendingDespawn = false;
            // DO NOT call Runner.Despawn(Object) on scene-placed network objects.
            // SetVisualsAndCollidersActive(false) already disables visuals and colliders across all clients.
        }
    }
}
