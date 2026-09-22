using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.HUD
{
    public class HUDObjectiveTracker : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MatchFlowController matchFlow;
        [SerializeField] private EnergyCoreObjectiveProgress coreProgress;
        [SerializeField] private PowerPuzzleController powerPuzzle;
        [SerializeField] private SecurityTerminalDownload securityTerminal;
        [SerializeField] private EscapeDoorCountdown escapeDoor;

        [Header("UI Elements")]
        [SerializeField] private Text phaseBadgeText;
        [SerializeField] private Text objectiveTitleText;
        [SerializeField] private Text objectiveDetailText;
        [SerializeField] private Image progressBarFill;
        [SerializeField] private Image headerGlow;

        private MatchPhase _lastPhase = (MatchPhase)(-1);
        private float _pulseTimer;

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

        private void Update()
        {
            if (matchFlow == null)
            {
                ResolveReferences();
                if (matchFlow == null) return;
            }

            MatchPhase currentPhase = matchFlow.Phase;
            if (currentPhase != _lastPhase)
            {
                _lastPhase = currentPhase;
                _pulseTimer = 1.5f; // trigger pulse animation
            }

            UpdateUI(currentPhase);
            UpdatePulseAnimation();
        }

        private void ResolveReferences()
        {
            if (matchFlow == null) matchFlow = FindAnyObjectByType<MatchFlowController>();
            if (coreProgress == null) coreProgress = FindAnyObjectByType<EnergyCoreObjectiveProgress>();
            if (powerPuzzle == null) powerPuzzle = FindAnyObjectByType<PowerPuzzleController>();
            if (securityTerminal == null) securityTerminal = FindAnyObjectByType<SecurityTerminalDownload>();
            if (escapeDoor == null) escapeDoor = FindAnyObjectByType<EscapeDoorCountdown>();
        }

        private void UpdateUI(MatchPhase phase)
        {
            if (EchoProtocol.MatchFlow.Zone2MissionDirector.Instance != null)
            {
                var z2 = EchoProtocol.MatchFlow.Zone2MissionDirector.Instance;
                switch (z2.CurrentStage)
                {
                    case EchoProtocol.MatchFlow.Zone2MissionStage.Zone1CoreObjective:
                        SetPhaseBadge("ZONE 1 // RESTORE SECTOR POWER", "#00E5FF");
                        int placed = coreProgress != null ? coreProgress.PlacedCoreCount : 0;
                        int required = coreProgress != null ? coreProgress.RequiredCoreCount : 4;
                        if (required <= 0) required = 4;
                        SetObjective(
                            "RESTORE SECTOR POWER",
                            $"Tìm và nạp {required} Energy Core về Sector Box [{placed}/{required}]",
                            required > 0 ? (float)placed / required : 0f,
                            new Color(0f, 0.85f, 1f, 1f));
                        return;

                    case EchoProtocol.MatchFlow.Zone2MissionStage.FindSecurityTerminal:
                        SetPhaseBadge("ZONE 2 // FIND SECURITY TERMINAL", "#00E5FF");
                        SetObjective(
                            "ENTER ZONE 2",
                            "Tiến vào Zone 2 và tìm Security Terminal",
                            0f,
                            new Color(0f, 0.85f, 1f, 1f));
                        return;

                    case EchoProtocol.MatchFlow.Zone2MissionStage.RepairRelays:
                        SetPhaseBadge("ZONE 2 // RESTORE RELAY NETWORK", "#FFB300");
                        int online = z2.CompletedRelayCount;
                        SetObjective(
                            "RESTORE RELAY NETWORK",
                            $"Khôi phục các trạm Relay khẩn cấp [Relays Online: {online} / 4]",
                            (float)online / 4f,
                            new Color(1f, 0.7f, 0.1f, 1f));
                        return;

                    case EchoProtocol.MatchFlow.Zone2MissionStage.SecurityHoldReady:
                        SetPhaseBadge("ZONE 2 // SECURITY HOLD READY", "#00E676");
                        SetObjective(
                            "RETURN TO SECURITY TERMINAL",
                            "Mạng Relay đã khôi phục hoàn toàn (4/4). Quay lại Security Terminal để xác thực",
                            1f,
                            new Color(0f, 0.9f, 0.4f, 1f));
                        return;

                    case EchoProtocol.MatchFlow.Zone2MissionStage.SecurityHold:
                        SetPhaseBadge("ZONE 2 // SECURITY AUTHENTICATION", "#FF3D00");
                        float secProgress = securityTerminal != null ? securityTerminal.Progress01 : 0f;
                        int secPercent = Mathf.RoundToInt(secProgress * 100f);
                        SetObjective(
                            "SECURITY AUTHENTICATION",
                            $"Giữ để tải dữ liệu bảo mật tại Security Terminal ({secPercent}%)",
                            secProgress,
                            new Color(1f, 0.3f, 0.1f, 1f));
                        return;

                    case EchoProtocol.MatchFlow.Zone2MissionStage.AuthorizationCodeGranted:
                    case EchoProtocol.MatchFlow.Zone2MissionStage.UnlockZoneDoors:
                        SetPhaseBadge("ZONE 2 // UNLOCK ZONE ACCESS", "#00FF99");
                        string code = z2.AuthorizationCode;
                        SetObjective(
                            "UNLOCK ZONE ACCESS",
                            $"Nhập mã cấp quyền tại một trong hai Access Panel | Mã: <color=#00FF99><b>{code}</b></color>",
                            1f,
                            new Color(0f, 1f, 0.6f, 1f));
                        return;

                    case EchoProtocol.MatchFlow.Zone2MissionStage.Zone2Completed:
                        SetPhaseBadge("ZONE 2 // ZONE ACCESS GRANTED", "#00E676");
                        SetObjective(
                            "ZONE 2 COMPLETE",
                            "Cửa thông đạo Zone 2 đã mở. Tiến vào khu vực tiếp theo!",
                            1f,
                            new Color(0f, 0.9f, 0.4f, 1f));
                        return;
                }
            }

            switch (phase)
            {
                case MatchPhase.ExploreCore:
                    SetPhaseBadge("GIAI ĐOẠN 1 // THU THẬP NĂNG LƯỢNG", "#00E5FF");
                    int placed = coreProgress != null ? coreProgress.PlacedCoreCount : 0;
                    int required = coreProgress != null ? coreProgress.RequiredCoreCount : 4;
                    if (required <= 0) required = 4;
                    SetObjective(
                        "TÌM VÀ NẠP ENERGY CORE",
                        $"Tìm và nạp {required} Energy Core về Sector Box [{placed}/{required}]",
                        required > 0 ? (float)placed / required : 0f,
                        new Color(0f, 0.85f, 1f, 1f));
                    break;

                case MatchPhase.SecurityHold:
                    SetPhaseBadge("NHIỆM VỤ CHÍNH // RESTORE MAIN POWER (GIAI ĐOẠN 1/2)", "#FF3D00");
                    float progress = securityTerminal != null ? securityTerminal.Progress01 : 0f;
                    int percent = Mathf.RoundToInt(progress * 100f);
                    SetObjective(
                        "XÁC THỰC BẢO MẬT (SECURITY HOLD)",
                        $"Tải dữ liệu bảo mật tại Security Terminal để nhận mã cấp điện ({percent}%)",
                        progress,
                        new Color(1f, 0.3f, 0.1f, 1f));
                    break;

                case MatchPhase.PowerPuzzle:
                    SetPhaseBadge("NHIỆM VỤ CHÍNH // RESTORE MAIN POWER (GIAI ĐOẠN 2/2)", "#FFB300");
                    string authCode = securityTerminal != null ? securityTerminal.AuthorizationCode : string.Empty;
                    if (string.IsNullOrEmpty(authCode) && matchFlow != null)
                    {
                        authCode = matchFlow.PowerAuthorizationCode;
                    }
                    string codeDisplay = !string.IsNullOrEmpty(authCode)
                        ? $" | Mã: <color=#00FF99><b>{authCode}</b></color>"
                        : "";
                    SetObjective(
                        "NHẬP MÃ CẤP ĐIỆN (POWER CONTROL)",
                        $"Đến Power Control và nhập mã cấp điện{codeDisplay}",
                        1f,
                        new Color(1f, 0.7f, 0.1f, 1f));
                    break;

                case MatchPhase.FinalHunt:
                case MatchPhase.ExitCountdown:
                    SetPhaseBadge("NGUY CẤP // BÁO ĐỘNG ĐỎ!", "#FF1744");
                    float secondsRemaining = escapeDoor != null ? escapeDoor.RemainingSeconds : 45f;
                    int mins = Mathf.FloorToInt(secondsRemaining / 60f);
                    int secs = Mathf.FloorToInt(secondsRemaining % 60f);
                    string timeFormatted = string.Format("{0:00}:{1:00}", mins, secs);

                    string statusMsg = (escapeDoor != null && escapeDoor.IsCountingDown)
                        ? $"Chạy đến Cửa Thoát Hiểm và sống sót ({timeFormatted})"
                        : "Chạy đến Cửa Thoát Hiểm và sống sót (00:45)";

                    SetObjective(
                        "THOÁT HIỂM KHẨN CẤP",
                        statusMsg,
                        escapeDoor != null && escapeDoor.IsCountingDown ? (secondsRemaining / 45f) : 1f,
                        new Color(1f, 0.15f, 0.25f, 1f));
                    break;

                case MatchPhase.Win:
                    SetPhaseBadge("NHIỆM VỤ THÀNH CÔNG", "#00E676");
                    SetObjective(
                        "ĐÃ THOÁT HIỂM AN TOÀN",
                        "Toàn bộ đội đã sống sót rời khỏi cơ sở nghiên cứu!",
                        1f,
                        new Color(0f, 0.9f, 0.4f, 1f));
                    break;

                case MatchPhase.Lose:
                    SetPhaseBadge("NHIỆM VỤ THẤT BẠI", "#D50000");
                    SetObjective(
                        "TOÀN ĐỘI ĐÃ BỊ TIÊU DIỆT",
                        "Không ai sống sót rời khỏi cơ sở nghiên cứu.",
                        0f,
                        new Color(0.8f, 0.1f, 0.1f, 1f));
                    break;
            }
        }

        private void SetPhaseBadge(string badge, string hexColor)
        {
            if (phaseBadgeText != null)
            {
                phaseBadgeText.text = $"<color={hexColor}><b>{badge}</b></color>";
            }
        }

        private void SetObjective(string title, string detail, float progress01, Color accentColor)
        {
            if (objectiveTitleText != null) objectiveTitleText.text = title;
            if (objectiveDetailText != null) objectiveDetailText.text = detail;
            if (progressBarFill != null)
            {
                progressBarFill.fillAmount = Mathf.Clamp01(progress01);
                progressBarFill.color = accentColor;
            }
        }

        private void UpdatePulseAnimation()
        {
            if (_pulseTimer > 0f)
            {
                _pulseTimer -= Time.deltaTime;
                float pulse = 1f + 0.15f * Mathf.Sin(_pulseTimer * 12f);
                if (headerGlow != null)
                {
                    Color c = headerGlow.color;
                    c.a = Mathf.Clamp01(_pulseTimer);
                    headerGlow.color = c;
                }
            }
            else if (headerGlow != null)
            {
                Color c = headerGlow.color;
                c.a = 0.2f;
                headerGlow.color = c;
            }
        }
    }
}