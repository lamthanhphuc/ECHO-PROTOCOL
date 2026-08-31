using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SectorBox : MonoBehaviour, IInteractable
{
    [SerializeField] private EnergyCoreObjectiveProgress objectiveProgress;
    [SerializeField] private string placePrompt = "Place Energy Core";
    [SerializeField] private string completePrompt = "Sector Box complete";

    public string InteractionPrompt
    {
        get
        {
            if (objectiveProgress != null && objectiveProgress.IsComplete)
            {
                return completePrompt;
            }

            return objectiveProgress != null
                ? placePrompt + " (" + objectiveProgress.PlacedCoreCount + "/" + objectiveProgress.RequiredCoreCount + ")"
                : placePrompt;
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
