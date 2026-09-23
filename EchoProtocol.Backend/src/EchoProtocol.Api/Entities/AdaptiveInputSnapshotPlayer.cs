namespace EchoProtocol.Api.Entities;

public sealed class AdaptiveInputSnapshotPlayer
{
    public Guid SnapshotId { get; set; }
    public Guid UserId { get; set; }
    public bool ProfileAvailable { get; set; }
    public Guid? ProfileLineageId { get; set; }
    public long? ProfileRevision { get; set; }
    public string? ProfileFormulaVersion { get; set; }
    public string? MatchScoreFormulaVersion { get; set; }
    public string? NormalizationConfigVersion { get; set; }
    public string? ProfileNoiseFilterVersion { get; set; }
    public string? AlphaConfigVersion { get; set; }
    public decimal? SurvivalScore { get; set; }
    public string SurvivalStatus { get; set; } = string.Empty;
    public int? SurvivalSampleCount { get; set; }
    public string? SurvivalComparisonKey { get; set; }
    public decimal? NoiseScore { get; set; }
    public string NoiseStatus { get; set; } = string.Empty;
    public int? NoiseSampleCount { get; set; }
    public string? NoiseComparisonKey { get; set; }
    public string DeferredDimensionsJson { get; set; } = string.Empty;
    public DateTime CapturedAtUtc { get; set; }
    public AdaptiveInputSnapshot Snapshot { get; set; } = null!;
}
