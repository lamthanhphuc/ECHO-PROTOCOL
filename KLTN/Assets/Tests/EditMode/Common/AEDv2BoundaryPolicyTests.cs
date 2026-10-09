using System;
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
        public void LaterBoundaryRetainsEarlierKey()
        {
            var id = Guid.NewGuid();
            var previous = AEDv2Plan.Normal().With(AEDv2Key.ReviveBonusPerZone, 1);
            Assert.That(AEDv2BoundaryPolicy.TryPropose(previous,
                Evidence(id, downs: 0, noise: 9), id, "roster", 1, true,
                ScenarioDecisionPoint.AllowedPhaseBoundary,
                out var next, out var key, out _, new AEDv2RosterSafety(true, false)), Is.True);
            Assert.That(key, Is.EqualTo(AEDv2Key.DetectionAcquireSeconds));
            Assert.That(next.ReviveBonus, Is.EqualTo(1));
            Assert.That(next.Get(key), Is.EqualTo(1.5));
        }

        [TestCase(0, 0, 0, 0, 1, 0, 60,
            AEDv2Key.SpecialCooldownSeconds, 540d)]
        [TestCase(2, 0, 0, 0, 0, 0, 60,
            AEDv2Key.ReviveBonusPerZone, 1d)]
        [TestCase(0, 8, 0, 0, 0, 0, 60,
            AEDv2Key.DetectionAcquireSeconds, 1.5d)]
        [TestCase(1, 0, 0, 0, 0, 3, 60,
            AEDv2Key.HearingMultiplier, 0.85d)]
        [TestCase(0, 0, 2, 0, 0, 0, 120,
            AEDv2Key.PatrolSpeed, 7d)]
        [TestCase(0, 0, 1, 0, 0, 0, 60,
            AEDv2Key.ChaseSpeed, 8d)]
        public void ExpandedPolicyChoosesExpectedKey(
            int downs, int noise, int objectives, int revives,
            int eliminated, int tools, int duration,
            AEDv2Key expectedKey, double expectedValue)
        {
            var id = Guid.NewGuid();
            var evidence = Evidence(id,
                downs: downs,
                noise: noise,
                objectives: objectives,
                revives: revives,
                eliminated: eliminated,
                tools: tools,
                durationSeconds: duration);

            var proposed = AEDv2BoundaryPolicy.TryPropose(
                AEDv2Plan.Normal(), evidence, id, "roster", 1u, true,
                ScenarioDecisionPoint.AllowedPhaseBoundary,
                out var next, out var key, out _, new AEDv2RosterSafety(true, false));

            Assert.That(proposed, Is.True);
            Assert.That(key, Is.EqualTo(expectedKey));
            Assert.That(next.Get(key), Is.EqualTo(expectedValue));

            foreach (var spec in AEDv2Catalog.All)
            {
                if (spec.Key == key) continue;
                Assert.That(next.Get(spec.Key), Is.EqualTo(spec.Baseline));
            }
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
        public void PressureNeedsRosterSafetyButReliefRemainsAvailable()
        {
            var id = Guid.NewGuid();
            var pressureEvidence = Evidence(id, objectives: 2, durationSeconds: 120);
            Assert.That(AEDv2BoundaryPolicy.TryPropose(AEDv2Plan.Normal(), pressureEvidence,
                id, "roster", 1u, true, ScenarioDecisionPoint.AllowedPhaseBoundary,
                out _, out _, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("AED_V2_ROSTER_PRESSURE_GUARD"));

            Assert.That(AEDv2BoundaryPolicy.TryPropose(AEDv2Plan.Normal(), pressureEvidence,
                id, "roster", 1u, true, ScenarioDecisionPoint.AllowedPhaseBoundary,
                out _, out _, out _, new AEDv2RosterSafety(true, false)), Is.True);
            Assert.That(AEDv2BoundaryPolicy.TryPropose(AEDv2Plan.Normal(), pressureEvidence,
                id, "roster", 1u, true, ScenarioDecisionPoint.AllowedPhaseBoundary,
                out _, out _, out _, new AEDv2RosterSafety(true, true)), Is.False);
        }
    }
}
