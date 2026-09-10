using System.Text;
using EchoProtocol.Tools.Scanner;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.HUD
{
    [DisallowMultipleComponent]
    public class HUDFieldScanner : MonoBehaviour
    {
        public static HUDFieldScanner Instance { get; private set; }

        [Header("Canvas & Group")]
        [SerializeField] private Canvas parentCanvas;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("UI Text References")]
        [SerializeField] private Text titleText;
        [SerializeField] private Text modeBadgeText;
        [SerializeField] private Text radarText;
        [SerializeField] private Text signalBarsText;
        [SerializeField] private Text signalDetailText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text controlsText;

        [Header("UI Visual References")]
        [SerializeField] private Image panelBackground;
        [SerializeField] private Outline panelOutline;
        [SerializeField] private Image radarBoxBackground;
        [SerializeField] private Outline radarBoxOutline;

        [Header("Audio")]
        [SerializeField] private FieldScannerAudio scannerAudio;

        [Header("Settings")]
        [SerializeField] private float fadeSpeed = 16f;

        private NetworkFieldScanner _boundScanner;
        private float _targetAlpha;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            _targetAlpha = 0f;

            if (scannerAudio == null)
            {
                scannerAudio = GetComponent<FieldScannerAudio>();
                if (scannerAudio == null) scannerAudio = gameObject.AddComponent<FieldScannerAudio>();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            UnbindScanner();
        }

        public void SetVisible(bool visible)
        {
            _targetAlpha = visible ? 1f : 0f;
        }

        private void Update()
        {
            if (_boundScanner == null)
            {
                FindAndBindLocalScanner();
            }

            // Scanner is equipped when tool slot has FieldScanner and player is not carrying an Energy Core
            bool isEquipped = _boundScanner != null 
                && _boundScanner.IsScannerEquipped() 
                && !_boundScanner.IsCarryingCore();

            _targetAlpha = isEquipped ? 1f : 0f;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, _targetAlpha, Time.deltaTime * fadeSpeed);
                bool isVisible = canvasGroup.alpha > 0.005f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;

                if (!isVisible && _targetAlpha <= 0f)
                {
                    if (scannerAudio != null) scannerAudio.StopFeedback();
                    return;
                }
            }

            RenderScannerState();
        }

        public void BindScanner(NetworkFieldScanner scanner)
        {
            if (_boundScanner == scanner) return;

            UnbindScanner();
            _boundScanner = scanner;

            if (_boundScanner != null)
            {
                _boundScanner.LocalCoreResultReceived += HandleCoreResult;
                _boundScanner.LocalMotionResultReceived += HandleMotionResult;
                _boundScanner.LocalScanCleared += HandleScanCleared;
                _boundScanner.LocalModeChanged += HandleModeChanged;
            }
        }

        public void UnbindScanner()
        {
            if (_boundScanner != null)
            {
                _boundScanner.LocalCoreResultReceived -= HandleCoreResult;
                _boundScanner.LocalMotionResultReceived -= HandleMotionResult;
                _boundScanner.LocalScanCleared -= HandleScanCleared;
                _boundScanner.LocalModeChanged -= HandleModeChanged;
                _boundScanner = null;
            }

            if (scannerAudio != null)
            {
                scannerAudio.StopFeedback();
            }
        }

        private void FindAndBindLocalScanner()
        {
            var scanners = FindObjectsByType<NetworkFieldScanner>(FindObjectsInactive.Exclude);
            for (int i = 0; i < scanners.Length; i++)
            {
                var s = scanners[i];
                if (s.Object != null && s.Object.IsValid)
                {
                    if (s.Object.HasInputAuthority)
                    {
                        BindScanner(s);
                        return;
                    }
                }
                else
                {
                    // Local / offline play
                    BindScanner(s);
                    return;
                }
            }
        }

        private void HandleCoreResult(CoreScanResult result)
        {
            if (scannerAudio != null)
            {
                scannerAudio.PlayScanPulse();
                scannerAudio.SetFeedbackSignal((int)result.SignalBars);
            }
            RenderScannerState();
        }

        private void HandleMotionResult(MotionScanResult result)
        {
            if (scannerAudio != null)
            {
                scannerAudio.PlayScanPulse();
                if (result.HasMotion && result.BlipCount > 0)
                {
                    scannerAudio.SetFeedbackSignal((int)result.Blip0.Intensity);
                }
                else
                {
                    scannerAudio.StopFeedback();
                }
            }
            RenderScannerState();
        }

        private void HandleScanCleared()
        {
            if (scannerAudio != null)
            {
                scannerAudio.StopFeedback();
            }
            RenderScannerState();
        }

        private void HandleModeChanged(FieldScannerMode mode)
        {
            RenderScannerState();
        }

        private void RenderScannerState()
        {
            if (_boundScanner == null)
            {
                RenderIdleState();
                return;
            }

            FieldScannerMode mode = _boundScanner.CurrentMode;
            bool isActive = _boundScanner.IsScanActive;
            float activeRemaining = _boundScanner.ActiveRemainingTime;
            float cooldown = _boundScanner.LocalCooldownRemaining;
            bool hasResult = isActive && _boundScanner.HasActiveResult;

            // 1. Header & Mode
            if (mode == FieldScannerMode.Core)
            {
                if (titleText != null) titleText.text = "FIELD SCANNER";
                if (modeBadgeText != null)
                {
                    modeBadgeText.text = "[ CHẾ ĐỘ NÕI NĂNG LƯỢNG ]";
                    modeBadgeText.color = new Color(0.15f, 0.95f, 0.85f, 1f); // Turquoise
                }
                if (panelOutline != null) panelOutline.effectColor = new Color(0f, 0.85f, 1f, 0.65f);
            }
            else // Motion Mode
            {
                if (titleText != null) titleText.text = "FIELD SCANNER";
                if (modeBadgeText != null)
                {
                    modeBadgeText.text = "[ PHÁT HIỆN CHUYỂN ĐỘNG ]";
                    modeBadgeText.color = new Color(1f, 0.45f, 0.2f, 1f); // Coral
                }
                if (panelOutline != null) panelOutline.effectColor = new Color(1f, 0.45f, 0.2f, 0.65f);
            }

            // 2. Radar View
            if (!isActive)
            {
                if (scannerAudio != null)
                {
                    scannerAudio.StopFeedback();
                }

                if (cooldown > 0.05f)
                {
                    if (radarText != null)
                    {
                        radarText.text = "\n<color=#555555>●</color> <color=#888888>▲</color> <color=#555555>●</color>\n";
                        radarText.color = new Color(0.6f, 0.6f, 0.6f, 0.6f);
                    }
                    if (signalBarsText != null)
                    {
                        signalBarsText.text = $"<color=#FFA500>● ĐANG HỒI NĂNG LƯỢNG ({cooldown:F0}s)</color>";
                    }
                    if (signalDetailText != null)
                    {
                        signalDetailText.text = $"HẾT THỜI GIAN QUÉT (10s)\nCHỜ TÁI KÍCH HOẠT: {cooldown:F0}s";
                        signalDetailText.color = new Color(0.75f, 0.7f, 0.6f, 0.8f);
                    }
                }
                else
                {
                    float maxRange = mode == FieldScannerMode.Core ? _boundScanner.Tuning.CoreRange : _boundScanner.Tuning.MotionRange;
                    if (radarText != null)
                    {
                        radarText.text = (mode == FieldScannerMode.Core)
                            ? "\n<color=#00E5FF>●</color> <color=#FFFFFF>▲</color> <color=#00E5FF>●</color>\n"
                            : "\n<color=#FF5522>●</color> <color=#FFFFFF>▲</color> <color=#FF5522>●</color>\n";
                        radarText.color = (mode == FieldScannerMode.Core)
                            ? new Color(0.4f, 0.7f, 0.8f, 0.8f)
                            : new Color(0.8f, 0.5f, 0.4f, 0.8f);
                    }
                    if (signalBarsText != null)
                    {
                        signalBarsText.text = "<color=#00FF7F>● SẴN SÀNG KÍCH HOẠT</color>";
                    }
                    if (signalDetailText != null)
                    {
                        signalDetailText.text = "NHẤN [CHUỘT TRÁI] ĐỂ BẬT QUÉT 10s\nHỆ THỐNG SẴN SÀNG";
                        signalDetailText.color = (mode == FieldScannerMode.Core)
                            ? new Color(0.15f, 0.95f, 0.85f, 1f)
                            : new Color(1f, 0.5f, 0.2f, 1f);
                    }
                }
            }
            else if (mode == FieldScannerMode.Core)
            {
                if (hasResult && _boundScanner.CurrentCoreResult.HasTarget)
                {
                    var res = _boundScanner.CurrentCoreResult;
                    string arrow = GetDirectionArrow(res.Direction);
                    if (radarText != null)
                    {
                        radarText.text = BuildRealtimeCoreRadarString(res.Direction, res.SignalBars);
                        radarText.color = Color.white;
                    }

                    if (signalBarsText != null)
                    {
                        signalBarsText.text = GetColoredBarsString(res.SignalBars);
                    }
                    if (signalDetailText != null)
                    {
                        string strength = GetSignalStrengthVietnamese(res.SignalBars);
                        signalDetailText.text = $"TÍN HIỆU NÕI: {strength}\nHƯỚNG: {GetSectorVietnamese(res.Direction)} [{arrow}]";
                        signalDetailText.color = new Color(0f, 0.95f, 1f, 1f);
                    }

                    if (scannerAudio != null)
                    {
                        scannerAudio.SetFeedbackSignal((int)res.SignalBars);
                    }
                }
                else
                {
                    if (radarText != null)
                    {
                        radarText.text = "\n<color=#00E5FF>●</color> <color=#FFFFFF>▲</color> <color=#00E5FF>●</color>\n";
                        radarText.color = new Color(0.4f, 0.7f, 0.8f, 0.8f);
                    }
                    if (signalBarsText != null)
                    {
                        signalBarsText.text = "<color=#666666>SIGNAL  □ □ □ □</color>";
                    }
                    if (signalDetailText != null)
                    {
                        signalDetailText.text = "ĐANG QUÉT TRỰC TIẾP\nKHÔNG PHÁT HIỆN TÍN HIỆU NÕI";
                        signalDetailText.color = new Color(0.6f, 0.75f, 0.85f, 0.9f);
                    }

                    if (scannerAudio != null)
                    {
                        scannerAudio.StopFeedback();
                    }
                }
            }
            else // Motion Mode
            {
                if (hasResult && _boundScanner.CurrentMotionResult.HasMotion)
                {
                    var res = _boundScanner.CurrentMotionResult;
                    if (radarText != null)
                    {
                        radarText.text = BuildMotionRadarString(res);
                        radarText.color = new Color(1f, 0.35f, 0.15f, 1f);
                    }
                    if (signalBarsText != null)
                    {
                        signalBarsText.text = "<color=#FF3300>● CẢNH BÁO MỤC TIÊU DI CHUYỂN</color>";
                    }
                    if (signalDetailText != null)
                    {
                        var b0 = res.GetBlip(0);
                        string arrow = GetDirectionArrow(b0.Direction);
                        string intensity = GetMotionIntensityVietnamese(b0.Intensity);
                        signalDetailText.text = $"PHÁT HIỆN {res.BlipCount} MỤC TIÊU [{intensity}]\nHƯỚNG: {GetSectorVietnamese(b0.Direction)} [{arrow}]";
                        signalDetailText.color = new Color(1f, 0.5f, 0.2f, 1f);
                    }

                    if (scannerAudio != null)
                    {
                        scannerAudio.SetFeedbackSignal((int)res.GetBlip(0).Intensity);
                    }
                }
                else
                {
                    if (radarText != null)
                    {
                        radarText.text = "\n<color=#FF5522>●</color> <color=#FFFFFF>▲</color> <color=#FF5522>●</color>\n";
                        radarText.color = new Color(0.8f, 0.5f, 0.4f, 0.8f);
                    }
                    if (signalBarsText != null)
                    {
                        signalBarsText.text = "<color=#666666>SIGNAL  □ □ □ □</color>";
                    }
                    if (signalDetailText != null)
                    {
                        signalDetailText.text = "RADAR CHUYỂN ĐỘNG\nKHÔNG CÓ CHUYỂN ĐỘNG";
                        signalDetailText.color = new Color(0.75f, 0.7f, 0.65f, 0.9f);
                    }

                    if (scannerAudio != null)
                    {
                        scannerAudio.StopFeedback();
                    }
                }
            }

            // 3. Cooldown & Active Status
            if (statusText != null)
            {
                if (isActive)
                {
                    statusText.text = $"<color=#00FF7F>● ĐANG QUÉT TRỰC TIẾP ({activeRemaining:F1}s)</color>";
                }
                else if (cooldown > 0.05f)
                {
                    statusText.text = $"<color=#FFA500>● ĐANG HỒI NĂNG LƯỢNG ({cooldown:F0}s)</color>";
                }
                else
                {
                    statusText.text = "<color=#00FF7F>● SẴN SÀNG KÍCH HOẠT (10s)</color>";
                }
            }

            // 4. Controls Hint
            if (controlsText != null)
            {
                if (isActive)
                {
                    controlsText.text = $"[Đang Quét {activeRemaining:F1}s]  •  [Chuột Phải] Đổi Chế Độ";
                }
                else if (cooldown > 0.05f)
                {
                    controlsText.text = $"[Chuột Phải] Đổi Chế Độ  •  Chờ hồi chiêu ({cooldown:F0}s)";
                }
                else
                {
                    controlsText.text = "[Chuột Trái] Kích Hoạt Quét 10s  •  [Chuột Phải] Đổi Chế Độ";
                }
            }
        }

        private void RenderIdleState()
        {
            if (titleText != null) titleText.text = "FIELD SCANNER";
            if (modeBadgeText != null) modeBadgeText.text = "[ NGOẠI TUYẾN ]";
            if (radarText != null) radarText.text = "\n▲\n";
            if (signalBarsText != null) signalBarsText.text = "<color=#666666>SIGNAL  □ □ □ □</color>";
            if (signalDetailText != null) signalDetailText.text = "CHƯA KẾT NỐI";
            if (statusText != null) statusText.text = "<color=#888888>ĐANG CHỜ...</color>";
        }

        private static string BuildRealtimeCoreRadarString(RelativeDirectionSector sector, ScannerSignalStrength bars)
        {
            string arrow = GetDirectionArrow(sector);
            string barColor = "#00FFFF";
            if (bars == ScannerSignalStrength.Bar4) barColor = "#00FF7F";
            else if (bars == ScannerSignalStrength.Bar3) barColor = "#00E5FF";
            else if (bars == ScannerSignalStrength.Bar2) barColor = "#FFD700";

            return $"<color={barColor}>[ MỤC TIÊU NÕI ]</color>\n<size=34>{arrow}</size>\n<color=#FFFFFF>▲ (BẠN)</color>";
        }

        private static string GetSectorVietnamese(RelativeDirectionSector sector)
        {
            switch (sector)
            {
                case RelativeDirectionSector.Front: return "Trước Mặt";
                case RelativeDirectionSector.FrontRight: return "Trước Phải";
                case RelativeDirectionSector.Right: return "Bên Phải";
                case RelativeDirectionSector.BackRight: return "Sau Phải";
                case RelativeDirectionSector.Back: return "Phía Sau";
                case RelativeDirectionSector.BackLeft: return "Sau Trái";
                case RelativeDirectionSector.Left: return "Bên Trái";
                case RelativeDirectionSector.FrontLeft: return "Trước Trái";
                default: return "Trước Mặt";
            }
        }

        private static string GetSignalStrengthVietnamese(ScannerSignalStrength bars)
        {
            switch (bars)
            {
                case ScannerSignalStrength.Bar4: return "CỰC MẠNH (RẤT GẦN)";
                case ScannerSignalStrength.Bar3: return "MẠNH (GẦN)";
                case ScannerSignalStrength.Bar2: return "TRUNG BÌNH";
                case ScannerSignalStrength.Bar1: return "YẾU (XA)";
                default: return "KHÔNG CÓ TÍN HIỆU";
            }
        }

        private static string GetMotionIntensityVietnamese(MotionBlipIntensity intensity)
        {
            switch (intensity)
            {
                case MotionBlipIntensity.Critical: return "CỰC GẦN";
                case MotionBlipIntensity.Strong: return "GẦN";
                case MotionBlipIntensity.Medium: return "TRUNG BÌNH";
                case MotionBlipIntensity.Weak: return "XA";
                default: return "KHÔNG RÕ";
            }
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

        private static string GetColoredBarsString(ScannerSignalStrength bars)
        {
            switch (bars)
            {
                case ScannerSignalStrength.Bar4:
                    return "<color=#00FF7F>SIGNAL  ▮ ▮ ▮ ▮</color>";
                case ScannerSignalStrength.Bar3:
                    return "<color=#00E5FF>SIGNAL  ▮ ▮ ▮ <color=#444444>□</color></color>";
                case ScannerSignalStrength.Bar2:
                    return "<color=#FFD700>SIGNAL  ▮ ▮ <color=#444444>□ □</color></color>";
                case ScannerSignalStrength.Bar1:
                    return "<color=#FF8C00>SIGNAL  ▮ <color=#444444>□ □ □</color></color>";
                default:
                    return "<color=#666666>SIGNAL  □ □ □ □</color>";
            }
        }

        private static string BuildMotionRadarString(MotionScanResult result)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < result.BlipCount; i++)
            {
                var blip = result.GetBlip(i);
                if (blip.IsValid)
                {
                    sb.Append("<color=#FF3300>●</color> ");
                    sb.Append(GetDirectionArrow(blip.Direction));
                    sb.Append("  ");
                }
            }
            return $"{sb}\n<color=#FFFFFF>▲</color>";
        }

        public static HUDFieldScanner EnsureInstance()
        {
            if (Instance != null) return Instance;

            Instance = FindAnyObjectByType<HUDFieldScanner>();
            if (Instance != null) return Instance;

            // Look for existing GameplayHUD_Canvas
            GameObject hudCanvas = GameObject.Find("GameplayHUD_Canvas");
            Transform parentTransform = hudCanvas != null ? hudCanvas.transform : null;

            // Load prefab if present
            GameObject prefab = Resources.Load<GameObject>("PF_FieldScanner_HUD");
            if (prefab != null)
            {
                GameObject go = Instantiate(prefab, parentTransform);
                Instance = go.GetComponent<HUDFieldScanner>();
                if (Instance != null) return Instance;
            }

            // Fallback: build ScreenSpace Overlay dynamically
            Instance = CreateDefaultScreenHUD(parentTransform);
            return Instance;
        }

        public static HUDFieldScanner CreateDefaultScreenHUD(Transform parent)
        {
            Transform hudParent = parent;
            if (hudParent == null)
            {
                GameObject canvasGo = new GameObject("FieldScanner_ScreenHUD_Canvas");
                Canvas canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 95;

                CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                canvasGo.AddComponent<GraphicRaycaster>();
                hudParent = canvasGo.transform;
            }

            // Main Panel (Middle-Right of screen)
            GameObject panelGo = new GameObject("FieldScanner_HUD_Panel");
            panelGo.transform.SetParent(hudParent, false);

            RectTransform rt = panelGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-35f, 0f);
            rt.sizeDelta = new Vector2(320f, 430f);

            CanvasGroup cg = panelGo.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            Image bg = panelGo.AddComponent<Image>();
            bg.sprite = HUDTextureUtility.RoundedBox;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.02f, 0.05f, 0.09f, 0.90f);

            Outline outline = panelGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0.85f, 1f, 0.65f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // 1. Header (Top)
            Text title = CreateText("Title", panelGo.transform, "FIELD SCANNER", font, 18, FontStyle.Bold, new Color(0f, 0.95f, 1f, 1f));
            RectTransform titleRt = title.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -16f);
            titleRt.sizeDelta = new Vector2(-20f, 26f);
            title.alignment = TextAnchor.MiddleCenter;

            Text mode = CreateText("ModeBadge", panelGo.transform, "[ CHẾ ĐỘ NÕI NĂNG LƯỢNG ]", font, 14, FontStyle.Bold, new Color(0.15f, 0.95f, 0.85f, 1f));
            RectTransform modeRt = mode.GetComponent<RectTransform>();
            modeRt.anchorMin = new Vector2(0f, 1f);
            modeRt.anchorMax = new Vector2(1f, 1f);
            modeRt.pivot = new Vector2(0.5f, 1f);
            modeRt.anchoredPosition = new Vector2(0f, -44f);
            modeRt.sizeDelta = new Vector2(-20f, 22f);
            mode.alignment = TextAnchor.MiddleCenter;

            // Divider 1
            CreateDivider("Divider1", panelGo.transform, new Vector2(0f, -72f), new Color(0f, 0.85f, 1f, 0.35f));

            // 2. Radar Box (Center)
            GameObject radarBox = new GameObject("RadarBox");
            radarBox.transform.SetParent(panelGo.transform, false);
            RectTransform radarBoxRt = radarBox.AddComponent<RectTransform>();
            radarBoxRt.anchorMin = new Vector2(0.5f, 1f);
            radarBoxRt.anchorMax = new Vector2(0.5f, 1f);
            radarBoxRt.pivot = new Vector2(0.5f, 1f);
            radarBoxRt.anchoredPosition = new Vector2(0f, -82f);
            radarBoxRt.sizeDelta = new Vector2(270f, 140f);

            Image radarBoxBg = radarBox.AddComponent<Image>();
            radarBoxBg.sprite = HUDTextureUtility.RoundedBox;
            radarBoxBg.type = Image.Type.Sliced;
            radarBoxBg.color = new Color(0.01f, 0.02f, 0.05f, 0.95f);

            Outline radarOutline = radarBox.AddComponent<Outline>();
            radarOutline.effectColor = new Color(0f, 0.8f, 1f, 0.35f);
            radarOutline.effectDistance = new Vector2(1f, -1f);

            Text radar = CreateText("RadarText", radarBox.transform, "\n▲\n", font, 24, FontStyle.Bold, Color.white);
            RectTransform radarRt = radar.GetComponent<RectTransform>();
            radarRt.anchorMin = Vector2.zero;
            radarRt.anchorMax = Vector2.one;
            radarRt.sizeDelta = Vector2.zero;
            radar.alignment = TextAnchor.MiddleCenter;

            // 3. Signal Strength & Detail
            Text signalBars = CreateText("SignalBars", panelGo.transform, "SIGNAL  □ □ □ □", font, 20, FontStyle.Bold, Color.white);
            RectTransform signalBarsRt = signalBars.GetComponent<RectTransform>();
            signalBarsRt.anchorMin = new Vector2(0f, 1f);
            signalBarsRt.anchorMax = new Vector2(1f, 1f);
            signalBarsRt.pivot = new Vector2(0.5f, 1f);
            signalBarsRt.anchoredPosition = new Vector2(0f, -232f);
            signalBarsRt.sizeDelta = new Vector2(-20f, 26f);
            signalBars.alignment = TextAnchor.MiddleCenter;

            Text signalDetail = CreateText("SignalDetail", panelGo.transform, "CHỜ PHÁT XUNG QUÉT", font, 14, FontStyle.Normal, new Color(0.6f, 0.75f, 0.85f, 0.9f));
            RectTransform signalDetailRt = signalDetail.GetComponent<RectTransform>();
            signalDetailRt.anchorMin = new Vector2(0f, 1f);
            signalDetailRt.anchorMax = new Vector2(1f, 1f);
            signalDetailRt.pivot = new Vector2(0.5f, 1f);
            signalDetailRt.anchoredPosition = new Vector2(0f, -260f);
            signalDetailRt.sizeDelta = new Vector2(-20f, 22f);
            signalDetail.alignment = TextAnchor.MiddleCenter;

            // Divider 2
            CreateDivider("Divider2", panelGo.transform, new Vector2(0f, -292f), new Color(0f, 0.85f, 1f, 0.25f));

            // 4. Status (Cooldown) & Controls
            Text status = CreateText("Status", panelGo.transform, "<color=#00FF7F>● SẴN SÀNG QUÉT</color>", font, 16, FontStyle.Bold, Color.white);
            RectTransform statusRt = status.GetComponent<RectTransform>();
            statusRt.anchorMin = new Vector2(0f, 1f);
            statusRt.anchorMax = new Vector2(1f, 1f);
            statusRt.pivot = new Vector2(0.5f, 1f);
            statusRt.anchoredPosition = new Vector2(0f, -304f);
            statusRt.sizeDelta = new Vector2(-20f, 24f);
            status.alignment = TextAnchor.MiddleCenter;

            Text controls = CreateText("Controls", panelGo.transform, "[Chuột Trái] Quét\n[Chuột Phải] Đổi Chế Độ", font, 12, FontStyle.Normal, new Color(0.65f, 0.75f, 0.85f, 0.8f));
            RectTransform controlsRt = controls.GetComponent<RectTransform>();
            controlsRt.anchorMin = new Vector2(0f, 1f);
            controlsRt.anchorMax = new Vector2(1f, 1f);
            controlsRt.pivot = new Vector2(0.5f, 1f);
            controlsRt.anchoredPosition = new Vector2(0f, -336f);
            controlsRt.sizeDelta = new Vector2(-20f, 36f);
            controls.alignment = TextAnchor.MiddleCenter;

            // Add and bind script
            HUDFieldScanner hud = panelGo.AddComponent<HUDFieldScanner>();
            hud.canvasGroup = cg;
            hud.titleText = title;
            hud.modeBadgeText = mode;
            hud.radarText = radar;
            hud.signalBarsText = signalBars;
            hud.signalDetailText = signalDetail;
            hud.statusText = status;
            hud.controlsText = controls;
            hud.panelBackground = bg;
            hud.panelOutline = outline;
            hud.radarBoxBackground = radarBoxBg;
            hud.radarBoxOutline = radarOutline;

            return hud;
        }

        private static Text CreateText(string name, Transform parent, string defaultContent, Font font, int fontSize, FontStyle fontStyle, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Text t = go.AddComponent<Text>();
            t.text = defaultContent;
            if (font != null) t.font = font;
            t.fontSize = fontSize;
            t.fontStyle = fontStyle;
            t.color = color;
            t.supportRichText = true;
            t.raycastTarget = false;
            return t;
        }

        private static GameObject CreateDivider(string name, Transform parent, Vector2 anchoredPos, Color color)
        {
            GameObject div = new GameObject(name);
            div.transform.SetParent(parent, false);
            RectTransform rt = div.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(270f, 1.5f);

            Image img = div.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return div;
        }
    }
}

