using System;
using System.Linq;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;

namespace EchoProtocol.AI.Common.Tests
{
    public sealed class AEDToolNoiseEvidenceCollectorV1Tests
    {
        private readonly Guid _matchId =
            Guid.Parse("00000000-0000-0000-0000-000000000050");
        private const string UserId = "00000000-0000-0000-0000-000000000060";
        private const string OtherUserId = "00000000-0000-0000-0000-000000000070";

        private AEDToolNoiseEvidenceCollectorV1 Create()
        {
            var collector = new AEDToolNoiseEvidenceCollectorV1();
            collector.ResetMatch(_matchId);
            collector.StartPhase(_matchId, 1, "CORE_COLLECTION");
            return collector;
        }

        [Test]
        public void ToolActionAndFirstAidEffectHaveTypedProvenance()
        {
            var collector = Create();
            Assert.That(collector.RecordToolAction("CORE_STABILIZER", UserId,
                "stabilizer:1", AEDEvidenceSourceCategoryV1.GameplayOnlyAccepted,
                null, "FusionStateAuthority"), Is.True);
            Assert.That(collector.RecordToolAction("CORE_STABILIZER", UserId,
                "stabilizer:1", AEDEvidenceSourceCategoryV1.GameplayOnlyAccepted,
                null, "FusionStateAuthority"), Is.False);

            Assert.That(collector.RecordToolAction("FIRST_AID_KIT", UserId,
                "firstaid:1", AEDEvidenceSourceCategoryV1.CanonicalTelemetryAccepted,
                "event-tool", "FusionStateAuthority"), Is.True);
            Assert.That(collector.RecordToolEffect("FIRST_AID_KIT", UserId,
                OtherUserId, "revive:4", AEDToolEffectOutcomeV1.ResolvedSuccess,
                "event-revive", "FusionStateAuthority"), Is.True);

            var facts = collector.Freeze().Facts;
            Assert.That(facts.Count, Is.EqualTo(3));
            var effect = facts.Single(fact => fact.Kind == AEDToolNoiseFactKindV1.ToolEffectResolved);
            Assert.That(effect.UserId, Is.EqualTo(UserId));
            Assert.That(effect.RelatedUserId, Is.EqualTo(OtherUserId));
            Assert.That(effect.CanonicalEventId, Is.EqualTo("event-revive"));
        }

        [Test]
        public void CanonicalAndGameplayNoiseKeepTheirOriginalSourceTypes()
        {
            var collector = Create();
            collector.RecordNoise("SPRINT", UserId, "noise:sprint",
                AEDEvidenceSourceCategoryV1.CanonicalTelemetryAccepted,
                "event-sprint", "FusionStateAuthority");
            collector.RecordNoise("MINION_ALERT", UserId, "noise:minion",
                AEDEvidenceSourceCategoryV1.GameplayOnlyAccepted,
                null, "FusionStateAuthority");
            collector.RecordNoise("FIELD_SCANNER", UserId, "noise:scanner",
                AEDEvidenceSourceCategoryV1.GameplayOnlyAccepted,
                null, "FusionStateAuthority");

            var facts = collector.Freeze().Facts;
            Assert.That(facts.Select(fact => fact.NoiseType),
                Is.EquivalentTo(new[] { "SPRINT", "MINION_ALERT", "FIELD_SCANNER" }));
            Assert.That(facts.Count(fact => fact.SourceCategory
                == AEDEvidenceSourceCategoryV1.GameplayOnlyAccepted), Is.EqualTo(2));
        }

        [Test]
        public void RepeatedSourceOccurrenceAndFrozenLateFactDoNotMutateSnapshot()
        {
            var collector = Create();
            collector.RecordNoise("NOISE_MAKER", UserId, "same-occurrence",
                AEDEvidenceSourceCategoryV1.GameplayOnlyAccepted,
                null, "FusionStateAuthority");
            Assert.That(collector.RecordNoise("NOISE_MAKER", UserId, "same-occurrence",
                AEDEvidenceSourceCategoryV1.GameplayOnlyAccepted,
                null, "FusionStateAuthority"), Is.False);

            var frozen = collector.Freeze();
            Assert.That(collector.RecordToolAction("DOOR_JAMMER", UserId,
                "late-action", AEDEvidenceSourceCategoryV1.GameplayOnlyAccepted,
                null, "FusionStateAuthority"), Is.False);
            Assert.That(frozen.Facts.Count, Is.EqualTo(1));

            collector.StartPhase(_matchId, 2, "ZONE_2_OBJECTIVE");
            Assert.That(collector.LastFrozen, Is.SameAs(frozen));
            Assert.That(collector.Freeze().Facts, Is.Empty);
        }

        [Test]
        public void RejectedCanonicalToolAndNoiseUseRejectedFactKinds()
        {
            var collector = Create();
            Assert.That(collector.RecordToolAction("FIELD_SCANNER", UserId,
                "tool:rejected", AEDEvidenceSourceCategoryV1.CanonicalEmissionRejected,
                null, "FusionStateAuthority"), Is.True);
            Assert.That(collector.RecordNoise("SPRINT", UserId,
                "noise:rejected", AEDEvidenceSourceCategoryV1.CanonicalEmissionRejected,
                null, "FusionStateAuthority"), Is.True);

            var facts = collector.Freeze().Facts;
            Assert.That(facts.Select(fact => fact.Kind), Is.EquivalentTo(new[]
            {
                AEDToolNoiseFactKindV1.ToolActionRejected,
                AEDToolNoiseFactKindV1.GameplayNoiseRejected
            }));
            Assert.That(facts.Any(fact => fact.Kind == AEDToolNoiseFactKindV1.ToolActionAccepted
                || fact.Kind == AEDToolNoiseFactKindV1.GameplayNoiseAccepted), Is.False);
        }

        [Test]
        public void InvalidIdentityIsRejectedAndDoesNotCreateAnOutcome()
        {
            var collector = Create();
            Assert.That(collector.RecordNoise("SPRINT", "invalid", "noise:1",
                AEDEvidenceSourceCategoryV1.CanonicalTelemetryAccepted,
                "event-1", "FusionStateAuthority"), Is.False);
            Assert.That(collector.IsInvalid, Is.True);
            Assert.That(collector.Freeze().Facts, Is.Empty);
        }
    }
}
