using System;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Gameplay;

namespace EchoProtocol.AI.AED
{
    // Unity/gameplay-facing adapter. Keep this OUTSIDE the noEngineReferences
    // EchoProtocol.AI.Common assembly.
    public static class AEDv2GameplayBridge
    {
        public static bool IsNormalCompatible()
        {
            var n = MatchDifficultyProfiles.Get(MatchDifficulty.Normal);
            return Matches(AEDv2Key.DetectionAcquireSeconds, n.DetectionDurationSeconds)
                && Matches(AEDv2Key.DetectionForgetSeconds, n.DetectionDecayDurationSeconds)
                && Matches(AEDv2Key.ChaseSpeed, n.ChaseSpeed)
                && Matches(AEDv2Key.SearchSeconds, n.SearchDurationSeconds)
                && Matches(AEDv2Key.PatrolSpeed, n.PatrolSpeed)
                && Matches(AEDv2Key.HearingMultiplier, n.HearingRangeMultiplier)
                && Matches(AEDv2Key.SeekPlayersAfterSeconds, n.SeekPlayersAfterSeconds)
                && Matches(AEDv2Key.CoreCarrierAfterSeconds, n.CoreCarrierPursuitDelaySeconds)
                && Matches(AEDv2Key.SpecialCooldownSeconds, n.SpecialEncounterCooldownSeconds)
                && Matches(AEDv2Key.DoorBreakSeconds, n.DoorBreakDurationSeconds)
                && Matches(AEDv2Key.ObjectiveNoiseInvestigationEnabled,
                    n.ObjectiveInvestigationEnabled ? 1 : 0)
                && Matches(AEDv2Key.Zone1MinionCap, n.Zone1MinionCap)
                && Matches(AEDv2Key.Zone2MinionCap, n.Zone2MinionCap);
        }

        private static bool Matches(AEDv2Key key, double value)
            => Math.Abs(AEDv2Catalog.Find(key).Baseline - value) <= 0.000001;

        public static MatchDifficultyProfile ToNormalDifficultyProfile(AEDv2Plan plan)
        {
            if (plan == null || !IsNormalCompatible() ||
                !plan.TrySingleBoundedChange(out _))
                throw new InvalidOperationException("AED_V2_PROFILE_INVALID_OR_BASELINE_DRIFT");
            var n = MatchDifficultyProfiles.Get(MatchDifficulty.Normal);
            return new MatchDifficultyProfile(
                n.TeamToolsPerZone,
                (float)plan.Get(AEDv2Key.PatrolSpeed),
                (float)plan.Get(AEDv2Key.ChaseSpeed),
                (float)plan.Get(AEDv2Key.DetectionAcquireSeconds),
                (float)plan.Get(AEDv2Key.DetectionForgetSeconds),
                (float)plan.Get(AEDv2Key.SearchSeconds),
                n.AttackRange, n.AttackWindupSeconds, n.AttackRecoverySeconds,
                (float)plan.Get(AEDv2Key.DoorBreakSeconds),
                (float)plan.Get(AEDv2Key.SeekPlayersAfterSeconds),
                (float)plan.Get(AEDv2Key.CoreCarrierAfterSeconds),
                (float)plan.Get(AEDv2Key.HearingMultiplier),
                (float)plan.Get(AEDv2Key.SpecialCooldownSeconds),
                (int)plan.Get(AEDv2Key.Zone1MinionCap),
                (int)plan.Get(AEDv2Key.Zone2MinionCap),
                n.MaximumRevivesPerZone,
                plan.Get(AEDv2Key.ObjectiveNoiseInvestigationEnabled) != 0);
        }
    }
}
