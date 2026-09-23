namespace EchoProtocol.RelayA
{
    public enum RelayAStatus
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
