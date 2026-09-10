using UnityEngine;

[CreateAssetMenu(menuName = "ECHO Protocol/Inventory/Item Definition")]
public class InventoryItemDefinition : ScriptableObject
{
    [SerializeField] private string itemId;
    [SerializeField] private string displayName;
    [SerializeField] private InventoryItemType itemType;
    [SerializeField] private GameObject worldPrefab;
    [Tooltip("Runtime prefab instantiated while this Team Tool is equipped.")]
    [SerializeField] private GameObject teamToolGameplayPrefab;
    [SerializeField] private Sprite icon;

    public string ItemId => string.IsNullOrWhiteSpace(itemId) ? name : itemId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public InventoryItemType ItemType => itemType;
    public GameObject WorldPrefab => worldPrefab;
    public GameObject TeamToolGameplayPrefab => teamToolGameplayPrefab;
    public Sprite Icon => icon;
}
