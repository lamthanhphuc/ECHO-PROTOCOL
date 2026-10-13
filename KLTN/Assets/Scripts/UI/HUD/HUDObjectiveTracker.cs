using EchoProtocol.Settings;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace EchoProtocol.UI.HUD
{
    public class HUDObjectiveTracker : MonoBehaviour
    {
        private static readonly int[] RelayWarningThresholds = { 60, 30, 10 };

        [Header("References")]
        [SerializeField] private MatchFlowController matchFlow;
        [SerializeField] private EnergyCoreObjectiveProgress coreProgress;
        [SerializeField] private PowerPuzzleController powerPuzzle;
        [SerializeField] private SecurityTerminalDownload securityTerminal;
        [SerializeField] private EscapeDoorCountdown escapeDoor;

        [Header("UI Elements (Legacy Text)")]
        [SerializeField] private Text phaseBadgeText;
        [SerializeField] private Text objectiveTitleText;
        [SerializeField] private Text objectiveDetailText;

        [Header("UI Elements (TextMeshPro)")]
        [SerializeField] private TMP_Text phaseBadgeTmp;
        [SerializeField] private TMP_Text objectiveTitleTmp;
        [SerializeField] private TMP_Text objectiveDetailTmp;

        [Header("Visual Feedback")]
        [SerializeField] private Image progressBarFill;
        [SerializeField] private Image headerGlow;
        [SerializeField] private RectTransform containerRect;

        private MatchPhase _lastPhase = (MatchPhase)(-1);
        private int _lastZone2Stage = -1;
        private float _pulseTimer;
        private Image[] _progressSteps;
        private float _lastRelayRemaining = -1f;
        private int _lastRelayWarningThreshold = -1;
        private float _localRelayResetNoticeUntil = -1f;
        private CanvasGroup _visibilityGroup;
        private bool _hideZone1Objective;

        public void HideZone1Objective()
        {
            _hideZone1Objective = true;
            RefreshVisibility();
        }

        public void BindMatchFlow(
            MatchFlowController flow,
            EnergyCoreObjectiveProgress core,
            PowerPuzzleController puzzle,
            SecurityTerminalDownload terminal,
            EscapeDoorCountdown door)
        {
            matchFlow = flow;
            coreProgress = core;
            powerPuzzle = puzzle;
            securityTerminal = terminal;
            escapeDoor = door;
        }

        private void Awake()
        {
            ResolveComponents();
        }

        private void Start()
        {
            ResolveReferences();
            if (matchFlow != null)
            {
                UpdateUI(matchFlow.Phase);
            }
            else
            {
                UpdateUI(MatchPhase.ExploreCore);
            }
        }

        private void ResolveComponents()
        {
            if (phaseBadgeTmp == null && phaseBadgeText != null) phaseBadgeTmp = phaseBadgeText.GetComponent<TMP_Text>();
            if (objectiveTitleTmp == null && objectiveTitleText != null) objectiveTitleTmp = objectiveTitleText.GetComponent<TMP_Text>();
            if (objectiveDetailTmp == null && objectiveDetailText != null) objectiveDetailTmp = objectiveDetailText.GetComponent<TMP_Text>();
            if (containerRect == null) containerRect = GetComponent<RectTransform>();
            if (_visibilityGroup == null)
            {
                _visibilityGroup = GetComponent<CanvasGroup>();
                if (_visibilityGroup == null) _visibilityGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void Update()
        {
            if (matchFlow == null)
            {
                ResolveReferences();
                if (matchFlow == null)
                {
                    RefreshVisibility();
                    return;
                }
            }

            MatchPhase currentPhase = matchFlow.Phase;
            RefreshVisibility();
            int z2Stage = -1;
            if (EchoProtocol.MatchFlow.Zone2MissionDirector.Instance != null
                && currentPhase != MatchPhase.Zone3FindFrigate
                && currentPhase != MatchPhase.Zone3PushFrigate
                && currentPhase != MatchPhase.FinalHunt
                && currentPhase != MatchPhase.ExitCountdown
                && currentPhase != MatchPhase.Win
                && currentPhase != MatchPhase.Lose)
            {
                z2Stage = (int)EchoProtocol.MatchFlow.Zone2MissionDirector.Instance.CurrentStage;
            }

            if (currentPhase != _lastPhase || z2Stage != _lastZone2Stage)
            {
                _lastPhase = currentPhase;
                _lastZone2Stage = z2Stage;
                _pulseTimer = 0.2f; // Brief fade when the objective changes.
            }

            UpdateUI(currentPhase);
            UpdateRelayWarning();
            ShowRelayResetNotice();
            UpdatePulseAnimation();
        }

        private void RefreshVisibility()
        {
            if (_visibilityGroup == null) return;

            var director = EchoProtocol.MatchFlow.Zone2MissionDirector.Instance;
            bool isZone1 = director != null
                ? director.CurrentStage == EchoProtocol.MatchFlow.Zone2MissionStage.Zone1CoreObjective
                : matchFlow == null || matchFlow.Phase == MatchPhase.ExploreCore;
            bool visible = !_hideZone1Objective || !isZone1;
            _visibilityGroup.alpha = visible ? 1f : 0f;
            _visibilityGroup.blocksRaycasts = visible;
        }

        private void ResolveReferences()
        {
            if (matchFlow == null) matchFlow = FindAnyObjectByType<MatchFlowController>();
            if (coreProgress == null) coreProgress = FindAnyObjectByType<EnergyCoreObjectiveProgress>();
            if (powerPuzzle == null) powerPuzzle = FindAnyObjectByType<PowerPuzzleController>();
            if (securityTerminal == null) securityTerminal = FindAnyObjectByType<SecurityTerminalDownload>();
            if (escapeDoor == null) escapeDoor = FindAnyObjectByType<EscapeDoorCountdown>();
            ResolveComponents();
        }

        private void UpdateUI(MatchPhase phase)
        {
            if (EchoProtocol.MatchFlow.Zone2MissionDirector.Instance != null
                && phase != MatchPhase.Zone3FindFrigate
                && phase != MatchPhase.Zone3PushFrigate
                && phase != MatchPhase.FinalHunt
                && phase != MatchPhase.ExitCountdown
                && phase != MatchPhase.Win
                && phase != MatchPhase.Lose)
            {
                var z2 = EchoProtocol.MatchFlow.Zone2MissionDirector.Instance;
                switch (z2.CurrentStage)
                {
                    case EchoProtocol.MatchFlow.Zone2MissionStage.Zone1CoreObjective:
                        SetPhaseBadge("ZONE 1", "#00E5FF");
                        int placed = coreProgress != null ? coreProgress.PlacedCoreCount : 0;
                        int required = coreProgress != null ? coreProgress.RequiredCoreCount : 4;
                        if (required <= 0) required = 4;
                        SetObjective(
                            "Khôi phục nguồn điện",
                            $"Lắp lõi năng lượng vào trạm cấp điện    {placed}/{required}",
                            required > 0 ? (float)placed / required : 0f,
                            new Color(0f, 0.85f, 1f, 1f));
                        return;

                    case EchoProtocol.MatchFlow.Zone2MissionStage.FindSecurityTerminal:
                        SetPhaseBadge("ZONE 2 // FIND SECURITY TERMINAL", "#00E5FF");
                        SetObjective(
                            "Tìm trạm an ninh",
                            "Qua cửa airlock và tìm trạm an ninh.",
                            0f,
                            new Color(0f, 0.85f, 1f, 1f));
                        return;

                    case EchoProtocol.MatchFlow.Zone2MissionStage.RepairRelays:
                        SetPhaseBadge("ZONE 2 // Khôi phục hệ thống relay", "#FFB300");
                        int online = z2.CompletedRelayCount;
                        SetObjective(
                            "Khôi phục hệ thống relay",
                            $"Sửa và đồng bộ relay    {online}/4",
                            (float)online / 4f,
                            new Color(1f, 0.7f, 0.1f, 1f));
                        return;

                    case EchoProtocol.MatchFlow.Zone2MissionStage.SecurityHoldReady:
                        SetPhaseBadge("ZONE 2 // SECURITY HOLD READY", "#00E676");
                        SetObjective(
                            "Trở về trạm an ninh",
                            $"Relay 4/4 · Trở về trạm\nĐặt lại sau {RelayTimeText(z2)}",
                            1f,
                            new Color(0f, 0.9f, 0.4f, 1f));
                        return;

                    case EchoProtocol.MatchFlow.Zone2MissionStage.SecurityHold:
                        SetPhaseBadge("ZONE 2 // Xác thực bảo mật", "#FF3D00");
                        float secProgress = securityTerminal != null ? securityTerminal.Progress01 : 0f;
                        int secPercent = Mathf.RoundToInt(secProgress * 100f);
                        var matchState = EchoProtocol.Networking.NetworkMatchState.Instance;
                        int holders = matchState != null && matchState.Object != null && matchState.Object.IsValid
                            ? matchState.SecurityHoldParticipantCount : 1;
                        SetObjective(
                            "Xác thực bảo mật",
                            $"Giữ {GameplayInputSettings.GetKeyLabel(GameplayAction.Interact)} · {secPercent}% · {holders}/4 người\nCòn {SecurityHoldEtaText(matchState)} · Đặt lại {RelayTimeText(z2)}",
                            secProgress,
                            new Color(1f, 0.3f, 0.1f, 1f));
                        return;

                    case EchoProtocol.MatchFlow.Zone2MissionStage.AuthorizationCodeGranted:
                    case EchoProtocol.MatchFlow.Zone2MissionStage.UnlockZoneDoors:
                        SetPhaseBadge("ZONE 2 // Mở lối sang khu vực tiếp theo", "#00FF99");
                        SetObjective(
                            "Mở lối sang khu vực tiếp theo",
                            "Nhập mã xác thực tại bảng mở cửa.",
                            1f,
                            new Color(0f, 1f, 0.6f, 1f));
                        return;

                    case EchoProtocol.MatchFlow.Zone2MissionStage.Zone2Completed:
                        SetPhaseBadge("ZONE 2 // FACILITY ACCESS GRANTED", "#00E676");
                        SetObjective(
                            "Đã mở cửa an ninh",
                            "Đi tiếp đến hành lang sơ tán.",
                            1f,
                            new Color(0f, 0.9f, 0.4f, 1f));
                        return;
                }
            }

            switch (phase)
            {
                case MatchPhase.Zone3FindFrigate:
                    SetPhaseBadge("ZONE 3 // EMERGENCY POWER", "#00E5FF");
                    SetObjective("Tìm tàu", "Tìm nguồn dự phòng và chuẩn bị vận chuyển.", 0f,
                        new Color(0f, 0.85f, 1f, 1f));
                    break;

                case MatchPhase.Zone3PushFrigate:
                    bool docked = EchoProtocol.Networking.Zone3MissionDirector.Instance?.IsFrigateAtDestination == true;
                    var convoy = EchoProtocol.Networking.Zone3MissionDirector.Instance?.Convoy;
                    float chargeProgress = EchoProtocol.Networking.Zone3MissionDirector.Instance?.ChargeStation?.Progress01 ?? 0f;
                    SetPhaseBadge(docked ? "ZONE 3 // POWER DOCK" : "ZONE 3 // EMERGENCY POWER", "#00E5FF");
                    if (docked)
                    {
                        SetObjective("Chuyển nguồn điện",
                            $"Cấp điện cho hệ thống sơ tán · {Mathf.RoundToInt(chargeProgress * 100f)}%\nThả tay sẽ làm giảm tiến độ.",
                            chargeProgress, new Color(0f, 0.85f, 1f, 1f));
                    }
                    else if (EchoProtocol.MatchFlow.Zone3FuelCell.FindCarried(
                        EchoProtocol.Visuals.ObjectiveGlowHighlight.GetLocalPlayerTransform()?.gameObject) != null)
                    {
                        SetObjective("Nạp nhiên liệu cho tàu",
                            "Mang pin nhiên liệu về cổng nạp nhiên liệu.\nGiữ E để nạp khi hết nhiên liệu · G để thả.",
                            convoy != null ? convoy.Fuel01 : 0f, new Color(1f, 0.65f, 0.1f, 1f));
                    }
                    else if (convoy != null && convoy.IsFuelEmpty)
                    {
                        SetObjective("Tìm pin nhiên liệu",
                            "Hết nhiên liệu. Tìm pin nhiên liệu trong khu bảo trì\nrồi mang về cổng nạp nhiên liệu.",
                            0f, new Color(1f, 0.35f, 0.1f, 1f));
                    }
                    else if (convoy != null && convoy.WasFuelRestoredRecently)
                    {
                        SetObjective("Đã nạp nhiên liệu", convoy.FuelDisplay + "\nTiếp tục hộ tống tàu.",
                            convoy.Fuel01, new Color(0.2f, 1f, 0.5f, 1f));
                    }
                    else if (convoy != null && convoy.IsWaitingForRouteChoice)
                    {
                        SetObjective("Chọn hướng di chuyển",
                            convoy.FuelDisplay + "\n1 Trái · 2 Thẳng · 3 Phải · 4 Lùi",
                            0.5f, new Color(0f, 0.85f, 1f, 1f));
                    }
                    else
                    {
                        SetObjective("Hộ tống tàu",
                            (convoy != null ? convoy.FuelDisplay + "\n" : "") + "Đứng gần tàu để tiếp tục di chuyển.",
                            0.35f,
                            new Color(0f, 0.85f, 1f, 1f));
                    }
                    break;

                case MatchPhase.ExploreCore:
                    SetPhaseBadge("PHASE 1 // COLLECT ENERGY CORES", "#00E5FF");
                    int placed = coreProgress != null ? coreProgress.PlacedCoreCount : 0;
                    int required = coreProgress != null ? coreProgress.RequiredCoreCount : 4;
                    if (required <= 0) required = 4;
                    SetObjective(
                        "Khôi phục nguồn điện",
                        $"Lắp lõi năng lượng vào trạm cấp điện    {placed}/{required}",
                        required > 0 ? (float)placed / required : 0f,
                        new Color(0f, 0.85f, 1f, 1f));
                    break;

                case MatchPhase.SecurityHold:
                    SetPhaseBadge("PRIMARY OBJECTIVE // Xác thực bảo mật", "#FF3D00");
                    float progress = securityTerminal != null ? securityTerminal.Progress01 : 0f;
                    int percent = Mathf.RoundToInt(progress * 100f);
                    SetObjective(
                        "Xác thực bảo mật",
                        $"Tải dữ liệu tại trạm an ninh · {percent}%",
                        progress,
                        new Color(1f, 0.3f, 0.1f, 1f));
                    break;

                case MatchPhase.PowerPuzzle:
                    SetPhaseBadge("PRIMARY OBJECTIVE // RESTORE MAIN POWER", "#FFB300");
                    SetObjective(
                        "Nhập mã truy cập",
                        "Nhập mã xác thực tại Power Control.",
                        1f,
                        new Color(1f, 0.7f, 0.1f, 1f));
                    break;

                case MatchPhase.FinalHunt:
                case MatchPhase.ExitCountdown:
                    bool escapePhase = phase == MatchPhase.ExitCountdown;
                    SetPhaseBadge(escapePhase ? "ESCAPE // EXIT ONLINE" : "FINAL HUNT // EMERGENCY POWER ACTIVE", "#FF1744");
                    float secondsRemaining = GetEmergencyPowerRemainingSeconds();
                    bool timerRunning = IsEmergencyPowerTimerRunning();
                    string timeFormatted = FormatTime(secondsRemaining);
                    string statusMsg = timerRunning
                        ? $"Cửa thoát đã mở · Còn {timeFormatted}\nTrở về cửa thoát để sơ tán."
                        : "Cửa thoát đã mở. Trở về cửa thoát để sơ tán.";

                    SetObjective(
                        escapePhase ? "Thoát khỏi cơ sở" : "Trở về cửa thoát hiểm",
                        statusMsg,
                        timerRunning ? Mathf.Clamp01(secondsRemaining / GetEmergencyPowerDurationSeconds()) : 1f,
                        new Color(1f, 0.15f, 0.25f, 1f));
                    break;

                case MatchPhase.Win:
                    SetPhaseBadge("MISSION ACCOMPLISHED", "#00E676");
                    SetObjective(
                        "Sơ tán thành công",
                        "Đội đã hoàn thành cuộc sơ tán.",
                        1f,
                        new Color(0f, 0.9f, 0.4f, 1f));
                    break;

                case MatchPhase.Lose:
                    SetPhaseBadge("MISSION FAILED", "#D50000");
                    SetObjective(
                        "Nhiệm vụ thất bại",
                        "Không thể hoàn thành cuộc sơ tán.",
                        0f,
                        new Color(0.8f, 0.1f, 0.1f, 1f));
                    break;
            }
        }

        private void SetPhaseBadge(string badge, string hexColor)
        {
            string label = badge.StartsWith("ZONE 1") ? "ZONE 01"
                : badge.StartsWith("ZONE 2") ? "ZONE 02"
                : badge.StartsWith("ZONE 3") ? "ZONE 03"
                : badge.StartsWith("PHASE 1") ? "ZONE 01"
                : badge.StartsWith("PRIMARY OBJECTIVE") ? "ZONE 02"
                : badge.StartsWith("FINAL HUNT") || badge.StartsWith("ESCAPE") ? "THOÁT HIỂM"
                : badge == "MISSION ACCOMPLISHED" ? "HOÀN THÀNH"
                : badge == "MISSION FAILED" ? "THẤT BẠI" : badge;
            SetText(phaseBadgeTmp, phaseBadgeText, label);
        }

        private static string RelayTimeText(EchoProtocol.MatchFlow.Zone2MissionDirector director)
        {
            return FormatTime(director.RelayRepairRemainingSeconds);
        }

        private string SecurityHoldEtaText(EchoProtocol.Networking.NetworkMatchState matchState)
        {
            if (matchState != null && matchState.Object != null && matchState.Object.IsValid)
                return FormatTime(matchState.SecurityHoldEstimatedRemainingSeconds);
            return securityTerminal != null
                ? FormatTime(securityTerminal.DownloadDurationSeconds * (1f - securityTerminal.Progress01))
                : "--:--";
        }

        private static string FormatTime(float seconds)
        {
            int remaining = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return $"{remaining / 60:00}:{remaining % 60:00}";
        }

        private float GetEmergencyPowerRemainingSeconds()
        {
            var match = EchoProtocol.Networking.NetworkMatchState.Instance;
            if (match != null && match.Object != null && match.Object.IsValid)
                return match.EscapeRemainingSeconds;
            return escapeDoor != null ? escapeDoor.RemainingSeconds : 45f;
        }

        private bool IsEmergencyPowerTimerRunning()
        {
            var match = EchoProtocol.Networking.NetworkMatchState.Instance;
            if (match != null && match.Object != null && match.Object.IsValid)
                return match.IsEscapeTimerRunning;
            return escapeDoor != null && escapeDoor.IsCountingDown;
        }

        private float GetEmergencyPowerDurationSeconds()
        {
            var match = EchoProtocol.Networking.NetworkMatchState.Instance;
            if (match != null && match.Object != null && match.Object.IsValid)
                return Mathf.Max(1f, match.EscapeDurationSeconds);
            return escapeDoor != null ? escapeDoor.DurationSeconds : 1f;
        }

        private void UpdateRelayWarning()
        {
            var director = EchoProtocol.MatchFlow.Zone2MissionDirector.Instance;
            if (director == null || !director.IsRelayRepairWindowRunning)
            {
                if (_lastRelayRemaining > 0f && director != null && !director.IsSecurityHoldComplete
                    && director.CurrentStage == EchoProtocol.MatchFlow.Zone2MissionStage.RepairRelays)
                {
                    EchoProtocol.Audio.GameAudioRuntime.UI("security_terminal/access_denied");
                    _localRelayResetNoticeUntil = Time.unscaledTime + 6f;
                }
                _lastRelayRemaining = -1f;
                _lastRelayWarningThreshold = -1;
                return;
            }

            float remaining = director.RelayRepairRemainingSeconds;
            foreach (int threshold in RelayWarningThresholds)
            {
                if (_lastRelayRemaining > threshold && remaining <= threshold && _lastRelayWarningThreshold != threshold)
                {
                    EchoProtocol.Audio.GameAudioRuntime.UI("escape_endgame/countdown_tick");
                    _lastRelayWarningThreshold = threshold;
                    break;
                }
            }
            _lastRelayRemaining = remaining;
        }

        private void ShowRelayResetNotice()
        {
            var match = EchoProtocol.Networking.NetworkMatchState.Instance;
            bool networkNotice = match != null && match.Object != null && match.Object.IsValid
                && match.IsRelayRepairResetNoticeActive;
            if (!networkNotice && Time.unscaledTime >= _localRelayResetNoticeUntil) return;
            SetPhaseBadge("SECURITY HOLD QUÁ HẠN", "#FF3D00");
            SetText(objectiveDetailTmp, objectiveDetailText,
                "Cả 4 Relay đã bị đặt lại vì xác thực bảo mật quá hạn. Hãy sửa lại các Relay.");
        }

        private void SetObjective(string title, string detail, float progress01, Color accentColor)
        {
            SetText(objectiveTitleTmp, objectiveTitleText, title);
            SetText(objectiveDetailTmp, objectiveDetailText, detail);

            if (progressBarFill != null)
            {
                progressBarFill.fillAmount = Mathf.Clamp01(progress01);
                progressBarFill.color = accentColor.r > 0.7f && accentColor.g < 0.5f
                    ? HUDPresentationStyle.Danger : HUDPresentationStyle.Accent;
                UpdateProgressSteps(title, progress01);
            }
        }

        private void UpdateProgressSteps(string title, float progress)
        {
            bool discrete = title == "Khôi phục nguồn điện" || title == "Khôi phục hệ thống relay";
            int count = title == "Khôi phục nguồn điện" && coreProgress != null
                ? Mathf.Clamp(coreProgress.RequiredCoreCount, 1, 12) : 4;
            if (discrete && (_progressSteps == null || _progressSteps.Length != count))
            {
                if (_progressSteps != null)
                    foreach (var step in _progressSteps) if (step != null) Destroy(step.gameObject);
                _progressSteps = new Image[count];
                float width = (392f - (count - 1) * 6f) / count;
                for (int i = 0; i < count; i++)
                {
                    var go = new GameObject("ObjectiveStep", typeof(RectTransform), typeof(Image));
                    go.transform.SetParent(progressBarFill.transform.parent, false);
                    var rect = go.GetComponent<RectTransform>();
                    rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                    rect.anchoredPosition = new Vector2(i * (width + 6f), 0f);
                    rect.sizeDelta = new Vector2(width, 3f);
                    var image = go.GetComponent<Image>();
                    image.sprite = HUDTextureUtility.WhitePixel;
                    image.raycastTarget = false;
                    _progressSteps[i] = image;
                }
            }
            progressBarFill.gameObject.SetActive(!discrete);
            var track = progressBarFill.transform.parent.GetComponent<Image>();
            if (track != null) track.color = discrete ? Color.clear : HUDPresentationStyle.Track;
            if (_progressSteps == null) return;
            int completed = Mathf.RoundToInt(Mathf.Clamp01(progress) * count);
            for (int i = 0; i < _progressSteps.Length; i++)
            {
                _progressSteps[i].gameObject.SetActive(discrete);
                _progressSteps[i].color = i < completed ? HUDPresentationStyle.Accent : HUDPresentationStyle.Track;
            }
        }

        private void UpdatePulseAnimation()
        {
            if (containerRect != null) containerRect.localScale = Vector3.one;
            if (_pulseTimer <= 0f) return;
            _pulseTimer = Mathf.Max(0f, _pulseTimer - Time.deltaTime);
            if (_visibilityGroup != null)
                _visibilityGroup.alpha *= Mathf.Lerp(0.65f, 1f, 1f - _pulseTimer / 0.2f);
        }

        private static void SetText(TMP_Text tmp, Text legacy, string content)
        {
            if (tmp != null) tmp.text = content;
            if (legacy != null) legacy.text = content;
        }
    }
}
