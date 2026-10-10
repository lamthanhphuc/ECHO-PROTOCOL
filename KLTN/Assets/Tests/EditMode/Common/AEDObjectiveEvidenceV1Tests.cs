using System;
using System.Linq;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;

namespace EchoProtocol.AI.Common.Tests
{
    public sealed class AEDObjectiveEvidenceV1Tests
    {
        private static readonly Guid MatchId =
            Guid.Parse("00000000-0000-0000-0000-000000000010");
        private static readonly Guid UserId =
            Guid.Parse("00000000-0000-0000-0000-000000000020");

        private static AEDObjectiveEvidenceCollectorV1 Create(string phase = "CORE_COLLECTION")
        {
            var collector = new AEDObjectiveEvidenceCollectorV1();
            collector.ResetMatch(MatchId);
            collector.StartPhase(MatchId, 1, phase);
            return collector;
        }

        private static bool Place(AEDObjectiveEvidenceCollectorV1 collector,
            Guid eventId, string sector = "sector-a", int slot = 0,
            uint ordinal = 1, string phase = "CORE_COLLECTION", bool accepted = true,
            string occurrence = "core:place:1") =>
            collector.RecordCorePlaced(accepted, eventId, "core-object", sector,
                slot, ordinal, occurrence, UserId.ToString("D"), 120,
                "FusionStateAuthority", phase);

        private static AEDObjectiveEvidenceV1 Freeze(AEDObjectiveEvidenceCollectorV1 collector) =>
            collector.Freeze(DateTime.UnixEpoch, DateTime.UnixEpoch.AddSeconds(30));

        [Test]
        public void AcceptedAndRejectedPlacementAreSeparatedFromOpportunityCount()
        {
            var collector = Create();
            Assert.That(collector.RegisterCorePlacementSlots("sector-a", 2), Is.True);
            Assert.That(Place(collector, Guid.NewGuid(), accepted: false), Is.False);
            Assert.That(Place(collector, Guid.NewGuid()), Is.True);

            var frozen = Freeze(collector);
            Assert.That(frozen.UnitCompletionRate.Status, Is.EqualTo(AEDMetricStatusV1.Available));
            Assert.That(frozen.UnitCompletionRate.EligibleOpportunities, Is.EqualTo(2));
            Assert.That(frozen.UnitCompletionRate.ResolvedSuccesses, Is.EqualTo(1));
            Assert.That(frozen.UnitCompletionRate.ResolvedFailures, Is.EqualTo(1));
            Assert.That(frozen.UnitCompletionRate.Value, Is.EqualTo(0.5d));
            Assert.That(frozen.UnitCompletionRate.ConfigSource, Is.Null);
            Assert.That(frozen.UnitCompletionRate.SourceSystem,
                Is.EqualTo(AEDObjectiveEvidenceV1.SourceSystemName));
            Assert.That(frozen.Units.Count, Is.EqualTo(1));
        }

        [Test]
        public void CanonicalEventAndObjectiveUnitAreDeduplicated()
        {
            var collector = Create();
            collector.RegisterCorePlacementSlots("sector-a", 2);
            var eventId = Guid.NewGuid();
            Assert.That(Place(collector, eventId), Is.True);
            Assert.That(Place(collector, eventId), Is.False);
            Assert.That(Place(collector, Guid.NewGuid(), slot: 1,
                ordinal: 2, occurrence: "core:place:1"), Is.False);
            Assert.That(Place(collector, Guid.NewGuid(), slot: 0,
                ordinal: 2, occurrence: "core:place:2"), Is.False);

            var frozen = Freeze(collector);
            Assert.That(frozen.UnitCompletionRate.Status, Is.EqualTo(AEDMetricStatusV1.Invalid));
            Assert.That(frozen.Units.Count, Is.EqualTo(1));
        }

        [Test]
        public void InvalidPhaseAndMissingOpportunityAreNotZeroRates()
        {
            var invalid = Create("POWER_PUZZLE");
            Assert.That(Place(invalid, Guid.NewGuid()), Is.False);
            Assert.That(Freeze(invalid).UnitCompletionRate.Status,
                Is.EqualTo(AEDMetricStatusV1.Invalid));

            var missing = Create();
            Assert.That(Place(missing, Guid.NewGuid()), Is.False);
            var incomplete = Freeze(missing).UnitCompletionRate;
            Assert.That(incomplete.Status, Is.EqualTo(AEDMetricStatusV1.Incomplete));
            Assert.That(incomplete.Value, Is.Null);
        }

        [Test]
        public void NoOpportunityUnsupportedAndCensoredOnlyAreExplicitStatuses()
        {
            var none = Create();
            none.RegisterCorePlacementSlots("sector-a", 0);
            Assert.That(Freeze(none).UnitCompletionRate.Status,
                Is.EqualTo(AEDMetricStatusV1.NoOpportunity));

            var unsupported = Create("ZONE_2_OBJECTIVE");
            Assert.That(Freeze(unsupported).UnitCompletionRate.Status,
                Is.EqualTo(AEDMetricStatusV1.Unsupported));

            Assert.That(Enum.GetValues(typeof(AEDMetricStatusV1)).Length, Is.EqualTo(6));
            var censored = new AEDMetricResultV1(
                "Objective.UnitCompletionRate", MatchId, 1, "CORE_COLLECTION",
                null, null, null, null, null, null, null, "1.1", null, null,
                "UNITY_FUSION_HOST_OBJECTIVE", "FusionStateAuthority", null,
                null, null, 1, 0, 0, 1, 0, null, "unit", null,
                AEDMetricMeasurementKindV1.Rate, "unit", null, null, null, 0,
                "InsufficientForPolicy", AEDMetricStatusV1.CensoredOnly,
                new[] { "PHASE_ENDED" }, DateTime.UnixEpoch,
                DateTime.UnixEpoch.AddSeconds(1));
            Assert.That(censored.Value, Is.Null);
        }

        [Test]
        public void FrozenSnapshotIsStableAndPhasesRemainIsolated()
        {
            var collector = Create();
            collector.RegisterCorePlacementSlots("sector-a", 1);
            Place(collector, Guid.NewGuid());
            var first = Freeze(collector);
            Assert.That(Place(collector, Guid.NewGuid(), slot: 0,
                ordinal: 2, occurrence: "late"), Is.False);
            Assert.That(first.Units.Count, Is.EqualTo(1));

            collector.StartPhase(MatchId, 2, "ZONE_2_OBJECTIVE");
            var second = Freeze(collector);
            Assert.That(second.PhaseOrdinal, Is.EqualTo(2));
            Assert.That(second.Units, Is.Empty);
            Assert.That(second.UnitCompletionRate.Status,
                Is.EqualTo(AEDMetricStatusV1.Unsupported));

            var nextMatch = Guid.NewGuid();
            collector.ResetMatch(nextMatch);
            collector.StartPhase(nextMatch, 1, "CORE_COLLECTION");
            collector.RegisterCorePlacementSlots("new-sector", 1);
            Assert.That(Place(collector, Guid.NewGuid(), sector: "new-sector"), Is.True);
            Assert.That(Freeze(collector).MatchId, Is.EqualTo(nextMatch));

            var v2 = new AEDv2CurrentMatchEvidence(
                Guid.Parse("00000000-0000-0000-0000-000000000001"),
                "ZONE_1_OBJECTIVE", 1, "roster-fingerprint",
                DateTime.Parse("2026-10-08T00:00:00.123Z").ToUniversalTime(),
                DateTime.Parse("2026-10-08T00:01:00.456Z").ToUniversalTime(),
                2, 1, 2, 0, 3, 4, true, Array.Empty<string>(), 5);
            Assert.That(v2.EvidenceFingerprint,
                Is.EqualTo("2a3ad9de5f5f5ee16fbb36879049951b5e12ab83a96912b75fe84481fd22cd18"));
        }
    }
}
