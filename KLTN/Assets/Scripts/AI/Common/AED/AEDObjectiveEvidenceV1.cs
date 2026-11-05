using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class AEDObjectiveEvidenceV1
    {
        public const string SourceSystemName = "UNITY_FUSION_HOST_OBJECTIVE";

        public sealed class Unit
        {
            public string ObjectiveUnitId { get; }
            public string UnitType { get; }
            public string PhaseName { get; }
            public string Stage { get; }
            public string SectorId { get; }
            public int PlacementSlot { get; }
            public bool Accepted { get; }
            public string SourceCanonicalEventId { get; }
            public string SourceOccurrenceKey { get; }
            public string CoreObjectId { get; }
            public uint TransitionOrdinal { get; }
            public string UserId { get; }
            public long CompletionTick { get; }
            public string SourceAuthority { get; }

            internal Unit(string objectiveUnitId, string unitType, string phaseName,
                string stage, string sectorId, int placementSlot, bool accepted,
                string sourceCanonicalEventId, string sourceOccurrenceKey,
                string coreObjectId, uint transitionOrdinal, string userId,
                long completionTick, string sourceAuthority)
            {
                ObjectiveUnitId = objectiveUnitId;
                UnitType = unitType;
                PhaseName = phaseName;
                Stage = stage;
                SectorId = sectorId;
                PlacementSlot = placementSlot;
                Accepted = accepted;
                SourceCanonicalEventId = sourceCanonicalEventId;
                SourceOccurrenceKey = sourceOccurrenceKey;
                CoreObjectId = coreObjectId;
                TransitionOrdinal = transitionOrdinal;
                UserId = userId;
                CompletionTick = completionTick;
                SourceAuthority = sourceAuthority;
            }
        }

        public Guid MatchId { get; }
        public uint PhaseOrdinal { get; }
        public string PhaseName { get; }
        public IReadOnlyList<Unit> Units { get; }
        public IReadOnlyList<string> OpportunityUnitIds { get; }
        public AEDMetricResultV1 UnitCompletionRate { get; }
        public bool IsFrozen { get; }

        internal AEDObjectiveEvidenceV1(Guid matchId, uint phaseOrdinal,
            string phaseName, IEnumerable<Unit> units,
            IEnumerable<string> opportunityUnitIds, AEDMetricResultV1 metric)
        {
            MatchId = matchId;
            PhaseOrdinal = phaseOrdinal;
            PhaseName = phaseName;
            Units = new ReadOnlyCollection<Unit>(new List<Unit>(units));
            OpportunityUnitIds = new ReadOnlyCollection<string>(
                new List<string>(opportunityUnitIds));
            UnitCompletionRate = metric;
            IsFrozen = true;
        }
    }
}
