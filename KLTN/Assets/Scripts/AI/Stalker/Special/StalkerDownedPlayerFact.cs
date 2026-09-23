using EchoProtocol.AI.Common;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Special
{
    public readonly struct StalkerDownedPlayerFact
    {
        public StalkerDownedPlayerFact(
            StalkerAttackEpisodeId attackEpisodeId,
            PlayerId playerId,
            Vector3 authoritativeHitPosition,
            AiSimulationTime resolvedAt)
        {
            AttackEpisodeId = attackEpisodeId;
            PlayerId = playerId;
            AuthoritativeHitPosition = authoritativeHitPosition;
            ResolvedAt = resolvedAt;
        }

        public StalkerAttackEpisodeId AttackEpisodeId { get; }
        public PlayerId PlayerId { get; }
        public Vector3 AuthoritativeHitPosition { get; }
        public AiSimulationTime ResolvedAt { get; }

        public bool IsValid =>
            AttackEpisodeId.IsValid
            && PlayerId.IsValid
            && ResolvedAt.IsValid;
    }
}
