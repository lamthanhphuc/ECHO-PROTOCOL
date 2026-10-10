using System;
using System.Collections.Generic;
using System.Linq;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class AEDObjectiveEvidenceCollectorV1
    {
        private const string CoreUnitType = "CORE_PLACEMENT_SLOT";
        private readonly Dictionary<string, AEDObjectiveEvidenceV1.Unit> _completed =
            new Dictionary<string, AEDObjectiveEvidenceV1.Unit>(StringComparer.Ordinal);
        private readonly HashSet<string> _opportunities =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _occurrences =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _eventIds =
            new HashSet<string>(StringComparer.Ordinal);
        private Guid _matchId;
        private uint _phaseOrdinal;
        private string _phaseName;
        private string _stage;
        private bool _coverageComplete;
        private bool _frozen;
        private bool _invalid;
        private bool _incomplete;

        public AEDObjectiveEvidenceV1 LastFrozen { get; private set; }

        public void MarkIncomplete()
        {
            if (!_frozen) _incomplete = true;
        }

        public void ResetMatch(Guid matchId)
        {
            Clear();
            _matchId = matchId;
        }

        public void StartPhase(Guid matchId, uint phaseOrdinal,
            string phaseName, string stage = null)
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

            if (_phaseOrdinal == phaseOrdinal && _phaseName == phaseName
                && !_frozen)
            {
                _matchId = matchId;
                _stage = stage;
                return;
            }

            ClearPhase();
            _matchId = matchId;
            _phaseOrdinal = phaseOrdinal;
            _phaseName = phaseName;
            _stage = stage;
            _coverageComplete = phaseName != "CORE_COLLECTION";
        }

        public bool RegisterCorePlacementSlots(string sectorId, int slotCount)
        {
            if (_frozen || _matchId == Guid.Empty || _phaseOrdinal == 0
                || _phaseName != "CORE_COLLECTION" || string.IsNullOrWhiteSpace(sectorId)
                || slotCount < 0)
            {
                _invalid = true;
                return false;
            }

            for (var slot = 0; slot < slotCount; slot++)
                _opportunities.Add(UnitId(sectorId, slot));
            _coverageComplete = true;
            return true;
        }

        public bool RecordCorePlaced(bool canonicalAccepted, Guid sourceEventId,
            string coreObjectId, string sectorId, int placementSlot,
            uint transitionOrdinal, string sourceOccurrenceKey, string userId,
            long completionTick, string sourceAuthority, string phaseName)
        {
            if (!canonicalAccepted) return false;
            if (_frozen) return false;
            if (_matchId == Guid.Empty || _phaseOrdinal == 0
                || _phaseName != "CORE_COLLECTION" || phaseName != _phaseName
                || sourceEventId == Guid.Empty || string.IsNullOrWhiteSpace(coreObjectId)
                || string.IsNullOrWhiteSpace(sectorId) || placementSlot < 0
                || transitionOrdinal == 0 || string.IsNullOrWhiteSpace(sourceOccurrenceKey)
                || !Guid.TryParse(userId, out var verifiedUserId) || verifiedUserId == Guid.Empty
                || string.IsNullOrWhiteSpace(sourceAuthority))
            {
                _invalid = true;
                return false;
            }

            var unitId = UnitId(sectorId, placementSlot);
            if (!_opportunities.Contains(unitId))
            {
                _incomplete = true;
                return false;
            }

            var eventId = sourceEventId.ToString("D");
            if (_eventIds.Contains(eventId)) return false;
            if (_occurrences.Contains(sourceOccurrenceKey))
            {
                _invalid = true;
                return false;
            }
            if (_completed.ContainsKey(unitId))
            {
                _invalid = true;
                return false;
            }

            _eventIds.Add(eventId);
            _occurrences.Add(sourceOccurrenceKey);
            _completed.Add(unitId, new AEDObjectiveEvidenceV1.Unit(
                unitId, CoreUnitType, _phaseName, _stage, sectorId,
                placementSlot, true, sourceEventId.ToString("D"),
                sourceOccurrenceKey, coreObjectId, transitionOrdinal,
                verifiedUserId.ToString("D"), completionTick, sourceAuthority));
            return true;
        }

        public AEDObjectiveEvidenceV1 Freeze(DateTime windowStartedAtUtc,
            DateTime windowEndedAtUtc)
        {
            if (_frozen) return LastFrozen;
            var sourceUnits = _completed.Values.OrderBy(unit => unit.ObjectiveUnitId,
                StringComparer.Ordinal).ToArray();
            var opportunityIds = _opportunities.OrderBy(id => id, StringComparer.Ordinal).ToArray();
            var successes = sourceUnits.Length;
            var failures = Math.Max(0, opportunityIds.Length - successes);
            var status = _invalid ? AEDMetricStatusV1.Invalid
                : _incomplete || !_coverageComplete || _matchId == Guid.Empty
                    ? AEDMetricStatusV1.Incomplete
                    : !IsSupportedPhase(_phaseName)
                        ? AEDMetricStatusV1.Unsupported
                        : opportunityIds.Length == 0
                            ? AEDMetricStatusV1.NoOpportunity
                            : AEDMetricStatusV1.Available;
            var denominator = opportunityIds.Length;
            var value = status == AEDMetricStatusV1.Available && denominator > 0
                ? (double?)successes / denominator : null;
            var reason = status == AEDMetricStatusV1.Invalid ? "OBJECTIVE_EVIDENCE_INVALID"
                : status == AEDMetricStatusV1.Incomplete ? "OBJECTIVE_EVIDENCE_INCOMPLETE"
                : status == AEDMetricStatusV1.Unsupported ? "OBJECTIVE_METRIC_UNSUPPORTED"
                : status == AEDMetricStatusV1.NoOpportunity ? "OBJECTIVE_NO_OPPORTUNITY"
                : null;
            var reasons = reason == null ? Array.Empty<string>() : new[] { reason };
            var metric = new AEDMetricResultV1(
                "Objective.UnitCompletionRate", _matchId, _phaseOrdinal, _phaseName,
                null, null, null, null, null, null, null, "1.1", null,
                null, AEDObjectiveEvidenceV1.SourceSystemName, "FusionStateAuthority",
                sourceUnits.Select(unit => unit.SourceCanonicalEventId),
                sourceUnits.Select(unit => unit.SourceOccurrenceKey), null,
                denominator, successes, failures, 0, successes, denominator,
                "unit", value, AEDMetricMeasurementKindV1.Rate, "unit",
                null, null, null, sourceUnits.Length, "InsufficientForPolicy",
                status, reasons, windowStartedAtUtc, windowEndedAtUtc);
            LastFrozen = new AEDObjectiveEvidenceV1(
                _matchId, _phaseOrdinal, _phaseName, sourceUnits,
                opportunityIds, metric);
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
            _completed.Clear();
            _opportunities.Clear();
            _occurrences.Clear();
            _eventIds.Clear();
            _phaseOrdinal = 0;
            _phaseName = null;
            _stage = null;
            _coverageComplete = false;
            _frozen = false;
            _invalid = false;
            _incomplete = false;
            LastFrozen = null;
        }

        private static bool IsSupportedPhase(string phase) => phase == "CORE_COLLECTION";

        private static string UnitId(string sectorId, int slot) =>
            $"CORE:{sectorId}:{slot}";
    }
}
