#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace EchoProtocol.AI.Common.AED
{
    // Pilot-only defaults authorized 2026-10-15; production calibration is a separate release decision.
    public static class AEDV02PilotOpportunities
    {
        public const string Version = "AED_V02_PILOT_OPPORTUNITIES_2";
        public const double MinimumExposureSeconds = 5;
        public const double SafeResolutionSeconds = 10;
        public const double LongChaseSeconds = 20;
        public const double ResourceLossNeedSeconds = 30;
        public const double ScarcityMinimumSeconds = 5;
        public const int HistoryMatches = 3;
        public const int HistoryResolved = 8;
        public const double HistoryMaxAgeDays = 30;
        public const double HistoryHalfLifeDays = 14;
        public const double WeakestCompetence = .65;
        public const int OrdinarySupportQuota = 2;
        public const int EmergencySupportQuota = 1;
        public const double SupportSeparationSeconds = 90;
        public const double Zone3EntryBufferSeconds = 30;
        public const double NoiseProjectionRadius = 1.5;
        public const double NoiseDiversionSeconds = 2;
        public const double NoiseDiversionMeters = .5;
        public const double StabilizerEffectSeconds = 2;
        public const double JammerDelaySeconds = 1;
        public const double ScannerCensusPositionTolerance = 1.5;
        public const double ToolThreatContextSeconds = 30;

        public static string ComparisonContext(string scene, int rosterSize) => AEDV02Plan.Hash(
            Version + "|" + AEDV02PilotPolicy.Version + "|Adaptive|Normal|" + scene + "|" + rosterSize);

        public static AEDV02TeamPressure TeamPressure(IReadOnlyList<AEDV02PressureSnapshot> players,
            int downOrEliminated, int simultaneouslyTargeted)
        {
            if (players == null || players.Count < 1 || players.Count > 4 || players.Any(x => !x.Complete ||
                !AEDV02PilotPolicy.Finite(x.Score) || x.Score < 0 || x.Score > 1 || x.Tier < 0 || x.Tier > 4 ||
                x.Components == null || x.Components.Count != 5 || x.Components.Any(c => !AEDV02PilotPolicy.Finite(c) || c < 0 || c > 1)) ||
                downOrEliminated < 0 || downOrEliminated > players.Count || simultaneouslyTargeted < 0 || simultaneouslyTargeted > players.Count)
                return new AEDV02TeamPressure(false, 0, -1);
            double worst = players.Max(x => x.Score), mean = players.Average(x => x.Score);
            double severity = Math.Max(.6 * worst + .4 * mean, Math.Max(downOrEliminated / (double)players.Count,
                Math.Max(players.Average(x => x.Components[4]), simultaneouslyTargeted / (double)players.Count)));
            if (players.Any(x => x.Tier >= 3)) severity = Math.Max(.65, severity);
            severity = Math.Min(1, Math.Max(0, severity));
            return new AEDV02TeamPressure(true, severity, 4 - AEDV02PilotPolicy.Tier(severity));
        }

        public static AEDV02HistoricalCompetence Competence(Guid user, string axis, string context, DateTime now,
            IEnumerable<AEDV02HistoricalOpportunity> observations)
        {
            if (user == Guid.Empty || axis is not ("Stalker" or "Minion" or "Objective" or "Tool" or "Teamwork" or "ResourceManagement") ||
                !AEDV02WorldSample.Hash(context) || observations == null)
                return new AEDV02HistoricalCompetence(null, 0, 0, 0, "Invalid");
            var all = observations.ToArray();
            if (all.Any(x => x == null || x.Opportunity == null || x.MatchId == Guid.Empty))
                return new AEDV02HistoricalCompetence(null, 0, 0, 0, "Invalid");
            var source = all.Where(x => x.Opportunity.UserId == user && x.Opportunity.Axis == axis && x.Context == context &&
                x.ContractVersion == Version && x.EndedAtUtc <= now && (now - x.EndedAtUtc).TotalDays <= HistoryMaxAgeDays).ToArray();
            if (source.Any(x => !x.Opportunity.IsValid() || !AEDV02WorldSample.Hash(x.SourceFingerprint)))
                return new AEDV02HistoricalCompetence(null, 0, 0, 0, "Invalid");
            var groups = source.GroupBy(x => (x.MatchId, x.Opportunity.Id)).ToArray();
            if (groups.Any(g => g.Where(x => x.Opportunity.Resolution != "Censored").Select(x => x.Opportunity.Resolution).Distinct().Count() > 1))
                return new AEDV02HistoricalCompetence(null, 0, 0, 0, "Invalid");
            var unique = groups.Select(g => g.OrderBy(x => x.Opportunity.Resolution == "Censored").ThenByDescending(x => x.EndedAtUtc).First()).ToArray();
            var resolved = unique.Where(x => x.Opportunity.Resolution != "Censored").ToArray();
            int matches = resolved.Select(x => x.MatchId).Distinct().Count(), censored = unique.Length - resolved.Length;
            double? score = MatchBalancedScore(resolved, now);
            string confidence = unique.Length == 0 ? "ColdStart" : matches >= HistoryMatches && resolved.Length >= HistoryResolved &&
                censored * 4 <= unique.Length ? "Sufficient" : "Insufficient";
            if (axis == "Tool" && unique.Length != 0)
            {
                double minimum = 1;
                foreach (int type in new[] { 1, 2, 3, 4, 6 })
                {
                    var family = unique.Where(x => x.Opportunity.ToolId == type).ToArray();
                    var successesAndFailures = family.Where(x => x.Opportunity.Resolution != "Censored").ToArray();
                    var familyScore = MatchBalancedScore(successesAndFailures, now);
                    if (!familyScore.HasValue || successesAndFailures.Length < HistoryResolved ||
                        successesAndFailures.Select(x => x.MatchId).Distinct().Count() < HistoryMatches ||
                        (family.Length - successesAndFailures.Length) * 4 > family.Length)
                    { score = null; confidence = "Insufficient"; break; }
                    minimum = Math.Min(minimum, familyScore.Value);
                    score = minimum;
                }
            }
            return new AEDV02HistoricalCompetence(score, matches, resolved.Length, censored, confidence);
        }

        private static double? MatchBalancedScore(IEnumerable<AEDV02HistoricalOpportunity> resolved, DateTime now)
        {
            double weighted = 0, weights = 0;
            foreach (var match in resolved.GroupBy(x => x.MatchId))
            {
                double weight = Math.Pow(.5, (now - match.Max(x => x.EndedAtUtc)).TotalDays / HistoryHalfLifeDays);
                weighted += match.Average(x => x.Opportunity.Resolution == "Success" ? 1d : 0d) * weight;
                weights += weight;
            }
            return weights == 0 ? (double?)null : weighted / weights;
        }
    }

    public readonly struct AEDV02TeamPressure
    {
        public AEDV02TeamPressure(bool complete, double severity, int tier) { Complete = complete; Severity = severity; Tier = tier; }
        public bool Complete { get; }
        public double Severity { get; }
        public int Tier { get; }
    }

    public sealed class AEDV02CausalImpact
    {
        public string Version { get; set; } = AEDV02PilotOpportunities.Version;
        public Guid UserId { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string ObjectiveUnitId { get; set; } = string.Empty;
        public string ToolInstanceId { get; set; } = string.Empty;
        public int ToolId { get; set; }
        public long Tick { get; set; }
        public long UntilTick { get; set; }
        public bool IsValid() => Version == AEDV02PilotOpportunities.Version && UserId != Guid.Empty &&
            Kind is "STALKER_DOWN" or "MINION_SLOW" or "MINION_CORE_DROP" or "MINION_TOOL_LOSS" &&
            !string.IsNullOrWhiteSpace(EntityId) && EntityId.Length <= 128 && ObjectiveUnitId != null && ObjectiveUnitId.Length <= 128 &&
            ToolInstanceId != null && ToolInstanceId.Length <= 128 && Tick >= 0 && UntilTick >= Tick &&
            (Kind == "MINION_TOOL_LOSS" ? ToolId is 1 or 2 or 3 or 4 or 6 && ToolInstanceId.Length != 0 : ToolId == 0);
    }

    public sealed class AEDV02ResourceManagementAction
    {
        public string Version { get; set; } = AEDV02PilotOpportunities.Version;
        public Guid CauseEventId { get; set; }
        public Guid ActorUserId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string ToolInstanceId { get; set; } = string.Empty;
        public string LifecycleOccurrenceKey { get; set; } = string.Empty;
        public int ToolId { get; set; }
        public long Tick { get; set; }

        public bool IsValid() => Version == AEDV02PilotOpportunities.Version &&
            CauseEventId != Guid.Empty && ActorUserId != Guid.Empty &&
            Action is "PICKUP" or "USE" or "CONSUME" or "DROP" &&
            !string.IsNullOrWhiteSpace(ToolInstanceId) && ToolInstanceId.Length <= 128 &&
            !string.IsNullOrWhiteSpace(LifecycleOccurrenceKey) && LifecycleOccurrenceKey.Length <= 256 &&
            ToolId is 1 or 2 or 3 or 4 or 6 && Tick >= 0;
    }

    public sealed class AEDV02LongChaseRecovery
    {
        private double _started = double.NaN;
        private double _last = -1;
        public double SeekPausedUntil { get; private set; }
        public void Reset() { _started = double.NaN; _last = -1; SeekPausedUntil = 0; }
        public void Observe(bool chasing, double seconds)
        {
            if (!AEDV02PilotPolicy.Finite(seconds) || seconds < _last) { Reset(); return; }
            if (chasing) { if (double.IsNaN(_started)) _started = seconds; }
            else if (!double.IsNaN(_started))
            {
                if (seconds - _started >= AEDV02PilotOpportunities.LongChaseSeconds)
                    SeekPausedUntil = Math.Max(SeekPausedUntil, seconds + AEDV02PilotPolicy.SeekRecoverySeconds);
                _started = double.NaN;
            }
            _last = seconds;
        }
    }

    public sealed class AEDV02ResolvedOpportunity
    {
        public string Id { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string Axis { get; set; } = string.Empty;
        public int ToolId { get; set; }
        public string Resolution { get; set; } = string.Empty;
        public long StartTick { get; set; }
        public long EndTick { get; set; }
        public bool IsValid() => !string.IsNullOrWhiteSpace(Id) && Id.Length <= 256 && UserId != Guid.Empty &&
            Axis is "Stalker" or "Minion" or "Objective" or "Tool" or "Teamwork" or "ResourceManagement" &&
            (Axis == "Tool" ? ToolId is 1 or 2 or 3 or 4 or 6 : ToolId == 0) &&
            Resolution is "Success" or "Failure" or "Censored" && StartTick >= 0 && EndTick > StartTick;
    }

    public sealed class AEDV02HistoricalOpportunity
    {
        public Guid MatchId { get; set; }
        public string Context { get; set; } = string.Empty;
        public string ContractVersion { get; set; } = string.Empty;
        public string SourceFingerprint { get; set; } = string.Empty;
        public DateTime EndedAtUtc { get; set; }
        public AEDV02ResolvedOpportunity Opportunity { get; set; } = new AEDV02ResolvedOpportunity();
    }

    public readonly struct AEDV02HistoricalCompetence
    {
        public AEDV02HistoricalCompetence(double? score, int matches, int resolved, int censored, string confidence)
        { Score = score; Matches = matches; Resolved = resolved; Censored = censored; Confidence = confidence; }
        public double? Score { get; }
        public int Matches { get; }
        public int Resolved { get; }
        public int Censored { get; }
        public string Confidence { get; }
        public bool ReadyForIncrease => Confidence == "Sufficient" && Score >= AEDV02PilotOpportunities.WeakestCompetence;
    }
}
