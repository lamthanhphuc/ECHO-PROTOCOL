namespace EchoProtocol.RelayA
{
    public readonly struct RelayAOutputs
    {
        public RelayAOutputs(float voltage, float frequency, float loadBalance)
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
