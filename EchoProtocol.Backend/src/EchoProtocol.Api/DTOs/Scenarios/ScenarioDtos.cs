using System.ComponentModel.DataAnnotations;
using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.DTOs.Scenarios;

public sealed class ResolveScenarioRequest
{
    public Guid DecisionId { get; set; }
    public ScenarioResolutionMode ResolutionMode { get; set; }
    [Required, StringLength(80, MinimumLength = 1)] public string UnityCompatibilityVersion { get; set; } = string.Empty;
    [StringLength(80)] public string? ExperimentCondition { get; set; }
}

public sealed class ConfirmScenarioAppliedRequest
{
    [Required, StringLength(128)] public string ScenarioConfigId { get; set; } = string.Empty;
    [Required, StringLength(80)] public string ScenarioConfigVersion { get; set; } = string.Empty;
    [Required, StringLength(64, MinimumLength = 64)] public string ScenarioConfigFingerprint { get; set; } = string.Empty;
}

public sealed record ScenarioConfigResponse(
    string ScenarioConfigId, string ScenarioConfigVersion, string SchemaVersion,
    string PolicyVersion, string ConfigSource, string MapId, string MonsterType,
    string ObjectiveSpawnSetId, int SupportItemBudget, double DetectionFillRate,
    double DetectionDecayRate, double ChaseSpeed, double SearchDuration,
    string RouteModifier, double EscapeDoorTimerSeconds, string FallbackConfigId,
    string FallbackConfigVersion, string ContentWhitelistVersion,
    string UnityCompatibilityVersion, string ContentFingerprint);

public sealed record ScenarioDecisionResponse(
    Guid DecisionId, Guid MatchId, Guid SnapshotId, string SnapshotContentFingerprint,
    string RosterIdentity, string SnapshotValidity, IReadOnlyList<string> SnapshotReasonCodes,
    string ResolutionMode, string ResolutionResult, string? CandidateValidationStatus,
    bool UsedFixedFallback, string? FallbackReasonCode, string DecisionStatus,
    string UnityApplyStatus, DateTime CommittedAtUtc, DateTime? AppliedAtUtc,
    bool IsReplay, ScenarioConfigResponse Config);
