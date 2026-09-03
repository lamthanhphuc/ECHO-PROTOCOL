using System;
using UnityEngine;
using UnityEngine.Events;

public class EnergyCoreObjectiveProgress : MonoBehaviour
{
    [SerializeField] private int requiredCoreCount = 3;
    [SerializeField] private UnityEvent progressChanged;
    [SerializeField] private UnityEvent objectiveCompleted;

    private int _placedCoreCount;

    public event Action<int, int> ProgressChanged;
    public event Action ObjectiveCompleted;

    public int RequiredCoreCount => requiredCoreCount;
    public int PlacedCoreCount => _placedCoreCount;
    public bool IsComplete => _placedCoreCount >= requiredCoreCount;

    public bool RegisterCorePlaced()
    {
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

    /// <summary>
    /// Presentation bridge for Fusion sessions. This mirrors an authoritative snapshot for existing HUD code;
    /// it never decides or increments objective progress.
    /// </summary>
    public void ApplyAuthoritativeSnapshot(int placedCoreCount, int authoritativeRequiredCoreCount)
    {
        requiredCoreCount = Mathf.Max(1, authoritativeRequiredCoreCount);
        _placedCoreCount = Mathf.Clamp(placedCoreCount, 0, requiredCoreCount);
        ProgressChanged?.Invoke(_placedCoreCount, requiredCoreCount);
    }

    public void ResetProgress()
    {
        _placedCoreCount = 0;
        progressChanged?.Invoke();
        ProgressChanged?.Invoke(_placedCoreCount, requiredCoreCount);
    }
}
