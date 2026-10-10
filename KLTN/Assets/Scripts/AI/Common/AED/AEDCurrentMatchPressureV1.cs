using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace EchoProtocol.AI.Common.AED
{
    public enum AEDPressureLevelV1
    {
        Unknown,
        Quiet,
        Elevated,
        Critical
    }

    public sealed class AEDCurrentMatchPressureV1
    {
        public const string RuleVersion =
            "AED_CURRENT_PRESSURE_V1_RESEARCH";

        public Guid MatchId { get; }
        public uint PhaseOrdinal { get; }
        public AEDPressureLevelV1 Level { get; }
        public double? Score { get; }
        public bool SourceComplete { get; }
        public bool ResearchOnly => true;
        public IReadOnlyList<string> ReasonCodes { get; }

        private AEDCurrentMatchPressureV1(
            Guid matchId,
            uint ordinal,
            AEDPressureLevelV1 level,
            double? score,
            bool complete,
            IEnumerable<string> reasons)
        {
            MatchId = matchId;
            PhaseOrdinal = ordinal;
            Level = level;
            Score = score;
            SourceComplete = complete;
            ReasonCodes = new ReadOnlyCollection<string>(
                reasons.ToArray());
        }

        public static AEDCurrentMatchPressureV1 Project(
            AEDv2CurrentMatchEvidence canonical,
            AEDPursuitEvidenceSnapshotV1 pursuit,
            AEDMinionEvidenceSnapshotV1 minion,
            AEDSurvivalEvidenceSnapshotV1 survival,
            long endTick,
            double tickRate)
        {
            var matchId = canonical?.MatchId ?? Guid.Empty;
            var ordinal = canonical?.PhaseOrdinal ?? 0;

            if (canonical == null ||
                pursuit == null ||
                minion == null ||
                survival == null ||
                tickRate <= 0 ||
                endTick < 0 ||
                !canonical.TelemetryCompleteness ||
                !canonical.HasValidFingerprint() ||
                pursuit.IsIncomplete || pursuit.IsInvalid ||
                minion.IsIncomplete || minion.IsInvalid ||
                !survival.IsUsable ||
                pursuit.MatchId != matchId ||
                minion.MatchId != matchId ||
                survival.MatchId != matchId ||
                pursuit.PhaseOrdinal != ordinal ||
                minion.PhaseOrdinal != ordinal ||
                survival.PhaseOrdinal != ordinal ||
                !string.Equals(pursuit.PhaseName, canonical.CompletedPhase,
                    StringComparison.Ordinal) ||
                !string.Equals(minion.PhaseName, canonical.CompletedPhase,
                    StringComparison.Ordinal) ||
                !string.Equals(survival.PhaseName, canonical.CompletedPhase,
                    StringComparison.Ordinal) ||
                double.IsNaN(tickRate) || double.IsInfinity(tickRate))
            {
                return new AEDCurrentMatchPressureV1(
                    matchId, ordinal,
                    AEDPressureLevelV1.Unknown,
                    null, false,
                    new[] { "PRESSURE_SOURCE_INCOMPLETE" });
            }

            double Decay(long sourceTick)
            {
                var ageSeconds =
                    (endTick - sourceTick) / tickRate;

                if (ageSeconds < 0 || ageSeconds > 60)
                    return 0;

                return Math.Exp(
                    -Math.Log(2d) * ageSeconds / 15d);
            }

            var stalkerImpact = pursuit.Episodes
                .Select(e => e.Facts
                    .Select(f =>
                        (f.Kind == AEDPursuitFactKindV1.Downed ||
                         f.Kind == AEDPursuitFactKindV1.Eliminated
                            ? 1d
                            : f.Kind == AEDPursuitFactKindV1.Hit
                                ? 0.8d : 0d)
                        * Decay(f.SourceTick))
                    .DefaultIfEmpty(0d).Max())
                .Sum();

            var recentChase = pursuit.Episodes
                .SelectMany(e => e.StateSegments)
                .Where(s => s.State == "CHASE")
                .Select(s => 0.35d * Decay(s.EndTick))
                .DefaultIfEmpty(0d).Max();

            var minionFacts = minion.Episodes
                .SelectMany(e => e.Facts)
                .Concat(minion.Facts);

            var minionImpact = minionFacts
                .Where(f => f.Accepted)
                .Where(f =>
                    f.Kind == AEDMinionFactKindV1.SlowApplied ||
                    f.Kind == AEDMinionFactKindV1.CoreForcedDrop ||
                    f.Kind == AEDMinionFactKindV1.CoreStolen ||
                    f.Kind == AEDMinionFactKindV1.ToolRelocated)
                .GroupBy(f => f.MinionNetworkId)
                .Select(g => g.Select(f =>
                    (f.Kind == AEDMinionFactKindV1.CoreStolen
                        ? 0.8d : 0.65d) *
                    Decay(f.SourceTick))
                    .DefaultIfEmpty(0d).Max())
                .Sum();

            var recentHarass = minion.Episodes
                .SelectMany(e => e.StateSegments)
                .Where(s => s.State == "Harass" && s.EndTick.HasValue)
                .Select(s => 0.30d * Decay(s.EndTick.Value))
                .DefaultIfEmpty(0d).Max();

            var phaseDownPressure =
                canonical.DownCount > 0 ? 0.25d : 0d;

            var score = Math.Min(1d,
                stalkerImpact +
                recentChase +
                minionImpact +
                recentHarass +
                phaseDownPressure);

            var level = score >= 0.70d
                ? AEDPressureLevelV1.Critical
                : score >= 0.25d
                    ? AEDPressureLevelV1.Elevated
                    : AEDPressureLevelV1.Quiet;

            var reasons = new List<string>();

            if (stalkerImpact > 0)
                reasons.Add("RECENT_STALKER_CONSEQUENCE");

            if (recentChase > 0)
                reasons.Add("RECENT_STALKER_CHASE");

            if (minionImpact > 0)
                reasons.Add("RECENT_MINION_EFFECT");

            if (recentHarass > 0)
                reasons.Add("RECENT_MINION_HARASS");

            if (phaseDownPressure > 0)
                reasons.Add("PHASE_DOWN_OBSERVED_NOT_TIME_RESOLVED");

            return new AEDCurrentMatchPressureV1(
                matchId, ordinal, level,
                score, true, reasons);
        }
    }
}
