using System;
using System.Text;
using EchoProtocol.MatchFlow;
using EchoProtocol.Networking;
using UnityEngine;
using UnityEngine.InputSystem;
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

    [Header("Feedback Indicators")]
    [SerializeField] private Image statusLed;

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
    private float _ledFlashTimer;
    private Color _ledFlashColor = EchoProtocol.UI.HUD.HUDPresentationStyle.Accent;

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
        Close();
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

        if (_controlLock.ShouldAutoRelease() || _controlLock.ConsumeEscape())
        {
            Close();
            return;
        }

        HandleKeyboardInput();
        UpdateLedAnimation();
        RefreshDisplay();
    }

    private void UpdateLedAnimation()
    {
        if (statusLed == null) return;

        if (_ledFlashTimer > 0f)
        {
            _ledFlashTimer -= Time.deltaTime;
            float pulse = 0.5f + 0.5f * Mathf.PingPong(Time.time * 10f, 1f);
            statusLed.color = new Color(_ledFlashColor.r, _ledFlashColor.g, _ledFlashColor.b, pulse);
        }
    }

    private void TriggerLedFlash(Color color, float duration)
    {
        _ledFlashColor = color;
        _ledFlashTimer = duration;
        if (statusLed != null)
        {
            statusLed.color = color;
        }
    }

    private void HandleKeyboardInput()
    {
        if (IsInCooldown || IsOnline() || _awaitingServerResult)
        {
            return;
        }

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        for (int i = 0; i <= 9; i++)
        {
            if (WasDigitPressed(keyboard, i))
            {
                OnDigitClicked(i);
                return;
            }
        }

        if (keyboard.backspaceKey.wasPressedThisFrame || keyboard.deleteKey.wasPressedThisFrame)
        {
            OnClearClicked();
        }
        else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
        {
            OnConfirmClicked();
        }
    }

    private static bool WasDigitPressed(Keyboard keyboard, int digit)
    {
        switch (digit)
        {
            case 0: return keyboard.digit0Key.wasPressedThisFrame || keyboard.numpad0Key.wasPressedThisFrame;
            case 1: return keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame;
            case 2: return keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame;
            case 3: return keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame;
            case 4: return keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame;
            case 5: return keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame;
            case 6: return keyboard.digit6Key.wasPressedThisFrame || keyboard.numpad6Key.wasPressedThisFrame;
            case 7: return keyboard.digit7Key.wasPressedThisFrame || keyboard.numpad7Key.wasPressedThisFrame;
            case 8: return keyboard.digit8Key.wasPressedThisFrame || keyboard.numpad8Key.wasPressedThisFrame;
            case 9: return keyboard.digit9Key.wasPressedThisFrame || keyboard.numpad9Key.wasPressedThisFrame;
            default: return false;
        }
    }

    public void Open(GameObject interactor)
    {
        Zone2MinigameUIFocus.CloseOthers(this);
        _controlLock.Acquire(interactor, Close);
        if (!_controlLock.IsLocked) return;
        _isOpen = true;
        _inputBuffer.Clear();
        _ledFlashTimer = 0f;

        if (feedbackText != null)
        {
            feedbackText.text = string.Empty;
        }

        if (rootCanvas != null)
        {
            EchoProtocol.UI.HUD.HUDModalPresentation.Apply(rootCanvas);
            rootCanvas.SetActive(true);
        }

        RefreshDisplay();
    }

    public void Close()
    {
        _isOpen = false;
        _inputBuffer.Clear();
        _ledFlashTimer = 0f;

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
                feedbackText.text = "<color=#D8A85E>MẬT MÃ PHẢI CÓ ĐỦ 4 CHỮ SỐ</color>";
            }
            TriggerLedFlash(new Color(1f, 0.7f, 0.1f, 1f), 1.0f);
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
                    if (feedbackText != null) feedbackText.text = "<color=#7EA6A4>Đang xác thực mã…</color>";
                    TriggerLedFlash(EchoProtocol.UI.HUD.HUDPresentationStyle.Accent, 1.5f);
                    RefreshDisplay();
                }
                else if (disposition == Zone2AccessSubmissionDisposition.Rejected
                    && !networkMatchState.Object.HasStateAuthority)
                {
                    if (feedbackText != null) feedbackText.text = "<color=#D8A85E>Trạng thái đã thay đổi · Thử lại</color>";
                    TriggerLedFlash(new Color(1f, 0.7f, 0.1f, 1f), 1.2f);
                    RefreshDisplay();
                }
                return;
            }

            HandleOfflineSubmission(disposition == Zone2AccessSubmissionDisposition.Accepted);
            return;
        }

        if (networked)
        {
            if (feedbackText != null) feedbackText.text = "<color=#D8A85E>Trạng thái đã thay đổi · Thử lại</color>";
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
            TriggerLedFlash(new Color(0f, 1f, 0.6f, 1f), 3.0f);
            EchoProtocol.Audio.GameAudioRuntime.UI("power_puzzle/puzzle_complete");
            RefreshDisplay();
        }
        else
        {
            _consecutiveFails++;
            _inputBuffer.Clear();
            UpdateCodeSlotsText();
            TriggerLedFlash(new Color(1f, 0.15f, 0.15f, 1f), 1.8f);
            EchoProtocol.Audio.GameAudioRuntime.UI("power_puzzle/wrong_input");

            if (_consecutiveFails >= 3)
            {
                _cooldownUntil = Time.time + 5f;
                _consecutiveFails = 0;
                if (feedbackText != null)
                {
                    feedbackText.text = "<color=#D0685F>Sai mã · Tạm khóa 5 giây</color>";
                }
            }
            else
            {
                int remaining = 3 - _consecutiveFails;
                if (feedbackText != null)
                {
                    feedbackText.text = $"<color=#D0685F>Sai mã · Còn {remaining} lượt thử</color>";
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
                TriggerLedFlash(new Color(0f, 1f, 0.6f, 1f), 3.0f);
                EchoProtocol.Audio.GameAudioRuntime.UI("power_puzzle/puzzle_complete");
                break;
            case Zone2NetworkCommandResult.InvalidCode:
                _inputBuffer.Clear();
                EchoProtocol.Audio.GameAudioRuntime.UI("power_puzzle/wrong_input");
                TriggerLedFlash(new Color(1f, 0.15f, 0.15f, 1f), 1.8f);
                if (feedbackText != null) feedbackText.text = "<color=#D0685F>Không được cấp quyền</color>";
                break;
            case Zone2NetworkCommandResult.Cooldown:
                _networkCooldownPresentationUntil = Mathf.Max(
                    _networkCooldownPresentationUntil,
                    Time.unscaledTime + Mathf.Max(0f, response.CooldownSeconds));
                TriggerLedFlash(new Color(1f, 0.55f, 0f, 1f), 2.0f);
                if (feedbackText != null) feedbackText.text = "<color=#D8A85E>Hệ thống tạm khóa · Vui lòng chờ</color>";
                break;
            default:
                if (feedbackText != null) feedbackText.text = "<color=#D8A85E>Trạng thái đã thay đổi · Thử lại</color>";
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
            headerTitleText.text = "Mở cửa khu vực";
        }

        if (online)
        {
            if (lockedPanel != null) lockedPanel.SetActive(false);
            if (keypadPanel != null) keypadPanel.SetActive(false);
            if (onlinePanel != null) onlinePanel.SetActive(true);

            if (statusBannerText != null)
            {
                statusBannerText.text = "<color=#86AD91>Đã mở cửa khu vực</color>";
            }
            if (_ledFlashTimer <= 0f && statusLed != null)
            {
                statusLed.color = new Color(0f, 1f, 0.6f, 1f);
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
                statusBannerText.text = "<color=#D0685F>Cần xác thực tại Security Terminal</color>";
            }
            if (_ledFlashTimer <= 0f && statusLed != null)
            {
                statusLed.color = new Color(1f, 0.15f, 0.15f, 0.8f);
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
                    statusBannerText.text = $"<color=#D8A85E>Tạm khóa · Còn {sec} giây</color>";
                }
                if (_ledFlashTimer <= 0f && statusLed != null)
                {
                    statusLed.color = new Color(1f, 0.55f, 0f, 1f);
                }
                SetKeypadInteractable(false);
            }
            else
            {
                if (statusBannerText != null)
                {
                    statusBannerText.text = "<color=#7EA6A4>Nhập mã xác thực để mở cửa</color>";
                }
                if (_ledFlashTimer <= 0f && statusLed != null)
                {
                    statusLed.color = new Color(0f, 0.9f, 1f, 0.8f);
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

        codeSlotsText.text = $"{d0}    {d1}    {d2}    {d3}";
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
