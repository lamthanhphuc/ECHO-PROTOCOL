using System.Collections.Generic;
using EchoProtocol.AI.Common;
using EchoProtocol.Diagnostics;
using EchoProtocol.Networking;
using EchoProtocol.Player;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

namespace EchoProtocol.AI.Stalker
{
    public sealed class StalkerUnityHideSpotAdapter : IStalkerHideSpotInspectionResolver
    {
        private const float InspectPointNavMeshSampleRadius = 1.5f;
        private const float RevealedHideSpotLockoutSeconds = 15f;

        private readonly Dictionary<ulong, global::HidingSpot> _spotsById =
            new Dictionary<ulong, global::HidingSpot>();

        public int CollectCandidates(
            List<StalkerHideSpotCandidate> results)
        {
            if (results == null)
            {
                throw new System.ArgumentNullException(nameof(results));
            }

            results.Clear();
            _spotsById.Clear();

            var spots = UnityEngine.Object.FindObjectsByType<global::HidingSpot>(
                FindObjectsInactive.Exclude);

            for (var i = 0; i < spots.Length; i++)
            {
                var spot = spots[i];

                if (spot == null
                    || !spot.isActiveAndEnabled)
                {
                    continue;
                }

                var parentTransform =
                    spot.transform.parent;

                var parentHideSpot =
                    parentTransform != null
                        ? parentTransform.GetComponentInParent<global::HidingSpot>()
                        : null;

                if (parentHideSpot != null
                    && parentHideSpot != spot)
                {
                    RuntimeLog.Log(
                        RuntimeLogCategory.StalkerHideFlow,
                        $"[STK_HIDE_FLOW][NESTED_SPOT_SKIPPED] " +
                        $"child={spot.name} " +
                        $"parent={parentHideSpot.name}",
                        spot);

                    continue;
                }

                if (spot.IsTemporarilyLockedOut())
                {
                    continue;
                }

                var stableId = spot.StableId;
                var inspectPoint = spot.InspectPoint != null
                    ? spot.InspectPoint
                    : spot.ExitPoint != null
                        ? spot.ExitPoint
                        : spot.transform;

                var rawInspectPosition = inspectPoint.position;

                var inspectPosition = rawInspectPosition;

                if (NavMesh.SamplePosition(
                        rawInspectPosition,
                        out var inspectHit,
                        InspectPointNavMeshSampleRadius,
                        NavMesh.AllAreas))
                {
                    inspectPosition = inspectHit.position;

                    RuntimeLog.Log(
                        RuntimeLogCategory.StalkerHideFlow,
                        $"[STK_HIDE_FLOW][INSPECT_NAVMESH_SNAP] " +
                        $"stableId={stableId} " +
                        $"raw={rawInspectPosition} " +
                        $"sampled={inspectPosition} " +
                        $"delta={Vector3.Distance(rawInspectPosition, inspectPosition):F2}",
                        inspectPoint);
                }
                else
                {
                    RuntimeLog.Log(
                        RuntimeLogCategory.StalkerHideFlow,
                        $"[STK_HIDE_FLOW][INSPECT_NAVMESH_FALLBACK] " +
                        $"stableId={stableId} " +
                        $"raw={rawInspectPosition}",
                        inspectPoint);
                }

                results.Add(new StalkerHideSpotCandidate(
                    stableId,
                    spot.transform.position,
                    inspectPosition,
                    spot.gameObject.activeInHierarchy));

                _spotsById[stableId] = spot;
            }

            return results.Count;
        }

        public bool TryResolveInspection(
            StalkerHideSpotCandidate candidate,
            out StalkerHideSpotInspectionResult result)
        {
            result = default;

            if (!_spotsById.TryGetValue(candidate.StableId, out var spot)
                || spot == null
                || !spot.isActiveAndEnabled)
            {
                return false;
            }

            var networkMovements =
                UnityEngine.Object.FindObjectsByType<NetworkPlayerMovement>(
                    FindObjectsInactive.Exclude);

            for (var i = 0; i < networkMovements.Length; i++)
            {
                var movement = networkMovements[i];

                if (movement == null
                    || !movement.IsHidden
                    || movement.CurrentHideSpotId != candidate.StableId)
                {
                    continue;
                }

                if (movement.Object == null
                    || !movement.Object.IsValid
                    || !movement.Object.HasStateAuthority)
                {
                    return false;
                }

                if (!TryResolvePlayerId(movement, out var playerId))
                {
                    return false;
                }

                var exitPoint = spot.ExitPoint != null
                    ? spot.ExitPoint
                    : spot.transform;

                var exitRotation = Quaternion.Euler(
                    0f,
                    exitPoint.eulerAngles.y,
                    0f);

                var preExitPosition =
                    movement.transform.position;

                var forceExitSucceeded =
                    movement.TryForceExitHidingAuthoritative(
                        exitPoint.position,
                        exitRotation);

                RuntimeLog.Log(
                    RuntimeLogCategory.StalkerHideFlow,
                    $"[STK_HIDE_FLOW][FORCE_EXIT] " +
                    $"player={playerId} " +
                    $"stableId={candidate.StableId} " +
                    $"success={forceExitSucceeded} " +
                    $"pre={preExitPosition} " +
                    $"requestedExit={exitPoint.position} " +
                    $"post={movement.transform.position} " +
                    $"hidden={movement.IsHidden}",
                    movement);

                if (!forceExitSucceeded)
                {
                    return false;
                }

                var lockoutBroadcastSucceeded =
                    movement.TryBroadcastHideSpotLockoutAuthoritative(
                        candidate.StableId,
                        RevealedHideSpotLockoutSeconds);

                if (!lockoutBroadcastSucceeded)
                {
                    spot.BeginTemporaryLockout(
                        RevealedHideSpotLockoutSeconds);
                }

                RuntimeLog.Log(
                    RuntimeLogCategory.StalkerHideFlow,
                    $"[STK_HIDE_FLOW][LOCKOUT_TRIGGERED] " +
                    $"stableId={candidate.StableId} " +
                    $"duration={RevealedHideSpotLockoutSeconds:F2} " +
                    $"networked={lockoutBroadcastSucceeded}",
                    spot);

                var occupantTransform = movement.transform;
                var observedPosition = occupantTransform.position;
                var observedDirection =
                    occupantTransform.forward.sqrMagnitude > 0f
                        ? occupantTransform.forward
                        : Vector3.forward;

                var postRevealEligibility =
                    EvaluatePostRevealEligibility(
                        movement,
                        playerId);

                result = StalkerHideSpotInspectionResult.OccupiedBy(
                    playerId,
                    observedPosition,
                    observedDirection,
                    postRevealEligibility);

                return true;
            }

            // Preserve non-network/offline hiding behavior.
            if (!spot.TryInspectOccupant(out var occupant)
                || occupant == null)
            {
                result = StalkerHideSpotInspectionResult.Empty();
                return true;
            }

            if (!TryResolvePlayerId(occupant, out var localPlayerId))
            {
                return false;
            }

            occupant.ExitHiding();

            spot.BeginTemporaryLockout(
                RevealedHideSpotLockoutSeconds);

            RuntimeLog.Log(
                RuntimeLogCategory.StalkerHideFlow,
                $"[STK_HIDE_FLOW][LOCKOUT_TRIGGERED] " +
                $"stableId={candidate.StableId} " +
                $"duration={RevealedHideSpotLockoutSeconds:F2} " +
                $"networked=False",
                spot);

            var localTransform = occupant.transform;
            var localObservedPosition = localTransform.position;
            var localObservedDirection =
                localTransform.forward.sqrMagnitude > 0f
                    ? localTransform.forward
                    : Vector3.forward;

            result = StalkerHideSpotInspectionResult.OccupiedBy(
                localPlayerId,
                localObservedPosition,
                localObservedDirection);

            return true;
        }

        private static bool TryResolvePlayerId(
            NetworkPlayerMovement movement,
            out PlayerId playerId)
        {
            playerId = PlayerId.Invalid;

            if (movement == null)
            {
                return false;
            }

            var identity =
                movement.GetComponentInParent<PlayerRuntimeIdentity>();

            if (identity != null
                && identity.IsBound
                && identity.PlayerId.IsValid)
            {
                playerId = identity.PlayerId;
                return true;
            }

            if (movement.Object != null
                && movement.Object.IsValid)
            {
                var actorId = movement.Object.InputAuthority.PlayerId;

                if (actorId >= 0)
                {
                    playerId = new PlayerId(actorId + 1);
                    return true;
                }
            }

            return false;
        }

        private static StalkerTargetEligibilityResult
            EvaluatePostRevealEligibility(
                NetworkPlayerMovement movement,
                PlayerId playerId)
        {
            if (movement == null
                || !playerId.IsValid
                || movement.Object == null
                || !movement.Object.IsValid)
            {
                return StalkerTargetEligibilityResult.Ineligible(
                    StalkerTargetEligibilityReason.Disconnected);
            }

            var runner = movement.Runner;
            var inputAuthority = movement.Object.InputAuthority;

            var isConnected =
                runner != null
                && inputAuthority.IsValid
                && runner.TryGetPlayerObject(
                    inputAuthority,
                    out var playerObject)
                && playerObject == movement.Object;

            var lobbyState =
                movement.GetComponent<LobbyPlayerState>();

            var isInActiveSession =
                isConnected
                && lobbyState != null
                && lobbyState.Object != null
                && lobbyState.Object.IsValid
                && lobbyState.IsGameplayPlayer;

            var lifeState =
                movement.GetComponent<NetworkPlayerLifeState>();

            var health =
                movement.GetComponent<NetworkPlayerHealth>();

            var isDowned =
                (lifeState != null && lifeState.IsDowned)
                || (health != null && health.IsDowned);

            var isEliminated =
                lifeState != null
                && lifeState.IsEliminated;

            var hasOtherInvalidGameplayState =
                (lifeState != null && lifeState.IsCaught)
                || movement.IsHidden;

            return StalkerTargetEligibility.Evaluate(
                new StalkerTargetEligibilitySnapshot(
                    isInActiveSession,
                    isConnected,
                    isDowned,
                    isEliminated,
                    hasOtherInvalidGameplayState));
        }

        private static bool TryResolvePlayerId(
            global::PlayerHidingController occupant,
            out PlayerId playerId)
        {
            playerId = PlayerId.Invalid;

            if (occupant == null)
            {
                return false;
            }

            var identity = occupant.GetComponentInParent<PlayerRuntimeIdentity>();
            if (identity != null
                && identity.IsBound
                && identity.PlayerId.IsValid)
            {
                playerId = identity.PlayerId;
                return true;
            }

            var networkObject = occupant.GetComponentInParent<NetworkObject>();
            if (networkObject != null
                && networkObject.IsValid)
            {
                var actorId = networkObject.InputAuthority.PlayerId;
                if (actorId >= 0)
                {
                    playerId = new PlayerId(actorId + 1);
                    return true;
                }
            }

            var movement = occupant.GetComponentInParent<NetworkPlayerMovement>();
            if (movement != null
                && movement.Object != null
                && movement.Object.IsValid)
            {
                var actorId = movement.Object.InputAuthority.PlayerId;
                if (actorId >= 0)
                {
                    playerId = new PlayerId(actorId + 1);
                    return true;
                }
            }

            return false;
        }
    }
}
