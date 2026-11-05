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
    }
}
