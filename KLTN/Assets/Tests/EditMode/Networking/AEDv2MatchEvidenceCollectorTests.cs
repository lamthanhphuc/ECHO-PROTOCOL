using System;
using System.Linq;
using System.Reflection;
using EchoProtocol.AI.Common.AED;
using Fusion;
using NUnit.Framework;
using UnityEngine;

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
        public void GameplayOnlyEventsDoNotInvalidateCanonicalEvidence()
        {
            var host = new GameObject("AEDv2-GameplayOnly-Test");
            try
            {
                var type = Type.GetType(
                    "EchoProtocol.Networking.Authority.MatchAuthorityRuntime, Assembly-CSharp",
                    true);
                var runtime = host.AddComponent(type);
                var collector = type.GetField(
                    "_aedv2Evidence",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(runtime);

                var start = DateTime.UtcNow;
                Call(collector, "StartPhase",
                    Guid.NewGuid(), "roster", "CORE_COLLECTION", 1u, start);

                Assert.That((bool)type.GetMethod("RecordTeamToolUsed")
                    .Invoke(runtime, new object[]
                    {
                        PlayerRef.None, "core-1", "CORE_STABILIZER", null
                    }), Is.False);

                Assert.That((bool)type.GetMethod("RecordRuntimeNoise")
                    .Invoke(runtime, new object[]
                    {
                        PlayerRef.None, "noise-1", start,
                        "MINION_ALERT", 1d, Vector3.zero, 55d
                    }), Is.False);

                var evidence = (AEDv2CurrentMatchEvidence)Call(
                    collector, "Freeze",
                    "CORE_COLLECTION", 1, "roster",
                    start.AddSeconds(40));

                Assert.That(evidence.TelemetryCompleteness, Is.True);
                Assert.That(evidence.TeamToolUseCount, Is.Zero);
                Assert.That(evidence.AcceptedNoiseCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void MissingCanonicalTelemetryInvalidatesEvidence()
        {
            var host = new GameObject("AEDv2-TelemetryFailure-Test");
            try
            {
                var type = Type.GetType(
                    "EchoProtocol.Networking.Authority.MatchAuthorityRuntime, Assembly-CSharp",
                    true);
                var runtime = host.AddComponent(type);
                var collector = type.GetField(
                    "_aedv2Evidence",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(runtime);

                var matchId = Guid.NewGuid();
                var start = DateTime.UtcNow;

                Call(collector, "StartPhase",
                    matchId, "roster", "CORE_COLLECTION", 1u, start);

                Assert.That((bool)type.GetMethod("RecordRuntimeNoise")
                    .Invoke(runtime, new object[]
                    {
                        PlayerRef.None, "noise-2", start,
                        "SPRINT", 1d, Vector3.zero, 30d
                    }), Is.False);

                var first = (AEDv2CurrentMatchEvidence)Call(
                    collector, "Freeze",
                    "CORE_COLLECTION", 1, "roster",
                    start.AddSeconds(40));

                Assert.That(first.TelemetryCompleteness, Is.False);
                Assert.That(first.ReasonCodes,
                    Does.Contain("TELEMETRY_INCOMPLETE"));

                Call(collector, "StartPhase",
                    matchId, "roster", "ZONE_2_OBJECTIVE",
                    2u, start.AddMinutes(1));

                Assert.That((bool)type.GetMethod("RecordTeamToolUsed")
                    .Invoke(runtime, new object[]
                    {
                        PlayerRef.None, "tool-2", "FIELD_SCANNER", null
                    }), Is.False);

                var second = (AEDv2CurrentMatchEvidence)Call(
                    collector, "Freeze",
                    "ZONE_2_OBJECTIVE", 1, "roster",
                    start.AddMinutes(2));

                Assert.That(second.TelemetryCompleteness, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
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
