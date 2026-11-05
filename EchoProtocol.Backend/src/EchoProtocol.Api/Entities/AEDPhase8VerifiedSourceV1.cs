namespace EchoProtocol.Api.Entities;

public sealed class AEDPhase8VerifiedSourceV1
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public int CompletedPhaseOrdinal { get; set; }
    public string SourceVersion { get; set; } = string.Empty;
    public string EvidenceFingerprint { get; set; } = string.Empty;
    public string SourceFingerprint { get; set; } = string.Empty;
    public string RosterIdentity { get; set; } = string.Empty;
    public bool CanonicalVerified { get; set; }
    public bool HistoricalSkillVerified { get; set; }
    public bool PressureVerified { get; set; }
    public bool SupplyLedgerVerified { get; set; }
    public bool MinionPopulationVerified { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string VerifiedContentJson { get; set; } = string.Empty;
    public DateTime VerifiedAtUtc { get; set; }
}
