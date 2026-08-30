using System;
using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking
{
    /// <summary>Reference implementation: the RPC is transient; IsActive is the replicated result.</summary>
    public sealed class NetworkToggleInteractable : NetworkInteractable
    {
        public static event Action<NetworkToggleInteractable, bool> StateChanged;

        [SerializeField] private GameObject _activeVisual;

        [Networked, OnChangedRender(nameof(ApplyReplicatedState))]
        public NetworkBool IsActive { get; private set; }

        public override void Spawned()
        {
            ApplyReplicatedState();
        }

        protected override void ExecuteInteraction(in InteractionContext context)
        {
            IsActive = !IsActive;
            ApplyReplicatedState();
            Debug.Log($"[Interaction] {context.Player} set target {Object.Id} active={IsActive}.");
        }

        private void ApplyReplicatedState()
        {
            if (_activeVisual != null) _activeVisual.SetActive(IsActive);
            StateChanged?.Invoke(this, IsActive);
        }
    }
}
