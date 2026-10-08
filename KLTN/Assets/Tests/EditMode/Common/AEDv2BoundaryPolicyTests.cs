using System;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;

namespace EchoProtocol.AI.Common.Tests
{
    public sealed class AEDv2BoundaryPolicyTests
    {
        private static AEDv2CurrentMatchEvidence Evidence(Guid matchId,
            bool complete = true, int downs = 2, int noise = 0, int objectives = 1) =>
            new AEDv2CurrentMatchEvidence(matchId, "CORE_COLLECTION", 1, "roster",
                new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 10, 8, 0, 1, 0, DateTimeKind.Utc),
                2, downs, 0, 0, noise, objectives, complete,
                complete ? Array.Empty<string>() : new[] { "TELEMETRY_INCOMPLETE" });

        [Test]
        public void CompletedPhaseCanGrantOneReviveAtNextBoundary()
        {
            var matchId = Guid.NewGuid();
            Assert.That(AEDv2BoundaryPolicy.TryPropose(AEDv2Plan.Normal(), Evidence(matchId),
                matchId, "roster", 1, true, ScenarioDecisionPoint.AllowedPhaseBoundary,
                out var next, out var key, out _), Is.True);
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
                (Evidence(id), "roster", 1u, false)
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
                out var next, out var key, out _), Is.True);
            Assert.That(key, Is.EqualTo(AEDv2Key.DetectionAcquireSeconds));
            Assert.That(next.ReviveBonus, Is.EqualTo(1));
            Assert.That(next.Get(key), Is.EqualTo(1.5));
        }
    }
}
