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
            float hearingRangeMultiplier, float specialEncounterCooldownSeconds)
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
    }

    public static class MatchDifficultyProfiles
    {
        public static MatchDifficultyProfile Get(MatchDifficulty difficulty)
        {
            switch (difficulty)
            {
                case MatchDifficulty.Easy:
                    return new MatchDifficultyProfile(
                        6, 7f, 8f, 1.25f, 0.6f, 2f, 2.8f, 0.55f,
                        1.4f, 4f, 120f, 25f, 1f, 420f);
                case MatchDifficulty.Hard:
                    return new MatchDifficultyProfile(
                        5, 9f, 10f, 0.5f, 1.5f, 5f, 3.2f, 0.25f,
                        0.75f, 2f, 45f, 8f, 1.5f, 300f);
                default:
                    // Current serialized values in StalkerNetwork.prefab.
                    return new MatchDifficultyProfile(
                        5, 8f, 9f, 0.75f, 1f, 3f, 3f, 0.35f,
                        1f, 3f, 75f, 15f, 1.25f, 300f);
            }
        }
    }
}
