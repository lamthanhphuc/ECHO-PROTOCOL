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
            new[] { 0d, 1 }, new[] { 1d, 2 }, new[] { 0d, 1 }, new[] { 0d, 1, 2 },
            new[] { -1d, 0, 1, 2, 3 }, new[] { 0d, 1 }
        };
        private static readonly double[] NormalValues = { 1.25, 18, 1, 2.25, 7.5, 420, 1, 2, 0, 0, 0, 0 };
        private readonly double[] _values;
        public AEDV02Plan(double[] values)
        {
            if (values == null || values.Length != NormalValues.Length ||
                values.Where((x, i) => !Values[i].Contains(x)).Any()) throw new ArgumentException("AED_V02_PLAN_INVALID");
            _values = (double[])values.Clone();
        }
        public static AEDV02Plan Normal() => new AEDV02Plan(NormalValues);
        public bool TryProposeRelief(double[][] components, double[] scores, bool recentDown, int zone, out AEDV02Plan next)
        {
            next = null!;
            if (components == null || scores == null || components.Length == 0 || components.Length != scores.Length ||
                scores.Any(x => !AEDV02PilotPolicy.Finite(x) || x < 0 || x > 1) ||
                components.Any(x => x == null || x.Length != 5 || x.Any(y => !AEDV02PilotPolicy.Finite(y) || y < 0 || y > 1)) ||
                !recentDown && scores.All(x => x < .65)) return false;
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
                        next = With(key, Values[(int)key][index + Math.Sign(Array.IndexOf(Values[(int)key], value) - index)]); return true;
                    }
                }
            }
            if (zone >= 1 && zone <= 3 && components.Any(x => x[1] > 0))
            {
                var key = zone == 1 ? AEDV02Key.Zone1MinionCap : zone == 2 ? AEDV02Key.Zone2MinionCap : AEDV02Key.Zone3AdaptiveMinionCap;
                if (Get(key) != Values[(int)key][0]) { next = With(key, Values[(int)key][0]); return true; }
            }
            return false;
        }
        public static System.Collections.Generic.IReadOnlyList<double> AllowedValues(AEDV02Key key) => Array.AsReadOnly(Values[(int)key]);
        public double Get(AEDV02Key key) => _values[(int)key];
        public double[] ToArray() => (double[])_values.Clone();
        public AEDV02Plan With(AEDV02Key key, double value) { var values = ToArray(); values[(int)key] = value; return new AEDV02Plan(values); }
        public string Fingerprint() => Hash(CatalogVersion + "|" + string.Join("|", _values.Select(x => x.ToString("R", CultureInfo.InvariantCulture))));
        public int PositiveTokens(int initialOptionalBudget)
        {
            int count = 0;
            for (int i = 0; i < 6; i++) if (IsAdverse((AEDV02Key)i, NormalValues[i], _values[i])) count++;
            if (Get(AEDV02Key.Zone3AdaptiveMinionCap) > 0) count++;
            if (Get(AEDV02Key.OptionalToolBudget) < initialOptionalBudget || Get(AEDV02Key.ResourceGrade) < 0) count++;
            return count;
        }
        public bool TryValidateChange(AEDV02Plan previous, AEDV02DecisionPoint point, bool finalPhase,
            int initialOptionalBudget, out AEDV02Key key, out string reason)
        {
            key = default; reason = "AED_V02_PLAN_INVALID";
            if (previous == null || initialOptionalBudget < 0 || initialOptionalBudget > 2 || !Enum.IsDefined(typeof(AEDV02DecisionPoint), point)) return false;
            var changed = Enumerable.Range(0, _values.Length).Where(i => _values[i] != previous._values[i]).ToArray();
            if (point != AEDV02DecisionPoint.PRE_MATCH && changed.Length != 1) { reason = "HOLD_SINGLE_DELTA_REQUIRED"; return false; }
            if (point != AEDV02DecisionPoint.PRE_MATCH && changed.Any(i => Math.Abs(Array.IndexOf(Values[i], _values[i]) - Array.IndexOf(Values[i], previous._values[i])) > 1))
            { reason = "HOLD_SINGLE_TIER_REQUIRED"; return false; }
            if (point == AEDV02DecisionPoint.PRE_MATCH && changed.Count(i => i < 6) > 1) { reason = "HOLD_STALKER_COMPOSITE_BUDGET"; return false; }
            if (PositiveTokens(initialOptionalBudget) > 1) { reason = "HOLD_GLOBAL_PRESSURE_BUDGET"; return false; }
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
