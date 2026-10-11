using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Services;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class AEDPreMatchPolicySettingsTests
{
    private static AEDPreMatchPolicySettings Valid() => new()
    {
        Enabled = false,
        PolicyVersion = ScenarioConfigValidator.SupportedPolicyVersion,
        PolicyConfigVersion = "AED_POLICY_CONFIG_V1_1",
        EvidencePolicyVersion = "AED_EVIDENCE_POLICY_V1_1",
        ParameterRegistryVersion = "AED_PARAMETER_REGISTRY_V1_1",
        ContentWhitelistVersion = "M2-WHITELIST-1",
        SurvivalLowThreshold = 30,
        SurvivalHighThreshold = 70,
        NoiseLowThreshold = 30,
        NoiseHighThreshold = 70,
        MinimumSurvivalSampleCountPerObservedPlayer = 1,
        MinimumNoiseSampleCountPerObservedPlayer = 1,
        RequireFullRosterObservedCoverage = true,
        SupportItemBudgetCandidateValues = [0, 1, 2],
        ChaseSpeedCandidateValues = [9, 10, 11]
    };

    [Fact, Trait("Category", "AEDv2")]
    public void ValidVersionedPolicy_IsAccepted()
    {
        var config = Valid();

        Assert.True(config.IsContractValid());
        Assert.False(config.Enabled);
    }

    [Fact, Trait("Category", "AEDv2")]
    public void InvalidVersionsAndThresholds_AreRejected()
    {
        var config = Valid();

        config.PolicyVersion = "UNSUPPORTED";
        Assert.False(config.IsContractValid());

        config = Valid();
        config.SurvivalLowThreshold = 70;
        config.SurvivalHighThreshold = 30;
        Assert.False(config.IsContractValid());

        config = Valid();
        config.ParameterRegistryVersion = "UNKNOWN";
        Assert.False(config.IsContractValid());
    }

    [Fact, Trait("Category", "AEDv2")]
    public void InvalidParameterValues_AreRejected()
    {
        var config = Valid();

        config.SupportItemBudgetCandidateValues = [0, 2, 2];
        Assert.False(config.IsContractValid());

        config = Valid();
        config.ChaseSpeedCandidateValues = [10, 11];
        Assert.False(config.IsContractValid());

        config = Valid();
        config.SupportItemBudgetCandidateValues = [0, 1.5, 2];
        Assert.False(config.IsContractValid());
    }

    [Fact, Trait("Category", "AEDv2")]
    public void IncompleteEvidencePolicy_IsRejected()
    {
        var config = Valid();
        config.RequireFullRosterObservedCoverage = false;

        Assert.False(config.IsContractValid());
    }
}
