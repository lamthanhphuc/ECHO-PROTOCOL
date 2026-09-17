using EchoProtocol.AI.Common.AED;
using NUnit.Framework;

namespace EchoProtocol.AI.Common.Tests
{
    public sealed class ScenarioValidatorContractTests
    {
        [Test]
        public void RegisteredButSkippedNextValue_IsRejected()
        {
            var baseConfig =
                FixedDirector.CreateFixedBaseline();

            var candidateConfig =
                AEDTestFactory.AdaptiveConfig(
                    baseConfig,
                    "CANDIDATE_SKIP",
                    chaseSpeed: 11d);

            var candidate =
                new CandidateScenarioConfig(
                    candidateConfig,
                    FixedDirector.CreatePreMatchBaseRef(
                        baseConfig),
                    new ScenarioConfigChange(
                        ScenarioConfigKey.ChaseSpeed,
                        9d,
                        11d,
                        PressureAxis.ChasePressure),
                    "AED-PRE-030-BOTH-HIGH-INCREASE");

            var validation =
                ScenarioValidator.ValidateCandidate(
                    candidate,
                    AEDTestFactory.Registry(),
                    ScenarioDecisionPoint.PreMatch,
                    baseConfig);

            Assert.That(validation.IsValid, Is.False);

            Assert.That(
                validation.ReasonCodes,
                Does.Contain(
                    AEDReasonCodes.RegisteredValueRejected));
        }

        [Test]
        public void UnregisteredValue_IsRejected()
        {
            var baseConfig =
                FixedDirector.CreateFixedBaseline();

            var candidate =
                new CandidateScenarioConfig(
                    AEDTestFactory.AdaptiveConfig(
                        baseConfig,
                        "CANDIDATE_10_5",
                        chaseSpeed: 10.5d),
                    FixedDirector.CreatePreMatchBaseRef(
                        baseConfig),
                    new ScenarioConfigChange(
                        ScenarioConfigKey.ChaseSpeed,
                        9d,
                        10.5d,
                        PressureAxis.ChasePressure),
                    "AED-PRE-030-BOTH-HIGH-INCREASE");

            var validation =
                ScenarioValidator.ValidateCandidate(
                    candidate,
                    AEDTestFactory.Registry(),
                    ScenarioDecisionPoint.PreMatch,
                    baseConfig);

            Assert.That(
                validation.ReasonCodes,
                Does.Contain(
                    AEDReasonCodes.RegisteredValueRejected));
        }

        [Test]
        public void DetectionFillAndDecayAggressiveTogether_IsPressureRejected()
        {
            var baseConfig =
                FixedDirector.CreateFixedBaseline();

            var config =
                AEDTestFactory.AdaptiveConfig(
                    baseConfig,
                    "DOUBLE_DETECTION",
                    detectionFill:
                        baseConfig.MonsterParameters
                            .DetectionFillRate + 1d,
                    detectionDecay:
                        baseConfig.MonsterParameters
                            .DetectionDecayRate - 1d);

            var candidate =
                new CandidateScenarioConfig(
                    config,
                    FixedDirector.CreatePreMatchBaseRef(
                        baseConfig),
                    new ScenarioConfigChange(
                        ScenarioConfigKey.DetectionFillRate,
                        baseConfig.MonsterParameters
                            .DetectionFillRate,
                        config.MonsterParameters
                            .DetectionFillRate,
                        PressureAxis.DetectionPressure),
                    "AED-PRE-030-BOTH-HIGH-INCREASE");

            var validation =
                ScenarioValidator.ValidateCandidate(
                    candidate,
                    AEDTestFactory.Registry(),
                    ScenarioDecisionPoint.PreMatch,
                    baseConfig);

            Assert.That(
                validation.ReasonCodes,
                Does.Contain(
                    AEDReasonCodes.PressureRuleRejected));
        }

        [Test]
        public void ChaseAndSearchAggressiveTogether_IsPressureRejected()
        {
            var baseConfig =
                FixedDirector.CreateFixedBaseline();

            var config =
                AEDTestFactory.AdaptiveConfig(
                    baseConfig,
                    "DOUBLE_PRESSURE",
                    chaseSpeed: 10d,
                    searchDuration: 6d);

            var candidate =
                new CandidateScenarioConfig(
                    config,
                    FixedDirector.CreatePreMatchBaseRef(
                        baseConfig),
                    new ScenarioConfigChange(
                        ScenarioConfigKey.ChaseSpeed,
                        9d,
                        10d,
                        PressureAxis.ChasePressure),
                    "AED-PRE-030-BOTH-HIGH-INCREASE");

            var validation =
                ScenarioValidator.ValidateCandidate(
                    candidate,
                    AEDTestFactory.Registry(),
                    ScenarioDecisionPoint.PreMatch,
                    baseConfig);

            Assert.That(
                validation.ReasonCodes,
                Does.Contain(
                    AEDReasonCodes.PressureRuleRejected));
        }

        [Test]
        public void SingleInactiveDetectionChange_IsPolicyKeyNotActive()
        {
            var baseConfig =
                FixedDirector.CreateFixedBaseline();

            var config =
                AEDTestFactory.AdaptiveConfig(
                    baseConfig,
                    "INACTIVE_DETECTION",
                    detectionFill:
                        baseConfig.MonsterParameters
                            .DetectionFillRate + 1d);

            var candidate =
                new CandidateScenarioConfig(
                    config,
                    FixedDirector.CreatePreMatchBaseRef(
                        baseConfig),
                    new ScenarioConfigChange(
                        ScenarioConfigKey.DetectionFillRate,
                        baseConfig.MonsterParameters
                            .DetectionFillRate,
                        config.MonsterParameters
                            .DetectionFillRate,
                        PressureAxis.DetectionPressure),
                    "AED-PRE-030-BOTH-HIGH-INCREASE");

            var validation =
                ScenarioValidator.ValidateCandidate(
                    candidate,
                    AEDTestFactory.Registry(),
                    ScenarioDecisionPoint.PreMatch,
                    baseConfig);

            Assert.That(
                validation.ReasonCodes,
                Does.Contain(
                    AEDReasonCodes.PolicyKeyNotActive));
        }

        [TestCase(45d, true)]
        [TestCase(60d, true)]
        [TestCase(44.999d, false)]
        [TestCase(60.001d, false)]
        public void FinalHuntTimer_UsesCanonicalBounds(
            double seconds,
            bool expectedValid)
        {
            var baseline =
                FixedDirector.CreateFixedBaseline();

            var config =
                new ScenarioConfig(
                    "TIMER_TEST",
                    baseline.PolicyVersion,
                    ScenarioConfigSource.Fixed,
                    baseline.MapId,
                    baseline.MonsterType,
                    baseline.ObjectiveSpawnSetId,
                    baseline.SupportItemBudget,
                    baseline.MonsterParameters,
                    baseline.RouteModifier,
                    new ScenarioFinalHuntParameters(
                        seconds),
                    baseline.FallbackConfigId);

            var result =
                ScenarioValidator.ValidateFixedBaseline(
                    config);

            Assert.That(
                result.IsValid,
                Is.EqualTo(expectedValid));

            if (!expectedValid)
            {
                Assert.That(
                    result.ReasonCodes,
                    Does.Contain(
                        AEDReasonCodes.BoundRejected));
            }
        }

        [Test]
        public void WrongBaseFingerprint_IsStaleBase()
        {
            var baseConfig =
                FixedDirector.CreateFixedBaseline();

            var wrongBase =
                AEDTestFactory.AdaptiveConfig(
                    baseConfig,
                    "DIFFERENT_BASE",
                    chaseSpeed: 10d);

            var candidateConfig =
                AEDTestFactory.AdaptiveConfig(
                    baseConfig,
                    "CANDIDATE",
                    chaseSpeed: 10d);

            var candidate =
                new CandidateScenarioConfig(
                    candidateConfig,
                    FixedDirector.CreatePreMatchBaseRef(
                        wrongBase),
                    new ScenarioConfigChange(
                        ScenarioConfigKey.ChaseSpeed,
                        9d,
                        10d,
                        PressureAxis.ChasePressure),
                    "AED-PRE-030-BOTH-HIGH-INCREASE");

            var result =
                ScenarioValidator.ValidateCandidate(
                    candidate,
                    AEDTestFactory.Registry(),
                    ScenarioDecisionPoint.PreMatch,
                    baseConfig);

            Assert.That(
                result.ReasonCodes,
                Does.Contain(
                    AEDReasonCodes.StaleBaseConfig));
        }
    }
}
