using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInventory))]
public sealed class PlayerTeamToolController : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private Transform handSocket;
    [SerializeField] private Transform aimOrigin;
    [SerializeField] private Key fallbackUseKey = Key.Q;
    [SerializeField] private Vector3 heldLocalPosition = new Vector3(0.04f, 0.01f, 0.11f);
    [SerializeField] private Vector3 heldLocalEulerAngles = new Vector3(12f, 88f, -18f);

    private InventoryItemDefinition equippedDefinition;
    private GameObject equippedObject;
    private ITeamToolGameplay equippedTool;

    private void Awake()
    {
        if (inventory == null) inventory = GetComponent<PlayerInventory>();
        if (aimOrigin == null && Camera.main != null) aimOrigin = Camera.main.transform;
        PlayerHeldItemAnchor heldItemAnchor = GetComponent<PlayerHeldItemAnchor>();
        if (heldItemAnchor != null) handSocket = heldItemAnchor.RightHandAnchor;
        if (handSocket == null) handSocket = transform;
    }

    private void OnEnable()
    {
        if (inventory != null) inventory.InventoryChanged += RefreshEquippedTool;
        RefreshEquippedTool();
    }

    private void OnDisable()
    {
        if (inventory != null) inventory.InventoryChanged -= RefreshEquippedTool;
        ClearEquippedTool();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[fallbackUseKey].wasPressedThisFrame)
        {
            UseEquippedTool();
        }
    }

    public bool UseEquippedTool()
    {
        return inventory != null
            && !inventory.IsTeamToolLocked
            && equippedTool != null
            && equippedTool.TryUse();
    }

    public void RefreshEquippedTool()
    {
        InventoryItemDefinition definition = inventory != null ? inventory.TeamToolSlot : null;
        if (definition == equippedDefinition && equippedObject != null) return;
        ClearEquippedTool();

        if (definition == null || definition.TeamToolGameplayPrefab == null) return;
        equippedDefinition = definition;
        equippedObject = Instantiate(definition.TeamToolGameplayPrefab, handSocket, false);
        equippedObject.name = definition.TeamToolGameplayPrefab.name;
        equippedObject.transform.localPosition = heldLocalPosition;
        equippedObject.transform.localRotation = Quaternion.Euler(heldLocalEulerAngles);
        equippedTool = FindGameplay(equippedObject);
        equippedTool?.Equip(gameObject, aimOrigin != null ? aimOrigin : transform);
    }

    private void ClearEquippedTool()
    {
        equippedTool?.Unequip();
        if (equippedObject != null) Destroy(equippedObject);
        equippedObject = null;
        equippedTool = null;
        equippedDefinition = null;
    }

    private static ITeamToolGameplay FindGameplay(GameObject root)
    {
        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is ITeamToolGameplay gameplay) return gameplay;
        }
        return null;
    }
}
