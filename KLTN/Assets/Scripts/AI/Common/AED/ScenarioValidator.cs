using System;
using System.Collections.Generic;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class ScenarioValidationResult
    {
        public ScenarioValidationResult(CandidateValidationStatus status, IReadOnlyList<string> reasonCodes)
        {
            Status = status;
            ReasonCodes = Copy(reasonCodes);
        }

        public CandidateValidationStatus Status { get; }
        public IReadOnlyList<string> ReasonCodes { get; }
        public bool IsValid => Status == CandidateValidationStatus.Valid;

        private static IReadOnlyList<string> Copy(IReadOnlyList<string> values)
        {
            if (values == null) return Array.Empty<string>();
            var copy = new List<string>(values.Count);
            for (var i = 0; i < values.Count; i++) copy.Add(values[i] ?? string.Empty);
            return copy.AsReadOnly();
        }
    }

    public static class ScenarioValidator
    {
        public const double MinEscapeDoorTimerSeconds = 45d;
        public const double MaxEscapeDoorTimerSeconds = 60d;

        public static ScenarioValidationResult ValidateFixedBaseline(ScenarioConfig config)
        {
            return ValidateConfig(config, null, requirePolicyActiveChange: false);
        }

        public static ScenarioValidationResult ValidateCandidate(
            CandidateScenarioConfig candidate,
            AdaptiveParameterRegistry registry,
            ScenarioDecisionPoint decisionPoint,
            ScenarioConfig baseConfig)
        {
            if (candidate == null)
            {
                return new ScenarioValidationResult(
                    CandidateValidationStatus.NotEvaluated,
                    new[]
                    {
                        AEDReasonCodes.ScenarioInvalid
                    });
            }

            if (baseConfig == null)
            {
                return new ScenarioValidationResult(CandidateValidationStatus.Invalid, new[] { AEDReasonCodes.ScenarioInvalid });
            }

            var expectedBaseRef =
                decisionPoint == ScenarioDecisionPoint.PreMatch
                    ? FixedDirector.CreatePreMatchBaseRef(baseConfig)
                    : FixedDirector.CreateAppliedBaseRef(baseConfig);

            if (!string.Equals(
                    candidate.BaseRef.BaseConfigId,
                    expectedBaseRef.BaseConfigId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    candidate.BaseRef.BaseScenarioConfigVersion,
                    expectedBaseRef.BaseScenarioConfigVersion,
                    StringComparison.Ordinal)
                || !string.Equals(
                    candidate.BaseRef.BaseContentFingerprint,
                    expectedBaseRef.BaseContentFingerprint,
                    StringComparison.Ordinal)
                || candidate.BaseRef.BaseKind != expectedBaseRef.BaseKind)
            {
                return new ScenarioValidationResult(CandidateValidationStatus.Invalid, new[] { AEDReasonCodes.StaleBaseConfig });
            }

            if (!string.Equals(
                    candidate.Config.MapId,
                    baseConfig.MapId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    candidate.Config.MonsterType,
                    baseConfig.MonsterType,
                    StringComparison.Ordinal))
            {
                return new ScenarioValidationResult(CandidateValidationStatus.Invalid, new[] { AEDReasonCodes.ScenarioInvalid });
            }

            if (ViolatesPressureFairness(
                    baseConfig,
                    candidate.Config))
            {
                return new ScenarioValidationResult(
                    CandidateValidationStatus.Invalid,
                    new[]
                    {
                        AEDReasonCodes.PressureRuleRejected
                    });
            }

            if (!string.Equals(
                    candidate.Config.ObjectiveSpawnSetId,
                    baseConfig.ObjectiveSpawnSetId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    candidate.Config.RouteModifier,
                    baseConfig.RouteModifier,
                    StringComparison.Ordinal))
            {
                return new ScenarioValidationResult(CandidateValidationStatus.Invalid, new[] { AEDReasonCodes.PolicyKeyNotActive });
            }

            if (!string.Equals(
                    candidate.Config.FallbackConfigId,
                    baseConfig.FallbackConfigId,
                    StringComparison.Ordinal))
            {
                return new ScenarioValidationResult(CandidateValidationStatus.Invalid, new[] { AEDReasonCodes.ScenarioInvalid });
            }

            if (!candidate.HasChange)
            {
                return ValidateConfig(candidate.Config, registry, requirePolicyActiveChange: false);
            }

            if (candidate.Config.ConfigSource != ScenarioConfigSource.Adaptive)
            {
                return new ScenarioValidationResult(CandidateValidationStatus.Invalid, new[] { AEDReasonCodes.ScenarioInvalid });
            }

            if (!AEDPolicyDefinition.IsPolicyActiveKey(candidate.Change.Key))
            {
                return new ScenarioValidationResult(CandidateValidationStatus.Invalid, new[] { AEDReasonCodes.PolicyKeyNotActive });
            }

            if (!string.Equals(
                    candidate.Config.PolicyVersion,
                    AEDPolicyDefinition.PolicySemanticId,
                    StringComparison.Ordinal))
            {
                return new ScenarioValidationResult(CandidateValidationStatus.Invalid, new[] { AEDReasonCodes.UnsupportedVersion });
            }

            var canonicalRule =
                AEDPolicyDefinition.GetRule(candidate.RuleId);

            if (canonicalRule == null)
            {
                return new ScenarioValidationResult(CandidateValidationStatus.Invalid, new[] { AEDReasonCodes.PolicyConfigInvalid });
            }

            if (canonicalRule.DecisionPoint != decisionPoint)
            {
                return new ScenarioValidationResult(CandidateValidationStatus.Invalid, new[] { AEDReasonCodes.TimingRejected });
            }

            if ((canonicalRule.Intent == AdaptationIntent.Relieve
                    && candidate.Change.Key != ScenarioConfigKey.SupportItemBudget)
                || (canonicalRule.Intent == AdaptationIntent.IncreasePressure
                    && candidate.Change.Key != ScenarioConfigKey.ChaseSpeed)
                || canonicalRule.Intent == AdaptationIntent.Hold)
            {
                return new ScenarioValidationResult(CandidateValidationStatus.Invalid, new[] { AEDReasonCodes.PolicyConfigInvalid });
            }

            if (registry == null || !registry.TryGetRule(candidate.Change.Key, out var rule))
            {
                return new ScenarioValidationResult(CandidateValidationStatus.NotEvaluated, new[] { AEDReasonCodes.ParameterRegistryInvalid });
            }

            if (!rule.Allows(decisionPoint))
            {
                return new ScenarioValidationResult(CandidateValidationStatus.Invalid, new[] { AEDReasonCodes.TimingRejected });
            }

            if (!string.Equals(
                    rule.AdjustmentRuleId,
                    AEDPolicyDefinition.NextHigherRegisteredValueStrategyId,
                    StringComparison.Ordinal))
            {
                return new ScenarioValidationResult(CandidateValidationStatus.Invalid, new[] { AEDReasonCodes.RegisteredValueRejected });
            }

            if (candidate.Change.PressureAxis != rule.PressureAxis)
            {
                return new ScenarioValidationResult(CandidateValidationStatus.Invalid, new[] { AEDReasonCodes.PressureRuleRejected });
            }

            var actualBaseValue =
                AdaptiveCandidateBuilder.GetValue(
                    baseConfig,
                    candidate.Change.Key);

            if (actualBaseValue != candidate.Change.BeforeValue)
            {
                return new ScenarioValidationResult(CandidateValidationStatus.Invalid, new[] { AEDReasonCodes.ScenarioInvalid });
            }

            if ((candidate.Change.Key != ScenarioConfigKey.SupportItemBudget
                    && candidate.Config.SupportItemBudget != baseConfig.SupportItemBudget)
                || (candidate.Change.Key != ScenarioConfigKey.DetectionFillRate
                    && candidate.Config.MonsterParameters.DetectionFillRate
                        != baseConfig.MonsterParameters.DetectionFillRate)
                || (candidate.Change.Key != ScenarioConfigKey.DetectionDecayRate
                    && candidate.Config.MonsterParameters.DetectionDecayRate
                        != baseConfig.MonsterParameters.DetectionDecayRate)
                || (candidate.Change.Key != ScenarioConfigKey.ChaseSpeed
                    && candidate.Config.MonsterParameters.ChaseSpeed
                        != baseConfig.MonsterParameters.ChaseSpeed)
                || (candidate.Change.Key != ScenarioConfigKey.SearchDuration
                    && candidate.Config.MonsterParameters.SearchDuration
                        != baseConfig.MonsterParameters.SearchDuration)
                || (candidate.Change.Key != ScenarioConfigKey.EscapeDoorTimer
                    && candidate.Config.FinalHuntParameters.EscapeDoorTimerSeconds
                        != baseConfig.FinalHuntParameters.EscapeDoorTimerSeconds))
            {
                return new ScenarioValidationResult(CandidateValidationStatus.Invalid, new[] { AEDReasonCodes.ScenarioInvalid });
            }

            var actualCandidateValue =
                AdaptiveCandidateBuilder.GetValue(
                    candidate.Config,
                    candidate.Change.Key);

            if (actualCandidateValue != candidate.Change.AfterValue)
            {
                return new ScenarioValidationResult(CandidateValidationStatus.Invalid, new[] { AEDReasonCodes.ScenarioInvalid });
            }

            if (candidate.Change.AfterValue < rule.MinValue || candidate.Change.AfterValue > rule.MaxValue)
            {
                return new ScenarioValidationResult(CandidateValidationStatus.Invalid, new[] { AEDReasonCodes.BoundRejected });
            }

            var registered = false;
            for (var i = 0; i < rule.CandidateValues.Count; i++)
            {
                if (rule.CandidateValues[i] == candidate.Change.AfterValue)
                {
                    registered = true;
                    break;
                }
            }

            if (!registered)
            {
                return new ScenarioValidationResult(CandidateValidationStatus.Invalid, new[] { AEDReasonCodes.RegisteredValueRejected });
            }

            if (!rule.TryGetNextHigher(
                    candidate.Change.BeforeValue,
                    out var expectedNextValue)
                || expectedNextValue != candidate.Change.AfterValue)
            {
                return new ScenarioValidationResult(CandidateValidationStatus.Invalid, new[] { AEDReasonCodes.RegisteredValueRejected });
            }

            return ValidateConfig(candidate.Config, registry, requirePolicyActiveChange: true);
        }

        private static bool ViolatesPressureFairness(
            ScenarioConfig baseConfig,
            ScenarioConfig candidateConfig)
        {
            if (baseConfig == null
                || candidateConfig == null)
            {
                return false;
            }

            var detectionFillAggressive =
                candidateConfig.MonsterParameters.DetectionFillRate
                > baseConfig.MonsterParameters.DetectionFillRate;

            var detectionDecayAggressive =
                candidateConfig.MonsterParameters.DetectionDecayRate
                < baseConfig.MonsterParameters.DetectionDecayRate;

            // Explicit inherited prohibition:
            // do not strengthen both Detection directions together.
            if (detectionFillAggressive
                && detectionDecayAggressive)
            {
                return true;
            }

            var aggressiveAxisCount = 0;

            if (detectionFillAggressive
                || detectionDecayAggressive)
            {
                aggressiveAxisCount++;
            }

            if (candidateConfig.MonsterParameters.ChaseSpeed
                > baseConfig.MonsterParameters.ChaseSpeed)
            {
                aggressiveAxisCount++;
            }

            if (candidateConfig.MonsterParameters.SearchDuration
                > baseConfig.MonsterParameters.SearchDuration)
            {
                aggressiveAxisCount++;
            }

            return aggressiveAxisCount > 1;
        }

        private static ScenarioValidationResult ValidateConfig(
            ScenarioConfig config,
            AdaptiveParameterRegistry registry,
            bool requirePolicyActiveChange)
        {
            if (config == null)
            {
                return new ScenarioValidationResult(
                    CandidateValidationStatus.Invalid,
                    new[]
                    {
                        AEDReasonCodes.ScenarioInvalid
                    });
            }

            var reasons =
                new List<string>();

            if (config.FinalHuntParameters
                    .EscapeDoorTimerSeconds
                    < MinEscapeDoorTimerSeconds
                || config.FinalHuntParameters
                    .EscapeDoorTimerSeconds
                    > MaxEscapeDoorTimerSeconds)
            {
                reasons.Add(
                    AEDReasonCodes.BoundRejected);
            }

            if (config.SupportItemBudget < 0
                || config.MonsterParameters
                    .DetectionFillRate < 0d
                || config.MonsterParameters
                    .DetectionDecayRate < 0d
                || config.MonsterParameters
                    .ChaseSpeed < 0d
                || config.MonsterParameters
                    .SearchDuration < 0d)
            {
                reasons.Add(
                    AEDReasonCodes.ScenarioInvalid);
            }

            if (requirePolicyActiveChange && registry == null)
            {
                reasons.Add(
                    AEDReasonCodes.ParameterRegistryInvalid);
            }

            return new ScenarioValidationResult(
                reasons.Count == 0
                    ? CandidateValidationStatus.Valid
                    : CandidateValidationStatus.Invalid,
                reasons);
        }
    }
}
