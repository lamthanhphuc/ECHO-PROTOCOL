using System;
using System.Linq;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;

namespace EchoProtocol.Networking.Tests
{
    public sealed class AEDv2MatchEvidenceCollectorTests
    {
        private static object CreateCollector()
        {
            var type = Type.GetType(
                "EchoProtocol.AI.AED.AEDv2MatchEvidenceCollector, Assembly-CSharp",
                true);
            return Activator.CreateInstance(type);
        }

        private static object Call(object target, string method,
            params object[] arguments)
        {
            return target.GetType().GetMethods().First(candidate => candidate.Name == method
                && candidate.GetParameters().Length == arguments.Length)
                .Invoke(target, arguments);
        }

        [Test]
        public void CanonicalPhaseProducesCompleteEvidence()
        {
            var collector = CreateCollector();
            var start = DateTime.UtcNow;

            Call(collector, "StartPhase",
                Guid.NewGuid(), "roster",
                "ZONE_2_OBJECTIVE", 2u, start);

            Call(collector, "RecordAcceptedDown", "down-1");
            Call(collector, "RecordAcceptedDown", "down-2");

            var evidence = (AEDv2CurrentMatchEvidence)Call(
                collector, "Freeze",
                "ZONE_2_OBJECTIVE", 2, "roster",
                start.AddSeconds(40));

            Assert.That(evidence.TelemetryCompleteness, Is.True);
            Assert.That(evidence.PhaseOrdinal, Is.EqualTo(2u));
            Assert.That(evidence.DownCount, Is.EqualTo(2));
        }

        [Test]
        public void InvalidatedEvidenceMustRemainIncomplete()
        {
            var collector = CreateCollector();
            var start = DateTime.UtcNow;

            Call(collector, "StartPhase",
                Guid.NewGuid(), "roster",
                "ZONE_3_FIND_FRIGATE", 3u, start);

            Call(collector, "MarkIncomplete");

            var evidence = (AEDv2CurrentMatchEvidence)Call(
                collector, "Freeze",
                "ZONE_3_FIND_FRIGATE", 2, "roster",
                start.AddSeconds(40));

            Assert.That(evidence.TelemetryCompleteness, Is.False);
        }

        [Test]
        public void PlayerEvidenceDeduplicatesWithTeamCounters()
        {
            var collector = CreateCollector();
            var start = DateTime.UtcNow;
            Call(collector, "StartPhase", Guid.NewGuid(), "roster", "ZONE_2", 1u, start);
            Call(collector, "RecordAcceptedDown", "down-1", "user-a");
            Call(collector, "RecordAcceptedDown", "down-1", "user-a");
            var evidence = (AEDv2CurrentMatchEvidence)Call(
                collector, "Freeze", "ZONE_2", 2, "roster", start.AddSeconds(40));
            var players = (System.Collections.IDictionary)collector.GetType()
                .GetProperty("PlayerEvidence").GetValue(collector);
            var player = (AEDv2PlayerPhaseEvidence)players["user-a"];
            Assert.That(evidence.DownCount, Is.EqualTo(1));
            Assert.That(player.DownCount, Is.EqualTo(1));
        }

        [Test]
        public void PhaseEvidenceFingerprintMatchesVersionedBackendContract()
        {
            var evidence = new AEDv2CurrentMatchEvidence(
                Guid.Parse("00000000-0000-0000-0000-000000000001"),
                "ZONE_1_OBJECTIVE", 1, "roster-fingerprint",
                DateTime.Parse("2026-10-08T00:00:00.123Z").ToUniversalTime(),
                DateTime.Parse("2026-10-08T00:01:00.456Z").ToUniversalTime(),
                2, 1, 2, 0, 3, 4, true, Array.Empty<string>(), 5);
            Assert.That(evidence.EvidenceFingerprint,
                Is.EqualTo("2a3ad9de5f5f5ee16fbb36879049951b5e12ab83a96912b75fe84481fd22cd18"));
        }
    }
}
