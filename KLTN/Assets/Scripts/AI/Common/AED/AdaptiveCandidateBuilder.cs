using System;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class ScenarioConfigChange
    {
        public ScenarioConfigChange(ScenarioConfigKey key, double beforeValue, double afterValue, PressureAxis pressureAxis)
        {
            ScenarioMonsterParameters.RequireFinite(beforeValue, nameof(beforeValue));
            ScenarioMonsterParameters.RequireFinite(afterValue, nameof(afterValue));
            Key = key;
            BeforeValue = beforeValue;
            AfterValue = afterValue;
            PressureAxis = pressureAxis;
        }

        public ScenarioConfigKey Key { get; }
        public double BeforeValue { get; }
        public double AfterValue { get; }
        public PressureAxis PressureAxis { get; }
    }

    public sealed class CandidateScenarioConfig
    {
        public CandidateScenarioConfig(
            ScenarioConfig config,
            ScenarioConfigBaseRef baseRef,
            ScenarioConfigChange change,
            string ruleId)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            BaseRef = baseRef ?? throw new ArgumentNullException(nameof(baseRef));
            Change = change;
            RuleId = ruleId ?? string.Empty;
        }

        public ScenarioConfig Config { get; }
        public ScenarioConfigBaseRef BaseRef { get; }
        public ScenarioConfigChange Change { get; }
        public string RuleId { get; }
        public bool HasChange => Change != null;
    }

    public sealed class AdaptiveCandidateBuildResult
    {
        public AdaptiveCandidateBuildResult(
            bool built,
            CandidateScenarioConfig candidate,
            PolicyNoChangeReason noChangeReason,
            string reasonCode)
        {
            Built = built;
            Candidate = candidate;
            NoChangeReason = noChangeReason;
            ReasonCode = reasonCode ?? string.Empty;
        }

        public bool Built { get; }
        public CandidateScenarioConfig Candidate { get; }
        public PolicyNoChangeReason NoChangeReason { get; }
        public string ReasonCode { get; }
    }

    public static class AdaptiveCandidateBuilder
    {
        public static AdaptiveCandidateBuildResult Build(
            ScenarioResolutionRequest request,
            ScenarioConfig currentConfig,
            ScenarioConfigBaseRef baseRef,
            AEDPolicyEvaluationResult policy,
            AdaptiveParameterRegistry registry)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (currentConfig == null) throw new ArgumentNullException(nameof(currentConfig));
            if (baseRef == null) throw new ArgumentNullException(nameof(baseRef));
            if (policy == null) throw new ArgumentNullException(nameof(policy));

            if (!policy.Evaluated)
            {
                return new AdaptiveCandidateBuildResult(false, null, PolicyNoChangeReason.None, policy.ReasonCode);
            }

            if (policy.Intent == AdaptationIntent.Hold || !policy.RequestedKey.HasValue)
            {
                return new AdaptiveCandidateBuildResult(false, null, PolicyNoChangeReason.HoldRule, "NO_CHANGE_HOLD_RULE");
            }

            if (registry == null || !registry.HasCurrentPolicyActiveRules())
            {
                return new AdaptiveCandidateBuildResult(false, null, PolicyNoChangeReason.None, "PARAMETER_REGISTRY_INVALID");
            }

            var key = policy.RequestedKey.Value;
            if (!AEDPolicyDefinition.IsPolicyActiveKey(key) || !registry.TryGetRule(key, out var rule))
            {
                return new AdaptiveCandidateBuildResult(false, null, PolicyNoChangeReason.KeyNotApplicable, "KEY_NOT_APPLICABLE");
            }

            if (!rule.Allows(request.DecisionPoint))
            {
                return new AdaptiveCandidateBuildResult(false, null, PolicyNoChangeReason.KeyNotApplicable, "KEY_NOT_APPLICABLE");
            }

            if (policy.Intent == AdaptationIntent.IncreasePressure
                && !string.Equals(currentConfig.MonsterType, "STALKER", StringComparison.OrdinalIgnoreCase))
            {
                return new AdaptiveCandidateBuildResult(false, null, PolicyNoChangeReason.KeyNotApplicable, "KEY_NOT_APPLICABLE");
            }

            var currentValue = GetValue(currentConfig, key);
            if (!rule.TryGetNextHigher(currentValue, out var nextValue))
            {
                return new AdaptiveCandidateBuildResult(false, null, PolicyNoChangeReason.ValueLimitReached, "VALUE_LIMIT_REACHED");
            }

            var candidateConfig = WithValue(
                currentConfig,
                key,
                nextValue,
                ScenarioConfigFingerprint.CreateVersion(
                    request.TargetMatchId,
                    request.ResolutionId,
                    currentConfig,
                    key,
                    nextValue),
                ScenarioConfigSource.Adaptive);

            return new AdaptiveCandidateBuildResult(
                true,
                new CandidateScenarioConfig(
                    candidateConfig,
                    baseRef,
                    new ScenarioConfigChange(key, currentValue, nextValue, rule.PressureAxis),
                    policy.RuleId),
                PolicyNoChangeReason.None,
                string.Empty);
        }

        public static double GetValue(ScenarioConfig config, ScenarioConfigKey key)
        {
            switch (key)
            {
                case ScenarioConfigKey.SupportItemBudget: return config.SupportItemBudget;
                case ScenarioConfigKey.DetectionFillRate: return config.MonsterParameters.DetectionFillRate;
                case ScenarioConfigKey.DetectionDecayRate: return config.MonsterParameters.DetectionDecayRate;
                case ScenarioConfigKey.ChaseSpeed: return config.MonsterParameters.ChaseSpeed;
                case ScenarioConfigKey.SearchDuration: return config.MonsterParameters.SearchDuration;
                case ScenarioConfigKey.EscapeDoorTimer: return config.FinalHuntParameters.EscapeDoorTimerSeconds;
                default: throw new ArgumentException("Scenario key is not numeric in v1.1.", nameof(key));
            }
        }

        private static ScenarioConfig WithValue(
            ScenarioConfig config,
            ScenarioConfigKey key,
            double value,
            string version,
            ScenarioConfigSource source)
        {
            var supportItemBudget = config.SupportItemBudget;
            var detectionFillRate = config.MonsterParameters.DetectionFillRate;
            var detectionDecayRate = config.MonsterParameters.DetectionDecayRate;
            var chaseSpeed = config.MonsterParameters.ChaseSpeed;
            var searchDuration = config.MonsterParameters.SearchDuration;
            var escapeTimer = config.FinalHuntParameters.EscapeDoorTimerSeconds;

            switch (key)
            {
                case ScenarioConfigKey.SupportItemBudget: supportItemBudget = checked((int)Math.Round(value)); break;
                case ScenarioConfigKey.DetectionFillRate: detectionFillRate = value; break;
                case ScenarioConfigKey.DetectionDecayRate: detectionDecayRate = value; break;
                case ScenarioConfigKey.ChaseSpeed: chaseSpeed = value; break;
                case ScenarioConfigKey.SearchDuration: searchDuration = value; break;
                case ScenarioConfigKey.EscapeDoorTimer: escapeTimer = value; break;
                default: throw new ArgumentException("Scenario key is not numeric in v1.1.", nameof(key));
            }

            return new ScenarioConfig(
                version,
                config.PolicyVersion,
                source,
                config.MapId,
                config.MonsterType,
                config.ObjectiveSpawnSetId,
                supportItemBudget,
                new ScenarioMonsterParameters(detectionFillRate, detectionDecayRate, chaseSpeed, searchDuration),
                config.RouteModifier,
                new ScenarioFinalHuntParameters(escapeTimer),
                config.FallbackConfigId);
        }
    }
}
