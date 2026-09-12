using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerHeldItemView : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerEnergyCoreCarrier coreCarrier;
    [SerializeField] private PlayerHeldItemAnchor heldItemAnchor;
    [SerializeField] private Vector3 energyCoreLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 energyCoreLocalEulerAngles = Vector3.zero;
    [SerializeField] private Vector3 energyCoreLocalScale = new Vector3(25f, 25f, 25f);
    [SerializeField] private Vector3 energyCoreChildLocalPosition = new Vector3(0.0012f, 0.02f, -0.05f);
    [SerializeField] private Vector3 energyCoreChildLocalEulerAngles = new Vector3(-89.116f, 77.236f, -92.522f);
    [SerializeField] private Vector3 energyCoreChildLocalScale = new Vector3(0.9f, 0.9f, 0.9f);
    [SerializeField] private Vector3 teamToolLocalPosition = new Vector3(0.04f, 0.18f, 0.11f);
    [SerializeField] private Vector3 teamToolLocalEulerAngles = new Vector3(12f, 88f, -18f);
    [SerializeField] private Vector3 teamToolLocalScale = new Vector3(0.45f, 0.45f, 0.45f);
    [SerializeField] private Vector3 fieldScannerLocalPosition = new Vector3(-0.003f, 0.291f, 0.118f);
    [SerializeField] private Vector3 fieldScannerLocalEulerAngles = new Vector3(184.192f, 99.672f, -5.550995f);
    [SerializeField] private Vector3 fieldScannerLocalScale = new Vector3(3f, 3f, 3f);
    [SerializeField] private Vector3 fieldScannerChildLocalPosition = new Vector3(0f, 0.04f, 0f);
    [SerializeField] private Vector3 fieldScannerChildLocalEulerAngles = Vector3.zero;
    [SerializeField] private Vector3 fieldScannerChildLocalScale = Vector3.one;
    [SerializeField] private GameObject fieldScannerHeldPrefab;
    [SerializeField] private Vector3 firstAidLocalPosition = new Vector3(0.035f, 0.16f, 0.14f);
    [SerializeField] private Vector3 firstAidLocalEulerAngles = new Vector3(8f, 92f, 170f);
    [SerializeField] private Vector3 firstAidLocalScale = new Vector3(0.15f, 0.15f, 0.3f);
    [SerializeField] private Vector3 firstAidChildLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 firstAidChildLocalEulerAngles = Vector3.zero;
    [SerializeField] private Vector3 firstAidChildLocalScale = Vector3.one;
    [SerializeField] private Vector3 noiseMakerLocalPosition = new Vector3(0.018f, 0.16f, 0.02f);
    [SerializeField] private Vector3 noiseMakerLocalEulerAngles = new Vector3(6.176f, 93.2f, 94.562f);
    [SerializeField] private Vector3 noiseMakerLocalScale = new Vector3(0.7f, 0.7f, 0.7f);
    [SerializeField] private Vector3 plankLocalPosition = new Vector3(0.022f, 0.172f, 0.034f);
    [SerializeField] private Vector3 plankLocalEulerAngles = new Vector3(90f, 0f, 90f);
    [SerializeField] private Vector3 plankLocalScale = new Vector3(5f, 5f, 5f);
    [SerializeField] private Vector3 plankChildLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 plankChildLocalEulerAngles = Vector3.zero;
    [SerializeField] private Vector3 plankChildLocalScale = Vector3.one;
    [Header("Core Stabilizer Transform")]
    [SerializeField] private Vector3 coreStabilizerLocalPosition = new Vector3(0.035f, 0.18f, 0.12f);
    [SerializeField] private Vector3 coreStabilizerLocalEulerAngles = new Vector3(10f, 90f, -15f);
    [SerializeField] private Vector3 coreStabilizerLocalScale = new Vector3(0.45f, 0.45f, 0.45f);

    private GameObject _currentVisual;
    private InventoryItemDefinition _currentItem;

    public bool IsShowingTeamTool => _currentVisual != null
        && _currentItem != null
        && _currentItem.ItemType == InventoryItemType.TeamTool;

    public GameObject FieldScannerHeldPrefab => fieldScannerHeldPrefab;

    private void Awake()
    {
        if (inventory == null) inventory = GetComponentInParent<PlayerInventory>();
        if (coreCarrier == null) coreCarrier = GetComponentInParent<PlayerEnergyCoreCarrier>();
        if (heldItemAnchor == null) heldItemAnchor = GetComponentInParent<PlayerHeldItemAnchor>();
        if (heldItemAnchor == null) heldItemAnchor = gameObject.AddComponent<PlayerHeldItemAnchor>();
    }

    private void OnEnable()
    {
        if (inventory != null) inventory.InventoryChanged += RefreshVisual;
        if (coreCarrier != null) coreCarrier.CarryStateChanged += HandleCarryStateChanged;
        RefreshVisual();
    }

    private void OnDisable()
    {
        if (inventory != null) inventory.InventoryChanged -= RefreshVisual;
        if (coreCarrier != null) coreCarrier.CarryStateChanged -= HandleCarryStateChanged;
    }

    private void HandleCarryStateChanged(PlayerEnergyCoreCarrier carrier)
    {
        RefreshVisual();
    }

    private void RefreshVisual()
    {
        InventoryItemDefinition desiredItem = ResolveDesiredHeldItem();
        if (desiredItem == _currentItem)
        {
            return;
        }

        ClearVisual();
        _currentItem = desiredItem;

        if (_currentItem == null || _currentItem.WorldPrefab == null)
        {
            return;
        }

        // Trong phiên multiplayer Fusion, với remote players: Energy Core là NetworkObject
        // tự bám vào PlayerHeldItemAnchor.ResolveCoreCarryAnchor trên thế giới.
        // Với local player (HasInputAuthority): cần spawn visual first-person riêng.
        if (_currentItem.ItemType == InventoryItemType.EnergyCore && IsActiveFusionSession())
        {
            var netObj = GetComponentInParent<Fusion.NetworkObject>();
            bool isLocalPlayer = netObj != null && netObj.HasInputAuthority;
            if (!isLocalPlayer)
            {
                return; // remote player — NetworkPickupItem handles world rendering
            }
        }

        // Complete Team Tool prefabs own both their held visual and gameplay lifecycle.
        // PlayerTeamToolController renders these so the generic held-item view must not
        // instantiate the pickup prefab a second time.
        // If PlayerTeamToolController is present, it renders them; otherwise PlayerHeldItemView renders them.
        if (_currentItem.ItemType == InventoryItemType.TeamTool
            && _currentItem.TeamToolGameplayPrefab != null
            && GetComponentInParent<PlayerTeamToolController>() != null)
        {
            return;
        }

        Transform anchor = ResolveAnchor(_currentItem.ItemType);
        if (anchor == null)
        {
            return;
        }

        GameObject prefabToSpawn = _currentItem.WorldPrefab;
        if (_currentItem.TeamToolGameplayPrefab != null)
        {
            prefabToSpawn = _currentItem.TeamToolGameplayPrefab;
        }

        if (IsItem(_currentItem, "scan", "fieldscanner", "scanner"))
        {
            if (fieldScannerHeldPrefab != null)
            {
                prefabToSpawn = fieldScannerHeldPrefab;
            }
            else
            {
                var loaded = Resources.Load<GameObject>("PF_FieldScanner")
                    ?? Resources.Load<GameObject>("Prefabs/Tools/PF_FieldScanner");
                if (loaded != null)
                {
                    prefabToSpawn = loaded;
                }
            }
        }

        if (IsItem(_currentItem, "plank", "jammer"))
        {
#if UNITY_EDITOR
            var visualPlank = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Imported/PF_Plank_HeldVisual.prefab");
            if (visualPlank != null) prefabToSpawn = visualPlank;
#endif
            if (prefabToSpawn == null || prefabToSpawn.GetComponentInChildren<Fusion.NetworkObject>(true) != null)
            {
                prefabToSpawn = Resources.Load<GameObject>("PF_Plank_HeldVisual")
                    ?? Resources.Load<GameObject>("Prefabs/Gameplay/Imported/PF_Plank_HeldVisual")
                    ?? _currentItem.TeamToolGameplayPrefab;
            }
        }

        if (prefabToSpawn == null)
        {
            return;
        }

        _currentVisual = NetworkTeamToolHeldView.InstantiateHeldVisualSafely(prefabToSpawn, anchor);
        if (_currentVisual == null) return;
        _currentVisual.name = "Held_" + _currentItem.ItemId;
        ApplyLocalPose(_currentVisual.transform, _currentItem);
        StripWorldGameplayComponents(_currentVisual);

        bool isHoldingScanner = IsItem(_currentItem, "scan", "fieldscanner", "scanner");
        EchoProtocol.UI.HUD.HUDFieldScanner.EnsureInstance()?.SetVisible(isHoldingScanner);
    }

    private InventoryItemDefinition ResolveDesiredHeldItem()
    {
        if (coreCarrier != null && coreCarrier.IsCarrying && coreCarrier.CarriedCoreItem != null)
        {
            return coreCarrier.CarriedCoreItem;
        }

        return inventory != null ? inventory.TeamToolSlot : null;
    }

    private void ApplyLocalPose(Transform visual, InventoryItemDefinition item)
    {
        Vector3 baseScale = item != null && item.WorldPrefab != null
            ? item.WorldPrefab.transform.localScale
            : Vector3.one;

        if (item != null && item.ItemType == InventoryItemType.EnergyCore)
        {
            visual.localPosition = energyCoreLocalPosition;
            visual.localRotation = Quaternion.Euler(energyCoreLocalEulerAngles);
            visual.localScale = energyCoreLocalScale;

            Transform childVisual = visual.Find("Visual");
            if (childVisual == null && visual.childCount > 0)
            {
                childVisual = visual.GetChild(0);
            }
            if (childVisual != null)
            {
                childVisual.localPosition = energyCoreChildLocalPosition;
                childVisual.localRotation = Quaternion.Euler(energyCoreChildLocalEulerAngles);
                childVisual.localScale = energyCoreChildLocalScale;
            }
            return;
        }

        if (IsItem(item, "first_aid", "first aid"))
        {
            visual.localPosition = firstAidLocalPosition;
            visual.localRotation = Quaternion.Euler(firstAidLocalEulerAngles);
            visual.localScale = firstAidLocalScale;

            Transform childVisual = visual.Find("Visual");
            if (childVisual == null && visual.childCount > 0)
            {
                childVisual = visual.GetChild(0);
            }
            if (childVisual != null)
            {
                childVisual.localPosition = firstAidChildLocalPosition;
                childVisual.localRotation = Quaternion.Euler(firstAidChildLocalEulerAngles);
                childVisual.localScale = firstAidChildLocalScale;
            }
            return;
        }

        if (IsItem(item, "scan", "fieldscanner", "scanner"))
        {
            visual.localPosition = fieldScannerLocalPosition;
            visual.localRotation = Quaternion.Euler(fieldScannerLocalEulerAngles);
            visual.localScale = fieldScannerLocalScale;

            Transform childVisual = visual.Find("Visual");
            if (childVisual == null && visual.childCount > 0)
            {
                childVisual = visual.GetChild(0);
            }
            if (childVisual != null)
            {
                childVisual.localPosition = fieldScannerChildLocalPosition;
                childVisual.localRotation = Quaternion.Euler(fieldScannerChildLocalEulerAngles);
                childVisual.localScale = fieldScannerChildLocalScale;
            }
            return;
        }

        if (IsItem(item, "noise", "beacon"))
        {
            visual.localPosition = noiseMakerLocalPosition;
            visual.localRotation = Quaternion.Euler(noiseMakerLocalEulerAngles);
            visual.localScale = noiseMakerLocalScale;
            return;
        }

        if (IsItem(item, "plank", "jammer"))
        {
            visual.localPosition = plankLocalPosition;
            visual.localRotation = Quaternion.Euler(plankLocalEulerAngles);
            visual.localScale = plankLocalScale;
            return;
        }

        if (IsItem(item, "stabilizer", "core_stabilizer"))
        {
            visual.localPosition = coreStabilizerLocalPosition;
            visual.localRotation = Quaternion.Euler(coreStabilizerLocalEulerAngles);
            visual.localScale = coreStabilizerLocalScale;
            return;
        }

        visual.localPosition = teamToolLocalPosition;
        visual.localRotation = Quaternion.Euler(teamToolLocalEulerAngles);
        visual.localScale = baseScale;
    }

    private static bool IsItem(InventoryItemDefinition item, params string[] tokens)
    {
        if (item == null || tokens == null)
        {
            return false;
        }

        string itemId = item.ItemId.ToLowerInvariant();
        string displayName = item.DisplayName.ToLowerInvariant();
        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (!string.IsNullOrEmpty(token) && (itemId.Contains(token) || displayName.Contains(token)))
            {
                return true;
            }
        }

        return false;
    }

    private Transform ResolveAnchor(InventoryItemType itemType)
    {
        if (heldItemAnchor == null)
        {
            heldItemAnchor = GetComponentInParent<PlayerHeldItemAnchor>();
        }

        if (heldItemAnchor == null)
        {
            heldItemAnchor = gameObject.AddComponent<PlayerHeldItemAnchor>();
        }

        return itemType == InventoryItemType.EnergyCore
            ? heldItemAnchor.CoreCarryAnchor
            : heldItemAnchor.RightHandAnchor;
    }

    private void StripWorldGameplayComponents(GameObject visualRoot)
    {
        if (visualRoot == null) return;

        foreach (Collider collider in visualRoot.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }

        foreach (Rigidbody body in visualRoot.GetComponentsInChildren<Rigidbody>(true))
        {
            body.detectCollisions = false;
            body.isKinematic = true;
        }

        foreach (var netObj in visualRoot.GetComponentsInChildren<Fusion.NetworkObject>(true))
        {
            netObj.enabled = false;
        }

        foreach (var netBehaviour in visualRoot.GetComponentsInChildren<Fusion.NetworkBehaviour>(true))
        {
            netBehaviour.enabled = false;
        }

        foreach (MonoBehaviour behaviour in visualRoot.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour is EchoProtocol.Tools.Scanner.FieldScannerScreenView ||
                behaviour is EchoProtocol.Tools.Scanner.FieldScannerAudio ||
                behaviour is TMPro.TMP_Text ||
                behaviour is UnityEngine.UI.Graphic ||
                behaviour is UnityEngine.UI.CanvasScaler)
            {
                behaviour.enabled = true;
                continue;
            }

            if (behaviour is EchoProtocol.Tools.Scanner.NetworkToolPickup ||
                behaviour.GetType().Name.Contains("Pickup") ||
                behaviour.GetType().Name.Contains("Interactable"))
            {
                behaviour.enabled = false;
            }
        }
    }

    private void ClearVisual()
    {
        if (_currentVisual != null)
        {
            Destroy(_currentVisual);
        }

        _currentVisual = null;
        _currentItem = null;
        EchoProtocol.UI.HUD.HUDFieldScanner.Instance?.SetVisible(false);
    }

    private bool IsActiveFusionSession()
    {
        var netObj = GetComponentInParent<Fusion.NetworkObject>();
        return netObj != null
            && netObj.IsValid
            && netObj.Runner != null
            && netObj.Runner.IsRunning;
    }
}
