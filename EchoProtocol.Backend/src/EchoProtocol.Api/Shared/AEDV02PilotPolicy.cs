#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace EchoProtocol.AI.Common.AED
{
    public static class AEDV02PilotPolicy
    {
        public const string Version = "AED_STALKER_POLICY_V0_2_REV02_PILOT_DEFAULTS_2";
        public const double ObservationSeconds = 30;
        public const double WindowSeconds = 120;
        public const double EvidenceMaxAgeSeconds = 10;
        public const double DecayHalfLifeSeconds = 90;
        public const double TierDwellSeconds = 12;
        public const double TierHysteresis = .05;
        public const double ProtectionSeconds = 10;
        public const double ProtectionCooldownSeconds = 120;
        public const int ProtectionQuota = 2;
        public const double ProtectionGraceSeconds = 3;
        public const double DownRecoverySeconds = 20;
        public const double SeekRecoverySeconds = 15;
        public const double SensoryRefreshSeconds = 5;
        public const double AmbushWarningSeconds = 2.5;
        public const double AmbushCooldownSeconds = 420;
        public const double SharedSpecialSeparationSeconds = 300;
        public const double AmbushClueAgeSeconds = 12;
        public const double AmbushApproachSeconds = 12;
        public const double AmbushBackoffSeconds = 90;
        public const double AmbushRecoverySeconds = 25;

        public static int Tier(double score) => score >= .85 ? 4 : score >= .65 ? 3 : score >= .45 ? 2 : score >= .25 ? 1 : 0;
        public static double Sensory(int tier) => tier == 0 ? 1.08 : tier == 1 ? 1.04 : tier == 3 ? .92 : tier == 4 ? .85 : 1;
        public static double TargetAdjustment(int tier) => tier == 0 ? .15 : tier == 1 ? .08 : tier == 3 ? -.15 : tier == 4 ? -.25 : 0;
        public static double RevealTrackSeconds(int tier) => tier == 0 ? .45 : tier == 1 ? .40 : tier == 3 ? .25 : tier == 4 ? .20 : .35;
        public static double RevealGraceSeconds(int tier) => tier == 0 ? 1.25 : tier == 1 ? 1.35 : tier == 3 ? 1.80 : tier == 4 ? 2.20 : 1.50;
        public static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public enum AEDV02PressureChannel { Stalker, Minion, Down, Resource, Objective }

    // These are source facts, not precomputed scores or client verification flags.
    public sealed class AEDV02PressureFact
    {
        public Guid MatchId { get; set; }
        public Guid UserId { get; set; }
        public string EventId { get; set; } = string.Empty;
        public string EpisodeId { get; set; } = string.Empty;
        public AEDV02PressureChannel Channel { get; set; }
        public double StartedSeconds { get; set; }
        public double EndedSeconds { get; set; }
        public string? CauseEventId { get; set; }
    }

    public readonly struct AEDV02PressureSnapshot
    {
        public AEDV02PressureSnapshot(double[] components, double score, bool complete, string reason)
        { Components = Array.AsReadOnly((double[])components.Clone()); Score = score; Complete = complete; Reason = reason; }
        public IReadOnlyList<double> Components { get; }
        public double Score { get; }
        public bool Complete { get; }
        public int Tier => Complete ? AEDV02PilotPolicy.Tier(Score) : -1;
        public string Reason { get; }
    }

    public static class AEDV02PressureNormalizer
    {
        public static IReadOnlyList<double> DefaultWeights { get; } = Array.AsReadOnly(new[] { .30, .20, .25, .15, .10 });
        private static readonly double[] Denominators = { 60, 45, 2, 2, 60 };

        public static AEDV02PressureSnapshot Project(Guid matchId, Guid userId, double now,
            double observedFrom, IReadOnlyList<double>? sourceWatermarks, IEnumerable<AEDV02PressureFact> facts,
            double observationSeconds = AEDV02PilotPolicy.ObservationSeconds,
            double windowSeconds = AEDV02PilotPolicy.WindowSeconds,
            double evidenceMaxAgeSeconds = AEDV02PilotPolicy.EvidenceMaxAgeSeconds,
            IReadOnlyList<double>? weights = null)
        {
            var components = new double[5];
            if (matchId == Guid.Empty || userId == Guid.Empty || !AEDV02PilotPolicy.Finite(now) || now < 0 ||
                !AEDV02PilotPolicy.Finite(observedFrom) || observedFrom < 0 || observedFrom > now || facts == null)
                return new AEDV02PressureSnapshot(components, 0, false, "INVALID_PRESSURE_SOURCE");
            var unique = new Dictionary<string, AEDV02PressureFact>(StringComparer.Ordinal);
            foreach (var fact in facts)
            {
                if (fact == null || fact.MatchId != matchId || fact.UserId != userId ||
                    string.IsNullOrWhiteSpace(fact.EventId) || string.IsNullOrWhiteSpace(fact.EpisodeId) ||
                    !Enum.IsDefined(typeof(AEDV02PressureChannel), fact.Channel) ||
                    !AEDV02PilotPolicy.Finite(fact.StartedSeconds) || !AEDV02PilotPolicy.Finite(fact.EndedSeconds) ||
                    fact.StartedSeconds < 0 || fact.EndedSeconds < fact.StartedSeconds || fact.EndedSeconds > now ||
                    (fact.Channel == AEDV02PressureChannel.Objective && string.IsNullOrWhiteSpace(fact.CauseEventId)))
                    return new AEDV02PressureSnapshot(components, 0, false, "INVALID_PRESSURE_SOURCE");
                if (unique.TryGetValue(fact.EventId, out var previous))
                {
                    if (previous.EpisodeId != fact.EpisodeId || previous.Channel != fact.Channel ||
                        previous.StartedSeconds != fact.StartedSeconds || previous.EndedSeconds != fact.EndedSeconds ||
                        previous.CauseEventId != fact.CauseEventId)
                        return new AEDV02PressureSnapshot(components, 0, false, "CONFLICTING_PRESSURE_EVENT");
                    continue;
                }
                unique.Add(fact.EventId, fact);
            }
            foreach (var group in unique.Values.Where(x => x.EndedSeconds >= now - windowSeconds)
                .GroupBy(x => (x.Channel, x.EpisodeId)))
            {
                var channel = (int)group.Key.Channel;
                // Merge exposure intervals in one episode; overlapping callbacks do not multiply duration.
                double amount = 0, end = now - windowSeconds;
                foreach (var fact in group.OrderBy(x => x.StartedSeconds))
                {
                    var decay = Math.Pow(.5, (now - fact.EndedSeconds) / AEDV02PilotPolicy.DecayHalfLifeSeconds);
                    if (channel == 2 || channel == 3) { amount = Math.Max(amount, decay); continue; }
                    var start = Math.Max(end, Math.Max(fact.StartedSeconds, now - windowSeconds));
                    amount += Math.Max(0, fact.EndedSeconds - start) * decay;
                    end = Math.Max(end, fact.EndedSeconds);
                }
                components[channel] += amount / Denominators[channel];
            }
            double score = 0;
            for (int i = 0; i < 5; i++) { components[i] = Math.Min(1, components[i]); score += components[i] * (weights ?? DefaultWeights)[i]; }
            var complete = sourceWatermarks != null && sourceWatermarks.Count == 5 &&
                sourceWatermarks.All(x => AEDV02PilotPolicy.Finite(x) && x >= observedFrom && x <= now &&
                    now - x <= evidenceMaxAgeSeconds) && now - observedFrom >= observationSeconds;
            return new AEDV02PressureSnapshot(components, Math.Min(1, score), complete,
                complete ? "PRESSURE_SOURCE_COMPLETE" : "HOLD_INSUFFICIENT_EVIDENCE");
        }
    }

    public sealed class AEDV02PressureTierMemory
    {
        private int _tier = -1;
        private double _lowerSince = double.NaN;
        private double _lastNow = -1;
        public int Update(AEDV02PressureSnapshot snapshot, double now,
            double dwellSeconds = AEDV02PilotPolicy.TierDwellSeconds,
            double evidenceMaxAgeSeconds = AEDV02PilotPolicy.EvidenceMaxAgeSeconds)
        {
            if (!snapshot.Complete || !AEDV02PilotPolicy.Finite(now) || now < _lastNow) { _lowerSince = double.NaN; return -1; }
            if (now - _lastNow > evidenceMaxAgeSeconds) _lowerSince = double.NaN;
            _lastNow = now;
            int proposed = snapshot.Tier;
            if (_tier < 0 || proposed >= _tier) { _tier = proposed; _lowerSince = double.NaN; return _tier; }
            double threshold = _tier == 4 ? .85 : _tier == 3 ? .65 : _tier == 2 ? .45 : .25;
            if (snapshot.Score >= threshold - AEDV02PilotPolicy.TierHysteresis) { _lowerSince = double.NaN; return _tier; }
            if (double.IsNaN(_lowerSince)) _lowerSince = now;
            if (now - _lowerSince >= dwellSeconds) { _tier = proposed; _lowerSince = double.NaN; }
            return _tier;
        }
        public void InvalidateObservation() => _lowerSince = double.NaN;
    }

    public sealed class AEDV02HideProtectionToken
    {
        public Guid TokenId { get; internal set; }
        public Guid MatchId { get; internal set; }
        public Guid UserId { get; internal set; }
        public ulong HideSpotId { get; internal set; }
        public int PlanRevision { get; internal set; }
        public int GrantOrdinal { get; internal set; }
        public double GrantedSeconds { get; internal set; }
        public double ExpiresSeconds { get; internal set; }
        public double GraceUntilSeconds { get; internal set; }
        public bool VoluntaryExit { get; internal set; }
    }

    public sealed class AEDV02HideProtectionLedger
    {
        private sealed class PlayerState
        {
            public int Grants;
            public double LastGrant = double.NegativeInfinity;
            public readonly HashSet<string> Encounters = new HashSet<string>(StringComparer.Ordinal);
            public AEDV02HideProtectionToken? Token;
        }
        private readonly Dictionary<Guid, PlayerState> _players = new Dictionary<Guid, PlayerState>();
        public Guid MatchId { get; private set; }
        public void Reset(Guid matchId) { _players.Clear(); MatchId = matchId; }

        public bool TryGrant(Guid matchId, Guid userId, ulong spotId, string encounterId, int revision,
            double now, bool approvedPolicy, AEDV02PressureSnapshot pressure, out AEDV02HideProtectionToken? token,
            double protectionSeconds = AEDV02PilotPolicy.ProtectionSeconds,
            double protectionCooldownSeconds = AEDV02PilotPolicy.ProtectionCooldownSeconds,
            int protectionQuota = AEDV02PilotPolicy.ProtectionQuota)
        {
            token = null;
            if (matchId == Guid.Empty || matchId != MatchId || userId == Guid.Empty || spotId == 0 ||
                string.IsNullOrWhiteSpace(encounterId) || revision < 1 || !AEDV02PilotPolicy.Finite(now) || now < 0 ||
                !approvedPolicy || !pressure.Complete || pressure.Tier != 4) return false;
            if (!_players.TryGetValue(userId, out var state)) _players.Add(userId, state = new PlayerState());
            if (state.Token != null && state.Token.HideSpotId == spotId && !state.Token.VoluntaryExit && now < state.Token.ExpiresSeconds)
            { token = state.Token; return true; }
            if (state.Grants >= protectionQuota ||
                now - state.LastGrant < protectionCooldownSeconds || !state.Encounters.Add(encounterId)) return false;
            state.Grants++; state.LastGrant = now;
            token = state.Token = new AEDV02HideProtectionToken
            {
                TokenId = Guid.NewGuid(), MatchId = matchId, UserId = userId, HideSpotId = spotId,
                PlanRevision = revision, GrantOrdinal = state.Grants, GrantedSeconds = now, ExpiresSeconds = now + protectionSeconds,
                GraceUntilSeconds = now + protectionSeconds + AEDV02PilotPolicy.ProtectionGraceSeconds
            };
            return true;
        }

        public bool BlocksForceExit(Guid matchId, Guid userId, ulong spotId, double now, out string reason)
        {
            reason = string.Empty;
            if (matchId != MatchId || !_players.TryGetValue(userId, out var state) || state.Token == null ||
                state.Token.HideSpotId != spotId || !AEDV02PilotPolicy.Finite(now) || now < state.Token.GrantedSeconds) return false;
            if (now < state.Token.ExpiresSeconds && !state.Token.VoluntaryExit) { reason = "PROTECT_P4_HIDE_WINDOW"; return true; }
            if (now < state.Token.GraceUntilSeconds) { reason = "GRACE_AFTER_PROTECTION"; return true; }
            return false;
        }

        public bool TryGetCurrentToken(Guid matchId, Guid userId, ulong spotId, double now, out AEDV02HideProtectionToken? token)
        {
            token = null;
            if (!BlocksForceExit(matchId, userId, spotId, now, out _) || !_players.TryGetValue(userId, out var state)) return false;
            token = state.Token; return token != null;
        }

        public void ExitToken(Guid tokenId, ulong spotId, double now)
        {
            foreach (var item in _players)
                if (item.Value.Token?.TokenId == tokenId) { Exit(item.Key, spotId, now); return; }
        }

        public void Exit(Guid userId, ulong spotId, double now)
        {
            if (!_players.TryGetValue(userId, out var state) || state.Token == null || state.Token.HideSpotId != spotId ||
                !AEDV02PilotPolicy.Finite(now) || now < state.Token.GrantedSeconds || state.Token.VoluntaryExit) return;
            state.Token.VoluntaryExit = true;
            state.Token.ExpiresSeconds = Math.Min(state.Token.ExpiresSeconds, now);
            state.Token.GraceUntilSeconds = now + AEDV02PilotPolicy.ProtectionGraceSeconds;
        }
    }
}
