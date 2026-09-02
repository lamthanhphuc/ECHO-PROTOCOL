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

        [Header("Runtime Auto-Find")]
        [SerializeField] private bool autoFindLocalPlayerOnStart = true;

        public HUDInteractionPrompt InteractionPrompt => interactionPrompt;
        public HUDObjectiveTracker ObjectiveTracker => objectiveTracker;
        public HUDPlayerVitals PlayerVitals => playerVitals;
        public HUDHotbar Hotbar => hotbar;
        public HUDTeammateStatus TeammateStatus => teammateStatus;
        public HUD3DWorldMarker WorldMarker => worldMarker;

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

        public void EnsureSubModuleReferences()
        {
            if (interactionPrompt == null) interactionPrompt = GetComponentInChildren<HUDInteractionPrompt>(true);
            if (objectiveTracker == null) objectiveTracker = GetComponentInChildren<HUDObjectiveTracker>(true);
            if (playerVitals == null) playerVitals = GetComponentInChildren<HUDPlayerVitals>(true);
            if (hotbar == null) hotbar = GetComponentInChildren<HUDHotbar>(true);
            if (teammateStatus == null) teammateStatus = GetComponentInChildren<HUDTeammateStatus>(true);
            if (worldMarker == null) worldMarker = GetComponentInChildren<HUD3DWorldMarker>(true);
        }

        public void FindAndBindLocalPlayer()
        {
            PlayerMovement movement = FindAnyObjectByType<PlayerMovement>();
            if (movement != null)
            {
                BindLocalPlayer(movement.gameObject);
            }
        }

        public void BindLocalPlayer(GameObject playerRoot)
        {
            if (playerRoot == null) return;

            var movement = playerRoot.GetComponent<PlayerMovement>();
            var downState = playerRoot.GetComponent<PlayerDownState>();
            var carrier = playerRoot.GetComponent<PlayerEnergyCoreCarrier>();
            var interaction = playerRoot.GetComponent<PlayerInteraction>();
            var inventory = playerRoot.GetComponent<PlayerInventory>();

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
