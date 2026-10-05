using UnityEngine;

namespace EchoProtocol.RelayA
{
    public readonly struct RelayAStabilizationOutputs
    {
        public RelayAStabilizationOutputs(float voltage, float frequency, float loadBalance)
        {
            Voltage = voltage;
            Frequency = frequency;
            LoadBalance = loadBalance;
        }

        public float Voltage { get; }
        public float Frequency { get; }
        public float LoadBalance { get; }
    }
}

namespace EchoProtocol.RelayA
{
    public readonly struct RelayAStabilizationSnapshot
    {
        public RelayAStabilizationSnapshot(
            RelayAStabilizationStatus status,
            Vector3 controls,
            RelayAStabilizationOutputs outputs,
            RelayAStabilizationOutputs targetOutputs,
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
            bool isOnline,
            int recoveredFaults = 0)
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
            RecoveredFaults = recoveredFaults;
        }

        public RelayAStabilizationStatus Status { get; }
        public Vector3 Controls { get; }
        public RelayAStabilizationOutputs Outputs { get; }
        public RelayAStabilizationOutputs TargetOutputs { get; }
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
        public int RecoveredFaults { get; }
        public float Stability01 => StabilityRequiredSeconds <= 0f ? 1f : Mathf.Clamp01(StabilitySeconds / StabilityRequiredSeconds);
    }
}

namespace EchoProtocol.RelayA
{
    public enum RelayAStabilizationStatus
    {
        Offline,
        Calibrating,
        Unstable = Calibrating,
        Stabilizing,
        FaultWarning,
        Overload,
        Online
    }

    public enum RelayAFaultType
    {
        None,
        Overvoltage,
        FrequencyDesynchronization,
        LoadImbalance
    }

    public enum RelayAReadingTrend
    {
        Stable,
        Rising,
        Falling
    }
}
