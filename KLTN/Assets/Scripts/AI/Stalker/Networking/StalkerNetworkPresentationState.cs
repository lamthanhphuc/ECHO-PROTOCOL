using EchoProtocol.AI.Common;
using EchoProtocol.AI.Stalker.Presentation;
using EchoProtocol.AI.Stalker.Special;

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
            : this(
                semanticState,
                attackEpisodeId,
                attackPhase,
                attackProgressSeconds,
                attackHitMomentResolved,
                attackOutcome,
                attackStartedTick,
                attackResolvedTick,
                StalkerPresentationAction.None,
                0,
                0f,
                StalkerSpecialEncounterPhase.None,
                0u,
                0f,
                true)
        {
        }

        public StalkerNetworkPresentationState(
            StalkerState semanticState,
            StalkerAttackEpisodeId attackEpisodeId,
            StalkerNetworkAttackPhase attackPhase,
            float attackProgressSeconds,
            bool attackHitMomentResolved,
            StalkerAttackOutcome attackOutcome,
            long attackStartedTick,
            long attackResolvedTick,
            StalkerPresentationAction presentationAction = StalkerPresentationAction.None,
            int presentationActionOrdinal = 0,
            float presentationActionProgress01 = 0f,
            StalkerSpecialEncounterPhase specialPhase = StalkerSpecialEncounterPhase.None,
            uint specialSequenceOrdinal = 0,
            float specialPhaseElapsed = 0f,
            bool presentationVisible = true)
        {
            SemanticState = semanticState;
            AttackEpisodeId = attackEpisodeId;
            AttackPhase = attackPhase;
            AttackProgressSeconds = attackProgressSeconds;
            AttackHitMomentResolved = attackHitMomentResolved;
            AttackOutcome = attackOutcome;
            AttackStartedTick = attackStartedTick;
            AttackResolvedTick = attackResolvedTick;
            PresentationAction = presentationAction;
            PresentationActionOrdinal = presentationActionOrdinal;
            PresentationActionProgress01 = presentationActionProgress01;
            SpecialPhase = specialPhase;
            SpecialSequenceOrdinal = specialSequenceOrdinal;
            SpecialPhaseElapsed = specialPhaseElapsed;
            PresentationVisible = presentationVisible;
        }

        public StalkerState SemanticState { get; }
        public StalkerAttackEpisodeId AttackEpisodeId { get; }
        public StalkerNetworkAttackPhase AttackPhase { get; }
        public float AttackProgressSeconds { get; }
        public bool AttackHitMomentResolved { get; }
        public StalkerAttackOutcome AttackOutcome { get; }
        public long AttackStartedTick { get; }
        public long AttackResolvedTick { get; }
        public StalkerPresentationAction PresentationAction { get; }
        public int PresentationActionOrdinal { get; }
        public float PresentationActionProgress01 { get; }
        public StalkerSpecialEncounterPhase SpecialPhase { get; }
        public uint SpecialSequenceOrdinal { get; }
        public float SpecialPhaseElapsed { get; }
        public bool PresentationVisible { get; }

        public bool HasAttackEpisode => AttackEpisodeId.IsValid;
    }
}
