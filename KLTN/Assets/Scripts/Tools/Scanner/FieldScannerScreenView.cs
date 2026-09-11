using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.Tools.Scanner
{
    public class FieldScannerScreenView : MonoBehaviour
    {
        [Header("UI Text References")]
        [SerializeField] private TextMeshProUGUI tmpHeader;
        [SerializeField] private TextMeshProUGUI tmpRadar;
        [SerializeField] private TextMeshProUGUI tmpSignal;
        [SerializeField] private TextMeshProUGUI tmpStatus;

        [Header("Legacy Text Fallbacks (if TMP not used)")]
        [SerializeField] private Text legacyHeader;
        [SerializeField] private Text legacyRadar;
        [SerializeField] private Text legacySignal;
        [SerializeField] private Text legacyStatus;

        [Header("Audio")]
        [SerializeField] private FieldScannerAudio scannerAudio;

        private NetworkFieldScanner _boundScanner;

        private void Awake()
        {
            if (scannerAudio == null) scannerAudio = GetComponentInChildren<FieldScannerAudio>(true);
            ResolveTextReferences();
        }

        private void ResolveTextReferences()
        {
            if (tmpHeader == null || tmpRadar == null || tmpSignal == null || tmpStatus == null)
            {
                var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    string n = texts[i].gameObject.name.ToLowerInvariant();
                    if (tmpHeader == null && n.Contains("head")) tmpHeader = texts[i];
                    else if (tmpRadar == null && n.Contains("radar")) tmpRadar = texts[i];
                    else if (tmpSignal == null && n.Contains("signal")) tmpSignal = texts[i];
                    else if (tmpStatus == null && n.Contains("status")) tmpStatus = texts[i];
                }
            }
        }

        private void OnEnable()
        {
            ResolveTextReferences();
            FindAndBindScanner();
        }

        private void OnDisable()
        {
            UnbindScanner();
        }

        private void Update()
        {
            if (_boundScanner == null)
            {
                FindAndBindScanner();
                if (_boundScanner == null)
                {
                    RenderIdleScreen();
                    return;
                }
            }

            RenderScannerState();
        }

        private void FindAndBindScanner()
        {
            if (_boundScanner != null) return;

            // Look in parents (held by player)
            _boundScanner = GetComponentInParent<NetworkFieldScanner>();
            if (_boundScanner == null)
            {
                // Fallback to local player
                var players = FindObjectsByType<NetworkFieldScanner>(FindObjectsInactive.Exclude);
                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i].Object != null && players[i].Object.HasInputAuthority)
                    {
                        _boundScanner = players[i];
                        break;
                    }
                }

                if (_boundScanner == null && players.Length > 0)
                {
                    _boundScanner = players[0];
                }
            }

            if (_boundScanner != null)
            {
                _boundScanner.LocalCoreResultReceived += HandleCoreResult;
                _boundScanner.LocalMotionResultReceived += HandleMotionResult;
                _boundScanner.LocalScanCleared += HandleScanCleared;
                _boundScanner.LocalModeChanged += HandleModeChanged;
            }
        }

        private void UnbindScanner()
        {
            if (_boundScanner != null)
            {
                _boundScanner.LocalCoreResultReceived -= HandleCoreResult;
                _boundScanner.LocalMotionResultReceived -= HandleMotionResult;
                _boundScanner.LocalScanCleared -= HandleScanCleared;
                _boundScanner.LocalModeChanged -= HandleModeChanged;
                _boundScanner = null;
            }
        }

        private void HandleModeChanged(FieldScannerMode mode)
        {
            RenderScannerState();
        }

        private void HandleCoreResult(CoreScanResult result)
        {
            if (scannerAudio != null)
            {
                scannerAudio.PlayScanPulse();
                scannerAudio.SetFeedbackSignal((int)result.SignalBars);
            }
        }

        private void HandleMotionResult(MotionScanResult result)
        {
            if (scannerAudio != null)
            {
                scannerAudio.PlayScanPulse();
                if (result.HasMotion && result.BlipCount > 0)
                {
                    // Use intensity of nearest blip
                    scannerAudio.SetFeedbackSignal((int)result.Blip0.Intensity);
                }
                else
                {
                    scannerAudio.StopFeedback();
                }
            }
        }

        private void HandleScanCleared()
        {
            if (scannerAudio != null)
            {
                scannerAudio.StopFeedback();
            }
        }

        private void RenderScannerState()
        {
            if (_boundScanner == null) return;

            FieldScannerMode mode = _boundScanner.CurrentMode;
            bool hasResult = _boundScanner.HasActiveResult;
            float cooldown = _boundScanner.LocalCooldownRemaining;

            // 1. Header
            string headerText = mode == FieldScannerMode.Core
                ? "FIELD SCANNER\n<color=#40E0D0>[ CORE MODE ]</color>"
                : "FIELD SCANNER\n<color=#FF7F50>[ MOTION MODE ]</color>";
            SetHeaderText(headerText);

            // 2. Radar & Signal
            if (mode == FieldScannerMode.Core)
            {
                if (hasResult && _boundScanner.CurrentCoreResult.HasTarget)
                {
                    var res = _boundScanner.CurrentCoreResult;
                    string arrow = GetDirectionArrow(res.Direction);
                    SetRadarText($"   ●\n    {arrow}\n   ▲");
                    SetSignalText($"SIGNAL {GetBarsString(res.SignalBars)}");
                }
                else if (hasResult)
                {
                    SetRadarText("\n   ▲\n");
                    SetSignalText("<color=#888888>NO SIGNAL</color>");
                }
                else
                {
                    SetRadarText("\n   ▲\n");
                    SetSignalText("<color=#666666>STANDBY</color>");
                }
            }
            else // Motion Mode
            {
                if (hasResult && _boundScanner.CurrentMotionResult.HasMotion)
                {
                    var res = _boundScanner.CurrentMotionResult;
                    SetRadarText(BuildMotionRadarString(res));
                    SetSignalText("<color=#FF4500>MOTION DETECTED</color>");
                }
                else if (hasResult)
                {
                    SetRadarText("\n   ▲\n");
                    SetSignalText("<color=#888888>NO MOTION DETECTED</color>");
                }
                else
                {
                    SetRadarText("\n   ▲\n");
                    SetSignalText("<color=#666666>STANDBY</color>");
                }
            }

            // 3. Status / Cooldown
            if (cooldown > 0.05f)
            {
                SetStatusText($"RECHARGING {cooldown:F1}s");
            }
            else
            {
                SetStatusText("<color=#00FF7F>SCAN READY</color>");
            }
        }

        private void RenderIdleScreen()
        {
            SetHeaderText("FIELD SCANNER\nOFFLINE");
            SetRadarText("\n   ▲\n");
            SetSignalText("NO LINK");
            SetStatusText("WAITING...");
        }

        private static string GetDirectionArrow(RelativeDirectionSector sector)
        {
            switch (sector)
            {
                case RelativeDirectionSector.Front: return "↑";
                case RelativeDirectionSector.FrontRight: return "↗";
                case RelativeDirectionSector.Right: return "→";
                case RelativeDirectionSector.BackRight: return "↘";
                case RelativeDirectionSector.Back: return "↓";
                case RelativeDirectionSector.BackLeft: return "↙";
                case RelativeDirectionSector.Left: return "←";
                case RelativeDirectionSector.FrontLeft: return "↖";
                default: return "↑";
            }
        }

        private static string GetBarsString(ScannerSignalStrength bars)
        {
            switch (bars)
            {
                case ScannerSignalStrength.Bar4: return "▮▮▮▮";
                case ScannerSignalStrength.Bar3: return "▮▮▮□";
                case ScannerSignalStrength.Bar2: return "▮▮□□";
                case ScannerSignalStrength.Bar1: return "▮□□□";
                default: return "□□□□";
            }
        }

        private static string BuildMotionRadarString(MotionScanResult result)
        {
            // Up to 3 blip arrows
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < result.BlipCount; i++)
            {
                var blip = result.GetBlip(i);
                if (blip.IsValid)
                {
                    sb.Append("● ");
                    sb.Append(GetDirectionArrow(blip.Direction));
                    sb.Append("  ");
                }
            }
            return $"{sb}\n   ▲";
        }

        private void SetHeaderText(string text)
        {
            if (tmpHeader != null) tmpHeader.text = text;
            if (legacyHeader != null) legacyHeader.text = StripRichTags(text);
        }

        private void SetRadarText(string text)
        {
            if (tmpRadar != null) tmpRadar.text = text;
            if (legacyRadar != null) legacyRadar.text = StripRichTags(text);
        }

        private void SetSignalText(string text)
        {
            if (tmpSignal != null) tmpSignal.text = text;
            if (legacySignal != null) legacySignal.text = StripRichTags(text);
        }

        private void SetStatusText(string text)
        {
            if (tmpStatus != null) tmpStatus.text = text;
            if (legacyStatus != null) legacyStatus.text = StripRichTags(text);
        }

        private static string StripRichTags(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return System.Text.RegularExpressions.Regex.Replace(input, "<.*?>", string.Empty);
        }
    }
}
