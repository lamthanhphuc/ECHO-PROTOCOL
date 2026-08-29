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
        {
            Step = step;
            VisibleTargetCandidates = visibleTargetCandidates;
            TargetStatuses = targetStatuses;
        }

        public AiSimulationStep Step { get; }

        public IReadOnlyList<StalkerTargetCandidate> VisibleTargetCandidates { get; }

        public IReadOnlyList<StalkerTargetStatus> TargetStatuses { get; }
    }
}
