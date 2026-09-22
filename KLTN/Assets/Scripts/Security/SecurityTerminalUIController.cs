using System;
using UnityEngine;
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

            if (_controlLock.ShouldAutoRelease() || Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            RefreshDisplay();
        }

        public void Open(GameObject interactor)
        {
            _controlLock.Acquire(interactor);
            _isOpen = true;

            if (rootCanvas != null)
            {
                rootCanvas.SetActive(true);
            }

            RefreshDisplay();
        }

        public void Close()
        {
            _isOpen = false;

            if (rootCanvas != null)
            {
                rootCanvas.SetActive(false);
            }

            _controlLock.Release();
        }

        public void RefreshDisplay()
        {
            bool relaysOnline = EmergencyNetworkState.AreRelaysOnline();
            bool isComplete = terminal != null && terminal.IsComplete;

            if (headerTitleText != null)
            {
                headerTitleText.text = "ECHO FACILITY // SECURITY JUNCTION";
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

                if (statusBannerText != null)
                {
                    statusBannerText.text = $"<color=#FF1744>SECURITY NETWORK OFFLINE\nRELAY CONNECTION LOST\nRESTORE RELAY NETWORK TO CONTINUE\nPOWER RELAYS: {powerCount}/2 | DATA RELAYS: {dataCount}/2\nRELAYS ONLINE: {totalRelays} / 4</color>";
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
                    statusBannerText.text = "<color=#00E676>AUTHENTICATION SUCCESSFUL</color>";
                }

                if (codeDisplayText != null)
                {
                    codeDisplayText.text = $"ZONE ACCESS AUTHORIZATION CODE:\n<size=56><color=#00FF99><b>{code}</b></color></size>";
                }

                if (instructionText != null)
                {
                    instructionText.text = "PROCEED TO ZONE ACCESS PANEL\nENTER CODE AT EITHER PANEL TO UNLOCK ZONE DOORS";
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
                        ? "<color=#FFB300>SECURITY AUTHENTICATION IN PROGRESS...</color>"
                        : "<color=#00E5FF>SECURITY NETWORK RESTORED\nAUTHENTICATION AVAILABLE</color>";
                }

                if (progressText != null)
                {
                    progressText.text = $"AUTHENTICATION PROGRESS: {percent}%";
                }

                if (progressBarFill != null)
                {
                    progressBarFill.fillAmount = progress;
                }
            }
        }
    }
