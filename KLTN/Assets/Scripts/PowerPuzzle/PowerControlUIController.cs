using System;
using System.Text;
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

        public bool IsOpen => _isOpen;
        public bool IsInCooldown => Time.time < _cooldownUntil;
        public float CooldownRemaining => Mathf.Max(0f, _cooldownUntil - Time.time);

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
            if (IsInCooldown || IsOnline())
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
            if (IsInCooldown || IsOnline() || !IsSecurityHoldComplete())
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
            if (IsInCooldown || IsOnline())
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
            if (IsInCooldown || IsOnline() || !IsSecurityHoldComplete())
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
            bool success = SubmitCode(enteredCode);

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

        private bool SubmitCode(string code)
        {
            // 1. Zone2MissionDirector (handles both Fusion RPCs and offline)
            if (EchoProtocol.MatchFlow.Zone2MissionDirector.Instance != null)
            {
                return EchoProtocol.MatchFlow.Zone2MissionDirector.Instance.SubmitAccessCode(code);
            }

            // 2. If Fusion networked match state
            var matchState = NetworkMatchState.Instance ?? FindAnyObjectByType<NetworkMatchState>();
            if (matchState != null && matchState.Object != null && matchState.Object.IsValid)
            {
                if (matchState.Object.HasStateAuthority)
                {
                    return matchState.TrySubmitZoneAccessCode(Fusion.PlayerRef.None, code);
                }
                else
                {
                    matchState.RpcSubmitZoneAccessCode(matchState.Runner.LocalPlayer, code);
                    return true;
                }
            }

            // 3. Controller submission fallback
            if (controller != null)
            {
                return controller.SubmitAuthorizationCode(code, gameObject);
            }

            // 4. Fallback to MatchFlowController
            var flow = FindAnyObjectByType<MatchFlowController>();
            if (flow != null && flow.VerifyPowerCode(code))
            {
                flow.NotifyPowerPuzzleComplete();
                return true;
            }

            return false;
        }

        private bool IsSecurityHoldComplete()
        {
            if (EchoProtocol.MatchFlow.Zone2MissionDirector.Instance != null && EchoProtocol.MatchFlow.Zone2MissionDirector.Instance.IsSecurityHoldComplete) return true;

            var matchState = NetworkMatchState.Instance ?? FindAnyObjectByType<NetworkMatchState>();
            if (matchState != null && matchState.SecurityHoldCompleted) return true;

            var flow = FindAnyObjectByType<MatchFlowController>();
            if (flow != null && flow.IsSecurityHoldComplete) return true;

            var terminal = FindAnyObjectByType<SecurityTerminalDownload>();
            if (terminal != null && terminal.IsComplete) return true;

            if (controller != null && controller.IsSecurityHoldComplete) return true;

            return false;
        }

        private bool IsOnline()
        {
            if (EchoProtocol.MatchFlow.Zone2MissionDirector.Instance != null && EchoProtocol.MatchFlow.Zone2MissionDirector.Instance.AreZoneDoorsUnlocked) return true;

            var matchState = NetworkMatchState.Instance ?? FindAnyObjectByType<NetworkMatchState>();
            if (matchState != null && (matchState.ZoneDoorsUnlocked || matchState.PowerPuzzleCompleted)) return true;

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
                    SetKeypadInteractable(true);
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
    }
