using EchoProtocol.AI.Common;

namespace EchoProtocol.AI.Stalker.Telemetry
{
    public enum StalkerSearchTerminalOutcome
    {
        SAME_TARGET_REACQUIRED,
        NEW_ELIGIBLE_TARGET_OBSERVED,
        TIMEOUT,
        CURRENT_TARGET_INVALID_NO_REPLACEMENT
    }

    public readonly struct StalkerAttackResolvedFact
    {
        public StalkerAttackResolvedFact(
            StalkerAttackEpisodeId episodeId,
            StalkerAttackOutcome outcome,
            AiSimulationTime resolvedAt)
        {
            EpisodeId = episodeId;
            Outcome = outcome;
            ResolvedAt = resolvedAt;
        }

        public StalkerAttackEpisodeId EpisodeId { get; }
        public StalkerAttackOutcome Outcome { get; }
        public AiSimulationTime ResolvedAt { get; }
        public bool IsValid => EpisodeId.IsValid
            && ResolvedAt.IsValid
            && (Outcome == StalkerAttackOutcome.Hit || Outcome == StalkerAttackOutcome.Miss);
    }

    public readonly struct StalkerSearchEndedFact
    {
        public StalkerSearchEndedFact(
            SearchEpisodeId episodeId,
            StalkerSearchTerminalOutcome outcome,
            AiSimulationTime endedAt)
        {
            EpisodeId = episodeId;
            Outcome = outcome;
            EndedAt = endedAt;
        }

        public SearchEpisodeId EpisodeId { get; }
        public StalkerSearchTerminalOutcome Outcome { get; }
        public AiSimulationTime EndedAt { get; }
        public bool IsValid => EpisodeId.IsValid
            && EndedAt.IsValid
            && IsDefinedOutcome(Outcome);

        private static bool IsDefinedOutcome(StalkerSearchTerminalOutcome outcome)
        {
            switch (outcome)
            {
                case StalkerSearchTerminalOutcome.SAME_TARGET_REACQUIRED:
                case StalkerSearchTerminalOutcome.NEW_ELIGIBLE_TARGET_OBSERVED:
                case StalkerSearchTerminalOutcome.TIMEOUT:
                case StalkerSearchTerminalOutcome.CURRENT_TARGET_INVALID_NO_REPLACEMENT:
                    return true;
                default:
                    return false;
            }
        }
    }
}
