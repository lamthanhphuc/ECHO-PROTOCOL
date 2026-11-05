using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.Entities;

public sealed class AdaptiveInputSnapshot
{
    public Guid SnapshotId { get; set; }
    public Guid MatchId { get; set; }
    public string DecisionPoint { get; set; } = "PRE_MATCH";
    public string SnapshotContentFingerprint { get; set; } = string.Empty;
    public string FingerprintVersion { get; set; } = "V1";
    public string RosterIdentity { get; set; } = string.Empty;
    public int TeamSize { get; set; }
    public AdaptiveSnapshotValidity Validity { get; set; }
    public string ReasonCodesJson { get; set; } = "[]";
    public string? ProfileFormulaSemanticId { get; set; }
    public string? SurvivalComparisonKey { get; set; }
    public string? NoiseComparisonKey { get; set; }
    public string SurvivalAggregationStatus { get; set; } = string.Empty;
    public string NoiseAggregationStatus { get; set; } = string.Empty;
    public decimal? SurvivalMeanObservedScore { get; set; }
    public decimal? NoiseMeanObservedScore { get; set; }
    public int SurvivalObservedActiveCount { get; set; }
    public int NoiseObservedActiveCount { get; set; }
    public string ObjectiveAggregationStatus { get; set; } = "UNAVAILABLE";
    public string? ObjectiveComparisonKey { get; set; }
    public decimal? ObjectiveMeanObservedScore { get; set; }
    public int ObjectiveObservedActiveCount { get; set; }
    public string ToolUsageAggregationStatus { get; set; } = "UNAVAILABLE";
    public string? ToolUsageComparisonKey { get; set; }
    public decimal? ToolUsageMeanObservedScore { get; set; }
    public int ToolUsageObservedActiveCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public ICollection<AdaptiveInputSnapshotPlayer> Players { get; set; } = [];
    public ScenarioDecision? Decision { get; set; }
}
