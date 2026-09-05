using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SectorBox : MonoBehaviour, IInteractable
{
    [SerializeField] private EnergyCoreObjectiveProgress objectiveProgress;
    [SerializeField] private string placePrompt = "Nạp Energy Core vào Sector Box";
    [SerializeField] private string completePrompt = "Đã nạp đủ Energy Core";

    public string InteractionPrompt
    {
        get
        {
            if (objectiveProgress != null && objectiveProgress.IsComplete)
            {
                return string.IsNullOrWhiteSpace(completePrompt) || completePrompt == "Sector Box complete" ? "Đã nạp đủ Energy Core" : completePrompt;
            }

            return string.IsNullOrWhiteSpace(placePrompt) || placePrompt == "Place Energy Core" ? "Nạp Energy Core vào Sector Box" : placePrompt;
        }
    }

    public bool CanInteract(GameObject interactor)
    {
        PlayerEnergyCoreCarrier carrier = GetCarrier(interactor);
        return carrier != null && carrier.IsCarrying && CanAcceptCore(carrier);
    }

    public void Interact(GameObject interactor)
    {
        PlayerEnergyCoreCarrier carrier = GetCarrier(interactor);
        if (carrier != null)
        {
            carrier.PlaceCoreInSectorBox(this);
        }
    }

    public bool CanAcceptCore(PlayerEnergyCoreCarrier carrier)
    {
        return carrier != null
            && carrier.IsCarrying
            && objectiveProgress != null
            && !objectiveProgress.IsComplete;
    }

    public void AcceptPlacedCore()
    {
        if (objectiveProgress != null)
        {
            objectiveProgress.RegisterCorePlaced();
        }
    }

    private static PlayerEnergyCoreCarrier GetCarrier(GameObject interactor)
    {
        return interactor != null ? interactor.GetComponentInParent<PlayerEnergyCoreCarrier>() : null;
    }
}
