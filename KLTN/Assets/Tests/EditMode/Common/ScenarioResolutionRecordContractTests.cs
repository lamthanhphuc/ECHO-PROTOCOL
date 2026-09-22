using System;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;

namespace EchoProtocol.AI.Common.Tests
{
    public sealed class ScenarioResolutionRecordContractTests
    {
        [Test]
        public void PrecommitStaleBase_FinalRecordReportsFallback()
        {
            var engine =
                new ScenarioResolutionEngine();

            var request =
                AEDTestFactory.Request(
                    mode:
                        ScenarioResolutionMode.Fixed);

            var resolved =
                engine.Resolve(
                    new ScenarioResolutionEngineInput(
                        request,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null));

            var rejected =
                resolved.WithPrecommitRejection(
                    AEDReasonCodes.StaleBaseConfig);

            var record =
                ScenarioResolutionRecord.Create(
                    rejected,
                    FixedDirector.CreateFixedBaseline(),
                    DateTime.UtcNow,
                    true);

            Assert.That(
                record.CommitDisposition,
                Is.EqualTo(
                    ScenarioResolutionCommitDisposition
                        .PrecommitRejected));

            Assert.That(
                record.Result,
                Is.EqualTo(
                    AdaptiveDecisionResult.FixedFallback));

            Assert.That(
                record.ReasonCode,
                Is.EqualTo(
                    AEDReasonCodes.StaleBaseConfig));

            Assert.That(
                record.StaleBaseConfigDetected,
                Is.True);
        }

        [Test]
        public void DecisionIdentityConflict_PreservesAttemptEvidence()
        {
            var engine =
                new ScenarioResolutionEngine();

            var decisionId = Guid.NewGuid();
            var matchId = Guid.NewGuid();

            var firstRequest =
                AEDTestFactory.Request(
                    mode:
                        ScenarioResolutionMode.Fixed,
                    decisionId:
                        decisionId,
                    matchId:
                        matchId,
                    phaseContext:
                        "PRE_MATCH");

            engine.Resolve(
                new ScenarioResolutionEngineInput(
                    firstRequest,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null));

            var conflictingRequest =
                AEDTestFactory.Request(
                    mode:
                        ScenarioResolutionMode.Fixed,
                    decisionId:
                        decisionId,
                    matchId:
                        matchId,
                    phaseContext:
                        "DIFFERENT_CONTEXT");

            var conflict =
                engine.Resolve(
                    new ScenarioResolutionEngineInput(
                        conflictingRequest,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null));

            Assert.That(
                conflict.HasDecisionIdentityConflict,
                Is.True);

            Assert.That(
                conflict.AttemptedDecision,
                Is.Not.Null);

            Assert.That(
                conflict.Decision,
                Is.Not.Null);

            var record =
                ScenarioResolutionRecord.Create(
                    conflict,
                    null,
                    DateTime.UtcNow,
                    true);

            Assert.That(record.Result, Is.Null);

            Assert.That(
                record.ReasonCode,
                Is.EqualTo(
                    AEDReasonCodes
                        .DecisionIdentityConflict));
        }

        [Test]
        public void FixedRecord_UsesCanonicalContentWhitelistBinding()
        {
            var engine =
                new ScenarioResolutionEngine();

            var request =
                AEDTestFactory.Request(
                    mode:
                        ScenarioResolutionMode.Fixed);

            var result =
                engine.Resolve(
                    new ScenarioResolutionEngineInput(
                        request,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null));

            var record =
                ScenarioResolutionRecord.Create(
                    result,
                    result.Decision.AppliedConfig,
                    DateTime.UtcNow,
                    true);

            Assert.That(
                record.ContentWhitelistVersion,
                Is.EqualTo(
                    FixedDirector
                        .FixedBaselineContentWhitelistVersion));
        }
    }
}
