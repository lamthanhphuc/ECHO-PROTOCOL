using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace EchoProtocol.Networking.Authority
{
    [Serializable]
    public sealed class RemoteFixedBalanceSnapshot
    {
        public string schemaVersion;
        public PlayerStats player;
        public StalkerStats stalker;
        public MinionStats minion;

        [Serializable]
        public sealed class PlayerStats
        {
            public float maxHealth;
            public float walkSpeed;
            public float sprintSpeed;
            public float maxStamina;
        }

        [Serializable]
        public sealed class StalkerStats
        {
            public float patrolSpeed;
            public float chaseSpeed;
        }

        [Serializable]
        public sealed class MinionStats
        {
            public float roamSpeed;
            public float trackSpeed;
            public float attackDamage;
        }

        public static bool TryDecodeLocked(
            MatchAuthorityDto context,
            LockedBalanceDto locked,
            Guid expectedMatchId,
            string expectedMap,
            string expectedDifficulty,
            out RemoteFixedBalanceSnapshot snapshot)
        {
            snapshot = null;

            return context != null
                && locked != null
                && Guid.TryParse(context.matchId, out var contextId)
                && contextId == expectedMatchId
                && Guid.TryParse(locked.matchId, out var lockedId)
                && lockedId == expectedMatchId
                && context.mapId == expectedMap
                && context.difficulty == expectedDifficulty
                && context.resolutionMode == "FIXED"
                && context.balanceSource == "REMOTE_FIXED"
                && context.balanceProtocolVersion == "RFB-1"
                && (context.matchDataPolicy == "STANDARD"
                    || context.matchDataPolicy == "TEST_ONLY")
                && !string.IsNullOrWhiteSpace(context.balanceVersion)
                && !string.IsNullOrWhiteSpace(context.balanceHash)
                && context.balanceVersion == locked.balanceVersion
                && context.balanceHash == locked.balanceHash
                && locked.balanceProtocolVersion == "RFB-1"
                && TryDecode(
                    locked.snapshotPayloadBase64,
                    context.balanceHash,
                    out snapshot);
        }

        public static bool TryDecode(
            string payloadBase64,
            string expectedHash,
            out RemoteFixedBalanceSnapshot snapshot)
        {
            snapshot = null;
            if (string.IsNullOrWhiteSpace(payloadBase64) ||
                string.IsNullOrWhiteSpace(expectedHash))
                return false;

            try
            {
                var bytes = Convert.FromBase64String(payloadBase64);
                if (bytes.Length == 0 || bytes.Length > 8192)
                    return false;

                using (var sha = SHA256.Create())
                {
                    var actualHash = BitConverter.ToString(sha.ComputeHash(bytes))
                        .Replace("-", "").ToLowerInvariant();
                    if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
                        return false;
                }

                var json = new UTF8Encoding(false, true).GetString(bytes);
                var value = JsonUtility.FromJson<RemoteFixedBalanceSnapshot>(json);
                if (value == null || value.schemaVersion != "RFB-1" ||
                    value.player == null || value.stalker == null || value.minion == null)
                    return false;

                var p = value.player;
                var s = value.stalker;
                var m = value.minion;
                if (!InRange(p.maxHealth, 1f, 500f) ||
                    !InRange(p.walkSpeed, 0.5f, 15f) ||
                    !InRange(p.sprintSpeed, 0.5f, 25f) ||
                    !InRange(p.maxStamina, 1f, 500f) ||
                    !InRange(s.patrolSpeed, 0.5f, 20f) ||
                    !InRange(s.chaseSpeed, 0.5f, 25f) ||
                    !InRange(m.roamSpeed, 0.5f, 20f) ||
                    !InRange(m.trackSpeed, 0.5f, 25f) ||
                    !InRange(m.attackDamage, 1f, 100f) ||
                    p.sprintSpeed < p.walkSpeed || s.chaseSpeed < s.patrolSpeed)
                    return false;

                snapshot = value;
                return true;
            }
            catch (Exception exception) when (
                exception is FormatException ||
                exception is DecoderFallbackException ||
                exception is ArgumentException)
            {
                return false;
            }
        }

        private static bool InRange(float value, float minimum, float maximum) =>
            !float.IsNaN(value) && !float.IsInfinity(value) &&
            value >= minimum && value <= maximum;
    }
}
