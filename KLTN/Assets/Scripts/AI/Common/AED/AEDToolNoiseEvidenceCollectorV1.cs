using System;
using System.Collections.Generic;
using System.Linq;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class AEDToolNoiseEvidenceCollectorV1
    {
        private readonly Dictionary<string, AEDToolNoiseFactV1> _facts =
            new Dictionary<string, AEDToolNoiseFactV1>(StringComparer.Ordinal);
        private Guid _matchId;
        private uint _phaseOrdinal;
        private string _phaseName;
        private bool _frozen;
        private bool _incomplete;
        private bool _invalid;

        public AEDToolNoiseEvidenceSnapshotV1 LastFrozen { get; private set; }
        public bool IsIncomplete => _incomplete;
        public bool IsInvalid => _invalid;

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

        public bool RecordToolAction(string toolType, string userId,
            string occurrenceKey, AEDEvidenceSourceCategoryV1 sourceCategory,
            string canonicalEventId, string sourceAuthority)
        {
            return Record(AEDToolNoiseFactKindV1.ToolActionAccepted,
                sourceCategory, toolType, null, userId, null, occurrenceKey,
                canonicalEventId, null, sourceAuthority);
        }

        public bool RecordToolEffect(string toolType, string userId,
            string relatedUserId, string occurrenceKey,
            AEDToolEffectOutcomeV1 outcome, string canonicalEventId,
            string sourceAuthority)
        {
            return Record(AEDToolNoiseFactKindV1.ToolEffectResolved,
                AEDEvidenceSourceCategoryV1.GameplayOnlyAccepted, toolType,
                null, userId, relatedUserId, occurrenceKey, canonicalEventId,
                outcome, sourceAuthority);
        }

        public bool RecordNoise(string noiseType, string userId,
            string occurrenceKey, AEDEvidenceSourceCategoryV1 sourceCategory,
            string canonicalEventId, string sourceAuthority)
        {
            return Record(AEDToolNoiseFactKindV1.GameplayNoiseAccepted,
                sourceCategory, null, noiseType, userId, null, occurrenceKey,
                canonicalEventId, null, sourceAuthority);
        }

        public void MarkIncomplete()
        {
            if (!_frozen) _incomplete = true;
        }

        public AEDToolNoiseEvidenceSnapshotV1 Freeze()
        {
            if (_frozen) return LastFrozen;
            LastFrozen = new AEDToolNoiseEvidenceSnapshotV1(
                _matchId, _phaseOrdinal, _phaseName,
                _facts.Values.OrderBy(fact => fact.Kind)
                    .ThenBy(fact => fact.OccurrenceKey, StringComparer.Ordinal),
                _incomplete);
            _frozen = true;
            return LastFrozen;
        }

        public void Clear()
        {
            ClearPhase();
            _matchId = Guid.Empty;
        }

        private bool Record(AEDToolNoiseFactKindV1 kind,
            AEDEvidenceSourceCategoryV1 sourceCategory, string toolType,
            string noiseType, string userId, string relatedUserId,
            string occurrenceKey, string canonicalEventId,
            AEDToolEffectOutcomeV1? effectOutcome, string sourceAuthority)
        {
            if (_frozen) return false;
            if (_matchId == Guid.Empty || _phaseOrdinal == 0
                || string.IsNullOrWhiteSpace(_phaseName)
                || !Guid.TryParse(userId, out var verifiedUserId)
                || verifiedUserId == Guid.Empty
                || string.IsNullOrWhiteSpace(occurrenceKey)
                || string.IsNullOrWhiteSpace(sourceAuthority)
                || (kind != AEDToolNoiseFactKindV1.GameplayNoiseAccepted
                    && string.IsNullOrWhiteSpace(toolType))
                || (kind == AEDToolNoiseFactKindV1.GameplayNoiseAccepted
                    && string.IsNullOrWhiteSpace(noiseType)))
            {
                _invalid = true;
                return false;
            }

            if (kind == AEDToolNoiseFactKindV1.ToolEffectResolved
                && !string.IsNullOrWhiteSpace(relatedUserId)
                && !Guid.TryParse(relatedUserId, out _))
            {
                _invalid = true;
                return false;
            }

            var key = $"{_matchId:D}|{_phaseOrdinal}|{kind}|{occurrenceKey}";
            if (_facts.ContainsKey(key)) return false;
            _facts.Add(key, new AEDToolNoiseFactV1(
                kind, sourceCategory, toolType, noiseType,
                verifiedUserId.ToString("D"), relatedUserId, occurrenceKey,
                canonicalEventId, effectOutcome, sourceAuthority));
            return true;
        }

        private void ClearPhase()
        {
            _facts.Clear();
            _phaseOrdinal = 0;
            _phaseName = null;
            _frozen = false;
            _incomplete = false;
            _invalid = false;
            LastFrozen = null;
        }
    }
}
