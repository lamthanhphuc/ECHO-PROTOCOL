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

    public event Action<PowerPuzzleController> PuzzleActivated;
    public event Action<PowerPuzzleController> StepAdvanced;
    public event Action<PowerPuzzleController> PuzzleFailed;
    public event Action<PowerPuzzleController> PuzzleCompleted;

    public bool IsActive => _isActive;
    public bool IsComplete => _isComplete;
    public int StepIndex => _stepIndex;
    public int StepCount => sequence != null ? sequence.Length : 0;
    public int FailureCount => _failureCount;
    public bool IsSoloFallbackActive => enableSoloFallback && activePlayerCount <= 1;
    public bool IsLockedOut => Time.time < _lockoutUntil;
    public bool HasInstructionForCurrentStep => _instructionReadForStep;
    public float LockoutRemaining => Mathf.Max(0f, _lockoutUntil - Time.time);

    public string CurrentCode
    {
        get
        {
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
            return "Power restored";
        }

        if (!_isActive)
        {
            return "Power puzzle locked";
        }

        if (IsLockedOut)
        {
            return "Power system cooling down";
        }

        if (stationType == PowerPuzzleStationType.PowerControl)
        {
            return IsSoloFallbackActive ? "Read solo power code" : "Read power routing code";
        }

        if (requirePowerControlRead && !_instructionReadForStep && !IsSoloFallbackActive)
        {
            return "Needs Power Control code";
        }

        return "Input distribution code";
    }

    public bool CanUseStation(PowerPuzzleStationType stationType)
    {
        if (!_isActive || _isComplete || IsLockedOut)
        {
            return false;
        }

        return stationType == PowerPuzzleStationType.PowerControl
            || !requirePowerControlRead
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
            ReadCurrentInstruction();
            return true;
        }

        return false;
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

        if (!string.Equals(submittedCode, CurrentCode, StringComparison.OrdinalIgnoreCase))
        {
            FailPuzzle();
            return false;
        }

        AdvanceStep();
        return true;
    }

    public void ActivatePuzzle()
    {
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
        _stepIndex = 0;
        _failureCount = 0;
        _isComplete = false;
        _instructionReadForStep = false;
        _lockoutUntil = 0f;
        _isActive = startsActive || (coreProgress != null && coreProgress.IsComplete && activateWhenCoresComplete);
    }

    private void ReadCurrentInstruction()
    {
        _instructionReadForStep = true;
        Debug.Log("[PowerPuzzle] Current routing code: " + CurrentCode + " (" + (_stepIndex + 1) + "/" + StepCount + ")");
    }

    public void ForceFailForPenaltyTest()
    {
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
