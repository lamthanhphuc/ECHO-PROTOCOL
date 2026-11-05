using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace EchoProtocol.Api.Services;

public sealed class ConfiguredPlayerAIProfilePolicy : IPlayerAIProfilePolicy
{
    private readonly HashSet<string> _noisePenaltyTypes;

    public ConfiguredPlayerAIProfilePolicy(IOptions<PlayerAIProfileSettings> options)
    {
        var value = options.Value;
        ProfileFormulaVersion = value.ProfileFormulaVersion;
        MatchScoreFormulaVersion = value.MatchScoreFormulaVersion;
        NormalizationConfigVersion = value.NormalizationConfigVersion;
        ProfileNoiseFilterVersion = value.ProfileNoiseFilterVersion;
        AlphaConfigVersion = value.AlphaConfigVersion;
        SurvivalAlpha = value.SurvivalAlpha;
        NoiseAlpha = value.NoiseAlpha;
        ProfileNoiseCountMin = value.ProfileNoiseCountMin;
        ProfileNoiseCountMax = value.ProfileNoiseCountMax;
        _noisePenaltyTypes = value.NoisePenaltyTypes.ToHashSet(StringComparer.Ordinal);
    }

    public string ProfileFormulaVersion { get; }
    public string MatchScoreFormulaVersion { get; }
    public string? NormalizationConfigVersion { get; }
    public string? ProfileNoiseFilterVersion { get; }
    public string? AlphaConfigVersion { get; }
    public decimal? SurvivalAlpha { get; }
    public decimal? NoiseAlpha { get; }
    public decimal? ProfileNoiseCountMin { get; }
    public decimal? ProfileNoiseCountMax { get; }

    public bool IsNoisePenalty(string noiseType) => _noisePenaltyTypes.Contains(noiseType);

    public bool TryValidate(PlayerAIDimension dimension, out string reason)
    {
        if (!string.Equals(ProfileFormulaVersion, "PROFILE_FORMULA_V1_1", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(MatchScoreFormulaVersion) ||
            string.IsNullOrWhiteSpace(AlphaConfigVersion))
        {
            reason = "AI profile formula/alpha version is not configured for v1.1";
            return false;
        }

        var alpha = dimension == PlayerAIDimension.Survival ? SurvivalAlpha : NoiseAlpha;
        if (alpha is null or <= 0 or > 1)
        {
            reason = $"EMA alpha for {dimension} is not configured or outside (0,1]";
            return false;
        }

        if (dimension == PlayerAIDimension.Noise &&
            (string.IsNullOrWhiteSpace(NormalizationConfigVersion) ||
             string.IsNullOrWhiteSpace(ProfileNoiseFilterVersion) ||
             _noisePenaltyTypes.Count == 0 ||
             ProfileNoiseCountMin is null ||
             ProfileNoiseCountMax is null ||
             ProfileNoiseCountMax <= ProfileNoiseCountMin))
        {
            reason = "Noise filter and normalization configuration is not approved/configured";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
