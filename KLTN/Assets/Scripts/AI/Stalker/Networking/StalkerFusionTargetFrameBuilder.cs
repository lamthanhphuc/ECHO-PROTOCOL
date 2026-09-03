using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common;
using EchoProtocol.Networking;
using EchoProtocol.Player;

namespace EchoProtocol.AI.Stalker.Networking
{
    public sealed class StalkerFusionTargetFrameBuilder
    {
        private readonly List<PlayerId> _activePlayerIds = new List<PlayerId>();

        public bool TryBuild(
            FusionPlayerLifecycle lifecycle,
            PlayerId detectionTargetId,
            PlayerId currentTargetId,
            List<StalkerPerceptionTargetSnapshot> perceptionSnapshots,
            List<StalkerTargetStatus> targetStatuses)
        {
            if (perceptionSnapshots == null)
            {
                throw new ArgumentNullException(nameof(perceptionSnapshots));
            }

            if (targetStatuses == null)
            {
                throw new ArgumentNullException(nameof(targetStatuses));
            }

            perceptionSnapshots.Clear();
            targetStatuses.Clear();
            _activePlayerIds.Clear();

            if (lifecycle == null)
            {
                return false;
            }

            var identityRegistry = lifecycle.IdentityRegistry;
            var entityRegistry = lifecycle.EntityRegistry;
            if (identityRegistry == null || entityRegistry == null)
            {
                return false;
            }

            identityRegistry.CollectActivePlayerIds(_activePlayerIds);

            for (var i = 0; i < _activePlayerIds.Count; i++)
            {
                var playerId = _activePlayerIds[i];
                if (!playerId.IsValid
                    || !entityRegistry.TryGetEntity(playerId, out var identity)
                    || !IsValidIdentity(playerId, identity))
                {
                    ClearOutputs(perceptionSnapshots, targetStatuses);
                    return false;
                }

                var isDowned = identity.TryGetComponent<NetworkPlayerHealth>(out var health) && health.IsDowned;
                var eligibilitySnapshot = StalkerFusionTargetEligibilityAdapter.CreateActive(
                    playerId,
                    isDowned,
                    false);
                var eligibility = StalkerTargetEligibility.Evaluate(eligibilitySnapshot);
                InsertStatusSortedUnique(targetStatuses, new StalkerTargetStatus(playerId, eligibility));

                perceptionSnapshots.Add(new StalkerPerceptionTargetSnapshot(
                    playerId,
                    identity.VisionTargetPoint,
                    identity.EntityRoot,
                    eligibilitySnapshot));
            }

            AddDisconnectedLockedTarget(detectionTargetId, targetStatuses);
            AddDisconnectedLockedTarget(currentTargetId, targetStatuses);

            return true;
        }

        private void AddDisconnectedLockedTarget(
            PlayerId playerId,
            List<StalkerTargetStatus> targetStatuses)
        {
            if (!playerId.IsValid || _activePlayerIds.Contains(playerId))
            {
                return;
            }

            var snapshot = StalkerFusionTargetEligibilityAdapter.CreateDisconnected(playerId);
            InsertStatusSortedUnique(targetStatuses, new StalkerTargetStatus(
                playerId,
                StalkerTargetEligibility.Evaluate(snapshot)));
        }

        private static bool IsValidIdentity(PlayerId playerId, PlayerRuntimeIdentity identity)
        {
            if (identity == null || !identity.IsBound || identity.PlayerId != playerId)
            {
                return false;
            }

            var root = identity.EntityRoot;
            var targetPoint = identity.VisionTargetPoint;
            return root != null
                && targetPoint != null
                && (targetPoint == root || targetPoint.IsChildOf(root));
        }

        private static void InsertStatusSortedUnique(
            List<StalkerTargetStatus> targetStatuses,
            StalkerTargetStatus status)
        {
            for (var i = 0; i < targetStatuses.Count; i++)
            {
                var comparison = status.PlayerId.CompareTo(targetStatuses[i].PlayerId);
                if (comparison == 0)
                {
                    return;
                }

                if (comparison < 0)
                {
                    targetStatuses.Insert(i, status);
                    return;
                }
            }

            targetStatuses.Add(status);
        }

        private static void ClearOutputs(
            List<StalkerPerceptionTargetSnapshot> perceptionSnapshots,
            List<StalkerTargetStatus> targetStatuses)
        {
            perceptionSnapshots.Clear();
            targetStatuses.Clear();
        }
    }
}
