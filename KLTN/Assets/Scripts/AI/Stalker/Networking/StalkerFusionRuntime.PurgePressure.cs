using EchoProtocol.AI.Common;
using EchoProtocol.Networking;
using EchoProtocol.Player;
using Fusion;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Networking
{
    public sealed partial class StalkerFusionRuntime
    {
        private PlayerRef _purgeInterceptPlayer = PlayerRef.None;

        private bool TryGetPurgeTarget(PlayerRef player, out Vector3 position)
        {
            position = default;
            if (!Runner.TryGetPlayerObject(player, out var obj) || obj == null || !obj.IsValid
                || !obj.TryGetComponent<LobbyPlayerState>(out var lobby) || !lobby.IsGameplayPlayer
                || !obj.TryGetComponent<NetworkPlayerLifeState>(out var life) || life.Status != NetworkPlayerLifeStatus.Alive
                || obj.TryGetComponent<NetworkPlayerMovement>(out var movement) && movement.IsHidden
                || obj.TryGetComponent<PlayerHidingController>(out var hiding) && hiding.IsHidden) return false;
            position = obj.transform.position;
            return true;
        }

        private static readonly Unity.Profiling.ProfilerMarker InterceptMarker =
            new Unity.Profiling.ProfilerMarker("Purge.InterceptInvestigation");

        private double _purgeNextRingPointAt;
        private Vector3 _purgeRingCenter, _purgeRingGoal;
        private bool _purgeHasRingCenter, _purgeHasRingGoal;

        private void UpdatePurgeIntercept(NetworkMatchState match, double now)
        {
            if (Object == null || !Object.HasStateAuthority || match.IsEnded) return;
            if (match.PurgeMood == PurgeBossMood.Warning || match.PurgeMood == PurgeBossMood.Staggered) return;
            if (now < _purgeNextRingPointAt) return;
            _purgeNextRingPointAt = now + 2d;

            if (!TryGetPurgeTarget(_purgeInterceptPlayer, out var position))
            {
                _purgeInterceptPlayer = PlayerRef.None;
                float nearest = float.PositiveInfinity;
                foreach (var player in Runner.ActivePlayers)
                {
                    if (!TryGetPurgeTarget(player, out var candidate)) continue;
                    float distance = (candidate - transform.position).sqrMagnitude;
                    if (distance >= nearest) continue;
                    nearest = distance;
                    _purgeInterceptPlayer = player;
                    position = candidate;
                }
            }
            if (_purgeInterceptPlayer.IsRealPlayer)
            {
                _purgeRingCenter = position;
                _purgeHasRingCenter = true;
            }
            if (!_purgeHasRingCenter || !controller.CanAcceptPurgeRingPatrol) return;
            float goalDistance = Vector3.ProjectOnPlane(_purgeRingGoal - _purgeRingCenter, Vector3.up).magnitude;
            if (_purgeHasRingGoal && goalDistance >= 20f && goalDistance <= 40f
                && Vector3.Distance(transform.position, _purgeRingGoal) > 3f
                && controller.NavigationPathStatus == NavigationPathStatus.Complete) return;

            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
            Vector2 direction = Random.insideUnitCircle.normalized;
            Vector3 candidatePoint = _purgeRingCenter
                + new Vector3(direction.x, 0f, direction.y) * Random.Range(20f, 40f);
            if (!UnityEngine.AI.NavMesh.SamplePosition(candidatePoint, out var hit, 1f, agent.areaMask)) return;
            float sampledDistance = Vector3.ProjectOnPlane(hit.position - _purgeRingCenter, Vector3.up).magnitude;
            if (sampledDistance < 20f || sampledDistance > 40f) return;
            if (!controller.TryBeginPurgeRingPatrol(hit.position)) return;
            _purgeRingGoal = hit.position;
            _purgeHasRingGoal = true;
        }
    }
}
