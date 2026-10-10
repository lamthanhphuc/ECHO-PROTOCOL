using System;
using System.Collections.Generic;
using System.Linq;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class AEDPursuitEvidenceCollectorV1
    {
        public const string GraceWindowVersion = "PURSUIT_ESCAPE_GRACE_V1";
        public const int DefaultEscapeGraceSeconds = 5;
        private const string HostAuthority = "FusionStateAuthority";

        private sealed class ActiveEpisode
        {
            public string Id;
            public string StalkerId;
            public string TargetId;
            public long StartedTick;
            public long? LostTick;
            public bool LostWindowPending;
            public int EligibleLostWindows;
            public int ResolvedLostWindows;
            public int ReacquisitionCount;
            public readonly List<AEDPursuitFactV1> Facts = new List<AEDPursuitFactV1>();
            public readonly List<AEDPursuitStateSegmentV1> StateSegments = new List<AEDPursuitStateSegmentV1>();
            public string OpenState;
            public long OpenStateTick;
        }

        private readonly List<AEDPursuitEpisodeV1> _episodes = new();
        private readonly HashSet<string> _occurrences = new(StringComparer.Ordinal);
        private Guid _matchId;
        private uint _phaseOrdinal;
        private string _phaseName;
        private AEDMetricPolicyContextV1 _policyContext;
        private ActiveEpisode _active;
        private long _episodeOrdinal;
        private bool _incomplete;
        private bool _invalid;
        private bool _frozen;
        private long _lastSourceTick;

        public AEDPursuitEvidenceSnapshotV1 LastFrozen { get; private set; }
        public bool IsIncomplete => _incomplete;
        public void MarkIncomplete() { if (!_frozen) _incomplete = true; }

        public void ResetMatch(Guid matchId)
        {
            Clear();
            _matchId = matchId;
            if (matchId == Guid.Empty) _invalid = true;
        }

        public void StartPhase(Guid matchId, uint phaseOrdinal,
            string phaseName, AEDMetricPolicyContextV1 policyContext = null)
        {
            if (matchId == Guid.Empty || phaseOrdinal == 0
                || string.IsNullOrWhiteSpace(phaseName))
            {
                _invalid = true;
                return;
            }

            if (_matchId != Guid.Empty && _matchId != matchId) ResetMatch(matchId);
            if (_phaseOrdinal != 0 && !_frozen)
            {
                CensorActive("PHASE_END", _lastSourceTick);
                Freeze();
            }

            _episodes.Clear();
            _occurrences.Clear();
            _active = null;
            _phaseOrdinal = phaseOrdinal;
            _phaseName = phaseName;
            _policyContext = policyContext;
            _incomplete = false;
            _invalid = false;
            _frozen = false;
            _matchId = matchId;
            _lastSourceTick = 0;
        }

        public bool Observe(bool hostAuthority, string stalkerNetworkId,
            string targetUserId, string state, long tick, int tickRate,
            string sourceOccurrenceKey)
        {
            if (_frozen) return false;
            if (!hostAuthority || string.IsNullOrWhiteSpace(stalkerNetworkId)
                || tick < 0 || tickRate <= 0
                || string.IsNullOrWhiteSpace(sourceOccurrenceKey))
            {
                _invalid = true;
                return false;
            }

            if (!AcceptOccurrence("STATE", sourceOccurrenceKey)) return false;
            _lastSourceTick = Math.Max(_lastSourceTick, tick);
            bool targetValid = Guid.TryParse(targetUserId, out var targetGuid)
                && targetGuid != Guid.Empty;
            string target = targetValid ? targetGuid.ToString("D") : null;
            bool chase = string.Equals(state, "CHASE", StringComparison.Ordinal);

            if (chase && !targetValid)
            {
                _incomplete = true;
                return false;
            }

            if (_active != null && !string.Equals(_active.StalkerId,
                    stalkerNetworkId, StringComparison.Ordinal))
                CensorActive("AUTHORITY_SOURCE_CHANGED", tick);

            if (_active != null && chase
                && !string.Equals(_active.TargetId, target, StringComparison.Ordinal))
                Close(AEDPursuitTerminalV1.TargetSwitched, "TARGET_SWITCHED", tick);

            if (chase)
            {
                if (_active == null) Begin(stalkerNetworkId, target, tick);
                else
                {
                    RecordState(_active, state, tick);
                    if (_active.LostTick.HasValue && _active.LostWindowPending)
                    {
                        _active.ResolvedLostWindows++;
                        _active.ReacquisitionCount++;
                        _active.Facts.Add(new AEDPursuitFactV1(
                            AEDPursuitFactKindV1.Reacquired,
                            sourceOccurrenceKey, tick, HostAuthority, "CHASE_REACQUIRED"));
                        _active.LostWindowPending = false;
                    }
                    _active.LostTick = null;
                }
                return true;
            }

            if (_active == null) return false;
            if (!string.Equals(_active.StalkerId, stalkerNetworkId, StringComparison.Ordinal))
                return false;
            RecordState(_active, state, tick);

            if ((state == "SEARCH" || state == "PATROL") && !_active.LostTick.HasValue)
            {
                _active.LostTick = tick;
                _active.LostWindowPending = true;
                _active.EligibleLostWindows++;
            }

            if ((state == "SEARCH" || state == "PATROL")
                && _active.LostTick.HasValue
                && tick - _active.LostTick.Value >= (long)tickRate * DefaultEscapeGraceSeconds)
            {
                if (_active.LostWindowPending)
                {
                    _active.ResolvedLostWindows++;
                    _active.LostWindowPending = false;
                }
                Close(AEDPursuitTerminalV1.Escaped,
                    $"{GraceWindowVersion}:{DefaultEscapeGraceSeconds}", tick);
            }
            return true;
        }

        public bool RecordConsequence(string targetUserId,
            AEDPursuitFactKindV1 kind, string occurrenceKey, long tick,
            string cause, bool directFromHit)
        {
            if (_frozen) return false;
            if (!Guid.TryParse(targetUserId, out var target) || target == Guid.Empty
                || string.IsNullOrWhiteSpace(occurrenceKey) || tick < 0
                || kind == AEDPursuitFactKindV1.Reacquired
                || string.IsNullOrWhiteSpace(cause)
                || (directFromHit && kind != AEDPursuitFactKindV1.Eliminated))
            {
                _invalid = true;
                return false;
            }
            if (!AcceptOccurrence(kind.ToString(), occurrenceKey)) return false;
            _lastSourceTick = Math.Max(_lastSourceTick, tick);
            if (_active == null || _active.TargetId != target.ToString("D"))
            {
                _incomplete = true;
                return false;
            }

            _active.Facts.Add(new AEDPursuitFactV1(kind, occurrenceKey,
                tick, HostAuthority, cause, directFromHit));
            if (kind == AEDPursuitFactKindV1.Downed)
                Close(AEDPursuitTerminalV1.Downed, cause, tick);
            else if (kind == AEDPursuitFactKindV1.Eliminated)
                Close(AEDPursuitTerminalV1.Eliminated, cause, tick);
            return true;
        }

        public void CensorTarget(string targetUserId, string reason, long tick)
        {
            if (_active != null && _active.TargetId == targetUserId)
                CensorActive(reason, tick);
        }

        public void CensorStalker(string stalkerNetworkId, string reason, long tick)
        {
            if (_active != null && _active.StalkerId == stalkerNetworkId)
                CensorActive(reason, tick);
        }

        public void CensorActive(string reason, long tick)
        {
            if (_active == null || _frozen) return;
            Close(string.IsNullOrWhiteSpace(reason)
                    ? AEDPursuitTerminalV1.Incomplete
                    : AEDPursuitTerminalV1.Censored,
                reason, Math.Max(_active.StartedTick, tick));
            if (string.IsNullOrWhiteSpace(reason)) _incomplete = true;
        }

        public AEDPursuitEvidenceSnapshotV1 Freeze()
        {
            if (_frozen) return LastFrozen;
            if (_active != null) CensorActive("PHASE_END", _lastSourceTick);
            LastFrozen = new AEDPursuitEvidenceSnapshotV1(_matchId,
                _phaseOrdinal, _phaseName, _episodes, _incomplete, _invalid,
                _policyContext);
            _frozen = true;
            return LastFrozen;
        }

        public void Clear()
        {
            _episodes.Clear();
            _occurrences.Clear();
            _active = null;
            _matchId = Guid.Empty;
            _phaseOrdinal = 0;
            _phaseName = null;
            _policyContext = null;
            _episodeOrdinal = 0;
            _lastSourceTick = 0;
            _incomplete = false;
            _invalid = false;
            _frozen = false;
            LastFrozen = null;
        }

        private bool AcceptOccurrence(string kind, string occurrenceKey)
        {
            if (_frozen) return false;
            return _occurrences.Add($"{_matchId:D}|{_phaseOrdinal}|{kind}|{occurrenceKey}");
        }

        private void Begin(string stalkerId, string targetId, long tick)
        {
            _active = new ActiveEpisode
            {
                Id = $"{_matchId:D}:{_phaseOrdinal}:{stalkerId}:{targetId}:{tick}:{++_episodeOrdinal}",
                StalkerId = stalkerId,
                TargetId = targetId,
                StartedTick = tick
            };
            _active.OpenState = "CHASE";
            _active.OpenStateTick = tick;
        }

        private void Close(AEDPursuitTerminalV1 outcome, string reason, long tick)
        {
            if (_active == null) return;
            _active.StateSegments.Add(new AEDPursuitStateSegmentV1(
                _active.OpenState, _active.OpenStateTick,
                Math.Max(_active.OpenStateTick, tick)));
            _episodes.Add(new AEDPursuitEpisodeV1(_active.Id, _matchId,
                _phaseOrdinal, _phaseName, null, _active.TargetId,
                _active.StalkerId, _active.StartedTick, Math.Max(_active.StartedTick, tick),
                outcome, reason, _policyContext, _active.Facts,
                _active.StateSegments,
                _active.EligibleLostWindows, _active.ResolvedLostWindows,
                _active.ReacquisitionCount, HostAuthority));
            _active = null;
        }

        private static void RecordState(ActiveEpisode episode, string state, long tick)
        {
            if (string.Equals(episode.OpenState, state, StringComparison.Ordinal)) return;
            episode.StateSegments.Add(new AEDPursuitStateSegmentV1(
                episode.OpenState, episode.OpenStateTick,
                Math.Max(episode.OpenStateTick, tick)));
            episode.OpenState = state;
            episode.OpenStateTick = tick;
        }
    }
}
