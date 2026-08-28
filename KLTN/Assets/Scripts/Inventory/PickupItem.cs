using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PickupItem : MonoBehaviour, IInteractable
{
    [SerializeField] private InventoryItemDefinition item;
    [SerializeField] private string promptOverride;
    [SerializeField] private bool destroyOnPickup = true;

    public string InteractionPrompt
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(promptOverride))
            {
                return promptOverride;
            }

            return item != null ? "Pick up " + item.DisplayName : "Pick up";
        }
    }

    public bool CanInteract(GameObject interactor)
    {
        PlayerInventory inventory = interactor != null ? interactor.GetComponentInParent<PlayerInventory>() : null;
        return item != null
            && item.ItemType != InventoryItemType.EnergyCore
            && inventory != null
            && inventory.CanAdd(item);
    }

    public void Interact(GameObject interactor)
    {
        PlayerInventory inventory = interactor != null ? interactor.GetComponentInParent<PlayerInventory>() : null;
        if (inventory == null || !inventory.TryAdd(item))
        {
            return;
        }

        if (destroyOnPickup)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
