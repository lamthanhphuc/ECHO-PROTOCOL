using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EchoProtocol.AI.Common.AED
{
    public enum AEDSurvivalOutcomeKindV1
    {
        Downed,
        Revived,
        DirectElimination,
        BleedoutElimination,
        TeamElimination,
        OtherElimination
    }

    public sealed class AEDSurvivalOutcomeV1
    {
        public Guid MatchId { get; }
        public uint PhaseOrdinal { get; }
        public string PhaseName { get; }
        public AEDSurvivalOutcomeKindV1 Kind { get; }
        public string UserId { get; }
        public string RelatedUserId { get; }
        public string OccurrenceKey { get; }
        public uint TransitionOrdinal { get; }
        public string AuthoritativeCause { get; }
        public bool DirectFromHit { get; }
        public string CanonicalEventId { get; }
        public string SourceAuthority { get; }

        internal AEDSurvivalOutcomeV1(Guid matchId, uint phaseOrdinal,
            string phaseName, AEDSurvivalOutcomeKindV1 kind, string userId,
            string relatedUserId, string occurrenceKey, uint transitionOrdinal,
            string authoritativeCause, bool directFromHit,
            string canonicalEventId, string sourceAuthority)
        {
            MatchId = matchId;
            PhaseOrdinal = phaseOrdinal;
            PhaseName = phaseName;
            Kind = kind;
            UserId = userId;
            RelatedUserId = relatedUserId;
            OccurrenceKey = occurrenceKey;
            TransitionOrdinal = transitionOrdinal;
            AuthoritativeCause = authoritativeCause;
            DirectFromHit = directFromHit;
            CanonicalEventId = canonicalEventId;
            SourceAuthority = sourceAuthority;
        }
    }

    public sealed class AEDSurvivalEvidenceSnapshotV1
    {
        public Guid MatchId { get; }
        public uint PhaseOrdinal { get; }
        public string PhaseName { get; }
        public IReadOnlyList<AEDSurvivalOutcomeV1> Outcomes { get; }
        public bool IsIncomplete { get; }
        public bool IsFrozen => true;

        internal AEDSurvivalEvidenceSnapshotV1(Guid matchId, uint phaseOrdinal,
            string phaseName, IEnumerable<AEDSurvivalOutcomeV1> outcomes,
            bool isIncomplete)
        {
            MatchId = matchId;
            PhaseOrdinal = phaseOrdinal;
            PhaseName = phaseName;
            Outcomes = new ReadOnlyCollection<AEDSurvivalOutcomeV1>(
                new List<AEDSurvivalOutcomeV1>(outcomes));
            IsIncomplete = isIncomplete;
        }
    }
}
