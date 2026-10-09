using EchoProtocol.Networking;
using EchoProtocol.Tools.Scanner;
using UnityEngine;
using UnityEngine.Serialization;
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
        [FormerlySerializedAs("radarText")]
        [SerializeField] private Text scanTimerText;
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
        [SerializeField] private Text detectedText;
        [SerializeField] private ScannerRadarGraphic radarGraphic;
        private float _nextRenderTime;
        private float _emptySince = -1f;
        private Camera _viewCamera;
        private bool _wasEquipped;
        private float _modeHintUntil;

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
            if (radarBoxBackground != null) ConfigureLayout();

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
            if (_boundScanner != null && !CanBindScanner(_boundScanner))
            {
                UnbindScanner();
            }

            if (_boundScanner == null)
            {
                FindAndBindLocalScanner();
            }

            // Scanner is equipped when tool slot has FieldScanner and player is not carrying an Energy Core
            bool isEquipped = false;
            if (_boundScanner != null)
            {
                isEquipped = _boundScanner.IsScannerEquipped() && !_boundScanner.IsCarryingCore();
            }

            if (isEquipped && !_wasEquipped) _modeHintUntil = Time.unscaledTime + 6f;
            _wasEquipped = isEquipped;
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

            if (Time.unscaledTime >= _nextRenderTime)
            {
                _nextRenderTime = Time.unscaledTime + 0.05f;
                RenderScannerState();
            }
        }

        public void BindScanner(NetworkFieldScanner scanner)
        {
            if (scanner != null && !CanBindScanner(scanner)) return;
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
                if (CanBindScanner(s))
                {
                    BindScanner(s);
                    return;
                }
            }
        }

        private static bool CanBindScanner(NetworkFieldScanner scanner)
        {
            if (scanner == null)
            {
                return false;
            }

            if (scanner.Object == null
                || !scanner.Object.IsValid
                || scanner.Runner == null
                || !scanner.Runner.IsRunning)
            {
                return true;
            }

            var playerState = scanner.GetComponent<LobbyPlayerState>();
            return playerState != null
                && playerState.Object != null
                && playerState.Object.IsValid
                && playerState.Object.HasInputAuthority
                && playerState.IsGameplayPlayer;
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
            _modeHintUntil = 0f;
            RenderScannerState();
        }

        private void RenderScannerState()
        {
            bool connected = _boundScanner != null;
            bool motion = connected && _boundScanner.CurrentMode == FieldScannerMode.Motion;
            string coreTargetLabel = connected
                ? _boundScanner.CoreTargetLabel
                : "LÕI NĂNG LƯỢNG";
            bool active = connected && _boundScanner.IsScanActive;
            bool hasResult = active && _boundScanner.HasActiveResult;
            float cooldown = connected ? _boundScanner.LocalCooldownRemaining : 0f;
            float activeRemaining = connected ? _boundScanner.ActiveRemainingTime : 0f;
            var offsets = hasResult ? _boundScanner.RadarOffsets : null;
            int count = offsets != null ? offsets.Count : 0;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++) nearest = Mathf.Min(nearest, offsets[i].magnitude);
            Color accent = motion ? HUDPresentationStyle.Danger : HUDPresentationStyle.Accent;

            SetText(titleText, motion ? "STALKER" : coreTargetLabel);
            SetText(modeBadgeText, "[Chuột phải] Đổi chế độ");
            SetText(controlsText, "");
            SetText(detectedText, motion ? $"<size=18>{count}</size>\nPHÁT HIỆN" : $"<size=18>{count}</size>  {coreTargetLabel}");
            SetText(signalDetailText, "GẦN NHẤT\n<size=16>" + (count > 0 ? $"{nearest:F0}m" : "—") + "</size>");
            int bars = connected && count > 0 ? (int)FieldScannerCoreDetector.ResolveSignalBars(nearest, _boundScanner.Tuning) : 0;
            if (motion)
            {
                int intensity = count > 0 ? (int)_boundScanner.CurrentMotionResult.Blip0.Intensity : 0;
                string signal = "";
                const string glyphs = "▂▄▆█";
                for (int i = 0; i < 4; i++) signal += (i < intensity ? glyphs[i].ToString() : $"<color=#503E3A>{glyphs[i]}</color>") + " ";
                SetText(signalBarsText, "CƯỜNG ĐỘ\n<size=16>" + (count > 0 ? signal : "—") + "</size>");
            }
            else
            {
                string signal = "";
                const string glyphs = "▂▄▆█";
                for (int i = 0; i < 4; i++) signal += (i < bars ? glyphs[i].ToString() : $"<color=#3E5050>{glyphs[i]}</color>") + " ";
                SetText(signalBarsText, "TÍN HIỆU\n<size=16>" + signal + "</size>");
            }

            if (!active || !hasResult || count > 0) _emptySince = -1f;
            else if (_emptySince < 0f) _emptySince = Time.unscaledTime;
            if (scanTimerText != null)
            {
                if (!connected) SetText(scanTimerText, "MẤT KẾT NỐI");
                else if (active) SetText(scanTimerText, $"ĐANG QUÉT  {Mathf.Max(1, Mathf.CeilToInt(activeRemaining)):00}s");
                else if (cooldown > 0.05f) SetText(scanTimerText, $"HỒI  {Mathf.CeilToInt(cooldown):00}s");
                else SetText(scanTimerText, "SẴN SÀNG");
            }

            string status = !connected ? "ĐANG CHỜ KẾT NỐI" : active
                ? count > 0 ? "PHÁT HIỆN TÍN HIỆU"
                    : _emptySince >= 0f && Time.unscaledTime - _emptySince < 1.2f ? "KHÔNG CÓ TÍN HIỆU" : "ĐANG QUÉT..."
                : cooldown > 0.05f ? "MÁY QUÉT ĐANG HỒI"
                : Time.unscaledTime < _modeHintUntil ? "[Chuột trái] Quét · [Chuột phải] Đổi chế độ"
                : "[Chuột trái] Quét";
            SetText(statusText, status);
            if (titleText != null) titleText.color = accent;
            if (detectedText != null) detectedText.color = accent;
            if (signalBarsText != null) signalBarsText.color = accent;
            if (signalDetailText != null) signalDetailText.color = accent;
            if (panelOutline != null) panelOutline.effectColor = new Color(accent.r * 0.15f, accent.g * 0.15f, accent.b * 0.15f, 0.65f);

            if (_viewCamera == null) _viewCamera = Camera.main;
            float heading = _viewCamera != null ? _viewCamera.transform.eulerAngles.y : connected ? _boundScanner.transform.eulerAngles.y : 0f;
            if (radarGraphic != null)
                radarGraphic.Present(offsets, connected ? (motion ? _boundScanner.Tuning.MotionRange : _boundScanner.Tuning.CoreRange) : 1f, heading, motion, active);
            if (scannerAudio != null)
            {
                if (count == 0) scannerAudio.StopFeedback();
                else scannerAudio.SetFeedbackSignal(motion ? (int)_boundScanner.CurrentMotionResult.Blip0.Intensity : (int)_boundScanner.CurrentCoreResult.SignalBars);
            }
        }

        private static void SetText(Text label, string value)
        {
            if (label != null && label.text != value) label.text = value;
        }

        public static HUDFieldScanner EnsureInstance()
        {
            if (Instance != null) return Instance;
            Instance = FindAnyObjectByType<HUDFieldScanner>();
            if (Instance != null) return Instance;
            var manager = FindAnyObjectByType<GameplayHUDManager>();
            Canvas canvas = manager != null ? manager.GetComponentInParent<Canvas>() : null;
            Transform parent = canvas != null ? canvas.transform : CreateCanvas();
            var prefab = Resources.Load<GameObject>("PF_FieldScanner_HUD");
            Instance = prefab != null ? Instantiate(prefab, parent).GetComponent<HUDFieldScanner>() : CreateDefaultScreenHUD(parent);
            return Instance;
        }

        private static Transform CreateCanvas()
        {
            var go = new GameObject("FieldScanner_ScreenHUD_Canvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return go.transform;
        }

        public static HUDFieldScanner CreateDefaultScreenHUD(Transform parent)
        {
            var go = new GameObject("FieldScanner_HUD_Panel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Outline));
            go.transform.SetParent(parent != null ? parent : CreateCanvas(), false);
            var hud = go.AddComponent<HUDFieldScanner>();
            hud.canvasGroup = go.GetComponent<CanvasGroup>();
            hud.panelBackground = go.GetComponent<Image>();
            hud.panelOutline = go.GetComponent<Outline>();
            hud.ConfigureLayout();
            return hud;
        }

        // Also used to migrate existing prefab children in place: preserve object IDs and references.
        public void ConfigureLayout()
        {
            var root = (RectTransform)transform;
            root.anchorMin = root.anchorMax = root.pivot = Vector2.one;
            root.anchoredPosition = new Vector2(-32f, -276f);
            root.sizeDelta = new Vector2(380f, 390f);
            parentCanvas = GetComponentInParent<Canvas>();
            if (panelBackground != null)
            {
                panelBackground.sprite = null;
                panelBackground.color = new Color(0.025f, 0.035f, 0.037f, 0.88f);
                panelBackground.raycastTarget = false;
            }
            if (panelOutline != null) panelOutline.effectDistance = new Vector2(1f, -1f);
            titleText = Label(titleText, "Title", transform, 14, TextAnchor.MiddleLeft);
            modeBadgeText = Label(modeBadgeText, "ModeBadge", transform, 11, TextAnchor.MiddleCenter);
            controlsText = Label(controlsText, "Controls", transform, 10, TextAnchor.MiddleRight);
            Place(titleText.rectTransform, 0f, 0.45f, 7, 28, 10, 0);
            Place(modeBadgeText.rectTransform, 0.45f, 1f, 9, 22, 0, 10);
            Place(controlsText.rectTransform, 1f, 1f, 7, 28, 0, 0);
            var badge = modeBadgeText.transform.Find("KeyBadge");
            if (badge == null)
            {
                var go = new GameObject("KeyBadge", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(modeBadgeText.transform, false);
                badge = go.transform;
            }
            var badgeImage = badge.GetComponent<Image>();
            badgeImage.color = new Color(0.65f, 0.73f, 0.72f, 0.15f);
            badgeImage.raycastTarget = false;
            var badgeRect = (RectTransform)badge;
            badgeRect.anchorMin = Vector2.zero; badgeRect.anchorMax = Vector2.one;
            badgeRect.offsetMin = badgeRect.offsetMax = Vector2.zero;

            if (radarBoxBackground == null)
            {
                var go = new GameObject("RadarBox", typeof(RectTransform), typeof(Image), typeof(Outline));
                go.transform.SetParent(transform, false);
                radarBoxBackground = go.GetComponent<Image>();
                radarBoxOutline = go.GetComponent<Outline>();
            }
            RectTransform radar = radarBoxBackground.rectTransform;
            if (radar.GetComponent<Canvas>() == null) radar.gameObject.AddComponent<Canvas>();
            radar.anchorMin = radar.anchorMax = new Vector2(0.5f, 1f);
            radar.pivot = new Vector2(0.5f, 1f);
            radar.anchoredPosition = new Vector2(0, -36);
            radar.sizeDelta = new Vector2(292, 292);
            radarBoxBackground.sprite = null;
            radarBoxBackground.color = new Color(0.02f, 0.035f, 0.038f, 0.85f);
            radarBoxBackground.raycastTarget = false;
            if (radarBoxOutline != null) radarBoxOutline.effectColor = new Color(0.05f, 0.08f, 0.08f, 0.3f);
            if (radarGraphic == null)
            {
                var go = new GameObject("RadarGraphic", typeof(RectTransform), typeof(ScannerRadarGraphic));
                go.transform.SetParent(radar, false);
                radarGraphic = go.GetComponent<ScannerRadarGraphic>();
            }
            radarGraphic.raycastTarget = false;
            if (radarGraphic.GetComponent<CanvasRenderer>() == null) radarGraphic.gameObject.AddComponent<CanvasRenderer>();
            radarGraphic.rectTransform.anchorMin = Vector2.zero;
            radarGraphic.rectTransform.anchorMax = Vector2.one;
            radarGraphic.rectTransform.offsetMin = radarGraphic.rectTransform.offsetMax = Vector2.zero;
            scanTimerText = Label(scanTimerText, "ScanTimer", radar, 11, TextAnchor.MiddleLeft);
            Place(scanTimerText.rectTransform, 0f, 0.45f, 8f, 22f, 10f, 0f);
            statusText = Label(statusText, "Status", transform, 11, TextAnchor.MiddleCenter);
            Place(statusText.rectTransform, 0, 1, 326, 20, 8, 8);
            detectedText = Label(detectedText, "DetectedCount", transform, 11, TextAnchor.MiddleCenter);
            signalDetailText = Label(signalDetailText, "SignalDetail", transform, 11, TextAnchor.MiddleCenter);
            signalBarsText = Label(signalBarsText, "SignalBars", transform, 11, TextAnchor.MiddleCenter);
            Place(detectedText.rectTransform, 0, 1f / 3, 348, 42, 4, 4);
            Place(signalDetailText.rectTransform, 1f / 3, 2f / 3, 348, 42, 4, 4);
            Place(signalBarsText.rectTransform, 2f / 3, 1, 348, 42, 4, 4);
            Divider("Divider1", 0, 1, 35, 1);
            Divider("Divider2", 0, 1, 346, 1);
            Divider("FooterSeparator1", 1f / 3, 1f / 3, 354, 30);
            Divider("FooterSeparator2", 2f / 3, 2f / 3, 354, 30);
            RenderScannerState();
        }

        private static Text Label(Text existing, string name, Transform parent, int size, TextAnchor alignment)
        {
            if (existing == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Text));
                go.transform.SetParent(parent, false);
                existing = go.GetComponent<Text>();
            }
            if (existing.font == null) existing.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            existing.fontSize = size;
            existing.fontStyle = FontStyle.Normal;
            existing.alignment = alignment;
            existing.supportRichText = true;
            existing.horizontalOverflow = HorizontalWrapMode.Overflow;
            existing.verticalOverflow = VerticalWrapMode.Truncate;
            existing.color = new Color(0.76f, 0.82f, 0.82f);
            existing.raycastTarget = false;
            return existing;
        }

        private static void Place(RectTransform rt, float left, float right, float top, float height, float insetLeft, float insetRight)
        {
            rt.anchorMin = new Vector2(left, 1); rt.anchorMax = new Vector2(right, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(insetLeft, -top - height);
            rt.offsetMax = new Vector2(-insetRight, -top);
        }

        private void Divider(string name, float left, float right, float top, float height)
        {
            var child = transform.Find(name);
            if (child == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                child = go.transform;
            }
            Place((RectTransform)child, left, right, top, height, left == right ? -0.5f : 8, left == right ? -0.5f : 8);
            var image = child.GetComponent<Image>();
            image.color = new Color(0.5f, 0.65f, 0.65f, 0.25f);
            image.raycastTarget = false;
        }
    }
}
