using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoProtocol.AI.Stalker
{
    public sealed class StalkerHideSpotSelector
    {
        public bool TrySelect(
            Vector3 searchAnchor,
            IReadOnlyList<StalkerHideSpotCandidate> candidates,
            StalkerHideSpotMemory memory,
            float currentTimeSeconds,
            StalkerHideSpotSelectorConfig config,
            out StalkerHideSpotSelection selection)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (memory == null)
            {
                throw new ArgumentNullException(nameof(memory));
            }

            selection = default;
            var hasSelection = false;
            var bestScore = 0f;

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (!candidate.IsValid || candidate.StableId == 0UL)
                {
                    continue;
                }

                var distance = Vector3.Distance(searchAnchor, candidate.Position);
                if (distance > config.SearchRadius)
                {
                    continue;
                }

                var history = memory.GetSnapshot(candidate.StableId);
                if (IsInsideReinspectionCooldown(
                        history,
                        currentTimeSeconds,
                        config.ReinspectCooldownSeconds))
                {
                    continue;
                }

                var score = distance;
                if (history.HasConfirmedUse)
                {
                    score -= config.ConfirmedUseBias;
                }

                if (history.ConsecutiveEmptyInspections > 0)
                {
                    score += config.EmptyInspectionPenalty
                        * history.ConsecutiveEmptyInspections;
                }

                if (!hasSelection
                    || score < bestScore - config.DistanceTieEpsilon
                    || (Mathf.Abs(score - bestScore) <= config.DistanceTieEpsilon
                        && candidate.StableId < selection.Candidate.StableId))
                {
                    bestScore = score;
                    selection = new StalkerHideSpotSelection(candidate, score);
                    hasSelection = true;
                }
            }

            return hasSelection;
        }

        private static bool IsInsideReinspectionCooldown(
            StalkerHideSpotMemorySnapshot history,
            float currentTimeSeconds,
            float cooldownSeconds)
        {
            if (cooldownSeconds <= 0f
                || !history.HasInspectionHistory
                || history.ConsecutiveEmptyInspections <= 0)
            {
                return false;
            }

            return currentTimeSeconds - (float)history.LastInspectedTime.Seconds
                < cooldownSeconds;
        }
    }
}
