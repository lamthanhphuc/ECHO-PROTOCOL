using System;
using EchoProtocol.Networking;
using Fusion;
using UnityEngine;

namespace EchoProtocol.Tools.Scanner
{
    /// <summary>
    /// Network-synced world pickup for handheld tools (e.g. Field Scanner).
    /// Inherits from NetworkInteractable for authoritative server validation,
    /// and implements IInteractable for local prompt display and interaction.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class NetworkToolPickup : NetworkInteractable, IInteractable
    {
        public static event Action<NetworkToolPickup, PlayerRef> ToolPickedUp;

        [Header("Tool Configuration")]
        [SerializeField] private InventoryItemDefinition _toolItemDefinition;
        [SerializeField, Min(1)] private int _toolId = 1;
        [SerializeField] private string _pickupPrompt = "Nhặt Field Scanner [E]";

        [Header("Components")]
        [SerializeField] private Collider _pickupCollider;
        [SerializeField] private Renderer _visualRenderer;

        [Networked, OnChangedRender(nameof(OnReplicatedStateChanged))]
        private NetworkBool _isPickedUp { get; set; }

        private bool _localPickedUp;
        private bool _pendingDespawn;

        public InventoryItemDefinition ToolItemDefinition => _toolItemDefinition;
        public int ToolId => _toolId;

        public bool IsPickedUp
        {
            get
            {
                if (Runner == null || Object == null || !Object.IsValid)
                {
                    return _localPickedUp;
                }
                return _isPickedUp;
            }
            set
            {
                _localPickedUp = value;
                if (Runner != null && Object != null && Object.IsValid && Object.HasStateAuthority)
                {
                    _isPickedUp = value;
                }
            }
        }

        public override string InteractionPrompt => _pickupPrompt;
        string IInteractable.InteractionPrompt => _pickupPrompt;

        private void Reset()
        {
            _pickupCollider = GetComponent<Collider>();
            _visualRenderer = GetComponentInChildren<Renderer>();
        }

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                _isPickedUp = false;
                _localPickedUp = false;
                _pendingDespawn = false;
            }

            OnReplicatedStateChanged();
        }

        private void OnReplicatedStateChanged()
        {
            if (Runner != null && Object != null && Object.IsValid)
            {
                _localPickedUp = _isPickedUp;
            }

            bool visible = !IsPickedUp;

            if (_pickupCollider == null)
            {
                _pickupCollider = GetComponent<Collider>();
            }
            if (_pickupCollider != null)
            {
                _pickupCollider.enabled = visible;
            }

            if (_visualRenderer == null)
            {
                _visualRenderer = GetComponentInChildren<Renderer>();
            }
            if (_visualRenderer != null)
            {
                _visualRenderer.enabled = visible;
            }

            // Also toggle child visuals
            for (int i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetActive(visible);
            }
        }

        // ==========================================
        // NETWORK INTERACTION VALIDATION (HOST)
        // ==========================================

        protected override InteractionValidationResult ValidateCurrentState(in InteractionContext context)
        {
            if (IsPickedUp)
            {
                return InteractionValidationResult.InvalidTargetState;
            }

            if (context.PlayerState == null)
            {
                return InteractionValidationResult.InvalidRequester;
            }

            // Cannot pick up tool if carrying an Energy Core
            if (context.PlayerState.Object != null && context.PlayerState.Object.IsValid)
            {
                if (context.PlayerState.CarriedCoreId.IsValid)
                {
                    return InteractionValidationResult.InvalidTargetState;
                }

                // Cannot pick up tool if already holding a tool
                if (context.PlayerState.ToolId > 0)
                {
                    return InteractionValidationResult.InvalidTargetState;
                }
            }

            return InteractionValidationResult.Accepted;
        }

        protected override void ExecuteInteraction(in InteractionContext context)
        {
            IsPickedUp = true;
            foreach (var c in GetComponentsInChildren<Collider>(true))
            {
                c.enabled = false;
            }
            OnReplicatedStateChanged();

            // Set authoritative tool ID on player
            if (context.PlayerState != null && context.PlayerState.Object != null && context.PlayerState.Object.IsValid)
            {
                context.PlayerState.SetGameplayToolId(_toolId);
            }

            // Give item to player's local inventory via Target RPC
            if (Runner != null && Object != null && Object.IsValid && Object.HasStateAuthority)
            {
                RpcTargetGiveLocalInventory(context.Player);
                _pendingDespawn = true;
            }

            // Also give directly to local inventory if Host is the requester
            if (context.Requester != null)
            {
                var netObj = context.Requester.GetComponentInParent<NetworkObject>();
                if (netObj != null && netObj.HasInputAuthority)
                {
                    GiveToPlayerLocal(context.Requester.gameObject);
                }
            }

            ToolPickedUp?.Invoke(this, context.Player);
            Debug.Log($"[NetworkToolPickup] Player {context.Player} picked up tool '{_toolItemDefinition?.DisplayName ?? _toolId.ToString()}'.");
        }

        public override void FixedUpdateNetwork()
        {
            if (IsPickedUp)
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

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        private void RpcTargetGiveLocalInventory([RpcTarget] PlayerRef targetPlayer)
        {
            var localPlayer = FindLocalPlayer();
            if (localPlayer != null)
            {
                GiveToPlayerLocal(localPlayer);
            }
        }

        // ==========================================
        // IINTERACTABLE (LOCAL / OFFLINE FALLBACK)
        // ==========================================

        public bool CanInteract(GameObject interactor)
        {
            if (IsPickedUp) return false;
            if (interactor == null) return false;

            var carrier = interactor.GetComponentInParent<PlayerEnergyCoreCarrier>();
            if (carrier != null && carrier.IsCarrying) return false;

            var lobbyState = interactor.GetComponentInParent<LobbyPlayerState>();
            if (lobbyState != null && lobbyState.Object != null && lobbyState.Object.IsValid && lobbyState.Object.Id.IsValid && lobbyState.Runner != null && lobbyState.Runner.IsRunning)
            {
                if (lobbyState.CarriedCoreId.IsValid) return false;
                if (lobbyState.ToolId > 0) return false;
            }

            var inventory = interactor.GetComponentInParent<PlayerInventory>();
            if (inventory != null && inventory.TeamToolSlot != null) return false;

            return true;
        }

        public void Interact(GameObject interactor)
        {
            if (interactor == null) return;

            // In networked gameplay, forward to NetworkPlayerInteractor
            var interactorComp = interactor.GetComponentInParent<NetworkPlayerInteractor>();
            if (interactorComp != null && interactorComp.Object != null && interactorComp.Object.IsValid && Object != null && Object.IsValid)
            {
                interactorComp.RequestInteraction(this);
                return;
            }

            // Local fallback (offline testing / single player / scene object without network authority)
            GiveToPlayerLocal(interactor);
        }

        private void GiveToPlayerLocal(GameObject interactor)
        {
            var inv = interactor.GetComponentInParent<PlayerInventory>();
            if (inv != null)
            {
                EnsureToolItemDefinition();
                if (_toolItemDefinition != null)
                {
                    if (inv.TryAdd(_toolItemDefinition))
                    {
                        IsPickedUp = true;
                        OnReplicatedStateChanged();
                    }
                }
            }
        }

        private void EnsureToolItemDefinition()
        {
            if (_toolItemDefinition != null) return;

            _toolItemDefinition = Resources.Load<InventoryItemDefinition>("SO_FieldScanner_ItemDefinition");
#if UNITY_EDITOR
            if (_toolItemDefinition == null)
            {
                _toolItemDefinition = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(
                    "Assets/ScriptableObjects/Inventory/SO_FieldScanner_ItemDefinition.asset");
            }
#endif
        }

        private static GameObject FindLocalPlayer()
        {
            foreach (var interactor in FindObjectsByType<NetworkPlayerInteractor>(FindObjectsInactive.Exclude))
            {
                if (interactor.Object != null && interactor.Object.HasInputAuthority)
                {
                    return interactor.gameObject;
                }
            }

            return null;
        }
    }
}
