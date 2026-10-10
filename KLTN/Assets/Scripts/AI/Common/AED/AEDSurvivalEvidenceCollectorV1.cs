using System;
using System.Collections.Generic;
using System.Linq;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class AEDSurvivalEvidenceCollectorV1
    {
        private readonly Dictionary<string, AEDSurvivalOutcomeV1> _outcomes =
            new Dictionary<string, AEDSurvivalOutcomeV1>(StringComparer.Ordinal);
        private readonly HashSet<string> _canonicalEventIds =
            new HashSet<string>(StringComparer.Ordinal);
        private Guid _matchId;
        private uint _phaseOrdinal;
        private string _phaseName;
        private bool _frozen;
        private bool _invalid;
        private bool _incomplete;

        public AEDSurvivalEvidenceSnapshotV1 LastFrozen { get; private set; }
        public bool IsInvalid => _invalid;

        public void MarkIncomplete() { if (!_frozen) _incomplete = true; }

        public void ResetMatch(Guid matchId)
        {
            Clear();
            _matchId = matchId;
        }

        public void StartPhase(Guid matchId, uint phaseOrdinal, string phaseName)
        {
            if (matchId == Guid.Empty || phaseOrdinal == 0
                || string.IsNullOrWhiteSpace(phaseName))
            {
                ClearPhase();
                _matchId = matchId;
                _phaseOrdinal = phaseOrdinal;
                _phaseName = phaseName;
                _invalid = true;
                return;
            }

            if (_matchId != Guid.Empty && _matchId != matchId)
                ResetMatch(matchId);
            if (_phaseOrdinal == phaseOrdinal && _phaseName == phaseName && !_frozen)
                return;

            ClearPhase();
            _matchId = matchId;
            _phaseOrdinal = phaseOrdinal;
            _phaseName = phaseName;
        }

        public bool Record(AEDSurvivalOutcomeKindV1 kind, string userId,
            string relatedUserId, string occurrenceKey, uint transitionOrdinal,
            string authoritativeCause, bool directFromHit, string canonicalEventId,
            string sourceAuthority)
        {
            if (_frozen) return false;
            if (_matchId == Guid.Empty || _phaseOrdinal == 0
                || string.IsNullOrWhiteSpace(_phaseName)
                || !Guid.TryParse(userId, out var verifiedUserId)
                || verifiedUserId == Guid.Empty
                || string.IsNullOrWhiteSpace(occurrenceKey)
                || transitionOrdinal == 0
                || string.IsNullOrWhiteSpace(authoritativeCause)
                || string.IsNullOrWhiteSpace(sourceAuthority)
                || (kind == AEDSurvivalOutcomeKindV1.DirectElimination && !directFromHit)
                || (kind != AEDSurvivalOutcomeKindV1.DirectElimination && directFromHit))
            {
                _invalid = true;
                return false;
            }

            var key = $"{_matchId:D}|{_phaseOrdinal}|{kind}|{occurrenceKey}";
            if (_outcomes.ContainsKey(key)) return false;
            if (!string.IsNullOrWhiteSpace(canonicalEventId)
                && _canonicalEventIds.Contains(canonicalEventId))
                return false;

            var fact = new AEDSurvivalOutcomeV1(
                _matchId, _phaseOrdinal, _phaseName, kind,
                verifiedUserId.ToString("D"), relatedUserId, occurrenceKey,
                transitionOrdinal, authoritativeCause, directFromHit,
                canonicalEventId, sourceAuthority);
            _outcomes.Add(key, fact);
            if (!string.IsNullOrWhiteSpace(canonicalEventId))
                _canonicalEventIds.Add(canonicalEventId);
            return true;
        }

        public AEDSurvivalEvidenceSnapshotV1 Freeze()
        {
            if (_frozen) return LastFrozen;
            LastFrozen = new AEDSurvivalEvidenceSnapshotV1(
                _matchId, _phaseOrdinal, _phaseName,
                _outcomes.Values.OrderBy(outcome => outcome.TransitionOrdinal)
                    .ThenBy(outcome => outcome.OccurrenceKey, StringComparer.Ordinal),
                _incomplete);
            _frozen = true;
            return LastFrozen;
        }

        public void Clear()
        {
            ClearPhase();
            _matchId = Guid.Empty;
        }

        private void ClearPhase()
        {
            _outcomes.Clear();
            _canonicalEventIds.Clear();
            _phaseOrdinal = 0;
            _phaseName = null;
            _frozen = false;
            _invalid = false;
            _incomplete = false;
            LastFrozen = null;
        }
    }
}
