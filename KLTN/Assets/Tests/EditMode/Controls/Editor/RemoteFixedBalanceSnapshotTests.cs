using System;
using System.Security.Cryptography;
using System.Text;
using EchoProtocol.Networking.Authority;
using NUnit.Framework;

namespace EchoProtocol.Networking.Tests
{
    public sealed class RemoteFixedBalanceSnapshotTests
    {
        private const string ValidJson =
            "{\"schemaVersion\":\"RFB-1\"," +
            "\"player\":{\"maxHealth\":100,\"walkSpeed\":4," +
            "\"sprintSpeed\":7,\"maxStamina\":100}," +
            "\"stalker\":{\"patrolSpeed\":6,\"chaseSpeed\":7}," +
            "\"minion\":{\"roamSpeed\":5.5,\"trackSpeed\":8," +
            "\"attackDamage\":20}}";

        private static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(bytes))
                    .Replace("-", "").ToLowerInvariant();
            }
        }

        private static bool Parse(
            string json, out RemoteFixedBalanceSnapshot snapshot)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            return RemoteFixedBalanceSnapshot.TryDecode(
                Convert.ToBase64String(bytes), Hash(bytes), out snapshot);
        }

        [Test]
        public void ValidSnapshot_DecodesAllGameplayStats()
        {
            Assert.That(Parse(ValidJson, out var snapshot), Is.True);
            Assert.That(snapshot.player.maxHealth, Is.EqualTo(100f));
            Assert.That(snapshot.player.walkSpeed, Is.EqualTo(4f));
            Assert.That(snapshot.player.sprintSpeed, Is.EqualTo(7f));
            Assert.That(snapshot.player.maxStamina, Is.EqualTo(100f));
            Assert.That(snapshot.stalker.patrolSpeed, Is.EqualTo(6f));
            Assert.That(snapshot.stalker.chaseSpeed, Is.EqualTo(7f));
            Assert.That(snapshot.minion.roamSpeed, Is.EqualTo(5.5f));
            Assert.That(snapshot.minion.trackSpeed, Is.EqualTo(8f));
            Assert.That(snapshot.minion.attackDamage, Is.EqualTo(20f));
        }

        [Test]
        public void ModifiedPayload_WithOriginalHash_IsRejected()
        {
            var original = Encoding.UTF8.GetBytes(ValidJson);
            var modified = Encoding.UTF8.GetBytes(
                ValidJson.Replace("\"maxHealth\":100", "\"maxHealth\":200"));

            Assert.That(RemoteFixedBalanceSnapshot.TryDecode(
                Convert.ToBase64String(modified), Hash(original), out _), Is.False);
        }

        [Test]
        public void OutOfRangeSnapshot_IsRejected()
        {
            Assert.That(Parse(
                ValidJson.Replace("\"walkSpeed\":4", "\"walkSpeed\":100"), out _),
                Is.False);
        }

        [Test]
        public void MissingStat_IsRejected()
        {
            Assert.That(Parse(
                ValidJson.Replace(",\"maxStamina\":100", ""), out _), Is.False);
        }

        [Test]
        public void InvalidSchema_IsRejected()
        {
            Assert.That(Parse(ValidJson.Replace("RFB-1", "RFB-2"), out _), Is.False);
        }

        [Test]
        public void InvalidBase64_IsRejected()
        {
            Assert.That(RemoteFixedBalanceSnapshot.TryDecode(
                "not-base64!", new string('0', 64), out _), Is.False);
        }

        [Test]
        public void OversizedPayload_IsRejected()
        {
            var bytes = Encoding.UTF8.GetBytes(ValidJson + new string(' ', 8193));
            Assert.That(RemoteFixedBalanceSnapshot.TryDecode(
                Convert.ToBase64String(bytes), Hash(bytes), out _), Is.False);
        }

        private static (
            MatchAuthorityDto Context,
            LockedBalanceDto Locked,
            Guid MatchId) LockedExample()
        {
            var bytes = Encoding.UTF8.GetBytes(ValidJson);
            var hash = Hash(bytes);
            var matchId = Guid.NewGuid();

            return (
                new MatchAuthorityDto
                {
                    matchId = matchId.ToString("D"),
                    mapId = "SciFi",
                    difficulty = "NORMAL",
                    resolutionMode = "FIXED",
                    balanceSource = "REMOTE_FIXED",
                    balanceProtocolVersion = "RFB-1",
                    balanceVersion = "pr3-test-version",
                    balanceHash = hash,
                    matchDataPolicy = "STANDARD"
                },
                new LockedBalanceDto
                {
                    matchId = matchId.ToString("D"),
                    balanceVersion = "pr3-test-version",
                    balanceHash = hash,
                    balanceProtocolVersion = "RFB-1",
                    snapshotPayloadBase64 =
                        Convert.ToBase64String(bytes)
                },
                matchId
            );
        }

        [Test]
        public void LockedBalance_ValidContext_IsAccepted()
        {
            var data = LockedExample();

            Assert.That(
                RemoteFixedBalanceSnapshot.TryDecodeLocked(
                    data.Context,
                    data.Locked,
                    data.MatchId,
                    "SciFi",
                    "NORMAL",
                    out var snapshot),
                Is.True);

            Assert.That(snapshot.player.maxHealth, Is.EqualTo(100f));
        }

        [TestCase("version")]
        [TestCase("hash")]
        [TestCase("policy")]
        [TestCase("map")]
        [TestCase("difficulty")]
        [TestCase("match")]
        [TestCase("protocol")]
        [TestCase("mode")]
        public void LockedBalance_ContextMismatch_IsRejected(
            string mismatch)
        {
            var data = LockedExample();

            switch (mismatch)
            {
                case "version":
                    data.Locked.balanceVersion = "other-version";
                    break;

                case "hash":
                    data.Locked.balanceHash = new string('0', 64);
                    break;

                case "policy":
                    data.Context.matchDataPolicy = "INVALID";
                    break;

                case "map":
                    data.Context.mapId = "Scene_A";
                    break;

                case "difficulty":
                    data.Context.difficulty = "HARD";
                    break;

                case "match":
                    data.Locked.matchId = Guid.NewGuid().ToString("D");
                    break;

                case "protocol":
                    data.Locked.balanceProtocolVersion = "RFB-2";
                    break;

                case "mode":
                    data.Context.resolutionMode = "ADAPTIVE";
                    break;
            }

            Assert.That(
                RemoteFixedBalanceSnapshot.TryDecodeLocked(
                    data.Context,
                    data.Locked,
                    data.MatchId,
                    "SciFi",
                    "NORMAL",
                    out _),
                Is.False);
        }

        [Test]
        public void LockedBalance_LegacyTestOnly_IsAccepted()
        {
            var data = LockedExample();
            data.Context.matchDataPolicy = "TEST_ONLY";

            Assert.That(
                RemoteFixedBalanceSnapshot.TryDecodeLocked(
                    data.Context,
                    data.Locked,
                    data.MatchId,
                    "SciFi",
                    "NORMAL",
                    out _),
                Is.True);
        }
    }
}
