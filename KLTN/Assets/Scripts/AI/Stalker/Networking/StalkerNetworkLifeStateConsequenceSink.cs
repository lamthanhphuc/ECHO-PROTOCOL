using EchoProtocol.AI.Common;
using EchoProtocol.Networking;
using Fusion;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Networking
{
    /// <summary>Commits Stalker hits through the player Life-State owner on the Fusion Host.</summary>
    public sealed class StalkerNetworkLifeStateConsequenceSink : IPlayerAttackConsequenceSink
    {
        private readonly NetworkRunner _runner;
        private readonly FusionPlayerIdentityRegistry _identityRegistry;

        public StalkerNetworkLifeStateConsequenceSink(
            NetworkRunner runner,
            FusionPlayerIdentityRegistry identityRegistry)
        {
            _runner = runner;
            _identityRegistry = identityRegistry;
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

            return lifeState.Status == NetworkPlayerLifeStatus.Downed
                ? lifeState.TryEliminateForReviveLimit()
                : lifeState.TryApplyMonsterDown("STALKER", authoritativeHitPosition);
        }
    }
}
