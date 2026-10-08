using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using EchoProtocol.Api.Services.Models;

namespace EchoProtocol.Api.Services;

public static class MatchScoreNormalizer
{
    public static bool TryNormalize(
        AggregatedMetric metric,
        IPlayerAIProfilePolicy policy,
        out NormalizedMatchScore? result,
        out string reason)
    {
        result = null;
        if (metric.Availability != MetricAvailability.Available || metric.RawValue is null)
        {
            reason = metric.Reason;
            return false;
        }

        if (!policy.TryValidate(metric.Dimension, out reason))
        {
            return false;
        }

        decimal score;
        switch (metric.Dimension)
        {
            case PlayerAIDimension.Survival when metric.RawValue is 0m or 100m:
                score = metric.RawValue.Value;
                break;
            case PlayerAIDimension.Noise:
                var min = policy.ProfileNoiseCountMin!.Value;
                var max = policy.ProfileNoiseCountMax!.Value;
                var normalized = Math.Clamp((metric.RawValue.Value - min) / (max - min), 0m, 1m);
                score = 100m * (1m - normalized);
                break;
            case PlayerAIDimension.Objective when metric.RawValue is 0m or 100m:
                score = metric.RawValue.Value;
                break;
            case PlayerAIDimension.ToolUsage:
                var maxTools = policy.ToolUsageCountMax!.Value;
                score = 100m * Math.Clamp(metric.RawValue.Value / maxTools, 0m, 1m);
                break;
            default:
                reason = "MATCH_SCORE_INPUT_INVALID";
                return false;
        }

        var dimensionFormula = metric.Dimension switch
        {
            PlayerAIDimension.Objective => "MATCH_SCORE_OBJECTIVE_V1",
            PlayerAIDimension.ToolUsage => "MATCH_SCORE_TOOL_USAGE_V1",
            _ => policy.MatchScoreFormulaVersion
        };
        var normalizationVersion = metric.Dimension switch
        {
            PlayerAIDimension.Noise => policy.NormalizationConfigVersion!,
            PlayerAIDimension.ToolUsage =>
                $"TOOL_USAGE_COUNT_MAX_{policy.ToolUsageCountMax!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}_V1",
            _ => "NOT_APPLICABLE"
        };

        result = new NormalizedMatchScore(
            metric.Dimension,
            Math.Clamp(score, 0m, 100m),
            dimensionFormula,
            normalizationVersion,
            metric.Dimension == PlayerAIDimension.Noise ? policy.ProfileNoiseFilterVersion : null,
            metric.EvidenceFingerprint);
        reason = string.Empty;
        return true;
    }
}
