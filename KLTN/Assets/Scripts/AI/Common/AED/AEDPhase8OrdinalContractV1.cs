namespace EchoProtocol.AI.Common.AED
{
    public sealed class AEDPhase8OrdinalContextV1
    {
        public uint CompletedEvidenceOrdinal { get; }
        public uint BackendDecisionOrdinal { get; }
        public uint NetworkOrdinalBeforeTransition { get; }
        public uint NetworkOrdinalAfterTransition { get; }
        public uint ResearchTargetPhaseOrdinal { get; }

        internal AEDPhase8OrdinalContextV1(uint completed)
        {
            CompletedEvidenceOrdinal = completed;
            BackendDecisionOrdinal = completed;
            NetworkOrdinalBeforeTransition = completed - 1u;
            NetworkOrdinalAfterTransition = completed;
            ResearchTargetPhaseOrdinal = completed + 1u;
        }
    }

    public static class AEDPhase8OrdinalContractV1
    {
        public static bool TryCreate(uint completedEvidenceOrdinal, uint networkOrdinalBeforeTransition, out AEDPhase8OrdinalContextV1 context)
        {
            context = null;
            if (completedEvidenceOrdinal == 0 || completedEvidenceOrdinal == uint.MaxValue || networkOrdinalBeforeTransition == uint.MaxValue || completedEvidenceOrdinal != networkOrdinalBeforeTransition + 1u)
                return false;
            context = new AEDPhase8OrdinalContextV1(completedEvidenceOrdinal);
            return true;
        }
    }
}
