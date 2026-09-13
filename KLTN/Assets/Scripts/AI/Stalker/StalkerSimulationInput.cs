using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common;
using EchoProtocol.AI.Listener.Perception;

namespace EchoProtocol.AI.Stalker
{
    public readonly struct StalkerSimulationInput
    {
        public StalkerSimulationInput(
            AiSimulationStep step,
            IReadOnlyList<StalkerTargetCandidate> visibleTargetCandidates)
            : this(
                step,
                visibleTargetCandidates,
                null,
                null,
                null,
                default)
        {
        }

        public StalkerSimulationInput(
            AiSimulationStep step,
            IReadOnlyList<StalkerTargetCandidate> visibleTargetCandidates,
            IReadOnlyList<StalkerTargetStatus> targetStatuses)
            : this(
                step,
                visibleTargetCandidates,
                targetStatuses,
                null,
                null,
                default)
        {
        }

        public StalkerSimulationInput(
            AiSimulationStep step,
            IReadOnlyList<StalkerTargetCandidate> visibleTargetCandidates,
            IReadOnlyList<StalkerTargetStatus> targetStatuses,
            StalkerAttackTargetSnapshot? currentAttackTargetSnapshot)
            : this(
                step,
                visibleTargetCandidates,
                targetStatuses,
                currentAttackTargetSnapshot,
                null,
                default)
        {
        }

        public StalkerSimulationInput(
            AiSimulationStep step,
            IReadOnlyList<StalkerTargetCandidate> visibleTargetCandidates,
            IReadOnlyList<StalkerTargetStatus> targetStatuses,
            StalkerAttackTargetSnapshot? currentAttackTargetSnapshot,
            IReadOnlyList<HearingObservation> hearingObservations)
            : this(
                step,
                visibleTargetCandidates,
                targetStatuses,
                currentAttackTargetSnapshot,
                hearingObservations,
                ResolveHearingEvaluationTimeUtc(
                    hearingObservations))
        {
        }

        public StalkerSimulationInput(
            AiSimulationStep step,
            IReadOnlyList<StalkerTargetCandidate> visibleTargetCandidates,
            IReadOnlyList<StalkerTargetStatus> targetStatuses,
            StalkerAttackTargetSnapshot? currentAttackTargetSnapshot,
            IReadOnlyList<HearingObservation> hearingObservations,
            DateTime hearingEvaluationTimeUtc)
        {
            if (hearingEvaluationTimeUtc != default
                && hearingEvaluationTimeUtc.Kind
                    != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Hearing evaluation time must be UTC.",
                    nameof(hearingEvaluationTimeUtc));
            }

            Step = step;
            VisibleTargetCandidates =
                visibleTargetCandidates;
            TargetStatuses =
                targetStatuses;
            CurrentAttackTargetSnapshot =
                currentAttackTargetSnapshot;
            HearingObservations =
                hearingObservations;
            HearingEvaluationTimeUtc =
                hearingEvaluationTimeUtc;
        }

        public AiSimulationStep Step { get; }

        public IReadOnlyList<StalkerTargetCandidate>
            VisibleTargetCandidates { get; }

        public IReadOnlyList<StalkerTargetStatus>
            TargetStatuses { get; }

        public StalkerAttackTargetSnapshot?
            CurrentAttackTargetSnapshot { get; }

        /// <summary>
        /// Authoritative hearing observations available to the Stalker
        /// for this simulation step.
        ///
        /// Hearing observations are positional hypotheses only.
        /// They do not imply a player identity.
        /// </summary>
        public IReadOnlyList<HearingObservation>
            HearingObservations { get; }

        /// <summary>
        /// UTC time at which this hearing frame was evaluated.
        ///
        /// This keeps expiration/arbitration independent from
        /// wall-clock reads inside StalkerController.
        /// </summary>
        public DateTime HearingEvaluationTimeUtc { get; }

        public bool HasHearingEvaluationTimeUtc =>
            HearingEvaluationTimeUtc != default
            && HearingEvaluationTimeUtc.Kind
                == DateTimeKind.Utc;

        private static DateTime ResolveHearingEvaluationTimeUtc(
            IReadOnlyList<HearingObservation> observations)
        {
            if (observations == null
                || observations.Count == 0)
            {
                return default;
            }

            var latest = default(DateTime);

            for (var i = 0;
                 i < observations.Count;
                 i++)
            {
                var heardAtUtc =
                    observations[i].HeardAtUtc;

                if (heardAtUtc.Kind
                    != DateTimeKind.Utc)
                {
                    continue;
                }

                if (latest == default
                    || heardAtUtc > latest)
                {
                    latest = heardAtUtc;
                }
            }

            return latest;
        }
    }
}
