namespace EchoProtocol.Api.Entities;

public sealed class AEDDirectorDecisionV1
{
    public Guid DecisionId { get; set; }
    public Guid MatchId { get; set; }
    public Guid HostUserId { get; set; }
    public int CompletedPhaseOrdinal { get; set; }
    public string DirectorKind { get; set; } = string.Empty;
    public string PolicyVersion { get; set; } = string.Empty;
    public string RosterIdentity { get; set; } = string.Empty;
    public string SnapshotFingerprint { get; set; } = string.Empty;
    public string EvidenceFingerprint { get; set; } = string.Empty;
    public string SourceFingerprint { get; set; } = string.Empty;
    public string PayloadFingerprint { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string ApprovalScope { get; set; } = "SHADOW";
    public string Status { get; set; } = "APPROVED_RESEARCH";
    public string ReasonCode { get; set; } = string.Empty;
    public long ExpectedRevision { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? AppliedAtUtc { get; set; }
    public DateTime? AbortedAtUtc { get; set; }
}
