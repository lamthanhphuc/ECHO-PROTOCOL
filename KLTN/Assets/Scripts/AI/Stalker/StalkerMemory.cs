using System;
using EchoProtocol.AI.Common;
using UnityEngine;

namespace EchoProtocol.AI.Stalker
{
    public sealed class StalkerMemory
    {
        public PlayerId DetectionTargetId { get; private set; } = PlayerId.Invalid;

        public PlayerId CurrentTargetId { get; private set; } = PlayerId.Invalid;

        public float DetectionMeter { get; private set; }

        public bool HasLastKnownPosition { get; private set; }

        public Vector3 LastKnownPosition { get; private set; }

        public bool HasLastSeenDirection { get; private set; }

        public Vector3 LastSeenDirection { get; private set; }

        public bool HasTargetLastSeenTime { get; private set; }

        public AiSimulationTime TargetLastSeenTime { get; private set; }

        public bool HasLastCurrentTargetObservation { get; private set; }

        public VisionObservation LastCurrentTargetObservation { get; private set; }

        public bool HasLastDetectionTargetObservation { get; private set; }

        public VisionObservation LastDetectionTargetObservation { get; private set; }

        public void SetDetectionTarget(PlayerId playerId)
        {
            if (!playerId.IsValid)
            {
                throw new ArgumentException("Detection target requires a valid player id.", nameof(playerId));
            }

            if (DetectionTargetId != playerId)
            {
                ClearDetectionTargetObservation();
                if (!CurrentTargetId.IsValid)
                {
                    ClearObservedKnowledge();
                }
            }

            DetectionTargetId = playerId;
            DetectionMeter = 0f;
        }

        public void ClearDetectionTarget()
        {
            DetectionTargetId = PlayerId.Invalid;
            DetectionMeter = 0f;
            ClearDetectionTargetObservation();
            if (!CurrentTargetId.IsValid)
            {
                ClearObservedKnowledge();
            }
        }

        public void SetDetectionMeter(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Detection meter must be finite and non-negative.");
            }

            DetectionMeter = value;
        }

        public void SetCurrentTarget(PlayerId playerId)
        {
            if (!playerId.IsValid)
            {
                throw new ArgumentException("Current target requires a valid player id.", nameof(playerId));
            }

            if (CurrentTargetId != playerId)
            {
                ClearCurrentTargetKnowledge();
            }

            CurrentTargetId = playerId;
        }

        public void ClearCurrentTarget()
        {
            CurrentTargetId = PlayerId.Invalid;
            ClearCurrentTargetKnowledge();
        }

        public bool TryAcceptCurrentTargetObservation(VisionObservation observation)
        {
            if (!CurrentTargetId.IsValid || observation.PlayerId != CurrentTargetId)
            {
                return false;
            }

            if (HasLastCurrentTargetObservation
                && observation.ObservedAt.CompareTo(LastCurrentTargetObservation.ObservedAt) < 0)
            {
                return false;
            }

            LastKnownPosition = observation.ObservedPosition;
            HasLastKnownPosition = true;
            LastSeenDirection = observation.ObservedDirection;
            HasLastSeenDirection = true;
            TargetLastSeenTime = observation.ObservedAt;
            HasTargetLastSeenTime = true;
            LastCurrentTargetObservation = observation;
            HasLastCurrentTargetObservation = true;
            return true;
        }

        public bool TryAcceptDetectionTargetObservation(VisionObservation observation)
        {
            if (!DetectionTargetId.IsValid || observation.PlayerId != DetectionTargetId)
            {
                return false;
            }

            if (HasLastDetectionTargetObservation
                && observation.ObservedAt.CompareTo(LastDetectionTargetObservation.ObservedAt) < 0)
            {
                return false;
            }

            LastKnownPosition = observation.ObservedPosition;
            HasLastKnownPosition = true;
            LastSeenDirection = observation.ObservedDirection;
            HasLastSeenDirection = true;
            TargetLastSeenTime = observation.ObservedAt;
            HasTargetLastSeenTime = true;
            LastDetectionTargetObservation = observation;
            HasLastDetectionTargetObservation = true;
            return true;
        }

        private void ClearCurrentTargetKnowledge()
        {
            ClearObservedKnowledge();
            LastCurrentTargetObservation = default;
            HasLastCurrentTargetObservation = false;
        }

        private void ClearDetectionTargetObservation()
        {
            LastDetectionTargetObservation = default;
            HasLastDetectionTargetObservation = false;
        }

        private void ClearObservedKnowledge()
        {
            LastKnownPosition = default;
            HasLastKnownPosition = false;
            LastSeenDirection = default;
            HasLastSeenDirection = false;
            TargetLastSeenTime = AiSimulationTime.Invalid;
            HasTargetLastSeenTime = false;
        }
    }
}
