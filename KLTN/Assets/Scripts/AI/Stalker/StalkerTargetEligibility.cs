namespace EchoProtocol.AI.Stalker
{
    public readonly struct StalkerTargetEligibilitySnapshot
    {
        public StalkerTargetEligibilitySnapshot(
            bool isInActiveSession,
            bool isConnected,
            bool isDowned,
            bool isEliminated,
            bool hasOtherInvalidGameplayState)
        {
            IsInActiveSession = isInActiveSession;
            IsConnected = isConnected;
            IsDowned = isDowned;
            IsEliminated = isEliminated;
            HasOtherInvalidGameplayState = hasOtherInvalidGameplayState;
        }

        public bool IsInActiveSession { get; }

        public bool IsConnected { get; }

        public bool IsDowned { get; }

        public bool IsEliminated { get; }

        public bool HasOtherInvalidGameplayState { get; }
    }

    public enum StalkerTargetEligibilityReason
    {
        NotInActiveSession = 0,
        Eligible = 1,
        Disconnected = 2,
        Downed = 3,
        Eliminated = 4,
        OtherGameplayState = 5
    }

    public readonly struct StalkerTargetEligibilityResult
    {
        private StalkerTargetEligibilityResult(bool eligible, StalkerTargetEligibilityReason reason)
        {
            Eligible = eligible;
            Reason = reason;
        }

        public bool Eligible { get; }

        public StalkerTargetEligibilityReason Reason { get; }

        public static StalkerTargetEligibilityResult EligibleTarget()
        {
            return new StalkerTargetEligibilityResult(true, StalkerTargetEligibilityReason.Eligible);
        }

        public static StalkerTargetEligibilityResult Ineligible(StalkerTargetEligibilityReason reason)
        {
            if (reason == StalkerTargetEligibilityReason.Eligible)
            {
                return EligibleTarget();
            }

            return new StalkerTargetEligibilityResult(false, reason);
        }
    }

    public static class StalkerTargetEligibility
    {
        public static StalkerTargetEligibilityResult Evaluate(StalkerTargetEligibilitySnapshot snapshot)
        {
            if (!snapshot.IsInActiveSession)
            {
                return StalkerTargetEligibilityResult.Ineligible(StalkerTargetEligibilityReason.NotInActiveSession);
            }

            if (!snapshot.IsConnected)
            {
                return StalkerTargetEligibilityResult.Ineligible(StalkerTargetEligibilityReason.Disconnected);
            }

            if (snapshot.IsDowned)
            {
                return StalkerTargetEligibilityResult.Ineligible(StalkerTargetEligibilityReason.Downed);
            }

            if (snapshot.IsEliminated)
            {
                return StalkerTargetEligibilityResult.Ineligible(StalkerTargetEligibilityReason.Eliminated);
            }

            if (snapshot.HasOtherInvalidGameplayState)
            {
                return StalkerTargetEligibilityResult.Ineligible(StalkerTargetEligibilityReason.OtherGameplayState);
            }

            return StalkerTargetEligibilityResult.EligibleTarget();
        }
    }
}
