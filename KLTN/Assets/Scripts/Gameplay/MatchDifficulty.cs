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
            int maximumRevivesPerZone,
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
            MaximumRevivesPerZone = maximumRevivesPerZone;
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
        public int MaximumRevivesPerZone { get; }
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
                        6,
                        5.0f,
                        5.0f,
                        2.0f,
                        20f,
                        1.0f,
                        2.2f,
                        0.85f,
                        2.0f,
                        8f,
                        240f,
                        35f,
                        0.5f,
                        600f,
                        1,
                        0,
                        4,
                        false);
                case MatchDifficulty.Hard:
                    return new MatchDifficultyProfile(
                        5, 8f, 9f, 0.75f, 30f, 3f, 3f, 0.35f,
                        1f, 3f, 75f, 15f, 1.25f, 300f, 1, 1, 2, true);
                default:
                    return new MatchDifficultyProfile(
                        5, 6.5f, 8f, 1.25f, 25f, 2.25f, 2.7f, 0.6f,
                        1.4f, 4.5f, 120f, 25f, 1f, 420f, 1, 2, 3, true);
            }
        }
    }
}
