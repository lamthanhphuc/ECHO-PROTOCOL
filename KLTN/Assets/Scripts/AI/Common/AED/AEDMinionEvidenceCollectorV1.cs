using System;
using System.Collections.Generic;
using System.Linq;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class AEDMinionEvidenceCollectorV1
    {
        private const string HostAuthority = "FusionStateAuthority";
        private sealed class ActiveEncounter
        {
            public string Id, MinionId, Zone, TargetId;
            public long StartedTick, LostTick;
            public bool HasLostTick;
            public readonly List<AEDMinionParticipantSegmentV1> Participants = new();
            public readonly List<AEDMinionStateSegmentV1> StateSegments = new();
            public long? OpenSegmentStart;
            public string OpenSegmentUser;
            public long? OpenStateStart;
            public string OpenState;
            public int SegmentOrdinal;
            public readonly List<AEDMinionFactV1> Facts = new();
        }

        private readonly Dictionary<string, ActiveEncounter> _active = new(StringComparer.Ordinal);
        private readonly List<AEDMinionEncounterV1> _episodes = new();
        private readonly List<AEDMinionFactV1> _facts = new();
        private readonly HashSet<string> _occurrences = new(StringComparer.Ordinal);
        private Guid _matchId;
        private uint _phaseOrdinal;
        private string _phaseName;
        private long _episodeOrdinal;
        private bool _frozen, _incomplete, _invalid;
        private long _lastSourceTick;
        private AEDMetricPolicyContextV1 _policyContext;

        public AEDMinionEvidenceSnapshotV1 LastFrozen { get; private set; }
        public bool IsIncomplete => _incomplete;
        public bool IsInvalid => _invalid;

        public void ResetMatch(Guid matchId)
        {
            Clear(); _matchId = matchId;
            if (matchId == Guid.Empty) _invalid = true;
        }

        public void StartPhase(Guid matchId, uint ordinal, string phase,
            AEDMetricPolicyContextV1 policyContext = null)
        {
            if (matchId == Guid.Empty || ordinal == 0 || string.IsNullOrWhiteSpace(phase))
            { _invalid = true; return; }
            if (_matchId != Guid.Empty && _matchId != matchId) ResetMatch(matchId);
            if (_phaseOrdinal != 0 && !_frozen) { CensorAll("PHASE_END", _lastSourceTick); Freeze(); }
            _active.Clear(); _episodes.Clear(); _facts.Clear(); _occurrences.Clear();
            _matchId = matchId; _phaseOrdinal = ordinal; _phaseName = phase;
            _policyContext = policyContext;
            _lastSourceTick = 0;
            _frozen = false; _incomplete = false; _invalid = false;
        }

        public bool Observe(bool hostAuthority, string minionId, string zone,
            string state, string targetUserId, long tick, int tickRate,
            string occurrenceKey)
        {
            if (_frozen) return false;
            if (!hostAuthority || string.IsNullOrWhiteSpace(minionId) || tick < 0
                || tickRate <= 0 || string.IsNullOrWhiteSpace(occurrenceKey))
            { _invalid = true; return false; }
            if (!Accept("STATE", occurrenceKey)) return false;
            _lastSourceTick = Math.Max(_lastSourceTick, tick);

            var targetValid = Guid.TryParse(targetUserId, out var user) && user != Guid.Empty;
            var target = targetValid ? user.ToString("D") : null;
            bool engaging = state == "Track" || state == "Harass";
            if (engaging && !targetValid) { _incomplete = true; return false; }

            if (engaging)
            {
                if (!_active.TryGetValue(minionId, out var episode))
                {
                    episode = new ActiveEncounter
                    {
                        Id = $"{_matchId:D}:{_phaseOrdinal}:{minionId}:{tick}:{++_episodeOrdinal}",
                        MinionId = minionId, Zone = zone, TargetId = target,
                        StartedTick = tick
                    };
                    _active.Add(minionId, episode);
                }
                RecordState(episode, state, tick);
                if (!string.Equals(episode.TargetId, target, StringComparison.Ordinal))
                {
                    CloseSegment(episode, tick);
                    episode.TargetId = target;
                }
                episode.HasLostTick = false;
                if (!string.Equals(episode.OpenSegmentUser, target, StringComparison.Ordinal))
                {
                    CloseSegment(episode, tick);
                    episode.OpenSegmentUser = target;
                    episode.OpenSegmentStart = tick;
                }
                return true;
            }

            if (!_active.TryGetValue(minionId, out var active)) return false;
            RecordState(active, state, tick);
            if (active.OpenSegmentStart.HasValue) CloseSegment(active, tick);
            if (state == "Flee")
            {
                CloseEpisode(active, AEDMinionTerminalV1.Disengaged, "MINION_FLEE", tick);
                return true;
            }
            if (state == "Roam")
            {
                if (!active.HasLostTick) { active.HasLostTick = true; active.LostTick = tick; }
                if (tick - active.LostTick >= (long)tickRate * 5)
                {
                    CloseEpisode(active, AEDMinionTerminalV1.Disengaged,
                        "ROAM_GRACE_NO_VERIFIED_EVASION", tick);
                }
            }
            return true;
        }

        public bool RecordFact(AEDMinionFactKindV1 kind, string minionId,
            string occurrenceKey, long tick, string userId = null,
            string relatedUserId = null, string objectId = null,
            string effectKind = null, long attemptOrdinal = 0,
            bool accepted = false, double seconds = 0,
            string sourceEventId = null, float x = 0, float y = 0, float z = 0)
        {
            if (_frozen) return false;
            if (string.IsNullOrWhiteSpace(minionId) || string.IsNullOrWhiteSpace(occurrenceKey)
                || tick < 0 || (userId != null && !ValidUser(userId))
                || (relatedUserId != null && !ValidUser(relatedUserId))
                || double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
            { _invalid = true; return false; }
            if (!Accept(kind.ToString(), $"{minionId}:{occurrenceKey}"))
                return false;
            _lastSourceTick = Math.Max(_lastSourceTick, tick);
            _active.TryGetValue(minionId, out var active);
            if (active != null
                && (kind == AEDMinionFactKindV1.NoiseMakerOpportunity
                    || kind == AEDMinionFactKindV1.NoiseMakerReaction)
                && string.IsNullOrWhiteSpace(relatedUserId))
                relatedUserId = active.TargetId;
            var fact = new AEDMinionFactV1(kind, active?.Id, minionId,
                occurrenceKey, sourceEventId, NormalizeUser(userId),
                NormalizeUser(relatedUserId), objectId, effectKind, tick,
                attemptOrdinal, accepted, seconds, x, y, z, HostAuthority);
            if (active != null)
            {
                active.Facts.Add(fact);
                if (kind == AEDMinionFactKindV1.TeamDeathReceipt
                    && accepted && string.Equals(effectKind, "FLASHLIGHT_DEATH",
                        StringComparison.Ordinal))
                    CloseEpisode(active, AEDMinionTerminalV1.Countered,
                        "FLASHLIGHT_DEATH_CONFIRMED", tick);
            }
            else _facts.Add(fact);
            return true;
        }

        public void MarkIncomplete() { if (!_frozen) _incomplete = true; }

        public void CensorMinion(string minionId, string reason, long tick)
        {
            if (_active.TryGetValue(minionId, out var episode))
                CloseEpisode(episode, AEDMinionTerminalV1.Censored, reason, tick);
        }

        public void CensorTarget(string userId, string reason, long tick)
        {
            foreach (var encounter in _active.Values.Where(e =>
                e.TargetId == userId || e.OpenSegmentUser == userId
                || e.Participants.Any(segment => segment.UserId == userId)).ToArray())
                CloseEpisode(encounter, AEDMinionTerminalV1.Censored, reason, tick);
        }

        public void CensorAll(string reason, long tick)
        {
            foreach (var episode in _active.Values.ToArray())
                CloseEpisode(episode, AEDMinionTerminalV1.Censored, reason, tick);
        }

        public AEDMinionEvidenceSnapshotV1 Freeze()
        {
            if (_frozen) return LastFrozen;
            CensorAll("PHASE_END", _lastSourceTick);
            LastFrozen = new AEDMinionEvidenceSnapshotV1(_matchId,
                _phaseOrdinal, _phaseName, _episodes, _facts,
                _incomplete, _invalid, _policyContext);
            _frozen = true;
            return LastFrozen;
        }

        public void Clear()
        {
            _active.Clear(); _episodes.Clear(); _facts.Clear(); _occurrences.Clear();
            _matchId = Guid.Empty; _phaseOrdinal = 0; _phaseName = null;
            _episodeOrdinal = 0; _frozen = false; _incomplete = false; _invalid = false;
            _lastSourceTick = 0; _policyContext = null;
            LastFrozen = null;
        }

        private bool Accept(string kind, string occurrence) => _occurrences.Add(
            $"{_matchId:D}|{_phaseOrdinal}|{kind}|{occurrence}");

        private static bool ValidUser(string value) => Guid.TryParse(value, out var id) && id != Guid.Empty;
        private static string NormalizeUser(string value) => ValidUser(value) ? Guid.Parse(value).ToString("D") : value;

        private static void CloseSegment(ActiveEncounter episode, long tick)
        {
            if (!episode.OpenSegmentStart.HasValue) return;
            episode.Participants.Add(new AEDMinionParticipantSegmentV1(
                $"{episode.Id}:participant:{++episode.SegmentOrdinal}",
                episode.OpenSegmentUser, episode.OpenSegmentStart.Value,
                Math.Max(episode.OpenSegmentStart.Value, tick)));
            episode.OpenSegmentStart = null; episode.OpenSegmentUser = null;
        }

        private static void RecordState(ActiveEncounter episode, string state, long tick)
        {
            if (string.Equals(episode.OpenState, state, StringComparison.Ordinal)) return;
            if (episode.OpenStateStart.HasValue)
                episode.StateSegments.Add(new AEDMinionStateSegmentV1(
                    episode.OpenState, episode.OpenStateStart.Value,
                    Math.Max(episode.OpenStateStart.Value, tick)));
            episode.OpenState = state;
            episode.OpenStateStart = tick;
        }

        private void CloseEpisode(ActiveEncounter active,
            AEDMinionTerminalV1 outcome, string reason, long tick)
        {
            CloseSegment(active, tick);
            if (active.OpenStateStart.HasValue)
                active.StateSegments.Add(new AEDMinionStateSegmentV1(
                    active.OpenState, active.OpenStateStart.Value,
                    Math.Max(active.OpenStateStart.Value, tick)));
            _episodes.Add(new AEDMinionEncounterV1(active.Id, _matchId,
                _phaseOrdinal, _phaseName, active.Zone, active.MinionId,
                active.StartedTick, Math.Max(active.StartedTick, tick), outcome,
                reason, active.Participants, active.StateSegments,
                active.Facts, _policyContext));
            _active.Remove(active.MinionId);
        }
    }
}
