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
    [SerializeField] private Vector3 energyCoreChildLocalPosition = new Vector3(0.0012f, -0.2456f, -1.1109f);
    [SerializeField] private Vector3 energyCoreChildLocalEulerAngles = new Vector3(-89.116f, 77.236f, -92.522f);
    [SerializeField] private Vector3 energyCoreChildLocalScale = new Vector3(0.9f, 0.9f, 0.9f);
    [SerializeField] private Vector3 teamToolLocalPosition = new Vector3(0.04f, 0.01f, 0.11f);
    [SerializeField] private Vector3 teamToolLocalEulerAngles = new Vector3(12f, 88f, -18f);
    [SerializeField] private Vector3 teamToolLocalScale = new Vector3(0.45f, 0.45f, 0.45f);
    [SerializeField] private Vector3 firstAidLocalPosition = new Vector3(0.035f, -0.015f, 0.155f);
    [SerializeField] private Vector3 firstAidLocalEulerAngles = new Vector3(8f, 92f, 170f);
    [SerializeField] private Vector3 firstAidLocalScale = new Vector3(0.15f, 0.15f, 0.3f);
    [SerializeField] private Vector3 firstAidChildLocalPosition = new Vector3(-0.533528f, -3.405526f, -0.3114559f);
    [SerializeField] private Vector3 firstAidChildLocalEulerAngles = new Vector3(0.12f, -0.416f, 5.923f);
    [SerializeField] private Vector3 firstAidChildLocalScale = Vector3.one;
    [SerializeField] private Vector3 noiseMakerLocalPosition = new Vector3(0.018f, 0.132f, -0.065f);
    [SerializeField] private Vector3 noiseMakerLocalEulerAngles = new Vector3(6.176f, 93.2f, 94.562f);
    [SerializeField] private Vector3 noiseMakerLocalScale = new Vector3(0.7f, 0.7f, 0.7f);

    private GameObject _currentVisual;
    private InventoryItemDefinition _currentItem;

    public bool IsShowingTeamTool => _currentVisual != null
        && _currentItem != null
        && _currentItem.ItemType == InventoryItemType.TeamTool;

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

        Transform anchor = ResolveAnchor(_currentItem.ItemType);
        if (anchor == null)
        {
            return;
        }

        _currentVisual = Instantiate(_currentItem.WorldPrefab, anchor);
        _currentVisual.name = "Held_" + _currentItem.ItemId;
        ApplyLocalPose(_currentVisual.transform, _currentItem);
        StripWorldGameplayComponents(_currentVisual);
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

        if (IsItem(item, "noise", "beacon"))
        {
            visual.localPosition = noiseMakerLocalPosition;
            visual.localRotation = Quaternion.Euler(noiseMakerLocalEulerAngles);
            visual.localScale = noiseMakerLocalScale;
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
        foreach (Collider collider in visualRoot.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }

        foreach (Rigidbody body in visualRoot.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }

        foreach (MonoBehaviour behaviour in visualRoot.GetComponentsInChildren<MonoBehaviour>(true))
        {
            behaviour.enabled = false;
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
    }
}
