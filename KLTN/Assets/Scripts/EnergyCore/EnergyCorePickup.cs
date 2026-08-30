using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnergyCorePickup : MonoBehaviour, IInteractable
{
    [SerializeField] private InventoryItemDefinition coreItem;
    [SerializeField] private string coreId = "EnergyCore";
    [SerializeField] private string pickupPrompt = "Pick up Energy Core";

    public InventoryItemDefinition CoreItem => coreItem;
    public string CoreId => coreId;
    public string InteractionPrompt => pickupPrompt;

    public bool CanInteract(GameObject interactor)
    {
        PlayerEnergyCoreCarrier carrier = GetCarrier(interactor);
        return carrier != null && carrier.CanPickupCore(this);
    }

    public void Interact(GameObject interactor)
    {
        PlayerEnergyCoreCarrier carrier = GetCarrier(interactor);
        if (carrier != null)
        {
            carrier.TryPickupCore(this);
        }
    }

    public void SetCarried(bool carried)
    {
        gameObject.SetActive(!carried);
    }

    public void PlaceInWorld(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
        gameObject.SetActive(true);
    }

    private static PlayerEnergyCoreCarrier GetCarrier(GameObject interactor)
    {
        return interactor != null ? interactor.GetComponentInParent<PlayerEnergyCoreCarrier>() : null;
    }
}
