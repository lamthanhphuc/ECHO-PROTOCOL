using System;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class AEDv2BoundaryHoldContext
    {
        public bool StalkerUnsafeState { get; set; }
        public bool PlayerDownedOrReviving { get; set; }
        public bool AdjustmentBudgetExhausted { get; set; }
        public bool PressureMetricDecisionEligible { get; set; }
        public AEDMetricStatusV1? PressureMetricStatus { get; set; }
        public string ExpectedPhaseName { get; set; }
    }

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
            AEDv2RosterSafety rosterSafety = null,
            AEDv2BoundaryHoldContext holdContext = null)
        {
            next = previous;
            changedKey = default;
            reason = "HOLD_NO_ELIGIBLE_ADJUSTMENT";
            if (previous == null || !previous.IsBounded())
            {
                reason = "HOLD_INVALID_PLAN";
                return false;
            }
            if (evidence == null || !evidence.TelemetryCompleteness)
            {
                reason = "HOLD_EVIDENCE_INCOMPLETE";
                return false;
            }
            if (!evidence.HasValidFingerprint())
            {
                reason = "HOLD_FINGERPRINT_MISMATCH";
                return false;
            }
            if (evidence.MatchId != matchId || matchId == Guid.Empty)
            {
                reason = "HOLD_INVALID_EVIDENCE";
                return false;
            }
            if (evidence.RosterIdentity != rosterIdentity)
            {
                reason = "HOLD_ROSTER_CHANGED";
                return false;
            }
            if (evidence.PhaseOrdinal != phaseOrdinal
                || (!string.IsNullOrWhiteSpace(holdContext?.ExpectedPhaseName)
                    && evidence.CompletedPhase != holdContext.ExpectedPhaseName))
            {
                reason = "HOLD_PHASE_MISMATCH";
                return false;
            }
            if (holdContext?.PlayerDownedOrReviving == true)
            {
                reason = "HOLD_PLAYER_DOWNED_OR_REVIVING";
                return false;
            }
            if (holdContext?.StalkerUnsafeState == true)
            {
                reason = "HOLD_UNSAFE_STALKER_STATE";
                return false;
            }
            if (!safeBoundary)
            {
                reason = "HOLD_UNSAFE_BOUNDARY";
                return false;
            }
            if (evidence.ElapsedSeconds < 30 || evidence.AlivePlayers < 1)
            {
                reason = "HOLD_INSUFFICIENT_OBSERVATION";
                return false;
            }
            if (holdContext?.AdjustmentBudgetExhausted == true)
            {
                reason = "HOLD_ADJUSTMENT_BUDGET_EXHAUSTED";
                return false;
            }

            var intent = AdaptationIntent.Hold;
            var elapsedMinutes = evidence.ElapsedSeconds / 60.0;
            var downsPerMinute = evidence.DownCount / elapsedMinutes;

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
            if (intent == AdaptationIntent.Hold)
            {
                if (holdContext?.PressureMetricStatus == AEDMetricStatusV1.NoOpportunity)
                    reason = "HOLD_METRIC_NO_OPPORTUNITY";
                else if (holdContext?.PressureMetricStatus == AEDMetricStatusV1.Incomplete)
                    reason = "HOLD_EVIDENCE_INCOMPLETE";
                else if (holdContext?.PressureMetricStatus != AEDMetricStatusV1.Available)
                    reason = "HOLD_METRIC_UNSUPPORTED";
                else if (!holdContext.PressureMetricDecisionEligible
                    || rosterSafety == null || !rosterSafety.AllowPressure)
                    reason = "HOLD_INSUFFICIENT_OBSERVATION";
                else
                    reason = "HOLD_NO_ELIGIBLE_ADJUSTMENT";
                return false;
            }

            var spec = AEDv2Catalog.Find(changedKey);
            next = previous.With(changedKey, spec.Target(intent));
            reason = string.Empty;
            return true;
        }
    }
}
