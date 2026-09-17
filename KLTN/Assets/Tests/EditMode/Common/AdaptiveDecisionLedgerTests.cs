using System;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;

namespace EchoProtocol.AI.Common.Tests
{
    public sealed class AdaptiveDecisionLedgerTests
    {
        [Test]
        public void Lookup_SameDecisionIdAndFingerprint_ReturnsExactReplay()
        {
            var ledger = new AdaptiveDecisionLedger();
            var decisionId = Guid.NewGuid();
            var decision = CreateDecision(decisionId);

            Assert.That(
                ledger.TryRecord(
                    decision,
                    "fingerprint-A"),
                Is.True);

            var lookup = ledger.Lookup(
                decisionId,
                "fingerprint-A",
                out var replay);

            Assert.That(
                lookup,
                Is.EqualTo(
                    AdaptiveDecisionLedgerLookup.ExactReplay));

            Assert.That(
                replay,
                Is.SameAs(decision));
        }

        [Test]
        public void Lookup_SameDecisionIdDifferentFingerprint_ReturnsConflictAndPreservesOriginal()
        {
            var ledger = new AdaptiveDecisionLedger();
            var decisionId = Guid.NewGuid();

            var original =
                CreateDecision(decisionId);

            Assert.That(
                ledger.TryRecord(
                    original,
                    "fingerprint-A"),
                Is.True);

            var lookup = ledger.Lookup(
                decisionId,
                "fingerprint-B",
                out var existing);

            Assert.That(
                lookup,
                Is.EqualTo(
                    AdaptiveDecisionLedgerLookup.IdentityConflict));

            Assert.That(
                existing,
                Is.SameAs(original));

            var conflictingReplacement =
                CreateDecision(decisionId);

            Assert.That(
                ledger.TryRecord(
                    conflictingReplacement,
                    "fingerprint-B"),
                Is.False);

            var replayLookup = ledger.Lookup(
                decisionId,
                "fingerprint-A",
                out var retained);

            Assert.That(
                replayLookup,
                Is.EqualTo(
                    AdaptiveDecisionLedgerLookup.ExactReplay));

            Assert.That(
                retained,
                Is.SameAs(original));
        }

        private static AdaptiveDecision CreateDecision(
            Guid decisionId)
        {
            return new AdaptiveDecision(
                decisionId,
                AdaptiveDecisionResult.NoChange,
                ScenarioFallbackAction.None,
                string.Empty,
                AdaptationIntent.Hold,
                PolicyNoChangeReason.HoldRule,
                null,
                Array.Empty<string>());
        }
    }
}
