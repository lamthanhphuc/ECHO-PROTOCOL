using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IPlayerAIProfilePolicy
{
    string ProfileFormulaVersion { get; }
    string MatchScoreFormulaVersion { get; }
    string? NormalizationConfigVersion { get; }
    string? ProfileNoiseFilterVersion { get; }
    string? AlphaConfigVersion { get; }
    decimal? SurvivalAlpha { get; }
    decimal? NoiseAlpha { get; }
    decimal? ProfileNoiseCountMin { get; }
    decimal? ProfileNoiseCountMax { get; }

    bool IsNoisePenalty(string noiseType);
    bool TryValidate(PlayerAIDimension dimension, out string reason);
}
