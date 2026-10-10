using System;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;

namespace EchoProtocol.AI.Common.Tests
{
    public sealed class AEDSurvivalEvidenceCollectorV1Tests
    {
        private readonly Guid _matchId =
            Guid.Parse("00000000-0000-0000-0000-000000000030");
        private readonly string _userId =
            "00000000-0000-0000-0000-000000000040";

        private AEDSurvivalEvidenceCollectorV1 Create()
        {
            var collector = new AEDSurvivalEvidenceCollectorV1();
            collector.ResetMatch(_matchId);
            collector.StartPhase(_matchId, 1, "CORE_COLLECTION");
            return collector;
        }

        private bool Record(AEDSurvivalEvidenceCollectorV1 collector,
            AEDSurvivalOutcomeKindV1 kind, string key, uint ordinal,
            string cause, bool direct = false, string eventId = null,
            string relatedUser = null, string userId = null) =>
            collector.Record(kind, userId ?? _userId, relatedUser, key,
                ordinal, cause, direct, eventId, "FusionStateAuthority");

        [Test]
        public void OutcomeOccurrencesAndCanonicalEventsAreIdempotent()
        {
            var collector = Create();
            Assert.That(Record(collector, AEDSurvivalOutcomeKindV1.Downed,
                "life:down:1", 1, "Damage", eventId: "event-1"), Is.True);
            Assert.That(Record(collector, AEDSurvivalOutcomeKindV1.Downed,
                "life:down:1", 1, "Damage", eventId: "event-1"), Is.False);
            Assert.That(Record(collector, AEDSurvivalOutcomeKindV1.Revived,
                "life:revive:2", 2, "ReviveCompleted", eventId: "event-1"), Is.False);
            Assert.That(collector.Freeze().Outcomes.Count, Is.EqualTo(1));
        }

        [Test]
        public void DirectEliminationDoesNotCreateDownOutcome()
        {
            var collector = Create();
            Assert.That(Record(collector, AEDSurvivalOutcomeKindV1.DirectElimination,
                "life:eliminate:2", 2, "ReviveLimit", direct: true), Is.True);

            var frozen = collector.Freeze();
            Assert.That(frozen.Outcomes.Count, Is.EqualTo(1));
            Assert.That(frozen.Outcomes[0].Kind,
                Is.EqualTo(AEDSurvivalOutcomeKindV1.DirectElimination));
            Assert.That(frozen.Outcomes[0].DirectFromHit, Is.True);
        }

        [Test]
        public void BleedoutTeamEliminationReviveAndMixedCausesRemainDistinct()
        {
            var collector = Create();
            Record(collector, AEDSurvivalOutcomeKindV1.Downed,
                "down", 1, "Damage");
            Record(collector, AEDSurvivalOutcomeKindV1.Revived,
                "revive", 2, "ReviveCompleted", relatedUser: "reviver-user");
            Record(collector, AEDSurvivalOutcomeKindV1.BleedoutElimination,
                "bleedout", 3, "Bleedout");
            Record(collector, AEDSurvivalOutcomeKindV1.TeamElimination,
                "team", 4, "Bleedout:TEAM_DOWNED");
            Record(collector, AEDSurvivalOutcomeKindV1.OtherElimination,
                "other", 5, "GhostCatch");

            var frozen = collector.Freeze();
            Assert.That(frozen.Outcomes[1].RelatedUserId, Is.EqualTo("reviver-user"));
            Assert.That(frozen.Outcomes[2].Kind,
                Is.EqualTo(AEDSurvivalOutcomeKindV1.BleedoutElimination));
            Assert.That(frozen.Outcomes[3].Kind,
                Is.EqualTo(AEDSurvivalOutcomeKindV1.TeamElimination));
            Assert.That(frozen.Outcomes[4].Kind,
                Is.EqualTo(AEDSurvivalOutcomeKindV1.OtherElimination));
        }

        [Test]
        public void FrozenSnapshotIsStableAndPhaseAndMatchAreIsolated()
        {
            var collector = Create();
            Record(collector, AEDSurvivalOutcomeKindV1.Downed, "down", 1, "Damage");
            var first = collector.Freeze();
            Assert.That(Record(collector, AEDSurvivalOutcomeKindV1.Revived,
                "late", 2, "ReviveCompleted"), Is.False);
            Assert.That(first.Outcomes.Count, Is.EqualTo(1));

            collector.StartPhase(_matchId, 2, "ZONE_2_OBJECTIVE");
            Assert.That(collector.Freeze().Outcomes, Is.Empty);
            var nextMatch = Guid.NewGuid();
            collector.ResetMatch(nextMatch);
            collector.StartPhase(nextMatch, 1, "CORE_COLLECTION");
            Assert.That(Record(collector, AEDSurvivalOutcomeKindV1.Downed,
                "new-match", 1, "Damage"), Is.True);
            Assert.That(collector.Freeze().MatchId, Is.EqualTo(nextMatch));
        }

        [Test]
        public void InvalidIdentityOrDirectSourceIsRejected()
        {
            var collector = Create();
            Assert.That(Record(collector, AEDSurvivalOutcomeKindV1.Downed,
                "down", 1, "Damage", userId: "not-a-guid"), Is.False);
            Assert.That(collector.IsInvalid, Is.True);

            collector.StartPhase(_matchId, 2, "ZONE_2_OBJECTIVE");
            Assert.That(Record(collector, AEDSurvivalOutcomeKindV1.DirectElimination,
                "direct", 1, "ReviveLimit", direct: false), Is.False);
            Assert.That(collector.IsInvalid, Is.True);
        }
    }
}
