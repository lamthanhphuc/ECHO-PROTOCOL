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

        if (IsActiveFusionGameplay())
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

        bool throwPressed = Mouse.current != null
            && Mouse.current.leftButton.wasPressedThisFrame;

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
        if (inventory != null && inventory.TeamToolSlot != null)
        {
            int toolId = PlayerInventory.ResolveToolId(inventory.TeamToolSlot);
            if (toolId == 1) // Field Scanner
            {
                rot = Quaternion.Euler(90f, rot.eulerAngles.y, 0f);
            }
        }
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

        if (toolId.Contains("scan") || toolName.Contains("scan"))
        {
            var scanner = GetComponentInParent<EchoProtocol.Tools.Scanner.NetworkFieldScanner>();
            if (scanner != null)
            {
                return scanner.RequestScan();
            }
        }

        if (toolId.Contains("plank") || toolName.Contains("plank") || toolId.Contains("jammer") || toolName.Contains("jammer"))
        {
            var cam = Camera.main;
            Ray ray = cam != null
                ? cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
                : new Ray(transform.position + Vector3.up * 1.5f, transform.forward);

            if (Physics.Raycast(ray, out var hit, 4.0f, ~0, QueryTriggerInteraction.Collide))
            {
                var door = hit.collider.GetComponentInParent<EchoProtocol.Networking.NetworkSlidingDoor>();
                if (door != null && door.CanAcceptJammer())
                {
                    return door.DeployJammerOffline(gameObject);
                }
            }

            var colliders = Physics.OverlapSphere(transform.position + transform.forward * 1.5f, 2.5f, ~0, QueryTriggerInteraction.Collide);
            foreach (var col in colliders)
            {
                var door = col.GetComponentInParent<EchoProtocol.Networking.NetworkSlidingDoor>();
                if (door != null && door.CanAcceptJammer())
                {
                    return door.DeployJammerOffline(gameObject);
                }
            }
        }

        if (toolId.Contains("stabilizer") || toolName.Contains("stabilizer") || toolId.Contains("core") || toolName.Contains("core"))
        {
            EchoProtocol.Audio.GameAudioRuntime.AtPoint("energy_core/pickup", transform.position);
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

    private bool IsActiveFusionGameplay()
    {
        var networkObject = GetComponentInParent<NetworkObject>();
        var lobbyState = GetComponentInParent<EchoProtocol.Networking.LobbyPlayerState>();
        return networkObject != null
            && networkObject.IsValid
            && networkObject.Runner != null
            && networkObject.Runner.IsRunning
            && lobbyState != null
            && lobbyState.IsGameplayPlayer;
    }

}
