using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.RelayA
{
    public sealed partial class RelayAUIController
    {
        [SerializeField] private RectTransform _stabilizationSurface;
        [SerializeField] private Slider[] _stabilizationSliders;
        [SerializeField] private TMP_Text[] _stabilizationControlValues;
        [SerializeField] private TMP_Text[] _stabilizationReadings;
        [SerializeField] private TMP_Text[] _stabilizationSafeLabels;
        [SerializeField] private Image[] _stabilizationGauges;
        [SerializeField] private Image[] _stabilizationSafeBands;
        [SerializeField] private TMP_Text _stabilizationStatus;
        [SerializeField] private TMP_Text _stabilizationProgressText;
        [SerializeField] private TMP_Text _stabilizationHoldState;
        [SerializeField] private TMP_Text _stabilizationRecovery;
        [SerializeField] private Image[] _recoverySegments;
        [SerializeField] private Image _stabilizationProgress;
        [SerializeField] private RectTransform _stabilizationStabilityPanel;
        [SerializeField] private RectTransform _stabilizationWarningPanel;
        [SerializeField] private TMP_Text _stabilizationWarningText;
        [SerializeField] private Image _stabilizationHazard;
        [SerializeField] private Button _stabilizationStart;
        [SerializeField] private Button _stabilizationStop;
        [SerializeField] private Button _stabilizationClose;
        [SerializeField] private Sprite stabilizationPanelSprite;
        [SerializeField] private Sprite stabilizationFrameSprite;
        [SerializeField] private Sprite stabilizationButtonSprite;
        [SerializeField] private Sprite stabilizationDangerSprite;
        [SerializeField] private Sprite stabilizationHazardSprite;
        [SerializeField] private Sprite stabilizationPixelSprite;
        [SerializeField] private int stabilizationLayoutRevision;
        private const int StabilizationLayoutRevision = 5;
        private static readonly Color WarningYellow = new Color(1f, 0.78f, 0.18f);
        private bool _stabilizationBound;

        public void RebuildStabilizationLayout()
        {
            if (_stabilizationSurface != null)
            {
                _stabilizationSurface.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(_stabilizationSurface.gameObject);
                else DestroyImmediate(_stabilizationSurface.gameObject);
                _stabilizationSurface = null;
            }
            _stabilizationBound = false;
            EnsureSurface();
            EnsureStabilizationPanel();
        }

        private void EnsureStabilizationPanel()
        {
            if (panelRoot == null) return;
            if (_stabilizationSurface != null && stabilizationLayoutRevision != StabilizationLayoutRevision)
            {
                RebuildStabilizationLayout();
                return;
            }
            LoadStabilizationArtInEditor();
            if (_stabilizationSurface == null)
            {
                _stabilizationSurface = AddIndustrialPanel(panelRoot.transform, "StabilizeOutput",
                    Vector2.zero, Vector2.one, stabilizationPanelSprite);
                Transform root = _stabilizationSurface;
                AddText(root, "Title", "Relay A · Nguồn điện", new Vector2(0.045f, 0.865f),
                    new Vector2(0.62f, 0.06f), 24, TextAlignmentOptions.Left, Color.white);
                AddText(root, "Stage", "03 · Ổn định đầu ra", new Vector2(0.045f, 0.815f),
                    new Vector2(0.62f, 0.04f), 17, TextAlignmentOptions.Left, LiveLine);
                _stabilizationStatus = AddText(root, "Status", "Chưa hoạt động", new Vector2(0.72f, 0.845f),
                    new Vector2(0.23f, 0.065f), 22, TextAlignmentOptions.Right, LiveLine);

                var monitoring = AddIndustrialPanel(root, "SystemMonitoring", new Vector2(0.045f, 0.42f),
                    new Vector2(0.49f, 0.80f), stabilizationFrameSprite);
                AddText(monitoring, "Title", "Theo dõi hệ thống", new Vector2(0.045f, 0.875f),
                    new Vector2(0.91f, 0.095f), 17, TextAlignmentOptions.Left, Color.white);
                _stabilizationReadings = new TMP_Text[3];
                _stabilizationSafeLabels = new TMP_Text[3];
                _stabilizationGauges = new Image[3];
                _stabilizationSafeBands = new Image[3];
                string[] labels = { "VOLTAGE", "Tần số", "Cân bằng tải" };
                for (int i = 0; i < 3; i++)
                {
                    var row = new GameObject(labels[i], typeof(RectTransform)).GetComponent<RectTransform>();
                    row.SetParent(monitoring, false);
                    SetRect(row, new Vector2(0f, 0.63f - i * 0.275f), new Vector2(1f, 0.875f - i * 0.275f));
                    AddText(row, "Label", labels[i], new Vector2(0.06f, 0.58f),
                        new Vector2(0.43f, 0.30f), 16, TextAlignmentOptions.Left, Color.white);
                    _stabilizationReadings[i] = AddText(row, "Value", "", new Vector2(0.49f, 0.53f),
                        new Vector2(0.45f, 0.40f), 20, TextAlignmentOptions.Right, Green);
                    _stabilizationSafeLabels[i] = AddText(row, "SafeRange", "", new Vector2(0.06f, 0.29f),
                        new Vector2(0.88f, 0.28f), 14, TextAlignmentOptions.Left, IdleLine);
                    var track = AddFlatImage(row, "GaugeTrack", new Vector2(0.06f, 0.10f), new Vector2(0.94f, 0.20f), TileBack);
                    _stabilizationGauges[i] = AddFlatImage(track.transform, "Fill", Vector2.zero, Vector2.one, Green);
                    _stabilizationSafeBands[i] = AddFlatImage(track.transform, "SafeBand", Vector2.zero, Vector2.one,
                        new Color(0.4f, 1f, 0.65f, 0.35f));
                }

                var manual = AddIndustrialPanel(root, "ManualControl", new Vector2(0.045f, 0.16f),
                    new Vector2(0.49f, 0.395f), stabilizationFrameSprite);
                AddText(manual, "Title", "Điều chỉnh", new Vector2(0.045f, 0.80f),
                    new Vector2(0.91f, 0.15f), 17, TextAlignmentOptions.Left, Color.white);
                _stabilizationSliders = new Slider[3];
                _stabilizationControlValues = new TMP_Text[3];
                string[] names = { "Đầu ra máy phát", "Điều chỉnh tần số", "Phân phối tải" };
                for (int i = 0; i < 3; i++)
                {
                    float y = 0.55f - i * 0.225f;
                    AddText(manual, names[i], names[i], new Vector2(0.045f, y),
                        new Vector2(0.37f, 0.18f), 16, TextAlignmentOptions.Left, Color.white);
                    _stabilizationSliders[i] = AddStabilizationSlider(manual, new Vector2(0.42f, y + 0.035f));
                    _stabilizationControlValues[i] = AddText(manual, "Setting", "", new Vector2(0.865f, y),
                        new Vector2(0.09f, 0.18f), 15, TextAlignmentOptions.Right, LiveLine);
                }

                _stabilizationStabilityPanel = AddIndustrialPanel(root, "Stability", new Vector2(0.52f, 0.46f),
                    new Vector2(0.955f, 0.80f), stabilizationFrameSprite);
                AddText(_stabilizationStabilityPanel, "Title", "STABILITY", new Vector2(0.06f, 0.80f),
                    new Vector2(0.88f, 0.12f), 19, TextAlignmentOptions.Left, Color.white);
                _stabilizationProgressText = AddText(_stabilizationStabilityPanel, "Progress", "",
                    new Vector2(0.06f, 0.44f), new Vector2(0.88f, 0.20f), 28, TextAlignmentOptions.Left, Green);
                var stabilityTrack = AddFlatImage(_stabilizationStabilityPanel, "ProgressTrack",
                    new Vector2(0.06f, 0.36f), new Vector2(0.94f, 0.36f), TileBack);
                stabilityTrack.rectTransform.sizeDelta = new Vector2(0f, 12f);
                _stabilizationProgress = AddFlatImage(stabilityTrack.transform, "Fill", Vector2.zero, Vector2.one, Green);
                _stabilizationHoldState = AddText(_stabilizationStabilityPanel, "HoldState", "",
                    new Vector2(0.06f, 0.14f), new Vector2(0.88f, 0.13f), 17, TextAlignmentOptions.Left, IdleLine);
                _stabilizationRecovery = AddText(_stabilizationStabilityPanel, "Recovery", "",
                    new Vector2(0.06f, 0.71f), new Vector2(0.88f, 0.08f), 14, TextAlignmentOptions.Left, LiveLine);
                _recoverySegments = new Image[3];
                for (int i = 0; i < 3; i++)
                {
                    _recoverySegments[i] = AddFlatImage(_stabilizationStabilityPanel, "RecoverySegment",
                        new Vector2(0.06f + i * 0.30f, 0.675f), new Vector2(0.34f + i * 0.30f, 0.675f), TileBack);
                    _recoverySegments[i].rectTransform.sizeDelta = new Vector2(0f, 6f);
                }

                _stabilizationWarningPanel = AddIndustrialPanel(root, "Warning", new Vector2(0.52f, 0.16f),
                    new Vector2(0.955f, 0.425f), stabilizationFrameSprite);
                _stabilizationHazard = AddFlatImage(_stabilizationWarningPanel, "Hazard",
                    new Vector2(0.06f, 0.47f), new Vector2(0.18f, 0.80f), WarningYellow);
                _stabilizationHazard.sprite = stabilizationHazardSprite;
                _stabilizationHazard.preserveAspect = true;
                _stabilizationWarningText = AddText(_stabilizationWarningPanel, "Fault", "",
                    new Vector2(0.23f, 0.14f), new Vector2(0.71f, 0.71f), 19, TextAlignmentOptions.TopLeft, WarningYellow);
                _stabilizationWarningPanel.gameObject.SetActive(false);

                _stabilizationStart = AddIndustrialButton(root, "StartStabilization", "Bắt đầu ổn định",
                    new Vector2(0.045f, 0.045f), new Vector2(0.32f, 0.075f), stabilizationButtonSprite);
                _stabilizationStop = AddIndustrialButton(root, "EmergencyStop", "Dừng khẩn cấp",
                    new Vector2(0.395f, 0.045f), new Vector2(0.30f, 0.075f), stabilizationDangerSprite);
                _stabilizationClose = AddIndustrialButton(root, "Close", "CLOSE",
                    new Vector2(0.805f, 0.045f), new Vector2(0.15f, 0.075f), stabilizationButtonSprite);
                stabilizationLayoutRevision = StabilizationLayoutRevision;
                _stabilizationSurface.gameObject.SetActive(false);
            }
            if (_stabilizationBound || !Application.isPlaying) return;
            _stabilizationBound = true;
            foreach (var slider in _stabilizationSliders) slider.onValueChanged.AddListener(_ => SendStabilizationControls());
            _stabilizationStart.onClick.AddListener(() => SendStabilizationRunning(true));
            _stabilizationStop.onClick.AddListener(() => SendStabilizationRunning(false));
            _stabilizationClose.onClick.AddListener(Close);
        }

        private RectTransform AddIndustrialPanel(Transform parent, string name, Vector2 min, Vector2 max, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            SetRect(rect, min, max);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 2f;
            image.color = sprite != null ? new Color(0.75f, 0.77f, 0.79f, 1f) : Back;
            bool mainSurface = name == "PowerRoutingMatrix" || name == "StabilizeOutput" || name == "BreakerMatrix";
            AddFlatImage(rect, "MatteBacking", new Vector2(0.015f, 0.025f), new Vector2(0.985f, 0.975f),
                mainSurface ? new Color(0.038f, 0.045f, 0.05f, 1f) : new Color(0.052f, 0.062f, 0.068f, 1f));
            if (mainSurface)
            {
                AddFlatImage(rect, "HeaderDivider", new Vector2(0.045f, 0.799f), new Vector2(0.955f, 0.801f), TileBack);
                AddFlatImage(rect, "FooterDivider", new Vector2(0.045f, 0.124f), new Vector2(0.955f, 0.126f), TileBack);
            }
            return rect;
        }

        private Image AddFlatImage(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            SetRect(go.GetComponent<RectTransform>(), min, max);
            var image = go.GetComponent<Image>();
            image.sprite = stabilizationPixelSprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Button AddIndustrialButton(Transform parent, string name, string label, Vector2 min, Vector2 size, Sprite sprite)
        {
            var button = AddButton(parent, name, label, min, size);
            var image = button.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 2f;
            image.color = sprite != null ? Color.white : TileBack;
            bool primary = name == "TestCircuit" || name == "StartStabilization";
            var body = AddFlatImage(button.transform, "ButtonBody", new Vector2(0.035f, 0.13f), new Vector2(0.965f, 0.87f),
                sprite == stabilizationDangerSprite ? new Color(0.24f, 0.075f, 0.07f, 1f)
                    : primary ? new Color(0.07f, 0.23f, 0.25f, 1f) : new Color(0.085f, 0.13f, 0.15f, 1f));
            body.transform.SetAsFirstSibling();
            button.targetGraphic = body;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.35f, 1.4f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.65f, 0.8f, 0.85f, 1f);
            colors.disabledColor = new Color(0.35f, 0.38f, 0.4f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            var buttonLabel = button.GetComponentInChildren<TMP_Text>();
            if (buttonLabel != null) { buttonLabel.fontSize = 16f; buttonLabel.fontSizeMax = 16f; }
            return button;
        }

        private Slider AddStabilizationSlider(Transform parent, Vector2 min)
        {
            var go = new GameObject("Control", typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            SetRect(go.GetComponent<RectTransform>(), min, min + new Vector2(0.42f, 0.10f));
            var track = AddFlatImage(go.transform, "Track", new Vector2(0f, 0.24f), new Vector2(1f, 0.76f), TileBack);
            track.raycastTarget = true;
            var fill = AddFlatImage(track.transform, "Fill", Vector2.zero, Vector2.one, LiveLine);
            var handle = AddFlatImage(go.transform, "Handle", new Vector2(0f, 0f), new Vector2(0f, 1f), Color.white);
            handle.raycastTarget = true;
            handle.rectTransform.sizeDelta = new Vector2(14f, 8f);
            var slider = go.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            return slider;
        }

        private void RefreshStabilization()
        {
            if (_controller == null || _controller.Config == null) return;
            var state = _controller.Circuit.Stabilization.Snapshot;
            var config = _controller.Config;
            float[] controls = { state.Controls.x, state.Controls.y, state.Controls.z };
            float[] readings = { state.Outputs.Voltage, state.Outputs.Frequency, state.Outputs.LoadBalance };
            Vector2[] safe = { config.VoltageSafeRange, config.FrequencySafeRange, config.LoadSafeRange };
            Vector2[] danger = { config.VoltageDangerRange, config.FrequencyDangerRange, config.LoadDangerRange };
            string[] units = { " V", " Hz", "%" };
            for (int i = 0; i < 3; i++)
            {
                bool isSafe = readings[i] >= safe[i].x && readings[i] <= safe[i].y;
                bool isDanger = readings[i] < danger[i].x || readings[i] > danger[i].y;
                Color color = isSafe ? Green : isDanger ? Red : WarningYellow;
                _stabilizationSliders[i].SetValueWithoutNotify(controls[i]);
                _stabilizationSliders[i].interactable = !_controller.IsOnline;
                _stabilizationControlValues[i].text = $"{controls[i]:0}%";
                _stabilizationReadings[i].text = $"{readings[i]:0.0}{units[i]}";
                _stabilizationReadings[i].color = color;
            _stabilizationSafeLabels[i].text = $"{(isSafe ? "An toàn" : readings[i] < safe[i].x ? "Tăng" : "Giảm")} {safe[i].x:0.#}-{safe[i].y:0.#}{units[i]}";
                float padding = Mathf.Max(1f, (danger[i].y - danger[i].x) * 0.2f);
                float min = danger[i].x - padding;
                float span = Mathf.Max(0.01f, danger[i].y - danger[i].x + padding * 2f);
                _stabilizationGauges[i].rectTransform.anchorMax = new Vector2(Mathf.Clamp01((readings[i] - min) / span), 1f);
                _stabilizationGauges[i].color = color;
                _stabilizationSafeBands[i].rectTransform.anchorMin = new Vector2(Mathf.Clamp01((safe[i].x - min) / span), 0f);
                _stabilizationSafeBands[i].rectTransform.anchorMax = new Vector2(Mathf.Clamp01((safe[i].y - min) / span), 1f);
            }
            bool faultVisible = state.WarningFault != RelayAFaultType.None || state.ActiveFault != RelayAFaultType.None || state.IsDangerous;
            _stabilizationWarningPanel.gameObject.SetActive(faultVisible && !_controller.IsOnline);
            _stabilizationStabilityPanel.anchorMin = new Vector2(0.52f, faultVisible && !_controller.IsOnline ? 0.46f : 0.16f);
            Color statusColor = _controller.IsOnline || state.IsStable && state.IsRunning ? Green
                : state.IsDangerous || state.ActiveFault != RelayAFaultType.None ? Red
                : state.IsRunning || state.WarningFault != RelayAFaultType.None ? WarningYellow : LiveLine;
            _stabilizationStatus.text = _controller.IsOnline ? "Hoạt động" : state.ActiveFault != RelayAFaultType.None || state.IsDangerous
                ? "Lỗi" : state.IsRunning ? "Đang ổn định" : "Chưa hoạt động";
            _stabilizationStatus.color = statusColor;
            _stabilizationProgressText.text = $"{state.StabilitySeconds:0.0} / {state.StabilityRequiredSeconds:0.0} s";
            _stabilizationProgressText.color = statusColor;
            _stabilizationRecovery.text = config.RequireFaultRecovery
                ? state.RecoveredFaults > 0 ? "Đã qua kiểm tra tải · Giữ đầu ra an toàn" : "Kiểm tra tải 0/1 · Xử lý lỗi" : "";
            for (int i = 0; i < _recoverySegments.Length; i++)
            {
                _recoverySegments[i].gameObject.SetActive(config.RequireFaultRecovery && i == 0);
                _recoverySegments[i].color = i < state.RecoveredFaults ? Green
                    : i == state.RecoveredFaults && state.IsRunning ? WarningYellow : TileBack;
            }
            _stabilizationProgress.rectTransform.anchorMax = new Vector2(state.Stability01, 1f);
            _stabilizationHoldState.text = _controller.IsOnline ? "Ổn định" : !state.IsRunning ? "Tạm dừng"
                : config.RequireFaultRecovery && state.RecoveredFaults < 1
                    ? state.ActiveFault != RelayAFaultType.None ? "Bù lỗi" : "Chờ kiểm tra tải"
                    : state.IsStable ? "Ổn định" : "Đầu ra chưa ổn định";
            _stabilizationHoldState.color = statusColor;
            bool active = state.ActiveFault != RelayAFaultType.None || state.IsDangerous;
            Color alertColor = active ? Red : WarningYellow;
            _stabilizationWarningPanel.GetComponent<Image>().color = active
                ? new Color(1f, 0.45f, 0.35f, 1f) : new Color(1f, 0.85f, 0.35f, 1f);
            _stabilizationHazard.color = alertColor;
            _stabilizationWarningText.color = alertColor;
            _stabilizationWarningText.text = state.ActiveFault != RelayAFaultType.None
                ? config.RequireFaultRecovery
                    ? state.IsStable
                        ? $"{FaultName(state.ActiveFault)}\nGiữ đầu ra an toàn\nCòn {state.FaultActiveRemaining:0.0} giây"
                        : $"{FaultName(state.ActiveFault)}\n{FaultHint(state.ActiveFault)}\nGiữ an toàn {config.FaultRecoveryHoldSeconds:0.0} s"
                    : $"Lỗi đang tác động\n{FaultName(state.ActiveFault)}\n{state.FaultActiveRemaining:0.0} s"
                : state.WarningFault != RelayAFaultType.None
                    ? $"Cảnh báo\n{FaultName(state.WarningFault)}\nLỗi xuất hiện sau {state.FaultWarningRemaining:0.0} s" : "Quá tải";
            _stabilizationStart.interactable = !state.IsRunning && !_controller.IsOnline;
            _stabilizationStop.gameObject.SetActive(state.IsRunning && !_controller.IsOnline);
            _stabilizationStop.interactable = state.IsRunning && !_controller.IsOnline;
        }

        private void LoadStabilizationArtInEditor()
        {
#if UNITY_EDITOR
            const string art = "Assets/_Project/UI/BunkerSurvivalUI/Sprites/";
            if (stabilizationPanelSprite == null) stabilizationPanelSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(art + "NineSlice/panel_industrial_main_normal_9slice.png");
            if (stabilizationFrameSprite == null) stabilizationFrameSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(art + "NineSlice/frame_security_terminal_normal_9slice.png");
            if (stabilizationButtonSprite == null) stabilizationButtonSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(art + "NineSlice/button_primary_normal_9slice.png");
            if (stabilizationDangerSprite == null) stabilizationDangerSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(art + "NineSlice/button_danger_normal_9slice.png");
            if (stabilizationHazardSprite == null) stabilizationHazardSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(art + "Icons/icon_hazard_sign.png");
            if (stabilizationPixelSprite == null) stabilizationPixelSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/UI/white_pixel.png");
#endif
        }

        private static string FaultName(RelayAFaultType fault) => fault == RelayAFaultType.Overvoltage
            ? "Quá áp" : fault == RelayAFaultType.FrequencyDesynchronization ? "Lệch tần số" : "Mất cân bằng tải";

        private static string FaultHint(RelayAFaultType fault) => fault == RelayAFaultType.Overvoltage
            ? "Giảm máy phát; kiểm tra tần số và tải" : fault == RelayAFaultType.FrequencyDesynchronization
            ? "Giảm tần số; kiểm tra điện áp và tải" : "Giảm tải; kiểm tra điện áp và tần số";

        private void SendStabilizationControls()
        {
            if (_controller == null) return;
            float generator = _stabilizationSliders[0].value;
            float frequency = _stabilizationSliders[1].value;
            float load = _stabilizationSliders[2].value;
            if (TryGetNetworkDirector(out var director)) director.RequestRelayAStabilizationControls(_controller, generator, frequency, load);
            else _controller.SetControls(generator, frequency, load);
        }

        private void SendStabilizationRunning(bool running)
        {
            if (_controller == null) return;
            if (TryGetNetworkDirector(out var director)) director.RequestRelayAStabilizationRunning(_controller, running);
            else if (running) _controller.StartStabilization();
            else _controller.EmergencyStop();
        }
    }
}
