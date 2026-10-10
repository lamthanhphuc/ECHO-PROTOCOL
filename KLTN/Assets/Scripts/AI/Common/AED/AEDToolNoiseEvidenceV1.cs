using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EchoProtocol.AI.Common.AED
{
    public enum AEDToolNoiseFactKindV1
    {
        ToolActionAccepted,
        ToolEffectResolved,
        GameplayNoiseAccepted
    }

    public enum AEDEvidenceSourceCategoryV1
    {
        CanonicalTelemetryAccepted,
        GameplayOnlyAccepted,
        CanonicalEmissionRejected
    }

    public enum AEDToolEffectOutcomeV1
    {
        ResolvedSuccess,
        ResolvedFailure
    }

    public sealed class AEDToolNoiseFactV1
    {
        public AEDToolNoiseFactKindV1 Kind { get; }
        public AEDEvidenceSourceCategoryV1 SourceCategory { get; }
        public string ToolType { get; }
        public string NoiseType { get; }
        public string UserId { get; }
        public string RelatedUserId { get; }
        public string OccurrenceKey { get; }
        public string CanonicalEventId { get; }
        public AEDToolEffectOutcomeV1? EffectOutcome { get; }
        public string SourceAuthority { get; }

        internal AEDToolNoiseFactV1(AEDToolNoiseFactKindV1 kind,
            AEDEvidenceSourceCategoryV1 sourceCategory, string toolType,
            string noiseType, string userId, string relatedUserId,
            string occurrenceKey, string canonicalEventId,
            AEDToolEffectOutcomeV1? effectOutcome, string sourceAuthority)
        {
            Kind = kind;
            SourceCategory = sourceCategory;
            ToolType = toolType;
            NoiseType = noiseType;
            UserId = userId;
            RelatedUserId = relatedUserId;
            OccurrenceKey = occurrenceKey;
            CanonicalEventId = canonicalEventId;
            EffectOutcome = effectOutcome;
            SourceAuthority = sourceAuthority;
        }
    }

    public sealed class AEDToolNoiseEvidenceSnapshotV1
    {
        public Guid MatchId { get; }
        public uint PhaseOrdinal { get; }
        public string PhaseName { get; }
        public IReadOnlyList<AEDToolNoiseFactV1> Facts { get; }
        public bool IsIncomplete { get; }
        public bool IsFrozen => true;

        internal AEDToolNoiseEvidenceSnapshotV1(Guid matchId,
            uint phaseOrdinal, string phaseName,
            IEnumerable<AEDToolNoiseFactV1> facts, bool isIncomplete)
        {
            MatchId = matchId;
            PhaseOrdinal = phaseOrdinal;
            PhaseName = phaseName;
            Facts = new ReadOnlyCollection<AEDToolNoiseFactV1>(
                new List<AEDToolNoiseFactV1>(facts));
            IsIncomplete = isIncomplete;
        }
    }
}
