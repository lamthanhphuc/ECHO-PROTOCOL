using EchoProtocol.AI.Common;
using UnityEngine;

namespace EchoProtocol.AI.Stalker
{
    public sealed class StalkerDiagnosticAttackConsequenceSink : IPlayerAttackConsequenceSink
    {
        public const string ProductionBindingStatus = "PLAYER_LIFE_STATE_BINDING_REQUIRED";

        public int CallCount { get; private set; }

        public StalkerAttackEpisodeId LastEpisodeId { get; private set; }

        public PlayerId LastPlayerId { get; private set; }

        public Vector3 LastHitPosition { get; private set; }

        public AiSimulationTime LastResolvedAt { get; private set; }

        public bool TryApplyStalkerHit(
            StalkerAttackEpisodeId episodeId,
            PlayerId playerId,
            Vector3 authoritativeHitPosition,
            AiSimulationTime resolvedAt)
        {
            CallCount++;
            LastEpisodeId = episodeId;
            LastPlayerId = playerId;
            LastHitPosition = authoritativeHitPosition;
            LastResolvedAt = resolvedAt;
            return true;
        }
    }
}
