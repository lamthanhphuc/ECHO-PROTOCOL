using UnityEngine;

namespace EchoProtocol.RelayA
{
    [CreateAssetMenu(menuName = "ECHO Protocol/Relay A/Voltage Stabilization Config", fileName = "RelayAConfig")]
    public sealed class RelayAConfig : ScriptableObject
    {
        [Header("Safe Ranges")]
        [SerializeField] private Vector2 voltageSafeRange = new Vector2(220f, 230f);
        [SerializeField] private Vector2 frequencySafeRange = new Vector2(49f, 51f);
        [SerializeField] private Vector2 loadSafeRange = new Vector2(47f, 53f);

        [Header("Danger Ranges")]
        [SerializeField] private Vector2 voltageDangerRange = new Vector2(210f, 240f);
        [SerializeField] private Vector2 frequencyDangerRange = new Vector2(46.5f, 53.5f);
        [SerializeField] private Vector2 loadDangerRange = new Vector2(38f, 62f);

        [Header("Simulation")]
        [SerializeField, Min(0.05f)] private float responseDelaySeconds = 1f;
        [SerializeField, Min(0.1f)] private float stabilityRequiredSeconds = 12f;
        [SerializeField, Min(0f)] private float instabilityToleranceSeconds = 0.5f;
        [SerializeField] private Vector3 initialControls = new Vector3(34f, 66f, 38f);
        [SerializeField] private Vector3 solvedControls = new Vector3(58f, 42f, 54f);

        [Header("Cross Coupling")]
        [SerializeField] private Vector3 voltageWeights = new Vector3(0.72f, 0.18f, -0.26f);
        [SerializeField] private Vector3 frequencyWeights = new Vector3(-0.08f, 0.56f, 0.12f);
        [SerializeField] private Vector3 loadWeights = new Vector3(0.16f, -0.12f, 0.62f);
        [SerializeField] private Vector3 neutralControls = new Vector3(50f, 50f, 50f);
        [SerializeField] private Vector3 baselines = new Vector3(225f, 50f, 50f);
        [SerializeField] private Vector3 outputScales = new Vector3(22f, 5.2f, 18f);
        [SerializeField, Min(0f)] private float signalDriftAmplitude = 0.35f;
        [SerializeField, Min(0.01f)] private float signalDriftFrequency = 0.43f;

        [Header("Fault")]
        [SerializeField] private bool enableFault = true;
        [SerializeField, Min(0f)] private float earliestFaultAtSeconds = 8f;
        [SerializeField, Min(0f)] private float faultWarningSeconds = 4f;
        [SerializeField, Min(0.1f)] private float faultDurationSeconds = 6f;
        [SerializeField] private Vector3 overvoltageFaultOffset = new Vector3(9f, -0.35f, -2.2f);
        [SerializeField] private Vector3 frequencyFaultOffset = new Vector3(-2f, 2.2f, 1.1f);
        [SerializeField] private Vector3 loadFaultOffset = new Vector3(3f, -0.35f, 8.5f);

        public Vector2 VoltageSafeRange => voltageSafeRange;
        public Vector2 FrequencySafeRange => frequencySafeRange;
        public Vector2 LoadSafeRange => loadSafeRange;
        public Vector2 VoltageDangerRange => voltageDangerRange;
        public Vector2 FrequencyDangerRange => frequencyDangerRange;
        public Vector2 LoadDangerRange => loadDangerRange;
        public float ResponseDelaySeconds => responseDelaySeconds;
        public float StabilityRequiredSeconds => stabilityRequiredSeconds;
        public float InstabilityToleranceSeconds => instabilityToleranceSeconds;
        public Vector3 InitialControls => ClampControls(initialControls);
        public Vector3 SolvedControls => ClampControls(solvedControls);
        public bool EnableFault => enableFault;
        public float EarliestFaultAtSeconds => earliestFaultAtSeconds;
        public float FaultWarningSeconds => faultWarningSeconds;
        public float FaultDurationSeconds => faultDurationSeconds;

        public RelayAOutputs EvaluateTarget(Vector3 controls, RelayAFaultType activeFault, float elapsedSeconds)
        {
            Vector3 normalized = (ClampControls(controls) - neutralControls) / 50f;
            Vector3 outputs = baselines;
            outputs.x += Vector3.Dot(voltageWeights, normalized) * outputScales.x;
            outputs.y += Vector3.Dot(frequencyWeights, normalized) * outputScales.y;
            outputs.z += Vector3.Dot(loadWeights, normalized) * outputScales.z;

            float drift = Mathf.Sin(elapsedSeconds * Mathf.PI * 2f * signalDriftFrequency) * signalDriftAmplitude;
            outputs.x += drift;
            outputs.y += drift * 0.035f;
            outputs.z -= drift * 0.16f;
            outputs += GetFaultOffset(activeFault);

            return new RelayAOutputs(outputs.x, outputs.y, outputs.z);
        }

        public Vector3 GetFaultOffset(RelayAFaultType faultType)
        {
            switch (faultType)
            {
                case RelayAFaultType.Overvoltage:
                    return overvoltageFaultOffset;
                case RelayAFaultType.FrequencyDesynchronization:
                    return frequencyFaultOffset;
                case RelayAFaultType.LoadImbalance:
                    return loadFaultOffset;
                default:
                    return Vector3.zero;
            }
        }

        public bool HasValidSolution(out RelayAOutputs solvedOutputs)
        {
            solvedOutputs = EvaluateTarget(SolvedControls, RelayAFaultType.None, 0f);
            return IsOutputStable(solvedOutputs);
        }

        public bool IsVoltageSafe(float voltage) => IsInRange(voltage, voltageSafeRange);
        public bool IsFrequencySafe(float frequency) => IsInRange(frequency, frequencySafeRange);
        public bool IsLoadSafe(float load) => IsInRange(load, loadSafeRange);

        public bool IsVoltageDangerous(float voltage) => !IsInRange(voltage, voltageDangerRange);
        public bool IsFrequencyDangerous(float frequency) => !IsInRange(frequency, frequencyDangerRange);
        public bool IsLoadDangerous(float load) => !IsInRange(load, loadDangerRange);

        public bool IsOutputStable(RelayAOutputs outputs)
        {
            return IsVoltageSafe(outputs.Voltage)
                && IsFrequencySafe(outputs.Frequency)
                && IsLoadSafe(outputs.LoadBalance);
        }

        public bool IsOutputDangerous(RelayAOutputs outputs)
        {
            return IsVoltageDangerous(outputs.Voltage)
                || IsFrequencyDangerous(outputs.Frequency)
                || IsLoadDangerous(outputs.LoadBalance);
        }

        private static bool IsInRange(float value, Vector2 range)
        {
            return value >= range.x && value <= range.y;
        }

        private static Vector3 ClampControls(Vector3 controls)
        {
            return new Vector3(
                Mathf.Clamp(controls.x, 0f, 100f),
                Mathf.Clamp(controls.y, 0f, 100f),
                Mathf.Clamp(controls.z, 0f, 100f));
        }

        private void OnValidate()
        {
            responseDelaySeconds = Mathf.Max(0.05f, responseDelaySeconds);
            stabilityRequiredSeconds = Mathf.Max(0.1f, stabilityRequiredSeconds);
            instabilityToleranceSeconds = Mathf.Max(0f, instabilityToleranceSeconds);
            faultWarningSeconds = Mathf.Max(0f, faultWarningSeconds);
            faultDurationSeconds = Mathf.Max(0.1f, faultDurationSeconds);
            initialControls = ClampControls(initialControls);
            solvedControls = ClampControls(solvedControls);
        }
    }
}
