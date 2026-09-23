using System;
using UnityEngine;
using UnityEngine.Events;

public class PowerPuzzleController : MonoBehaviour
{
    [Header("Activation")]
    [SerializeField] private EnergyCoreObjectiveProgress coreProgress;
    [SerializeField] private bool activateWhenCoresComplete = true;
    [SerializeField] private bool startsActive;

    [Header("Sequence")]
    [SerializeField] private string[] sequence = { "A1", "C3", "B2" };
    [SerializeField] private bool requirePowerControlRead = true;

    [Header("Solo Fallback")]
    [SerializeField] private int activePlayerCount = 1;
    [SerializeField] private bool enableSoloFallback = true;

    [Header("Fail Penalty")]
    [SerializeField] private int maxFailuresBeforeReset = 3;
    [SerializeField] private float failLockoutSeconds = 4f;

    [Header("Events")]
    [SerializeField] private UnityEvent puzzleActivated;
    [SerializeField] private UnityEvent stepAdvanced;
    [SerializeField] private UnityEvent puzzleFailed;
    [SerializeField] private UnityEvent puzzleCompleted;

    private int _stepIndex;
    private int _failureCount;
    private bool _isActive;
    private bool _isComplete;
    private bool _instructionReadForStep;
    private float _lockoutUntil;
    private int _authoritativeStepCount = -1;
    private bool _authoritativeLockout;
    private bool _networkAuthorityPresentationOnly;

    public event Action<PowerPuzzleController> PuzzleActivated;
    public event Action<PowerPuzzleController> StepAdvanced;
    public event Action<PowerPuzzleController> PuzzleFailed;
    public event Action<PowerPuzzleController> PuzzleCompleted;

    public bool IsActive => _isActive;
    public bool IsComplete => _isComplete;
    public int StepIndex => _stepIndex;
    public int StepCount => _authoritativeStepCount >= 0
        ? _authoritativeStepCount
        : sequence != null ? sequence.Length : 0;
    public int FailureCount => _failureCount;
    public bool IsSoloFallbackActive => enableSoloFallback && activePlayerCount <= 1;
    public bool IsLockedOut => _authoritativeLockout || Time.time < _lockoutUntil;
    public bool HasInstructionForCurrentStep => _instructionReadForStep;
    public float LockoutRemaining => Mathf.Max(0f, _lockoutUntil - Time.time);

    private string _authoritativeCode;

    public bool IsSecurityHoldComplete
    {
        get
        {
            var matchState = FindAnyObjectByType<EchoProtocol.Networking.NetworkMatchState>();
            if (matchState != null && matchState.SecurityHoldCompleted) return true;

            var flow = FindAnyObjectByType<MatchFlowController>();
            if (flow != null && flow.IsSecurityHoldComplete) return true;

            var terminal = FindAnyObjectByType<SecurityTerminalDownload>();
            if (terminal != null && terminal.IsComplete) return true;

            return false;
        }
    }

    public string AuthoritativeCode
    {
        get
        {
            var matchState = FindAnyObjectByType<EchoProtocol.Networking.NetworkMatchState>();
            if (matchState != null && !string.IsNullOrEmpty(matchState.PowerAuthorizationCode.ToString()))
            {
                return matchState.PowerAuthorizationCode.ToString();
            }

            var flow = FindAnyObjectByType<MatchFlowController>();
            if (flow != null && !string.IsNullOrEmpty(flow.PowerAuthorizationCode))
            {
                return flow.PowerAuthorizationCode;
            }

            return _authoritativeCode;
        }
        set
        {
            _authoritativeCode = value;
        }
    }

    public string CurrentCode
    {
        get
        {
            if (_networkAuthorityPresentationOnly)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(AuthoritativeCode))
            {
                return AuthoritativeCode;
            }

            if (sequence == null || sequence.Length == 0 || _stepIndex >= sequence.Length)
            {
                return string.Empty;
            }

            return sequence[_stepIndex];
        }
    }

    private void Awake()
    {
        if (coreProgress == null)
        {
            coreProgress = GetComponent<EnergyCoreObjectiveProgress>();
        }

        _isActive = startsActive || (coreProgress != null && coreProgress.IsComplete && activateWhenCoresComplete);
    }

    private void OnEnable()
    {
        if (coreProgress != null)
        {
            coreProgress.ObjectiveCompleted += ActivatePuzzle;
        }
    }

    private void OnDisable()
    {
        if (coreProgress != null)
        {
            coreProgress.ObjectiveCompleted -= ActivatePuzzle;
        }
    }

    public string GetPrompt(PowerPuzzleStationType stationType)
    {
        if (_isComplete)
        {
            return "MAIN POWER RESTORED. POWER CONTROL ONLINE. [E]";
        }

        if (!IsSecurityHoldComplete && stationType == PowerPuzzleStationType.PowerControl)
        {
            return "LOCKED. SECURITY AUTHENTICATION REQUIRED. [E]";
        }

        if (!_isActive && !IsSecurityHoldComplete)
        {
            return "Power puzzle locked";
        }

        if (IsLockedOut)
        {
            return "Power system cooling down (" + Mathf.CeilToInt(LockoutRemaining) + "s)";
        }

        if (stationType == PowerPuzzleStationType.PowerControl)
        {
            return "AUTHORIZATION AVAILABLE. ENTER MAIN POWER ACCESS CODE. [E]";
        }

        if (requirePowerControlRead && !_instructionReadForStep && !IsSoloFallbackActive)
        {
            return "Needs Power Control code";
        }

        return "Input distribution code";
    }

    public bool CanUseStation(PowerPuzzleStationType stationType)
    {
        if (_networkAuthorityPresentationOnly || _isComplete || IsLockedOut)
        {
            return false;
        }

        if (stationType == PowerPuzzleStationType.PowerControl)
        {
            return true;
        }

        if (!IsSecurityHoldComplete)
        {
            return false;
        }

        return !requirePowerControlRead
            || _instructionReadForStep
            || IsSoloFallbackActive;
    }

    public bool UseStation(PowerPuzzleStationType stationType, GameObject interactor)
    {
        if (!CanUseStation(stationType))
        {
            return false;
        }

        if (stationType == PowerPuzzleStationType.PowerControl)
        {
            var ui = GetComponentInChildren<PowerControlUIController>(true);
            if (ui == null && interactor != null)
            {
                ui = FindAnyObjectByType<PowerControlUIController>();
            }

            if (ui != null)
            {
                ui.Open(interactor);
                return true;
            }

            ReadCurrentInstruction();
            return true;
        }

        return false;
    }

    public bool SubmitAuthorizationCode(string code, GameObject interactor)
    {
        if (_isComplete) return false;
        if (!IsSecurityHoldComplete) return false;
        if (IsLockedOut) return false;

        string expected = AuthoritativeCode;
        if (string.IsNullOrEmpty(expected))
        {
            expected = CurrentCode;
        }

        if (string.Equals(code, expected, StringComparison.OrdinalIgnoreCase))
        {
            _failureCount = 0;
            CompletePuzzle();

            var matchState = FindAnyObjectByType<EchoProtocol.Networking.NetworkMatchState>();
            if (matchState != null && matchState.Object != null && matchState.Object.HasStateAuthority)
            {
                matchState.TrySubmitPowerCode(Fusion.PlayerRef.None, code);
            }

            var flow = FindAnyObjectByType<MatchFlowController>();
            if (flow != null)
            {
                flow.NotifyPowerPuzzleComplete();
            }

            return true;
        }
        else
        {
            FailPuzzle();
            return false;
        }
    }

    public bool SubmitCurrentDistributionCode(GameObject interactor)
    {
        return SubmitDistributionCode(CurrentCode, interactor);
    }

    public bool SubmitDistributionCode(string submittedCode, GameObject interactor)
    {
        if (!CanUseStation(PowerPuzzleStationType.DistributionPanel))
        {
            return false;
        }

        string expected = CurrentCode;
        if (!string.Equals(submittedCode, expected, StringComparison.OrdinalIgnoreCase))
        {
            FailPuzzle();
            return false;
        }

        AdvanceStep();
        return true;
    }

    public void ActivatePuzzle()
    {
        if (_networkAuthorityPresentationOnly) return;

        if (_isActive || _isComplete)
        {
            return;
        }

        _isActive = true;
        _instructionReadForStep = false;
        puzzleActivated?.Invoke();
        PuzzleActivated?.Invoke(this);
    }

    public void SetActivePlayerCount(int count)
    {
        activePlayerCount = Mathf.Max(1, count);
    }

    public void ResetPuzzle()
    {
        if (_networkAuthorityPresentationOnly) return;

        _stepIndex = 0;
        _failureCount = 0;
        _isComplete = false;
        _instructionReadForStep = false;
        _lockoutUntil = 0f;
        _isActive = startsActive || (coreProgress != null && coreProgress.IsComplete && activateWhenCoresComplete);
        _authoritativeStepCount = -1;
        _authoritativeLockout = false;
    }

    /// <summary>
    /// Presentation-only bridge for multiplayer. It deliberately does not invoke legacy gameplay events;
    /// the Fusion State Authority owns phase transitions and completion side effects.
    /// </summary>
    public void ApplyAuthoritativeSnapshot(
        bool isActive,
        bool isComplete,
        int stepIndex,
        int stepCount,
        int failureCount,
        bool isLockedOut)
    {
        _isActive = isActive;
        _isComplete = isComplete;
        _stepIndex = Mathf.Clamp(stepIndex, 0, Mathf.Max(0, stepCount));
        _failureCount = Mathf.Max(0, failureCount);
        _instructionReadForStep = false;
        _lockoutUntil = 0f;
        _authoritativeStepCount = Mathf.Max(0, stepCount);
        _authoritativeLockout = isLockedOut;
    }

    public void SetNetworkAuthorityPresentationOnly(bool enabled)
    {
        _networkAuthorityPresentationOnly = enabled;
    }

    private void ReadCurrentInstruction()
    {
        _instructionReadForStep = true;
        Debug.Log("[PowerPuzzle] Current routing code: " + CurrentCode + " (" + (_stepIndex + 1) + "/" + StepCount + ")");
    }

    public void ForceFailForPenaltyTest()
    {
        if (_networkAuthorityPresentationOnly) return;

        if (_isActive && !_isComplete)
        {
            FailPuzzle();
        }
    }

    private void AdvanceStep()
    {
        if (sequence == null || sequence.Length == 0)
        {
            CompletePuzzle();
            return;
        }

        _stepIndex++;
        _instructionReadForStep = false;

        if (_stepIndex >= sequence.Length)
        {
            CompletePuzzle();
            return;
        }

        stepAdvanced?.Invoke();
        StepAdvanced?.Invoke(this);
    }

    private void FailPuzzle()
    {
        _failureCount++;
        _instructionReadForStep = false;
        _lockoutUntil = Time.time + failLockoutSeconds;

        if (maxFailuresBeforeReset > 0 && _failureCount >= maxFailuresBeforeReset)
        {
            _stepIndex = 0;
            _failureCount = 0;
        }

        puzzleFailed?.Invoke();
        PuzzleFailed?.Invoke(this);
        Debug.LogWarning("[PowerPuzzle] Puzzle input failed. System lockout started.");
    }

    private void CompletePuzzle()
    {
        _isComplete = true;
        _isActive = false;
        _instructionReadForStep = false;
        puzzleCompleted?.Invoke();
        PuzzleCompleted?.Invoke(this);
        Debug.Log("[PowerPuzzle] Power restored.");
    }
}
