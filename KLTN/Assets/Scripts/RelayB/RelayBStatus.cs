namespace EchoProtocol.RelayB
{
    public enum RelayBStatus
    {
        Offline,
        Scanning,
        ChannelSelected,
        SignalMismatch,
        Synchronizing,
        DriftWarning,
        ConnectionLost,
        Online
    }

    public enum WaveformType
    {
        Sine,
        Square,
        Triangle,
        Sawtooth,
        CompositeHarmonic
    }
}

