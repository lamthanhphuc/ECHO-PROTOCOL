using System.Collections.Generic;
using EchoProtocol.AI.Common;
using UnityEngine;
using UnityEngine.AI;

namespace EchoProtocol.AI.Stalker.Special
{
    public static class StalkerJumpEntrySelector
    {
        public static bool TrySelect(
            StalkerJumpEntryRegistry registry,
            IReadOnlyList<Transform> alivePlayers,
            PlayerId excludedPlayerId,
            AiSimulationTime now,
            StalkerSpecialEncounterSettings settings,
            out Vector3 entryPosition)
        {
            entryPosition = default;

            if (alivePlayers == null
                || alivePlayers.Count == 0
                || settings == null)
            {
                return false;
            }

            StalkerJumpEntryPoint bestAuthoredEntry = null;
            var bestAuthoredScore = float.NegativeInfinity;

            if (registry != null)
            {
                var entries = registry.Entries;

                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];

                    if (entry == null
                        || !entry.JumpInAllowed
                        || !registry.IsReuseEligible(
                            entry,
                            now,
                            settings.MinimumEntryReuseSeconds))
                    {
                        continue;
                    }

                    if (!TryScorePosition(
                            entry.EntryPosition,
                            alivePlayers,
                            entry.MinPlayerDistance,
                            entry.MaxPlayerDistance,
                            entry.FrontFacingDotThreshold,
                            settings.PreferredJumpInDistance,
                            settings.GroupRadius,
                            settings.CandidateLosHeight,
                            out var score))
                    {
                        continue;
                    }

                    if (score <= bestAuthoredScore)
                    {
                        continue;
                    }

                    bestAuthoredScore = score;
                    bestAuthoredEntry = entry;
                }
            }

            if (bestAuthoredEntry != null)
            {
                entryPosition =
                    bestAuthoredEntry.EntryPosition;

                registry.MarkUsed(
                    bestAuthoredEntry,
                    now);

                return true;
            }

            if (!settings.AllowDynamicEntryFallback)
            {
                return false;
            }

            return TrySelectDynamic(
                alivePlayers,
                settings,
                out entryPosition);
        }

        private static bool TrySelectDynamic(
            IReadOnlyList<Transform> alivePlayers,
            StalkerSpecialEncounterSettings settings,
            out Vector3 entryPosition)
        {
            entryPosition = default;

            var bestScore =
                float.NegativeInfinity;

            var foundCandidate =
                false;

            var samples =
                settings.DynamicArcSampleCount;

            var halfAngle =
                settings.DynamicArcHalfAngleDegrees;

            var distances =
                new[]
                {
                    settings.PreferredJumpInDistance,
                    settings.JumpInMinDistance,
                    settings.JumpInMaxDistance
                };

            for (var playerIndex = 0;
                 playerIndex < alivePlayers.Count;
                 playerIndex++)
            {
                var anchor =
                    alivePlayers[playerIndex];

                if (anchor == null)
                {
                    continue;
                }

                var anchorForward =
                    anchor.forward;

                anchorForward.y = 0f;

                if (anchorForward.sqrMagnitude
                    <= 0.0001f)
                {
                    continue;
                }

                anchorForward.Normalize();

                for (var sampleIndex = 0;
                     sampleIndex < samples;
                     sampleIndex++)
                {
                    var t =
                        samples == 1
                            ? 0.5f
                            : sampleIndex
                                / (float)(samples - 1);

                    var angle =
                        Mathf.Lerp(
                            -halfAngle,
                            halfAngle,
                            t);

                    //
                    // IMPORTANT:
                    // Candidate is generated in FRONT of
                    // the Player, not behind the Player.
                    //
                    var direction =
                        Quaternion.AngleAxis(
                            angle,
                            Vector3.up)
                        * anchorForward;

                    direction.y = 0f;

                    if (direction.sqrMagnitude
                        <= 0.0001f)
                    {
                        continue;
                    }

                    direction.Normalize();

                    for (var distanceIndex = 0;
                         distanceIndex < distances.Length;
                         distanceIndex++)
                    {
                        var distance =
                            distances[distanceIndex];

                        var candidate =
                            anchor.position
                            + direction * distance;

                        if (!NavMesh.SamplePosition(
                                candidate,
                                out var hit,
                                settings.DynamicNavMeshSampleRadius,
                                NavMesh.AllAreas))
                        {
                            continue;
                        }

                        if (!TryScorePosition(
                                hit.position,
                                alivePlayers,
                                settings.JumpInMinDistance,
                                settings.JumpInMaxDistance,
                                settings.FrontFacingDotThreshold,
                                settings.PreferredJumpInDistance,
                                settings.GroupRadius,
                                settings.CandidateLosHeight,
                                out var score))
                        {
                            continue;
                        }

                        if (score <= bestScore)
                        {
                            continue;
                        }

                        bestScore = score;
                        entryPosition = hit.position;
                        foundCandidate = true;
                    }
                }
            }

            return foundCandidate;
        }

        private static bool TryScorePosition(
            Vector3 position,
            IReadOnlyList<Transform> alivePlayers,
            float minDistance,
            float maxDistance,
            float frontFacingDotThreshold,
            float preferredDistance,
            float groupRadius,
            float candidateLosHeight,
            out float score)
        {
            score =
                float.NegativeInfinity;

            var foundEligiblePlayer =
                false;

            var clampedMinDistance =
                Mathf.Max(
                    0f,
                    minDistance);

            var clampedMaxDistance =
                Mathf.Max(
                    clampedMinDistance,
                    maxDistance);

            var preferred =
                Mathf.Clamp(
                    preferredDistance,
                    clampedMinDistance,
                    clampedMaxDistance);

            var facingThreshold =
                Mathf.Clamp(
                    frontFacingDotThreshold,
                    -1f,
                    1f);

            var distanceRange =
                Mathf.Max(
                    0.01f,
                    clampedMaxDistance
                        - clampedMinDistance);

            var groupRadiusSqr =
                Mathf.Max(
                    0f,
                    groupRadius);

            groupRadiusSqr *=
                groupRadiusSqr;

            for (var i = 0;
                 i < alivePlayers.Count;
                 i++)
            {
                var player =
                    alivePlayers[i];

                if (player == null)
                {
                    continue;
                }

                var playerForward =
                    player.forward;

                playerForward.y = 0f;

                if (playerForward.sqrMagnitude
                    <= 0.0001f)
                {
                    continue;
                }

                playerForward.Normalize();

                var playerToEntry =
                    position - player.position;

                playerToEntry.y = 0f;

                var distance =
                    playerToEntry.magnitude;

                if (distance
                        < clampedMinDistance
                    || distance
                        > clampedMaxDistance
                    || distance <= 0.0001f)
                {
                    continue;
                }

                var directionToEntry =
                    playerToEntry / distance;

                //
                // Positive dot means the entry is physically
                // in FRONT of the Player's current facing.
                //
                var facingDot =
                    Vector3.Dot(
                        playerForward,
                        directionToEntry);

                if (facingDot
                    < facingThreshold)
                {
                    continue;
                }

                if (!HasClearPlayerToEntryLine(
                        player,
                        position,
                        candidateLosHeight))
                {
                    continue;
                }

                var distanceError =
                    Mathf.Abs(
                        distance
                        - preferred);

                var distanceScore =
                    1f
                    - Mathf.Clamp01(
                        distanceError
                        / distanceRange);

                var nearbyPlayers =
                    CountNearbyPlayers(
                        player.position,
                        alivePlayers,
                        groupRadiusSqr);

                var candidateScore =
                    facingDot * 4f
                    + distanceScore * 2f
                    + nearbyPlayers;

                if (!foundEligiblePlayer
                    || candidateScore > score)
                {
                    score =
                        candidateScore;

                    foundEligiblePlayer =
                        true;
                }
            }

            return foundEligiblePlayer;
        }

        private static bool HasClearPlayerToEntryLine(
            Transform player,
            Vector3 entryPosition,
            float losHeight)
        {
            if (player == null)
            {
                return false;
            }

            var height =
                Mathf.Max(
                    0.1f,
                    losHeight);

            var start =
                player.position
                + Vector3.up * height;

            var end =
                entryPosition
                + Vector3.up * height;

            var delta =
                end - start;

            var distance =
                delta.magnitude;

            if (distance <= 0.0001f)
            {
                return true;
            }

            var direction =
                delta / distance;

            //
            // Move the ray endpoints slightly away from the
            // Player/candidate positions so their own colliders
            // do not immediately invalidate the visibility test.
            //
            const float endpointInset = 0.25f;

            if (distance > endpointInset * 2f)
            {
                start +=
                    direction * endpointInset;

                end -=
                    direction * endpointInset;

                delta =
                    end - start;

                distance =
                    delta.magnitude;

                if (distance <= 0.0001f)
                {
                    return true;
                }

                direction =
                    delta / distance;
            }

            var hits =
                Physics.RaycastAll(
                    start,
                    direction,
                    distance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);

            for (var i = 0;
                 i < hits.Length;
                 i++)
            {
                var collider =
                    hits[i].collider;

                if (collider == null)
                {
                    continue;
                }

                var hitTransform =
                    collider.transform;

                if (hitTransform == player
                    || hitTransform.IsChildOf(player))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private static int CountNearbyPlayers(
            Vector3 anchorPosition,
            IReadOnlyList<Transform> alivePlayers,
            float radiusSqr)
        {
            if (radiusSqr <= 0f)
            {
                return 0;
            }

            var count = 0;

            for (var i = 0;
                 i < alivePlayers.Count;
                 i++)
            {
                var player =
                    alivePlayers[i];

                if (player == null)
                {
                    continue;
                }

                var delta =
                    player.position
                    - anchorPosition;

                delta.y = 0f;

                if (delta.sqrMagnitude
                    <= radiusSqr)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
