using System;
using System.Collections.Generic;
using EchoProtocol.AI.Listener.Noise;
using EchoProtocol.Networking;
using UnityEngine;

namespace EchoProtocol.AI.Listener.Perception
{
    public enum ListenerOcclusionClass
    {
        CLEAR,
        OPEN_DOOR,
        CLOSED_DOOR,
        SOLID_WALL,
        QUERY_FAILED
    }

    public enum ListenerHearingRejectReason
    {
        None,
        Expired,
        OutsideRange,
        BelowThreshold,
        OccludedBelowThreshold,
        OcclusionQueryFailed,
        InvalidEvent
    }

    public enum ListenerHearingEvaluationStatus
    {
        None,
        NotMatchBound,
        Evaluated,
        AlreadyEvaluated
    }

    public readonly struct ListenerHearingPolicy
    {
        public ListenerHearingPolicy(
            double hearingThreshold,
            double closedDoorMultiplier,
            double wallMultiplier)
        {
            if (!RuntimeNoiseDefinition.IsFinite(hearingThreshold) || hearingThreshold < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(hearingThreshold));
            }

            if (!RuntimeNoiseDefinition.IsFinite(closedDoorMultiplier)
                || !RuntimeNoiseDefinition.IsFinite(wallMultiplier)
                || wallMultiplier <= 0d
                || wallMultiplier > closedDoorMultiplier
                || closedDoorMultiplier >= 1d)
            {
                throw new ArgumentOutOfRangeException(nameof(wallMultiplier));
            }

            HearingThreshold = hearingThreshold;
            ClosedDoorMultiplier = closedDoorMultiplier;
            WallMultiplier = wallMultiplier;
        }

        public double HearingThreshold { get; }
        public double ClosedDoorMultiplier { get; }
        public double WallMultiplier { get; }

        public static ListenerHearingPolicy CreateImplementationDefault()
        {
            // Implementation defaults only. Canonical Listener v1.0 marks final tuning TBD.
            return new ListenerHearingPolicy(0.1d, 0.5d, 0.25d);
        }

        public double OcclusionMultiplier(ListenerOcclusionClass occlusionClass)
        {
            switch (occlusionClass)
            {
                case ListenerOcclusionClass.CLEAR:
                case ListenerOcclusionClass.OPEN_DOOR:
                    return 1d;
                case ListenerOcclusionClass.CLOSED_DOOR:
                    return ClosedDoorMultiplier;
                case ListenerOcclusionClass.SOLID_WALL:
                    return WallMultiplier;
                default:
                    return 0d;
            }
        }
    }

    public readonly struct HearingObservation
    {
        public HearingObservation(
            string noiseEventId,
            RuntimeNoiseEventOrderKey eventOrderKey,
            RuntimeNoiseType noiseType,
            Vector3 observedNoisePosition,
            DateTime emittedAtUtc,
            DateTime heardAtUtc,
            DateTime expiresAtUtc,
            double distance,
            double rawLoudness,
            double effectiveIntensity,
            ListenerOcclusionClass occlusionClass)
        {
            NoiseEventId = noiseEventId ?? string.Empty;
            EventOrderKey = eventOrderKey;
            NoiseType = noiseType;
            ObservedNoisePosition = observedNoisePosition;
            EmittedAtUtc = emittedAtUtc;
            HeardAtUtc = heardAtUtc;
            ExpiresAtUtc = expiresAtUtc;
            Distance = distance;
            RawLoudness = rawLoudness;
            EffectiveIntensity = effectiveIntensity;
            OcclusionClass = occlusionClass;
        }

        public string NoiseEventId { get; }
        public RuntimeNoiseEventOrderKey EventOrderKey { get; }
        public RuntimeNoiseType NoiseType { get; }
        public Vector3 ObservedNoisePosition { get; }
        public DateTime EmittedAtUtc { get; }
        public DateTime HeardAtUtc { get; }
        public DateTime ExpiresAtUtc { get; }
        public double Distance { get; }
        public double RawLoudness { get; }
        public double EffectiveIntensity { get; }
        public ListenerOcclusionClass OcclusionClass { get; }

        public bool IsExpiredAt(DateTime nowUtc)
        {
            if (nowUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("Hearing observation time must be UTC.", nameof(nowUtc));
            }

            return nowUtc >= ExpiresAtUtc;
        }
    }

    public interface IListenerOcclusionResolver
    {
        ListenerOcclusionClass Classify(Vector3 listenerPosition, Vector3 noisePosition);
    }

    public static class ListenerOcclusionClassifier
    {
        public static ListenerOcclusionClass Strongest(
            ListenerOcclusionClass first,
            ListenerOcclusionClass second)
        {
            return Rank(first) >= Rank(second) ? first : second;
        }

        public static ListenerOcclusionClass ClassifyDoorState(NetworkDoorState doorState)
        {
            switch (doorState)
            {
                case NetworkDoorState.Open:
                    return ListenerOcclusionClass.OPEN_DOOR;
                case NetworkDoorState.Closed:
                case NetworkDoorState.Locked:
                    return ListenerOcclusionClass.CLOSED_DOOR;
                default:
                    return ListenerOcclusionClass.CLEAR;
            }
        }

        private static int Rank(ListenerOcclusionClass occlusionClass)
        {
            switch (occlusionClass)
            {
                case ListenerOcclusionClass.QUERY_FAILED:
                    return 4;
                case ListenerOcclusionClass.SOLID_WALL:
                    return 3;
                case ListenerOcclusionClass.CLOSED_DOOR:
                    return 2;
                case ListenerOcclusionClass.OPEN_DOOR:
                    return 1;
                default:
                    return 0;
            }
        }
    }

    public sealed class HearingObservationComparer : IComparer<HearingObservation>
    {
        public static readonly HearingObservationComparer Instance = new HearingObservationComparer();

        private HearingObservationComparer()
        {
        }

        public int Compare(HearingObservation left, HearingObservation right)
        {
            var intensity = right.EffectiveIntensity.CompareTo(left.EffectiveIntensity);
            if (intensity != 0) return intensity;

            var emittedAt = right.EmittedAtUtc.CompareTo(left.EmittedAtUtc);
            if (emittedAt != 0) return emittedAt;

            var distance = left.Distance.CompareTo(right.Distance);
            if (distance != 0) return distance;

            return left.EventOrderKey.CompareTo(right.EventOrderKey);
        }
    }
}
