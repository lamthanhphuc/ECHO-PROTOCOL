using UnityEngine;

namespace EchoProtocol.RelayA
{
    public readonly struct RelayASnapshot
    {
        public RelayASnapshot(
            RelayAStatus status,
            Vector3 controls,
            RelayAOutputs outputs,
            RelayAOutputs targetOutputs,
            RelayAReadingTrend voltageTrend,
            RelayAReadingTrend frequencyTrend,
            RelayAReadingTrend loadTrend,
            float stabilitySeconds,
            float stabilityRequiredSeconds,
            float instabilityGraceRemaining,
            RelayAFaultType warningFault,
            RelayAFaultType activeFault,
            float faultWarningRemaining,
            float faultActiveRemaining,
            bool isStable,
            bool isDangerous,
            bool isRunning,
            bool isOnline)
        {
            Status = status;
            Controls = controls;
            Outputs = outputs;
            TargetOutputs = targetOutputs;
            VoltageTrend = voltageTrend;
            FrequencyTrend = frequencyTrend;
            LoadTrend = loadTrend;
            StabilitySeconds = stabilitySeconds;
            StabilityRequiredSeconds = stabilityRequiredSeconds;
            InstabilityGraceRemaining = instabilityGraceRemaining;
            WarningFault = warningFault;
            ActiveFault = activeFault;
            FaultWarningRemaining = faultWarningRemaining;
            FaultActiveRemaining = faultActiveRemaining;
            IsStable = isStable;
            IsDangerous = isDangerous;
            IsRunning = isRunning;
            IsOnline = isOnline;
        }

        public RelayAStatus Status { get; }
        public Vector3 Controls { get; }
        public RelayAOutputs Outputs { get; }
        public RelayAOutputs TargetOutputs { get; }
        public RelayAReadingTrend VoltageTrend { get; }
        public RelayAReadingTrend FrequencyTrend { get; }
        public RelayAReadingTrend LoadTrend { get; }
        public float StabilitySeconds { get; }
        public float StabilityRequiredSeconds { get; }
        public float InstabilityGraceRemaining { get; }
        public RelayAFaultType WarningFault { get; }
        public RelayAFaultType ActiveFault { get; }
        public float FaultWarningRemaining { get; }
        public float FaultActiveRemaining { get; }
        public bool IsStable { get; }
        public bool IsDangerous { get; }
        public bool IsRunning { get; }
        public bool IsOnline { get; }
        public float Stability01 => StabilityRequiredSeconds <= 0f ? 1f : Mathf.Clamp01(StabilitySeconds / StabilityRequiredSeconds);
    }
}
