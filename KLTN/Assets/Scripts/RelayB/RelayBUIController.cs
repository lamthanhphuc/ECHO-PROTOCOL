using System;
using EchoProtocol.MatchFlow;
using EchoProtocol.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace EchoProtocol.RelayB
{
    [DisallowMultipleComponent]
    public sealed partial class RelayBUIController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Header")]
        [SerializeField] private TMP_Text relayLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Button[] tabButtons = new Button[3];
        [SerializeField] private Image[] tabHighlights = new Image[3];

        [Header("Tab Panels")]
        [SerializeField] private GameObject spectrumTabPanel;
        [SerializeField] private GameObject processingTabPanel;
        [SerializeField] private GameObject syncTabPanel;

        [Header("Tab 1: Spectrum")]
        [SerializeField] private TMP_Text referenceProfileText;
        [SerializeField] private Button scanSpectrumButton;
        [SerializeField] private Button[] candidateButtons = new Button[4];
        [SerializeField] private Image[] candidateHighlights = new Image[4];
        [SerializeField] private TMP_Text[] candidateTexts = new TMP_Text[4];


        [Header("Tab 3: Carrier & Link Sync")]
        [SerializeField] private RelayBWaveformRenderer referenceWaveformRenderer;
        [SerializeField] private RelayBWaveformRenderer currentWaveformRenderer;
        [SerializeField] private TMP_Text referenceSignalLabel;
        [SerializeField] private TMP_Text currentSignalLabel;
        [SerializeField] private TMP_Text phaseValueText;
        [SerializeField] private Slider frequencySlider;
        [SerializeField] private Slider phaseSlider;
        [SerializeField] private TMP_Text frequencyValueText;
        [SerializeField] private TMP_Text linkProgressText;
        [SerializeField] private Image linkProgressFill;
        [SerializeField] private TMP_Text warningBannerText;

        [Header("Footer Actions & Log")]
        [SerializeField] private Button startLinkButton;
        [SerializeField] private Button abortLinkButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text systemLogText;

        [Header("Colors")]
        [SerializeField] private Color safeColor = new Color(0.35f, 1f, 0.58f, 1f);
        [SerializeField] private Color warningColor = new Color(1f, 0.78f, 0.18f, 1f);
        [SerializeField] private Color dangerColor = new Color(1f, 0.2f, 0.14f, 1f);
        [SerializeField] private Color offlineColor = new Color(0.55f, 0.65f, 0.7f, 1f);
        [SerializeField] private Color referenceColor = new Color(0.2f, 0.9f, 1f, 1f);
        [SerializeField] private Color activeTabColor = new Color(0.15f, 0.45f, 0.55f, 1f);
        [SerializeField] private Color inactiveTabColor = new Color(0.06f, 0.14f, 0.18f, 0.9f);

        private readonly RelayBPlayerControlLock _controlLock = new RelayBPlayerControlLock();
        private RelayBController _controller;
        private bool _sliderHooked;
        private int _pendingChannelIndex = -1;

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        private void Awake()
        {
            safeColor = new Color(0.52f, 0.68f, 0.57f);
            warningColor = EchoProtocol.UI.HUD.HUDPresentationStyle.Warning;
            dangerColor = EchoProtocol.UI.HUD.HUDPresentationStyle.Danger;
            offlineColor = EchoProtocol.UI.HUD.HUDPresentationStyle.Muted;
            referenceColor = EchoProtocol.UI.HUD.HUDPresentationStyle.Accent;
            activeTabColor = new Color(0.23f, 0.32f, 0.3f);
            inactiveTabColor = new Color(0.1f, 0.13f, 0.13f);
            EchoProtocol.UI.HUD.HUDModalPresentation.Apply(panelRoot);
            EnsureProgressFillSprite();
            HookControls();
            SetVisible(false);
        }

        private void OnDestroy() => Close();
        private void OnDisable() => Close();

        private void Update()
        {
            if (!IsOpen) return;
            FitTerminal();
            if (_controller != null) RefreshDecoderPresentation(_controller.Snapshot);
            HandleDecoderKeyboard();

            if (_controlLock.ShouldAutoRelease())
            {
                Close();
                return;
            }

            if (_controlLock.ConsumeEscape())
            {
                Close();
            }
        }

        public void Bind(RelayBController controller)
        {
            _controller = controller;
            EnsureProgressFillSprite();
            HookControls();
        }

        public void Open(GameObject interactor)
        {
            _pendingChannelIndex = -1;
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

            _controlLock.Acquire(interactor, Close);
            if (!_controlLock.IsLocked) return;
            SetVisible(true);
        }

        public void Close()
        {
            if (!_controlLock.IsLocked && !IsOpen) return;
            if (_controller != null)
            {
                var snapshot = _controller.Snapshot;
                bool syncing = snapshot.Status == RelayBStatus.Synchronizing
                    || snapshot.Status == RelayBStatus.DriftWarning
                    || snapshot.Status == RelayBStatus.ConnectionLost
                    || snapshot.Status == RelayBStatus.SignalMismatch;
                if (syncing)
                {
                    if (TryGetNetworkDirector(out var cancelDirector)) cancelDirector.RequestRelayBCancelSync(_controller);
                    else _controller.CancelSynchronization();
                }
            }

            if (TryGetNetworkDirector(out var director)) director.RequestRelayRelease(_controller);
            SetVisible(false);
            _controlLock.Release();
        }

        public void Refresh(RelayBSnapshot snapshot)
        {
            if (snapshot.SelectedChannelIndex >= 0 || !snapshot.HasScanned)
                _pendingChannelIndex = -1;
            bool readOnly = snapshot.IsOnline;
            bool canOperate = !TryGetNetworkDirector(out var director) || director.CanLocalPlayerOperateRelay(_controller);
            if (IsOpen && !readOnly && !canOperate)
            {
                Close();
                return;
            }

            // Header
            FitTerminal();
            SetText(relayLabel, snapshot.ActiveTab == 0 ? "Tìm tín hiệu" : snapshot.ActiveTab == 1 ? "Giải mã" : "Đồng bộ");
            SetText(stageLabel, $"Relay B · Bước {snapshot.ActiveTab + 1:00}");
            SetText(statusLabel, StatusToDisplayString(snapshot.Status));
            if (statusLabel != null) statusLabel.color = StatusToColor(snapshot.Status);

            // Tab Switching
            UpdateTabs(snapshot.ActiveTab, snapshot.HasScanned && snapshot.SelectedChannelIndex >= 0,
                snapshot.Decoder.IsComplete || snapshot.IsOnline);

            // 1. Spectrum Tab
            RefreshSpectrumTab(snapshot, readOnly, canOperate);
            if (snapshot.ActiveTab == 0)
                SetText(statusLabel, snapshot.SelectedChannelIndex >= 0 ? "Đã chọn tín hiệu" : snapshot.HasScanned ? "4 kênh tín hiệu" : "Chờ quét");

            // 2. Processing Tab
            RefreshProcessingTab(snapshot, readOnly, canOperate);

            // 3. Sync Tab
            RefreshSyncTab(snapshot, readOnly, canOperate);

            // System Log
            if (systemLogText != null && snapshot.SystemLog != null && snapshot.SystemLog.Length > 0)
                systemLogText.gameObject.SetActive(false);

            // Action Buttons
            bool isStage3 = snapshot.ActiveTab == 2;
            bool syncActive = snapshot.Status == RelayBStatus.Synchronizing
                || snapshot.Status == RelayBStatus.DriftWarning || snapshot.Status == RelayBStatus.ConnectionLost;
            bool canSync = !readOnly && canOperate && snapshot.HasScanned && snapshot.SelectedChannelIndex >= 0
                && isStage3 && snapshot.Decoder.IsComplete && !syncActive;
            bool canAbort = !readOnly && canOperate && syncActive;
            if (startLinkButton != null)
            {
                startLinkButton.gameObject.SetActive(isStage3);
                SetInteractable(startLinkButton, canSync);
                SetText(startLinkButton.GetComponentInChildren<TMP_Text>(), "Bắt đầu đồng bộ");
            }
            if (abortLinkButton != null)
            {
                abortLinkButton.gameObject.SetActive(isStage3);
                SetInteractable(abortLinkButton, canAbort);
                SetText(abortLinkButton.GetComponentInChildren<TMP_Text>(), "Hủy đồng bộ");
            }
            SetInteractable(closeButton, true);
        }

        private void UpdateTabs(int activeTab, bool stage2Unlocked, bool stage3Unlocked)
        {
            if (spectrumTabPanel != null) spectrumTabPanel.SetActive(activeTab == 0);
            if (processingTabPanel != null) processingTabPanel.SetActive(activeTab == 1);
            if (syncTabPanel != null) syncTabPanel.SetActive(activeTab == 2);

            for (int i = 0; i < tabHighlights.Length; i++)
            {
                if (tabHighlights[i] != null)
                {
                    tabHighlights[i].color = (i == activeTab) ? activeTabColor : inactiveTabColor;
                }
            }

            if (tabButtons != null)
            {
                if (tabButtons.Length > 0 && tabButtons[0] != null) SetInteractable(tabButtons[0], true);
                if (tabButtons.Length > 1 && tabButtons[1] != null) SetInteractable(tabButtons[1], stage2Unlocked);
                if (tabButtons.Length > 2 && tabButtons[2] != null) SetInteractable(tabButtons[2], stage3Unlocked);
            }
        }

        private void RefreshSpectrumTab(RelayBSnapshot snapshot, bool readOnly, bool canOperate)
        {
            var targetTitle = spectrumTabPanel != null
                ? spectrumTabPanel.transform.Find("ReferenceProfileCard/Title")?.GetComponent<TMP_Text>() : null;
            SetText(targetTitle, "Tín hiệu đích");
            if (snapshot.ReferenceProfile != null)
            {
                var profile = snapshot.ReferenceProfile;
                SetText(referenceProfileText,
                    EchoProtocol.Settings.GameLanguage.Choose(
                        $"Tần số: {profile.FundamentalMinKhz:0.0} - {profile.FundamentalMaxKhz:0.0} kHz\nDạng sóng: {snapshot.ReferenceWaveform.ToString().ToUpper()}\nPilot: {profile.ExpectedPilot}",
                        $"Frequency: {profile.FundamentalMinKhz:0.0} - {profile.FundamentalMaxKhz:0.0} kHz\nWaveform: {snapshot.ReferenceWaveform.ToString().ToUpper()}\nPilot: {profile.ExpectedPilot}"));
                if (referenceProfileText != null) { referenceProfileText.enableAutoSizing = true; referenceProfileText.fontSizeMin = 12f; referenceProfileText.fontSizeMax = 18f; referenceProfileText.fontSize = 18f; }
            }

            if (scanSpectrumButton != null)
            {
                scanSpectrumButton.gameObject.SetActive(snapshot.SelectedChannelIndex < 0);
                bool canScanOrRoute = !readOnly && canOperate && snapshot.SelectedChannelIndex < 0
                    && (!snapshot.HasScanned || _pendingChannelIndex >= 0);
                SetInteractable(scanSpectrumButton, canScanOrRoute);
                SetText(scanSpectrumButton.GetComponentInChildren<TMP_Text>(),
                    snapshot.SelectedChannelIndex >= 0 ? "Đã chọn tín hiệu"
                    : snapshot.HasScanned ? "Chọn tín hiệu" : "Quét tín hiệu");
            }

            for (int i = 0; i < candidateButtons.Length; i++)
            {
                bool hasCandidate = snapshot.Candidates != null && i < snapshot.Candidates.Length
                    && snapshot.Candidates[i] != null && snapshot.Candidates[i].Peaks.Length > 0;
                SetInteractable(candidateButtons[i], !readOnly && canOperate && snapshot.HasScanned && hasCandidate
                    && snapshot.SelectedChannelIndex < 0
                    && !(_controller != null && _controller.Simulation.IsSynchronizing));
                if (candidateHighlights != null && i < candidateHighlights.Length && candidateHighlights[i] != null)
                    candidateHighlights[i].color = snapshot.SelectedChannelIndex == i || _pendingChannelIndex == i
                        ? (snapshot.SelectedChannelIndex == i ? new Color(0.08f, 0.28f, 0.18f) : new Color(0.08f, 0.29f, 0.32f))
                        : new Color(0.07f, 0.1f, 0.115f, 1f);
                if (candidateTexts == null || i >= candidateTexts.Length || candidateTexts[i] == null) continue;
                if (!snapshot.HasScanned || !hasCandidate)
                {
                    SetText(candidateTexts[i], $"CH {i + 1:00}\nChưa có dữ liệu");
                    continue;
                }

                var candidate = snapshot.Candidates[i];
                SetText(candidateTexts[i],
                    $"CH {i + 1:00}  /  {candidate.Waveform.ToString().ToUpper()}\nFREQUENCY {candidate.Peaks[0]:0.0} kHz\nPILOT {candidate.PilotFrame}");
                candidateTexts[i].enableAutoSizing = true;
                candidateTexts[i].fontSizeMin = 12f;
                candidateTexts[i].fontSizeMax = 14f;
                candidateTexts[i].fontSize = 14f;
            }
            SetText(findNotice, snapshot.SelectedChannelIndex >= 0 ? "Đã chọn tín hiệu"
                : snapshot.FalseLockDetected ? "Tín hiệu đích thay đổi · Quét lại"
                : snapshot.HasScanned ? _pendingChannelIndex >= 0 ? "Đã chọn · Xác nhận cả 3 dấu hiệu" : "Kiểm tra tần số, dạng sóng và pilot" : "Quét để xem 4 kênh");
            if (findNotice != null) findNotice.color = snapshot.SelectedChannelIndex >= 0 ? safeColor : offlineColor;
            if (findContinue != null) findContinue.gameObject.SetActive(snapshot.SelectedChannelIndex >= 0);
        }


        private void RefreshSyncTab(RelayBSnapshot snapshot, bool readOnly, bool canOperate)
        {
            bool canAdjust = !readOnly && canOperate && snapshot.Decoder.IsComplete && !snapshot.IsOnline;
            var holdRule = syncTabPanel != null
                ? syncTabPanel.transform.Find("CalibrationSection/Rule3")?.GetComponent<TMP_Text>() : null;
            SetText(holdRule, "3. Bắt đầu khi đã khớp. Nhiễu có thể đổi tần số hoặc pha.");
            if (referenceWaveformRenderer != null)
            {
                referenceWaveformRenderer.SetWaveParameters(snapshot.ReferenceWaveform,
                    snapshot.TargetFrequency, snapshot.TargetPhase, 1f);
                referenceWaveformRenderer.SetWaveformColor(referenceColor);
                referenceWaveformRenderer.SetNoise(0.06f);
            }
            if (currentWaveformRenderer != null)
            {
                currentWaveformRenderer.SetWaveParameters(snapshot.CurrentWaveform,
                    snapshot.CurrentFrequency, snapshot.CurrentPhase, 1f);
                currentWaveformRenderer.SetWaveformColor(snapshot.IsSynchronized ? safeColor : warningColor);
                currentWaveformRenderer.SetNoise(snapshot.IsSynchronized ? 0.05f : 0.25f);
            }

            SetText(referenceSignalLabel, "Sóng tham chiếu");
            SetText(currentSignalLabel, "Sóng hiện tại");
            float frequencyTolerance = _controller != null && _controller.Config != null
                ? _controller.Simulation.FrequencyTolerancePercent : 3f;
            float phaseTolerance = _controller != null && _controller.Config != null
                ? _controller.Simulation.PhaseToleranceDegrees : 12f;
            bool frequencyAligned = snapshot.FrequencyErrorPercent <= frequencyTolerance;
            bool phaseAligned = snapshot.PhaseErrorDegrees <= phaseTolerance;
            SetText(frequencyValueText, frequencyAligned ? "Tần số · Khớp" : "Tần số");
            SetText(phaseValueText, phaseAligned ? "Pha · Khớp" : "Pha");
            if (frequencyValueText != null) { frequencyValueText.enableAutoSizing = true; frequencyValueText.fontSizeMin = 12f; frequencyValueText.fontSizeMax = 16f; frequencyValueText.fontSize = 16f; frequencyValueText.color = frequencyAligned ? safeColor : warningColor; }
            if (phaseValueText != null) { phaseValueText.enableAutoSizing = true; phaseValueText.fontSizeMin = 12f; phaseValueText.fontSizeMax = 16f; phaseValueText.fontSize = 16f; phaseValueText.color = phaseAligned ? safeColor : warningColor; }

            if (frequencySlider != null && phaseSlider != null && !_sliderHooked)
            {
                _sliderHooked = true;
                if (_controller != null && _controller.Config != null)
                {
                    frequencySlider.minValue = _controller.Config.MinFrequency;
                    frequencySlider.maxValue = _controller.Config.MaxFrequency;
                }
                frequencySlider.onValueChanged.RemoveAllListeners();
                frequencySlider.onValueChanged.AddListener(value =>
                {
                    if (_controller == null) return;
                    var current = _controller.Snapshot;
                    if (TryGetNetworkDirector(out var director))
                        director.RequestRelayBControls(_controller, current.SelectedChannelIndex, value, current.CurrentPhase);
                    else _controller.SetFrequency(value);
                });
                phaseSlider.onValueChanged.RemoveAllListeners();
                phaseSlider.onValueChanged.AddListener(value =>
                {
                    if (_controller == null) return;
                    var current = _controller.Snapshot;
                    if (TryGetNetworkDirector(out var director))
                        director.RequestRelayBControls(_controller, current.SelectedChannelIndex, current.CurrentFrequency, value);
                    else _controller.SetPhase(value);
                });
            }
            if (frequencySlider != null)
            {
                frequencySlider.interactable = canAdjust;
                frequencySlider.SetValueWithoutNotify(snapshot.CurrentFrequency);
            }
            if (phaseSlider != null)
            {
                phaseSlider.interactable = canAdjust;
                phaseSlider.SetValueWithoutNotify(snapshot.CurrentPhase);
            }

            SetText(linkProgressText,
                $"Đồng bộ · {snapshot.SyncProgressSeconds:0.0} / {snapshot.HoldRequiredSeconds:0.0} giây");
            if (linkProgressFill != null)
            {
                EnsureProgressFillSprite();
                linkProgressFill.fillAmount = snapshot.Progress01;
                linkProgressFill.color = snapshot.IsOnline || snapshot.IsSynchronized ? safeColor : warningColor;
            }
            if (warningBannerText == null) return;
            if (snapshot.IsOnline)
                SetSyncNotice("Relay hoạt động", safeColor);
            else if (snapshot.IsDriftActive && !snapshot.IsSynchronized)
                SetSyncNotice(snapshot.FrequencyErrorPercent > frequencyTolerance ? "Tần số bị trôi · Chỉnh lại tần số" : "Pha bị trôi · Chỉnh lại pha", dangerColor);
            else if (snapshot.IsDriftWarning && !snapshot.IsDriftActive)
                SetSyncNotice("Sắp có nhiễu tín hiệu", warningColor);
            else if (snapshot.Status == RelayBStatus.Synchronizing)
                SetSyncNotice(snapshot.IsSynchronized ? "Đã khớp · Giữ vị trí" : "Tín hiệu lệch · Chỉnh lại slider", snapshot.IsSynchronized ? safeColor : warningColor);
            else
                SetSyncNotice(snapshot.IsSynchronized ? "Đã khớp · Bắt đầu đồng bộ" : "Chỉnh sóng hiện tại khớp sóng tham chiếu", snapshot.IsSynchronized ? safeColor : offlineColor);
        }

        private void SetSyncNotice(string message, Color color)
        {
            SetText(warningBannerText, message);
            warningBannerText.color = color;
        }


        private void HookControls()
        {
            HookDecoderControls();
            // Tab Buttons
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int tabIndex = i;
                if (tabButtons[i] != null)
                {
                    tabButtons[i].onClick.RemoveAllListeners();
                    tabButtons[i].onClick.AddListener(() => _controller?.SetActiveTab(tabIndex));
                }
            }

            // Spectrum Tab
            if (scanSpectrumButton != null)
            {
                scanSpectrumButton.onClick.RemoveAllListeners();
                scanSpectrumButton.onClick.AddListener(HandleScanClicked);
            }

            for (int i = 0; i < candidateButtons.Length; i++)
            {
                int chIndex = i;
                if (candidateButtons[i] != null)
                {
                    candidateButtons[i].onClick.RemoveAllListeners();
                    candidateButtons[i].onClick.AddListener(() => HandleChannelSelected(chIndex));
                }
            }


            // Sync Tab: sliders are wired lazily in RefreshSyncTab on first refresh.
            // (Phase step buttons and timing buttons removed per redesign)

            // Footer Actions
            if (startLinkButton != null)
            {
                startLinkButton.onClick.RemoveAllListeners();
                startLinkButton.onClick.AddListener(HandleStartLink);
            }
            if (abortLinkButton != null)
            {
                abortLinkButton.onClick.RemoveAllListeners();
                abortLinkButton.onClick.AddListener(HandleAbortLink);
            }
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Close);
            }
        }

        private void HandleScanClicked()
        {
            if (_controller == null) return;
            var snapshot = _controller.Snapshot;
            if (snapshot.SelectedChannelIndex >= 0) return;
            if (snapshot.HasScanned)
            {
                if (_pendingChannelIndex >= 0) RouteSelectedChannel(_pendingChannelIndex);
                return;
            }
            if (TryGetNetworkDirector(out var director))
            {
                director.RequestRelayBScan(_controller);
            }
            else
            {
                _controller?.ScanSpectrum();
            }
        }

        private void HandleChannelSelected(int channelIndex)
        {
            if (_controller == null) return;
            var snapshot = _controller.Snapshot;
            if (!snapshot.HasScanned || snapshot.ReferenceProfile == null || snapshot.Candidates == null
                || channelIndex < 0 || channelIndex >= snapshot.Candidates.Length) return;
            var candidate = snapshot.Candidates[channelIndex];
            if (candidate == null || candidate.Peaks.Length == 0) return;
            if (snapshot.SelectedChannelIndex >= 0) return;
            _pendingChannelIndex = channelIndex;
            Refresh(snapshot);
        }

        private void RouteSelectedChannel(int channelIndex)
        {
            var snapshot = _controller.Snapshot;
            _pendingChannelIndex = -1;
            if (TryGetNetworkDirector(out var director))
            {
                director.RequestRelayBControls(_controller, channelIndex, snapshot.CurrentFrequency, snapshot.CurrentPhase);
            }
            else
            {
                _controller.SelectChannel(channelIndex);
                if (_controller.Snapshot.SelectedChannelIndex < 0)
                    _controller.RerollFindAfterMismatch(channelIndex);
            }
        }



        private void HandleStartLink()
        {
            if (TryGetNetworkDirector(out var director))
            {
                director.RequestRelayBStartSync(_controller);
            }
            else
            {
                _controller?.StartSynchronization();
            }
        }

        private void HandleAbortLink()
        {
            if (TryGetNetworkDirector(out var director))
            {
                director.RequestRelayBCancelSync(_controller);
            }
            else
            {
                _controller?.CancelSynchronization();
            }
        }

        public void AddLog(string message)
        {
            // Maintained for backward compatibility
        }

        private static bool TryGetNetworkDirector(out Zone2MissionDirector director)
        {
            director = Zone2MissionDirector.Instance;
            var matchState = NetworkMatchState.Instance ?? FindAnyObjectByType<NetworkMatchState>();
            return director != null && matchState != null && matchState.Object != null && matchState.Object.IsValid;
        }

        private void SetVisible(bool visible)
        {
            if (panelRoot != null) panelRoot.SetActive(visible);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.blocksRaycasts = visible;
                canvasGroup.interactable = visible;
            }
        }

        private void EnsureProgressFillSprite()
        {
            if (linkProgressFill != null)
            {
                linkProgressFill.type = Image.Type.Filled;
                linkProgressFill.fillMethod = Image.FillMethod.Horizontal;
                linkProgressFill.fillOrigin = 0;
                if (linkProgressFill.sprite == null)
                {
                    linkProgressFill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
                }
            }
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) eventSystem = FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("RuntimeEventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }

            var standalone = eventSystem.GetComponent<StandaloneInputModule>();
            if (standalone != null)
            {
                if (Application.isPlaying) Destroy(standalone);
                else DestroyImmediate(standalone);
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
                text.gameObject.SetActive(true);
                text.text = value;
            }
        }

        private static void SetInteractable(Selectable selectable, bool interactable)
        {
            if (selectable != null) selectable.interactable = interactable;
        }

        private static string StatusToDisplayString(RelayBStatus status)
        {
            switch (status)
            {
                case RelayBStatus.Scanning: return "Đang quét tín hiệu…";
                case RelayBStatus.ChannelSelected: return "Đã chọn kênh";
                case RelayBStatus.SignalMismatch: return "Tín hiệu không khớp";
                case RelayBStatus.Synchronizing: return "Đang đồng bộ";
                case RelayBStatus.DriftWarning: return "Cảnh báo trôi tín hiệu";
                case RelayBStatus.ConnectionLost: return "Mất kết nối";
                case RelayBStatus.Online: return "Hoạt động";
                default: return "Chưa hoạt động";
            }
        }

        private Color StatusToColor(RelayBStatus status)
        {
            switch (status)
            {
                case RelayBStatus.Online: return safeColor;
                case RelayBStatus.Synchronizing: return referenceColor;
                case RelayBStatus.DriftWarning: return warningColor;
                case RelayBStatus.ChannelSelected: return warningColor;
                case RelayBStatus.Scanning: return warningColor;
                case RelayBStatus.SignalMismatch:
                case RelayBStatus.ConnectionLost: return dangerColor;
                default: return offlineColor;
            }
        }
    }
}
