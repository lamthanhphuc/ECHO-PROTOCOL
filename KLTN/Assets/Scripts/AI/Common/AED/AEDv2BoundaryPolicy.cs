using System;

namespace EchoProtocol.AI.Common.AED
{
    public static class AEDv2BoundaryPolicy
    {
        public static bool TryPropose(AEDv2Plan previous,
            AEDv2CurrentMatchEvidence evidence, Guid matchId,
            string rosterIdentity, uint phaseOrdinal, bool safeBoundary,
            ScenarioDecisionPoint decisionPoint,
            out AEDv2Plan next, out AEDv2Key changedKey, out string reason)
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
            if (evidence.DownCount >= 2
                && previous.ReviveBonus == 0
                && decisionPoint != ScenarioDecisionPoint.FinalHuntSetup)
            {
                changedKey = AEDv2Key.ReviveBonusPerZone;
                intent = AdaptationIntent.Relieve;
            }
            else if (evidence.AcceptedNoiseCount >= 8
                && previous.Get(AEDv2Key.DetectionAcquireSeconds)
                    == AEDv2Catalog.Find(AEDv2Key.DetectionAcquireSeconds).Baseline)
            {
                changedKey = AEDv2Key.DetectionAcquireSeconds;
                intent = AdaptationIntent.Relieve;
            }
            else if (evidence.DownCount == 0 && evidence.ObjectiveProgress > 0
                && evidence.AcceptedNoiseCount < 3
                && previous.Get(AEDv2Key.ChaseSpeed)
                    == AEDv2Catalog.Find(AEDv2Key.ChaseSpeed).Baseline)
            {
                changedKey = AEDv2Key.ChaseSpeed;
                intent = AdaptationIntent.IncreasePressure;
            }
            if (intent == AdaptationIntent.Hold) return false;
            var spec = AEDv2Catalog.Find(changedKey);
            next = previous.With(changedKey, spec.Target(intent));
            reason = string.Empty;
            return true;
        }
    }
}
