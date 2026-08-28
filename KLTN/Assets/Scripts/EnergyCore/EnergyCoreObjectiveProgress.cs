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

    public void ResetProgress()
    {
        _placedCoreCount = 0;
        progressChanged?.Invoke();
        ProgressChanged?.Invoke(_placedCoreCount, requiredCoreCount);
    }
}
