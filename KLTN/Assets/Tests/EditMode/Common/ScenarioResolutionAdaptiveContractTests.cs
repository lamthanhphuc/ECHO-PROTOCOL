using EchoProtocol.AI.Common.AED;
using EchoProtocol.AI.Common.Profile;
using NUnit.Framework;

namespace EchoProtocol.AI.Common.Tests
{
    public sealed class ScenarioResolutionAdaptiveContractTests
    {
        [TestCase(
            20d,
            50d,
            "AED-PRE-010-SURVIVAL-LOW-RELIEVE")]
        [TestCase(
            50d,
            20d,
            "AED-PRE-020-NOISE-LOW-RELIEVE")]
        [TestCase(
            80d,
            80d,
            "AED-PRE-030-BOTH-HIGH-INCREASE")]
        [TestCase(
            50d,
            50d,
            "AED-PRE-040-MIXED-HOLD")]
        public void PreMatch_SelectsCanonicalRule(
            double survival,
            double noise,
            string expectedRule)
        {
            var request = AEDTestFactory.Request();

            var policy =
                AEDPolicyEvaluator.Evaluate(
                    request,
                    AEDTestFactory.Snapshot(
                        request,
                        survival,
                        noise),
                    AEDTestFactory.Policy());

            Assert.That(policy.Evaluated, Is.True);
            Assert.That(
                policy.RuleId,
                Is.EqualTo(expectedRule));
        }

        [Test]
        public void ScoreEqualLowThreshold_IsMid()
        {
            var thresholds =
                new ScoreBandThresholds(
                    30d,
                    70d);

            Assert.That(
                thresholds.Classify(30d),
                Is.EqualTo(ScoreBand.Mid));
        }

        [Test]
        public void ScoreEqualHighThreshold_IsHigh()
        {
            var thresholds =
                new ScoreBandThresholds(
                    30d,
                    70d);

            Assert.That(
                thresholds.Classify(70d),
                Is.EqualTo(ScoreBand.High));
        }

        [Test]
        public void SurvivalLow_AppliesNextSupportCandidate()
        {
            var engine =
                new ScenarioResolutionEngine();

            var request = AEDTestFactory.Request();

            var result =
                engine.Resolve(
                    AEDTestFactory.Input(
                        request,
                        AEDTestFactory.Snapshot(
                            request,
                            20d,
                            50d)));

            Assert.That(
                result.Decision.Result,
                Is.EqualTo(
                    AdaptiveDecisionResult.Applied));

            Assert.That(
                result.Decision.AppliedConfig.SupportItemBudget,
                Is.EqualTo(1));

            Assert.That(
                result.Decision.RequestedChanges.Count,
                Is.EqualTo(1));
        }

        [Test]
        public void BothHigh_AppliesNextChaseCandidate()
        {
            var engine =
                new ScenarioResolutionEngine();

            var request = AEDTestFactory.Request();

            var result =
                engine.Resolve(
                    AEDTestFactory.Input(
                        request,
                        AEDTestFactory.Snapshot(
                            request,
                            80d,
                            80d)));

            Assert.That(
                result.Decision.Result,
                Is.EqualTo(
                    AdaptiveDecisionResult.Applied));

            Assert.That(
                result.Decision.AppliedConfig
                    .MonsterParameters.ChaseSpeed,
                Is.EqualTo(10d));
        }

        [Test]
        public void MixedPreMatch_ReturnsNoChange()
        {
            var engine =
                new ScenarioResolutionEngine();

            var request = AEDTestFactory.Request();

            var result =
                engine.Resolve(
                    AEDTestFactory.Input(
                        request,
                        AEDTestFactory.Snapshot(
                            request,
                            50d,
                            50d)));

            Assert.That(
                result.Decision.Result,
                Is.EqualTo(
                    AdaptiveDecisionResult.NoChange));

            Assert.That(
                result.Decision.AppliedConfig,
                Is.Null);

            Assert.That(
                result.Decision.FallbackAction,
                Is.EqualTo(
                    ScenarioFallbackAction.None));
        }

        [Test]
        public void GateFailure_PreMatch_FallsBackToFixed()
        {
            var engine =
                new ScenarioResolutionEngine();

            var request = AEDTestFactory.Request();

            var snapshot =
                AEDTestFactory.Snapshot(
                    request,
                    50d,
                    50d,
                    SnapshotValidity.Partial);

            var result =
                engine.Resolve(
                    AEDTestFactory.Input(
                        request,
                        snapshot));

            Assert.That(
                result.Decision.Result,
                Is.EqualTo(
                    AdaptiveDecisionResult.FixedFallback));

            Assert.That(
                result.Decision.FallbackAction,
                Is.EqualTo(
                    ScenarioFallbackAction.FullFixedConfig));

            Assert.That(
                result.Decision.InputStatus,
                Is.EqualTo(
                    AEDInputGateStatus.Ineligible));

            Assert.That(
                result.Decision.ReasonCodes,
                Does.Contain(
                    AEDReasonCodes.InputIncomplete));
        }

        [Test]
        public void AnyActiveBaseValueOutsideRegistry_IsRejectedBeforePolicyCandidate()
        {
            var engine =
                new ScenarioResolutionEngine();

            var request = AEDTestFactory.Request();

            var baseline =
                FixedDirector.CreateFixedBaseline();

            var incompatibleBase =
                AEDTestFactory.AdaptiveConfig(
                    baseline,
                    "BASE_WITH_UNREGISTERED_SUPPORT",
                    supportBudget: 5);

            var result =
                engine.Resolve(
                    AEDTestFactory.Input(
                        request,
                        AEDTestFactory.Snapshot(
                            request,
                            80d,
                            80d),
                        currentConfig:
                            incompatibleBase));

            Assert.That(
                result.Decision.Result,
                Is.EqualTo(
                    AdaptiveDecisionResult.FixedFallback));

            Assert.That(
                result.Decision.ReasonCodes,
                Does.Contain(
                    AEDReasonCodes.ParameterRegistryInvalid));

            Assert.That(
                result.Decision.CandidateValidationStatus,
                Is.EqualTo(
                    CandidateValidationStatus.NotEvaluated));
        }

        [Test]
        public void SameEvidenceVersionWithDifferentOwnedContent_IsRejected()
        {
            var engine =
                new ScenarioResolutionEngine();

            var request1 = AEDTestFactory.Request();

            engine.Resolve(
                AEDTestFactory.Input(
                    request1,
                    AEDTestFactory.Snapshot(
                        request1,
                        50d,
                        50d),
                    evidence:
                        AEDTestFactory.Evidence(
                            AEDTestFactory.EvidenceVersion,
                            1)));

            var request2 = AEDTestFactory.Request();

            var second =
                engine.Resolve(
                    AEDTestFactory.Input(
                        request2,
                        AEDTestFactory.Snapshot(
                            request2,
                            50d,
                            50d,
                            sampleCount: 5),
                    evidence:
                        AEDTestFactory.Evidence(
                            AEDTestFactory.EvidenceVersion,
                            2)));

            Assert.That(
                second.Decision.Result,
                Is.EqualTo(
                    AdaptiveDecisionResult.FixedFallback));

            Assert.That(
                second.Decision.ReasonCodes,
                Does.Contain(
                    AEDReasonCodes.PolicyConfigInvalid));
        }

        [Test]
        public void NewEvidenceVersionWithChangedThreshold_IsLegal()
        {
            var engine =
                new ScenarioResolutionEngine();

            var request1 = AEDTestFactory.Request();

            engine.Resolve(
                AEDTestFactory.Input(
                    request1,
                    AEDTestFactory.Snapshot(
                        request1,
                        50d,
                        50d),
                    evidence:
                        AEDTestFactory.Evidence(
                            "EVIDENCE_REV_A",
                            1),
                    policy:
                        AEDTestFactory.Policy(
                            evidenceVersion:
                                "EVIDENCE_REV_A")));

            var request2 = AEDTestFactory.Request();

            var second =
                engine.Resolve(
                    AEDTestFactory.Input(
                        request2,
                        AEDTestFactory.Snapshot(
                            request2,
                            50d,
                            50d,
                            sampleCount: 5),
                    evidence:
                        AEDTestFactory.Evidence(
                            "EVIDENCE_REV_B",
                            2),
                    policy:
                        AEDTestFactory.Policy(
                            evidenceVersion:
                                "EVIDENCE_REV_B")));

            Assert.That(
                second.Decision.ReasonCodes,
                Does.Not.Contain(
                    AEDReasonCodes.PolicyConfigInvalid));

            Assert.That(
                second.Decision.Result,
                Is.EqualTo(
                    AdaptiveDecisionResult.NoChange));
        }
    }
}
