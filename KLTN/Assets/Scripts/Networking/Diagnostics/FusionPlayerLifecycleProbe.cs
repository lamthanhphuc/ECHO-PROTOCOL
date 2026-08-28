using EchoProtocol.AI.Common;
using EchoProtocol.Player;
using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking.Diagnostics
{
    public sealed class FusionPlayerLifecycleProbe : MonoBehaviour
    {
        [SerializeField] private FusionC2CHarnessController controller;

        public void CaptureSnapshot()
        {
            if (controller == null)
            {
                controller = GetComponent<FusionC2CHarnessController>();
            }

            var runner = controller != null ? controller.Runner : null;
            if (runner == null)
            {
                Debug.LogError("C2C|SNAPSHOT_FAIL|reason=MissingRunner");
                return;
            }

            var role = runner.IsServer ? "Host" : "Client";
            var lifecycle = runner.GetComponent<FusionPlayerLifecycle>();
            if (lifecycle == null)
            {
                Debug.LogError($"C2C|SNAPSHOT_FAIL|role={role}|reason=MissingFusionPlayerLifecycle");
                return;
            }

            var activeCount = 0;
            Debug.Log($"C2C|SNAPSHOT_BEGIN|role={role}|local={runner.LocalPlayer}");

            foreach (var player in runner.ActivePlayers)
            {
                activeCount++;
                LogPlayerSnapshot(runner, lifecycle, player);
            }

            Debug.Log($"C2C|SNAPSHOT_END|role={role}|active={activeCount}|identityRegistry={lifecycle.IdentityRegistry.Count}|entityRegistry={lifecycle.EntityRegistry.Count}");
        }

        private static void LogPlayerSnapshot(
            NetworkRunner runner,
            FusionPlayerLifecycle lifecycle,
            PlayerRef player)
        {
            var hasLogicalId = lifecycle.IdentityRegistry.TryGetPlayerId(player, out var logicalPlayerId);
            var hasPlayerObject = runner.TryGetPlayerObject(player, out var playerObject) && playerObject != null;
            PlayerRuntimeIdentity identity = null;

            if (hasPlayerObject)
            {
                identity = playerObject.GetComponent<PlayerRuntimeIdentity>();
            }

            var identityBound = identity != null && identity.IsBound;
            var identityPlayerId = identityBound ? identity.PlayerId.ToString() : "unbound";
            var entityExactMatch = false;
            var transformResolves = false;
            PlayerId resolvedTransformId = PlayerId.Invalid;

            if (hasLogicalId)
            {
                entityExactMatch = lifecycle.EntityRegistry.TryGetEntity(logicalPlayerId, out var registeredIdentity)
                    && registeredIdentity == identity;
            }

            if (identity != null)
            {
                transformResolves = lifecycle.EntityRegistry.TryResolvePlayerId(identity.EntityRoot, out resolvedTransformId);
            }

            Debug.Log(
                $"C2C|PLAYER|ref={player}|id={(hasLogicalId ? logicalPlayerId.ToString() : "none")}|object={(hasPlayerObject ? playerObject.name : "none")}|objectId={(hasPlayerObject ? playerObject.Id.ToString() : "none")}|identityBound={identityBound}|identityId={identityPlayerId}|entityMatch={entityExactMatch}|transformMatch={transformResolves}|transformId={(transformResolves ? resolvedTransformId.ToString() : "none")}|stateAuth={(hasPlayerObject ? playerObject.HasStateAuthority.ToString() : "none")}|inputAuth={(hasPlayerObject ? playerObject.HasInputAuthority.ToString() : "none")}|inputAuthority={(hasPlayerObject ? playerObject.InputAuthority.ToString() : "none")}|stateAuthority={(hasPlayerObject ? playerObject.StateAuthority.ToString() : "none")}");
        }
    }
}
