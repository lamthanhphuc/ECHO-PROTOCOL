using System;
using System.Collections.Generic;
using System.Linq;

namespace EchoProtocol.AI.Common.AED
{
    public static class AEDPursuitMetricProjectorV1
    {
        public static IReadOnlyList<AEDMetricResultV1> Project(
            AEDPursuitEvidenceSnapshotV1 snapshot, int tickRate,
            DateTime windowStartedAtUtc, DateTime windowEndedAtUtc)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var episodes = snapshot.Episodes;
            var terminal = episodes.Where(e => e.TerminalOutcome.HasValue).ToArray();
            var censored = terminal.Count(e => e.TerminalOutcome == AEDPursuitTerminalV1.Censored);
            var resolved = terminal.Where(e => e.TerminalOutcome != AEDPursuitTerminalV1.Censored
                && e.TerminalOutcome != AEDPursuitTerminalV1.Cancelled
                && e.TerminalOutcome != AEDPursuitTerminalV1.Incomplete).ToArray();
            var escape = resolved.Count(e => e.TerminalOutcome == AEDPursuitTerminalV1.Escaped);
            var down = resolved.Count(e => e.TerminalOutcome == AEDPursuitTerminalV1.Downed
                || e.TerminalOutcome == AEDPursuitTerminalV1.Eliminated);
            var switched = resolved.Count(e => e.TerminalOutcome == AEDPursuitTerminalV1.TargetSwitched);
            var lostEligible = episodes.Sum(e => e.EligibleLostWindows);
            var lostResolved = episodes.Sum(e => e.ResolvedLostWindows);
            var reacquired = episodes.Sum(e => e.ReacquisitionCount);
            var durationSeconds = tickRate > 0
                ? resolved.Sum(e => Math.Max(0, e.EndedTick.GetValueOrDefault() - e.StartedTick)) / (double)tickRate
                : 0d;
            var sourceOccurrences = episodes.SelectMany(e => e.Facts)
                .Select(f => f.OccurrenceKey).Distinct(StringComparer.Ordinal).ToArray();

            return new[]
            {
                Rate("Stalker.PursuitEscapeRate", snapshot, episodes.Count,
                    resolved.Length, escape, censored, sourceOccurrences,
                    resolved.Select(e => e.EpisodeId), windowStartedAtUtc,
                    windowEndedAtUtc),
                Rate("Stalker.ReacquisitionRate", snapshot, lostEligible,
                    lostResolved, reacquired, Math.Max(0, lostEligible - lostResolved),
                    sourceOccurrences, episodes.Select(e => e.EpisodeId),
                    windowStartedAtUtc, windowEndedAtUtc),
                Rate("Stalker.ChaseDownRate", snapshot, episodes.Count,
                    resolved.Length, down, censored, sourceOccurrences,
                    resolved.Select(e => e.EpisodeId), windowStartedAtUtc,
                    windowEndedAtUtc),
                Duration(snapshot, episodes.Count, resolved.Length,
                    durationSeconds, censored, sourceOccurrences,
                    resolved.Select(e => e.EpisodeId), windowStartedAtUtc,
                    windowEndedAtUtc),
                Unsupported("Stalker.HideEscapeSuccessRate", snapshot,
                    windowStartedAtUtc, windowEndedAtUtc),
                Unsupported("Stalker.NoiseDetectionAttributionRate", snapshot,
                    windowStartedAtUtc, windowEndedAtUtc)
            };
        }

        private static AEDMetricResultV1 Rate(string id,
            AEDPursuitEvidenceSnapshotV1 snapshot, int eligible,
            int resolved, int successes, int censored,
            IEnumerable<string> occurrences, IEnumerable<string> episodeIds,
            DateTime started, DateTime ended)
        {
            var status = Status(snapshot, eligible, resolved, censored);
            double? value = status == AEDMetricStatusV1.Available && resolved > 0
                ? (double?)successes / resolved : null;
            return Create(id, snapshot, eligible, successes,
                Math.Max(0, resolved - successes), censored, successes,
                resolved > 0 ? resolved : (int?)null, "ratio", value,
                AEDMetricMeasurementKindV1.Rate, "opportunity", occurrences,
                episodeIds, started, ended, status);
        }

        private static AEDMetricResultV1 Duration(
            AEDPursuitEvidenceSnapshotV1 snapshot, int eligible,
            int resolved, double seconds, int censored,
            IEnumerable<string> occurrences, IEnumerable<string> episodeIds,
            DateTime started, DateTime ended)
        {
            var status = Status(snapshot, eligible, resolved, censored);
            return Create("Stalker.PursuitDurationSeconds", snapshot,
                eligible, resolved, 0, censored, seconds, null, "seconds",
                status == AEDMetricStatusV1.Available ? seconds : null,
                AEDMetricMeasurementKindV1.DurationSeconds, null,
                occurrences, episodeIds, started, ended, status);
        }

        private static AEDMetricResultV1 Unsupported(string id,
            AEDPursuitEvidenceSnapshotV1 snapshot, DateTime started, DateTime ended) =>
            Create(id, snapshot, 0, 0, 0, 0, 0, null, "ratio", null,
                AEDMetricMeasurementKindV1.Rate, "opportunity",
                Array.Empty<string>(), Array.Empty<string>(), started, ended,
                snapshot.IsInvalid ? AEDMetricStatusV1.Invalid
                    : snapshot.IsIncomplete ? AEDMetricStatusV1.Incomplete
                    : AEDMetricStatusV1.Unsupported);

        private static AEDMetricStatusV1 Status(
            AEDPursuitEvidenceSnapshotV1 snapshot, int eligible,
            int resolved, int censored)
        {
            if (snapshot.IsInvalid) return AEDMetricStatusV1.Invalid;
            if (snapshot.IsIncomplete) return AEDMetricStatusV1.Incomplete;
            if (eligible == 0) return AEDMetricStatusV1.NoOpportunity;
            if (resolved == 0 && censored > 0) return AEDMetricStatusV1.CensoredOnly;
            if (resolved == 0) return AEDMetricStatusV1.Incomplete;
            return AEDMetricStatusV1.Available;
        }

        private static AEDMetricResultV1 Create(string id,
            AEDPursuitEvidenceSnapshotV1 snapshot, int eligible,
            int successes, int failures, int censored, double numerator,
            double? denominator, string unit, double? value,
            AEDMetricMeasurementKindV1 kind, string denominatorUnit,
            IEnumerable<string> occurrences, IEnumerable<string> episodeIds,
            DateTime started, DateTime ended, AEDMetricStatusV1 status)
        {
            var context = snapshot.PolicyContext;
            return new AEDMetricResultV1(id, snapshot.MatchId,
                snapshot.PhaseOrdinal, snapshot.PhaseName, null, null,
                context?.Difficulty, context?.ScenarioResolutionMode,
                context?.ConfigSource, context?.ScenarioConfigVersion,
                context?.PolicyVersion, "1.1", context?.AppliedPlanRevision,
                context?.AppliedParameterFingerprint, "AED_PURSUIT_EPISODE_V1",
                "FusionStateAuthority", Array.Empty<string>(), occurrences,
                episodeIds, eligible, successes, failures, censored, numerator,
                denominator, unit, value, kind, denominatorUnit,
                context?.ComparisonContextKey, null, null, 0,
                "InsufficientForPolicy", status,
                status == AEDMetricStatusV1.Available
                    ? Array.Empty<string>() : new[] { "PURSUIT_" + status.ToString().ToUpperInvariant() },
                started, ended, context);
        }
    }
}
