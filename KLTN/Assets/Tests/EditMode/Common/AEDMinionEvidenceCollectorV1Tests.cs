using System;
using System.Linq;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;

namespace EchoProtocol.AI.Common.Tests
{
    public sealed class AEDMinionEvidenceCollectorV1Tests
    {
        private static readonly Guid Match =
            Guid.Parse("00000000-0000-0000-0000-000000000051");
        private static readonly string UserA =
            "00000000-0000-0000-0000-000000000061";
        private static readonly string UserB =
            "00000000-0000-0000-0000-000000000062";

        private static AEDMinionEvidenceCollectorV1 Create()
        {
            var c = new AEDMinionEvidenceCollectorV1();
            c.ResetMatch(Match);
            c.StartPhase(Match, 1, "CORE_COLLECTION");
            return c;
        }

        private static bool Observe(AEDMinionEvidenceCollectorV1 c,
            string state, string target, long tick, string id = "minion-1",
            string key = null) => c.Observe(true, id, "Zone01", state, target,
                tick, 1, key ?? $"{id}:{state}:{target}:{tick}");

        [Test]
        public void RoamDoesNotOpenEncounter()
        {
            var c = Create();
            Observe(c, "Roam", null, 1);
            Assert.That(c.Freeze().Episodes, Is.Empty);
        }

        [Test]
        public void TrackAndHarassOpenOneEncounterAndTargetSwitchAddsSegment()
        {
            var c = Create();
            Observe(c, "Track", UserA, 1);
            Observe(c, "Harass", UserA, 2);
            Observe(c, "Track", UserB, 3);
            var snapshot = c.Freeze();
            Assert.That(snapshot.Episodes.Count, Is.EqualTo(1));
            Assert.That(snapshot.Episodes[0].Participants.Count, Is.EqualTo(2));
            Assert.That(snapshot.Episodes[0].Participants[0].UserId, Is.EqualTo(UserA));
            Assert.That(snapshot.Episodes[0].Participants[1].UserId, Is.EqualTo(UserB));
        }

        [Test]
        public void FleeIsDisengagedNotPlayerCounterSuccess()
        {
            var c = Create();
            Observe(c, "Harass", UserA, 1);
            Observe(c, "Flee", null, 2);
            Assert.That(c.Freeze().Episodes.Single().TerminalOutcome,
                Is.EqualTo(AEDMinionTerminalV1.Disengaged));
        }

        [Test]
        public void AcceptedAndRejectedAlertsRemainSeparateAndDeduplicated()
        {
            var c = Create();
            Observe(c, "Track", UserA, 1);
            c.RecordFact(AEDMinionFactKindV1.AlertAttempted, "minion-1", "alert-1",
                2, UserA, effectKind: "MINION_ALERT", accepted: false);
            c.RecordFact(AEDMinionFactKindV1.AlertAttempted, "minion-1", "alert-2",
                3, UserA, effectKind: "MINION_ALERT", accepted: true, sourceEventId: "noise-2");
            c.RecordFact(AEDMinionFactKindV1.AlertAccepted, "minion-1", "alert-2",
                3, UserA, effectKind: "MINION_ALERT", accepted: true, sourceEventId: "noise-2");
            Assert.That(c.RecordFact(AEDMinionFactKindV1.AlertAccepted, "minion-1", "alert-2",
                3, UserA, effectKind: "MINION_ALERT", accepted: true, sourceEventId: "noise-2"), Is.False);
            var facts = c.Freeze().Episodes.Single().Facts;
            Assert.That(facts.Count(f => f.Kind == AEDMinionFactKindV1.AlertAttempted), Is.EqualTo(2));
            Assert.That(facts.Count(f => f.Kind == AEDMinionFactKindV1.AlertAccepted), Is.EqualTo(1));
            Assert.That(facts.Single(f => f.OccurrenceKey == "alert-1").Accepted, Is.False);
        }

        [Test]
        public void RejectedSlowAttemptIsRecordedSeparatelyFromAppliedEffect()
        {
            var c = Create();
            Observe(c, "Harass", UserA, 1);
            c.RecordFact(AEDMinionFactKindV1.AttackAttempted, "minion-1", "attack-1",
                2, UserA, effectKind: "SLOW", attemptOrdinal: 1, accepted: false);
            var metric = AEDMinionMetricProjectorV1.Project(c.Freeze(),
                DateTime.UnixEpoch, DateTime.UnixEpoch.AddSeconds(3))
                .Single(m => m.MetricId == "Minion.SlowHitRate");
            Assert.That(metric.Status, Is.EqualTo(AEDMetricStatusV1.Available));
            Assert.That(metric.Value, Is.Zero);
        }

        [Test]
        public void DuplicateAttemptIsDeduplicatedAndFixedModeNeverEnablesPolicy()
        {
            var c = Create();
            Observe(c, "Harass", UserA, 1);
            Assert.That(c.RecordFact(AEDMinionFactKindV1.AttackAttempted,
                "minion-1", "attempt-1", 2, UserA, effectKind: "SLOW",
                attemptOrdinal: 1, accepted: true), Is.True);
            Assert.That(c.RecordFact(AEDMinionFactKindV1.AttackAttempted,
                "minion-1", "attempt-1", 2, UserA, effectKind: "SLOW",
                attemptOrdinal: 1, accepted: true), Is.False);

            var metric = AEDMinionMetricProjectorV1.Project(c.Freeze(),
                DateTime.UnixEpoch, DateTime.UnixEpoch.AddSeconds(3))
                .Single(m => m.MetricId == "Minion.SlowHitRate");
            Assert.That(metric.EligibleOpportunities, Is.EqualTo(1));
            Assert.That(metric.Value, Is.EqualTo(1d));
            Assert.That(metric.DecisionEligible, Is.False);
        }

        [Test]
        public void NoiseMakerNeedsMatchedHostReactionAndKeepsUnresolvedOpportunityCensored()
        {
            var c = Create();
            Observe(c, "Harass", UserA, 0);
            c.RecordFact(AEDMinionFactKindV1.NoiseMakerOpportunity,
                "minion-1", "noise-opportunity", 1, UserA,
                effectKind: "ACTIVE_THREAT_DIVERSION",
                sourceEventId: "noise-event");
            var beforeReaction = AEDMinionMetricProjectorV1.Project(c.Freeze(),
                DateTime.UnixEpoch, DateTime.UnixEpoch.AddSeconds(2))
                .Single(m => m.MetricId == "Minion.DistractionSuccessRate");
            Assert.That(beforeReaction.Status, Is.EqualTo(AEDMetricStatusV1.CensoredOnly));
            Assert.That(beforeReaction.Value, Is.Null);

            var resolved = Create();
            Observe(resolved, "Harass", UserA, 0);
            resolved.RecordFact(AEDMinionFactKindV1.NoiseMakerOpportunity,
                "minion-1", "noise-opportunity", 1, UserA,
                effectKind: "ACTIVE_THREAT_DIVERSION",
                sourceEventId: "noise-event");
            resolved.RecordFact(AEDMinionFactKindV1.NoiseMakerReaction,
                "minion-1", "noise-reaction", 2, UserA,
                effectKind: "ACTIVE_THREAT_DIVERSION",
                accepted: true, sourceEventId: "noise-event");
            var metric = AEDMinionMetricProjectorV1.Project(resolved.Freeze(),
                DateTime.UnixEpoch, DateTime.UnixEpoch.AddSeconds(3))
                .Single(m => m.MetricId == "Minion.DistractionSuccessRate");
            Assert.That(metric.Status, Is.EqualTo(AEDMetricStatusV1.Available));
            Assert.That(metric.Value, Is.EqualTo(1d));
            Assert.That(metric.DecisionEligible, Is.False);
        }

        [Test]
        public void SlowAppliedThenRoamIsNotCountered()
        {
            var c = Create();
            Observe(c, "Harass", UserA, 1);
            c.RecordFact(AEDMinionFactKindV1.SlowApplied, "minion-1", "slow-1",
                2, UserA, effectKind: "SLOW_APPLIED", accepted: true);
            Observe(c, "Roam", null, 3);
            Observe(c, "Roam", null, 8);
            var episode = c.Freeze().Episodes.Single();
            Assert.That(episode.TerminalOutcome, Is.EqualTo(AEDMinionTerminalV1.Disengaged));
            Assert.That(episode.Facts.Any(f => f.Kind == AEDMinionFactKindV1.SlowApplied), Is.True);
        }

        [Test]
        public void CoreForcedDropThenRoamIsNotCountered()
        {
            var c = Create();
            Observe(c, "Harass", UserA, 1);
            c.RecordFact(AEDMinionFactKindV1.CoreForcedDrop, "minion-1", "core-drop-1",
                2, UserA, objectId: "core-1", accepted: true);
            Observe(c, "Roam", null, 3);
            Observe(c, "Roam", null, 8);
            Assert.That(c.Freeze().Episodes.Single().TerminalOutcome,
                Is.EqualTo(AEDMinionTerminalV1.Disengaged));
        }

        [Test]
        public void FlashlightDeathConfirmsCounteredExactlyOnce()
        {
            var c = Create();
            Observe(c, "Harass", UserA, 1);
            Assert.That(c.RecordFact(AEDMinionFactKindV1.TeamDeathReceipt,
                "minion-1", "death-1", 2, effectKind: "FLASHLIGHT_DEATH", accepted: true), Is.True);
            Assert.That(c.RecordFact(AEDMinionFactKindV1.TeamDeathReceipt,
                "minion-1", "death-1", 2, effectKind: "FLASHLIGHT_DEATH", accepted: true), Is.False);
            var snapshot = c.Freeze();
            Assert.That(snapshot.Episodes.Single().TerminalOutcome,
                Is.EqualTo(AEDMinionTerminalV1.Countered));
            Assert.That(snapshot.Episodes.Single().Facts.Count(f =>
                f.Kind == AEDMinionFactKindV1.TeamDeathReceipt), Is.EqualTo(1));
        }

        [Test]
        public void IdleNoiseMakerIsNotThreatOpportunity()
        {
            var c = Create();
            c.RecordFact(AEDMinionFactKindV1.NoiseMakerOpportunity,
                "minion-1", "idle-noise", 1, UserA, sourceEventId: "noise-idle");
            var metric = AEDMinionMetricProjectorV1.Project(c.Freeze(),
                DateTime.UnixEpoch, DateTime.UnixEpoch.AddSeconds(2))
                .Single(m => m.MetricId == "Minion.DistractionSuccessRate");
            Assert.That(metric.Status, Is.EqualTo(AEDMetricStatusV1.NoOpportunity));
            Assert.That(metric.Value, Is.Null);
        }

        [Test]
        public void CoreForcedDropAndCarryStealHaveIndependentFacts()
        {
            var c = Create();
            Observe(c, "Harass", UserA, 1);
            c.RecordFact(AEDMinionFactKindV1.AttackAttempted, "minion-1", "attack-1",
                2, UserA, objectId: "core-1", effectKind: "CORE", attemptOrdinal: 1, accepted: true);
            c.RecordFact(AEDMinionFactKindV1.CoreForcedDrop, "minion-1", "attack-1",
                2, UserA, objectId: "core-1", effectKind: "CORE_FORCED_DROP", accepted: true);
            var snapshot = c.Freeze();
            Assert.That(snapshot.Episodes.Single().Facts.Count(f =>
                f.Kind == AEDMinionFactKindV1.CoreForcedDrop), Is.EqualTo(1));
            Assert.That(snapshot.Episodes.Single().Facts.Any(f =>
                f.Kind == AEDMinionFactKindV1.CoreStolen), Is.False);
        }

        [Test]
        public void ToolRelocationRequiresCommittedEffect()
        {
            var c = Create();
            Observe(c, "Harass", UserA, 1);
            c.RecordFact(AEDMinionFactKindV1.AttackAttempted, "minion-1", "tool-attempt",
                2, UserA, objectId: "tool-type:3", effectKind: "TOOL", accepted: false);
            c.RecordFact(AEDMinionFactKindV1.AttackAttempted, "minion-1", "tool-success",
                3, UserA, objectId: "tool-type:3", effectKind: "TOOL", accepted: true);
            c.RecordFact(AEDMinionFactKindV1.ToolRelocated, "minion-1", "tool-success",
                3, UserA, objectId: "tool-type:3", effectKind: "TOOL_RELOCATED", accepted: true);
            var rate = AEDMinionMetricProjectorV1.Project(c.Freeze(),
                DateTime.UnixEpoch, DateTime.UnixEpoch.AddSeconds(4))
                .Single(m => m.MetricId == "Minion.ToolProtectionRate");
            Assert.That(rate.Value, Is.EqualTo(0.5d));
        }

        [Test]
        public void FlashlightContributorsRemainDistinctAndTeamDeathIsSingleReceipt()
        {
            var c = Create();
            c.RecordFact(AEDMinionFactKindV1.FlashlightContribution, "minion-1", "beam-a",
                1, UserA, accepted: true, seconds: 0.02);
            c.RecordFact(AEDMinionFactKindV1.FlashlightContribution, "minion-1", "beam-b",
                1, UserB, accepted: true, seconds: 0.02);
            c.RecordFact(AEDMinionFactKindV1.TeamDeathReceipt, "minion-1", "death-1",
                2, effectKind: "FLASHLIGHT_DEATH", accepted: true);
            Assert.That(c.RecordFact(AEDMinionFactKindV1.TeamDeathReceipt, "minion-1", "death-1",
                2, effectKind: "FLASHLIGHT_DEATH", accepted: true), Is.False);
            var facts = c.Freeze().Facts;
            Assert.That(facts.Count(f => f.Kind == AEDMinionFactKindV1.FlashlightContribution), Is.EqualTo(2));
            Assert.That(facts.Count(f => f.Kind == AEDMinionFactKindV1.TeamDeathReceipt), Is.EqualTo(1));
        }

        [Test]
        public void RecoveryUsesMatchingObjectAndUnrelatedPickupDoesNotResolveLoss()
        {
            var c = Create();
            Observe(c, "Harass", UserA, 1);
            c.RecordFact(AEDMinionFactKindV1.CoreForcedDrop, "minion-1", "loss-1",
                2, UserA, objectId: "core-a", accepted: true);
            c.RecordFact(AEDMinionFactKindV1.ItemRecovered, "world-pickup", "pickup-x",
                3, UserB, objectId: "core-b", accepted: true);
            var snapshot = c.Freeze();
            Assert.That(snapshot.Episodes.Single().Facts.Single(f =>
                f.Kind == AEDMinionFactKindV1.CoreForcedDrop).ObjectId, Is.EqualTo("core-a"));
            Assert.That(snapshot.Facts.Single().ObjectId, Is.EqualTo("core-b"));
            Assert.That(AEDMinionMetricProjectorV1.Project(snapshot,
                DateTime.UnixEpoch, DateTime.UnixEpoch.AddSeconds(4))
                .Single(m => m.MetricId == "Minion.RecoveryRate").Status,
                Is.EqualTo(AEDMetricStatusV1.Unsupported));
        }

        [Test]
        public void PhaseCutAndDespawnCensorAndFrozenFactsStayStable()
        {
            var c = Create();
            Observe(c, "Track", UserA, 4);
            c.CensorMinion("minion-1", "MINION_DESPAWN", 5);
            var frozen = c.Freeze();
            Assert.That(frozen.Episodes.Single().TerminalOutcome,
                Is.EqualTo(AEDMinionTerminalV1.Censored));
            c.StartPhase(Match, 2, "ZONE_2_OBJECTIVE");
            Assert.That(c.LastFrozen, Is.SameAs(frozen));
            Assert.That(frozen.Episodes.Single().TerminalReason, Is.EqualTo("MINION_DESPAWN"));
        }

        [Test]
        public void MissingUserIdentityMarksEvidenceIncomplete()
        {
            var c = Create();
            Assert.That(Observe(c, "Track", null, 1), Is.False);
            Assert.That(c.Freeze().IsIncomplete, Is.True);
        }

        [Test]
        public void MetricsKeepCensoredOnlyNoOpportunityAndUnsupportedExplicit()
        {
            var c = Create();
            var noOpportunity = AEDMinionMetricProjectorV1.Project(c.Freeze(),
                DateTime.UnixEpoch, DateTime.UnixEpoch.AddSeconds(1));
            Assert.That(noOpportunity.Single(m => m.MetricId == "Minion.EvasionRate").Status,
                Is.EqualTo(AEDMetricStatusV1.NoOpportunity));
            Assert.That(noOpportunity.Single(m => m.MetricId == "Minion.AlertImpactRate").Status,
                Is.EqualTo(AEDMetricStatusV1.Unsupported));

            var censored = Create();
            Observe(censored, "Track", UserA, 1);
            var onlyCensored = AEDMinionMetricProjectorV1.Project(censored.Freeze(),
                DateTime.UnixEpoch, DateTime.UnixEpoch.AddSeconds(2));
            Assert.That(onlyCensored.Single(m => m.MetricId == "Minion.EvasionRate").Status,
                Is.EqualTo(AEDMetricStatusV1.CensoredOnly));
        }
    }
}
