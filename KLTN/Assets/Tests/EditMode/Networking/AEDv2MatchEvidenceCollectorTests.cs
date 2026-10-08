using System;
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
            return target.GetType().GetMethod(method)
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
    }
}
