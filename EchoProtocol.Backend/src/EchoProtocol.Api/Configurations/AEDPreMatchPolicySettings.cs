using EchoProtocol.Api.Services;

namespace EchoProtocol.Api.Configurations;

public sealed class AEDPreMatchPolicySettings
{
    public const string SectionName = "AEDPreMatchPolicy";

    public bool Enabled { get; set; }

    public string PolicyVersion { get; set; } = "";
    public string PolicyConfigVersion { get; set; } = "";
    public string EvidencePolicyVersion { get; set; } = "";
    public string ParameterRegistryVersion { get; set; } = "";
    public string ContentWhitelistVersion { get; set; } = "";

    public decimal SurvivalLowThreshold { get; set; }
    public decimal SurvivalHighThreshold { get; set; }
    public decimal NoiseLowThreshold { get; set; }
    public decimal NoiseHighThreshold { get; set; }

    public int MinimumSurvivalSampleCountPerObservedPlayer { get; set; }
    public int MinimumNoiseSampleCountPerObservedPlayer { get; set; }
    public bool RequireFullRosterObservedCoverage { get; set; }

    public double[] SupportItemBudgetCandidateValues { get; set; } = [];
    public double[] ChaseSpeedCandidateValues { get; set; } = [];

    public bool IsContractValid() =>
        PolicyVersion == ScenarioConfigValidator.SupportedPolicyVersion &&
        PolicyConfigVersion == "AED_POLICY_CONFIG_V1_1" &&
        EvidencePolicyVersion == "AED_EVIDENCE_POLICY_V1_1" &&
        ParameterRegistryVersion == "AED_PARAMETER_REGISTRY_V1_1" &&
        ContentWhitelistVersion == "M2-WHITELIST-1" &&
        ValidThresholds(SurvivalLowThreshold, SurvivalHighThreshold) &&
        ValidThresholds(NoiseLowThreshold, NoiseHighThreshold) &&
        MinimumSurvivalSampleCountPerObservedPlayer >= 1 &&
        MinimumNoiseSampleCountPerObservedPlayer >= 1 &&
        RequireFullRosterObservedCoverage &&
        ValidValues(SupportItemBudgetCandidateValues, 0, true) &&
        ValidValues(ChaseSpeedCandidateValues, 9, false);

    private static bool ValidThresholds(decimal low, decimal high) =>
        low > 0 && low < high && high < 100;

    private static bool ValidValues(
        double[]? values, double baseline, bool integral) =>
        values is { Length: > 0 } &&
        values.All(x =>
            double.IsFinite(x) &&
            x >= 0 &&
            (!integral || x == Math.Truncate(x))) &&
        values.Distinct().Count() == values.Length &&
        values.Contains(baseline);
}
