using System.Collections.Generic;
using EchoProtocol.AI.Common;

namespace EchoProtocol.AI.Stalker
{
    public readonly struct StalkerSimulationInput
    {
        public StalkerSimulationInput(
            AiSimulationStep step,
            IReadOnlyList<StalkerTargetCandidate> visibleTargetCandidates)
            : this(step, visibleTargetCandidates, null)
        {
        }

        public StalkerSimulationInput(
            AiSimulationStep step,
            IReadOnlyList<StalkerTargetCandidate> visibleTargetCandidates,
            IReadOnlyList<StalkerTargetStatus> targetStatuses)
            : this(step, visibleTargetCandidates, targetStatuses, null)
        {
        }

        public StalkerSimulationInput(
            AiSimulationStep step,
            IReadOnlyList<StalkerTargetCandidate> visibleTargetCandidates,
            IReadOnlyList<StalkerTargetStatus> targetStatuses,
            StalkerAttackTargetSnapshot? currentAttackTargetSnapshot)
        {
            Step = step;
            VisibleTargetCandidates = visibleTargetCandidates;
            TargetStatuses = targetStatuses;
            CurrentAttackTargetSnapshot = currentAttackTargetSnapshot;
        }

        public AiSimulationStep Step { get; }

        public IReadOnlyList<StalkerTargetCandidate> VisibleTargetCandidates { get; }

        public IReadOnlyList<StalkerTargetStatus> TargetStatuses { get; }

        public StalkerAttackTargetSnapshot? CurrentAttackTargetSnapshot { get; }
    }
}
