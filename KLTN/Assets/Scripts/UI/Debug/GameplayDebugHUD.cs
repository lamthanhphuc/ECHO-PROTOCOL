using UnityEngine;

public class GameplayDebugHUD : MonoBehaviour
{
    [SerializeField] private bool showDebugHud = false;
    [SerializeField] private MatchFlowController matchFlow;
    [SerializeField] private EnergyCoreObjectiveProgress coreProgress;
    [SerializeField] private PowerPuzzleController powerPuzzle;
    [SerializeField] private SecurityTerminalDownload securityTerminal;
    [SerializeField] private PlayerDownState playerDownState;
    [SerializeField] private PlayerReviveInteractable reviveInteractable;
    [SerializeField] private EscapeDoorCountdown escapeDoor;

    private readonly GUIContent _content = new GUIContent();

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.BackQuote))
        {
            showDebugHud = !showDebugHud;
        }

        if (playerDownState == null || escapeDoor == null)
        {
            ResolveReferences();
        }
    }

    private void OnGUI()
    {
        if (!showDebugHud)
        {
            return;
        }

        _content.text = BuildDebugText();
        GUI.Box(new Rect(16f, 16f, 340f, 186f), _content);
        DrawPowerPuzzleDebugControls();
    }

    private void ResolveReferences()
    {
        if (matchFlow == null)
        {
            matchFlow = FindAnyObjectByType<MatchFlowController>();
        }

        if (coreProgress == null)
        {
            coreProgress = FindAnyObjectByType<EnergyCoreObjectiveProgress>();
        }

        if (powerPuzzle == null)
        {
            powerPuzzle = FindAnyObjectByType<PowerPuzzleController>();
        }

        if (securityTerminal == null)
        {
            securityTerminal = FindAnyObjectByType<SecurityTerminalDownload>();
        }

        if (playerDownState == null)
        {
            playerDownState = FindAnyObjectByType<PlayerDownState>();
        }

        if (reviveInteractable == null)
        {
            reviveInteractable = FindAnyObjectByType<PlayerReviveInteractable>();
        }

        if (escapeDoor == null)
        {
            escapeDoor = FindAnyObjectByType<EscapeDoorCountdown>();
        }
    }

    private string BuildDebugText()
    {
        string matchText = matchFlow != null ? matchFlow.Phase.ToString() : "Missing";
        string coreText = coreProgress != null
            ? coreProgress.PlacedCoreCount + "/" + coreProgress.RequiredCoreCount
            : "Missing";

        string powerText = "Missing";
        if (powerPuzzle != null)
        {
            int currentStep = powerPuzzle.StepCount == 0 ? 0 : Mathf.Min(powerPuzzle.StepIndex + 1, powerPuzzle.StepCount);
            powerText = (powerPuzzle.IsComplete ? "Complete" : powerPuzzle.IsActive ? "Active" : "Locked")
                + " step " + currentStep + "/" + powerPuzzle.StepCount
                + " code " + (powerPuzzle.HasInstructionForCurrentStep || powerPuzzle.IsSoloFallbackActive ? powerPuzzle.CurrentCode : "???")
                + (powerPuzzle.IsLockedOut ? " lockout " + powerPuzzle.LockoutRemaining.ToString("0.0") + "s" : string.Empty);
        }

        string securityText = securityTerminal != null
            ? securityTerminal.State + " " + Mathf.RoundToInt(securityTerminal.Progress01 * 100f) + "%"
            : "Missing";

        string lifeText = playerDownState != null
            ? playerDownState.State + " bleedout " + playerDownState.BleedoutRemaining.ToString("0.0") + "s"
            : "Missing";

        string protectionText = playerDownState != null && playerDownState.HasReviveProtection
            ? playerDownState.ReviveProtectionRemaining.ToString("0.0") + "s"
            : "None";

        string reviveText = reviveInteractable != null && reviveInteractable.IsReviving
            ? Mathf.RoundToInt(reviveInteractable.ReviveProgress01 * 100f) + "%"
            : "Idle";

        string escapeText = escapeDoor != null
            ? (escapeDoor.IsComplete ? "Ready" : escapeDoor.IsCountingDown ? escapeDoor.RemainingSeconds.ToString("0.0") + "s" : "Locked/Idle")
            : "Missing";

        return "Gameplay Debug\n"
            + "Match: " + matchText + "\n"
            + "Core: " + coreText + "\n"
            + "Power: " + powerText + "\n"
            + "Security: " + securityText + "\n"
            + "Player: " + lifeText + "\n"
            + "Protection: " + protectionText + "\n"
            + "Revive: " + reviveText + "\n"
            + "Escape: " + escapeText;
    }

    private void DrawPowerPuzzleDebugControls()
    {
        if (powerPuzzle == null || !powerPuzzle.IsActive || powerPuzzle.IsComplete)
        {
            return;
        }

        const float top = 210f;
        GUI.Box(new Rect(16f, top, 340f, 70f), "Power Puzzle Input");

        if (GUI.Button(new Rect(28f, top + 28f, 70f, 28f), "A1"))
        {
            powerPuzzle.SubmitDistributionCode("A1", gameObject);
        }

        if (GUI.Button(new Rect(108f, top + 28f, 70f, 28f), "B2"))
        {
            powerPuzzle.SubmitDistributionCode("B2", gameObject);
        }

        if (GUI.Button(new Rect(188f, top + 28f, 70f, 28f), "C3"))
        {
            powerPuzzle.SubmitDistributionCode("C3", gameObject);
        }

        if (GUI.Button(new Rect(268f, top + 28f, 70f, 28f), "Fail"))
        {
            powerPuzzle.ForceFailForPenaltyTest();
        }
    }
}
