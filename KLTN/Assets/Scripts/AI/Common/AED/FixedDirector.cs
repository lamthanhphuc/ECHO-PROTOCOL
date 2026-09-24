using System;

namespace EchoProtocol.AI.Common.AED
{
    public static class FixedDirector
    {
        public const string FixedBaselineId = "FIXED_BASELINE_V1";
        public const string FixedBaselineScenarioVersion = "FIXED_BASELINE_V1";
        public const string FixedBaselineMapId = "M2-MAP-1";
        public const string FixedBaselineMonsterType = "STALKER";
        public const string FixedBaselineObjectiveSpawnSetId = "DEFAULT_OBJECTIVES";
        public const string FixedBaselineRouteModifier = "DEFAULT_ROUTE";
        public const string FixedBaselineFallbackConfigId = "FIXED_BASELINE_V1";
        public const int FixedBaselineSupportItemBudget = 0;
        public const double FixedBaselineDetectionFillRate = 0.5d;
        public const double FixedBaselineDetectionDecayRate = 3.3333333d;
        public const double FixedBaselineChaseSpeed = 9d;
        public const double FixedBaselineSearchDuration = 5d;
        public const double FixedBaselineEscapeDoorTimerSeconds = 45d;
        public const string FixedBaselineContentWhitelistVersion =
    "M2-WHITELIST-1";

        public static ScenarioConfig CreateFixedBaseline()
        {
            return new ScenarioConfig(
                FixedBaselineScenarioVersion,
                AEDPolicyDefinition.PolicySemanticId,
                ScenarioConfigSource.Fixed,
                FixedBaselineMapId,
                FixedBaselineMonsterType,
                FixedBaselineObjectiveSpawnSetId,
                FixedBaselineSupportItemBudget,
                new ScenarioMonsterParameters(
                    FixedBaselineDetectionFillRate,
                    FixedBaselineDetectionDecayRate,
                    FixedBaselineChaseSpeed,
                    FixedBaselineSearchDuration),
                FixedBaselineRouteModifier,
                new ScenarioFinalHuntParameters(FixedBaselineEscapeDoorTimerSeconds),
                FixedBaselineFallbackConfigId);
        }

        public static ScenarioFallbackAction FallbackFor(ScenarioDecisionPoint decisionPoint)
        {
            return decisionPoint == ScenarioDecisionPoint.PreMatch
                ? ScenarioFallbackAction.FullFixedConfig
                : ScenarioFallbackAction.KeepLastValidConfig;
        }

        public static ScenarioConfigBaseRef CreatePreMatchBaseRef(ScenarioConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            return new ScenarioConfigBaseRef(
                FixedBaselineId,
                config.ScenarioConfigVersion,
                ScenarioConfigFingerprint.Compute(config),
                ScenarioConfigBaseKind.PreMatchResolvedBase);
        }

        public static ScenarioConfigBaseRef CreateAppliedBaseRef(ScenarioConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            return new ScenarioConfigBaseRef(
                config.ScenarioConfigVersion,
                config.ScenarioConfigVersion,
                ScenarioConfigFingerprint.Compute(config),
                ScenarioConfigBaseKind.AppliedScenarioConfig);
        }

        public static bool MatchesBaseRef(
            ScenarioConfigBaseRef baseRef,
            ScenarioConfig currentConfig,
            ScenarioDecisionPoint decisionPoint)
        {
            if (baseRef == null || currentConfig == null)
            {
                return false;
            }

            var currentBaseRef =
                decisionPoint == ScenarioDecisionPoint.PreMatch
                    ? CreatePreMatchBaseRef(currentConfig)
                    : CreateAppliedBaseRef(currentConfig);

            return string.Equals(
                       baseRef.BaseConfigId,
                       currentBaseRef.BaseConfigId,
                       StringComparison.Ordinal)
                   && string.Equals(
                       baseRef.BaseScenarioConfigVersion,
                       currentBaseRef.BaseScenarioConfigVersion,
                       StringComparison.Ordinal)
                   && string.Equals(
                       baseRef.BaseContentFingerprint,
                       currentBaseRef.BaseContentFingerprint,
                       StringComparison.Ordinal)
                   && baseRef.BaseKind == currentBaseRef.BaseKind;
        }
    }
}
