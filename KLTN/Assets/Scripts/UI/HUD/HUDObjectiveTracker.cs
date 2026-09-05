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
            switch (phase)
            {
                case MatchPhase.ExploreCore:
                    SetPhaseBadge("GIAI ĐOẠN 1 // THU THẬP NĂNG LƯỢNG", "#00E5FF");
                    int placed = coreProgress != null ? coreProgress.PlacedCoreCount : 0;
                    int required = coreProgress != null ? coreProgress.RequiredCoreCount : 3;
                    SetObjective(
                        "TÌM VÀ NẠP ENERGY CORE",
                        $"Tìm và nạp 3 Energy Core ({placed}/{required})",
                        required > 0 ? (float)placed / required : 0f,
                        new Color(0f, 0.85f, 1f, 1f));
                    break;

                case MatchPhase.PowerPuzzle:
                    SetPhaseBadge("GIAI ĐOẠN 2 // KHÔI PHỤC NĂNG LƯỢNG", "#FFB300");
                    int step = powerPuzzle != null ? powerPuzzle.StepIndex : 0;
                    int totalSteps = powerPuzzle != null ? powerPuzzle.StepCount : 3;
                    string codeHint = (powerPuzzle != null && powerPuzzle.HasInstructionForCurrentStep)
                        ? $" | Mã: <color=#00FF99>{powerPuzzle.CurrentCode}</color>" : "";
                    SetObjective(
                        "KHÔI PHỤC NGUỒN ĐIỆN",
                        $"Khôi phục nguồn điện (Power Puzzle) [{step}/{totalSteps}]{codeHint}",
                        totalSteps > 0 ? (float)step / totalSteps : 0.5f,
                        new Color(1f, 0.7f, 0.1f, 1f));
                    break;

                case MatchPhase.SecurityHold:
                    SetPhaseBadge("GIAI ĐOẠN 3 // TẢI DỮ LIỆU BẢO MẬT", "#FF3D00");
                    float progress = securityTerminal != null ? securityTerminal.Progress01 : 0f;
                    int percent = Mathf.RoundToInt(progress * 100f);
                    SetObjective(
                        "TẢI MÃ AN NINH",
                        $"Tải mã an ninh tại Phòng Security ({percent}%)",
                        progress,
                        new Color(1f, 0.3f, 0.1f, 1f));
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