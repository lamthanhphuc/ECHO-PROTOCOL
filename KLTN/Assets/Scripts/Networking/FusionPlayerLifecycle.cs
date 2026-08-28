using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common;
using EchoProtocol.Player;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace EchoProtocol.Networking
{
    [RequireComponent(typeof(NetworkRunner))]
    public sealed class FusionPlayerLifecycle : MonoBehaviour, INetworkRunnerCallbacks
    {
        [SerializeField] private NetworkObject playerPrefab;
        [SerializeField] private Vector3 spawnOrigin = Vector3.zero;
        [SerializeField] private float spawnSpacing = 2f;

        private readonly FusionPlayerIdentityRegistry _identityRegistry = new FusionPlayerIdentityRegistry();
        private readonly PlayerRuntimeEntityRegistry _entityRegistry = new PlayerRuntimeEntityRegistry();

        private NetworkRunner _runner;
        private bool _callbacksRegistered;

        public FusionPlayerIdentityRegistry IdentityRegistry => _identityRegistry;

        public PlayerRuntimeEntityRegistry EntityRegistry => _entityRegistry;

        private void Awake()
        {
            _runner = GetComponent<NetworkRunner>();
        }

        private void OnEnable()
        {
            RegisterCallbacks();
        }

        private void OnDisable()
        {
            UnregisterCallbacks();
        }

        private void OnDestroy()
        {
            UnregisterCallbacks();
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (!CanMutateLifecycle(runner))
            {
                return;
            }

            if (runner.TryGetPlayerObject(player, out var existingObject) && existingObject != null)
            {
                if (IsCommitted(player, existingObject))
                {
                    return;
                }

                Debug.LogError($"[FusionPlayerLifecycle] Player {player} already has an inconsistent player object.");
                return;
            }

            if (playerPrefab == null)
            {
                Debug.LogError("[FusionPlayerLifecycle] Player prefab is not assigned.");
                return;
            }

            PlayerId playerId = PlayerId.Invalid;
            NetworkObject spawnedObject = null;
            PlayerRuntimeIdentity identity = null;
            var entityRegistered = false;
            var playerObjectCommitted = false;

            try
            {
                if (!_identityRegistry.TryRegister(player, out playerId))
                {
                    Debug.LogError($"[FusionPlayerLifecycle] Failed to register logical identity for player {player}.");
                    return;
                }

                spawnedObject = runner.Spawn(
                    playerPrefab,
                    CreateSpawnPosition(playerId),
                    Quaternion.identity,
                    player);

                if (spawnedObject == null)
                {
                    throw new InvalidOperationException("Runner.Spawn returned null.");
                }

                identity = spawnedObject.GetComponent<PlayerRuntimeIdentity>();
                if (identity == null)
                {
                    throw new InvalidOperationException("Spawned player prefab is missing PlayerRuntimeIdentity.");
                }

                if (!identity.TryBind(playerId))
                {
                    throw new InvalidOperationException($"PlayerRuntimeIdentity rejected binding to {playerId}.");
                }

                if (!_entityRegistry.TryRegister(identity))
                {
                    throw new InvalidOperationException($"PlayerRuntimeEntityRegistry rejected {playerId}.");
                }

                entityRegistered = true;
                runner.SetPlayerObject(player, spawnedObject);
                playerObjectCommitted = true;

                if (!runner.TryGetPlayerObject(player, out var mappedObject) || mappedObject != spawnedObject)
                {
                    throw new InvalidOperationException("Runner.SetPlayerObject did not commit the spawned object.");
                }
            }
            catch (Exception ex)
            {
                RollbackJoin(runner, player, playerId, identity, spawnedObject, entityRegistered, playerObjectCommitted);
                Debug.LogError($"[FusionPlayerLifecycle] Failed to spawn/register player {player}: {ex.Message}");
            }
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!CanMutateLifecycle(runner))
            {
                return;
            }

            _identityRegistry.TryGetPlayerId(player, out var oldPlayerId);
            runner.TryGetPlayerObject(player, out var playerObject);

            PlayerRuntimeIdentity identity = null;
            if (playerObject != null)
            {
                identity = playerObject.GetComponent<PlayerRuntimeIdentity>();
            }

            if (identity != null)
            {
                _entityRegistry.Unregister(identity);
                identity.ClearBinding();
            }
            else if (oldPlayerId.IsValid)
            {
                _entityRegistry.Unregister(oldPlayerId);
            }

            _identityRegistry.Unregister(player);

            if (playerObject != null)
            {
                try
                {
                    runner.Despawn(playerObject);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[FusionPlayerLifecycle] Failed to despawn player object for {player}: {ex.Message}");
                }
            }
        }

        private void RegisterCallbacks()
        {
            if (_callbacksRegistered)
            {
                return;
            }

            if (_runner == null)
            {
                _runner = GetComponent<NetworkRunner>();
            }

            if (_runner == null)
            {
                return;
            }

            _runner.AddCallbacks(this);
            _callbacksRegistered = true;
        }

        private void UnregisterCallbacks()
        {
            if (!_callbacksRegistered || _runner == null)
            {
                return;
            }

            _runner.RemoveCallbacks(this);
            _callbacksRegistered = false;
        }

        private bool CanMutateLifecycle(NetworkRunner runner)
        {
            return runner != null
                && runner == _runner
                && runner.IsRunning
                && runner.IsServer;
        }

        private bool IsCommitted(PlayerRef player, NetworkObject existingObject)
        {
            if (!_identityRegistry.TryGetPlayerId(player, out var playerId))
            {
                return false;
            }

            var identity = existingObject.GetComponent<PlayerRuntimeIdentity>();
            return identity != null
                && identity.IsBound
                && identity.PlayerId == playerId
                && _entityRegistry.TryGetEntity(playerId, out var registeredIdentity)
                && registeredIdentity == identity;
        }

        private Vector3 CreateSpawnPosition(PlayerId playerId)
        {
            return spawnOrigin + new Vector3((playerId.Value - 1) * spawnSpacing, 0f, 0f);
        }

        private void RollbackJoin(
            NetworkRunner runner,
            PlayerRef player,
            PlayerId playerId,
            PlayerRuntimeIdentity identity,
            NetworkObject spawnedObject,
            bool entityRegistered,
            bool playerObjectCommitted)
        {
            if (entityRegistered && identity != null)
            {
                _entityRegistry.Unregister(identity);
            }
            else if (playerId.IsValid)
            {
                _entityRegistry.Unregister(playerId);
            }

            if (identity != null)
            {
                identity.ClearBinding();
            }

            if (playerObjectCommitted)
            {
                TryClearPlayerObject(runner, player);
            }

            if (spawnedObject != null)
            {
                try
                {
                    runner.Despawn(spawnedObject);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[FusionPlayerLifecycle] Rollback despawn failed for {player}: {ex.Message}");
                }
            }

            _identityRegistry.Unregister(player);
        }

        private static void TryClearPlayerObject(NetworkRunner runner, PlayerRef player)
        {
            try
            {
                runner.SetPlayerObject(player, null);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FusionPlayerLifecycle] Rollback player-object clear failed for {player}: {ex.Message}");
            }
        }

        void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input) { }

        void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

        void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }

        void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }

        void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }

        void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

        void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }

        void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

        void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }

        void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

        void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

        void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

        void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

        void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner) { }

        void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) { }
    }
}
