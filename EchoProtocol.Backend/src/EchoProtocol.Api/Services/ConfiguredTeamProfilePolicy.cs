using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace EchoProtocol.Api.Services;

public sealed class ConfiguredTeamProfilePolicy : ITeamProfilePolicy
{
    private readonly HashSet<string> _allowedOverlapPairs;

    public ConfiguredTeamProfilePolicy(IOptions<TeamProfileSettings> options)
    {
        var value = options.Value;
        ProfileFormulaVersion = value.ProfileFormulaVersion;
        TeamPerformanceFormulaVersion = value.TeamPerformanceFormulaVersion;
        PhaseRegistryVersion = value.PhaseRegistryVersion;
        NormalizationConfigVersion = value.NormalizationConfigVersion;
        ObjectiveTimeMin = value.ObjectiveTimeMin;
        ObjectiveTimeMax = value.ObjectiveTimeMax;
        ObjectiveWeight = value.ObjectiveWeight;
        SurvivalWeight = value.SurvivalWeight;
        TeamworkWeight = value.TeamworkWeight;
        ResourceWeight = value.ResourceWeight;
        ObjectiveBearingPhases = value.ObjectiveBearingPhases.ToHashSet(StringComparer.Ordinal);
        _allowedOverlapPairs = value.AllowedOverlapPairs.ToHashSet(StringComparer.Ordinal);
    }

    public string ProfileFormulaVersion { get; }
    public string? TeamPerformanceFormulaVersion { get; }
    public string? PhaseRegistryVersion { get; }
    public string? NormalizationConfigVersion { get; }
    public decimal? ObjectiveTimeMin { get; }
    public decimal? ObjectiveTimeMax { get; }
    public decimal? ObjectiveWeight { get; }
    public decimal? SurvivalWeight { get; }
    public decimal? TeamworkWeight { get; }
    public decimal? ResourceWeight { get; }
    public IReadOnlySet<string> ObjectiveBearingPhases { get; }

    public bool IsOverlapAllowed(string firstPhase, string secondPhase) =>
        _allowedOverlapPairs.Contains(CanonicalPair(firstPhase, secondPhase));

    public bool TryValidatePhaseRegistry(out string reason)
    {
        if (string.IsNullOrWhiteSpace(PhaseRegistryVersion) || ObjectiveBearingPhases.Count == 0 ||
            ObjectiveBearingPhases.Any(string.IsNullOrWhiteSpace))
        {
            reason = "Objective-bearing gameplay phase registry is not configured";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool TryValidateObjectiveNormalization(out string reason)
    {
        if (string.IsNullOrWhiteSpace(NormalizationConfigVersion) ||
            ObjectiveTimeMin is null || ObjectiveTimeMax is null || ObjectiveTimeMax <= ObjectiveTimeMin)
        {
            reason = "ObjectiveTime Min/Max normalization is not configured or invalid";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool TryValidateTeamPerformanceWeights(out string reason)
    {
        var weights = new[] { ObjectiveWeight, SurvivalWeight, TeamworkWeight, ResourceWeight };
        if (string.IsNullOrWhiteSpace(TeamPerformanceFormulaVersion) ||
            weights.Any(item => item is null or < 0) || weights.Sum(item => item!.Value) != 1m)
        {
            reason = "Four-component TeamPerformance weights are not configured or invalid";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static string CanonicalPair(string first, string second) =>
        string.CompareOrdinal(first, second) <= 0 ? $"{first}|{second}" : $"{second}|{first}";
}
