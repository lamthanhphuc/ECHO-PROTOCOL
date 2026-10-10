using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Telemetry;

namespace EchoProtocol.AI.AED
{
    public sealed class AEDv2MatchEvidenceCollector
    {
        private readonly HashSet<string> _acceptedOccurrences = new HashSet<string>();
        private readonly Dictionary<string, AEDv2PlayerPhaseEvidence> _playerEvidence = new Dictionary<string, AEDv2PlayerPhaseEvidence>();
        private Guid _matchId;
        private string _rosterIdentity;
        private string _phase;
        private uint _phaseOrdinal;
        private DateTime _startedAtUtc;
        private int _downCount;
        private int _reviveCount;
        private int _eliminatedCount;
        private int _noiseCount;
        private int _objectiveProgress;
        private int _teamToolUseCount;
        private bool _complete;
        private bool _frozen;

        public AEDv2CurrentMatchEvidence LastFrozen { get; private set; }
        public IReadOnlyDictionary<string, AEDv2PlayerPhaseEvidence> PlayerEvidence => _playerEvidence;
        public IReadOnlyDictionary<string, AEDv2PlayerPhaseEvidence> LastFrozenPlayerEvidence { get; private set; }

        public void StartPhase(Guid matchId, string rosterIdentity, string phase,
            uint ordinal, DateTime startedAtUtc)
        {
            _matchId = matchId;
            _rosterIdentity = rosterIdentity;
            _phase = phase;
            _phaseOrdinal = ordinal;
            _startedAtUtc = startedAtUtc;
            _downCount = _reviveCount = _eliminatedCount = _noiseCount = _objectiveProgress = 0;
            _teamToolUseCount = 0;
            _acceptedOccurrences.Clear();
            _playerEvidence.Clear();
            _frozen = false;
            _complete = matchId != Guid.Empty && !string.IsNullOrWhiteSpace(rosterIdentity)
                && !string.IsNullOrWhiteSpace(phase) && ordinal > 0;
        }

        public void RecordAcceptedDown(string key) =>
            Count(TelemetryEventTypes.PlayerDowned, key, ref _downCount);
        public void RecordAcceptedDown(string key, string userId) =>
            Count(TelemetryEventTypes.PlayerDowned, key, ref _downCount,
                () => GetPlayer(userId).RecordDown());
        public void RecordAcceptedRevive(string key) =>
            Count(TelemetryEventTypes.PlayerRevived, key, ref _reviveCount);
        public void RecordAcceptedRevive(string key, string userId) =>
            Count(TelemetryEventTypes.PlayerRevived, key, ref _reviveCount,
                () => GetPlayer(userId).RecordRevive());
        public void RecordAcceptedElimination(string key) =>
            Count(TelemetryEventTypes.PlayerEliminated, key, ref _eliminatedCount);
        public void RecordAcceptedElimination(string key, string userId) =>
            Count(TelemetryEventTypes.PlayerEliminated, key, ref _eliminatedCount,
                () => GetPlayer(userId).RecordElimination());
        public void RecordAcceptedNoise(string key) =>
            Count(TelemetryEventTypes.NoiseEmitted, key, ref _noiseCount);
        public void RecordAcceptedNoise(string key, string userId) =>
            Count(TelemetryEventTypes.NoiseEmitted, key, ref _noiseCount,
                () => GetPlayer(userId).RecordNoise());
        public void RecordAcceptedObjective(string key) =>
            Count(TelemetryEventTypes.PuzzleCompleted, key, ref _objectiveProgress);
        public void RecordAcceptedTeamTool(string key) =>
            Count(TelemetryEventTypes.TeamToolUsed, key, ref _teamToolUseCount);
        public void RecordAcceptedTeamTool(string key, string userId) =>
            Count(TelemetryEventTypes.TeamToolUsed, key, ref _teamToolUseCount,
                () => GetPlayer(userId).RecordToolUse());

        public void RecordActiveObservation(string userId, double seconds)
        {
            if (_frozen || !_complete || _matchId == Guid.Empty
                || string.IsNullOrWhiteSpace(userId)
                || seconds <= 0 || double.IsNaN(seconds)
                || double.IsInfinity(seconds))
                return;
            GetPlayer(userId).RecordActiveObservation(seconds);
        }
        public void MarkIncomplete() => _complete = false;

        public AEDv2CurrentMatchEvidence Freeze(string completedPhase, int rosterSize,
            string currentRosterIdentity, DateTime endedAtUtc)
        {
            if (_matchId == Guid.Empty || string.IsNullOrWhiteSpace(_phase)) return null;
            var reasons = new List<string>();
            if (!_complete) reasons.Add("TELEMETRY_INCOMPLETE");
            if (completedPhase != _phase) reasons.Add("PHASE_MISMATCH");
            if (currentRosterIdentity != _rosterIdentity) reasons.Add("ROSTER_CHANGED");
            if (rosterSize < _eliminatedCount) reasons.Add("ELIMINATION_COUNT_INVALID");
            if (endedAtUtc < _startedAtUtc) reasons.Add("TIME_INVALID");
            LastFrozen = new AEDv2CurrentMatchEvidence(_matchId, _phase,
                _phaseOrdinal, _rosterIdentity, _startedAtUtc,
                endedAtUtc < _startedAtUtc ? _startedAtUtc : endedAtUtc,
                Math.Max(0, rosterSize - _eliminatedCount), _downCount, _reviveCount,
                _eliminatedCount, _noiseCount, _objectiveProgress,
                reasons.Count == 0, reasons, _teamToolUseCount);
            LastFrozenPlayerEvidence = new ReadOnlyDictionary<string, AEDv2PlayerPhaseEvidence>(
                new Dictionary<string, AEDv2PlayerPhaseEvidence>(_playerEvidence));
            _frozen = true;
            return LastFrozen;
        }

        public void Clear()
        {
            _matchId = Guid.Empty;
            _phase = null;
            _rosterIdentity = null;
            _acceptedOccurrences.Clear();
            _playerEvidence.Clear();
            LastFrozen = null;
            LastFrozenPlayerEvidence = null;
            _frozen = false;
        }

        private bool TryAcceptOccurrence(string eventType, string key)
        {
            if (_frozen) return false;
            if (string.IsNullOrWhiteSpace(key))
            {
                _complete = false;
                return false;
            }
            return _acceptedOccurrences.Add(eventType + "|" + key);
        }

        private void Count(string eventType, string key, ref int counter)
        {
            if (TryAcceptOccurrence(eventType, key)) counter++;
        }

        private void Count(string eventType, string key, ref int counter, Action recordPlayer)
        {
            if (TryAcceptOccurrence(eventType, key))
            {
                counter++;
                recordPlayer();
            }
        }

        private AEDv2PlayerPhaseEvidence GetPlayer(string userId)
        {
            if (!_playerEvidence.TryGetValue(userId, out var player))
            {
                player = new AEDv2PlayerPhaseEvidence(userId);
                _playerEvidence.Add(userId, player);
            }
            return player;
        }
    }
}
