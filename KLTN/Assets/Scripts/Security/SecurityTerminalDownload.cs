using System;
using System.Reflection;
using EchoProtocol.Networking;
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

    private string _fallbackAuthCode;

    public float DownloadDurationSeconds => Mathf.Max(0.01f, downloadDurationSeconds);
    public float MaxInteractorDistance => Mathf.Max(0.1f, maxInteractorDistance);
    public SecurityDownloadState State
    {
        get
        {
            if (!TryGetNetworkMatchState(out var matchState)) return _state;
            if (matchState.SecurityHoldCompleted) return SecurityDownloadState.Completed;
            if (matchState.IsSecurityHoldRunning) return SecurityDownloadState.Downloading;
            return matchState.SecurityHoldProgress01 > 0f ? SecurityDownloadState.Paused : SecurityDownloadState.Idle;
        }
    }
    public bool IsDownloading => State == SecurityDownloadState.Downloading;
    public bool IsPaused => State == SecurityDownloadState.Paused;
    public bool IsComplete => State == SecurityDownloadState.Completed;
    public bool RequiresHold => requireHoldToDownload && !IsComplete && EmergencyNetworkState.AreRelaysOnline();
    public float Progress01 => TryGetNetworkMatchState(out var matchState)
        ? matchState.SecurityHoldProgress01
        : Mathf.Clamp01(_progressSeconds / DownloadDurationSeconds);
    public GameObject ActiveInteractor => _activeInteractor;

    public string AuthorizationCode
    {
        get
        {
            if (TryGetNetworkMatchState(out var matchState))
            {
                // Multiplayer uses only the State Authority-owned value.
                // Empty means authorization is not available yet.
                return matchState.PowerAuthorizationCode.ToString();
            }

            var flow = UnityEngine.Object.FindAnyObjectByType<MatchFlowController>();
            if (flow != null && !string.IsNullOrEmpty(flow.PowerAuthorizationCode))
            {
                return flow.PowerAuthorizationCode;
            }

            return _fallbackAuthCode;
        }
    }

    public string InteractionPrompt
    {
        get
        {
            if (IsComplete)
            {
                string code = AuthorizationCode;
                return string.IsNullOrEmpty(code)
                    ? completePrompt
                    : $"XÁC THỰC BẢO MẬT HOÀN TẤT [MÃ: {code}] - NHẤN [E] ĐỂ XEM";
            }

            if (!EmergencyNetworkState.AreRelaysOnline())
            {
                int online = 0;
                if (EchoProtocol.MatchFlow.Zone2MissionDirector.Instance != null)
                {
                    online = EchoProtocol.MatchFlow.Zone2MissionDirector.Instance.CompletedRelayCount;
                    if (EchoProtocol.MatchFlow.Zone2MissionDirector.Instance.CurrentStage == EchoProtocol.MatchFlow.Zone2MissionStage.FindSecurityTerminal)
                    {
                        return "SECURITY TERMINAL [E] - KIỂM TRA HỆ THỐNG BẢO MẬT";
                    }
                }
                return $"MẠNG BẢO MẬT OFFLINE [E] - TIẾN ĐỘ RELAY: {online}/4";
            }

            string percent = " (" + Mathf.RoundToInt(Progress01 * 100f) + "%)";
            if (IsDownloading)
            {
                return (string.IsNullOrWhiteSpace(downloadingPrompt) ? "Đang Tải Dữ Liệu" : downloadingPrompt) + percent;
            }

            if (IsPaused && Progress01 > 0f)
            {
                return (string.IsNullOrWhiteSpace(resumePrompt) ? "Giữ [E] để Tiếp tục Tải Dữ Liệu" : resumePrompt) + percent;
            }

            string prompt = string.IsNullOrWhiteSpace(startPrompt) || startPrompt == "Download Access Code" || startPrompt == "Giữ để Tải Dữ Liệu"
                ? "Giữ [E] để Bắt đầu Security Hold"
                : startPrompt;
            return prompt;
        }
    }

    private void Update()
    {
        if (TryGetNetworkMatchState(out _)) return;

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

        if (!EmergencyNetworkState.AreRelaysOnline())
        {
            return true;
        }

        if (TryGetNetworkMatchState(out _))
        {
            return true;
        }

        return !IsDownloading || _activeInteractor == interactor;
    }

    public void Interact(GameObject interactor)
    {
        // First terminal interaction: discovers terminal, does NOT start security hold
        if (EchoProtocol.MatchFlow.Zone2MissionDirector.Instance != null &&
            EchoProtocol.MatchFlow.Zone2MissionDirector.Instance.CurrentStage == EchoProtocol.MatchFlow.Zone2MissionStage.FindSecurityTerminal)
        {
            EchoProtocol.MatchFlow.Zone2MissionDirector.Instance.DiscoverSecurityTerminal();
            OpenUI(interactor);
            return;
        }

        if (IsComplete || !EmergencyNetworkState.AreRelaysOnline())
        {
            OpenUI(interactor);
            return;
        }

        if (requireHoldToDownload)
        {
            BeginHoldInteract(interactor);
            return;
        }

        if (interactor == null || !CanInteract(interactor))
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
        if (interactor == null || IsComplete)
        {
            return;
        }

        if (TryGetNetworkMatchState(out _))
        {
            EchoProtocol.MatchFlow.Zone2MissionDirector.Instance?.RequestStartSecurityHold(this);
            return;
        }

        if (!CanInteract(interactor)) return;

        StartOrResumeDownload(interactor);
    }

    public void EndHoldInteract(GameObject interactor)
    {
        InterruptDownload(interactor);
    }

    public void InterruptDownload(GameObject interactor)
    {
        if (TryGetNetworkMatchState(out _))
        {
            EchoProtocol.MatchFlow.Zone2MissionDirector.Instance?.RequestCancelSecurityHold(this);
            return;
        }

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
        if (TryGetNetworkMatchState(out _)) return;

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
        if (TryGetNetworkMatchState(out _)) return;

        _progressSeconds = Mathf.Max(0.01f, downloadDurationSeconds);
        _state = SecurityDownloadState.Completed;
        var interactor = _activeInteractor;
        _activeInteractor = null;

        if (string.IsNullOrEmpty(_fallbackAuthCode))
        {
            _fallbackAuthCode = UnityEngine.Random.Range(0, 10000).ToString("D4");
        }

        // Notify local MatchFlowController if active
        var flow = UnityEngine.Object.FindAnyObjectByType<MatchFlowController>();
        if (flow != null)
        {
            flow.NotifySecurityHoldComplete();
        }

        if (EchoProtocol.MatchFlow.Zone2MissionDirector.Instance != null)
        {
            EchoProtocol.MatchFlow.Zone2MissionDirector.Instance.OnSecurityHoldCompleted(this);
        }

        downloadCompleted?.Invoke();
        DownloadCompleted?.Invoke(this);
        ProgressChanged?.Invoke(this, Progress01);

        if (interactor != null)
        {
            OpenUI(interactor);
        }
    }

    public void OpenUI(GameObject interactor)
    {
        var ui = GetComponentInChildren<SecurityTerminalUIController>(true);
        if (ui != null)
        {
            ui.Open(interactor);
        }
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

    private static bool TryGetNetworkMatchState(out NetworkMatchState matchState)
    {
        matchState = NetworkMatchState.Instance ?? UnityEngine.Object.FindAnyObjectByType<NetworkMatchState>();
        return matchState != null && matchState.Object != null && matchState.Object.IsValid;
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
