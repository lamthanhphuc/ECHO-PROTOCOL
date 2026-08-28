using System;
using UnityEngine;

namespace EchoProtocol.AI.Stalker
{
    public readonly struct StalkerPhysicalVisionObservation : IEquatable<StalkerPhysicalVisionObservation>
    {
        public StalkerPhysicalVisionObservation(
            Transform candidate,
            Vector3 observedPosition,
            Vector3 observedDirection,
            float distance)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
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

            Candidate = candidate;
            ObservedPosition = observedPosition;
            ObservedDirection = observedDirection.normalized;
            Distance = distance;
        }

        public Transform Candidate { get; }

        public Vector3 ObservedPosition { get; }

        public Vector3 ObservedDirection { get; }

        public float Distance { get; }

        public bool Equals(StalkerPhysicalVisionObservation other)
        {
            return Candidate == other.Candidate
                && ObservedPosition.Equals(other.ObservedPosition)
                && ObservedDirection.Equals(other.ObservedDirection)
                && Distance.Equals(other.Distance);
        }

        public override bool Equals(object obj)
        {
            return obj is StalkerPhysicalVisionObservation other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + (Candidate != null ? Candidate.GetHashCode() : 0);
                hash = (hash * 31) + ObservedPosition.GetHashCode();
                hash = (hash * 31) + ObservedDirection.GetHashCode();
                hash = (hash * 31) + Distance.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(StalkerPhysicalVisionObservation left, StalkerPhysicalVisionObservation right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StalkerPhysicalVisionObservation left, StalkerPhysicalVisionObservation right)
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
