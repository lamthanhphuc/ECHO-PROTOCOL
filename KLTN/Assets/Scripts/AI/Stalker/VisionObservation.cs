using System;
using EchoProtocol.AI.Common;
using UnityEngine;

namespace EchoProtocol.AI.Stalker
{
    public readonly struct VisionObservation : IEquatable<VisionObservation>
    {
        public VisionObservation(
            PlayerId playerId,
            Vector3 observedPosition,
            Vector3 observedDirection,
            AiSimulationTime observedAt,
            float distance)
        {
            if (!playerId.IsValid)
            {
                throw new ArgumentException("Vision observation requires a valid player id.", nameof(playerId));
            }

            if (!observedAt.IsValid)
            {
                throw new ArgumentException("Vision observation requires a valid simulation time.", nameof(observedAt));
            }

            if (!IsFinite(observedPosition))
            {
                throw new ArgumentException("Observed position must contain only finite components.", nameof(observedPosition));
            }

            if (!IsFinite(observedDirection))
            {
                throw new ArgumentException("Observed direction must contain only finite components.", nameof(observedDirection));
            }

            if (observedDirection.x == 0f && observedDirection.y == 0f && observedDirection.z == 0f)
            {
                throw new ArgumentException("Observed direction must be non-zero.", nameof(observedDirection));
            }

            if (!IsFinite(distance) || distance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(distance), distance, "Observation distance must be finite and non-negative.");
            }

            PlayerId = playerId;
            ObservedPosition = observedPosition;
            ObservedDirection = observedDirection.normalized;
            ObservedAt = observedAt;
            Distance = distance;
        }

        public PlayerId PlayerId { get; }

        public Vector3 ObservedPosition { get; }

        public Vector3 ObservedDirection { get; }

        public AiSimulationTime ObservedAt { get; }

        public float Distance { get; }

        public bool Equals(VisionObservation other)
        {
            return PlayerId == other.PlayerId
                && ObservedPosition.Equals(other.ObservedPosition)
                && ObservedDirection.Equals(other.ObservedDirection)
                && ObservedAt == other.ObservedAt
                && Distance.Equals(other.Distance);
        }

        public override bool Equals(object obj)
        {
            return obj is VisionObservation other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + PlayerId.GetHashCode();
                hash = (hash * 31) + ObservedPosition.GetHashCode();
                hash = (hash * 31) + ObservedDirection.GetHashCode();
                hash = (hash * 31) + ObservedAt.GetHashCode();
                hash = (hash * 31) + Distance.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(VisionObservation left, VisionObservation right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VisionObservation left, VisionObservation right)
        {
            return !left.Equals(right);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x)
                && IsFinite(value.y)
                && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
