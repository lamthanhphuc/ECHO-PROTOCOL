namespace EchoProtocol.Api.DTOs.Profiles;

public sealed record AIProfileDimensionResponse(
    decimal? Score,
    string Status,
    int? SampleCount,
    Guid? LastMatchId,
    DateTime? LastUpdatedAtUtc);

public sealed record PlayerAIProfileResponse(
    Guid UserId,
    Guid ProfileLineageId,
    long ProfileRevision,
    string ProfileFormulaVersion,
    string MatchScoreFormulaVersion,
    string? NormalizationConfigVersion,
    string? ProfileNoiseFilterVersion,
    string AlphaConfigVersion,
    AIProfileDimensionResponse Survival,
    AIProfileDimensionResponse Noise,
    AIProfileDimensionResponse Objective,
    AIProfileDimensionResponse Teamwork,
    AIProfileDimensionResponse Exploration,
    AIProfileDimensionResponse Navigation,
    AIProfileDimensionResponse ToolUsage,
    AIProfileDimensionResponse Risk,
    AIProfileDimensionResponse Revive,
    DateTime UpdatedAtUtc);

public sealed record RosterAIProfileResponse(
    Guid MatchId,
    IReadOnlyList<RosterAIProfileItemResponse> Players);

public sealed record RosterAIProfileItemResponse(
    Guid UserId,
    PlayerAIProfileResponse? Profile);

public sealed record TeamMetricResponse(decimal? Value, string Status);

public sealed record TeamProfileResponse(
    Guid MatchId,
    long ProcessingRevision,
    string ProcessingStatus,
    string ProcessingReason,
    string TelemetryCompleteness,
    TeamMetricResponse ObjectiveTime,
    TeamMetricResponse SplitTime,
    TeamMetricResponse AvgDistance,
    TeamMetricResponse ReviveSuccess,
    TeamMetricResponse ResourceEfficiency,
    TeamMetricResponse Communication,
    TeamMetricResponse WipeRecovery,
    TeamMetricResponse ObjectiveSpeed,
    TeamMetricResponse Survival,
    TeamMetricResponse Teamwork,
    TeamMetricResponse ResourceEfficiencyScore,
    decimal? TeamPerformanceScore,
    string TeamPerformanceStatus,
    string ProfileFormulaVersion,
    string? TeamPerformanceFormulaVersion,
    string? PhaseRegistryVersion,
    string? NormalizationConfigVersion,
    string SourceTelemetrySchemaVersion,
    string SourceFingerprint,
    string ProjectionFingerprint,
    DateTime UpdatedAtUtc);
