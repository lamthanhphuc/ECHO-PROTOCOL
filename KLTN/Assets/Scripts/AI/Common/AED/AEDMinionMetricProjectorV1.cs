using System;
using System.Collections.Generic;
using System.Linq;

namespace EchoProtocol.AI.Common.AED
{
    public static class AEDMinionMetricProjectorV1
    {
        public static IReadOnlyList<AEDMetricResultV1> Project(
            AEDMinionEvidenceSnapshotV1 snapshot,
            DateTime windowStartedAtUtc, DateTime windowEndedAtUtc)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var facts = snapshot.Episodes.SelectMany(e => e.Facts)
                .Concat(snapshot.Facts).ToArray();
            var resolvedEvasion = snapshot.Episodes.Where(e =>
                e.TerminalOutcome == AEDMinionTerminalV1.Evaded
                || e.TerminalOutcome == AEDMinionTerminalV1.Countered).ToArray();
            var slowAttempts = facts.Where(f => f.Kind == AEDMinionFactKindV1.AttackAttempted
                && f.EffectKind == "SLOW").ToArray();
            var toolAttempts = facts.Where(f => f.Kind == AEDMinionFactKindV1.AttackAttempted
                && f.EffectKind == "TOOL").ToArray();
            var coreAttempts = facts.Where(f => f.Kind == AEDMinionFactKindV1.AttackAttempted
                && f.EffectKind == "CORE").ToArray();
            var alerts = facts.Where(f => f.Kind == AEDMinionFactKindV1.AlertAccepted).ToArray();
            var distractions = facts.Where(f => f.Kind == AEDMinionFactKindV1.NoiseMakerReaction).ToArray();
            var distractionOpportunities = facts.Where(f => f.Kind == AEDMinionFactKindV1.NoiseMakerOpportunity).ToArray();
            var resolvedDistractions = distractionOpportunities.Where(opportunity =>
                distractions.Any(reaction => !string.IsNullOrWhiteSpace(opportunity.SourceEventId)
                    && string.Equals(reaction.SourceEventId, opportunity.SourceEventId,
                        StringComparison.Ordinal))).ToArray();
            var recovery = facts.Where(f => f.Kind == AEDMinionFactKindV1.CoreForcedDrop
                && !string.IsNullOrWhiteSpace(f.ObjectId)).ToArray();
            var recovered = facts.Where(f => f.Kind == AEDMinionFactKindV1.ItemRecovered
                && f.Accepted && !string.IsNullOrWhiteSpace(f.ObjectId)).ToArray();

            return new[]
            {
                Rate("Minion.EvasionRate", snapshot, snapshot.Episodes.Count,
                    resolvedEvasion.Length,
                    resolvedEvasion.Count(e => e.TerminalOutcome == AEDMinionTerminalV1.Evaded),
                    snapshot.Episodes.Count(e => e.TerminalOutcome == AEDMinionTerminalV1.Censored
                        || e.TerminalOutcome == AEDMinionTerminalV1.Disengaged),
                    snapshot.Episodes.Select(e => e.EpisodeId), facts,
                    windowStartedAtUtc, windowEndedAtUtc),
                Unsupported("Minion.FlashlightDefenseRate", snapshot, 0,
                    0, facts, windowStartedAtUtc, windowEndedAtUtc),
                AttemptRate("Minion.SlowHitRate", snapshot, slowAttempts,
                    "SLOW", facts, windowStartedAtUtc, windowEndedAtUtc),
                AttemptRate("Minion.ToolProtectionRate", snapshot, toolAttempts,
                    "TOOL", facts, windowStartedAtUtc, windowEndedAtUtc, invert: true),
                AttemptRate("Minion.CoreProtectionRate", snapshot, coreAttempts,
                    "CORE", facts, windowStartedAtUtc, windowEndedAtUtc, invert: true),
                Unsupported("Minion.RecoveryRate", snapshot, recovery.Length,
                    recovered.Length, facts, windowStartedAtUtc, windowEndedAtUtc),
                Unsupported("Minion.AlertImpactRate", snapshot, alerts.Length,
                    0, facts, windowStartedAtUtc, windowEndedAtUtc),
                Rate("Minion.DistractionSuccessRate", snapshot,
                    distractionOpportunities.Length,
                    resolvedDistractions.Length,
                    resolvedDistractions.Length,
                    distractionOpportunities.Length - resolvedDistractions.Length,
                    resolvedDistractions.Select(f => f.OccurrenceKey), facts,
                    windowStartedAtUtc, windowEndedAtUtc),
                Unsupported("Minion.ObjectiveDisruptionSeconds", snapshot, 0,
                    0, facts, windowStartedAtUtc, windowEndedAtUtc)
            };
        }

        private static AEDMetricResultV1 AttemptRate(string id,
            AEDMinionEvidenceSnapshotV1 snapshot, AEDMinionFactV1[] attempts,
            string effect, AEDMinionFactV1[] allFacts, DateTime started,
            DateTime ended, bool invert = false)
        {
            var resolved = attempts.Length;
            var successes = attempts.Count(f => invert ? !f.Accepted : f.Accepted);
            return Rate(id, snapshot, attempts.Length, resolved, successes, 0,
                attempts.Select(f => f.OccurrenceKey), allFacts, started, ended);
        }

        private static AEDMetricResultV1 Rate(string id,
            AEDMinionEvidenceSnapshotV1 snapshot, int eligible, int resolved,
            int successes, int censored, IEnumerable<string> episodes,
            AEDMinionFactV1[] facts, DateTime started, DateTime ended)
        {
            var status = Status(snapshot, eligible, resolved, censored);
            double? value = status == AEDMetricStatusV1.Available && resolved > 0
                ? (double?)successes / resolved : null;
            return new AEDMetricResultV1(id, snapshot.MatchId,
                snapshot.PhaseOrdinal, snapshot.PhaseName, null, null,
                null, null, null, null, null, "1.1", null, null,
                "AED_MINION_ENCOUNTER_V1", "FusionStateAuthority",
                facts.Where(f => !string.IsNullOrWhiteSpace(f.SourceEventId))
                    .Select(f => f.SourceEventId),
                facts.Select(f => f.OccurrenceKey), episodes,
                eligible, successes, Math.Max(0, resolved - successes), censored,
                successes, resolved > 0 ? resolved : (double?)null,
                "ratio", value, AEDMetricMeasurementKindV1.Rate,
                "opportunity", null, null, null, 0, "InsufficientForPolicy",
                status, status == AEDMetricStatusV1.Available
                    ? Array.Empty<string>() : new[] { "MINION_" + status.ToString().ToUpperInvariant() },
                started, ended, snapshot.PolicyContext);
        }

        private static AEDMetricResultV1 Unsupported(string id,
            AEDMinionEvidenceSnapshotV1 snapshot, int eligible, int resolved,
            AEDMinionFactV1[] facts, DateTime started, DateTime ended)
        {
            if (snapshot.IsIncomplete || snapshot.IsInvalid)
                return Rate(id, snapshot, eligible, resolved, 0, 0,
                    snapshot.Episodes.Select(e => e.EpisodeId), facts, started, ended);
            return new AEDMetricResultV1(id, snapshot.MatchId,
                snapshot.PhaseOrdinal, snapshot.PhaseName, null, null,
                null, null, null, null, null, "1.1", null, null,
                "AED_MINION_ENCOUNTER_V1", "FusionStateAuthority",
                Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
                0, 0, 0, 0, 0, null, "ratio", null,
                AEDMetricMeasurementKindV1.Rate, "opportunity", null, null,
                null, 0, "InsufficientForPolicy", AEDMetricStatusV1.Unsupported,
                new[] { "MINION_SOURCE_UNSUPPORTED" }, started, ended,
                snapshot.PolicyContext);
        }

        private static AEDMetricStatusV1 Status(AEDMinionEvidenceSnapshotV1 snapshot,
            int eligible, int resolved, int censored)
        {
            if (snapshot.IsInvalid) return AEDMetricStatusV1.Invalid;
            if (snapshot.IsIncomplete) return AEDMetricStatusV1.Incomplete;
            if (eligible == 0) return AEDMetricStatusV1.NoOpportunity;
            if (resolved == 0 && censored > 0) return AEDMetricStatusV1.CensoredOnly;
            if (resolved == 0) return AEDMetricStatusV1.Incomplete;
            return AEDMetricStatusV1.Available;
        }
    }
}
