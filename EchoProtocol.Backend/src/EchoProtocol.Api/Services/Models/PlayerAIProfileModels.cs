using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.Services.Models;

public enum MatchProfileEligibilityStatus { Eligible, Ineligible, Pending }
public enum TelemetryCompleteness { Complete, Incomplete, Invalid, Unknown }
public enum MetricAvailability { Available, Unavailable, Invalid }

public sealed record AggregatedMetric(
    PlayerAIDimension Dimension,
    decimal? RawValue,
    MetricAvailability Availability,
    string Reason,
    string EvidenceFingerprint);

public sealed record MatchTelemetryAggregation(
    Guid MatchId,
    Guid UserId,
    DateTime? MatchEndTs,
    MatchProfileEligibilityStatus Eligibility,
    TelemetryCompleteness Completeness,
    IReadOnlyList<string> Reasons,
    string SourceSchemaVersion,
    string SourceFingerprint,
    bool ResearchCaptureEnabled,
    bool? ResearchEligible,
    IReadOnlyDictionary<PlayerAIDimension, AggregatedMetric> Metrics);

public sealed record NormalizedMatchScore(
    PlayerAIDimension Dimension,
    decimal Score,
    string MatchScoreFormulaVersion,
    string NormalizationConfigVersion,
    string? ProfileNoiseFilterVersion,
    string EvidenceFingerprint);

public sealed record PlayerAIProfileUpdateResult(
    Guid UserId,
    Guid MatchId,
    Guid? ProfileLineageId,
    long? ProfileRevision,
    bool IsDuplicate,
    IReadOnlyList<PlayerAIDimension> AppliedDimensions,
    IReadOnlyList<PlayerAIDimension> RetractedDimensions);
