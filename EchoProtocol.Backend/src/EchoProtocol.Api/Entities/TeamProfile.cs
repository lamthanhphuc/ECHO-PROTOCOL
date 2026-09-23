using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.Entities;

/// <summary>Current match-scoped TeamProfile projection. MatchId is the teamKey.</summary>
public sealed class TeamProfile
{
    public Guid MatchId { get; set; }
    public long ProcessingRevision { get; set; }
    public TeamProfileProcessingStatus ProcessingStatus { get; set; }
    public string ProcessingReason { get; set; } = string.Empty;
    public string TelemetryCompleteness { get; set; } = string.Empty;

    public decimal? ObjectiveTimeSeconds { get; set; }
    public TeamMetricStatus ObjectiveTimeStatus { get; set; }

    public decimal? SplitTime { get; set; }
    public TeamMetricStatus SplitTimeStatus { get; set; }
    public decimal? AvgDistance { get; set; }
    public TeamMetricStatus AvgDistanceStatus { get; set; }
    public decimal? ReviveSuccess { get; set; }
    public TeamMetricStatus ReviveSuccessStatus { get; set; }
    public decimal? ResourceEfficiency { get; set; }
    public TeamMetricStatus ResourceEfficiencyStatus { get; set; }
    public decimal? Communication { get; set; }
    public TeamMetricStatus CommunicationStatus { get; set; }
    public decimal? WipeRecovery { get; set; }
    public TeamMetricStatus WipeRecoveryStatus { get; set; }

    public decimal? ObjectiveSpeedScore { get; set; }
    public TeamMetricStatus ObjectiveSpeedStatus { get; set; }
    public decimal? SurvivalScore { get; set; }
    public TeamMetricStatus SurvivalStatus { get; set; }
    public decimal? TeamworkScore { get; set; }
    public TeamMetricStatus TeamworkStatus { get; set; }
    public decimal? ResourceEfficiencyScore { get; set; }
    public TeamMetricStatus ResourceEfficiencyScoreStatus { get; set; }
    public decimal? TeamPerformanceScore { get; set; }
    public TeamPerformanceStatus TeamPerformanceStatus { get; set; }

    public string ProfileFormulaVersion { get; set; } = string.Empty;
    public string? TeamPerformanceFormulaVersion { get; set; }
    public string? PhaseRegistryVersion { get; set; }
    public string? NormalizationConfigVersion { get; set; }
    public string SourceTelemetrySchemaVersion { get; set; } = string.Empty;
    public string SourceFingerprint { get; set; } = string.Empty;
    public string ProjectionFingerprint { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public MatchResult MatchResult { get; set; } = null!;
}
