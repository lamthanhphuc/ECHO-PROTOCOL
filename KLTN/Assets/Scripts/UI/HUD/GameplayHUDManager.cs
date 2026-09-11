using EchoProtocol.Networking;
using EchoProtocol.Tools.Scanner;
using Fusion;
using UnityEngine;

namespace EchoProtocol.UI.HUD
{
    [DisallowMultipleComponent]
    public class GameplayHUDManager : MonoBehaviour
    {
        [Header("HUD Sub-modules")]
        [SerializeField] private HUDInteractionPrompt interactionPrompt;
        [SerializeField] private HUDObjectiveTracker objectiveTracker;
        [SerializeField] private HUDPlayerVitals playerVitals;
        [SerializeField] private HUDHotbar hotbar;
        [SerializeField] private HUDTeammateStatus teammateStatus;
        [SerializeField] private HUD3DWorldMarker worldMarker;
        [SerializeField] private HUDFieldScanner fieldScannerHUD;

        [Header("Runtime Auto-Find")]
        [SerializeField] private bool autoFindLocalPlayerOnStart = true;

        private LobbyPlayerState _boundNetworkPlayerState;

        public HUDInteractionPrompt InteractionPrompt => interactionPrompt;
        public HUDObjectiveTracker ObjectiveTracker => objectiveTracker;
        public HUDPlayerVitals PlayerVitals => playerVitals;
        public HUDHotbar Hotbar => hotbar;
        public HUDTeammateStatus TeammateStatus => teammateStatus;
        public HUD3DWorldMarker WorldMarker => worldMarker;
        public HUDFieldScanner FieldScannerHUD => fieldScannerHUD;

        private void Awake()
        {
            EnsureSubModuleReferences();
        }

        private void Start()
        {
            if (autoFindLocalPlayerOnStart)
            {
                FindAndBindLocalPlayer();
            }
        }

        private void Update()
        {
            if (!autoFindLocalPlayerOnStart)
            {
                return;
            }

            if (IsActiveFusionSession() && !IsValidLocalNetworkPlayer(_boundNetworkPlayerState))
            {
                FindAndBindLocalPlayer();
            }
        }

        public void EnsureSubModuleReferences()
        {
            if (interactionPrompt == null) interactionPrompt = GetComponentInChildren<HUDInteractionPrompt>(true);
            if (objectiveTracker == null) objectiveTracker = GetComponentInChildren<HUDObjectiveTracker>(true);
            if (playerVitals == null) playerVitals = GetComponentInChildren<HUDPlayerVitals>(true);
            if (hotbar == null) hotbar = GetComponentInChildren<HUDHotbar>(true);
            if (teammateStatus == null) teammateStatus = GetComponentInChildren<HUDTeammateStatus>(true);
            if (worldMarker == null) worldMarker = GetComponentInChildren<HUD3DWorldMarker>(true);
            if (fieldScannerHUD == null) fieldScannerHUD = GetComponentInChildren<HUDFieldScanner>(true);
        }

        public void FindAndBindLocalPlayer()
        {
            var playerStates = FindObjectsByType<LobbyPlayerState>(FindObjectsInactive.Exclude);
            for (var i = 0; i < playerStates.Length; i++)
            {
                var state = playerStates[i];
                if (IsValidLocalNetworkPlayer(state))
                {
                    _boundNetworkPlayerState = state;
                    BindLocalPlayer(state.gameObject);
                    return;
                }
            }

            if (IsActiveFusionSession())
            {
                _boundNetworkPlayerState = null;
                ClearPlayerBinding();
                return;
            }

            _boundNetworkPlayerState = null;
            PlayerMovement movement = FindAnyObjectByType<PlayerMovement>();
            if (movement != null)
            {
                BindLocalPlayer(movement.gameObject);
            }
        }

        private void ClearPlayerBinding()
        {
            if (playerVitals != null)
            {
                playerVitals.BindPlayer(null, null, null);
            }

            if (interactionPrompt != null)
            {
                interactionPrompt.BindInteraction(null);
            }

            if (hotbar != null)
            {
                hotbar.BindInventory(null, null);
            }

            if (fieldScannerHUD != null)
            {
                fieldScannerHUD.UnbindScanner();
            }
        }

        private static bool IsValidLocalNetworkPlayer(LobbyPlayerState state)
        {
            return state != null
                && state.Object != null
                && state.Object.IsValid
                && state.Runner != null
                && state.Runner.IsRunning
                && state.Object.HasInputAuthority
                && state.IsGameplayPlayer;
        }

        private static bool IsActiveFusionSession()
        {
            var runners = FindObjectsByType<NetworkRunner>(FindObjectsInactive.Exclude);
            for (var i = 0; i < runners.Length; i++)
            {
                if (runners[i] != null && runners[i].IsRunning)
                {
                    return true;
                }
            }

            return false;
        }

        public void BindLocalPlayer(GameObject playerRoot)
        {
            if (playerRoot == null) return;

            var movement = playerRoot.GetComponentInChildren<PlayerMovement>(true);
            var downState = playerRoot.GetComponentInChildren<PlayerDownState>(true);
            var carrier = playerRoot.GetComponentInChildren<PlayerEnergyCoreCarrier>(true);
            var interaction = playerRoot.GetComponentInChildren<PlayerInteraction>(true);
            var inventory = playerRoot.GetComponentInChildren<PlayerInventory>(true);
            var fieldScanner = playerRoot.GetComponent<NetworkFieldScanner>();

            if (playerVitals != null)
            {
                playerVitals.BindPlayer(movement, downState, carrier);
            }

            if (interactionPrompt != null && interaction != null)
            {
                interactionPrompt.BindInteraction(interaction);
            }

            if (hotbar != null)
            {
                hotbar.BindInventory(inventory, carrier);
            }

            if (fieldScannerHUD != null)
            {
                fieldScannerHUD.BindScanner(fieldScanner);
            }

            if (teammateStatus != null)
            {
                teammateStatus.RefreshDiscoveredPlayers();
            }
        }

        public void SetHUDVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
    }
}
