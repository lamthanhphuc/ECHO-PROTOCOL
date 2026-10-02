using EchoProtocol.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInventory))]
public sealed class PlayerTeamToolController : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private Transform handSocket;
    [SerializeField] private Transform aimOrigin;
    [SerializeField] private NetworkPlayerMovement networkMovement;
    [SerializeField] private Animator animator;
    [SerializeField] private Key fallbackUseKey = Key.Q;
    [SerializeField] private Vector3 heldLocalPosition = new Vector3(0.04f, 0.01f, 0.11f);
    [SerializeField] private Vector3 heldLocalEulerAngles = new Vector3(12f, 88f, -18f);

    private InventoryItemDefinition equippedDefinition;
    private GameObject equippedObject;
    private ITeamToolGameplay equippedTool;
    private bool _wasPushing;
    private static readonly int IsPushingHash = Animator.StringToHash("IsPushing");

    private void Awake()
    {
        if (inventory == null) inventory = GetComponent<PlayerInventory>();
        if (aimOrigin == null && Camera.main != null) aimOrigin = Camera.main.transform;
        if (networkMovement == null) networkMovement = GetComponent<NetworkPlayerMovement>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
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
        bool pushing = IsPushing();
        if (pushing != _wasPushing)
        {
            _wasPushing = pushing;
            ApplyPushVisibility();
        }

        if (PlayerInteractionControlLock.IsGameplayInputBlocked(gameObject)) return;
        if (!pushing && Keyboard.current != null && Keyboard.current[fallbackUseKey].wasPressedThisFrame)
        {
            UseEquippedTool();
        }
    }

    public bool UseEquippedTool()
    {
        return inventory != null
            && !inventory.IsTeamToolLocked
            && !IsPushing()
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
        PlayerHeldItemAnchor anchor = GetComponent<PlayerHeldItemAnchor>();
        Transform socket = definition.ItemId == "core_stabilizer" && anchor != null
            ? anchor.CoreCarryAnchor
            : handSocket;
        equippedObject = Instantiate(definition.TeamToolGameplayPrefab, socket, false);
        equippedObject.name = definition.TeamToolGameplayPrefab.name;
        equippedObject.transform.localPosition = definition.ItemId == "core_stabilizer" ? Vector3.zero : heldLocalPosition;
        equippedObject.transform.localRotation = Quaternion.Euler(definition.ItemId == "core_stabilizer" ? new Vector3(0f, 90f, 0f) : heldLocalEulerAngles);
        equippedTool = FindGameplay(equippedObject);
        equippedTool?.Equip(gameObject, aimOrigin != null ? aimOrigin : transform);
        ApplyPushVisibility();
    }

    private void ClearEquippedTool()
    {
        equippedTool?.Unequip();
        if (equippedObject != null) Destroy(equippedObject);
        equippedObject = null;
        equippedTool = null;
        equippedDefinition = null;
    }

    private void ApplyPushVisibility()
    {
        if (equippedObject != null)
        {
            equippedObject.SetActive(!IsPushing());
        }
    }

    private bool IsPushing()
    {
        if (networkMovement == null) networkMovement = GetComponent<NetworkPlayerMovement>();
        if (networkMovement != null && networkMovement.IsAnimationPushing)
        {
            return true;
        }

        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        return animator != null
            && animator.isActiveAndEnabled
            && animator.runtimeAnimatorController != null
            && animator.GetBool(IsPushingHash);
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
