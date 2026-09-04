using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PowerPuzzleStation : MonoBehaviour, IInteractable
{
    [SerializeField] private PowerPuzzleController controller;
    [SerializeField] private PowerPuzzleStationType stationType;
    [SerializeField] private string fallbackPrompt = "Use power station";
    private bool _networkAuthorityPresentationOnly;

    public PowerPuzzleStationType StationType => stationType;

    public string InteractionPrompt
    {
        get
        {
            PowerPuzzleController activeController = GetController();
            return activeController != null ? activeController.GetPrompt(stationType) : fallbackPrompt;
        }
    }

    public bool CanInteract(GameObject interactor)
    {
        if (_networkAuthorityPresentationOnly) return false;

        PowerPuzzleController activeController = GetController();
        return activeController != null && !activeController.IsComplete;
    }

    public void Interact(GameObject interactor)
    {
        if (_networkAuthorityPresentationOnly) return;

        PowerPuzzleController activeController = GetController();
        if (activeController != null)
        {
            bool used = activeController.UseStation(stationType, interactor);
            if (!used && stationType == PowerPuzzleStationType.DistributionPanel)
            {
                Debug.Log("[PowerPuzzleStation] Distribution Panel needs a puzzle UI or explicit SubmitDistributionCode call.");
            }
        }
    }

    public void SetController(PowerPuzzleController puzzleController)
    {
        controller = puzzleController;
    }

    public void SetNetworkAuthorityPresentationOnly(bool enabled)
    {
        _networkAuthorityPresentationOnly = enabled;
    }

    private PowerPuzzleController GetController()
    {
        if (controller == null)
        {
            controller = FindAnyObjectByType<PowerPuzzleController>();
        }

        return controller;
    }
}
