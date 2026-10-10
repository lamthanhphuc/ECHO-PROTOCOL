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
        private readonly Dictionary<string, ActiveEpisode> _activeByStalker =
            new(StringComparer.Ordinal);
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
            _activeByStalker.Clear();
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

            _activeByStalker.TryGetValue(stalkerNetworkId, out var active);

            if (active != null && chase
                && !string.Equals(active.TargetId, target, StringComparison.Ordinal))
            {
                Close(active, AEDPursuitTerminalV1.TargetSwitched,
                    "TARGET_SWITCHED", tick);
                active = null;
            }

            if (chase)
            {
                if (active == null) Begin(stalkerNetworkId, target, tick);
                else
                {
                    RecordState(active, state, tick);
                    if (active.LostTick.HasValue && active.LostWindowPending)
                    {
                        active.ResolvedLostWindows++;
                        active.ReacquisitionCount++;
                        active.Facts.Add(new AEDPursuitFactV1(
                            AEDPursuitFactKindV1.Reacquired,
                            sourceOccurrenceKey, tick, HostAuthority, "CHASE_REACQUIRED"));
                        active.LostWindowPending = false;
                    }
                    active.LostTick = null;
                }
                return true;
            }

            if (active == null) return false;
            RecordState(active, state, tick);

            if ((state == "SEARCH" || state == "PATROL") && !active.LostTick.HasValue)
            {
                active.LostTick = tick;
                active.LostWindowPending = true;
                active.EligibleLostWindows++;
            }

            if ((state == "SEARCH" || state == "PATROL")
                && active.LostTick.HasValue
                && tick - active.LostTick.Value >= (long)tickRate * DefaultEscapeGraceSeconds)
            {
                active.LostWindowPending = false;
                Close(active, AEDPursuitTerminalV1.Censored,
                    $"{GraceWindowVersion}:UNVERIFIED_ESCAPE:{DefaultEscapeGraceSeconds}", tick);
            }
            return true;
        }

        public bool RecordConsequence(string stalkerNetworkId, string targetUserId,
            AEDPursuitFactKindV1 kind, string occurrenceKey, long tick,
            string cause, bool directFromHit)
        {
            if (_frozen) return false;
            if (string.IsNullOrWhiteSpace(stalkerNetworkId)
                || !Guid.TryParse(targetUserId, out var target) || target == Guid.Empty
                || string.IsNullOrWhiteSpace(occurrenceKey) || tick < 0
                || kind == AEDPursuitFactKindV1.Reacquired
                || string.IsNullOrWhiteSpace(cause)
                || (directFromHit && kind != AEDPursuitFactKindV1.Eliminated))
            {
                _invalid = true;
                return false;
            }
            if (!AcceptOccurrence(kind.ToString(), $"{stalkerNetworkId}:{occurrenceKey}")) return false;
            _lastSourceTick = Math.Max(_lastSourceTick, tick);
            if (!_activeByStalker.TryGetValue(stalkerNetworkId, out var active)
                || active.TargetId != target.ToString("D"))
            {
                _incomplete = true;
                return false;
            }

            active.Facts.Add(new AEDPursuitFactV1(kind, occurrenceKey,
                tick, HostAuthority, cause, directFromHit));
            if (kind == AEDPursuitFactKindV1.Downed)
                Close(active, AEDPursuitTerminalV1.Downed, cause, tick);
            else if (kind == AEDPursuitFactKindV1.Eliminated)
                Close(active, AEDPursuitTerminalV1.Eliminated, cause, tick);
            return true;
        }

        public void CensorTarget(string targetUserId, string reason, long tick)
        {
            if (_frozen) return;
            foreach (var active in _activeByStalker.Values
                .Where(e => e.TargetId == targetUserId).ToArray())
                Close(active, AEDPursuitTerminalV1.Censored, reason,
                    Math.Max(active.StartedTick, tick));
        }

        public void CensorStalker(string stalkerNetworkId, string reason, long tick)
        {
            if (_frozen || string.IsNullOrWhiteSpace(stalkerNetworkId)) return;
            if (_activeByStalker.TryGetValue(stalkerNetworkId, out var active))
                Close(active, AEDPursuitTerminalV1.Censored, reason,
                    Math.Max(active.StartedTick, tick));
        }

        public void CensorActive(string reason, long tick)
        {
            if (_frozen) return;
            bool missingReason = string.IsNullOrWhiteSpace(reason);
            foreach (var active in _activeByStalker.Values.ToArray())
                Close(active, missingReason ? AEDPursuitTerminalV1.Incomplete
                    : AEDPursuitTerminalV1.Censored, reason,
                    Math.Max(active.StartedTick, tick));
            if (missingReason) _incomplete = true;
        }

        public AEDPursuitEvidenceSnapshotV1 Freeze()
        {
            if (_frozen) return LastFrozen;
            CensorActive("PHASE_END", _lastSourceTick);
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
            _activeByStalker.Clear();
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
            var active = new ActiveEpisode
            {
                Id = $"{_matchId:D}:{_phaseOrdinal}:{stalkerId}:{targetId}:{tick}:{++_episodeOrdinal}",
                StalkerId = stalkerId,
                TargetId = targetId,
                StartedTick = tick
            };
            active.OpenState = "CHASE";
            active.OpenStateTick = tick;
            _activeByStalker.Add(stalkerId, active);
        }

        private void Close(ActiveEpisode active,
            AEDPursuitTerminalV1 outcome, string reason, long tick)
        {
            if (active == null) return;
            active.StateSegments.Add(new AEDPursuitStateSegmentV1(
                active.OpenState, active.OpenStateTick,
                Math.Max(active.OpenStateTick, tick)));
            _episodes.Add(new AEDPursuitEpisodeV1(active.Id, _matchId,
                _phaseOrdinal, _phaseName, null, active.TargetId,
                active.StalkerId, active.StartedTick, Math.Max(active.StartedTick, tick),
                outcome, reason, _policyContext, active.Facts,
                active.StateSegments, active.EligibleLostWindows,
                active.ResolvedLostWindows, active.ReacquisitionCount,
                HostAuthority));
            _activeByStalker.Remove(active.StalkerId);
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
