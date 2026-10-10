using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EchoProtocol.AI.Common.AED
{
    public enum AEDPursuitTerminalV1
    {
        Escaped,
        Downed,
        Eliminated,
        TargetSwitched,
        Censored,
        Cancelled,
        Incomplete
    }

    public enum AEDPursuitFactKindV1
    {
        Reacquired,
        Hit,
        Downed,
        Eliminated
    }

    public sealed class AEDPursuitFactV1
    {
        public AEDPursuitFactKindV1 Kind { get; }
        public string OccurrenceKey { get; }
        public long SourceTick { get; }
        public string SourceAuthority { get; }
        public string Cause { get; }
        public bool DirectFromHit { get; }

        internal AEDPursuitFactV1(AEDPursuitFactKindV1 kind,
            string occurrenceKey, long sourceTick, string sourceAuthority,
            string cause = null, bool directFromHit = false)
        {
            Kind = kind;
            OccurrenceKey = occurrenceKey;
            SourceTick = sourceTick;
            SourceAuthority = sourceAuthority;
            Cause = cause;
            DirectFromHit = directFromHit;
        }
    }

    public sealed class AEDPursuitStateSegmentV1
    {
        public string State { get; }
        public long StartTick { get; }
        public long EndTick { get; }

        internal AEDPursuitStateSegmentV1(string state, long startTick, long endTick)
        { State = state; StartTick = startTick; EndTick = endTick; }
    }

    public sealed class AEDPursuitEpisodeV1
    {
        public string EpisodeId { get; }
        public Guid MatchId { get; }
        public uint PhaseOrdinal { get; }
        public string PhaseName { get; }
        public string Zone { get; }
        public string TargetUserId { get; }
        public string StalkerNetworkId { get; }
        public long StartedTick { get; }
        public long? EndedTick { get; }
        public AEDPursuitTerminalV1? TerminalOutcome { get; }
        public string TerminalReason { get; }
        public AEDMetricPolicyContextV1 PolicyContext { get; }
        public IReadOnlyList<AEDPursuitFactV1> Facts { get; }
        public IReadOnlyList<AEDPursuitStateSegmentV1> StateSegments { get; }
        public int EligibleLostWindows { get; }
        public int ResolvedLostWindows { get; }
        public int ReacquisitionCount { get; }
        public string SourceAuthority { get; }
        public bool IsTerminal => TerminalOutcome.HasValue;

        internal AEDPursuitEpisodeV1(string episodeId, Guid matchId,
            uint phaseOrdinal, string phaseName, string zone,
            string targetUserId, string stalkerNetworkId, long startedTick,
            long? endedTick, AEDPursuitTerminalV1? terminalOutcome,
            string terminalReason, AEDMetricPolicyContextV1 policyContext,
            IEnumerable<AEDPursuitFactV1> facts,
            IEnumerable<AEDPursuitStateSegmentV1> stateSegments,
            int eligibleLostWindows,
            int resolvedLostWindows, int reacquisitionCount,
            string sourceAuthority)
        {
            EpisodeId = episodeId;
            MatchId = matchId;
            PhaseOrdinal = phaseOrdinal;
            PhaseName = phaseName;
            Zone = zone;
            TargetUserId = targetUserId;
            StalkerNetworkId = stalkerNetworkId;
            StartedTick = startedTick;
            EndedTick = endedTick;
            TerminalOutcome = terminalOutcome;
            TerminalReason = terminalReason;
            PolicyContext = policyContext;
            Facts = new ReadOnlyCollection<AEDPursuitFactV1>(
                new List<AEDPursuitFactV1>(facts));
            StateSegments = new ReadOnlyCollection<AEDPursuitStateSegmentV1>(
                new List<AEDPursuitStateSegmentV1>(stateSegments));
            EligibleLostWindows = eligibleLostWindows;
            ResolvedLostWindows = resolvedLostWindows;
            ReacquisitionCount = reacquisitionCount;
            SourceAuthority = sourceAuthority;
        }
    }

    public sealed class AEDPursuitEvidenceSnapshotV1
    {
        public Guid MatchId { get; }
        public uint PhaseOrdinal { get; }
        public string PhaseName { get; }
        public IReadOnlyList<AEDPursuitEpisodeV1> Episodes { get; }
        public AEDMetricPolicyContextV1 PolicyContext { get; }
        public bool IsIncomplete { get; }
        public bool IsInvalid { get; }
        public bool IsFrozen => true;

        internal AEDPursuitEvidenceSnapshotV1(Guid matchId,
            uint phaseOrdinal, string phaseName,
            IEnumerable<AEDPursuitEpisodeV1> episodes,
            bool isIncomplete, bool isInvalid,
            AEDMetricPolicyContextV1 policyContext)
        {
            MatchId = matchId;
            PhaseOrdinal = phaseOrdinal;
            PhaseName = phaseName;
            Episodes = new ReadOnlyCollection<AEDPursuitEpisodeV1>(
                new List<AEDPursuitEpisodeV1>(episodes));
            PolicyContext = policyContext;
            IsIncomplete = isIncomplete;
            IsInvalid = isInvalid;
        }
    }
}
