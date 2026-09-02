using EchoProtocol.AI.Common;

namespace EchoProtocol.AI.Stalker.Networking
{
    public enum StalkerNetworkAttackPhase
    {
        None,
        Windup,
        Resolved,
        Recover
    }

    public readonly struct StalkerNetworkPresentationState
    {
        public StalkerNetworkPresentationState(
            StalkerState semanticState,
            StalkerAttackEpisodeId attackEpisodeId,
            StalkerNetworkAttackPhase attackPhase,
            float attackProgressSeconds,
            bool attackHitMomentResolved,
            StalkerAttackOutcome attackOutcome,
            long attackStartedTick,
            long attackResolvedTick)
        {
            SemanticState = semanticState;
            AttackEpisodeId = attackEpisodeId;
            AttackPhase = attackPhase;
            AttackProgressSeconds = attackProgressSeconds;
            AttackHitMomentResolved = attackHitMomentResolved;
            AttackOutcome = attackOutcome;
            AttackStartedTick = attackStartedTick;
            AttackResolvedTick = attackResolvedTick;
        }

        public StalkerState SemanticState { get; }
        public StalkerAttackEpisodeId AttackEpisodeId { get; }
        public StalkerNetworkAttackPhase AttackPhase { get; }
        public float AttackProgressSeconds { get; }
        public bool AttackHitMomentResolved { get; }
        public StalkerAttackOutcome AttackOutcome { get; }
        public long AttackStartedTick { get; }
        public long AttackResolvedTick { get; }

        public bool HasAttackEpisode => AttackEpisodeId.IsValid;
    }
}
