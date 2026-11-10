#nullable enable
using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace EchoProtocol.AI.Common.AED
{
    public enum AEDV02DecisionPoint { PRE_MATCH, PHASE_BOUNDARY, SAFE_WINDOW }
    public static class AEDV02PhaseMap
    {
        public static int LogicalPhase(string phase) => phase == "CORE_COLLECTION" ? 1 :
            phase == "ZONE_2_OBJECTIVE" || phase == "POWER_PUZZLE" || phase == "SECURITY_HOLD" ? 2 :
            phase == "ZONE_3_FIND_FRIGATE" || phase == "ZONE_3_PUSH_FRIGATE" ? 3 : phase == "FINAL_HUNT" || phase == "ESCAPE" ? 4 : 0;
    }
    public enum AEDV02Key
    {
        DetectionAcquireSeconds, PostChaseCooldownSeconds, HearingMultiplier, SearchSeconds,
        ChaseSpeed, SpecialCooldownSeconds, Zone1MinionCap, Zone2MinionCap,
        Zone3AdaptiveMinionCap, OptionalToolBudget, ResourceGrade, CabinetAmbushEnabled
    }

    public sealed class AEDV02Plan
    {
        public const string CatalogVersion = "AED_V02_CATALOG_REV02";
        private static readonly double[][] Values = {
            new[] { 1, 1.25, 1.5 }, new[] { 12d, 18, 28 }, new[] { .85, 1, 1.15 },
            new[] { 1.5, 2.25, 3 }, new[] { 7d, 7.5, 8 }, new[] { 360d, 420, 540 },
            new[] { 0d, 1, 2 }, new[] { 1d, 2, 3 }, new[] { -1d, 0, 1 }, new[] { -1d, 0, 1, 2, 3 },
            new[] { -1d, 0, 1, 2, 3 }, new[] { 0d, 1 }
        };
        private static readonly double[] NormalValues = { 1.25, 18, 1, 2.25, 7.5, 420, 1, 2, 0, 0, 0, 0 };
        private readonly double[] _values;
        private string? _fingerprint;
        public AEDV02Plan(double[] values)
        {
            if (values == null || values.Length != NormalValues.Length ||
                values.Where((x, i) => !Values[i].Contains(x)).Any()) throw new ArgumentException("AED_V02_PLAN_INVALID");
            _values = (double[])values.Clone();
        }
        public static AEDV02Plan Normal() => new AEDV02Plan(NormalValues);
        public const double PreMatchReliefChaseTier = 7d;

        public static bool TryResolveChaseSpeed(double patrol, double baseline, double tier, out double speed)
        {
            speed = baseline;
            double factor = tier == 7d ? .90 : tier == 7.5d ? 1 : tier == 8d ? 1.10 : 0;
            if (!AEDV02PilotPolicy.Finite(patrol) || !AEDV02PilotPolicy.Finite(baseline) ||
                patrol < .5 || patrol > 20 || baseline < patrol || baseline > 25 || factor == 0) return false;
            // Neutral preserves the exact Fixed value. Only adjusted tiers round.
            double candidate = factor == 1 ? baseline : Math.Round(baseline * factor, 2, MidpointRounding.AwayFromZero);
            if (candidate < patrol || candidate > 25 || (factor != 1 && candidate == baseline)) return false;
            speed = candidate;
            return true;
        }
        public static bool TryResolveRelative(AEDV02Key key, double patrol, double baseline, double tier, out double value)
        {
            value = baseline;
            if (!Enum.IsDefined(typeof(AEDV02Key), key) || !AEDV02PilotPolicy.Finite(baseline) ||
                !AllowedValues(key).Contains(tier)) return false;
            if (key == AEDV02Key.ChaseSpeed) return TryResolveChaseSpeed(patrol, baseline, tier, out value);
            if ((int)key >= (int)AEDV02Key.OptionalToolBudget) return false;
            (double min, double max) = key switch
            {
                AEDV02Key.DetectionAcquireSeconds => (.25, 8),
                AEDV02Key.PostChaseCooldownSeconds => (12, 28),
                AEDV02Key.HearingMultiplier => (.25, 2),
                AEDV02Key.SearchSeconds => (.25, 30),
                AEDV02Key.SpecialCooldownSeconds => (30, 900),
                AEDV02Key.Zone1MinionCap => (0, 3),
                AEDV02Key.Zone2MinionCap => (0, 5),
                AEDV02Key.Zone3AdaptiveMinionCap => (0, 4),
                _ => throw new ArgumentOutOfRangeException(nameof(key))
            };
            double neutral = NormalValues[(int)key];
            double step = Math.Sign(tier - neutral);
            if (baseline < min || baseline > max) return false;
            if (step == 0) return true;
            double candidate = (int)key < 6
                ? Math.Round(baseline * (1 + step * .10), 2, MidpointRounding.AwayFromZero)
                : baseline + step;
            if (candidate < min || candidate > max || candidate == baseline) return false;
            value = candidate;
            return true;
        }

        public static bool TryResolveOptionalMask(int baselineMask, int step, out int mask)
        {
            mask = baselineMask;
            if (baselineMask < 0 || baselineMask > 7 || step < -1 || step > 1) return false;
            if (step == 0) return true;
            for (int zone = 3; zone >= 1; zone--)
            {
                int bit = 1 << (zone - 1);
                if (((mask & bit) != 0) == (step < 0))
                { mask ^= bit; return true; }
            }
            return false;
        }

        public static int OptionalCount(int mask) => (mask & 1) + ((mask >> 1) & 1) + ((mask >> 2) & 1);
        public static int OptionalForZone(int mask, int zone) => (mask >> (zone - 1)) & 1;

        public AEDV02Plan ForRelativePreMatch(Func<AEDV02Key, double, bool> canResolve)
        {
            var values = ToArray();
            for (int index = 0; index < (int)AEDV02Key.OptionalToolBudget; index++)
                if (index >= (int)AEDV02Key.Zone1MinionCap || !canResolve((AEDV02Key)index, values[index]))
                    values[index] = NormalValues[index];
            return new AEDV02Plan(values);
        }

        public double PreMatchReliefChase(Func<AEDV02Key, double, bool> canResolve)
        {
            var normal = Normal();
            bool anotherStalkerChange = Enumerable.Range(0, 6).Any(index =>
                index != (int)AEDV02Key.ChaseSpeed && _values[index] != NormalValues[index] &&
                canResolve((AEDV02Key)index, _values[index]));
            return !anotherStalkerChange && canResolve(AEDV02Key.ChaseSpeed, PreMatchReliefChaseTier)
                ? PreMatchReliefChaseTier : normal.Get(AEDV02Key.ChaseSpeed);
        }

        public static Guid FixedFallbackDecisionId(Guid candidateDecisionId)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes("AED_V02_FIXED_FALLBACK|" + candidateDecisionId.ToString("D"))).Take(16).ToArray();
            bytes[7] = (byte)((bytes[7] & 0x0f) | 0x50);
            bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
            return new Guid(bytes);
        }
        // The legacy scenario registry used a separate Stalker tuning scale.
        // This bridge maps only the approved next legacy tier to v0.2 world units per second.
        public static bool TryMapPreMatchChaseIncrease(double legacyBaseline, double legacyCandidate, out double speed)
        {
            speed = 8d;
            return (legacyBaseline == 9d && legacyCandidate == 10d) ||
                (legacyBaseline == 7.5d && legacyCandidate == 8d);
        }

        public bool TryProposeRelief(double[][] components, double[] scores, bool recentDown, int zone, out AEDV02Plan next,
            double reliefThreshold = .65, Func<AEDV02Key, double, bool>? allowed = null, bool relative = false)
        {
            next = null!;
            if (components == null || scores == null || components.Length == 0 || components.Length != scores.Length ||
                scores.Any(x => !AEDV02PilotPolicy.Finite(x) || x < 0 || x > 1) ||
                components.Any(x => x == null || x.Length != 5 || x.Any(y => !AEDV02PilotPolicy.Finite(y) || y < 0 || y > 1)) ||
                !recentDown && scores.All(x => x < reliefThreshold)) return false;
            if (components.Any(x => x[0] > 0))
            {
                foreach (var key in new[] { AEDV02Key.ChaseSpeed, AEDV02Key.DetectionAcquireSeconds, AEDV02Key.PostChaseCooldownSeconds,
                    AEDV02Key.HearingMultiplier, AEDV02Key.SearchSeconds, AEDV02Key.SpecialCooldownSeconds })
                {
                    var value = key is AEDV02Key.DetectionAcquireSeconds or AEDV02Key.PostChaseCooldownSeconds or AEDV02Key.SpecialCooldownSeconds
                        ? Values[(int)key].Last() : Values[(int)key].First();
                    if (Get(key) != value)
                    {
                        int index = Array.IndexOf(Values[(int)key], Get(key));
                        var tier = Values[(int)key][index + Math.Sign(Array.IndexOf(Values[(int)key], value) - index)];
                        if (allowed?.Invoke(key, tier) == false) continue;
                        next = With(key, tier); return true;
                    }
                }
            }
            if (zone >= 1 && zone <= 3 && components.Any(x => x[1] > 0))
            {
                var key = zone == 1 ? AEDV02Key.Zone1MinionCap : zone == 2 ? AEDV02Key.Zone2MinionCap : AEDV02Key.Zone3AdaptiveMinionCap;
                var tiers = Values[(int)key];
                int current = Array.IndexOf(tiers, Get(key));
                if (current > 0 && (relative || tiers[current - 1] >= 0) &&
                    allowed?.Invoke(key, tiers[current - 1]) != false)
                { next = With(key, tiers[current - 1]); return true; }
            }
            return false;
        }
        public static System.Collections.Generic.IReadOnlyList<double> AllowedValues(AEDV02Key key) => Array.AsReadOnly(Values[(int)key]);
        public double Get(AEDV02Key key) => _values[(int)key];
        public bool ReservesCabinetBudget(int zone) => (zone is 1 or 2) && Get(AEDV02Key.CabinetAmbushEnabled) == 1;
        public double[] ToArray() => (double[])_values.Clone();
        public AEDV02Plan With(AEDV02Key key, double value) { var values = ToArray(); values[(int)key] = value; return new AEDV02Plan(values); }
        public string Fingerprint() => _fingerprint ??= Hash(CatalogVersion + "|" + string.Join("|", _values.Select(x => x.ToString("R", CultureInfo.InvariantCulture))));
        public int PositiveTokens(int initialOptionalBudget, bool relative = false)
        {
            int count = 0;
            for (int i = 0; i < 6; i++) if (IsAdverse((AEDV02Key)i, NormalValues[i], _values[i])) count++;
            if (Get(AEDV02Key.Zone3AdaptiveMinionCap) > 0) count++;
            if (relative && Get(AEDV02Key.Zone1MinionCap) > 1) count++;
            if (relative && Get(AEDV02Key.Zone2MinionCap) > 2) count++;
            if (Get(AEDV02Key.OptionalToolBudget) < initialOptionalBudget || Get(AEDV02Key.ResourceGrade) < 0) count++;
            return count;
        }
        public bool HasWorldPressureSlot(int initialOptionalBudget, int liveZone3Minions, bool relative = false)
        {
            if (initialOptionalBudget is < 0 or > 2 || liveZone3Minions < 0) return false;
            int tokens = PositiveTokens(initialOptionalBudget, relative);
            return tokens <= 1 && (tokens == 0 || (Get(AEDV02Key.Zone3AdaptiveMinionCap) == 1 ? liveZone3Minions <= 1 : liveZone3Minions == 0));
        }
        public bool TryValidateChange(AEDV02Plan previous, AEDV02DecisionPoint point, bool finalPhase,
            int initialOptionalBudget, out AEDV02Key key, out string reason, bool relative = false)
        {
            key = default; reason = "AED_V02_PLAN_INVALID";
            if (previous == null || initialOptionalBudget < 0 || initialOptionalBudget > 2 || !Enum.IsDefined(typeof(AEDV02DecisionPoint), point)) return false;
            if (relative ? Math.Abs(Get(AEDV02Key.OptionalToolBudget) - initialOptionalBudget) > 1 :
                Get(AEDV02Key.OptionalToolBudget) < 0 || Get(AEDV02Key.OptionalToolBudget) > initialOptionalBudget ||
                Get(AEDV02Key.Zone1MinionCap) > 1 || Get(AEDV02Key.Zone2MinionCap) > 2 || Get(AEDV02Key.Zone3AdaptiveMinionCap) < 0)
            { reason = "HOLD_INITIAL_OPTIONAL_BUDGET"; return false; }
            var changed = Enumerable.Range(0, _values.Length).Where(i => _values[i] != previous._values[i]).ToArray();
            if (point != AEDV02DecisionPoint.PRE_MATCH && changed.Length != 1) { reason = "HOLD_SINGLE_DELTA_REQUIRED"; return false; }
            if (point != AEDV02DecisionPoint.PRE_MATCH && changed.Any(i => Math.Abs(Array.IndexOf(Values[i], _values[i]) - Array.IndexOf(Values[i], previous._values[i])) > 1))
            { reason = "HOLD_SINGLE_TIER_REQUIRED"; return false; }
            if (point == AEDV02DecisionPoint.PRE_MATCH && changed.Count(i => i < 6) > 1) { reason = "HOLD_STALKER_COMPOSITE_BUDGET"; return false; }
            if (PositiveTokens(initialOptionalBudget, relative) > 1) { reason = "HOLD_GLOBAL_PRESSURE_BUDGET"; return false; }
            if (point == AEDV02DecisionPoint.PRE_MATCH && Get(AEDV02Key.Zone3AdaptiveMinionCap) != 0) { reason = "HOLD_ZONE3_OBSERVATION_REQUIRED"; return false; }
            if (finalPhase && changed.Any(i => IsAdverse((AEDV02Key)i, previous._values[i], _values[i]))) { reason = "HOLD_PHASE4_INCREASE"; return false; }
            if (changed.Length != 0) key = (AEDV02Key)changed[0];
            reason = string.Empty; return true;
        }
        public static bool IsAdverse(AEDV02Key key, double before, double after) =>
            key == AEDV02Key.DetectionAcquireSeconds || key == AEDV02Key.PostChaseCooldownSeconds || key == AEDV02Key.SpecialCooldownSeconds || key == AEDV02Key.OptionalToolBudget
                ? after < before : key == AEDV02Key.ResourceGrade ? after < before : after > before;
        public static string WindowId(Guid matchId, int phase, AEDV02DecisionPoint point, int sequence,
            long startTick, long endTick, string sourceFingerprint) => Hash(string.Join("|", matchId.ToString("D"),
                phase.ToString(CultureInfo.InvariantCulture), point, sequence.ToString(CultureInfo.InvariantCulture),
                startTick.ToString(CultureInfo.InvariantCulture), endTick.ToString(CultureInfo.InvariantCulture), sourceFingerprint));
        public static string Hash(string value)
        {
            using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(x => x.ToString("x2", CultureInfo.InvariantCulture)));
        }
    }
}
