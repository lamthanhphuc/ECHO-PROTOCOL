using System;
using EchoProtocol.MatchFlow;
using EchoProtocol.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace EchoProtocol.RelayB
{
    [DisallowMultipleComponent]
    public sealed class RelayBUIController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Header")]
        [SerializeField] private TMP_Text facilityLabel;
        [SerializeField] private TMP_Text relayLabel;
        [SerializeField] private TMP_Text modeLabel;
        [SerializeField] private TMP_Text statusLabel;

        [Header("Waveform Oscilloscope")]
        [SerializeField] private RelayBWaveformRenderer referenceWaveformRenderer;
        [SerializeField] private RelayBWaveformRenderer currentWaveformRenderer;
        [SerializeField] private TMP_Text referenceSignalLabel;
        [SerializeField] private TMP_Text currentSignalLabel;

        [Header("Channel Selection")]
        [SerializeField] private Button[] channelButtons = new Button[4];
        [SerializeField] private Image[] channelHighlights = new Image[4];

        [Header("Controls")]
        [SerializeField] private Slider frequencySlider;
        [SerializeField] private TMP_Text frequencyValueText;
        [SerializeField] private Slider phaseSlider;
        [SerializeField] private TMP_Text phaseValueText;

        [Header("Synchronization Monitor")]
        [SerializeField] private TMP_Text signalMatchText;
        [SerializeField] private TMP_Text frequencyErrorText;
        [SerializeField] private TMP_Text phaseErrorText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Image progressFill;
        [SerializeField] private TMP_Text warningBannerText;

        [Header("Action Buttons")]
        [SerializeField] private Button scanButton;
        [SerializeField] private Button startSyncButton;
        [SerializeField] private Button cancelSyncButton;
        [SerializeField] private Button closeButton;

        [Header("System Log")]
        [SerializeField] private TMP_Text systemLogText;

        [Header("Colors")]
        [SerializeField] private Color safeColor = new Color(0.35f, 1f, 0.58f, 1f);
        [SerializeField] private Color warningColor = new Color(1f, 0.78f, 0.18f, 1f);
        [SerializeField] private Color dangerColor = new Color(1f, 0.2f, 0.14f, 1f);
        [SerializeField] private Color offlineColor = new Color(0.55f, 0.65f, 0.7f, 1f);
        [SerializeField] private Color referenceColor = new Color(0.2f, 0.9f, 1f, 1f);

        private readonly RelayBPlayerControlLock _controlLock = new RelayBPlayerControlLock();
        private readonly System.Collections.Generic.List<string> _logEntries = new System.Collections.Generic.List<string>();
        private RelayBController _controller;
        private bool _suppressSliderEvents;
        private RelayBStatus _lastStatus = (RelayBStatus)(-1);
        private int _lastLoggedChannel = -1;

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        private void Awake()
        {
            EnsureProgressFillSprite();
            HookControls();
            SetVisible(false);
            AddLog("SYSTEM INITIALIZED. WAITING FOR OPERATOR.");
        }

        private void EnsureProgressFillSprite()
        {
            if (progressFill != null)
            {
                progressFill.type = Image.Type.Filled;
                progressFill.fillMethod = Image.FillMethod.Horizontal;
                progressFill.fillOrigin = 0;
                if (progressFill.sprite == null)
                {
                    progressFill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
                }
            }
        }

        public void AddLog(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            string entry = $"> {message}";
            _logEntries.Add(entry);
            if (_logEntries.Count > 3)
            {
                _logEntries.RemoveAt(0);
            }

            if (systemLogText != null)
            {
                systemLogText.text = string.Join("\n", _logEntries);
            }
        }

        private void OnDestroy()
        {
            _controlLock.Release();
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (_controlLock.ShouldAutoRelease())
            {
                Close();
                return;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        public void Bind(RelayBController controller)
        {
            _controller = controller;
            HookControls();
        }

        public void Open(GameObject interactor)
        {
            EnsureEventSystem();
            Zone2MinigameUIFocus.CloseOthers(this);
            if (TryGetNetworkDirector(out var director))
            {
                if (!director.CanLocalPlayerOperateRelay(_controller)
                    || !director.RequestRelayAcquire(_controller))
                {
                    return;
                }
            }

            _controlLock.Acquire(interactor);
            SetVisible(true);
        }

        public void Close()
        {
            if (TryGetNetworkDirector(out var director)) director.RequestRelayRelease(_controller);
            SetVisible(false);
            _controlLock.Release();
        }

        public void Refresh(RelayBSnapshot snapshot)
        {
            bool readOnly = snapshot.IsOnline;
            bool canOperate = !TryGetNetworkDirector(out var director) || director.CanLocalPlayerOperateRelay(_controller);
            if (IsOpen && !readOnly && !canOperate)
            {
                Close();
                return;
            }

            _suppressSliderEvents = true;

            if (frequencySlider != null)
            {
                float minFreq = _controller != null && _controller.Config != null ? _controller.Config.MinFrequency : 10f;
                float maxFreq = _controller != null && _controller.Config != null ? _controller.Config.MaxFrequency : 100f;
                frequencySlider.minValue = minFreq;
                frequencySlider.maxValue = maxFreq;
                frequencySlider.SetValueWithoutNotify(snapshot.CurrentFrequency);
            }

            if (phaseSlider != null)
            {
                phaseSlider.minValue = 0f;
                phaseSlider.maxValue = 360f;
                phaseSlider.SetValueWithoutNotify(snapshot.CurrentPhase);
            }

            _suppressSliderEvents = false;

            // Header
            SetText(facilityLabel, "ECHO FACILITY");
            SetText(relayLabel, snapshot.IsOnline ? "DATA RELAY B – ONLINE" : "DATA RELAY B");
            SetText(modeLabel, snapshot.IsOnline ? "SECURITY NETWORK CONNECTED" : "SIGNAL SYNCHRONIZATION");
            SetText(statusLabel, StatusToDisplayString(snapshot.Status));
            if (statusLabel != null)
            {
                statusLabel.color = StatusToColor(snapshot.Status);
            }

            // Slider readouts
            SetText(frequencyValueText, $"{snapshot.CurrentFrequency:0.0} kHz");
            SetText(phaseValueText, $"{snapshot.CurrentPhase:0}°");

            // Waveform visualizers
            if (referenceWaveformRenderer != null)
            {
                referenceWaveformRenderer.SetWaveParameters(
                    snapshot.ReferenceWaveform,
                    snapshot.TargetFrequency,
                    snapshot.TargetPhase,
                    1f);
                referenceWaveformRenderer.SetWaveformColor(referenceColor);
            }

            if (currentWaveformRenderer != null)
            {
                currentWaveformRenderer.SetWaveParameters(
                    snapshot.CurrentWaveform,
                    snapshot.CurrentFrequency,
                    snapshot.CurrentPhase,
                    1f);

                Color curWaveColor = snapshot.IsOnline || snapshot.IsSynchronized
                    ? safeColor
                    : (snapshot.SelectedChannelIndex >= 0 ? warningColor : offlineColor);
                currentWaveformRenderer.SetWaveformColor(curWaveColor);

                // Add slight noise if signal mismatch or scanning
                float noise = snapshot.IsScanning ? 0.65f : (snapshot.IsSynchronized ? 0f : 0.18f);
                currentWaveformRenderer.SetNoise(noise);
            }

            SetText(referenceSignalLabel, $"REF: {snapshot.ReferenceWaveform.ToString().ToUpper()} | {snapshot.TargetFrequency:0.0} kHz | {snapshot.TargetPhase:0}°");
            SetText(currentSignalLabel, snapshot.SelectedChannelIndex >= 0
                ? $"CUR: {snapshot.CurrentWaveform.ToString().ToUpper()} | {snapshot.CurrentFrequency:0.0} kHz | {snapshot.CurrentPhase:0}°"
                : "NO CHANNEL SELECTED");

            // Channel highlight indicators
            for (int i = 0; i < channelHighlights.Length; i++)
            {
                if (channelHighlights[i] != null)
                {
                    bool isSelected = (i == snapshot.SelectedChannelIndex);
                    channelHighlights[i].color = isSelected ? safeColor : new Color(0.1f, 0.2f, 0.25f, 0.8f);
                }
            }

            // Synchronization Monitor
            if (snapshot.IsSynchronized && !snapshot.IsOnline)
            {
                SetText(signalMatchText, $"SIGNAL MATCH: {snapshot.SignalMatchPercent:0.0}% [CONDITIONS SATISFIED]");
            }
            else
            {
                SetText(signalMatchText, $"SIGNAL MATCH: {snapshot.SignalMatchPercent:0.0}%");
            }

            if (signalMatchText != null)
            {
                signalMatchText.color = snapshot.SignalMatchPercent >= 95f ? safeColor : (snapshot.SignalMatchPercent >= 50f ? warningColor : dangerColor);
            }

            float freqTol = _controller != null && _controller.Config != null ? _controller.Config.FrequencyTolerancePercent : 3f;
            bool freqPass = snapshot.FrequencyErrorPercent <= freqTol;
            SetText(frequencyErrorText, $"FREQ ERROR: {snapshot.FrequencyErrorPercent:0.00}% ({(freqPass ? "PASS" : "OUT OF BAND")})");
            if (frequencyErrorText != null)
            {
                frequencyErrorText.color = freqPass ? safeColor : warningColor;
            }

            float phaseTol = _controller != null && _controller.Config != null ? _controller.Config.PhaseToleranceDegrees : 12f;
            bool phasePass = snapshot.PhaseErrorDegrees <= phaseTol;
            SetText(phaseErrorText, $"PHASE ERROR: {snapshot.PhaseErrorDegrees:0.0}° ({(phasePass ? "PASS" : "DESYNC")})");
            if (phaseErrorText != null)
            {
                phaseErrorText.color = phasePass ? safeColor : warningColor;
            }

            SetText(progressText, $"SYNCHRONIZATION PROGRESS\nProgress: {snapshot.SyncProgressSeconds:0.0} / {snapshot.HoldRequiredSeconds:0} seconds");
            if (progressFill != null)
            {
                EnsureProgressFillSprite();
                progressFill.fillAmount = snapshot.Progress01;
                progressFill.color = snapshot.IsOnline || snapshot.IsSynchronized ? safeColor : (snapshot.Status == RelayBStatus.DriftWarning ? warningColor : dangerColor);
            }

            // Warnings & Drift Banner
            if (warningBannerText != null)
            {
                if (snapshot.IsOnline)
                {
                    warningBannerText.text = $"ACTIVE LINK: CHANNEL {snapshot.SelectedChannelIndex + 1:00} | CHANNEL LOCKED";
                    warningBannerText.color = safeColor;
                    warningBannerText.gameObject.SetActive(true);
                }
                else if (snapshot.IsDriftWarning)
                {
                    warningBannerText.text = "CAUTION: IONOSPHERIC DRIFT IMMINENT (3s)";
                    warningBannerText.color = new Color(1f, 0.55f, 0.1f, 1f);
                    warningBannerText.gameObject.SetActive(true);
                }
                else if (snapshot.IsDriftActive && !snapshot.IsSynchronized)
                {
                    warningBannerText.text = "DRIFT ACTIVE: COMPENSATE PHASE / FREQUENCY";
                    warningBannerText.color = dangerColor;
                    warningBannerText.gameObject.SetActive(true);
                }
                else if (snapshot.Status == RelayBStatus.ConnectionLost)
                {
                    warningBannerText.text = "CONNECTION LOST: SIGNAL DESYNCHRONIZED";
                    warningBannerText.color = dangerColor;
                    warningBannerText.gameObject.SetActive(true);
                }
                else if (snapshot.IsSynchronized && snapshot.Status != RelayBStatus.Synchronizing)
                {
                    warningBannerText.text = "SYNC CONDITIONS SATISFIED - PRESS START SYNC";
                    warningBannerText.color = safeColor;
                    warningBannerText.gameObject.SetActive(true);
                }
                else
                {
                    warningBannerText.text = string.Empty;
                    warningBannerText.gameObject.SetActive(false);
                }
            }

            // Interactability
            for (int i = 0; i < channelButtons.Length; i++)
            {
                SetInteractable(channelButtons[i], !readOnly && canOperate);
            }

            SetInteractable(frequencySlider, !readOnly && canOperate);
            SetInteractable(phaseSlider, !readOnly && canOperate);
            SetInteractable(scanButton, !readOnly && canOperate);
            SetInteractable(startSyncButton, !readOnly && canOperate && snapshot.SelectedChannelIndex >= 0 && snapshot.Status != RelayBStatus.Synchronizing);
            SetInteractable(cancelSyncButton, !readOnly && canOperate && snapshot.Status == RelayBStatus.Synchronizing);
            SetInteractable(closeButton, true);

            // Log transitions
            if (snapshot.Status != _lastStatus)
            {
                _lastStatus = snapshot.Status;
                AddLog($"STATUS: {StatusToDisplayString(snapshot.Status)}");
            }

            if (snapshot.SelectedChannelIndex != _lastLoggedChannel && snapshot.SelectedChannelIndex >= 0)
            {
                _lastLoggedChannel = snapshot.SelectedChannelIndex;
                AddLog($"ROUTING: CHANNEL {snapshot.SelectedChannelIndex + 1:00} ACTIVE");
            }
        }

        private void HookControls()
        {
            for (int i = 0; i < channelButtons.Length; i++)
            {
                int channelIndex = i;
                if (channelButtons[i] != null)
                {
                    channelButtons[i].onClick.RemoveAllListeners();
                    channelButtons[i].onClick.AddListener(() => HandleChannelClicked(channelIndex));
                }
            }

            if (frequencySlider != null)
            {
                frequencySlider.onValueChanged.RemoveAllListeners();
                frequencySlider.onValueChanged.AddListener(HandleFrequencyChanged);
            }

            if (phaseSlider != null)
            {
                phaseSlider.onValueChanged.RemoveAllListeners();
                phaseSlider.onValueChanged.AddListener(HandlePhaseChanged);
            }

            if (scanButton != null)
            {
                scanButton.onClick.RemoveAllListeners();
                scanButton.onClick.AddListener(HandleScanClicked);
            }

            if (startSyncButton != null)
            {
                startSyncButton.onClick.RemoveAllListeners();
                startSyncButton.onClick.AddListener(HandleStartSyncClicked);
            }

            if (cancelSyncButton != null)
            {
                cancelSyncButton.onClick.RemoveAllListeners();
                cancelSyncButton.onClick.AddListener(HandleCancelSyncClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Close);
            }
        }

        private void HandleChannelClicked(int channelIndex)
        {
            if (_controller == null) return;
            var snapshot = _controller.Snapshot;
            if (TryGetNetworkDirector(out var director))
                director.RequestRelayBControls(_controller, channelIndex, snapshot.CurrentFrequency, snapshot.CurrentPhase);
            else _controller.SelectChannel(channelIndex);
        }

        private void HandleFrequencyChanged(float value)
        {
            if (_suppressSliderEvents || _controller == null)
            {
                return;
            }

            var snapshot = _controller.Snapshot;
            if (TryGetNetworkDirector(out var director))
                director.RequestRelayBControls(_controller, snapshot.SelectedChannelIndex, value, snapshot.CurrentPhase);
            else _controller.SetFrequency(value);
        }

        private void HandlePhaseChanged(float value)
        {
            if (_suppressSliderEvents || _controller == null)
            {
                return;
            }

            var snapshot = _controller.Snapshot;
            if (TryGetNetworkDirector(out var director))
                director.RequestRelayBControls(_controller, snapshot.SelectedChannelIndex, snapshot.CurrentFrequency, value);
            else _controller.SetPhase(value);
        }

        private void HandleScanClicked()
        {
            if (TryGetNetworkDirector(out var director)) director.RequestRelayBScan(_controller);
            else _controller?.ScanChannels();
        }

        private void HandleStartSyncClicked()
        {
            if (TryGetNetworkDirector(out var director)) director.RequestRelayBStartSync(_controller);
            else _controller?.StartSynchronization();
        }

        private void HandleCancelSyncClicked()
        {
            if (TryGetNetworkDirector(out var director)) director.RequestRelayBCancelSync(_controller);
            else _controller?.CancelSynchronization();
        }

        private static bool TryGetNetworkDirector(out Zone2MissionDirector director)
        {
            director = Zone2MissionDirector.Instance;
            var matchState = NetworkMatchState.Instance ?? FindAnyObjectByType<NetworkMatchState>();
            return director != null && matchState != null && matchState.Object != null && matchState.Object.IsValid;
        }

        private void SetVisible(bool visible)
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(visible);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.blocksRaycasts = visible;
                canvasGroup.interactable = visible;
            }
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                eventSystem = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            }

            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("RuntimeEventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }

            var standalone = eventSystem.GetComponent<StandaloneInputModule>();
            if (standalone != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(standalone);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(standalone);
                }
            }

            var inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                inputModule.AssignDefaultActions();
            }
            else
            {
                inputModule.enabled = true;
            }
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }

        private static void SetInteractable(Selectable selectable, bool interactable)
        {
            if (selectable != null)
            {
                selectable.interactable = interactable;
            }
        }

        private static string StatusToDisplayString(RelayBStatus status)
        {
            switch (status)
            {
                case RelayBStatus.Scanning:
                    return "SCANNING...";
                case RelayBStatus.ChannelSelected:
                    return "CHANNEL SELECTED";
                case RelayBStatus.SignalMismatch:
                    return "SIGNAL MISMATCH";
                case RelayBStatus.Synchronizing:
                    return "SYNCHRONIZING";
                case RelayBStatus.DriftWarning:
                    return "DRIFT WARNING";
                case RelayBStatus.ConnectionLost:
                    return "CONNECTION LOST";
                case RelayBStatus.Online:
                    return "ONLINE";
                default:
                    return "OFFLINE";
            }
        }

        private Color StatusToColor(RelayBStatus status)
        {
            switch (status)
            {
                case RelayBStatus.Online:
                    return safeColor; // Green new Color(0.35f, 1f, 0.58f, 1f)
                case RelayBStatus.Synchronizing:
                    return referenceColor; // Cyan new Color(0.2f, 0.9f, 1f, 1f)
                case RelayBStatus.DriftWarning:
                    return new Color(1f, 0.55f, 0.1f, 1f); // Yellow/Orange
                case RelayBStatus.ChannelSelected:
                    return new Color(1f, 0.85f, 0.2f, 1f); // Yellow
                case RelayBStatus.Scanning:
                    return warningColor;
                case RelayBStatus.SignalMismatch:
                case RelayBStatus.ConnectionLost:
                    return dangerColor; // Red new Color(1f, 0.2f, 0.14f, 1f)
                default:
                    return offlineColor; // Grey new Color(0.55f, 0.65f, 0.7f, 1f)
            }
        }
    }
}

