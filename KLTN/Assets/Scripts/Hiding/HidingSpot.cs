using UnityEngine;

public class HidingSpot : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform hidePoint;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private string enterPrompt = "Hide";
    [SerializeField] private string exitPrompt = "Exit hiding";

    private PlayerHidingController _occupant;

    public Transform HidePoint => hidePoint != null ? hidePoint : transform;
    public Transform ExitPoint => exitPoint;
    public bool IsOccupied => _occupant != null;
    public string InteractionPrompt => IsOccupied ? exitPrompt : enterPrompt;

    public bool CanInteract(GameObject interactor)
    {
        PlayerHidingController hidingController = GetHidingController(interactor);
        return hidingController != null && (_occupant == null || _occupant == hidingController);
    }

    public void Interact(GameObject interactor)
    {
        PlayerHidingController hidingController = GetHidingController(interactor);
        if (hidingController == null)
        {
            return;
        }

        if (_occupant == hidingController)
        {
            hidingController.ExitHiding();
            return;
        }

        hidingController.EnterHiding(this);
    }

    public bool TryOccupy(PlayerHidingController hidingController)
    {
        if (_occupant != null && _occupant != hidingController)
        {
            return false;
        }

        _occupant = hidingController;
        return true;
    }

    public void Release(PlayerHidingController hidingController)
    {
        if (_occupant == hidingController)
        {
            _occupant = null;
        }
    }

    private static PlayerHidingController GetHidingController(GameObject interactor)
    {
        return interactor != null ? interactor.GetComponentInParent<PlayerHidingController>() : null;
    }
}
