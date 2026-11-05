using System;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;

namespace EchoProtocol.AI.Common.Tests
{
    public sealed class AEDCurrentMatchPressureV1Tests
    {
        private static readonly Guid Match =
            Guid.Parse("00000000-0000-0000-0000-000000000401");

        private static readonly string User =
            "00000000-0000-0000-0000-000000000402";

        private static AEDv2CurrentMatchEvidence Canonical(
            bool complete = true,
            int downCount = 0)
        {
            var start = new DateTime(
                2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);

            return new AEDv2CurrentMatchEvidence(
                Match, "CORE_COLLECTION", 1, "roster",
                start, start.AddSeconds(10),
                2, downCount, 0, 0, 0, 0,
                complete, Array.Empty<string>());
        }

        private static AEDPursuitEvidenceCollectorV1 Pursuit()
        {
            var c = new AEDPursuitEvidenceCollectorV1();
            c.ResetMatch(Match);
            c.StartPhase(Match, 1, "CORE_COLLECTION");
            return c;
        }

        private static AEDMinionEvidenceCollectorV1 Minion()
        {
            var c = new AEDMinionEvidenceCollectorV1();
            c.ResetMatch(Match);
            c.StartPhase(Match, 1, "CORE_COLLECTION");
            return c;
        }

        private static AEDSurvivalEvidenceCollectorV1 Survival()
        {
            var c = new AEDSurvivalEvidenceCollectorV1();
            c.ResetMatch(Match);
            c.StartPhase(Match, 1, "CORE_COLLECTION");
            return c;
        }

        [Test]
        public void EmptyValidEvidenceDoesNotInventHighPressure()
        {
            var pressure = AEDCurrentMatchPressureV1.Project(
                Canonical(),
                Pursuit().Freeze(),
                Minion().Freeze(),
                Survival().Freeze(),
                100, 1);

            Assert.That(pressure.SourceComplete, Is.True);
            Assert.That(pressure.Level,
                Is.EqualTo(AEDPressureLevelV1.Quiet));
            Assert.That(pressure.ResearchOnly, Is.True);
        }

        [Test]
        public void RecentCommittedStalkerHitRaisesPressure()
        {
            var c = Pursuit();

            c.Observe(true, "stalker-1", User,
                "CHASE", 100, 1, "state:100");

            c.RecordConsequence(
                "stalker-1", User,
                AEDPursuitFactKindV1.Hit,
                "hit:105", 105,
                "STALKER_HIT_COMMITTED", false);

            var pressure = AEDCurrentMatchPressureV1.Project(
                Canonical(),
                c.Freeze(),
                Minion().Freeze(),
                Survival().Freeze(),
                106, 1);

            Assert.That(pressure.Level,
                Is.EqualTo(AEDPressureLevelV1.Critical));
            Assert.That(pressure.Score, Is.GreaterThan(0.7d));
        }

        [Test]
        public void IncompleteCanonicalEvidenceProducesUnknownPressure()
        {
            var pressure = AEDCurrentMatchPressureV1.Project(
                Canonical(complete: false),
                Pursuit().Freeze(),
                Minion().Freeze(),
                Survival().Freeze(),
                100, 1);

            Assert.That(pressure.SourceComplete, Is.False);
            Assert.That(pressure.Score, Is.Null);
            Assert.That(pressure.Level,
                Is.EqualTo(AEDPressureLevelV1.Unknown));
        }

        [Test]
        public void PhaseLevelDownWithoutRecentThreatDoesNotRaisePressure()
        {
            var pressure = AEDCurrentMatchPressureV1.Project(
                Canonical(downCount: 1),
                Pursuit().Freeze(),
                Minion().Freeze(),
                Survival().Freeze(),
                1000,
                10);

            Assert.That(pressure.SourceComplete, Is.True);
            Assert.That(
                pressure.Level,
                Is.EqualTo(AEDPressureLevelV1.Quiet));
            Assert.That(pressure.Score, Is.EqualTo(0d));
            Assert.That(
                pressure.ReasonCodes,
                Does.Contain(
                    "PHASE_DOWN_OBSERVED_NOT_TIME_RESOLVED"));
        }
    }
}
