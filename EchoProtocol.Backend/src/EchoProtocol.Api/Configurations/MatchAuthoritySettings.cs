namespace EchoProtocol.Api.Configurations;

public sealed class MatchAuthoritySettings
{
    public const string SectionName = "MatchAuthority";

    public string ProofSigningKey { get; set; } = string.Empty;
    public int JoinProofLifetimeSeconds { get; set; } = 120;
    public int LeaseLifetimeSeconds { get; set; } = 45;
    public int TelemetryDelegationRetentionHours { get; set; } = 24;
    public bool EnableRemoteFixed { get; set; } = false;
    public bool EnableRemoteFixedGameplayTest { get; set; } = false;
    public const string CurrentGameVersion = "0.1.1";
    public string[] RemoteFixedGameVersions { get; set; } = [CurrentGameVersion];
    public string? UnityBalanceDefaultsManifestPath { get; set; }
}
