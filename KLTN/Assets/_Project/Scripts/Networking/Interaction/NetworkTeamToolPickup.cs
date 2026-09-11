using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking
{
    [DisallowMultipleComponent]
    public sealed class NetworkTeamToolPickup : NetworkInteractable
    {
        [SerializeField, Range(1, 4)] private int _toolId = 2;
        [SerializeField] private string _toolDisplayName = "Team Tool";

        [Networked] private NetworkBool IsConsumed { get; set; }

        private bool _pendingDespawn;

        public int ToolId => _toolId;

        public override string InteractionPrompt =>
            string.IsNullOrWhiteSpace(_toolDisplayName)
                ? "Pick Up Team Tool"
                : $"Pick Up {_toolDisplayName}";

        protected override InteractionValidationResult ValidateCurrentState(
            in InteractionContext context)
        {
            if (IsConsumed || _toolId < 1 || _toolId > 4)
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
                || _toolId > 4)
            {
                return;
            }

            IsConsumed = true;
            foreach (var c in GetComponentsInChildren<Collider>(true))
            {
                c.enabled = false;
            }
            playerState.SetGameplayToolId(_toolId);
            _pendingDespawn = true;
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

            if (!_pendingDespawn
                || Object == null
                || !Object.IsValid
                || !Object.HasStateAuthority
                || Runner == null)
            {
                return;
            }

            _pendingDespawn = false;
            Runner.Despawn(Object);
        }
    }
}
