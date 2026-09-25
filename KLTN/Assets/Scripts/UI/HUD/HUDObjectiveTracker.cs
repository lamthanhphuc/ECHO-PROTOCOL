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
        private float _lastRelayRemaining = -1f;
        private int _lastRelayWarningThreshold = -1;
        private float _localRelayResetNoticeUntil = -1f;

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
        }

        private void Update()
        {
            if (matchFlow == null)
            {
                ResolveReferences();
                if (matchFlow == null) return;
            }

            MatchPhase currentPhase = matchFlow.Phase;
            int z2Stage = -1;
            if (EchoProtocol.MatchFlow.Zone2MissionDirector.Instance != null)
            {
                z2Stage = (int)EchoProtocol.MatchFlow.Zone2MissionDirector.Instance.CurrentStage;
            }

            if (currentPhase != _lastPhase || z2Stage != _lastZone2Stage)
            {
                _lastPhase = currentPhase;
                _lastZone2Stage = z2Stage;
                _pulseTimer = 1.8f; // Trigger objective update pulse animation
            }

            UpdateUI(currentPhase);
            UpdateRelayWarning();
            ShowRelayResetNotice();
            UpdatePulseAnimation();
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
                            $"Locate and insert {required} Energy Cores into Sector Box [{placed}/{required}]",
                            required > 0 ? (float)placed / required : 0f,
                            new Color(0f, 0.85f, 1f, 1f));
                        return;

                    case EchoProtocol.MatchFlow.Zone2MissionStage.FindSecurityTerminal:
                        SetPhaseBadge("ZONE 2 // FIND SECURITY TERMINAL", "#00E5FF");
                        SetObjective(
                            "ENTER ZONE 2",
                            "Proceed through airlock into Zone 2 and locate Security Terminal",
                            0f,
                            new Color(0f, 0.85f, 1f, 1f));
                        return;

                    case EchoProtocol.MatchFlow.Zone2MissionStage.RepairRelays:
                        SetPhaseBadge("ZONE 2 // RESTORE RELAY NETWORK", "#FFB300");
                        int online = z2.CompletedRelayCount;
                        SetObjective(
                            "RESTORE RELAY NETWORK",
                            $"Stabilize and synchronize emergency relays [Relays Online: {online} / 4]",
                            (float)online / 4f,
                            new Color(1f, 0.7f, 0.1f, 1f));
                        return;

                    case EchoProtocol.MatchFlow.Zone2MissionStage.SecurityHoldReady:
                        SetPhaseBadge("ZONE 2 // SECURITY HOLD READY", "#00E676");
                        SetObjective(
                            "RETURN TO TERMINAL",
                            $"4/4 relays online. Reach terminal | Reset in {RelayTimeText(z2)}",
                            1f,
                            new Color(0f, 0.9f, 0.4f, 1f));
                        return;

                    case EchoProtocol.MatchFlow.Zone2MissionStage.SecurityHold:
                        SetPhaseBadge("ZONE 2 // SECURITY AUTHENTICATION", "#FF3D00");
                        float secProgress = securityTerminal != null ? securityTerminal.Progress01 : 0f;
                        int secPercent = Mathf.RoundToInt(secProgress * 100f);
                        var matchState = EchoProtocol.Networking.NetworkMatchState.Instance;
                        int holders = matchState != null && matchState.Object != null && matchState.Object.IsValid
                            ? matchState.SecurityHoldParticipantCount : 1;
                        SetObjective(
                            "SECURITY AUTHENTICATION",
                            $"Hold E {secPercent}% | {holders}/4 | ETA {SecurityHoldEtaText(matchState)} | Reset {RelayTimeText(z2)}",
                            secProgress,
                            new Color(1f, 0.3f, 0.1f, 1f));
                        return;

                    case EchoProtocol.MatchFlow.Zone2MissionStage.AuthorizationCodeGranted:
                    case EchoProtocol.MatchFlow.Zone2MissionStage.UnlockZoneDoors:
                        SetPhaseBadge("ZONE 2 // UNLOCK ZONE ACCESS", "#00FF99");
                        string code = z2.AuthorizationCode;
                        SetObjective(
                            "UNLOCK ZONE ACCESS",
                            $"Input authorization code at either Access Panel | Code: <color=#00FF99><b>{code}</b></color>",
                            1f,
                            new Color(0f, 1f, 0.6f, 1f));
                        return;

                    case EchoProtocol.MatchFlow.Zone2MissionStage.Zone2Completed:
                        SetPhaseBadge("ZONE 2 // FACILITY ACCESS GRANTED", "#00E676");
                        SetObjective(
                            "ZONE 2 COMPLETE",
                            "Security blast doors unsealed. Proceed to facility evacuation corridor!",
                            1f,
                            new Color(0f, 0.9f, 0.4f, 1f));
                        return;
                }
            }

            switch (phase)
            {
                case MatchPhase.ExploreCore:
                    SetPhaseBadge("PHASE 1 // COLLECT ENERGY CORES", "#00E5FF");
                    int placed = coreProgress != null ? coreProgress.PlacedCoreCount : 0;
                    int required = coreProgress != null ? coreProgress.RequiredCoreCount : 4;
                    if (required <= 0) required = 4;
                    SetObjective(
                        "RESTORE SECTOR POWER",
                        $"Locate and insert {required} Energy Cores into Sector Box [{placed}/{required}]",
                        required > 0 ? (float)placed / required : 0f,
                        new Color(0f, 0.85f, 1f, 1f));
                    break;

                case MatchPhase.SecurityHold:
                    SetPhaseBadge("PRIMARY OBJECTIVE // SECURITY AUTHENTICATION", "#FF3D00");
                    float progress = securityTerminal != null ? securityTerminal.Progress01 : 0f;
                    int percent = Mathf.RoundToInt(progress * 100f);
                    SetObjective(
                        "SECURITY AUTHENTICATION",
                        $"Download security credentials at Security Terminal ({percent}%)",
                        progress,
                        new Color(1f, 0.3f, 0.1f, 1f));
                    break;

                case MatchPhase.PowerPuzzle:
                    SetPhaseBadge("PRIMARY OBJECTIVE // RESTORE MAIN POWER", "#FFB300");
                    string authCode = securityTerminal != null ? securityTerminal.AuthorizationCode : string.Empty;
                    if (string.IsNullOrEmpty(authCode) && matchFlow != null)
                    {
                        authCode = matchFlow.PowerAuthorizationCode;
                    }
                    string codeDisplay = !string.IsNullOrEmpty(authCode)
                        ? $" | Code: <color=#00FF99><b>{authCode}</b></color>"
                        : "";
                    SetObjective(
                        "ENTER ACCESS CODE",
                        $"Proceed to Power Control panel and input authorization code{codeDisplay}",
                        1f,
                        new Color(1f, 0.7f, 0.1f, 1f));
                    break;

                case MatchPhase.FinalHunt:
                case MatchPhase.ExitCountdown:
                    SetPhaseBadge("CRITICAL ALERT // EMERGENCY EVACUATION", "#FF1744");
                    float secondsRemaining = escapeDoor != null ? escapeDoor.RemainingSeconds : 45f;
                    int mins = Mathf.FloorToInt(secondsRemaining / 60f);
                    int secs = Mathf.FloorToInt(secondsRemaining % 60f);
                    string timeFormatted = string.Format("{0:00}:{1:00}", mins, secs);

                    string statusMsg = (escapeDoor != null && escapeDoor.IsCountingDown)
                        ? $"Sprint to Evacuation Airlock and survive lockdown ({timeFormatted})"
                        : "Sprint to Evacuation Airlock and survive lockdown (00:45)";

                    SetObjective(
                        "FACILITY LOCKDOWN",
                        statusMsg,
                        escapeDoor != null && escapeDoor.IsCountingDown ? (secondsRemaining / 45f) : 1f,
                        new Color(1f, 0.15f, 0.25f, 1f));
                    break;

                case MatchPhase.Win:
                    SetPhaseBadge("MISSION ACCOMPLISHED", "#00E676");
                    SetObjective(
                        "EVACUATION SUCCESSFUL",
                        "All surviving personnel have extracted from the facility.",
                        1f,
                        new Color(0f, 0.9f, 0.4f, 1f));
                    break;

                case MatchPhase.Lose:
                    SetPhaseBadge("MISSION FAILED", "#D50000");
                    SetObjective(
                        "OPERATIVES LOST",
                        "All personnel eliminated within the facility.",
                        0f,
                        new Color(0.8f, 0.1f, 0.1f, 1f));
                    break;
            }
        }

        private void SetPhaseBadge(string badge, string hexColor)
        {
            string formatted = $"<color={hexColor}><b>{badge}</b></color>";
            SetText(phaseBadgeTmp, phaseBadgeText, formatted);
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
                "Cả 4 Relay đã bị đặt lại vì Security Hold quá hạn. Hãy sửa lại các Relay.");
        }

        private void SetObjective(string title, string detail, float progress01, Color accentColor)
        {
            SetText(objectiveTitleTmp, objectiveTitleText, title);
            SetText(objectiveDetailTmp, objectiveDetailText, detail);

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
                float progress = _pulseTimer / 1.8f;
                float pulse = 1f + 0.12f * Mathf.Sin(progress * Mathf.PI * 4f);

                if (headerGlow != null)
                {
                    Color c = headerGlow.color;
                    c.a = Mathf.Clamp01(0.2f + 0.8f * progress * pulse);
                    headerGlow.color = c;
                }

                if (containerRect != null)
                {
                    float scale = 1f + 0.03f * progress * Mathf.Sin(progress * Mathf.PI * 2f);
                    containerRect.localScale = new Vector3(scale, scale, 1f);
                }
            }
            else
            {
                if (headerGlow != null)
                {
                    Color c = headerGlow.color;
                    c.a = 0.2f;
                    headerGlow.color = c;
                }

                if (containerRect != null && containerRect.localScale != Vector3.one)
                {
                    containerRect.localScale = Vector3.one;
                }
            }
        }

        private static void SetText(TMP_Text tmp, Text legacy, string content)
        {
            if (tmp != null) tmp.text = content;
            if (legacy != null) legacy.text = content;
        }
    }
}
