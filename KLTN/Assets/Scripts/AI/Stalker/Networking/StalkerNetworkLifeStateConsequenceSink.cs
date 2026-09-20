using EchoProtocol.AI.Common;
using EchoProtocol.AI.Stalker.Special;
using EchoProtocol.Networking;
using Fusion;
using System;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Networking
{
    /// <summary>Commits Stalker hits through the player Life-State owner on the Fusion Host.</summary>
    public sealed class StalkerNetworkLifeStateConsequenceSink : IPlayerAttackConsequenceSink
    {
        private readonly NetworkRunner _runner;
        private readonly FusionPlayerIdentityRegistry _identityRegistry;
        private readonly Action<StalkerDownedPlayerFact> _onPlayerDowned;

        public StalkerNetworkLifeStateConsequenceSink(
            NetworkRunner runner,
            FusionPlayerIdentityRegistry identityRegistry,
            Action<StalkerDownedPlayerFact> onPlayerDowned = null)
        {
            _runner = runner;
            _identityRegistry = identityRegistry;
            _onPlayerDowned = onPlayerDowned;
        }

        public bool TryApplyStalkerHit(
            StalkerAttackEpisodeId episodeId,
            PlayerId playerId,
            Vector3 authoritativeHitPosition,
            AiSimulationTime resolvedAt)
        {
            if (_runner == null || !_runner.IsRunning || !_runner.IsServer
                || _identityRegistry == null
                || !_identityRegistry.TryGetPlayerRef(playerId, out var player)
                || !_runner.TryGetPlayerObject(player, out var playerObject)
                || !playerObject.TryGetComponent<NetworkPlayerLifeState>(out var lifeState))
            {
                return false;
            }

            if (playerObject.TryGetComponent<NetworkPlayerMovement>(out var movement)
                && movement.IsHidden)
            {
                return false;
            }

            var hidingController =
                playerObject.GetComponentInChildren<global::PlayerHidingController>(true);

            if (hidingController != null
                && hidingController.IsHidden)
            {
                return false;
            }

            var previousStatus =
                lifeState.Status;

            bool consequenceApplied;

            switch (previousStatus)
            {
                case NetworkPlayerLifeStatus.Alive:
                    consequenceApplied =
                        lifeState.TryApplyMonsterDown(
                            "STALKER",
                            authoritativeHitPosition);

                    break;

                case NetworkPlayerLifeStatus.Downed:
                    consequenceApplied =
                        lifeState.TryEliminateForReviveLimit();

                    break;

                default:
                    return false;
            }

            var transitionedAliveToDowned =
                consequenceApplied
                && previousStatus
                    == NetworkPlayerLifeStatus.Alive
                && lifeState.Status
                    == NetworkPlayerLifeStatus.Downed;

            if (transitionedAliveToDowned)
            {
                _onPlayerDowned?.Invoke(
                    new StalkerDownedPlayerFact(
                        episodeId,
                        playerId,
                        authoritativeHitPosition,
                        resolvedAt));
            }

            return consequenceApplied;
        }
    }
}
