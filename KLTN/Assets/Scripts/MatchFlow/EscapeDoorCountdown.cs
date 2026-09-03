using System;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class EscapeDoorCountdown : MonoBehaviour, IInteractable
{
    [SerializeField] private MatchFlowController matchFlow;
    [SerializeField] private float countdownSeconds = 8f;
    [SerializeField] private string lockedPrompt = "Escape locked";
    [SerializeField] private string startPrompt = "Start escape countdown";
    [SerializeField] private string countingPrompt = "Escape opening";
    [SerializeField] private string completePrompt = "Escape ready";
    [SerializeField] private UnityEvent countdownStarted;
    [SerializeField] private UnityEvent countdownCompleted;

    private float _remainingSeconds;
    private bool _isCountingDown;
    private bool _isComplete;
    private bool _networkAuthorityPresentationOnly;

    public event Action<EscapeDoorCountdown> CountdownStarted;
    public event Action<EscapeDoorCountdown> CountdownCompleted;
    public event Action<EscapeDoorCountdown, float> CountdownChanged;

    public bool IsCountingDown => _isCountingDown;
    public bool IsComplete => _isComplete;
    public float RemainingSeconds => _remainingSeconds;

    public string InteractionPrompt
    {
        get
        {
            if (_isComplete)
            {
                return completePrompt;
            }

            if (_isCountingDown)
            {
                return countingPrompt + " (" + Mathf.CeilToInt(_remainingSeconds) + "s)";
            }

            return CanStartCountdown() ? startPrompt : lockedPrompt;
        }
    }

    private void Awake()
    {
        if (matchFlow == null)
        {
            matchFlow = FindAnyObjectByType<MatchFlowController>();
        }

        _remainingSeconds = countdownSeconds;
    }

    private void Update()
    {
        if (_networkAuthorityPresentationOnly) return;

        if (!_isCountingDown)
        {
            return;
        }

        _remainingSeconds = Mathf.Max(0f, _remainingSeconds - Time.deltaTime);
        CountdownChanged?.Invoke(this, _remainingSeconds);

        if (_remainingSeconds <= 0f)
        {
            CompleteCountdown();
        }
    }

    public bool CanInteract(GameObject interactor)
    {
        if (_networkAuthorityPresentationOnly) return false;
        return !_isComplete && !_isCountingDown;
    }

    public void Interact(GameObject interactor)
    {
        if (_networkAuthorityPresentationOnly) return;
        StartCountdown();
    }

    public bool StartCountdown()
    {
        if (_networkAuthorityPresentationOnly) return false;

        if (_isComplete || _isCountingDown || !CanStartCountdown())
        {
            return false;
        }

        _remainingSeconds = countdownSeconds;
        _isCountingDown = true;
        countdownStarted?.Invoke();
        CountdownStarted?.Invoke(this);
        return true;
    }

    public void ResetCountdown()
    {
        if (_networkAuthorityPresentationOnly) return;

        _remainingSeconds = countdownSeconds;
        _isCountingDown = false;
        _isComplete = false;
    }

    public void SetNetworkAuthorityPresentationOnly(bool enabled)
    {
        _networkAuthorityPresentationOnly = enabled;
    }

    public void ApplyAuthoritativeSnapshot(
        bool escapeEnabled,
        bool isCountingDown,
        bool isComplete,
        float remainingSeconds)
    {
        var wasCountingDown = _isCountingDown;
        var wasComplete = _isComplete;
        _isCountingDown = escapeEnabled && isCountingDown;
        _isComplete = isComplete;
        _remainingSeconds = Mathf.Max(0f, remainingSeconds);
        CountdownChanged?.Invoke(this, _remainingSeconds);
        if (!wasCountingDown && _isCountingDown)
        {
            countdownStarted?.Invoke();
            CountdownStarted?.Invoke(this);
        }
        if (!wasComplete && _isComplete)
        {
            countdownCompleted?.Invoke();
            CountdownCompleted?.Invoke(this);
        }
    }

    private bool CanStartCountdown()
    {
        if (_networkAuthorityPresentationOnly) return false;
        return matchFlow == null || matchFlow.Phase == MatchPhase.FinalHunt || matchFlow.Phase == MatchPhase.ExitCountdown;
    }

    private void CompleteCountdown()
    {
        if (_isComplete)
        {
            return;
        }

        _isCountingDown = false;
        _isComplete = true;
        countdownCompleted?.Invoke();
        CountdownCompleted?.Invoke(this);
    }
}
