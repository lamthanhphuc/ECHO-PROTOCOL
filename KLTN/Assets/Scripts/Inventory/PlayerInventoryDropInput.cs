using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInventoryDropInput : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private Transform dropOrigin;
    [SerializeField] private float dropForwardDistance = 1.25f;
    [SerializeField] private int selectedNormalSlot;

    public int SelectedNormalSlot => selectedNormalSlot;

    private void Awake()
    {
        if (inventory == null)
        {
            inventory = GetComponent<PlayerInventory>();
        }

        if (dropOrigin == null && Camera.main != null)
        {
            dropOrigin = Camera.main.transform;
        }

    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || inventory == null)
        {
            return;
        }

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
            DropSelectedNormalSlot();
        }

        if (keyboard.tKey.wasPressedThisFrame)
        {
            DropTeamTool();
        }
    }

    public bool DropSelectedNormalSlot()
    {
        return inventory != null && inventory.TryDropNormalSlot(selectedNormalSlot, GetDropPosition(), GetDropRotation());
    }

    public bool DropTeamTool()
    {
        return inventory != null && inventory.TryDropTeamTool(GetDropPosition(), GetDropRotation());
    }

    private Vector3 GetDropPosition()
    {
        Transform origin = dropOrigin != null ? dropOrigin : transform;
        return origin.position + origin.forward * dropForwardDistance;
    }

    private Quaternion GetDropRotation()
    {
        Transform origin = dropOrigin != null ? dropOrigin : transform;
        return Quaternion.LookRotation(origin.forward, Vector3.up);
    }
}
