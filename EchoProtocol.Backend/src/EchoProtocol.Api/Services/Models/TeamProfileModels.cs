using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.Services.Models;

public sealed record TeamProfileProjection(
    Guid MatchId,
    TeamProfileProcessingStatus ProcessingStatus,
    string ProcessingReason,
    TelemetryCompleteness Completeness,
    decimal? ObjectiveTimeSeconds,
    TeamMetricStatus ObjectiveTimeStatus,
    decimal? ObjectiveSpeedScore,
    TeamMetricStatus ObjectiveSpeedStatus,
    decimal? SurvivalScore,
    TeamMetricStatus SurvivalStatus,
    TeamPerformanceStatus TeamPerformanceStatus,
    decimal? TeamPerformanceScore,
    string SourceTelemetrySchemaVersion,
    string SourceFingerprint,
    string ProjectionFingerprint);

public sealed record TeamProfileProcessResult(
    Guid MatchId,
    long ProcessingRevision,
    bool IsDuplicate,
    TeamProfileProcessingStatus ProcessingStatus);
