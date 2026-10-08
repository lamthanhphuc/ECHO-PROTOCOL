using System;
using EchoProtocol.AI.Common.AED;
using UnityEngine;

namespace EchoProtocol.AI.AED
{
    [CreateAssetMenu(menuName = "Echo Protocol/AI/AED Runtime Settings")]
    public sealed class AEDRuntimeSettings : ScriptableObject
    {
        [SerializeField] private string policyConfigVersion;
        [SerializeField] private string evidencePolicyVersion;
        [SerializeField] private string parameterRegistryVersion;
        [SerializeField] private string contentWhitelistVersion;
        [SerializeField] private double survivalLowThreshold;
        [SerializeField] private double survivalHighThreshold;
        [SerializeField] private double noiseLowThreshold;
        [SerializeField] private double noiseHighThreshold;
        [SerializeField] private int minimumSurvivalSampleCountPerObservedPlayer;
        [SerializeField] private int minimumNoiseSampleCountPerObservedPlayer;
        [SerializeField] private bool requireFullRosterObservedCoverage = true;
        [SerializeField] private double supportItemBudgetDefaultValue;
        [SerializeField] private double supportItemBudgetMinValue;
        [SerializeField] private double supportItemBudgetMaxValue;
        [SerializeField] private double[] supportItemBudgetCandidateValues;
        [SerializeField] private double chaseSpeedDefaultValue;
        [SerializeField] private double chaseSpeedMinValue;
        [SerializeField] private double chaseSpeedMaxValue;
        [SerializeField] private double[] chaseSpeedCandidateValues;

        public bool TryBuildPolicyConfig(out AEDPolicyConfig config, out string reason)
        {
            config = null;
            reason = string.Empty;
            try
            {
                config = new AEDPolicyConfig(
                    AEDPolicyDefinition.PolicySemanticId,
                    policyConfigVersion,
                    evidencePolicyVersion,
                    parameterRegistryVersion,
                    contentWhitelistVersion,
                    new ScoreBandThresholds(survivalLowThreshold, survivalHighThreshold),
                    new ScoreBandThresholds(noiseLowThreshold, noiseHighThreshold));
                if (!config.IsValid)
                {
                    reason = "POLICY_CONFIG_INVALID";
                    config = null;
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                reason = exception.GetType().Name;
                return false;
            }
        }

        public bool TryBuildEvidencePolicy(out AEDEvidencePolicy policy, out string reason)
        {
            policy = null;
            reason = string.Empty;
            try
            {
                policy = new AEDEvidencePolicy(
                    evidencePolicyVersion,
                    minimumSurvivalSampleCountPerObservedPlayer,
                    minimumNoiseSampleCountPerObservedPlayer,
                    requireFullRosterObservedCoverage);
                if (!policy.IsValid)
                {
                    reason = "EVIDENCE_POLICY_INVALID";
                    policy = null;
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                reason = exception.GetType().Name;
                return false;
            }
        }

        public bool TryBuildParameterRegistry(out AdaptiveParameterRegistry registry, out string reason)
        {
            registry = null;
            reason = string.Empty;
            try
            {
                if (!HasExplicitRange(
                        supportItemBudgetDefaultValue,
                        supportItemBudgetMinValue,
                        supportItemBudgetMaxValue)
                    || !HasExplicitRange(
                        chaseSpeedDefaultValue,
                        chaseSpeedMinValue,
                        chaseSpeedMaxValue))
                {
                    reason = "PARAMETER_REGISTRY_INVALID";
                    return false;
                }

                registry = new AdaptiveParameterRegistry(
                    parameterRegistryVersion,
                    new[]
                    {
                        new AdaptiveParameterRule(
                            ScenarioConfigKey.SupportItemBudget,
                            supportItemBudgetDefaultValue,
                            supportItemBudgetMinValue,
                            supportItemBudgetMaxValue,
                            supportItemBudgetCandidateValues,
                            new[] { ScenarioDecisionPoint.PreMatch },
                            PressureAxis.None,
                            AEDPolicyDefinition.NextHigherRegisteredValueStrategyId),
                        new AdaptiveParameterRule(
                            ScenarioConfigKey.ChaseSpeed,
                            chaseSpeedDefaultValue,
                            chaseSpeedMinValue,
                            chaseSpeedMaxValue,
                            chaseSpeedCandidateValues,
                            new[] { ScenarioDecisionPoint.PreMatch },
                            PressureAxis.ChasePressure,
                            AEDPolicyDefinition.NextHigherRegisteredValueStrategyId)
                    });
                if (!registry.HasCurrentPolicyActiveRules())
                {
                    reason = "PARAMETER_REGISTRY_INVALID";
                    registry = null;
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                reason = exception.GetType().Name;
                return false;
            }
        }

        private static bool HasExplicitRange(double defaultValue, double minValue, double maxValue)
        {
            return !double.IsNaN(defaultValue)
                   && !double.IsInfinity(defaultValue)
                   && !double.IsNaN(minValue)
                   && !double.IsInfinity(minValue)
                   && !double.IsNaN(maxValue)
                   && !double.IsInfinity(maxValue)
                   && minValue < maxValue
                   && defaultValue >= minValue
                   && defaultValue <= maxValue;
        }
    }
}
