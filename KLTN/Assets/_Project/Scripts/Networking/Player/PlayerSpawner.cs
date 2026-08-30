using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Networking
{
    /// <summary>Host-authoritative player spawn registry for lobby and gameplay scenes.</summary>
    public sealed class PlayerSpawner : MonoBehaviour
    {
        private const int SupportedPlayerCount = 4;
        private const float FallbackSpacing = 2.5f;

        [SerializeField] private NetworkBootstrap _bootstrap;
        [SerializeField] private NetworkObject _playerPrefab;
        [Header("M2-024 World State Demo")]
        [SerializeField] private NetworkObject _doorPrefab;
        [SerializeField] private NetworkObject _pickupItemPrefab;

        private readonly Dictionary<PlayerRef, int> _spawnSlots = new Dictionary<PlayerRef, int>();
        private NetworkObject _doorInstance;
        private NetworkObject _pickupItemInstance;

        private void Awake()
        {
            if (_bootstrap == null) _bootstrap = FindAnyObjectByType<NetworkBootstrap>();
        }

        private void OnEnable()
        {
            if (_bootstrap == null) _bootstrap = FindAnyObjectByType<NetworkBootstrap>();
            if (_bootstrap == null) return;

            _bootstrap.PlayerJoined += HandlePlayerJoined;
            _bootstrap.PlayerLeft += HandlePlayerLeft;
            _bootstrap.NetworkSceneLoadDone += HandleNetworkSceneLoadDone;
            _bootstrap.SessionStateChanged += HandleSessionStateChanged;
        }

        private void OnDisable()
        {
            if (_bootstrap == null) return;
            _bootstrap.PlayerJoined -= HandlePlayerJoined;
            _bootstrap.PlayerLeft -= HandlePlayerLeft;
            _bootstrap.NetworkSceneLoadDone -= HandleNetworkSceneLoadDone;
            _bootstrap.SessionStateChanged -= HandleSessionStateChanged;
        }

        private void HandlePlayerJoined(PlayerRef player)
        {
            var runner = _bootstrap?.Runner;
            if (runner == null || !runner.IsServer) return;

            var gameplay = SceneManager.GetActiveScene().name == LobbyManager.GameSceneName;
            EnsurePlayerObject(runner, player, gameplay, replaceLobbyObject: false);
        }

        private void HandlePlayerLeft(PlayerRef player)
        {
            var runner = _bootstrap?.Runner;
            if (runner != null && runner.IsServer && runner.TryGetPlayerObject(player, out var playerObject))
            {
                runner.SetPlayerObject(player, null);
                runner.Despawn(playerObject);
                Debug.Log($"[PlayerSpawner] Despawned player object for {player}.");
            }

            _spawnSlots.Remove(player);
        }

        private void HandleNetworkSceneLoadDone(NetworkRunner runner)
        {
            if (!runner.IsServer || SceneManager.GetActiveScene().name != LobbyManager.GameSceneName) return;

            Debug.Log("[PlayerSpawner] Gameplay scene ready. Ensuring one gameplay object per active player.");
            foreach (var player in runner.ActivePlayers)
            {
                EnsurePlayerObject(runner, player, gameplay: true, replaceLobbyObject: true);
            }

            EnsureWorldStateExamples(runner);
        }

        private void EnsureWorldStateExamples(NetworkRunner runner)
        {
            if (_doorInstance == null && _doorPrefab != null)
            {
                _doorInstance = runner.Spawn(_doorPrefab, new Vector3(0f, 1f, 2.5f), Quaternion.identity);
                Debug.Log($"[PlayerSpawner] Spawned authoritative door {_doorInstance.Id}.");
            }
            if (_pickupItemInstance == null && _pickupItemPrefab != null)
            {
                _pickupItemInstance = runner.Spawn(_pickupItemPrefab, new Vector3(2f, 0.5f, 2.5f), Quaternion.identity);
                Debug.Log($"[PlayerSpawner] Spawned authoritative pickup {_pickupItemInstance.Id}.");
            }
        }

        private void EnsurePlayerObject(
            NetworkRunner runner,
            PlayerRef player,
            bool gameplay,
            bool replaceLobbyObject)
        {
            if (!player.IsValid)
            {
                Debug.LogError("[PlayerSpawner] Cannot spawn for an invalid PlayerRef.");
                return;
            }
            if (_playerPrefab == null)
            {
                Debug.LogError("[PlayerSpawner] Player prefab is not assigned.");
                return;
            }

            var teamId = 0;
            var toolId = 0;
            if (runner.TryGetPlayerObject(player, out var existingObject))
            {
                var existingState = existingObject.GetComponent<LobbyPlayerState>();
                if (!replaceLobbyObject || (existingState != null && existingState.IsGameplayPlayer))
                {
                    Debug.Log($"[PlayerSpawner] Duplicate spawn prevented for {player}; object={existingObject.Id}.");
                    return;
                }

                if (existingState != null)
                {
                    teamId = existingState.TeamId;
                    toolId = existingState.ToolId;
                }

                runner.SetPlayerObject(player, null);
                runner.Despawn(existingObject);
            }

            var slot = GetOrAssignSlot(player);
            var pose = gameplay ? GetGameplaySpawnPose(slot) : GetFallbackPose(slot);
            var playerObject = runner.Spawn(
                _playerPrefab,
                pose.Position,
                pose.Rotation,
                player,
                (_, spawnedObject) =>
                {
                    var state = spawnedObject.GetComponent<LobbyPlayerState>();
                    state?.InitializeAuthoritativeSelection(teamId, toolId, gameplay);
                },
                NetworkSpawnFlags.DontDestroyOnLoad);

            if (playerObject == null)
            {
                Debug.LogError($"[PlayerSpawner] Runner.Spawn failed for {player} at slot {slot}.");
                return;
            }

            runner.SetPlayerObject(player, playerObject);
            Debug.Log(
                $"[PlayerSpawner] Spawned {player} object={playerObject.Id}, slot={slot}, " +
                $"inputAuthority={playerObject.InputAuthority}, stateAuthority=Host, gameplay={gameplay}.");
        }

        private int GetOrAssignSlot(PlayerRef player)
        {
            if (_spawnSlots.TryGetValue(player, out var existingSlot)) return existingSlot;

            for (var slot = 0; slot < SupportedPlayerCount; slot++)
            {
                if (!_spawnSlots.ContainsValue(slot))
                {
                    _spawnSlots[player] = slot;
                    return slot;
                }
            }

            var fallbackSlot = _spawnSlots.Count;
            _spawnSlots[player] = fallbackSlot;
            Debug.LogWarning($"[PlayerSpawner] More than {SupportedPlayerCount} players; using fallback slot {fallbackSlot}.");
            return fallbackSlot;
        }

        private static SpawnPose GetGameplaySpawnPose(int slot)
        {
            var points = FindObjectsByType<NetworkPlayerSpawnPoint>(FindObjectsInactive.Exclude);
            System.Array.Sort(points, (left, right) => left.Order.CompareTo(right.Order));
            if (slot >= 0 && slot < points.Length)
            {
                return new SpawnPose(points[slot].transform.position, points[slot].transform.rotation);
            }

            Debug.LogWarning($"[PlayerSpawner] Gameplay SpawnPoint {slot} missing; using deterministic fallback.");
            return GetFallbackPose(slot);
        }

        private static SpawnPose GetFallbackPose(int slot)
        {
            var row = slot / 2;
            var column = slot % 2;
            return new SpawnPose(
                new Vector3((column - 0.5f) * FallbackSpacing, 1f, row * FallbackSpacing),
                Quaternion.identity);
        }

        private void HandleSessionStateChanged(NetworkSessionState state, string message)
        {
            if (state == NetworkSessionState.Disconnected || state == NetworkSessionState.Failed)
            {
                _spawnSlots.Clear();
                _doorInstance = null;
                _pickupItemInstance = null;
            }
        }

        private readonly struct SpawnPose
        {
            public SpawnPose(Vector3 position, Quaternion rotation)
            {
                Position = position;
                Rotation = rotation;
            }

            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
        }
    }
}
