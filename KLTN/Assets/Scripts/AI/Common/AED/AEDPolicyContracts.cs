using System;
using System.Collections.Generic;

namespace EchoProtocol.AI.Common.AED
{
    public enum ScoreBand { Low, Mid, High }

    public sealed class ScoreBandThresholds
    {
        public ScoreBandThresholds(double lowThreshold, double highThreshold)
        {
            ScenarioMonsterParameters.RequireFinite(lowThreshold, nameof(lowThreshold));
            ScenarioMonsterParameters.RequireFinite(highThreshold, nameof(highThreshold));
            LowThreshold = lowThreshold;
            HighThreshold = highThreshold;
        }

        public double LowThreshold { get; }
        public double HighThreshold { get; }
        public bool IsValid => LowThreshold > 0d && LowThreshold < HighThreshold && HighThreshold < 100d;

        public ScoreBand Classify(double score)
        {
            ScenarioMonsterParameters.RequireFinite(score, nameof(score));
            if (!IsValid) throw new InvalidOperationException("Score thresholds are invalid.");
            if (score < 0d || score > 100d) throw new ArgumentOutOfRangeException(nameof(score));
            if (score < LowThreshold) return ScoreBand.Low;
            if (score < HighThreshold) return ScoreBand.Mid;
            return ScoreBand.High;
        }
    }

    public sealed class AEDPolicyConfig
    {
        public AEDPolicyConfig(
            string policyVersion,
            string policyConfigVersion,
            string evidencePolicyVersion,
            string parameterRegistryVersion,
            string contentWhitelistVersion,
            ScoreBandThresholds survivalThresholds,
            ScoreBandThresholds noiseThresholds)
        {
            PolicyVersion = ScenarioConfig.RequireText(policyVersion, nameof(policyVersion));
            PolicyConfigVersion = ScenarioConfig.RequireText(policyConfigVersion, nameof(policyConfigVersion));
            EvidencePolicyVersion = ScenarioConfig.RequireText(evidencePolicyVersion, nameof(evidencePolicyVersion));
            ParameterRegistryVersion = ScenarioConfig.RequireText(parameterRegistryVersion, nameof(parameterRegistryVersion));
            ContentWhitelistVersion = ScenarioConfig.RequireText(contentWhitelistVersion, nameof(contentWhitelistVersion));
            SurvivalThresholds = survivalThresholds ?? throw new ArgumentNullException(nameof(survivalThresholds));
            NoiseThresholds = noiseThresholds ?? throw new ArgumentNullException(nameof(noiseThresholds));
        }

        public string PolicyVersion { get; }
        public string PolicyConfigVersion { get; }
        public string EvidencePolicyVersion { get; }
        public string ParameterRegistryVersion { get; }
        public string ContentWhitelistVersion { get; }
        public ScoreBandThresholds SurvivalThresholds { get; }
        public ScoreBandThresholds NoiseThresholds { get; }

        public bool IsValid =>
            PolicyVersion == AEDPolicyDefinition.PolicySemanticId
            && SurvivalThresholds.IsValid
            && NoiseThresholds.IsValid;
    }

    public sealed class AEDEvidencePolicy
    {
        public AEDEvidencePolicy(
            string evidencePolicyVersion,
            int minimumSurvivalSampleCountPerObservedPlayer,
            int minimumNoiseSampleCountPerObservedPlayer,
            bool requireFullRosterObservedCoverage)
        {
            EvidencePolicyVersion = ScenarioConfig.RequireText(evidencePolicyVersion, nameof(evidencePolicyVersion));
            MinimumSurvivalSampleCountPerObservedPlayer = minimumSurvivalSampleCountPerObservedPlayer;
            MinimumNoiseSampleCountPerObservedPlayer = minimumNoiseSampleCountPerObservedPlayer;
            RequireFullRosterObservedCoverage = requireFullRosterObservedCoverage;
        }

        public string EvidencePolicyVersion { get; }
        public int MinimumSurvivalSampleCountPerObservedPlayer { get; }
        public int MinimumNoiseSampleCountPerObservedPlayer { get; }
        public bool RequireFullRosterObservedCoverage { get; }

        public bool IsValid =>
            MinimumSurvivalSampleCountPerObservedPlayer >= 1
            && MinimumNoiseSampleCountPerObservedPlayer >= 1
            && RequireFullRosterObservedCoverage;
    }

    public sealed class AdaptiveParameterRule
    {
        public AdaptiveParameterRule(
            ScenarioConfigKey key,
            double defaultValue,
            double minValue,
            double maxValue,
            IReadOnlyList<double> candidateValues,
            IReadOnlyList<ScenarioDecisionPoint> allowedTiming,
            PressureAxis pressureAxis,
            string adjustmentRuleId)
        {
            ScenarioMonsterParameters.RequireFinite(defaultValue, nameof(defaultValue));
            ScenarioMonsterParameters.RequireFinite(minValue, nameof(minValue));
            ScenarioMonsterParameters.RequireFinite(maxValue, nameof(maxValue));
            if (minValue > maxValue) throw new ArgumentOutOfRangeException(nameof(minValue));
            if (defaultValue < minValue || defaultValue > maxValue) throw new ArgumentOutOfRangeException(nameof(defaultValue));

            Key = key;
            DefaultValue = defaultValue;
            MinValue = minValue;
            MaxValue = maxValue;
            CandidateValues = NormalizeCandidates(key, candidateValues, minValue, maxValue);
            AllowedTiming = CopyTiming(allowedTiming);
            PressureAxis = pressureAxis;
            AdjustmentRuleId = ScenarioConfig.RequireText(adjustmentRuleId, nameof(adjustmentRuleId));
        }

        public ScenarioConfigKey Key { get; }
        public double DefaultValue { get; }
        public double MinValue { get; }
        public double MaxValue { get; }
        public IReadOnlyList<double> CandidateValues { get; }
        public IReadOnlyList<ScenarioDecisionPoint> AllowedTiming { get; }
        public PressureAxis PressureAxis { get; }
        public string AdjustmentRuleId { get; }

        public bool Allows(ScenarioDecisionPoint decisionPoint)
        {
            for (var i = 0; i < AllowedTiming.Count; i++)
            {
                if (AllowedTiming[i] == decisionPoint) return true;
            }

            return false;
        }

        public bool TryGetNextHigher(double currentValue, out double nextValue)
        {
            ScenarioMonsterParameters.RequireFinite(currentValue, nameof(currentValue));
            for (var i = 0; i < CandidateValues.Count; i++)
            {
                if (CandidateValues[i] > currentValue)
                {
                    nextValue = CandidateValues[i];
                    return true;
                }
            }

            nextValue = currentValue;
            return false;
        }

        private static IReadOnlyList<double> NormalizeCandidates(
            ScenarioConfigKey key,
            IReadOnlyList<double> values,
            double minValue,
            double maxValue)
        {
            if (values == null || values.Count == 0)
            {
                throw new ArgumentException("Candidate values are required.", nameof(values));
            }

            var copy = new List<double>(values.Count);
            for (var i = 0; i < values.Count; i++)
            {
                var value = values[i];
                ScenarioMonsterParameters.RequireFinite(value, nameof(values));
                if (value < minValue || value > maxValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(values));
                }

                if (key == ScenarioConfigKey.SupportItemBudget && Math.Abs(value - Math.Round(value)) > 0.000001d)
                {
                    throw new ArgumentException("SupportItemBudget candidates must be integer values.", nameof(values));
                }

                copy.Add(value);
            }

            copy.Sort();
            for (var i = 1; i < copy.Count; i++)
            {
                if (Math.Abs(copy[i] - copy[i - 1]) <= 0.000001d)
                {
                    throw new ArgumentException("Candidate values must be unique after sorting.", nameof(values));
                }
            }

            return copy.AsReadOnly();
        }

        private static IReadOnlyList<ScenarioDecisionPoint>
            CopyTiming(
                IReadOnlyList<ScenarioDecisionPoint> values)
        {
            if (values == null
                || values.Count == 0)
            {
                throw new ArgumentException(
                    "Allowed timing is required.",
                    nameof(values));
            }

            var copy =
                new List<ScenarioDecisionPoint>(
                    values.Count);

            for (var i = 0;
                 i < values.Count;
                 i++)
            {
                copy.Add(values[i]);
            }

            copy.Sort();

            for (var i = 1;
                 i < copy.Count;
                 i++)
            {
                if (copy[i] == copy[i - 1])
                {
                    throw new ArgumentException(
                        "Allowed timing entries must be unique.",
                        nameof(values));
                }
            }

            return copy.AsReadOnly();
        }
    }

    public sealed class AdaptiveParameterRegistry
    {
        private readonly List<AdaptiveParameterRule> _rules;

        public AdaptiveParameterRegistry(string parameterRegistryVersion, IReadOnlyList<AdaptiveParameterRule> rules)
        {
            ParameterRegistryVersion = ScenarioConfig.RequireText(parameterRegistryVersion, nameof(parameterRegistryVersion));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            _rules = new List<AdaptiveParameterRule>(rules.Count);
            for (var i = 0; i < rules.Count; i++)
            {
                var rule = rules[i] ?? throw new ArgumentNullException(nameof(rules));
                if (TryGetRule(rule.Key, out _)) throw new ArgumentException("Duplicate parameter rule.", nameof(rules));
                _rules.Add(rule);
            }

            _rules.Sort((left, right) => left.Key.CompareTo(right.Key));
        }

        public string ParameterRegistryVersion { get; }
        public IReadOnlyList<AdaptiveParameterRule> Rules => _rules.AsReadOnly();

        public bool TryGetRule(ScenarioConfigKey key, out AdaptiveParameterRule rule)
        {
            for (var i = 0; i < _rules.Count; i++)
            {
                if (_rules[i].Key == key)
                {
                    rule = _rules[i];
                    return true;
                }
            }

            rule = null;
            return false;
        }

        public bool HasCurrentPolicyActiveRules()
        {
            if (!TryGetRule(
                    ScenarioConfigKey.SupportItemBudget,
                    out var supportRule)
                || !TryGetRule(
                    ScenarioConfigKey.ChaseSpeed,
                    out var chaseRule))
            {
                return false;
            }

            if (!string.Equals(
                    supportRule.AdjustmentRuleId,
                    AEDPolicyDefinition.NextHigherRegisteredValueStrategyId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    chaseRule.AdjustmentRuleId,
                    AEDPolicyDefinition.NextHigherRegisteredValueStrategyId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            if (supportRule.PressureAxis != PressureAxis.None
                || chaseRule.PressureAxis != PressureAxis.ChasePressure)
            {
                return false;
            }

            if (!supportRule.Allows(ScenarioDecisionPoint.PreMatch)
                || !chaseRule.Allows(ScenarioDecisionPoint.PreMatch))
            {
                return false;
            }

            return true;
        }
        public bool IsCurrentPolicyBaseCompatible(
            ScenarioConfig baseConfig)
        {
            if (baseConfig == null
                || !HasCurrentPolicyActiveRules())
            {
                return false;
            }

            if (!TryGetRule(
                    ScenarioConfigKey.SupportItemBudget,
                    out var supportRule)
                || !TryGetRule(
                    ScenarioConfigKey.ChaseSpeed,
                    out var chaseRule))
            {
                return false;
            }

            return ContainsExact(
                    supportRule.CandidateValues,
                    baseConfig.SupportItemBudget)
                && ContainsExact(
                    chaseRule.CandidateValues,
                    baseConfig.MonsterParameters.ChaseSpeed);
        }

        private static bool ContainsExact(
            IReadOnlyList<double> values,
            double expected)
        {
            if (values == null)
            {
                return false;
            }

            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] == expected)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
