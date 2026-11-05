using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EchoProtocol.AI.Common.AED
{
    public enum AEDMinionTerminalV1 { Evaded, Countered, Disengaged, Cancelled, Censored }
    public enum AEDMinionFactKindV1
    {
        StateChanged, AlertAttempted, AlertAccepted, NoiseMakerOpportunity,
        NoiseMakerReaction,
        AttackAttempted, SlowApplied, ToolRelocated, CoreForcedDrop,
        CoreStolen, ItemRecovered, FlashlightContribution, TeamDeathReceipt
    }

    public sealed class AEDMinionFactV1
    {
        public AEDMinionFactKindV1 Kind { get; }
        public string EpisodeId { get; }
        public string MinionNetworkId { get; }
        public string OccurrenceKey { get; }
        public string SourceEventId { get; }
        public string UserId { get; }
        public string RelatedUserId { get; }
        public string ObjectId { get; }
        public string EffectKind { get; }
        public long SourceTick { get; }
        public long AttemptOrdinal { get; }
        public bool Accepted { get; }
        public double Seconds { get; }
        public float PositionX { get; }
        public float PositionY { get; }
        public float PositionZ { get; }
        public string SourceAuthority { get; }

        internal AEDMinionFactV1(AEDMinionFactKindV1 kind, string episodeId,
            string minionNetworkId, string occurrenceKey, string sourceEventId,
            string userId, string relatedUserId, string objectId,
            string effectKind, long sourceTick, long attemptOrdinal,
            bool accepted, double seconds, float positionX, float positionY,
            float positionZ, string sourceAuthority)
        {
            Kind = kind; EpisodeId = episodeId; MinionNetworkId = minionNetworkId;
            OccurrenceKey = occurrenceKey; SourceEventId = sourceEventId;
            UserId = userId; RelatedUserId = relatedUserId; ObjectId = objectId;
            EffectKind = effectKind; SourceTick = sourceTick;
            AttemptOrdinal = attemptOrdinal; Accepted = accepted; Seconds = seconds;
            PositionX = positionX; PositionY = positionY; PositionZ = positionZ;
            SourceAuthority = sourceAuthority;
        }
    }

    public sealed class AEDMinionParticipantSegmentV1
    {
        public string SegmentId { get; }
        public string UserId { get; }
        public long StartTick { get; }
        public long? EndTick { get; }
        internal AEDMinionParticipantSegmentV1(string segmentId, string userId,
            long startTick, long? endTick)
        { SegmentId = segmentId; UserId = userId; StartTick = startTick; EndTick = endTick; }
    }

    public sealed class AEDMinionStateSegmentV1
    {
        public string State { get; }
        public long StartTick { get; }
        public long? EndTick { get; }
        internal AEDMinionStateSegmentV1(string state, long startTick, long? endTick)
        { State = state; StartTick = startTick; EndTick = endTick; }
    }

    public sealed class AEDMinionEncounterV1
    {
        public string EpisodeId { get; }
        public Guid MatchId { get; }
        public uint PhaseOrdinal { get; }
        public string PhaseName { get; }
        public string Zone { get; }
        public string MinionNetworkId { get; }
        public long StartedTick { get; }
        public long? EndedTick { get; }
        public AEDMinionTerminalV1? TerminalOutcome { get; }
        public string TerminalReason { get; }
        public IReadOnlyList<AEDMinionParticipantSegmentV1> Participants { get; }
        public IReadOnlyList<AEDMinionStateSegmentV1> StateSegments { get; }
        public IReadOnlyList<AEDMinionFactV1> Facts { get; }
        public AEDMetricPolicyContextV1 PolicyContext { get; }
        public string SourceAuthority { get; }
        public bool IsTerminal => TerminalOutcome.HasValue;

        internal AEDMinionEncounterV1(string episodeId, Guid matchId,
            uint phaseOrdinal, string phaseName, string zone,
            string minionNetworkId, long startedTick, long? endedTick,
            AEDMinionTerminalV1? terminalOutcome, string terminalReason,
            IEnumerable<AEDMinionParticipantSegmentV1> participants,
            IEnumerable<AEDMinionStateSegmentV1> stateSegments,
            IEnumerable<AEDMinionFactV1> facts,
            AEDMetricPolicyContextV1 policyContext)
        {
            EpisodeId = episodeId; MatchId = matchId; PhaseOrdinal = phaseOrdinal;
            PhaseName = phaseName; Zone = zone; MinionNetworkId = minionNetworkId;
            StartedTick = startedTick; EndedTick = endedTick;
            TerminalOutcome = terminalOutcome; TerminalReason = terminalReason;
            Participants = new ReadOnlyCollection<AEDMinionParticipantSegmentV1>(
                new List<AEDMinionParticipantSegmentV1>(participants));
            StateSegments = new ReadOnlyCollection<AEDMinionStateSegmentV1>(
                new List<AEDMinionStateSegmentV1>(stateSegments));
            Facts = new ReadOnlyCollection<AEDMinionFactV1>(
                new List<AEDMinionFactV1>(facts));
            PolicyContext = policyContext;
            SourceAuthority = "FusionStateAuthority";
        }
    }

    public sealed class AEDMinionEvidenceSnapshotV1
    {
        public Guid MatchId { get; }
        public uint PhaseOrdinal { get; }
        public string PhaseName { get; }
        public IReadOnlyList<AEDMinionEncounterV1> Episodes { get; }
        public IReadOnlyList<AEDMinionFactV1> Facts { get; }
        public AEDMetricPolicyContextV1 PolicyContext { get; }
        public bool IsIncomplete { get; }
        public bool IsInvalid { get; }
        public bool IsFrozen => true;

        internal AEDMinionEvidenceSnapshotV1(Guid matchId, uint phaseOrdinal,
            string phaseName, IEnumerable<AEDMinionEncounterV1> episodes,
            IEnumerable<AEDMinionFactV1> facts, bool incomplete, bool invalid,
            AEDMetricPolicyContextV1 policyContext)
        {
            MatchId = matchId; PhaseOrdinal = phaseOrdinal; PhaseName = phaseName;
            Episodes = new ReadOnlyCollection<AEDMinionEncounterV1>(
                new List<AEDMinionEncounterV1>(episodes));
            Facts = new ReadOnlyCollection<AEDMinionFactV1>(new List<AEDMinionFactV1>(facts));
            PolicyContext = policyContext;
            IsIncomplete = incomplete; IsInvalid = invalid;
        }
    }
}
