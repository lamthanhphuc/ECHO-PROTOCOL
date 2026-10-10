using System;
using System.Linq;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;

namespace EchoProtocol.AI.Common.Tests
{
    public sealed class AEDPursuitEvidenceCollectorV1Tests
    {
        private static readonly Guid Match =
            Guid.Parse("00000000-0000-0000-0000-000000000031");
        private static readonly string UserA =
            "00000000-0000-0000-0000-000000000041";
        private static readonly string UserB =
            "00000000-0000-0000-0000-000000000042";

        private static AEDPursuitEvidenceCollectorV1 Create()
        {
            var collector = new AEDPursuitEvidenceCollectorV1();
            collector.ResetMatch(Match);
            collector.StartPhase(Match, 1, "CORE_COLLECTION");
            return collector;
        }

        private static bool Observe(AEDPursuitEvidenceCollectorV1 c,
            string state, string target, long tick, string key = null,
            string stalker = "stalker-1", int tickRate = 1) =>
            c.Observe(true, stalker, target, state, tick, tickRate,
                key ?? $"{state}:{target}:{tick}");

        [Test]
        public void DetectDoesNotOpenPursuit()
        {
            var c = Create();
            Assert.That(Observe(c, "DETECT", UserA, 1), Is.False);
            Assert.That(c.Freeze().Episodes, Is.Empty);
        }

        [Test]
        public void ChaseOpensExactlyOneEpisodeAndDuplicateSnapshotIsIgnored()
        {
            var c = Create();
            Assert.That(Observe(c, "CHASE", UserA, 1, "chase-1"), Is.True);
            Assert.That(Observe(c, "CHASE", UserA, 1, "chase-1"), Is.False);
            Assert.That(Observe(c, "CHASE", UserA, 2), Is.True);
            Assert.That(c.Freeze().Episodes.Count, Is.EqualTo(1));
        }

        [Test]
        public void SearchThenChaseSameTargetIsReacquired()
        {
            var c = Create();
            Observe(c, "CHASE", UserA, 1);
            Observe(c, "SEARCH", null, 2);
            Observe(c, "CHASE", UserA, 3);
            var episode = c.Freeze().Episodes.Single();
            Assert.That(episode.TerminalOutcome, Is.EqualTo(AEDPursuitTerminalV1.Censored));
            Assert.That(episode.ReacquisitionCount, Is.EqualTo(1));
            Assert.That(episode.Facts.Single().Kind, Is.EqualTo(AEDPursuitFactKindV1.Reacquired));
        }

        [Test]
        public void AttackRecoverAndSearchStatesArePreservedOnHostEpisode()
        {
            var c = Create();
            Observe(c, "CHASE", UserA, 1);
            Observe(c, "ATTACK", UserA, 2);
            Observe(c, "RECOVER", UserA, 3);
            Observe(c, "SEARCH", null, 4);
            c.CensorActive("PHASE_END", 5);
            var states = c.Freeze().Episodes.Single().StateSegments
                .Select(segment => segment.State).ToArray();
            Assert.That(states, Is.EqualTo(new[] { "CHASE", "ATTACK", "RECOVER", "SEARCH" }));
        }

        [Test]
        public void SearchThenPatrolBeforeGraceIsNotEscape()
        {
            var c = Create();
            Observe(c, "CHASE", UserA, 10);
            Observe(c, "SEARCH", null, 11);
            Observe(c, "PATROL", null, 15);
            Assert.That(c.Freeze().Episodes.Single().TerminalOutcome,
                Is.EqualTo(AEDPursuitTerminalV1.Censored));
        }

        [Test]
        public void TargetSwitchIsNotEscapeSuccess()
        {
            var c = Create();
            Observe(c, "CHASE", UserA, 1);
            Observe(c, "CHASE", UserB, 2);
            var episodes = c.Freeze().Episodes;
            Assert.That(episodes.Count, Is.EqualTo(2));
            Assert.That(episodes[0].TerminalOutcome,
                Is.EqualTo(AEDPursuitTerminalV1.TargetSwitched));
            Assert.That(episodes[1].TerminalOutcome,
                Is.EqualTo(AEDPursuitTerminalV1.Censored));
        }

        [Test]
        public void EscapeRequiresVersionedGraceWindow()
        {
            var c = Create();
            Observe(c, "CHASE", UserA, 100, tickRate: 10);
            Observe(c, "SEARCH", null, 101, tickRate: 10);
            Observe(c, "PATROL", null, 151, tickRate: 10);
            Assert.That(c.Freeze().Episodes.Single().TerminalOutcome,
                Is.EqualTo(AEDPursuitTerminalV1.Escaped));
            Assert.That(c.LastFrozen.Episodes.Single().TerminalReason,
                Does.Contain(AEDPursuitEvidenceCollectorV1.GraceWindowVersion));
        }

        [Test]
        public void DespawnAndPhaseEndCensorActiveEpisode()
        {
            var despawned = Create();
            Observe(despawned, "CHASE", UserA, 3);
            despawned.CensorStalker("stalker-1", "STALKER_DESPAWN", 4);
            Assert.That(despawned.Freeze().Episodes.Single().TerminalOutcome,
                Is.EqualTo(AEDPursuitTerminalV1.Censored));

            var phaseEnd = Create();
            Observe(phaseEnd, "CHASE", UserA, 3);
            phaseEnd.CensorActive("PHASE_END", 4);
            Assert.That(phaseEnd.Freeze().Episodes.Single().TerminalReason,
                Is.EqualTo("PHASE_END"));
        }

        [Test]
        public void MissingIdentityMarksEvidenceIncomplete()
        {
            var c = Create();
            Assert.That(Observe(c, "CHASE", null, 1), Is.False);
            var snapshot = c.Freeze();
            Assert.That(snapshot.IsIncomplete, Is.True);
            Assert.That(snapshot.Episodes, Is.Empty);
        }

        [Test]
        public void DuplicateEventDoesNotMultiplyEpisodesOrFacts()
        {
            var c = Create();
            Observe(c, "CHASE", UserA, 1, "state-1");
            Observe(c, "SEARCH", null, 2, "state-2");
            Observe(c, "CHASE", UserA, 3, "state-3");
            Observe(c, "CHASE", UserA, 3, "state-3");
            Assert.That(c.Freeze().Episodes.Single().ReacquisitionCount, Is.EqualTo(1));
        }

        [Test]
        public void FrozenSnapshotAndNestedFactsAreImmutable()
        {
            var c = Create();
            Observe(c, "CHASE", UserA, 1);
            Observe(c, "SEARCH", null, 2);
            Observe(c, "CHASE", UserA, 3);
            var snapshot = c.Freeze();
            Assert.That(Observe(c, "CHASE", UserA, 4), Is.False);
            Assert.That(c.LastFrozen, Is.SameAs(snapshot));
            Assert.That(snapshot.Episodes.Single().Facts.Count, Is.EqualTo(1));
            Assert.Throws<NotSupportedException>(() =>
                ((System.Collections.Generic.IList<AEDPursuitEpisodeV1>)snapshot.Episodes).Clear());
        }

        [Test]
        public void PursuitMetricProjectionKeepsUnsupportedSourcesUnsupported()
        {
            var c = Create();
            Observe(c, "CHASE", UserA, 1);
            Observe(c, "SEARCH", null, 2);
            Observe(c, "PATROL", null, 7);
            var metrics = AEDPursuitMetricProjectorV1.Project(c.Freeze(), 1,
                DateTime.UnixEpoch, DateTime.UnixEpoch.AddSeconds(10));
            Assert.That(metrics.Count, Is.EqualTo(6));
            Assert.That(metrics[0].Status, Is.EqualTo(AEDMetricStatusV1.Available));
            Assert.That(metrics[4].Status, Is.EqualTo(AEDMetricStatusV1.Unsupported));
            Assert.That(metrics[5].Status, Is.EqualTo(AEDMetricStatusV1.Unsupported));
            Assert.That(metrics.All(metric => !metric.DecisionEligible), Is.True);
        }

        [Test]
        public void CommittedHitWithoutLifeTransitionDoesNotBecomeDown()
        {
            var c = Create();
            Observe(c, "CHASE", UserA, 100);
            Assert.That(c.RecordConsequence(UserA, AEDPursuitFactKindV1.Hit,
                "hit-1", 101, "STALKER_HIT_COMMITTED", false), Is.True);
            var episode = c.Freeze().Episodes.Single();
            Assert.That(episode.TerminalOutcome, Is.EqualTo(AEDPursuitTerminalV1.Censored));
            Assert.That(episode.Facts.Count(f => f.Kind == AEDPursuitFactKindV1.Hit), Is.EqualTo(1));
            Assert.That(episode.Facts.Any(f => f.Kind == AEDPursuitFactKindV1.Downed), Is.False);
        }

        [Test]
        public void DownAndDirectEliminationAreDistinctTerminalConsequences()
        {
            var down = Create();
            Observe(down, "CHASE", UserA, 10);
            Assert.That(down.RecordConsequence(UserA, AEDPursuitFactKindV1.Downed,
                "down-1", 15, "STALKER_ATTACK", false), Is.True);
            Assert.That(down.Freeze().Episodes.Single().TerminalOutcome,
                Is.EqualTo(AEDPursuitTerminalV1.Downed));

            var eliminated = Create();
            Observe(eliminated, "CHASE", UserA, 20);
            Assert.That(eliminated.RecordConsequence(UserA,
                AEDPursuitFactKindV1.Eliminated, "elimination-1", 24,
                "STALKER_DIRECT_HIT", true), Is.True);
            var fact = eliminated.Freeze().Episodes.Single().Facts.Single();
            Assert.That(fact.Kind, Is.EqualTo(AEDPursuitFactKindV1.Eliminated));
            Assert.That(fact.DirectFromHit, Is.True);
        }

        [Test]
        public void BleedoutCauseIsPreservedWithoutBeingReclassifiedAsHit()
        {
            var c = Create();
            Observe(c, "CHASE", UserA, 10);
            Assert.That(c.RecordConsequence(UserA,
                AEDPursuitFactKindV1.Downed, "bleedout-down", 30,
                "BLEEDOUT", false), Is.True);
            var fact = c.Freeze().Episodes.Single().Facts.Single();
            Assert.That(fact.Cause, Is.EqualTo("BLEEDOUT"));
            Assert.That(fact.DirectFromHit, Is.False);
        }

        [Test]
        public void DuplicateLifeCallbackCannotCreateSecondTerminalReceipt()
        {
            var c = Create();
            Observe(c, "CHASE", UserA, 1);
            Assert.That(c.RecordConsequence(UserA, AEDPursuitFactKindV1.Downed,
                "down-event", 2, "STALKER_ATTACK", false), Is.True);
            Assert.That(c.RecordConsequence(UserA, AEDPursuitFactKindV1.Downed,
                "down-event", 2, "STALKER_ATTACK", false), Is.False);
            Assert.That(c.Freeze().Episodes.Count, Is.EqualTo(1));
        }

        [Test]
        public void PursuitMetricsUseRunnerTicksAndNullForNoOpportunityOrCensoredOnly()
        {
            var timed = Create();
            Observe(timed, "CHASE", UserA, 100);
            timed.RecordConsequence(UserA, AEDPursuitFactKindV1.Downed,
                "down-1", 140, "STALKER_ATTACK", false);
            var duration = AEDPursuitMetricProjectorV1.Project(timed.Freeze(), 10,
                DateTime.UnixEpoch, DateTime.UnixEpoch.AddSeconds(4))
                .Single(m => m.MetricId == "Stalker.PursuitDurationSeconds");
            Assert.That(duration.Value, Is.EqualTo(4d));

            var empty = Create();
            var emptyMetric = AEDPursuitMetricProjectorV1.Project(empty.Freeze(), 10,
                DateTime.UnixEpoch, DateTime.UnixEpoch.AddSeconds(1))
                .Single(m => m.MetricId == "Stalker.PursuitEscapeRate");
            Assert.That(emptyMetric.Status, Is.EqualTo(AEDMetricStatusV1.NoOpportunity));
            Assert.That(emptyMetric.Value, Is.Null);

            var censored = Create();
            Observe(censored, "CHASE", UserA, 1);
            var censoredMetric = AEDPursuitMetricProjectorV1.Project(censored.Freeze(), 10,
                DateTime.UnixEpoch, DateTime.UnixEpoch.AddSeconds(1))
                .Single(m => m.MetricId == "Stalker.PursuitEscapeRate");
            Assert.That(censoredMetric.Status, Is.EqualTo(AEDMetricStatusV1.CensoredOnly));
            Assert.That(censoredMetric.Value, Is.Null);
        }
    }
}
