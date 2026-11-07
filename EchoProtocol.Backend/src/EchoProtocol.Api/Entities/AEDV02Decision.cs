namespace EchoProtocol.Api.Entities;

public sealed class AEDV02ClientObservation
{
    public Guid DecisionId { get; set; }
    public Guid UserId { get; set; }
    public DateTime ObservedAtUtc { get; set; }
}

public sealed class AEDV02Cancellation
{
    public Guid DecisionId { get; set; }
    public Guid MatchId { get; set; }
    public Guid HostUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class AEDV02Decision
{
    public Guid DecisionId { get; set; }
    public Guid MatchId { get; set; }
    public Guid HostUserId { get; set; }
    public int PhaseOrdinal { get; set; }
    public string DecisionPoint { get; set; } = string.Empty;
    public string TargetPhaseName { get; set; } = string.Empty;
    public int WindowSequence { get; set; }
    public string WindowId { get; set; } = string.Empty;
    public int PlanRevision { get; set; }
    public int ExpectedPlanRevision { get; set; }
    public string PreviousPlanFingerprint { get; set; } = string.Empty;
    public string PlanFingerprint { get; set; } = string.Empty;
    public string RosterIdentity { get; set; } = string.Empty;
    public string SnapshotFingerprint { get; set; } = string.Empty;
    public Guid SnapshotId { get; set; }
    public string SourceFingerprint { get; set; } = string.Empty;
    public string SceneManifestFingerprint { get; set; } = string.Empty;
    public string RequestFingerprint { get; set; } = string.Empty;
    public string ProposalJson { get; set; } = string.Empty;
    public string ActionFingerprint { get; set; } = string.Empty;
    public string PolicyVersion { get; set; } = string.Empty;
    public string CatalogVersion { get; set; } = string.Empty;
    public string PlanValuesJson { get; set; } = string.Empty;
    public int InitialOptionalBudget { get; set; }
    public string ChangedKey { get; set; } = string.Empty;
    public string ReasonCode { get; set; } = string.Empty;
    public string ApprovalStatus { get; set; } = string.Empty;
    public string ApplyStatus { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public Guid? ReceiptId { get; set; }
    public Guid? EffectEventId { get; set; }
    public long? AppliedTick { get; set; }
    public DateTime? AppliedAtUtc { get; set; }
}

public sealed class AEDV02VerifiedSource
{
    public string Fingerprint { get; set; } = string.Empty;
    public Guid MatchId { get; set; }
    public int PhaseOrdinal { get; set; }
    public long StartTick { get; set; }
    public long EndTick { get; set; }
    public long EndEventSequence { get; set; }
    public string RosterIdentity { get; set; } = string.Empty;
    public string SceneManifestFingerprint { get; set; } = string.Empty;
    public string ReconstructedJson { get; set; } = string.Empty;
    public DateTime VerifiedAtUtc { get; set; }
    public DateTime SourceEndedAtUtc { get; set; }
}

public sealed class AEDV02SceneContent
{
    public string Fingerprint { get; set; } = string.Empty;
    public string SceneVersion { get; set; } = string.Empty;
    public string ManifestJson { get; set; } = string.Empty;
    public DateTime RegisteredAtUtc { get; set; }
}

public sealed class AEDV02SkillObservation
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public Guid UserId { get; set; }
    public string Dimension { get; set; } = string.Empty;
    public string ComparisonContextKey { get; set; } = string.Empty;
    public string SourceFingerprint { get; set; } = string.Empty;
    public int Eligible { get; set; }
    public int ResolvedSuccess { get; set; }
    public int ResolvedFailure { get; set; }
    public int Censored { get; set; }
    public DateTime VerifiedAtUtc { get; set; }
}
