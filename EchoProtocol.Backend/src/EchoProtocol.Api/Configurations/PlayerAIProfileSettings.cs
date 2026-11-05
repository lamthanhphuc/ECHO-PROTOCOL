namespace EchoProtocol.Api.Configurations;

public sealed class PlayerAIProfileSettings
{
    public const string SectionName = "PlayerAIProfile";

    public string ProfileFormulaVersion { get; init; } = "PROFILE_FORMULA_V1_1";
    public string MatchScoreFormulaVersion { get; init; } = "PLAYER_MATCH_SCORE_V1_1";
    public string? NormalizationConfigVersion { get; init; }
    public string? ProfileNoiseFilterVersion { get; init; }
    public string? AlphaConfigVersion { get; init; }
    public decimal? SurvivalAlpha { get; init; }
    public decimal? NoiseAlpha { get; init; }
    public decimal? ProfileNoiseCountMin { get; init; }
    public decimal? ProfileNoiseCountMax { get; init; }
    public string[] NoisePenaltyTypes { get; init; } = [];
}
