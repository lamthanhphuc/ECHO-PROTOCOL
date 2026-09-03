using System;
using UnityEngine;
using UnityEngine.Events;

public class EnergyCoreObjectiveProgress : MonoBehaviour
{
    [SerializeField] private int requiredCoreCount = 3;
    [SerializeField] private UnityEvent progressChanged;
    [SerializeField] private UnityEvent objectiveCompleted;

    private int _placedCoreCount;
    private bool _networkAuthorityPresentationOnly;

    public event Action<int, int> ProgressChanged;
    public event Action ObjectiveCompleted;

    public int RequiredCoreCount => requiredCoreCount;
    public int PlacedCoreCount => _placedCoreCount;
    public bool IsComplete => _placedCoreCount >= requiredCoreCount;

    public bool RegisterCorePlaced()
    {
        if (_networkAuthorityPresentationOnly) return false;

        if (IsComplete)
        {
            return false;
        }

        _placedCoreCount = Mathf.Clamp(_placedCoreCount + 1, 0, requiredCoreCount);
        progressChanged?.Invoke();
        ProgressChanged?.Invoke(_placedCoreCount, requiredCoreCount);

        if (IsComplete)
        {
            objectiveCompleted?.Invoke();
            ObjectiveCompleted?.Invoke();
        }

        return true;
    }

    public void ResetProgress()
    {
        if (_networkAuthorityPresentationOnly) return;

        _placedCoreCount = 0;
        progressChanged?.Invoke();
        ProgressChanged?.Invoke(_placedCoreCount, requiredCoreCount);
    }

    public void SetNetworkAuthorityPresentationOnly(bool enabled)
    {
        _networkAuthorityPresentationOnly = enabled;
    }

    public void ApplyAuthoritativeSnapshot(int placedCoreCount, int authoritativeRequiredCount)
    {
        var wasComplete = IsComplete;
        requiredCoreCount = Mathf.Max(1, authoritativeRequiredCount);
        _placedCoreCount = Mathf.Clamp(placedCoreCount, 0, requiredCoreCount);
        progressChanged?.Invoke();
        ProgressChanged?.Invoke(_placedCoreCount, requiredCoreCount);
        if (!wasComplete && IsComplete)
        {
            objectiveCompleted?.Invoke();
            ObjectiveCompleted?.Invoke();
        }
    }
}
