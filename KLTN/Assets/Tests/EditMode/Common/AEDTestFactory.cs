using System;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.AI.Common.Profile;

namespace EchoProtocol.AI.Common.Tests
{
    internal static class AEDTestFactory
    {
        public const string PolicyConfigVersion = "AED_TEST_POLICY_CONFIG_V1";
        public const string EvidenceVersion = "AED_TEST_EVIDENCE_V1";
        public const string RegistryVersion = "AED_TEST_REGISTRY_V1";

        public const string SurvivalKey = "SURVIVAL_SCORE_V1";
        public const string NoiseKey = "NOISE_SCORE_V1";

        public static AEDPolicyConfig Policy(
            string policyConfigVersion = PolicyConfigVersion,
            string evidenceVersion = EvidenceVersion,
            string registryVersion = RegistryVersion,
            double low = 30d,
            double high = 70d)
        {
            return new AEDPolicyConfig(
                AEDPolicyDefinition.PolicySemanticId,
                policyConfigVersion,
                evidenceVersion,
                registryVersion,
                FixedDirector.FixedBaselineContentWhitelistVersion,
                new ScoreBandThresholds(low, high),
                new ScoreBandThresholds(low, high));
        }

        public static AEDEvidencePolicy Evidence(
            string version = EvidenceVersion,
            int minimumSamples = 1)
        {
            return new AEDEvidencePolicy(
                version,
                minimumSamples,
                minimumSamples,
                true);
        }

        public static AdaptiveParameterRegistry Registry(
            string version = RegistryVersion,
            double[] supportCandidates = null,
            double[] chaseCandidates = null)
        {
            supportCandidates ??=
                new[] { 0d, 1d, 2d };

            chaseCandidates ??=
                new[] { 9d, 10d, 11d };

            return new AdaptiveParameterRegistry(
                version,
                new[]
                {
                    new AdaptiveParameterRule(
                        ScenarioConfigKey.SupportItemBudget,
                        0d,
                        0d,
                        2d,
                        supportCandidates,
                        new[]
                        {
                            ScenarioDecisionPoint.PreMatch
                        },
                        PressureAxis.None,
                        AEDPolicyDefinition
                            .NextHigherRegisteredValueStrategyId),

                    new AdaptiveParameterRule(
                        ScenarioConfigKey.ChaseSpeed,
                        9d,
                        9d,
                        11d,
                        chaseCandidates,
                        new[]
                        {
                            ScenarioDecisionPoint.PreMatch
                        },
                        PressureAxis.ChasePressure,
                        AEDPolicyDefinition
                            .NextHigherRegisteredValueStrategyId)
                });
        }

        public static ScenarioResolutionRequest Request(
            ScenarioDecisionPoint point =
                ScenarioDecisionPoint.PreMatch,
            ScenarioResolutionMode mode =
                ScenarioResolutionMode.Adaptive,
            Guid? decisionId = null,
            Guid? matchId = null,
            string phaseContext = null)
        {
            return new ScenarioResolutionRequest(
                decisionId ?? Guid.NewGuid(),
                matchId ?? Guid.NewGuid(),
                mode,
                point,
                phaseContext ?? Phase(point),
                string.Empty);
        }

        public static AdaptiveInputCurrencyValidation CurrentCurrency()
        {
            return new AdaptiveInputCurrencyValidation(
                true,
                true,
                true,
                true,
                true,
                true,
                true);
        }

        public static AdaptiveInputSnapshot Snapshot(
            ScenarioResolutionRequest request,
            double survival,
            double noise,
            SnapshotValidity validity =
                SnapshotValidity.Valid,
            int sampleCount = 5,
            string survivalKey = SurvivalKey,
            string noiseKey = NoiseKey,
            string summarySurvivalKey = SurvivalKey,
            string summaryNoiseKey = NoiseKey)
        {
            var revision =
                new ProfileRevisionRef(
                    "player-1",
                    1);

            var survivalDimension =
                new PlayerDimensionSnapshot(
                    survival,
                    PlayerDimensionStatus.Active,
                    sampleCount,
                    survivalKey,
                    1);

            var noiseDimension =
                new PlayerDimensionSnapshot(
                    noise,
                    PlayerDimensionStatus.Active,
                    sampleCount,
                    noiseKey,
                    1);

            var player =
                new PlayerProfileSnapshot(
                    "player-1",
                    1,
                    "lineage-1",
                    survivalDimension,
                    noiseDimension);

            var summary =
                new RosterProfileSummary(
                    "roster-1",
                    1,
                    new RosterDimensionSummary(
                        1,
                        0,
                        0,
                        0,
                        1d,
                        RosterAggregationStatus.Available,
                        summarySurvivalKey,
                        survival),
                    new RosterDimensionSummary(
                        1,
                        0,
                        0,
                        0,
                        1d,
                        RosterAggregationStatus.Available,
                        summaryNoiseKey,
                        noise));

            var provenance =
                new AdaptiveInputProvenance(
                    "PROFILE_FORMULA_TEST_V1",
                    SurvivalKey,
                    NoiseKey,
                    "TEAM_FORMULA_TEST_V1",
                    new[] { revision },
                    "TELEMETRY_TEST_V1");

            return new AdaptiveInputSnapshot(
                Guid.NewGuid(),
                "SNAPSHOT_TEST_FINGERPRINT",
                request.TargetMatchId,
                request.DecisionPoint,
                request.PhaseContext,
                DateTime.UtcNow,
                "roster-1",
                1,
                new[] { player },
                summary,
                validity,
                Array.Empty<string>(),
                provenance);
        }

        public static ScenarioResolutionEngineInput Input(
            ScenarioResolutionRequest request,
            AdaptiveInputSnapshot snapshot,
            AEDPolicyConfig policy = null,
            AEDEvidencePolicy evidence = null,
            AdaptiveParameterRegistry registry = null,
            ScenarioConfig currentConfig = null,
            AdaptiveInputCurrencyValidation currency = null)
        {
            return new ScenarioResolutionEngineInput(
                request,
                currentConfig,
                snapshot,
                currency ?? CurrentCurrency(),
                policy ?? Policy(),
                evidence ?? Evidence(),
                registry ?? Registry());
        }

        public static ScenarioConfig AdaptiveConfig(
            ScenarioConfig baseConfig,
            string version,
            int? supportBudget = null,
            double? detectionFill = null,
            double? detectionDecay = null,
            double? chaseSpeed = null,
            double? searchDuration = null,
            double? escapeTimer = null)
        {
            return new ScenarioConfig(
                version,
                baseConfig.PolicyVersion,
                ScenarioConfigSource.Adaptive,
                baseConfig.MapId,
                baseConfig.MonsterType,
                baseConfig.ObjectiveSpawnSetId,
                supportBudget ?? baseConfig.SupportItemBudget,
                new ScenarioMonsterParameters(
                    detectionFill
                        ?? baseConfig.MonsterParameters.DetectionFillRate,
                    detectionDecay
                        ?? baseConfig.MonsterParameters.DetectionDecayRate,
                    chaseSpeed
                        ?? baseConfig.MonsterParameters.ChaseSpeed,
                    searchDuration
                        ?? baseConfig.MonsterParameters.SearchDuration),
                baseConfig.RouteModifier,
                new ScenarioFinalHuntParameters(
                    escapeTimer
                        ?? baseConfig.FinalHuntParameters
                            .EscapeDoorTimerSeconds),
                baseConfig.FallbackConfigId);
        }

        private static string Phase(
            ScenarioDecisionPoint point)
        {
            switch (point)
            {
                case ScenarioDecisionPoint.PreMatch:
                    return "PRE_MATCH";

                case ScenarioDecisionPoint.AllowedPhaseBoundary:
                    return "PUZZLE";

                case ScenarioDecisionPoint.FinalHuntSetup:
                    return "FINAL_HUNT";

                default:
                    return string.Empty;
            }
        }
    }
}
