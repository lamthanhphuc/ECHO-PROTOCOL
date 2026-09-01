namespace EchoProtocol.Api.Configurations;

public sealed class MatchAuthoritySettings
{
    public const string SectionName = "MatchAuthority";

    public string ProofSigningKey { get; set; } = string.Empty;
    public int JoinProofLifetimeSeconds { get; set; } = 120;
    public int LeaseLifetimeSeconds { get; set; } = 45;
    public int TelemetryDelegationRetentionHours { get; set; } = 24;
}
