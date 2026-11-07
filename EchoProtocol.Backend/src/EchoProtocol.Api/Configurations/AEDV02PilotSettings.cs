namespace EchoProtocol.Api.Configurations;

public sealed class AEDV02PilotSettings
{
    public bool Enabled { get; set; }
    public bool CabinetAmbushEnabled { get; set; }
    public string[] ChaseSpeedVerifiedCompatibilityVersions { get; set; } = [];
}
