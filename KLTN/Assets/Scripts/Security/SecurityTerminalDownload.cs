using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class SecurityTerminalDownload : MonoBehaviour, IHoldInteractable
{
    [Header("Download")]
    [SerializeField] private float downloadDurationSeconds = 12f;
    [SerializeField] private float maxInteractorDistance = 3f;
    [SerializeField] private bool pauseWhenInteractorLooksAway = true;
    [SerializeField] private bool requireHoldToDownload = true;
    [SerializeField] private bool detectDownedPlayerByReflection = true;

    [Header("Prompt")]
    [SerializeField] private string startPrompt = "Giữ để Tải Dữ Liệu";
    [SerializeField] private string resumePrompt = "Giữ để Tiếp tục Tải Dữ Liệu";
    [SerializeField] private string downloadingPrompt = "Đang Tải Dữ Liệu";
    [SerializeField] private string completePrompt = "Đã Tải Xong Dữ Liệu";

    [Header("Events")]
    [SerializeField] private UnityEvent downloadStarted;
    [SerializeField] private UnityEvent downloadPaused;
    [SerializeField] private UnityEvent downloadResumed;
    [SerializeField] private UnityEvent downloadCompleted;

    private GameObject _activeInteractor;
    private float _progressSeconds;
    private SecurityDownloadState _state;

    public event Action<SecurityTerminalDownload> DownloadStarted;
    public event Action<SecurityTerminalDownload> DownloadPaused;
    public event Action<SecurityTerminalDownload> DownloadResumed;
    public event Action<SecurityTerminalDownload> DownloadCompleted;
    public event Action<SecurityTerminalDownload, float> ProgressChanged;

    public SecurityDownloadState State => _state;
    public bool IsDownloading => _state == SecurityDownloadState.Downloading;
    public bool IsPaused => _state == SecurityDownloadState.Paused;
    public bool IsComplete => _state == SecurityDownloadState.Completed;
    public bool RequiresHold => requireHoldToDownload;
    public float Progress01 => downloadDurationSeconds <= 0f ? 1f : Mathf.Clamp01(_progressSeconds / downloadDurationSeconds);
    public GameObject ActiveInteractor => _activeInteractor;

    public string InteractionPrompt
    {
        get
        {
            if (IsComplete)
            {
                return completePrompt;
            }

            string percent = " (" + Mathf.RoundToInt(Progress01 * 100f) + "%)";
            if (IsDownloading)
            {
                return downloadingPrompt + percent;
            }

            if (IsPaused && Progress01 > 0f)
            {
                return resumePrompt + percent;
            }

            string prompt = string.IsNullOrWhiteSpace(startPrompt) || startPrompt == "Download Access Code" ? "Giữ để Tải Dữ Liệu" : startPrompt;
            return prompt;
        }
    }

    private void Update()
    {
        if (!IsDownloading)
        {
            return;
        }

        if (!IsInteractorStillValid())
        {
            PauseDownload();
            return;
        }

        float previousProgress = Progress01;
        _progressSeconds = Mathf.Min(_progressSeconds + Time.deltaTime, Mathf.Max(0.01f, downloadDurationSeconds));

        if (!Mathf.Approximately(previousProgress, Progress01))
        {
            ProgressChanged?.Invoke(this, Progress01);
        }

        if (Progress01 >= 1f)
        {
            CompleteDownload();
        }
    }

    public bool CanInteract(GameObject interactor)
    {
        if (IsComplete)
        {
            return true;
        }

        return !IsDownloading || _activeInteractor == interactor;
    }

    public void Interact(GameObject interactor)
    {
        if (requireHoldToDownload)
        {
            BeginHoldInteract(interactor);
            return;
        }

        if (interactor == null || IsComplete || !CanInteract(interactor))
        {
            return;
        }

        if (IsDownloading)
        {
            return;
        }

        StartOrResumeDownload(interactor);
    }

    public void BeginHoldInteract(GameObject interactor)
    {
        if (interactor == null || IsComplete || !CanInteract(interactor))
        {
            return;
        }

        StartOrResumeDownload(interactor);
    }

    public void EndHoldInteract(GameObject interactor)
    {
        InterruptDownload(interactor);
    }

    public void InterruptDownload(GameObject interactor)
    {
        if (!IsDownloading)
        {
            return;
        }

        if (interactor == null || _activeInteractor == interactor)
        {
            PauseDownload();
        }
    }

    public void ResetDownload()
    {
        _activeInteractor = null;
        _progressSeconds = 0f;
        _state = SecurityDownloadState.Idle;
        ProgressChanged?.Invoke(this, Progress01);
    }

    private void StartOrResumeDownload(GameObject interactor)
    {
        bool resume = IsPaused && _progressSeconds > 0f;
        _activeInteractor = interactor;
        _state = SecurityDownloadState.Downloading;

        if (resume)
        {
            downloadResumed?.Invoke();
            DownloadResumed?.Invoke(this);
        }
        else
        {
            downloadStarted?.Invoke();
            DownloadStarted?.Invoke(this);
        }
    }

    private void PauseDownload()
    {
        if (!IsDownloading)
        {
            return;
        }

        _state = SecurityDownloadState.Paused;
        _activeInteractor = null;
        downloadPaused?.Invoke();
        DownloadPaused?.Invoke(this);
        ProgressChanged?.Invoke(this, Progress01);
    }

    private void CompleteDownload()
    {
        _progressSeconds = Mathf.Max(0.01f, downloadDurationSeconds);
        _state = SecurityDownloadState.Completed;
        _activeInteractor = null;
        downloadCompleted?.Invoke();
        DownloadCompleted?.Invoke(this);
        ProgressChanged?.Invoke(this, Progress01);
    }

    private bool IsInteractorStillValid()
    {
        if (_activeInteractor == null || !_activeInteractor.activeInHierarchy)
        {
            return false;
        }

        if (IsInteractorDowned(_activeInteractor))
        {
            return false;
        }

        float distance = Vector3.Distance(transform.position, _activeInteractor.transform.position);
        if (distance > maxInteractorDistance)
        {
            return false;
        }

        if (!pauseWhenInteractorLooksAway)
        {
            return true;
        }

        PlayerInteraction interaction = _activeInteractor.GetComponentInParent<PlayerInteraction>();
        return interaction == null || ReferenceEquals(interaction.CurrentInteractable, this);
    }

    private bool IsInteractorDowned(GameObject interactor)
    {
        if (!detectDownedPlayerByReflection)
        {
            return false;
        }

        Component[] components = interactor.GetComponentsInParent<Component>(true);
        foreach (Component component in components)
        {
            if (component == null)
            {
                continue;
            }

            if (TryReadBoolMember(component, "IsDowned", out bool isDowned)
                || TryReadBoolMember(component, "IsDown", out isDowned)
                || TryReadBoolMember(component, "IsDead", out isDowned))
            {
                if (isDowned)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryReadBoolMember(Component component, string memberName, out bool value)
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Type type = component.GetType();

        PropertyInfo property = type.GetProperty(memberName, Flags);
        if (property != null && property.PropertyType == typeof(bool))
        {
            value = (bool)property.GetValue(component);
            return true;
        }

        FieldInfo field = type.GetField(memberName, Flags);
        if (field != null && field.FieldType == typeof(bool))
        {
            value = (bool)field.GetValue(component);
            return true;
        }

        value = false;
        return false;
    }
}
