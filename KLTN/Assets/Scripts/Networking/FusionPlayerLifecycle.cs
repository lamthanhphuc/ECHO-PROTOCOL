using System;
using EchoProtocol.Diagnostics;
using System.Collections.Generic;
using System.Collections;
using System.Security.Cryptography;
using EchoProtocol.AI.Common;
using EchoProtocol.Player;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace EchoProtocol.Networking
{
    public readonly struct FusionPlayerObjectCommit
    {
        public FusionPlayerObjectCommit(PlayerRef player, NetworkObject playerObject, PlayerId playerId)
        {
            Player = player;
            PlayerObject = playerObject;
            PlayerId = playerId;
        }

        public PlayerRef Player { get; }
        public NetworkObject PlayerObject { get; }
        public PlayerId PlayerId { get; }
    }

    [RequireComponent(typeof(NetworkRunner))]
    public sealed class FusionPlayerLifecycle : MonoBehaviour, INetworkRunnerCallbacks
    {
        [SerializeField] private NetworkObject playerPrefab;
        [SerializeField] private Vector3 spawnOrigin = Vector3.zero;
        [SerializeField] private float spawnSpacing = 2f;
        [SerializeField, Min(1f)] private float reconnectGraceSeconds = 60f;

        private readonly FusionPlayerIdentityRegistry _identityRegistry = new FusionPlayerIdentityRegistry();
        private readonly PlayerRuntimeEntityRegistry _entityRegistry = new PlayerRuntimeEntityRegistry();

        private NetworkRunner _runner;
        private bool _callbacksRegistered;
        private readonly Dictionary<PlayerRef, byte[]> _connectionTokens = new();
        private readonly Dictionary<PlayerRef, Coroutine> _graceTimers = new();
        private readonly Dictionary<PlayerRef, NetworkObject> _playerObjects = new();
        private readonly HashSet<PlayerRef> _expiring = new();

        public FusionPlayerIdentityRegistry IdentityRegistry => _identityRegistry;
        public float ReconnectGraceSeconds => reconnectGraceSeconds;

        public PlayerRuntimeEntityRegistry EntityRegistry => _entityRegistry;

        public event Action<FusionPlayerObjectCommit> PlayerObjectCommitted;

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
            if (NetworkBootstrap.IsHostMigrationResumeActive)
            {
                RuntimeLog.Log(
                    RuntimeLogCategory.PlayerLifecycle,
                    $"FPL|JOIN_MIGRATION_DEFER|player={player}");
                return;
            }
            RuntimeLog.Log(
                RuntimeLogCategory.PlayerLifecycle,
                $"FPL|JOIN_CALLBACK|player={player}|isServer={IsServer(runner)}|isRunning={IsRunning(runner)}");

            if (!CanMutateLifecycle(runner))
            {
                RuntimeLog.Log(
                RuntimeLogCategory.PlayerLifecycle,
                $"FPL|JOIN_REJECT|player={player}|reason=Authority|isServer={IsServer(runner)}|isRunning={IsRunning(runner)}");
                return;
            }

            var reconnectToken =
                runner.GetPlayerConnectionToken(player);

            if (TryReconnectRetainedPlayer(
                    runner,
                    player,
                    reconnectToken))
            {
                return;
            }

            if (!runner.TryGetPlayerObject(player, out var existingObject) || existingObject == null)
                _playerObjects.TryGetValue(player, out existingObject);
            if (existingObject == null && NetworkBootstrap.Instance != null
                && NetworkBootstrap.Instance.State == NetworkSessionState.InMatch)
            {
                runner.Disconnect(player);
                return;
            }
            if (existingObject != null)
            {
                if (IsCommitted(player, existingObject))
                {
                    if (existingObject.TryGetComponent<LobbyPlayerState>(out var retained)
                        && retained.Disconnected)
                    {
                        var token = runner.GetPlayerConnectionToken(player);
                        if (!_connectionTokens.TryGetValue(player, out var previous)
                            || !TokensMatch(previous, token))
                        {
                            runner.Disconnect(player);
                            return;
                        }
                        if (_graceTimers.Remove(player, out var timer)) StopCoroutine(timer);
                        runner.SetPlayerObject(player, existingObject);
                        existingObject.AssignInputAuthority(player);
                        retained.SetDisconnectedAuthoritative(false);
                        NotifyPlayerObjectCommitted(new FusionPlayerObjectCommit(
                            player, existingObject, retained.GetComponent<PlayerRuntimeIdentity>().PlayerId));
                        return;
                    }
                    RuntimeLog.Log(
                RuntimeLogCategory.PlayerLifecycle,
                $"FPL|JOIN_REJECT|player={player}|reason=AlreadyCommitted|isServer={IsServer(runner)}|isRunning={IsRunning(runner)}");
                    return;
                }

                RuntimeLog.Log(
                RuntimeLogCategory.PlayerLifecycle,
                $"FPL|JOIN_REJECT|player={player}|reason=InconsistentExistingPlayerObject|isServer={IsServer(runner)}|isRunning={IsRunning(runner)}");
                Debug.LogError($"[FusionPlayerLifecycle] Player {player} already has an inconsistent player object.");
                return;
            }

            if (playerPrefab == null)
            {
                RuntimeLog.Log(
                RuntimeLogCategory.PlayerLifecycle,
                $"FPL|JOIN_REJECT|player={player}|reason=MissingPlayerPrefab|isServer={IsServer(runner)}|isRunning={IsRunning(runner)}");
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
                    RuntimeLog.Log(
                RuntimeLogCategory.PlayerLifecycle,
                $"FPL|JOIN_REJECT|player={player}|reason=IdentityRegistryRejected|isServer={IsServer(runner)}|isRunning={IsRunning(runner)}");
                    Debug.LogError($"[FusionPlayerLifecycle] Failed to register logical identity for player {player}.");
                    return;
                }

                spawnedObject = runner.Spawn(
                    playerPrefab,
                    CreateSpawnPosition(playerId),
                    Quaternion.identity,
                    player,
                    null,
                    NetworkSpawnFlags.DontDestroyOnLoad);

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

                if (!spawnedObject.TryGetComponent<LobbyPlayerState>(
                        out var lobbyState)
                    || !lobbyState.TrySetRuntimePlayerIdAuthoritative(
                        playerId.Value))
                {
                    throw new InvalidOperationException(
                        $"Failed to persist logical PlayerId {playerId}.");
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
                RuntimeLog.Log(
                RuntimeLogCategory.PlayerLifecycle,
                $"FPL|JOIN_REJECT|player={player}|reason=TransactionFailed|isServer={IsServer(runner)}|isRunning={IsRunning(runner)}");
                Debug.LogError($"[FusionPlayerLifecycle] Failed to spawn/register player {player}: {ex.Message}");
                return;
            }

            var commit = new FusionPlayerObjectCommit(player, spawnedObject, playerId);

            var connectionToken =
                runner.GetPlayerConnectionToken(player);

            _connectionTokens[player] =
                connectionToken;

            _playerObjects[player] =
                spawnedObject;

            if (spawnedObject.TryGetComponent<LobbyPlayerState>(
                    out var committedLobbyState))
            {
                committedLobbyState
                    .SetReconnectTokenHashAuthoritative(
                        ComputeConnectionTokenHash(
                            connectionToken));
            }
            RuntimeLog.Log(
                RuntimeLogCategory.PlayerLifecycle,
                $"FPL|JOIN_COMMIT|player={player}|playerId={playerId.Value}");
            NotifyPlayerObjectCommitted(commit);

            NetworkBootstrap.Instance
                ?.RequestHostMigrationSnapshotCheckpoint(
                    "player-commit");
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (NetworkBootstrap.IsHostMigrationResumeActive)
            {
                RuntimeLog.Log(
                    RuntimeLogCategory.PlayerLifecycle,
                    $"FPL|LEFT_MIGRATION_DEFER|player={player}");
                return;
            }
            RuntimeLog.Log(
                RuntimeLogCategory.PlayerLifecycle,
                $"FPL|LEFT_CALLBACK|player={player}|isServer={IsServer(runner)}|isRunning={IsRunning(runner)}");

            if (!CanMutateLifecycle(runner))
            {
                RuntimeLog.Log(
                RuntimeLogCategory.PlayerLifecycle,
                $"FPL|LEFT_REJECT|player={player}|reason=Authority|isServer={IsServer(runner)}|isRunning={IsRunning(runner)}");
                return;
            }

            _identityRegistry.TryGetPlayerId(player, out var oldPlayerId);
            if (!runner.TryGetPlayerObject(player, out var playerObject) || playerObject == null)
                _playerObjects.TryGetValue(player, out playerObject);

            if (NetworkBootstrap.Instance != null
                && NetworkBootstrap.Instance.State == NetworkSessionState.InMatch
                && !_expiring.Remove(player)
                && playerObject != null)
            {
                if (playerObject.TryGetComponent<LobbyPlayerState>(out var state))
                {
                    if (state.CarriedCoreId.IsValid
                        && runner.TryFindObject(state.CarriedCoreId, out var core)
                        && core.TryGetComponent<NetworkPickupItem>(out var item))
                    {
                        item.TryDrop(player, playerObject.transform.position,
                            playerObject.transform.rotation, state);
                    }
                    state.SetDisconnectedAuthoritative(true);
                }
                playerObject.RemoveInputAuthority();
                if (_graceTimers.Remove(player, out var oldTimer)) StopCoroutine(oldTimer);
                _graceTimers[player] = StartCoroutine(ExpireDisconnectedPlayer(runner, player, playerObject));
                return;
            }

            _connectionTokens.Remove(player);
            _playerObjects.Remove(player);

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

            RuntimeLog.Log(
                RuntimeLogCategory.PlayerLifecycle,
                $"FPL|LEFT_COMMIT|player={player}|playerId={(oldPlayerId.IsValid ? oldPlayerId.Value.ToString() : "none")}|hadPlayerObject={playerObject != null}");
        }

        public bool RebuildAfterHostMigration(
            NetworkRunner runner,
            IReadOnlyDictionary<PlayerRef, NetworkObject> restoredPlayerObjects)
        {
            if (!CanMutateLifecycle(runner))
            {
                Debug.LogError(
                    "[FusionPlayerLifecycle] Host Migration rebuild requires the authoritative Runner.");

                return false;
            }

            if (restoredPlayerObjects == null
                || restoredPlayerObjects.Count == 0)
            {
                Debug.LogError(
                    "[FusionPlayerLifecycle] Host Migration rebuild received no PlayerObjects.");

                return false;
            }

            if (_identityRegistry.Count != 0
                || _entityRegistry.Count != 0
                || _playerObjects.Count != 0)
            {
                Debug.LogError(
                    "[FusionPlayerLifecycle] Host Migration rebuild expected clean local registries.");

                return false;
            }

            _connectionTokens.Clear();
            _expiring.Clear();

            foreach (var timer in
                     _graceTimers.Values)
            {
                if (timer != null)
                {
                    StopCoroutine(
                        timer);
                }
            }

            _graceTimers.Clear();

            var activePlayers =
                new HashSet<PlayerRef>(
                    runner.ActivePlayers);

            var rebuilt = 0;
            var retained = 0;

            foreach (var entry in
                     restoredPlayerObjects)
            {
                var player =
                    entry.Key;

                var playerObject =
                    entry.Value;

                if (playerObject == null
                    || !playerObject.IsValid)
                {
                    Debug.LogError(
                        $"[FusionPlayerLifecycle] Migrated PlayerObject missing for {player}.");

                    return false;
                }

                if (!playerObject.TryGetComponent<LobbyPlayerState>(
                        out var lobbyState)
                    || lobbyState.RuntimePlayerIdValue <= 0)
                {
                    Debug.LogError(
                        $"[FusionPlayerLifecycle] Migrated logical PlayerId missing for {player}.");

                    return false;
                }

                var playerId =
                    new PlayerId(
                        lobbyState.RuntimePlayerIdValue);

                var identity =
                    playerObject
                        .GetComponent<PlayerRuntimeIdentity>();

                if (identity == null
                    || !identity.TryBind(
                        playerId))
                {
                    Debug.LogError(
                        $"[FusionPlayerLifecycle] Could not restore PlayerRuntimeIdentity for {player} -> {playerId}.");

                    return false;
                }

                if (!_identityRegistry.TryRestore(
                        player,
                        playerId))
                {
                    Debug.LogError(
                        $"[FusionPlayerLifecycle] Could not restore identity registry for {player} -> {playerId}.");

                    return false;
                }

                if (!_entityRegistry.TryRegister(
                        identity))
                {
                    Debug.LogError(
                        $"[FusionPlayerLifecycle] Could not restore entity registry for {playerId}.");

                    return false;
                }

                _playerObjects[player] =
                    playerObject;

                var isActive =
                    activePlayers.Contains(
                        player);

                if (isActive)
                {
                    runner.SetPlayerObject(
                        player,
                        playerObject);

                    playerObject
                        .AssignInputAuthority(
                            player);

                    var token =
                        runner.GetPlayerConnectionToken(
                            player);

                    _connectionTokens[player] =
                        token;

                    lobbyState
                        .SetReconnectTokenHashAuthoritative(
                            ComputeConnectionTokenHash(
                                token));

                    lobbyState
                        .SetDisconnectedAuthoritative(
                            false);
                }
                else
                {
                    // The player existed in the Host Migration snapshot
                    // but was not connected when the new Host restored it.
                    //
                    // Keep the exact PlayerObject and all Networked state
                    // alive for the bounded reconnect grace period.
                    playerObject
                        .RemoveInputAuthority();

                    lobbyState
                        .SetDisconnectedAuthoritative(
                            true);

                    _graceTimers[player] =
                        StartCoroutine(
                            ExpireDisconnectedPlayer(
                                runner,
                                player,
                                playerObject));

                    retained++;
                }

                rebuilt++;

                RuntimeLog.Log(
                    RuntimeLogCategory.PlayerLifecycle,
                    $"FPL|MIGRATION_REBUILD|player={player}|playerId={playerId.Value}|object={playerObject.Id}|active={isActive}");
            }

            RuntimeLog.Log(
                RuntimeLogCategory.PlayerLifecycle,
                $"FPL|MIGRATION_REBUILD_COMPLETE|players={rebuilt}|retained={retained}");

            // These are snapshot-restored objects. Do not raise
            // PlayerObjectCommitted here; normal commit observers could
            // reset presentation/gameplay state as if the player were new.
            return rebuilt > 0;
        }
        private bool TryReconnectRetainedPlayer(
            NetworkRunner runner,
            PlayerRef player,
            byte[] token)
        {
            if (token == null
                || token.Length == 0)
            {
                return false;
            }

            var retainedPlayerRef =
                PlayerRef.None;

            NetworkObject retainedObject =
                null;

            LobbyPlayerState retainedState =
                null;

            foreach (var entry in
                     _playerObjects)
            {
                var candidate =
                    entry.Value;

                if (candidate == null
                    || !candidate.IsValid
                    || !candidate.TryGetComponent<LobbyPlayerState>(
                        out var state)
                    || !state.Disconnected)
                {
                    continue;
                }

                var rawTokenMatches =
                    _connectionTokens.TryGetValue(
                        entry.Key,
                        out var previousToken)
                    && TokensMatch(
                        previousToken,
                        token);

                if (!rawTokenMatches
                    && !ReconnectTokenMatches(
                        state,
                        token))
                {
                    continue;
                }

                retainedPlayerRef =
                    entry.Key;

                retainedObject =
                    candidate;

                retainedState =
                    state;

                break;
            }

            if (retainedObject == null
                || retainedState == null)
            {
                return false;
            }

            var identity =
                retainedObject
                    .GetComponent<PlayerRuntimeIdentity>();

            if (identity == null
                || !identity.PlayerId.IsValid)
            {
                Debug.LogError(
                    $"FPL|RECONNECT_REJECT|player={player}|reason=MissingRuntimeIdentity");

                runner.Disconnect(
                    player);

                return true;
            }

            var playerId =
                identity.PlayerId;

            // PlayerUniqueId normally preserves the same PlayerRef.
            // If Fusion gives this client another PlayerRef after
            // Host Migration, securely remap the logical player to
            // the retained PlayerObject instead of spawning a duplicate.
            if (retainedPlayerRef != player)
            {
                if (!_identityRegistry.Unregister(
                        retainedPlayerRef))
                {
                    Debug.LogError(
                        $"FPL|RECONNECT_REJECT|player={player}|reason=OldIdentityMissing");

                    runner.Disconnect(
                        player);

                    return true;
                }

                if (!_identityRegistry.TryRestore(
                        player,
                        playerId))
                {
                    // Restore original registry ownership if remap failed.
                    _identityRegistry.TryRestore(
                        retainedPlayerRef,
                        playerId);

                    Debug.LogError(
                        $"FPL|RECONNECT_REJECT|player={player}|reason=IdentityRemapFailed");

                    runner.Disconnect(
                        player);

                    return true;
                }

                _playerObjects.Remove(
                    retainedPlayerRef);

                _connectionTokens.Remove(
                    retainedPlayerRef);

                if (_graceTimers.Remove(
                        retainedPlayerRef,
                        out var oldTimer)
                    && oldTimer != null)
                {
                    StopCoroutine(
                        oldTimer);
                }

                _expiring.Remove(
                    retainedPlayerRef);

                _playerObjects[player] =
                    retainedObject;
            }
            else
            {
                if (_graceTimers.Remove(
                        player,
                        out var currentTimer)
                    && currentTimer != null)
                {
                    StopCoroutine(
                        currentTimer);
                }
            }

            _connectionTokens[player] =
                token;

            retainedState
                .SetReconnectTokenHashAuthoritative(
                    ComputeConnectionTokenHash(
                        token));

            runner.SetPlayerObject(
                player,
                retainedObject);

            retainedObject
                .AssignInputAuthority(
                    player);

            retainedState
                .SetDisconnectedAuthoritative(
                    false);

            NotifyPlayerObjectCommitted(
                new FusionPlayerObjectCommit(
                    player,
                    retainedObject,
                    playerId));

            RuntimeLog.Log(
                RuntimeLogCategory.PlayerLifecycle,
                $"FPL|RECONNECT_COMMIT|player={player}|previousPlayer={retainedPlayerRef}|playerId={playerId.Value}|object={retainedObject.Id}");

            NetworkBootstrap.Instance
                ?.RequestHostMigrationSnapshotCheckpoint(
                    "player-reconnect");

            return true;
        }

        public bool CanReconnect(
            byte[] token)
        {
            if (token == null
                || token.Length == 0)
            {
                return false;
            }

            foreach (var entry in
                     _playerObjects)
            {
                var playerObject =
                    entry.Value;

                if (playerObject == null
                    || !playerObject.IsValid
                    || !playerObject.TryGetComponent<LobbyPlayerState>(
                        out var state)
                    || !state.Disconnected)
                {
                    continue;
                }

                if (_connectionTokens.TryGetValue(
                        entry.Key,
                        out var previousToken)
                    && TokensMatch(
                        previousToken,
                        token))
                {
                    return true;
                }

                if (ReconnectTokenMatches(
                        state,
                        token))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ReconnectTokenMatches(
            LobbyPlayerState state,
            byte[] token)
        {
            if (state == null
                || state.ReconnectTokenHash.Length == 0)
            {
                return false;
            }

            return string.Equals(
                state.ReconnectTokenHash.ToString(),
                ComputeConnectionTokenHash(
                    token),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string ComputeConnectionTokenHash(
            byte[] token)
        {
            if (token == null
                || token.Length == 0)
            {
                return string.Empty;
            }

            using var sha256 =
                SHA256.Create();

            var hash =
                sha256.ComputeHash(
                    token);

            return BitConverter
                .ToString(hash)
                .Replace(
                    "-",
                    string.Empty);
        }
        private static bool TokensMatch(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            var difference = 0;
            for (var i = 0; i < left.Length; i++) difference |= left[i] ^ right[i];
            return difference == 0;
        }

        private IEnumerator ExpireDisconnectedPlayer(NetworkRunner runner, PlayerRef player, NetworkObject obj)
        {
            yield return new WaitForSecondsRealtime(reconnectGraceSeconds);
            _graceTimers.Remove(player);
            if (runner == null || !runner.IsRunning || obj == null || !obj.IsValid) yield break;
            if (!obj.TryGetComponent<LobbyPlayerState>(out var state) || !state.Disconnected) yield break;
            _expiring.Add(player);
            OnPlayerLeft(runner, player);
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
                && runner.IsServer;
        }

        private static bool IsServer(NetworkRunner runner)
        {
            return runner != null && runner.IsServer;
        }

        private static bool IsRunning(NetworkRunner runner)
        {
            return runner != null && runner.IsRunning;
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

        private void NotifyPlayerObjectCommitted(FusionPlayerObjectCommit commit)
        {
            var handlers = PlayerObjectCommitted;
            if (handlers == null)
            {
                return;
            }

            var invocationList = handlers.GetInvocationList();
            for (var i = 0; i < invocationList.Length; i++)
            {
                var handler = (Action<FusionPlayerObjectCommit>)invocationList[i];
                try
                {
                    handler(commit);
                }
                catch (Exception ex)
                {
                    var observer = handler.Method != null
                        ? $"{handler.Method.DeclaringType?.FullName}.{handler.Method.Name}"
                        : "unknown";
                    Debug.LogError(
                        $"FPL|COMMIT_OBSERVER_ERROR|player={commit.Player}|playerId={commit.PlayerId}|observer={observer}|error={ex.GetType().Name}:{ex.Message}");
                }
            }
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

#pragma warning disable CS0618
        void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
#pragma warning restore CS0618

        void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }

        void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

        void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

        void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

        void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

        void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner) { }

        void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) { }
    }
}
