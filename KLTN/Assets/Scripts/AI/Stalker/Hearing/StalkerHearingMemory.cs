using System;
using EchoProtocol.AI.Listener.Perception;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Hearing
{
    /// <summary>
    /// Hearing knowledge owned by Stalker.
    ///
    /// Hearing never creates or infers a PlayerId.
    /// Noise investigation knowledge is intentionally separate from
    /// StalkerMemory.LastKnownPosition, which remains visual player knowledge.
    /// </summary>
    public sealed class StalkerHearingMemory
    {
        public bool HasLastHeardObservation { get; private set; }

        public HearingObservation LastHeardObservation
        {
            get;
            private set;
        }

        public bool HasActiveNoiseInvestigation
        {
            get;
            private set;
        }

        public string ActiveNoiseEventId
        {
            get;
            private set;
        } = string.Empty;

        public Vector3 InvestigationPosition
        {
            get;
            private set;
        }

        public double CommittedEffectiveIntensity
        {
            get;
            private set;
        }

        public DateTime InvestigationStartedAtUtc
        {
            get;
            private set;
        }

        public void RecordHeardObservation(
            HearingObservation observation)
        {
            ValidateObservation(observation);

            if (HasLastHeardObservation
                && HearingObservationComparer.Instance.Compare(
                    observation,
                    LastHeardObservation) > 0)
            {
                //
                // Do not replace a stronger/newer deterministic hearing
                // observation with a lower-ranked one.
                //
                return;
            }

            LastHeardObservation = observation;
            HasLastHeardObservation = true;
        }

        public void BeginNoiseInvestigation(
            HearingObservation observation)
        {
            ValidateObservation(observation);

            ActiveNoiseEventId = observation.NoiseEventId;
            InvestigationPosition =
                observation.ObservedNoisePosition;
            CommittedEffectiveIntensity =
                observation.EffectiveIntensity;
            InvestigationStartedAtUtc =
                observation.HeardAtUtc;

            HasActiveNoiseInvestigation = true;

            RecordHeardObservation(observation);
        }

        public void UpdateNoiseInvestigation(
            HearingObservation observation)
        {
            ValidateObservation(observation);

            if (!HasActiveNoiseInvestigation)
            {
                throw new InvalidOperationException(
                    "Stalker has no active noise investigation.");
            }

            ActiveNoiseEventId = observation.NoiseEventId;
            InvestigationPosition =
                observation.ObservedNoisePosition;

            CommittedEffectiveIntensity =
                Math.Max(
                    CommittedEffectiveIntensity,
                    observation.EffectiveIntensity);

            RecordHeardObservation(observation);
        }

        public void ClearNoiseInvestigation()
        {
            ActiveNoiseEventId = string.Empty;
            InvestigationPosition = default;
            CommittedEffectiveIntensity = 0d;
            InvestigationStartedAtUtc = default;

            HasActiveNoiseInvestigation = false;
        }

        public void Reset()
        {
            ClearNoiseInvestigation();

            LastHeardObservation = default;
            HasLastHeardObservation = false;
        }

        private static void ValidateObservation(
            HearingObservation observation)
        {
            if (string.IsNullOrWhiteSpace(
                    observation.NoiseEventId))
            {
                throw new ArgumentException(
                    "Stalker hearing requires a valid hearing observation.",
                    nameof(observation));
            }

            if (double.IsNaN(
                    observation.EffectiveIntensity)
                || double.IsInfinity(
                    observation.EffectiveIntensity)
                || observation.EffectiveIntensity < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(observation),
                    "Hearing observation intensity must be finite and non-negative.");
            }

            if (observation.HeardAtUtc.Kind
                != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Hearing observation time must be UTC.",
                    nameof(observation));
            }
        }
    }
}
