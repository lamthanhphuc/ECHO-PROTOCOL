namespace EchoProtocol.Gameplay
{
    public enum MatchDifficulty { Easy, Normal, Hard }

    public readonly struct MatchDifficultyProfile
    {
        public MatchDifficultyProfile(
            int teamToolsPerZone, float patrolSpeed, float chaseSpeed,
            float detectionDurationSeconds, float detectionDecayDurationSeconds,
            float searchDurationSeconds, float attackRange, float attackWindupSeconds,
            float attackRecoverySeconds, float doorBreakDurationSeconds,
            float seekPlayersAfterSeconds, float coreCarrierPursuitDelaySeconds,
            float hearingRangeMultiplier, float specialEncounterCooldownSeconds,
            int zone1MinionCap, int zone2MinionCap)
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
                        5.5f,   // PatrolSpeed
                        6.5f,   // ChaseSpeed
                        2.0f,   // DetectionDurationSeconds
                        0.35f,  // DetectionDecayDurationSeconds
                        1.5f,   // SearchDurationSeconds
                        2.4f,   // AttackRange
                        0.9f,   // AttackWindupSeconds
                        1.8f,   // AttackRecoverySeconds
                        6f,     // DoorBreakDurationSeconds
                        180f,   // SeekPlayersAfterSeconds
                        35f,    // CoreCarrierPursuitDelaySeconds
                        0.75f,  // HearingRangeMultiplier
                        600f,   // SpecialEncounterCooldownSeconds
                        1,      // Zone1MinionCap
                        1);     // Zone2MinionCap
                case MatchDifficulty.Hard:
                    return new MatchDifficultyProfile(
                        5, 9f, 10f, 0.5f, 1.5f, 5f, 3.2f, 0.25f,
                        0.75f, 2f, 45f, 8f, 1.5f, 300f, 3, 4);
                default:
                    // Current serialized values in StalkerNetwork.prefab.
                    return new MatchDifficultyProfile(
                        5, 8f, 9f, 0.75f, 1f, 3f, 3f, 0.35f,
                        1f, 3f, 75f, 15f, 1.25f, 300f, 2, 3);
            }
        }
    }
}
