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
            if (activeController != null)
            {
                return activeController.GetPrompt(stationType);
            }

            if (stationType == PowerPuzzleStationType.PowerControl)
            {
                var flow = FindAnyObjectByType<MatchFlowController>();
                bool isComplete = flow != null && flow.IsPowerPuzzleComplete;
                bool isAuth = flow != null && flow.IsSecurityHoldComplete;
                if (!isAuth)
                {
                    var terminal = FindAnyObjectByType<SecurityTerminalDownload>();
                    if (terminal != null && terminal.IsComplete) isAuth = true;
                }

                if (isComplete) return "MAIN POWER RESTORED. POWER CONTROL ONLINE. [E]";
                if (!isAuth) return "LOCKED. SECURITY AUTHENTICATION REQUIRED. [E]";
                return "AUTHORIZATION AVAILABLE. ENTER MAIN POWER ACCESS CODE. [E]";
            }

            return fallbackPrompt;
        }
    }

    public bool CanInteract(GameObject interactor)
    {
        if (_networkAuthorityPresentationOnly) return false;

        PowerPuzzleController activeController = GetController();
        if (activeController == null)
        {
            return stationType == PowerPuzzleStationType.PowerControl;
        }

        if (stationType == PowerPuzzleStationType.PowerControl)
        {
            return true;
        }

        return !activeController.IsComplete;
    }

    public void Interact(GameObject interactor)
    {
        if (_networkAuthorityPresentationOnly) return;

        if (stationType == PowerPuzzleStationType.PowerControl)
        {
            var ui = GetComponentInChildren<PowerControlUIController>(true);
            if (ui == null)
            {
                ui = FindAnyObjectByType<PowerControlUIController>();
            }

            if (ui != null)
            {
                ui.Open(interactor);
                return;
            }
        }

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
