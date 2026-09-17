using System;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;

namespace EchoProtocol.AI.Common.Tests
{
    public sealed class ScenarioResolutionEngineIdentityTests
    {
        [Test]
        public void Resolve_SameDecisionTwice_SecondCallIsDuplicateNoOp()
        {
            var engine = new ScenarioResolutionEngine();

            var request = new ScenarioResolutionRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                ScenarioResolutionMode.Fixed,
                ScenarioDecisionPoint.PreMatch,
                "PRE_MATCH",
                string.Empty);

            var input = new ScenarioResolutionEngineInput(
                request,
                null,
                null,
                null,
                null,
                null,
                null);

            var first = engine.Resolve(input);
            var second = engine.Resolve(input);

            Assert.That(
                first.CommitDisposition,
                Is.EqualTo(ScenarioResolutionCommitDisposition.NewDecision));

            Assert.That(
                first.ShouldApplyConfig,
                Is.True);

            Assert.That(
                second.CommitDisposition,
                Is.EqualTo(ScenarioResolutionCommitDisposition.DuplicateNoOp));

            Assert.That(
                second.ShouldApplyConfig,
                Is.False);

            Assert.That(
                second.GuardReasonCode,
                Is.EqualTo(
                    AEDGuardCodes.DuplicateDecisionNoOp));

            Assert.That(
                second.Decision,
                Is.SameAs(first.Decision));
        }

        [Test]
        public void Resolve_SameDecisionIdDifferentSemanticInput_ReturnsIdentityConflict()
        {
            var engine = new ScenarioResolutionEngine();

            var decisionId = Guid.NewGuid();
            var matchId = Guid.NewGuid();

            var firstRequest = new ScenarioResolutionRequest(
                decisionId,
                matchId,
                ScenarioResolutionMode.Fixed,
                ScenarioDecisionPoint.PreMatch,
                "PRE_MATCH",
                string.Empty);

            var firstInput = new ScenarioResolutionEngineInput(
                firstRequest,
                null,
                null,
                null,
                null,
                null,
                null);

            var first = engine.Resolve(firstInput);

            var conflictingRequest = new ScenarioResolutionRequest(
                decisionId,
                matchId,
                ScenarioResolutionMode.Fixed,
                ScenarioDecisionPoint.PreMatch,
                "DIFFERENT_PHASE_CONTEXT",
                string.Empty);

            var conflictingInput = new ScenarioResolutionEngineInput(
                conflictingRequest,
                null,
                null,
                null,
                null,
                null,
                null);

            var conflict = engine.Resolve(conflictingInput);

            Assert.That(
                conflict.CommitDisposition,
                Is.EqualTo(
                    ScenarioResolutionCommitDisposition.DecisionIdentityConflict));

            Assert.That(
                conflict.ShouldApplyConfig,
                Is.False);

            Assert.That(
                conflict.HasDecisionIdentityConflict,
                Is.True);

            Assert.That(
                conflict.GuardReasonCode,
                Is.EqualTo(
                    AEDReasonCodes.DecisionIdentityConflict));

            Assert.That(
                conflict.AttemptedDecision,
                Is.Not.SameAs(first.Decision));

            Assert.That(
                conflict.Decision,
                Is.SameAs(first.Decision));
        }
    }
}
