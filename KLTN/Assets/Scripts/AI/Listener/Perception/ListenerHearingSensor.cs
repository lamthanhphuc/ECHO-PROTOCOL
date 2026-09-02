using System;
using EchoProtocol.AI.Listener.Noise;
using UnityEngine;

namespace EchoProtocol.AI.Listener.Perception
{
    public sealed class ListenerHearingSensor
    {
        private readonly IListenerOcclusionResolver _occlusionResolver;
        private readonly ListenerHearingPolicy _policy;
        private Guid _boundMatchId;
        private ulong _highestEvaluatedPublicationOrdinal;
        private bool _hasEvaluatedPublicationOrdinal;

        public ListenerHearingSensor(
            IListenerOcclusionResolver occlusionResolver,
            ListenerHearingPolicy? policy = null)
        {
            _occlusionResolver = occlusionResolver ?? throw new ArgumentNullException(nameof(occlusionResolver));
            _policy = policy ?? ListenerHearingPolicy.CreateImplementationDefault();
        }

        public ListenerHearingEvaluationStatus LastEvaluationStatus { get; private set; }

        public void BeginMatch(Guid matchId)
        {
            if (matchId == Guid.Empty)
            {
                throw new ArgumentException("Match id must not be empty.", nameof(matchId));
            }

            if (_boundMatchId == matchId)
            {
                return;
            }

            _boundMatchId = matchId;
            ResetWatermark();
        }

        public void EndMatch()
        {
            _boundMatchId = Guid.Empty;
            ResetWatermark();
        }

        public bool TryEvaluate(
            RuntimeNoiseEvent noiseEvent,
            Vector3 listenerHearingOrigin,
            DateTime heardAtUtc,
            out HearingObservation observation,
            out ListenerHearingRejectReason rejectReason)
        {
            observation = default;
            rejectReason = ListenerHearingRejectReason.None;
            LastEvaluationStatus = ListenerHearingEvaluationStatus.None;
            if (heardAtUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("Hearing evaluation time must be UTC.", nameof(heardAtUtc));
            }

            if (_boundMatchId == Guid.Empty)
            {
                LastEvaluationStatus = ListenerHearingEvaluationStatus.NotMatchBound;
                return false;
            }

            if (string.IsNullOrWhiteSpace(noiseEvent.NoiseEventId)
                || noiseEvent.HearingRadius <= 0d
                || !RuntimeNoiseDefinition.IsFinite(noiseEvent.HearingRadius)
                || !RuntimeNoiseDefinition.IsFinite(noiseEvent.Loudness))
            {
                rejectReason = ListenerHearingRejectReason.InvalidEvent;
                return false;
            }

            if (_hasEvaluatedPublicationOrdinal
                && noiseEvent.EventOrderKey.Ordinal <= _highestEvaluatedPublicationOrdinal)
            {
                LastEvaluationStatus = ListenerHearingEvaluationStatus.AlreadyEvaluated;
                return false;
            }

            _highestEvaluatedPublicationOrdinal = noiseEvent.EventOrderKey.Ordinal;
            _hasEvaluatedPublicationOrdinal = true;
            LastEvaluationStatus = ListenerHearingEvaluationStatus.Evaluated;

            if (noiseEvent.IsExpiredAt(heardAtUtc))
            {
                rejectReason = ListenerHearingRejectReason.Expired;
                return false;
            }

            var distance = Vector3.Distance(listenerHearingOrigin, noiseEvent.WorldPosition);
            if (distance > noiseEvent.HearingRadius)
            {
                rejectReason = ListenerHearingRejectReason.OutsideRange;
                return false;
            }

            var occlusionClass = _occlusionResolver.Classify(listenerHearingOrigin, noiseEvent.WorldPosition);
            if (occlusionClass == ListenerOcclusionClass.QUERY_FAILED)
            {
                rejectReason = ListenerHearingRejectReason.OcclusionQueryFailed;
                return false;
            }

            var distanceFactor = Math.Max(0d, Math.Min(1d, 1d - distance / noiseEvent.HearingRadius));
            var effectiveIntensity = noiseEvent.Loudness
                * distanceFactor
                * _policy.OcclusionMultiplier(occlusionClass);
            if (effectiveIntensity < _policy.HearingThreshold)
            {
                rejectReason = occlusionClass == ListenerOcclusionClass.CLEAR
                    || occlusionClass == ListenerOcclusionClass.OPEN_DOOR
                        ? ListenerHearingRejectReason.BelowThreshold
                        : ListenerHearingRejectReason.OccludedBelowThreshold;
                return false;
            }

            observation = new HearingObservation(
                noiseEvent.NoiseEventId,
                noiseEvent.EventOrderKey,
                noiseEvent.NoiseType,
                noiseEvent.WorldPosition,
                noiseEvent.EmittedAtUtc,
                heardAtUtc,
                noiseEvent.ExpiresAtUtc,
                distance,
                noiseEvent.Loudness,
                effectiveIntensity,
                occlusionClass);
            return true;
        }

        private void ResetWatermark()
        {
            _highestEvaluatedPublicationOrdinal = 0;
            _hasEvaluatedPublicationOrdinal = false;
            LastEvaluationStatus = ListenerHearingEvaluationStatus.None;
        }
    }

    public sealed class StaticListenerOcclusionResolver : IListenerOcclusionResolver
    {
        private readonly ListenerOcclusionClass _occlusionClass;

        public StaticListenerOcclusionResolver(ListenerOcclusionClass occlusionClass)
        {
            _occlusionClass = occlusionClass;
        }

        public ListenerOcclusionClass Classify(Vector3 listenerPosition, Vector3 noisePosition)
        {
            return _occlusionClass;
        }
    }
}
