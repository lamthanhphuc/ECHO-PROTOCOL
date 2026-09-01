using EchoProtocol.AI.Common;
using EchoProtocol.Networking;
using Fusion;
using UnityEngine;
using EchoProtocol.Networking.Authority;

namespace EchoProtocol.AI.Stalker.Networking
{
    /// <summary>Commits Stalker hits through the player Life-State owner on the Fusion Host.</summary>
    public sealed class StalkerNetworkLifeStateConsequenceSink : IPlayerAttackConsequenceSink
    {
        private readonly NetworkRunner _runner;
        private readonly FusionPlayerIdentityRegistry _identityRegistry;
        private readonly string _monsterId;

        public StalkerNetworkLifeStateConsequenceSink(
            NetworkRunner runner,
            FusionPlayerIdentityRegistry identityRegistry,
            string monsterId)
        {
            _runner = runner;
            _identityRegistry = identityRegistry;
            _monsterId = monsterId;
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

            var consequenceApplied = lifeState.Status == NetworkPlayerLifeStatus.Downed
                ? lifeState.TryEliminateForReviveLimit()
                : lifeState.TryApplyMonsterDown("STALKER", authoritativeHitPosition);
            MatchAuthorityRuntime.Instance?.RecordStalkerAttackResolved(
                _monsterId,
                episodeId.Value.ToString(),
                "HIT");
            return consequenceApplied;
        }
    }
}
