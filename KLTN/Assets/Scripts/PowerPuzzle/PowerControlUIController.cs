using System;
using System.Text;
using EchoProtocol.MatchFlow;
using EchoProtocol.Networking;
using UnityEngine;
using UnityEngine.UI;

public class PowerControlUIController : MonoBehaviour
    {
        [Header("Puzzle Reference")]
        [SerializeField] private PowerPuzzleController controller;
        [SerializeField] private NetworkPowerPuzzle networkPuzzle;

        [Header("UI Panels")]
        [SerializeField] private GameObject rootCanvas;
        [SerializeField] private GameObject lockedPanel;
        [SerializeField] private GameObject keypadPanel;
        [SerializeField] private GameObject onlinePanel;

        [Header("UI Texts")]
        [SerializeField] private TMPro.TMP_Text headerTitleText;
        [SerializeField] private TMPro.TMP_Text statusBannerText;
        [SerializeField] private TMPro.TMP_Text codeSlotsText;
        [SerializeField] private TMPro.TMP_Text feedbackText;

        [Header("Keypad Buttons")]
        [SerializeField] private Button[] digitButtons = new Button[10];
        [SerializeField] private Button clearButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button closeButton;

        private readonly StringBuilder _inputBuffer = new StringBuilder(4);
        private readonly PlayerInteractionControlLock _controlLock = new PlayerInteractionControlLock();
        private bool _isOpen;
        private int _consecutiveFails;
        private float _cooldownUntil;
        private bool _awaitingServerResult;
        private float _networkCooldownPresentationUntil;

        public bool IsOpen => _isOpen;
        public bool IsInCooldown => TryGetNetworkMatchState(out var matchState)
            ? matchState.IsZoneAccessCooldownActive
                || Time.unscaledTime < _networkCooldownPresentationUntil
            : Time.time < _cooldownUntil;
        public float CooldownRemaining
        {
            get
            {
                if (TryGetNetworkMatchState(out var matchState))
                {
                    float replicated = matchState.ZoneAccessCooldownRemainingSeconds;
                    float acknowledged = Mathf.Max(
                        0f,
                        _networkCooldownPresentationUntil - Time.unscaledTime);

                    return Mathf.Max(
                        replicated,
                        acknowledged);
                }

                return Mathf.Max(0f, _cooldownUntil - Time.time);
            }
        }

        private void OnEnable()
        {
            NetworkMatchState.LocalZoneAccessCodeRequestCompleted += HandleServerAccessCodeResult;
        }

        private void OnDisable()
        {
            NetworkMatchState.LocalZoneAccessCodeRequestCompleted -= HandleServerAccessCodeResult;
            _awaitingServerResult = false;
            _networkCooldownPresentationUntil = 0f;
        }

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<PowerPuzzleController>();
            }

            if (networkPuzzle == null)
            {
                networkPuzzle = GetComponentInParent<NetworkPowerPuzzle>();
            }

            BindButtons();

            if (rootCanvas != null)
            {
                rootCanvas.SetActive(false);
            }
        }

        private void BindButtons()
        {
            if (digitButtons != null)
            {
                for (int i = 0; i < digitButtons.Length; i++)
                {
                    int digit = i;
                    if (digitButtons[i] != null)
                    {
                        digitButtons[i].onClick.AddListener(() => OnDigitClicked(digit));
                    }
                }
            }

            if (clearButton != null)
            {
                clearButton.onClick.AddListener(OnClearClicked);
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(OnConfirmClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
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

            HandleKeyboardInput();
            RefreshDisplay();
        }

        private void HandleKeyboardInput()
        {
            if (IsInCooldown || IsOnline() || _awaitingServerResult)
            {
                return;
            }

            for (int i = 0; i <= 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i))
                {
                    OnDigitClicked(i);
                    return;
                }
            }

            if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Delete))
            {
                OnClearClicked();
            }
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                OnConfirmClicked();
            }
        }

        public void Open(GameObject interactor)
        {
            _controlLock.Acquire(interactor);
            _isOpen = true;
            _inputBuffer.Clear();

            if (feedbackText != null)
            {
                feedbackText.text = string.Empty;
            }

            if (rootCanvas != null)
            {
                rootCanvas.SetActive(true);
            }

            RefreshDisplay();
        }

        public void Close()
        {
            _isOpen = false;
            _inputBuffer.Clear();

            if (rootCanvas != null)
            {
                rootCanvas.SetActive(false);
            }

            _controlLock.Release();
        }

        public void OnDigitClicked(int digit)
        {
            if (IsInCooldown || IsOnline() || _awaitingServerResult || !IsSecurityHoldComplete())
            {
                return;
            }

            if (_inputBuffer.Length < 4)
            {
                _inputBuffer.Append(digit);
                EchoProtocol.Audio.GameAudioRuntime.UI("power_puzzle/rotary_switch");
                UpdateCodeSlotsText();
            }
        }

        public void OnClearClicked()
        {
            if (IsInCooldown || IsOnline() || _awaitingServerResult)
            {
                return;
            }

            if (_inputBuffer.Length > 0)
            {
                _inputBuffer.Length--;
                EchoProtocol.Audio.GameAudioRuntime.UI("power_puzzle/breaker_toggle");
                UpdateCodeSlotsText();
            }
        }

        public void OnConfirmClicked()
        {
            if (IsInCooldown || IsOnline() || _awaitingServerResult || !IsSecurityHoldComplete())
            {
                return;
            }

            if (_inputBuffer.Length != 4)
            {
                if (feedbackText != null)
                {
                    feedbackText.text = "<color=#FFB300>MẬT MÃ PHẢI CÓ ĐỦ 4 CHỮ SỐ</color>";
                }
                EchoProtocol.Audio.GameAudioRuntime.UI("power_puzzle/wrong_input");
                return;
            }

            string enteredCode = _inputBuffer.ToString();
            var director = Zone2MissionDirector.Instance;
            bool networked = TryGetNetworkMatchState(out var networkMatchState);
            if (director != null)
            {
                var disposition = director.SubmitAccessCode(this, enteredCode);
                if (networked)
                {
                    if (disposition == Zone2AccessSubmissionDisposition.Pending)
                    {
                        _awaitingServerResult = true;
                        if (feedbackText != null) feedbackText.text = "<color=#00E5FF>VERIFYING // WAITING FOR AUTHORIZATION</color>";
                        RefreshDisplay();
                    }
                    else if (disposition == Zone2AccessSubmissionDisposition.Rejected
                        && !networkMatchState.Object.HasStateAuthority)
                    {
                        if (feedbackText != null) feedbackText.text = "<color=#FFB300>REQUEST REJECTED // STATE CHANGED</color>";
                        RefreshDisplay();
                    }
                    return;
                }

                HandleOfflineSubmission(disposition == Zone2AccessSubmissionDisposition.Accepted);
                return;
            }

            if (networked)
            {
                if (feedbackText != null) feedbackText.text = "<color=#FFB300>REQUEST REJECTED // STATE CHANGED</color>";
                return;
            }

            HandleOfflineSubmission(SubmitCodeOffline(enteredCode));
        }

        private void HandleOfflineSubmission(bool success)
        {

            if (success)
            {
                _consecutiveFails = 0;
                if (feedbackText != null)
                {
                    feedbackText.text = "<color=#00FF99>XÁC THỰC THÀNH CÔNG // KHÔI PHỤC NGUỒN ĐIỆN!</color>";
                }
                EchoProtocol.Audio.GameAudioRuntime.UI("power_puzzle/puzzle_complete");
                RefreshDisplay();
            }
            else
            {
                _consecutiveFails++;
                _inputBuffer.Clear();
                UpdateCodeSlotsText();
                EchoProtocol.Audio.GameAudioRuntime.UI("power_puzzle/wrong_input");

                if (_consecutiveFails >= 3)
                {
                    _cooldownUntil = Time.time + 5f;
                    _consecutiveFails = 0;
                    if (feedbackText != null)
                    {
                        feedbackText.text = "<color=#FF1744>ACCESS DENIED // HỆ THỐNG TẠM KHÓA 5 GIÂY</color>";
                    }
                }
                else
                {
                    int remaining = 3 - _consecutiveFails;
                    if (feedbackText != null)
                    {
                        feedbackText.text = $"<color=#FF1744>ACCESS DENIED // CÒN LẠI {remaining} LẦN THỬ</color>";
                    }
                }
            }
        }

        private bool SubmitCodeOffline(string code)
        {
            if (controller != null)
            {
                return controller.SubmitAuthorizationCode(code, gameObject);
            }

            var flow = FindAnyObjectByType<MatchFlowController>();
            if (flow != null && flow.VerifyPowerCode(code))
            {
                flow.NotifyPowerPuzzleComplete();
                return true;
            }

            return false;
        }

        private void HandleServerAccessCodeResult(Zone2AccessCodeResult response)
        {
            var director = Zone2MissionDirector.Instance;
            if (director == null || !director.TryGetDistributionPanelIndex(this, out var panelIndex)
                || panelIndex != response.PanelIndex) return;

            _awaitingServerResult = false;
            switch (response.Result)
            {
                case Zone2NetworkCommandResult.Accepted:
                    _inputBuffer.Clear();
                    if (feedbackText != null) feedbackText.text = "<color=#00FF99>XÁC THỰC THÀNH CÔNG // KHÔI PHỤC NGUỒN ĐIỆN!</color>";
                    EchoProtocol.Audio.GameAudioRuntime.UI("power_puzzle/puzzle_complete");
                    break;
                case Zone2NetworkCommandResult.InvalidCode:
                    _inputBuffer.Clear();
                    EchoProtocol.Audio.GameAudioRuntime.UI("power_puzzle/wrong_input");
                    if (feedbackText != null) feedbackText.text = "<color=#FF1744>ACCESS DENIED</color>";
                    break;
                case Zone2NetworkCommandResult.Cooldown:
                    _networkCooldownPresentationUntil = Mathf.Max(
                        _networkCooldownPresentationUntil,
                        Time.unscaledTime + Mathf.Max(0f, response.CooldownSeconds));
                    if (feedbackText != null) feedbackText.text = "<color=#FF9100>HỆ THỐNG TẠM KHÓA // VUI LÒNG CHỜ</color>";
                    break;
                default:
                    if (feedbackText != null) feedbackText.text = "<color=#FFB300>REQUEST REJECTED // STATE CHANGED</color>";
                    break;
            }
            RefreshDisplay();
        }

        private bool IsSecurityHoldComplete()
        {
            if (TryGetNetworkMatchState(out var networkMatchState)) return networkMatchState.SecurityHoldCompleted;

            if (EchoProtocol.MatchFlow.Zone2MissionDirector.Instance != null && EchoProtocol.MatchFlow.Zone2MissionDirector.Instance.IsSecurityHoldComplete) return true;

            var flow = FindAnyObjectByType<MatchFlowController>();
            if (flow != null && flow.IsSecurityHoldComplete) return true;

            var terminal = FindAnyObjectByType<SecurityTerminalDownload>();
            if (terminal != null && terminal.IsComplete) return true;

            if (controller != null && controller.IsSecurityHoldComplete) return true;

            return false;
        }

        private bool IsOnline()
        {
            if (TryGetNetworkMatchState(out var networkMatchState)) return networkMatchState.ZoneDoorsUnlocked;

            if (EchoProtocol.MatchFlow.Zone2MissionDirector.Instance != null && EchoProtocol.MatchFlow.Zone2MissionDirector.Instance.AreZoneDoorsUnlocked) return true;

            var flow = FindAnyObjectByType<MatchFlowController>();
            if (flow != null && flow.IsPowerPuzzleComplete) return true;

            if (controller != null && controller.IsComplete) return true;

            return false;
        }

        public void RefreshDisplay()
        {
            bool online = IsOnline();
            bool authAvailable = IsSecurityHoldComplete();

            if (headerTitleText != null)
            {
                headerTitleText.text = "ZONE ACCESS AUTHORIZATION";
            }

            if (online)
            {
                if (lockedPanel != null) lockedPanel.SetActive(false);
                if (keypadPanel != null) keypadPanel.SetActive(false);
                if (onlinePanel != null) onlinePanel.SetActive(true);

                if (statusBannerText != null)
                {
                    statusBannerText.text = "<color=#00E676>ACCESS GRANTED // ZONE DOORS UNLOCKED</color>";
                }
                SetKeypadInteractable(false);
            }
            else if (!authAvailable)
            {
                if (lockedPanel != null) lockedPanel.SetActive(true);
                if (keypadPanel != null) keypadPanel.SetActive(false);
                if (onlinePanel != null) onlinePanel.SetActive(false);

                if (statusBannerText != null)
                {
                    statusBannerText.text = "<color=#FF1744>LOCKED // SECURITY AUTHORIZATION REQUIRED</color>";
                }
                SetKeypadInteractable(false);
            }
            else
            {
                if (lockedPanel != null) lockedPanel.SetActive(false);
                if (keypadPanel != null) keypadPanel.SetActive(true);
                if (onlinePanel != null) onlinePanel.SetActive(false);

                if (IsInCooldown)
                {
                    int sec = Mathf.CeilToInt(CooldownRemaining);
                    if (statusBannerText != null)
                    {
                        statusBannerText.text = $"<color=#FF9100>HỆ THỐNG TẠM KHÓA // CHỜ ({sec}s)</color>";
                    }
                    SetKeypadInteractable(false);
                }
                else
                {
                    if (statusBannerText != null)
                    {
                        statusBannerText.text = "<color=#00E5FF>AUTHORIZATION AVAILABLE // ENTER ACCESS CODE</color>";
                    }
                    SetKeypadInteractable(!_awaitingServerResult);
                }

                UpdateCodeSlotsText();
            }
        }

        private void UpdateCodeSlotsText()
        {
            if (codeSlotsText == null) return;

            string d0 = _inputBuffer.Length > 0 ? _inputBuffer[0].ToString() : "_";
            string d1 = _inputBuffer.Length > 1 ? _inputBuffer[1].ToString() : "_";
            string d2 = _inputBuffer.Length > 2 ? _inputBuffer[2].ToString() : "_";
            string d3 = _inputBuffer.Length > 3 ? _inputBuffer[3].ToString() : "_";

            codeSlotsText.text = $"[ <color=#00FF99><b>{d0}</b></color> ]   [ <color=#00FF99><b>{d1}</b></color> ]   [ <color=#00FF99><b>{d2}</b></color> ]   [ <color=#00FF99><b>{d3}</b></color> ]";
        }

        private void SetKeypadInteractable(bool interactable)
        {
            if (digitButtons != null)
            {
                for (int i = 0; i < digitButtons.Length; i++)
                {
                    if (digitButtons[i] != null) digitButtons[i].interactable = interactable;
                }
            }

            if (clearButton != null) clearButton.interactable = interactable;
            if (confirmButton != null) confirmButton.interactable = interactable;
        }

        private static bool TryGetNetworkMatchState(out NetworkMatchState matchState)
        {
            matchState = NetworkMatchState.Instance ?? FindAnyObjectByType<NetworkMatchState>();
            return matchState != null && matchState.Object != null && matchState.Object.IsValid;
        }
    }
