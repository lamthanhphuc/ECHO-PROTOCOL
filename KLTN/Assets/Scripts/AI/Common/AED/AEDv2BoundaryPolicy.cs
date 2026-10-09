using System;

namespace EchoProtocol.AI.Common.AED
{
    public static class AEDv2BoundaryPolicy
    {
        private static bool CanChange(
            AEDv2Plan plan,
            AEDv2Key key,
            AdaptationIntent intent)
        {
            var spec = AEDv2Catalog.Find(key);

            return !spec.PreMatchOnly
                && spec.HasTarget(intent)
                && plan.Get(key) == spec.Baseline;
        }

        public static bool TryPropose(AEDv2Plan previous,
            AEDv2CurrentMatchEvidence evidence, Guid matchId,
            string rosterIdentity, uint phaseOrdinal, bool safeBoundary,
            ScenarioDecisionPoint decisionPoint,
            out AEDv2Plan next, out AEDv2Key changedKey, out string reason,
            AEDv2RosterSafety rosterSafety = null)
        {
            next = previous;
            changedKey = default;
            reason = "AED_V2_BOUNDARY_HOLD";
            if (previous == null || !previous.IsBounded() || evidence == null
                || !safeBoundary || !evidence.TelemetryCompleteness
                || !evidence.HasValidFingerprint()
                || evidence.MatchId != matchId
                || evidence.RosterIdentity != rosterIdentity
                || evidence.PhaseOrdinal != phaseOrdinal
                || evidence.ElapsedSeconds < 30 || evidence.AlivePlayers < 1)
                return false;

            var intent = AdaptationIntent.Hold;
            var elapsedMinutes = evidence.ElapsedSeconds / 60.0;
            var noisePerMinute = evidence.AcceptedNoiseCount / elapsedMinutes;
            var downsPerMinute = evidence.DownCount / elapsedMinutes;
            var objectivesPerMinute = evidence.ObjectiveProgress / elapsedMinutes;

            if (evidence.EliminatedCount >= 1
                && CanChange(previous, AEDv2Key.SpecialCooldownSeconds,
                    AdaptationIntent.Relieve))
            {
                changedKey = AEDv2Key.SpecialCooldownSeconds;
                intent = AdaptationIntent.Relieve;
            }
            else if (evidence.DownCount >= 2
                && downsPerMinute >= 0.5
                && decisionPoint != ScenarioDecisionPoint.FinalHuntSetup
                && CanChange(previous, AEDv2Key.ReviveBonusPerZone,
                    AdaptationIntent.Relieve))
            {
                changedKey = AEDv2Key.ReviveBonusPerZone;
                intent = AdaptationIntent.Relieve;
            }
            else if (evidence.DownCount >= 2
                && evidence.ReviveCount >= 1
                && CanChange(previous, AEDv2Key.SeekPlayersAfterSeconds,
                    AdaptationIntent.Relieve))
            {
                changedKey = AEDv2Key.SeekPlayersAfterSeconds;
                intent = AdaptationIntent.Relieve;
            }
            else if (evidence.AcceptedNoiseCount >= 8
                && noisePerMinute >= 4.0
                && CanChange(previous, AEDv2Key.DetectionAcquireSeconds,
                    AdaptationIntent.Relieve))
            {
                changedKey = AEDv2Key.DetectionAcquireSeconds;
                intent = AdaptationIntent.Relieve;
            }
            else if (evidence.TeamToolUseCount >= 3
                && evidence.DownCount >= 1
                && CanChange(previous, AEDv2Key.HearingMultiplier,
                    AdaptationIntent.Relieve))
            {
                changedKey = AEDv2Key.HearingMultiplier;
                intent = AdaptationIntent.Relieve;
            }
            else if (evidence.DownCount == 0
                && evidence.EliminatedCount == 0
                && evidence.ObjectiveProgress >= 2
                && objectivesPerMinute >= 0.5
                && CanChange(previous, AEDv2Key.PatrolSpeed,
                    AdaptationIntent.IncreasePressure))
            {
                changedKey = AEDv2Key.PatrolSpeed;
                intent = AdaptationIntent.IncreasePressure;
            }
            else if (evidence.DownCount == 0
                && evidence.EliminatedCount == 0
                && evidence.ObjectiveProgress > 0
                && noisePerMinute < 3.0
                && CanChange(previous, AEDv2Key.ChaseSpeed,
                    AdaptationIntent.IncreasePressure))
            {
                changedKey = AEDv2Key.ChaseSpeed;
                intent = AdaptationIntent.IncreasePressure;
            }
            if (intent == AdaptationIntent.IncreasePressure
                && (rosterSafety == null || !rosterSafety.AllowPressure))
            {
                reason = "AED_V2_ROSTER_PRESSURE_GUARD";
                return false;
            }
            if (intent == AdaptationIntent.Hold) return false;
            var spec = AEDv2Catalog.Find(changedKey);
            next = previous.With(changedKey, spec.Target(intent));
            reason = string.Empty;
            return true;
        }
    }
}
