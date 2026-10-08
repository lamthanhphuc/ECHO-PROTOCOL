using System.ComponentModel.DataAnnotations;

namespace EchoProtocol.Api.DTOs.Scenarios;

public sealed class SubmitScenarioAdaptivePlanV2Request
{
    public Guid DecisionId { get; set; }
    public int PhaseOrdinal { get; set; }
    [Required] public string DecisionPoint { get; set; } = string.Empty;
    [Required] public string PolicyVersion { get; set; } = string.Empty;
    [Required] public string BaselineVersion { get; set; } = string.Empty;
    [Required] public string PreviousPlanFingerprint { get; set; } = string.Empty;
    [Required] public string ResultingPlanFingerprint { get; set; } = string.Empty;
    [Required] public string ChangedKey { get; set; } = string.Empty;
    public double PreviousValue { get; set; }
    public double AppliedValue { get; set; }
    [Required] public string AdaptationIntent { get; set; } = string.Empty;
    [Required] public string DecisionReason { get; set; } = string.Empty;
    public Guid SnapshotId { get; set; }
    [Required] public string SnapshotFingerprint { get; set; } = string.Empty;
    public string EvidenceFingerprint { get; set; } = string.Empty;
    [Required] public string RosterIdentity { get; set; } = string.Empty;
    [Required] public string CommitStatus { get; set; } = string.Empty;
    [Required] public double[] PlanValues { get; set; } = [];
}

public sealed record ScenarioAdaptivePlanV2Dto(
    Guid MatchId, Guid DecisionId, int PhaseOrdinal, string DecisionPoint,
    string PolicyVersion, string BaselineVersion, string PreviousPlanFingerprint,
    string ResultingPlanFingerprint, string ChangedKey, double PreviousValue,
    double AppliedValue, string AdaptationIntent, string DecisionReason,
    Guid SnapshotId, string SnapshotFingerprint, string EvidenceFingerprint,
    string RosterIdentity, string CommitStatus, string ApplyStatus,
    DateTime? CommittedAtUtc, DateTime? AppliedAtUtc, bool IsReplay);

public sealed class ConfirmScenarioAdaptivePlanV2AppliedRequest
{
    [Required] public string PlanFingerprint { get; set; } = string.Empty;
}
