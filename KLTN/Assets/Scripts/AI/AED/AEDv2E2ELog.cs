using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using EchoProtocol.Networking;
using EchoProtocol.Networking.Authority;
#endif

namespace EchoProtocol.AI.AED
{
    /// <summary>
    /// Diagnostic-only, single-line events for local AED v2 E2E verification.
    /// Do not write credentials, request bodies, account IDs or raw player data here.
    /// Calls are removed from non-development builds.
    /// </summary>
    public static class AEDv2E2ELog
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly string SessionId = Guid.NewGuid().ToString("N");
        private static int _sequence;
#endif

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Write(string eventName, Guid matchId = default,
            Guid decisionId = default, string role = "UNKNOWN", string fields = "")
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var sequence = Interlocked.Increment(ref _sequence);
            var stamp = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            var match = matchId == Guid.Empty ? "none" : matchId.ToString("D");
            var decision = decisionId == Guid.Empty ? "none" : decisionId.ToString("D");
            UnityEngine.Debug.Log($"[AED_E2E] utc={stamp} session={SessionId} " +
                $"seq={sequence} role={Clean(role)} event={Clean(eventName)} " +
                $"match={match} decision={decision} {Clean(fields)}");
#endif
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void State(string eventName, string fields = "", Guid decisionId = default)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var state = NetworkMatchState.Instance;
            var authority = MatchAuthorityRuntime.Instance;
            var spawned = state != null && state.Object != null;
            var role = !spawned ? "UNSPAWNED" :
                state.Object.HasStateAuthority ? "HOST" : "CLIENT";
            var snapshot = spawned
                ? $"phase={state.CurrentPhase} ordinal={state.PhaseOrdinal} " +
                  $"networkRevision={state.AEDv2PlanRevision} " +
                  $"networkFingerprint={state.AEDv2PlanFingerprint} "
                : "phase=none ordinal=0 networkRevision=0 networkFingerprint=none ";
            Write(eventName, authority != null ? authority.MatchId : Guid.Empty,
                decisionId, role, snapshot + fields);
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static string Clean(string value)
        {
            return (value ?? string.Empty).Replace('\r', '_').Replace('\n', '_')
                .Replace('\t', '_').Replace('|', '/');
        }
#endif
    }

}
