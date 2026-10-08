using System;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.Networking.Tests
{
    public sealed class AEDv2SnapshotMapperTests
    {
        private static readonly Type DtoType = Type.GetType("EchoProtocol.AI.AED.AEDSnapshotApiData, Assembly-CSharp", true);
        private static readonly Type MapperType = Type.GetType("EchoProtocol.AI.AED.AEDSnapshotMapper, Assembly-CSharp", true);

        private static string Payload(string playerJson, int teamSize = 1) =>
            "{\"snapshotId\":\"" + Guid.NewGuid() + "\",\"targetMatchId\":\"" + Guid.NewGuid()
            + "\",\"decisionPoint\":\"PRE_MATCH\",\"phaseContext\":\"PRE_MATCH\","
            + "\"snapshotContentFingerprint\":\"verified-fingerprint\",\"rosterIdentity\":\"verified-roster\","
            + "\"teamSize\":" + teamSize + ",\"snapshotValidity\":\"VALID\",\"reasonCodes\":[],"
            + "\"createdAtUtc\":\"2026-10-08T00:00:00Z\",\"profileFormulaSemanticId\":\"PROFILE_FORMULA_V1_1\","
            + "\"survivalComparisonKey\":\"survival-key\",\"noiseComparisonKey\":\"noise-key\","
            + "\"survivalAggregationStatus\":\"AVAILABLE\",\"noiseAggregationStatus\":\"AVAILABLE\","
            + "\"survivalMeanObservedScore\":50,\"survivalMeanObservedScorePresent\":true,"
            + "\"noiseMeanObservedScore\":60,\"noiseMeanObservedScorePresent\":true,"
            + "\"survivalObservedActiveCount\":1,\"noiseObservedActiveCount\":1,"
            + "\"targetMatchCurrent\":true,\"decisionPointCurrent\":true,\"phaseContextCurrent\":true,"
            + "\"rosterCurrent\":true,\"profileRevisionsCurrent\":true,\"snapshotFingerprintValid\":true,"
            + "\"profileSemanticsSupported\":true,\"players\":[" + playerJson + "]}";

        private static string Player() =>
            "{\"userId\":\"" + Guid.NewGuid() + "\",\"profileAvailable\":true,"
            + "\"profileLineageId\":\"" + Guid.NewGuid() + "\",\"profileRevision\":5,"
            + "\"profileRevisionPresent\":true,\"survivalScore\":50,\"survivalScorePresent\":true,"
            + "\"survivalStatus\":\"ACTIVE\",\"survivalSampleCount\":2,\"survivalComparisonKey\":\"survival-key\","
            + "\"noiseScore\":60,\"noiseScorePresent\":true,\"noiseStatus\":\"ACTIVE\","
            + "\"noiseSampleCount\":2,\"noiseComparisonKey\":\"noise-key\"}";

        private static (bool Mapped, object Snapshot, object Currency, string Reason) Map(string json)
        {
            var dto = JsonUtility.FromJson(json, DtoType);
            var args = new object[] { dto, null, null, null };
            var mapped = (bool)MapperType.GetMethod("TryMap").Invoke(null, args);
            return (mapped, args[1], args[2], (string)args[3]);
        }

        [Test]
        public void RealBackendShapeMapsScoresAndRevision()
        {
            var result = Map(Payload(Player()));
            Assert.That(result.Mapped, Is.True, result.Reason);
            Assert.That(result.Currency.GetType().GetProperty("IsCurrent").GetValue(result.Currency), Is.True);
            var players = (System.Collections.IEnumerable)result.Snapshot.GetType()
                .GetProperty("PlayerProfileSnapshots").GetValue(result.Snapshot);
            foreach (var player in players)
                Assert.That(player.GetType().GetProperty("ProfileRevision").GetValue(player), Is.EqualTo(5L));
        }

        [Test]
        public void NullActiveScoreAndDuplicateUserAreRejected()
        {
            var player = Player();
            Assert.That(Map(Payload(player.Replace("\"survivalScorePresent\":true",
                "\"survivalScorePresent\":false"))).Mapped, Is.False);
            Assert.That(Map(Payload(player + "," + player, 2)).Mapped, Is.False);
        }

        [Test]
        public void StaleMissingColdAndUnsupportedInputCannotBeCurrent()
        {
            var valid = Payload(Player());
            var stale = Map(valid.Replace("\"profileRevisionsCurrent\":true",
                "\"profileRevisionsCurrent\":false"));
            Assert.That(stale.Mapped, Is.True);
            Assert.That(stale.Currency.GetType().GetProperty("IsCurrent")
                .GetValue(stale.Currency), Is.False);
            Assert.That(Map(valid.Replace("\"profileAvailable\":true",
                "\"profileAvailable\":false")).Mapped, Is.False);
            Assert.That(Map(valid.Replace("PROFILE_FORMULA_V1_1",
                "PROFILE_FORMULA_UNSUPPORTED")).Mapped, Is.False);
            Assert.That(Map(valid.Replace("\"survivalStatus\":\"ACTIVE\"",
                "\"survivalStatus\":\"COLD_START\"")).Mapped, Is.False);
        }
    }
}
