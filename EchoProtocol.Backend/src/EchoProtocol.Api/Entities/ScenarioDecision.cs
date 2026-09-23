using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.Entities;

public sealed class ScenarioDecision
{
    public Guid DecisionId { get; set; }
    public Guid MatchId { get; set; }
    public Guid HostUserId { get; set; }
    public Guid SnapshotId { get; set; }
    public ScenarioResolutionMode ResolutionMode { get; set; }
    public string DecisionPoint { get; set; } = "PRE_MATCH";
    public string? ExperimentCondition { get; set; }
    public string RequestFingerprint { get; set; } = string.Empty;
    public string DecisionSemanticFingerprint { get; set; } = string.Empty;
    public string ScenarioConfigId { get; set; } = string.Empty;
    public string ScenarioConfigVersion { get; set; } = string.Empty;
    public string ScenarioConfigFingerprint { get; set; } = string.Empty;
    public string PolicyVersion { get; set; } = string.Empty;
    public string? PolicyConfigVersion { get; set; }
    public string? EvidencePolicyVersion { get; set; }
    public string? ParameterRegistryVersion { get; set; }
    public string ContentWhitelistVersion { get; set; } = string.Empty;
    public string FallbackConfigId { get; set; } = string.Empty;
    public string FallbackConfigVersion { get; set; } = string.Empty;
    public bool UsedFixedFallback { get; set; }
    public string? FallbackReasonCode { get; set; }
    public ScenarioCandidateValidationStatus? CandidateValidationStatus { get; set; }
    public string ResolutionResult { get; set; } = string.Empty;
    public ScenarioDecisionStatus Status { get; set; }
    public bool IsCurrent { get; set; }
    public DateTime CommittedAtUtc { get; set; }
    public MatchAuthorityBinding Match { get; set; } = null!;
    public User HostUser { get; set; } = null!;
    public AdaptiveInputSnapshot Snapshot { get; set; } = null!;
    public ScenarioConfigDefinition ScenarioConfig { get; set; } = null!;
    public ScenarioApplyReceipt? ApplyReceipt { get; set; }
}
