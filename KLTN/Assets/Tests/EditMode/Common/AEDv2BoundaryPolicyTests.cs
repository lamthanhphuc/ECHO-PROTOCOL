using System;
using System.Reflection;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;

namespace EchoProtocol.AI.Common.Tests
{
    public sealed class AEDv2BoundaryPolicyTests
    {
        private static AEDv2CurrentMatchEvidence Evidence(
            Guid matchId,
            bool complete = true,
            int downs = 0,
            int noise = 0,
            int objectives = 0,
            int revives = 0,
            int eliminated = 0,
            int tools = 0,
            int durationSeconds = 60)
        {
            var start = new DateTime(
                2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);

            return new AEDv2CurrentMatchEvidence(
                matchId,
                "CORE_COLLECTION",
                1,
                "roster",
                start,
                start.AddSeconds(durationSeconds),
                Math.Max(1, 2 - eliminated),
                downs,
                revives,
                eliminated,
                noise,
                objectives,
                complete,
                complete
                    ? Array.Empty<string>()
                    : new[] { "TELEMETRY_INCOMPLETE" },
                tools);
        }

        [Test]
        public void CompletedPhaseCanGrantOneReviveAtNextBoundary()
        {
            var matchId = Guid.NewGuid();
            Assert.That(AEDv2BoundaryPolicy.TryPropose(AEDv2Plan.Normal(), Evidence(matchId, downs: 2),
                matchId, "roster", 1, true, ScenarioDecisionPoint.AllowedPhaseBoundary,
                out var next, out var key, out _, new AEDv2RosterSafety(true, false)), Is.True);
            Assert.That(key, Is.EqualTo(AEDv2Key.ReviveBonusPerZone));
            Assert.That(next.ReviveBonus, Is.EqualTo(1));
            Assert.That(next.Get(AEDv2Key.ChaseSpeed), Is.EqualTo(7.5));
        }

        [Test]
        public void FinalHuntSetupDoesNotGrantReviveForMultipleDowns()
        {
            var matchId = Guid.NewGuid();
            var previous = AEDv2Plan.Normal();

            Assert.That(AEDv2BoundaryPolicy.TryPropose(previous, Evidence(matchId, downs: 2),
                matchId, "roster", 1, true, ScenarioDecisionPoint.FinalHuntSetup,
                out var next, out var key, out _), Is.False);
            Assert.That(key, Is.EqualTo(default(AEDv2Key)));
            Assert.That(next.ReviveBonus, Is.EqualTo(previous.ReviveBonus));
        }

        [Test]
        public void IncompleteStaleOrUnsafeEvidenceHolds()
        {
            var id = Guid.NewGuid();
            foreach (var (evidence, roster, ordinal, safe) in new[]
            {
                (Evidence(id, false), "roster", 1u, true),
                (Evidence(id), "changed", 1u, true),
                (Evidence(id), "roster", 2u, true),
                (Evidence(id), "roster", 1u, false),
                (Evidence(id, durationSeconds: 29), "roster", 1u, true)
            })
                Assert.That(AEDv2BoundaryPolicy.TryPropose(AEDv2Plan.Normal(), evidence,
                    id, roster, ordinal, safe, ScenarioDecisionPoint.AllowedPhaseBoundary,
                    out _, out _, out _), Is.False);
        }

        [Test]
        public void NoiseOnlyEvidenceHoldsAndRetainsEarlierPlan()
        {
            var id = Guid.NewGuid();
            var previous = AEDv2Plan.Normal().With(AEDv2Key.ReviveBonusPerZone, 1);
            Assert.That(AEDv2BoundaryPolicy.TryPropose(previous,
                Evidence(id, downs: 0, noise: 9), id, "roster", 1, true,
                ScenarioDecisionPoint.AllowedPhaseBoundary,
                out var next, out _, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("HOLD_METRIC_UNSUPPORTED"));
            Assert.That(next.ReviveBonus, Is.EqualTo(1));
        }

        [Test]
        public void RepeatedDownsDelaySeekingAfterReviveBonus()
        {
            var id = Guid.NewGuid();
            var previous = AEDv2Plan.Normal()
                .With(AEDv2Key.ReviveBonusPerZone, 1);

            var proposed = AEDv2BoundaryPolicy.TryPropose(
                previous, Evidence(id, downs: 2, revives: 1),
                id, "roster", 1u, true,
                ScenarioDecisionPoint.AllowedPhaseBoundary,
                out var next, out var key, out _, new AEDv2RosterSafety(true, false));

            Assert.That(proposed, Is.True);
            Assert.That(key, Is.EqualTo(AEDv2Key.SeekPlayersAfterSeconds));
            Assert.That(next.Get(key), Is.EqualTo(150d));
            Assert.That(next.ReviveBonus, Is.EqualTo(1));
        }

        [Test]
        public void FinalHuntCannotGrantReviveBonus()
        {
            var id = Guid.NewGuid();

            AEDv2BoundaryPolicy.TryPropose(
                AEDv2Plan.Normal(), Evidence(id, downs: 2),
                id, "roster", 1u, true,
                ScenarioDecisionPoint.FinalHuntSetup,
                out _, out var key, out _);

            Assert.That(key, Is.Not.EqualTo(AEDv2Key.ReviveBonusPerZone));
        }

        [Test]
        public void ObjectiveOnlyEvidenceCannotIncreasePressure()
        {
            var id = Guid.NewGuid();
            var pressureEvidence = Evidence(id, objectives: 2, durationSeconds: 120);
            Assert.That(AEDv2BoundaryPolicy.TryPropose(AEDv2Plan.Normal(), pressureEvidence,
                id, "roster", 1u, true, ScenarioDecisionPoint.AllowedPhaseBoundary,
                out _, out _, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("HOLD_METRIC_UNSUPPORTED"));

            var verifiedButNoCandidate = new AEDv2BoundaryHoldContext
            {
                PressureMetricStatus = AEDMetricStatusV1.Available,
                PressureMetricDecisionEligible = true
            };
            Assert.That(AEDv2BoundaryPolicy.TryPropose(AEDv2Plan.Normal(), pressureEvidence,
                id, "roster", 1u, true, ScenarioDecisionPoint.AllowedPhaseBoundary,
                out _, out _, out reason, new AEDv2RosterSafety(true, false),
                verifiedButNoCandidate), Is.False);
            Assert.That(reason, Is.EqualTo("HOLD_NO_ELIGIBLE_ADJUSTMENT"));
            Assert.That(AEDv2BoundaryPolicy.TryPropose(AEDv2Plan.Normal(), pressureEvidence,
                id, "roster", 1u, true, ScenarioDecisionPoint.AllowedPhaseBoundary,
                out _, out _, out reason, new AEDv2RosterSafety(false, false),
                verifiedButNoCandidate), Is.False);
            Assert.That(reason, Is.EqualTo("HOLD_INSUFFICIENT_OBSERVATION"));
        }

        [Test]
        public void HoldsExposeDeterministicFailureReasons()
        {
            var id = Guid.NewGuid();
            var plan = AEDv2Plan.Normal();
            AssertHold(null, Evidence(id), id, "roster", 1, true, null,
                "HOLD_INVALID_PLAN");
            AssertHold(plan, Evidence(id, complete: false), id, "roster", 1, true, null,
                "HOLD_EVIDENCE_INCOMPLETE");

            var fingerprint = Evidence(id);
            typeof(AEDv2CurrentMatchEvidence).GetField(
                "<EvidenceFingerprint>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(fingerprint, "bad");
            AssertHold(plan, fingerprint, id, "roster", 1, true, null,
                "HOLD_FINGERPRINT_MISMATCH");
            AssertHold(plan, Evidence(id), id, "changed", 1, true, null,
                "HOLD_ROSTER_CHANGED");
            AssertHold(plan, Evidence(id, durationSeconds: 60), id, "roster", 1, true,
                new AEDv2BoundaryHoldContext { ExpectedPhaseName = "ZONE_2_OBJECTIVE" },
                "HOLD_PHASE_MISMATCH");
            AssertHold(plan, Evidence(id), id, "roster", 1, false,
                new AEDv2BoundaryHoldContext { StalkerUnsafeState = true },
                "HOLD_UNSAFE_STALKER_STATE");
            AssertHold(plan, Evidence(id), id, "roster", 1, false,
                new AEDv2BoundaryHoldContext { PlayerDownedOrReviving = true },
                "HOLD_PLAYER_DOWNED_OR_REVIVING");
            AssertHold(plan, Evidence(id, durationSeconds: 29), id, "roster", 1, true,
                null, "HOLD_INSUFFICIENT_OBSERVATION");
            AssertHold(plan, Evidence(id), id, "roster", 1, true,
                new AEDv2BoundaryHoldContext
                    { PressureMetricStatus = AEDMetricStatusV1.NoOpportunity },
                "HOLD_METRIC_NO_OPPORTUNITY");
            AssertHold(plan, Evidence(id), id, "roster", 1, true, null,
                "HOLD_METRIC_UNSUPPORTED");
            AssertHold(plan, Evidence(id), id, "roster", 1, true,
                new AEDv2BoundaryHoldContext { AdjustmentBudgetExhausted = true },
                "HOLD_ADJUSTMENT_BUDGET_EXHAUSTED");
        }

        private static void AssertHold(AEDv2Plan plan,
            AEDv2CurrentMatchEvidence evidence, Guid matchId, string roster,
            uint ordinal, bool safe, AEDv2BoundaryHoldContext context,
            string expectedReason)
        {
            Assert.That(AEDv2BoundaryPolicy.TryPropose(plan, evidence, matchId,
                roster, ordinal, safe, ScenarioDecisionPoint.AllowedPhaseBoundary,
                out _, out _, out var reason, null, context), Is.False);
            Assert.That(reason, Is.EqualTo(expectedReason));
        }
    }
}
