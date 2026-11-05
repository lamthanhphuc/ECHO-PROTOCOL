using System;
using EchoProtocol.AI.Common;
using UnityEngine;

namespace EchoProtocol.AI.Stalker
{
    public enum StalkerHidingInvestigationTickStatus
    {
        None,
        MovingToSpot,
        Inspecting,
        Empty,
        Occupied,
        Invalid
    }

    public readonly struct StalkerHidingInvestigationTickResult
    {
        public StalkerHidingInvestigationTickResult(
            StalkerHidingInvestigationTickStatus status,
            StalkerHideSpotCandidate candidate,
            StalkerHideSpotInspectionResult inspectionResult)
        {
            Status = status;
            Candidate = candidate;
            InspectionResult = inspectionResult;
        }

        public StalkerHidingInvestigationTickStatus Status { get; }
        public StalkerHideSpotCandidate Candidate { get; }
        public StalkerHideSpotInspectionResult InspectionResult { get; }
    }

    public sealed class StalkerHidingInvestigation
    {
        private readonly StalkerHideSpotMemory _memory;
        private readonly IStalkerHideSpotInspectionResolver _inspectionResolver;
        private StalkerHideSpotCandidate _selectedCandidate;
        private bool _hasSelectedCandidate;
        private float _inspectionElapsedSeconds;

        public StalkerHidingInvestigation(
            StalkerHideSpotMemory memory,
            IStalkerHideSpotInspectionResolver inspectionResolver)
        {
            _memory = memory ?? throw new ArgumentNullException(nameof(memory));
            _inspectionResolver = inspectionResolver
                ?? throw new ArgumentNullException(nameof(inspectionResolver));
        }

        public bool HasActiveCandidate => _hasSelectedCandidate;
        public StalkerHideSpotCandidate ActiveCandidate => _selectedCandidate;

        public void Reset()
        {
            _selectedCandidate = default;
            _hasSelectedCandidate = false;
            _inspectionElapsedSeconds = 0f;
        }

        public void Begin(StalkerHideSpotCandidate candidate)
        {
            if (!candidate.IsValid || candidate.StableId == 0UL)
            {
                Reset();
                return;
            }

            _selectedCandidate = candidate;
            _hasSelectedCandidate = true;
            _inspectionElapsedSeconds = 0f;
        }

        public StalkerHidingInvestigationTickResult Tick(
            Vector3 stalkerPosition,
            float deltaSeconds,
            AiSimulationTime now,
            StalkerHideSpotInspectionConfig config)
        {
            if (!_hasSelectedCandidate || !_selectedCandidate.IsValid)
            {
                Reset();
                return new StalkerHidingInvestigationTickResult(
                    StalkerHidingInvestigationTickStatus.Invalid,
                    default,
                    default);
            }

            if (!IsWithinInspectionRange(
                    stalkerPosition,
                    _selectedCandidate.InspectPosition,
                    config.InspectDistance))
            {
                _inspectionElapsedSeconds = 0f;
                return new StalkerHidingInvestigationTickResult(
                    StalkerHidingInvestigationTickStatus.MovingToSpot,
                    _selectedCandidate,
                    default);
            }

            _inspectionElapsedSeconds += Mathf.Max(0f, deltaSeconds);
            if (_inspectionElapsedSeconds < config.InspectDurationSeconds)
            {
                return new StalkerHidingInvestigationTickResult(
                    StalkerHidingInvestigationTickStatus.Inspecting,
                    _selectedCandidate,
                    default);
            }

            if (!_inspectionResolver.TryResolveInspection(
                    _selectedCandidate,
                    out var result)
                || !result.Inspected)
            {
                Reset();
                return new StalkerHidingInvestigationTickResult(
                    StalkerHidingInvestigationTickStatus.Invalid,
                    _selectedCandidate,
                    default);
            }

            var inspectedCandidate = _selectedCandidate;
            if (result.Occupied)
            {
                _memory.RecordConfirmedUse(inspectedCandidate.StableId, now);
                Reset();
                return new StalkerHidingInvestigationTickResult(
                    StalkerHidingInvestigationTickStatus.Occupied,
                    inspectedCandidate,
                    result);
            }

            _memory.RecordEmptyInspection(inspectedCandidate.StableId, now);
            Reset();
            return new StalkerHidingInvestigationTickResult(
                StalkerHidingInvestigationTickStatus.Empty,
                inspectedCandidate,
                result);
        }

        private static bool IsWithinInspectionRange(
            Vector3 stalkerPosition,
            Vector3 inspectPosition,
            float inspectDistance)
        {
            var delta = stalkerPosition - inspectPosition;
            return delta.sqrMagnitude
                <= inspectDistance * inspectDistance;
        }
    }
}
