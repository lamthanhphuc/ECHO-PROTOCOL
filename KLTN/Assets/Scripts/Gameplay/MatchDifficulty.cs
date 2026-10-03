namespace EchoProtocol.Gameplay
{
    public enum MatchDifficulty { Easy, Normal, Hard }

    public readonly struct MatchDifficultyProfile
    {
        public MatchDifficultyProfile(
            int teamToolsPerZone,
            float patrolSpeed,
            float chaseSpeed,
            float detectionDurationSeconds,
            float detectionDecayDurationSeconds,
            float searchDurationSeconds,
            float attackRange,
            float attackWindupSeconds,
            float attackRecoverySeconds,
            float doorBreakDurationSeconds,
            float seekPlayersAfterSeconds,
            float coreCarrierPursuitDelaySeconds,
            float hearingRangeMultiplier,
            float specialEncounterCooldownSeconds,
            int zone1MinionCap,
            int zone2MinionCap,
            bool objectiveInvestigationEnabled)
        {
            TeamToolsPerZone = teamToolsPerZone;
            PatrolSpeed = patrolSpeed;
            ChaseSpeed = chaseSpeed;
            DetectionDurationSeconds = detectionDurationSeconds;
            DetectionDecayDurationSeconds = detectionDecayDurationSeconds;
            SearchDurationSeconds = searchDurationSeconds;
            AttackRange = attackRange;
            AttackWindupSeconds = attackWindupSeconds;
            AttackRecoverySeconds = attackRecoverySeconds;
            DoorBreakDurationSeconds = doorBreakDurationSeconds;
            SeekPlayersAfterSeconds = seekPlayersAfterSeconds;
            CoreCarrierPursuitDelaySeconds = coreCarrierPursuitDelaySeconds;
            HearingRangeMultiplier = hearingRangeMultiplier;
            SpecialEncounterCooldownSeconds = specialEncounterCooldownSeconds;
            Zone1MinionCap = zone1MinionCap;
            Zone2MinionCap = zone2MinionCap;
            ObjectiveInvestigationEnabled = objectiveInvestigationEnabled;
        }

        public int TeamToolsPerZone { get; }
        public float PatrolSpeed { get; }
        public float ChaseSpeed { get; }
        public float DetectionDurationSeconds { get; }
        public float DetectionDecayDurationSeconds { get; }
        public float SearchDurationSeconds { get; }
        public float AttackRange { get; }
        public float AttackWindupSeconds { get; }
        public float AttackRecoverySeconds { get; }
        public float DoorBreakDurationSeconds { get; }
        public float SeekPlayersAfterSeconds { get; }
        public float CoreCarrierPursuitDelaySeconds { get; }
        public float HearingRangeMultiplier { get; }
        public float SpecialEncounterCooldownSeconds { get; }
        public int Zone1MinionCap { get; }
        public int Zone2MinionCap { get; }
        public bool ObjectiveInvestigationEnabled { get; }
    }

    public static class MatchDifficultyProfiles
    {
        public static MatchDifficultyProfile Get(MatchDifficulty difficulty)
        {
            switch (difficulty)
            {
                case MatchDifficulty.Easy:
                    return new MatchDifficultyProfile(
                        6,      // TeamToolsPerZone
                        5.0f,   // PatrolSpeed
                        6.0f,   // ChaseSpeed
                        2.5f,   // DetectionDurationSeconds
                        20f,    // DetectionDecayDurationSeconds
                        1.0f,   // SearchDurationSeconds
                        2.2f,   // AttackRange
                        0.85f,  // AttackWindupSeconds
                        2.0f,   // AttackRecoverySeconds
                        8f,     // DoorBreakDurationSeconds
                        240f,   // SeekPlayersAfterSeconds
                        45f,    // CoreCarrierPursuitDelaySeconds
                        0.5f,   // HearingRangeMultiplier
                        720f,   // SpecialEncounterCooldownSeconds
                        0,      // Zone1MinionCap
                        0,      // Zone2MinionCap
                        false); // ObjectiveInvestigationEnabled
                case MatchDifficulty.Hard:
                    return new MatchDifficultyProfile(
                        5, 8f, 9f, 0.75f, 30f, 3f, 3f, 0.35f,
                        1f, 3f, 75f, 15f, 1.25f, 300f, 2, 3, true);
                default:
                    // Between Easy and the old baseline, now used as Hard.
                    return new MatchDifficultyProfile(
                        5, 6.5f, 8f, 1.25f, 25f, 2.25f, 2.7f, 0.6f,
                        1.4f, 4.5f, 120f, 25f, 1f, 420f, 1, 1, true);
            }
        }
    }
}
