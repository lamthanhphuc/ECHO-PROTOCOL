using System.Collections.Generic;
using EchoProtocol.AI.Common;

namespace EchoProtocol.AI.Stalker
{
    public readonly struct StalkerSimulationInput
    {
        public StalkerSimulationInput(
            AiSimulationStep step,
            IReadOnlyList<StalkerTargetCandidate> visibleTargetCandidates)
        {
            Step = step;
            VisibleTargetCandidates = visibleTargetCandidates;
        }

        public AiSimulationStep Step { get; }

        public IReadOnlyList<StalkerTargetCandidate> VisibleTargetCandidates { get; }
    }
}
