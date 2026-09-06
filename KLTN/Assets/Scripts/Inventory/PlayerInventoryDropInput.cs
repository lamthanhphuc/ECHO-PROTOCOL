using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInventoryDropInput : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerEnergyCoreCarrier coreCarrier;
    [SerializeField] private Transform dropOrigin;
    [SerializeField] private float dropForwardDistance = 1.25f;
    [SerializeField] private float throwForwardDistance = 3.0f;
    [SerializeField] private int selectedNormalSlot;
    [SerializeField] private GameObject noiseMakerDeployedPrefab;

    public int SelectedNormalSlot => selectedNormalSlot;

    private void Awake()
    {
        if (inventory == null)
        {
            inventory = GetComponent<PlayerInventory>();
        }

        if (coreCarrier == null)
        {
            coreCarrier = GetComponent<PlayerEnergyCoreCarrier>();
        }

        if (dropOrigin == null && Camera.main != null)
        {
            dropOrigin = Camera.main.transform;
        }

    }

    private void Update()
    {
        if (!HasLocalControl())
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (inventory == null)
        {
            return;
        }

        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                selectedNormalSlot = 0;
            }

            if (keyboard.digit2Key.wasPressedThisFrame)
            {
                selectedNormalSlot = 1;
            }

            if (keyboard.gKey.wasPressedThisFrame)
            {
                DropCurrentItem();
            }
        }

        bool throwPressed = (keyboard != null && keyboard.tKey.wasPressedThisFrame)
            || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

        if (throwPressed)
        {
            TryUseOrThrowTeamTool();
        }
    }

    public bool DropCurrentItem()
    {
        if (coreCarrier != null && coreCarrier.IsCarrying)
        {
            return false;
        }

        if (DropSelectedNormalSlot())
        {
            return true;
        }

        return DropTeamTool();
    }

    public bool DropSelectedNormalSlot()
    {
        GetDropPose(out var pos, out var rot);
        return inventory != null && inventory.TryDropNormalSlot(selectedNormalSlot, pos, rot);
    }

    public bool DropTeamTool()
    {
        GetDropPose(out var pos, out var rot);
        return inventory != null && inventory.TryDropTeamTool(pos, rot);
    }

    public bool TryUseOrThrowTeamTool()
    {
        if (coreCarrier != null && coreCarrier.IsCarrying)
        {
            return false;
        }

        if (inventory == null || inventory.TeamToolSlot == null)
        {
            return false;
        }

        var networkObject = GetComponentInParent<NetworkObject>();
        var interactor = GetComponentInParent<EchoProtocol.Networking.NetworkPlayerInteractor>();
        var lobbyState = GetComponentInParent<EchoProtocol.Networking.LobbyPlayerState>();

        // If in an active online Fusion session with valid gameplay player:
        if (networkObject != null && networkObject.IsValid && networkObject.Runner != null 
            && networkObject.Runner.IsRunning && interactor != null && lobbyState != null && lobbyState.IsGameplayPlayer)
        {
            return interactor.RequestUseTeamTool();
        }

        // Otherwise (local play / offline / test scene):
        return ExecuteLocalTeamToolThrow();
    }

    private bool ExecuteLocalTeamToolThrow()
    {
        if (inventory == null || inventory.TeamToolSlot == null)
        {
            return false;
        }

        InventoryItemDefinition toolItem = inventory.TeamToolSlot;
        string toolId = (toolItem.ItemId ?? string.Empty).ToLowerInvariant();
        string toolName = (toolItem.DisplayName ?? string.Empty).ToLowerInvariant();

        if (toolId.Contains("noise") || toolId.Contains("beacon") || toolName.Contains("noise") || toolName.Contains("beacon"))
        {
            Transform origin = dropOrigin != null ? dropOrigin : transform;
            ItemDropPlacementUtility.GetFloorSnappedPose(origin, throwForwardDistance, out Vector3 spawnPos, out _);

            GameObject prefabToSpawn = noiseMakerDeployedPrefab;
            if (prefabToSpawn == null)
            {
                prefabToSpawn = Resources.Load<GameObject>("DistressBeaconDeployed");
            }

            if (prefabToSpawn != null)
            {
                GameObject beaconGo = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
                var beacon = beaconGo.GetComponent<NoiseMakerBeacon>();
                if (beacon != null)
                {
                    beacon.Initialize(default, "local_player", 1);
                }
            }

            inventory.TryRemove(toolItem);
            return true;
        }

        return false;
    }

    private void GetDropPose(out Vector3 position, out Quaternion rotation)
    {
        Transform origin = dropOrigin != null ? dropOrigin : transform;
        ItemDropPlacementUtility.GetFloorSnappedPose(origin, dropForwardDistance, out position, out rotation);
    }

    private bool HasLocalControl()
    {
        NetworkObject networkObject = GetComponentInParent<NetworkObject>();
        return networkObject == null || !networkObject.IsValid || networkObject.HasInputAuthority;
    }

}
