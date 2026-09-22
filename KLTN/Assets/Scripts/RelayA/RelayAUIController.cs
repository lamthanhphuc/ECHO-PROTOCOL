using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace EchoProtocol.RelayA
{
    [DisallowMultipleComponent]
    public sealed class RelayAUIController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Header")]
        [SerializeField] private TMP_Text facilityLabel;
        [SerializeField] private TMP_Text relayLabel;
        [SerializeField] private TMP_Text modeLabel;
        [SerializeField] private TMP_Text statusLabel;

        [Header("Monitoring")]
        [SerializeField] private TMP_Text voltageValue;
        [SerializeField] private TMP_Text voltageTrend;
        [SerializeField] private TMP_Text voltageStatus;
        [SerializeField] private Image voltageGauge;
        [SerializeField] private TMP_Text frequencyValue;
        [SerializeField] private TMP_Text frequencyTrend;
        [SerializeField] private TMP_Text frequencyStatus;
        [SerializeField] private Image frequencyGauge;
        [SerializeField] private TMP_Text loadValue;
        [SerializeField] private TMP_Text loadTrend;
        [SerializeField] private TMP_Text loadStatus;
        [SerializeField] private Image loadGauge;

        [Header("Controls")]
        [SerializeField] private Slider generatorSlider;
        [SerializeField] private TMP_Text generatorValue;
        [SerializeField] private Slider frequencySlider;
        [SerializeField] private TMP_Text frequencyControlValue;
        [SerializeField] private Slider loadSlider;
        [SerializeField] private TMP_Text loadControlValue;

        [Header("Stability")]
        [SerializeField] private TMP_Text stabilityLabel;
        [SerializeField] private Image stabilityFill;
        [SerializeField] private TMP_Text instabilityReasonLabel;

        [Header("Warning")]
        [SerializeField] private Image warningIcon;
        [SerializeField] private TMP_Text warningTitle;
        [SerializeField] private TMP_Text warningDescription;
        [SerializeField] private TMP_Text warningTimer;
        [SerializeField] private TMP_Text overloadLabel;

        [Header("Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button emergencyStopButton;
        [SerializeField] private Button closeButton;

        [Header("Colors")]
        [SerializeField] private Color safeColor = new Color(0.35f, 1f, 0.58f, 1f);
        [SerializeField] private Color warningColor = new Color(1f, 0.78f, 0.18f, 1f);
        [SerializeField] private Color dangerColor = new Color(1f, 0.2f, 0.14f, 1f);
        [SerializeField] private Color offlineColor = new Color(0.55f, 0.65f, 0.7f, 1f);

        private readonly RelayAPlayerControlLock _controlLock = new RelayAPlayerControlLock();
        private RelayAController _controller;
        private bool _suppressSliderEvents;

        private void Awake()
        {
            HookControls();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            _controlLock.Release();
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (_controlLock.ShouldAutoRelease())
            {
                Close();
                return;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        public void Bind(RelayAController controller)
        {
            _controller = controller;
            HookControls();
        }

        public void Open(GameObject interactor)
        {
            EnsureEventSystem();
            _controlLock.Acquire(interactor);
            SetVisible(true);
        }

        public void Close()
        {
            SetVisible(false);
            _controlLock.Release();
        }

        public void Refresh(RelayASnapshot snapshot)
        {
            _suppressSliderEvents = true;
            SetSlider(generatorSlider, snapshot.Controls.x);
            SetSlider(frequencySlider, snapshot.Controls.y);
            SetSlider(loadSlider, snapshot.Controls.z);
            _suppressSliderEvents = false;

            SetText(generatorValue, $"{snapshot.Controls.x:0}%");
            SetText(frequencyControlValue, $"{snapshot.Controls.y:0}%");
            SetText(loadControlValue, $"{snapshot.Controls.z:0}%");

            SetText(facilityLabel, "ECHO FACILITY");
            SetText(relayLabel, snapshot.IsOnline ? "POWER RELAY A - ONLINE" : "POWER RELAY A");
            SetText(modeLabel, snapshot.IsOnline ? "EMERGENCY POWER RESTORED" : "VOLTAGE STABILIZATION");
            SetText(statusLabel, StatusText(snapshot.Status));
            if (statusLabel != null)
            {
                statusLabel.color = StatusColor(snapshot.Status);
            }

            string vStatus = GetParameterStatus(snapshot.Outputs.Voltage, _controller != null && _controller.Config != null ? _controller.Config.VoltageSafeRange : new Vector2(220f, 230f), "VOLT");
            string fStatus = GetParameterStatus(snapshot.Outputs.Frequency, _controller != null && _controller.Config != null ? _controller.Config.FrequencySafeRange : new Vector2(49f, 51f), "FREQ");
            string lStatus = GetParameterStatus(snapshot.Outputs.LoadBalance, _controller != null && _controller.Config != null ? _controller.Config.LoadSafeRange : new Vector2(47f, 53f), "LOAD");

            RefreshGauge(voltageValue, voltageStatus, voltageTrend, voltageGauge, snapshot.Outputs.Voltage, "V", vStatus, 200f, 250f, snapshot.VoltageTrend, InRange(snapshot.Outputs.Voltage, _controller != null && _controller.Config != null ? _controller.Config.VoltageSafeRange : new Vector2(220f, 230f)), snapshot.IsDangerous);
            RefreshGauge(frequencyValue, frequencyStatus, frequencyTrend, frequencyGauge, snapshot.Outputs.Frequency, "Hz", fStatus, 45f, 55f, snapshot.FrequencyTrend, InRange(snapshot.Outputs.Frequency, _controller != null && _controller.Config != null ? _controller.Config.FrequencySafeRange : new Vector2(49f, 51f)), snapshot.IsDangerous);
            RefreshGauge(loadValue, loadStatus, loadTrend, loadGauge, snapshot.Outputs.LoadBalance, "%", lStatus, 35f, 65f, snapshot.LoadTrend, InRange(snapshot.Outputs.LoadBalance, _controller != null && _controller.Config != null ? _controller.Config.LoadSafeRange : new Vector2(47f, 53f)), snapshot.IsDangerous);

            SetText(stabilityLabel, snapshot.IsOnline
                ? "STABILITY VERIFICATION COMPLETE\nProgress: 12.0 / 12.0 seconds"
                : $"STABILITY VERIFICATION\nProgress: {snapshot.StabilitySeconds:0.0} / {snapshot.StabilityRequiredSeconds:0} seconds");

            if (stabilityFill != null)
            {
                EnsureProgressFillSprite(stabilityFill);
                stabilityFill.type = Image.Type.Filled;
                stabilityFill.fillMethod = Image.FillMethod.Horizontal;
                stabilityFill.fillOrigin = 0;
                stabilityFill.fillAmount = snapshot.Stability01;
                stabilityFill.color = snapshot.IsOnline || snapshot.IsStable ? safeColor : warningColor;
            }

            SetText(instabilityReasonLabel, BuildInstabilityReason(snapshot));
            RefreshWarning(snapshot);

            bool readOnly = snapshot.IsOnline;
            SetInteractable(generatorSlider, !readOnly);
            SetInteractable(frequencySlider, !readOnly);
            SetInteractable(loadSlider, !readOnly);
            SetInteractable(startButton, !readOnly && !snapshot.IsRunning);
            SetInteractable(emergencyStopButton, !readOnly && snapshot.IsRunning);
        }

        private static string GetParameterStatus(float value, Vector2 safeRange, string paramName)
        {
            if (value < safeRange.x) return $"LOW {paramName}";
            if (value > safeRange.y) return $"HIGH {paramName}";
            return "OPTIMAL";
        }

        private void RefreshGauge(
            TMP_Text valueText,
            TMP_Text statusText,
            TMP_Text trendText,
            Image gauge,
            float value,
            string unit,
            string statusLabel,
            float min,
            float max,
            RelayAReadingTrend trend,
            bool safe,
            bool dangerous)
        {
            SetText(valueText, $"{value:0.0} {unit}");
            SetText(statusText, statusLabel);
            SetText(trendText, TrendText(trend));
            Color color = dangerous ? dangerColor : safe ? safeColor : warningColor;
            if (valueText != null) valueText.color = color;
            if (statusText != null) statusText.color = color;
            if (trendText != null) trendText.color = trend == RelayAReadingTrend.Stable ? offlineColor : color;
            if (gauge != null)
            {
                EnsureProgressFillSprite(gauge);
                gauge.type = Image.Type.Filled;
                gauge.fillMethod = Image.FillMethod.Horizontal;
                gauge.fillOrigin = 0;
                gauge.fillAmount = Mathf.InverseLerp(min, max, value);
                gauge.color = color;
            }
        }

        private static void EnsureProgressFillSprite(Image image)
        {
            if (image != null && image.overrideSprite == null && image.sprite == null)
            {
                image.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            }
        }

        private void RefreshWarning(RelayASnapshot snapshot)
        {
            bool hasWarning = snapshot.WarningFault != RelayAFaultType.None || snapshot.ActiveFault != RelayAFaultType.None || snapshot.Status == RelayAStatus.Overload;
            RelayAFaultType fault = snapshot.ActiveFault != RelayAFaultType.None ? snapshot.ActiveFault : snapshot.WarningFault;
            if (warningIcon != null)
            {
                warningIcon.enabled = hasWarning;
                warningIcon.color = snapshot.Status == RelayAStatus.Overload ? dangerColor : warningColor;
            }

            SetText(warningTitle, hasWarning ? FaultTitle(fault, snapshot.Status == RelayAStatus.Overload) : "NO ACTIVE FAULT");
            SetText(warningDescription, hasWarning ? FaultDescription(fault, snapshot.ActiveFault != RelayAFaultType.None) : "Monitoring relay load and grid phase.");
            SetText(warningTimer, BuildFaultTimer(snapshot));
            SetText(overloadLabel, snapshot.Status == RelayAStatus.Overload ? "OVERLOAD: ACTIVE" : "OVERLOAD: CLEAR");
            if (overloadLabel != null)
            {
                overloadLabel.color = snapshot.Status == RelayAStatus.Overload ? dangerColor : safeColor;
            }
        }

        private string BuildInstabilityReason(RelayASnapshot snapshot)
        {
            if (!snapshot.IsRunning) return snapshot.IsOnline ? "Relay A is locked online." : "System offline. Start stabilization.";
            if (snapshot.IsStable) return "All readings inside safe band.";
            if (snapshot.IsDangerous) return "Danger threshold exceeded. Stabilizer timer reset.";
            return $"Signal outside safe band. Grace: {snapshot.InstabilityGraceRemaining:0.0}s";
        }

        private string BuildFaultTimer(RelayASnapshot snapshot)
        {
            if (snapshot.WarningFault != RelayAFaultType.None)
            {
                return $"FAULT IN {snapshot.FaultWarningRemaining:0.0}s";
            }

            if (snapshot.ActiveFault != RelayAFaultType.None)
            {
                return $"FAULT ACTIVE {snapshot.FaultActiveRemaining:0.0}s";
            }

            return snapshot.Status == RelayAStatus.Overload ? "RECOVER MANUALLY" : "STANDBY";
        }

        private void HookControls()
        {
            if (generatorSlider != null)
            {
                generatorSlider.minValue = 0f;
                generatorSlider.maxValue = 100f;
                generatorSlider.wholeNumbers = false;
                generatorSlider.onValueChanged.RemoveListener(HandleSliderChanged);
                generatorSlider.onValueChanged.AddListener(HandleSliderChanged);
            }

            if (frequencySlider != null)
            {
                frequencySlider.minValue = 0f;
                frequencySlider.maxValue = 100f;
                frequencySlider.wholeNumbers = false;
                frequencySlider.onValueChanged.RemoveListener(HandleSliderChanged);
                frequencySlider.onValueChanged.AddListener(HandleSliderChanged);
            }

            if (loadSlider != null)
            {
                loadSlider.minValue = 0f;
                loadSlider.maxValue = 100f;
                loadSlider.wholeNumbers = false;
                loadSlider.onValueChanged.RemoveListener(HandleSliderChanged);
                loadSlider.onValueChanged.AddListener(HandleSliderChanged);
            }

            if (startButton != null)
            {
                startButton.onClick.RemoveListener(HandleStartClicked);
                startButton.onClick.AddListener(HandleStartClicked);
            }

            if (emergencyStopButton != null)
            {
                emergencyStopButton.onClick.RemoveListener(HandleEmergencyStopClicked);
                emergencyStopButton.onClick.AddListener(HandleEmergencyStopClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
                closeButton.onClick.AddListener(Close);
            }
        }

        private void HandleSliderChanged(float value)
        {
            if (_suppressSliderEvents || _controller == null || generatorSlider == null || frequencySlider == null || loadSlider == null)
            {
                return;
            }

            _controller.SetControls(generatorSlider.value, frequencySlider.value, loadSlider.value);
        }

        private void HandleStartClicked()
        {
            _controller?.StartStabilization();
        }

        private void HandleEmergencyStopClicked()
        {
            _controller?.EmergencyStop();
        }

        private void SetVisible(bool visible)
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(visible);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.blocksRaycasts = visible;
                canvasGroup.interactable = visible;
            }
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                eventSystem = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            }

            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("RuntimeEventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }

            var standalone = eventSystem.GetComponent<StandaloneInputModule>();
            if (standalone != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(standalone);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(standalone);
                }
            }

            var inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                inputModule.AssignDefaultActions();
            }
            else
            {
                inputModule.enabled = true;
            }
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }

        private static void SetSlider(Slider slider, float value)
        {
            if (slider != null)
            {
                slider.SetValueWithoutNotify(value);
            }
        }

        private static void SetInteractable(Selectable selectable, bool interactable)
        {
            if (selectable != null)
            {
                selectable.interactable = interactable;
            }
        }

        private static bool InRange(float value, Vector2 range)
        {
            return value >= range.x && value <= range.y;
        }

        private static string StatusText(RelayAStatus status)
        {
            switch (status)
            {
                case RelayAStatus.Calibrating:
                    return "CALIBRATING";
                case RelayAStatus.Stabilizing:
                    return "STABILIZING";
                case RelayAStatus.FaultWarning:
                    return "FAULT WARNING";
                case RelayAStatus.Overload:
                    return "OVERLOAD";
                case RelayAStatus.Online:
                    return "ONLINE";
                default:
                    return "OFFLINE";
            }
        }

        private Color StatusColor(RelayAStatus status)
        {
            switch (status)
            {
                case RelayAStatus.Stabilizing:
                case RelayAStatus.Online:
                    return safeColor;
                case RelayAStatus.Calibrating:
                case RelayAStatus.FaultWarning:
                    return warningColor;
                case RelayAStatus.Overload:
                    return dangerColor;
                default:
                    return offlineColor;
            }
        }

        private static string TrendText(RelayAReadingTrend trend)
        {
            switch (trend)
            {
                case RelayAReadingTrend.Rising:
                    return "RISING ↑";
                case RelayAReadingTrend.Falling:
                    return "FALLING ↓";
                default:
                    return "STABLE →";
            }
        }

        private static string FaultTitle(RelayAFaultType fault, bool overload)
        {
            if (overload) return "SYSTEM OVERLOAD";
            switch (fault)
            {
                case RelayAFaultType.Overvoltage:
                    return "OVERVOLTAGE WARNING";
                case RelayAFaultType.FrequencyDesynchronization:
                    return "FREQUENCY DESYNC";
                case RelayAFaultType.LoadImbalance:
                    return "LOAD IMBALANCE";
                default:
                    return "FAULT WARNING";
            }
        }

        private static string FaultDescription(RelayAFaultType fault, bool active)
        {
            string prefix = active ? "Active fault: " : "Incoming fault: ";
            switch (fault)
            {
                case RelayAFaultType.Overvoltage:
                    return prefix + "voltage surging. Reduce Generator Output.";
                case RelayAFaultType.FrequencyDesynchronization:
                    return prefix + "frequency drifting. Adjust Frequency Regulator.";
                case RelayAFaultType.LoadImbalance:
                    return prefix + "load balance skewed. Rebalance Load Distribution.";
                default:
                    return "Monitoring relay load and grid phase.";
            }
        }
    }
}
