using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common.AED;

namespace EchoProtocol.AI.AED
{
    public sealed class AEDv2MatchEvidenceCollector
    {
        private readonly HashSet<string> _acceptedOccurrences = new HashSet<string>();
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

        public AEDv2CurrentMatchEvidence LastFrozen { get; private set; }

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
            _complete = matchId != Guid.Empty && !string.IsNullOrWhiteSpace(rosterIdentity)
                && !string.IsNullOrWhiteSpace(phase) && ordinal > 0;
        }

        public void RecordAcceptedDown(string occurrenceKey) => Count(occurrenceKey, ref _downCount);
        public void RecordAcceptedRevive(string occurrenceKey) => Count(occurrenceKey, ref _reviveCount);
        public void RecordAcceptedElimination(string occurrenceKey) => Count(occurrenceKey, ref _eliminatedCount);
        public void RecordAcceptedNoise(string occurrenceKey) => Count(occurrenceKey, ref _noiseCount);
        public void RecordAcceptedObjective(string occurrenceKey) => Count(occurrenceKey, ref _objectiveProgress);
        public void RecordAcceptedTeamTool(string occurrenceKey) => Count(occurrenceKey, ref _teamToolUseCount);
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
            return LastFrozen;
        }

        public void Clear()
        {
            _matchId = Guid.Empty;
            _phase = null;
            _rosterIdentity = null;
            _acceptedOccurrences.Clear();
            LastFrozen = null;
        }

        private void Count(string key, ref int counter)
        {
            if (string.IsNullOrWhiteSpace(key)) { _complete = false; return; }
            if (_acceptedOccurrences.Add(key)) counter++;
        }
    }
}
