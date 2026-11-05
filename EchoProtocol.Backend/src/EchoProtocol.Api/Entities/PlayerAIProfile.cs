using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.Entities;

public sealed class PlayerAIProfile
{
    public Guid UserId { get; set; }
    public Guid ProfileLineageId { get; set; }
    public long ProfileRevision { get; set; }
    public string ProfileFormulaVersion { get; set; } = string.Empty;
    public string MatchScoreFormulaVersion { get; set; } = string.Empty;
    public string? NormalizationConfigVersion { get; set; }
    public string? ProfileNoiseFilterVersion { get; set; }
    public string AlphaConfigVersion { get; set; } = string.Empty;

    public decimal SurvivalScore { get; set; }
    public ProfileDimensionStatus SurvivalStatus { get; set; }
    public int SurvivalSampleCount { get; set; }
    public DateTime? SurvivalLastMatchEndTs { get; set; }
    public Guid? SurvivalLastMatchId { get; set; }
    public DateTime? SurvivalLastUpdatedAtUtc { get; set; }

    public decimal NoiseScore { get; set; }
    public ProfileDimensionStatus NoiseStatus { get; set; }
    public int NoiseSampleCount { get; set; }
    public DateTime? NoiseLastMatchEndTs { get; set; }
    public Guid? NoiseLastMatchId { get; set; }
    public DateTime? NoiseLastUpdatedAtUtc { get; set; }

    // Contract v1.1 dimensions whose formulas are DEFERRED. They must remain null.
    public decimal? ObjectiveScore { get; set; }
    public decimal? TeamworkScore { get; set; }
    public decimal? ExplorationScore { get; set; }
    public decimal? NavigationScore { get; set; }
    public decimal? ToolUsageScore { get; set; }
    public decimal? RiskScore { get; set; }
    public decimal? ReviveScore { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public ICollection<MatchScore> MatchScores { get; set; } = [];
}
