using System;
using EchoProtocol.MatchFlow;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SecurityTerminalUIController : MonoBehaviour
    {
        [Header("Terminal Reference")]
        [SerializeField] private SecurityTerminalDownload terminal;

        [Header("UI Panels")]
        [SerializeField] private GameObject rootCanvas;
        [SerializeField] private GameObject inProgressPanel;
        [SerializeField] private GameObject completedPanel;
        [SerializeField] private GameObject offlineWarningPanel;

        [Header("UI Texts")]
        [SerializeField] private TMPro.TMP_Text headerTitleText;
        [SerializeField] private TMPro.TMP_Text statusBannerText;
        [SerializeField] private TMPro.TMP_Text progressText;
        [SerializeField] private TMPro.TMP_Text codeDisplayText;
        [SerializeField] private TMPro.TMP_Text instructionText;
        [SerializeField] private TMPro.TMP_Text offlineDetailText;
        [SerializeField] private Image progressBarFill;
        [SerializeField] private Button closeButton;

        private readonly PlayerInteractionControlLock _controlLock = new PlayerInteractionControlLock();
        private bool _isOpen;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (terminal == null)
            {
                terminal = GetComponentInParent<SecurityTerminalDownload>();
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }

            if (rootCanvas != null)
            {
                rootCanvas.SetActive(false);
            }
        }

        private void Update()
        {
            if (!_isOpen)
            {
                return;
            }

            if (_controlLock.ShouldAutoRelease()
                || _controlLock.ConsumeEscape())
            {
                Close();
                return;
            }

            RefreshDisplay();
        }

        public void Open(GameObject interactor)
        {
            Zone2MinigameUIFocus.CloseOthers(this);
            _controlLock.Acquire(interactor, Close);
            if (!_controlLock.IsLocked) return;
            _isOpen = true;

            if (rootCanvas != null)
            {
                rootCanvas.SetActive(true);
            }

            RefreshDisplay();
        }

        public void Close()
        {
            if (terminal != null && _controlLock.Player != null)
                terminal.InterruptDownload(_controlLock.Player);
            _isOpen = false;

            if (rootCanvas != null)
            {
                rootCanvas.SetActive(false);
            }

            _controlLock.Release();
        }

        private void OnDisable() => Close();
        private void OnDestroy() => Close();

        public void RefreshDisplay()
        {
            bool relaysOnline = EmergencyNetworkState.AreRelaysOnline();
            bool isComplete = terminal != null && terminal.IsComplete;
            var director = Zone2MissionDirector.Instance;

            if (headerTitleText != null)
            {
                headerTitleText.text = "ECHO PROTOCOL // SECURITY TERMINAL [ZONE 2]";
            }

            if (!relaysOnline && !isComplete)
            {
                if (offlineWarningPanel != null) offlineWarningPanel.SetActive(true);
                if (inProgressPanel != null) inProgressPanel.SetActive(false);
                if (completedPanel != null) completedPanel.SetActive(false);

                int powerCount = 0;
                int dataCount = 0;
                int totalRelays = 0;
                if (EchoProtocol.MatchFlow.Zone2MissionDirector.Instance != null)
                {
                    powerCount = EchoProtocol.MatchFlow.Zone2MissionDirector.Instance.PowerRelaysOnline;
                    dataCount = EchoProtocol.MatchFlow.Zone2MissionDirector.Instance.DataRelaysOnline;
                    totalRelays = EchoProtocol.MatchFlow.Zone2MissionDirector.Instance.CompletedRelayCount;
                }
                else
                {
                    var matchState = UnityEngine.Object.FindAnyObjectByType<EchoProtocol.Networking.NetworkMatchState>();
                    if (matchState != null)
                    {
                        powerCount = matchState.PowerRelaysOnline;
                        dataCount = matchState.DataRelaysOnline;
                        totalRelays = matchState.CompletedRelayCount;
                    }
                }

                // Clean single-line status badge on header
                if (statusBannerText != null)
                {
                    statusBannerText.text = "<color=#FF1744>● STATUS: NETWORK OFFLINE</color>";
                }

                // Clean structured diagnostic details inside the offline warning panel
                if (offlineDetailText != null)
                {
                    string powerStatus = powerCount >= 2
                        ? "<color=#00E676><b>2 / 2 ONLINE</b></color>"
                        : $"<color=#FF5252><b>{powerCount} / 2 OFFLINE</b></color>";
                    string dataStatus = dataCount >= 2
                        ? "<color=#00E676><b>2 / 2 ONLINE</b></color>"
                        : $"<color=#FF5252><b>{dataCount} / 2 OFFLINE</b></color>";
                    string totalStatus = totalRelays >= 4
                        ? "<color=#00E676><b>4 / 4 RESTORED</b></color>"
                        : $"<color=#FFD54F><b>{totalRelays} / 4 ACTIVE</b></color>";

                    offlineDetailText.text =
                        $"<size=17><color=#90A4AE>FACILITY RELAY GRID DIAGNOSTIC</color></size>\n\n" +
                        $"  • POWER RELAYS (RELAY A):   {powerStatus}\n" +
                        $"  • DATA RELAYS  (RELAY B):   {dataStatus}\n\n" +
                        $"<size=19>SYSTEM INTEGRITY: {totalStatus}</size>";
                }
                return;
            }

            if (offlineWarningPanel != null) offlineWarningPanel.SetActive(false);

            if (isComplete)
            {
                if (inProgressPanel != null) inProgressPanel.SetActive(false);
                if (completedPanel != null) completedPanel.SetActive(true);

                string code = terminal != null ? terminal.AuthorizationCode : "----";
                if (string.IsNullOrEmpty(code)) code = "----";

                if (statusBannerText != null)
                {
                    statusBannerText.text = "<color=#00E676>● STATUS: ACCESS GRANTED</color>";
                }

                if (codeDisplayText != null)
                {
                    codeDisplayText.text = $"ZONE 2 EXIT AUTHORIZATION CODE:\n<size=56><color=#00FF99><b>{code}</b></color></size>";
                }

                if (instructionText != null)
                {
                    instructionText.text = "<color=#FFEB3B><b>OBJECTIVE UPDATED:</b></color> ENTER ACCESS CODE AT ZONE DOOR\n<color=#90A4AE>Input code at either of the two exit distribution panels to unlock doors.</color>";
                }
            }
            else
            {
                if (inProgressPanel != null) inProgressPanel.SetActive(true);
                if (completedPanel != null) completedPanel.SetActive(false);

                float progress = terminal != null ? terminal.Progress01 : 0f;
                int percent = Mathf.RoundToInt(progress * 100f);

                if (statusBannerText != null)
                {
                    statusBannerText.text = terminal != null && terminal.IsDownloading
                        ? $"<color=#FFB300>● DOWNLOADING... ({percent}%)</color>"
                        : "<color=#00E5FF>● STATUS: READY FOR AUTHENTICATION</color>";
                }

                if (progressText != null)
                {
                    float remaining = director != null ? director.RelayRepairRemainingSeconds : 0f;
                    int seconds = Mathf.CeilToInt(remaining);
                    progressText.text = remaining > 0f
                        ? $"AUTHENTICATION: {percent}% | RELAYS RESET IN {seconds / 60:00}:{seconds % 60:00}"
                        : $"AUTHENTICATION PROGRESS: {percent}%";
                }

                if (progressBarFill != null)
                {
                    progressBarFill.fillAmount = progress;
                }
            }
        }
    }
