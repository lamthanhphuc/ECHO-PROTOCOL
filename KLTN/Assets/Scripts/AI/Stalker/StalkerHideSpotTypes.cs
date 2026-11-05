using System;
using EchoProtocol.AI.Common;
using UnityEngine;

namespace EchoProtocol.AI.Stalker
{
    public readonly struct StalkerHideSpotCandidate : IEquatable<StalkerHideSpotCandidate>
    {
        public StalkerHideSpotCandidate(
            ulong stableId,
            Vector3 position,
            Vector3 inspectPosition,
            bool isValid)
        {
            StableId = stableId;
            Position = position;
            InspectPosition = inspectPosition;
            IsValid = isValid;
        }

        public ulong StableId { get; }
        public Vector3 Position { get; }
        public Vector3 InspectPosition { get; }
        public bool IsValid { get; }

        public bool Equals(StalkerHideSpotCandidate other)
        {
            return StableId == other.StableId
                && Position.Equals(other.Position)
                && InspectPosition.Equals(other.InspectPosition)
                && IsValid == other.IsValid;
        }

        public override bool Equals(object obj)
        {
            return obj is StalkerHideSpotCandidate other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + StableId.GetHashCode();
                hash = (hash * 31) + Position.GetHashCode();
                hash = (hash * 31) + InspectPosition.GetHashCode();
                hash = (hash * 31) + IsValid.GetHashCode();
                return hash;
            }
        }
    }

    public readonly struct StalkerHideSpotMemorySnapshot
    {
        public StalkerHideSpotMemorySnapshot(
            int confirmedUseCount,
            AiSimulationTime lastConfirmedUseTime,
            AiSimulationTime lastInspectedTime,
            int consecutiveEmptyInspections)
        {
            ConfirmedUseCount = Math.Max(0, confirmedUseCount);
            LastConfirmedUseTime = lastConfirmedUseTime;
            LastInspectedTime = lastInspectedTime;
            ConsecutiveEmptyInspections = Math.Max(0, consecutiveEmptyInspections);
        }

        public int ConfirmedUseCount { get; }
        public AiSimulationTime LastConfirmedUseTime { get; }
        public AiSimulationTime LastInspectedTime { get; }
        public int ConsecutiveEmptyInspections { get; }
        public bool HasConfirmedUse => ConfirmedUseCount > 0;
        public bool HasInspectionHistory => LastInspectedTime.IsValid;
    }

    public readonly struct StalkerHideSpotSelection
    {
        public StalkerHideSpotSelection(
            StalkerHideSpotCandidate candidate,
            float score)
        {
            Candidate = candidate;
            Score = score;
        }

        public StalkerHideSpotCandidate Candidate { get; }
        public float Score { get; }
    }

    public readonly struct StalkerHideSpotSelectorConfig
    {
        public StalkerHideSpotSelectorConfig(
            float searchRadius,
            float confirmedUseBias,
            float emptyInspectionPenalty,
            float reinspectCooldownSeconds,
            float distanceTieEpsilon)
        {
            SearchRadius = Mathf.Max(0f, searchRadius);
            ConfirmedUseBias = Mathf.Max(0f, confirmedUseBias);
            EmptyInspectionPenalty = Mathf.Max(0f, emptyInspectionPenalty);
            ReinspectCooldownSeconds = Mathf.Max(0f, reinspectCooldownSeconds);
            DistanceTieEpsilon = Mathf.Max(0f, distanceTieEpsilon);
        }

        public static StalkerHideSpotSelectorConfig Default =>
            new StalkerHideSpotSelectorConfig(
                6f,
                2f,
                1.5f,
                8f,
                0.01f);

        public float SearchRadius { get; }
        public float ConfirmedUseBias { get; }
        public float EmptyInspectionPenalty { get; }
        public float ReinspectCooldownSeconds { get; }
        public float DistanceTieEpsilon { get; }
    }

    public readonly struct StalkerHideSpotInspectionConfig
    {
        public StalkerHideSpotInspectionConfig(
            float inspectDistance,
            float inspectDurationSeconds)
        {
            InspectDistance = Mathf.Max(0f, inspectDistance);
            InspectDurationSeconds = Mathf.Max(0f, inspectDurationSeconds);
        }

        public static StalkerHideSpotInspectionConfig Default =>
            new StalkerHideSpotInspectionConfig(1.5f, 0.75f);

        public float InspectDistance { get; }
        public float InspectDurationSeconds { get; }
    }

    public readonly struct StalkerHideSpotInspectionResult
    {
        private StalkerHideSpotInspectionResult(
            bool inspected,
            bool occupied,
            PlayerId confirmedPlayerId,
            Vector3 confirmedPosition,
            Vector3 confirmedDirection,
            StalkerTargetEligibilityResult confirmedEligibility)
        {
            Inspected = inspected;
            Occupied = occupied;
            ConfirmedPlayerId = confirmedPlayerId;
            ConfirmedPosition = confirmedPosition;
            ConfirmedDirection = confirmedDirection.sqrMagnitude > 0f
                ? confirmedDirection.normalized
                : Vector3.forward;
            ConfirmedEligibility = confirmedEligibility;
        }

        public bool Inspected { get; }
        public bool Occupied { get; }
        public PlayerId ConfirmedPlayerId { get; }
        public Vector3 ConfirmedPosition { get; }
        public Vector3 ConfirmedDirection { get; }
        public StalkerTargetEligibilityResult ConfirmedEligibility { get; }

        public static StalkerHideSpotInspectionResult Empty()
        {
            return new StalkerHideSpotInspectionResult(
                true,
                false,
                PlayerId.Invalid,
                default,
                Vector3.forward,
                default);
        }

        public static StalkerHideSpotInspectionResult OccupiedBy(
            PlayerId confirmedPlayerId,
            Vector3 confirmedPosition,
            Vector3 confirmedDirection)
        {
            return OccupiedBy(
                confirmedPlayerId,
                confirmedPosition,
                confirmedDirection,
                StalkerTargetEligibilityResult.EligibleTarget());
        }

        public static StalkerHideSpotInspectionResult OccupiedBy(
            PlayerId confirmedPlayerId,
            Vector3 confirmedPosition,
            Vector3 confirmedDirection,
            StalkerTargetEligibilityResult confirmedEligibility)
        {
            if (!confirmedPlayerId.IsValid)
            {
                throw new ArgumentException(
                    "Occupied hide spot inspection requires a valid player id.",
                    nameof(confirmedPlayerId));
            }

            return new StalkerHideSpotInspectionResult(
                true,
                true,
                confirmedPlayerId,
                confirmedPosition,
                confirmedDirection,
                confirmedEligibility);
        }
    }

    public interface IStalkerHideSpotInspectionResolver
    {
        bool TryResolveInspection(
            StalkerHideSpotCandidate candidate,
            out StalkerHideSpotInspectionResult result);
    }
}
