namespace EchoProtocol.Api.Entities;

public sealed class ScenarioAdaptivePlanV2
{
    public Guid DecisionId { get; set; }
    public Guid MatchId { get; set; }
    public Guid HostUserId { get; set; }
    public int PhaseOrdinal { get; set; }
    public string DecisionPoint { get; set; } = string.Empty;
    public string PolicyVersion { get; set; } = string.Empty;
    public string BaselineVersion { get; set; } = string.Empty;
    public string PreviousPlanFingerprint { get; set; } = string.Empty;
    public string ResultingPlanFingerprint { get; set; } = string.Empty;
    public string ChangedKey { get; set; } = string.Empty;
    public double PreviousValue { get; set; }
    public double AppliedValue { get; set; }
    public string AdaptationIntent { get; set; } = string.Empty;
    public string DecisionReason { get; set; } = string.Empty;
    public Guid SnapshotId { get; set; }
    public string SnapshotFingerprint { get; set; } = string.Empty;
    public string EvidenceFingerprint { get; set; } = string.Empty;
    public string RosterIdentity { get; set; } = string.Empty;
    public string PlanValuesJson { get; set; } = string.Empty;
    public string CommitStatus { get; set; } = string.Empty;
    public string ApplyStatus { get; set; } = "PENDING";
    public DateTime? CommittedAtUtc { get; set; }
    public DateTime? AppliedAtUtc { get; set; }
}
