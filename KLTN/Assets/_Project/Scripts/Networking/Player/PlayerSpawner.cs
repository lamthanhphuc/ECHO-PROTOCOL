using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Networking
{
    /// <summary>Host-authoritative gameplay placement coordinator for lifecycle-owned player objects.</summary>
    public sealed class PlayerSpawner : MonoBehaviour
    {
        private const int SupportedPlayerCount = 4;
        private const float FallbackSpacing = 2.5f;

        [SerializeField] private NetworkBootstrap _bootstrap;
        [Header("M2-024 World State Demo")]
        [SerializeField] private NetworkObject _doorPrefab;
        [SerializeField] private NetworkObject _pickupItemPrefab;

        private readonly Dictionary<PlayerRef, int> _spawnSlots = new Dictionary<PlayerRef, int>();
        private FusionPlayerLifecycle _subscribedLifecycle;
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
            TryAttachLifecycle(_bootstrap.Runner);
        }

        private void OnDisable()
        {
            DetachLifecycle();
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
            TryAttachLifecycle(runner);
            GetOrAssignSlot(player);
            if (runner.TryGetPlayerObject(player, out var playerObject) && playerObject != null)
            {
                ConfigureExistingPlayerObject(player, playerObject, gameplay);
            }
        }

        private void HandlePlayerLeft(PlayerRef player)
        {
            _spawnSlots.Remove(player);
        }

        private void HandleNetworkSceneLoadDone(NetworkRunner runner)
        {
            if (!runner.IsServer || SceneManager.GetActiveScene().name != LobbyManager.GameSceneName) return;

            TryAttachLifecycle(runner);
            Debug.Log("[PlayerSpawner] Gameplay scene ready. Placing lifecycle-owned player objects.");
            foreach (var player in runner.ActivePlayers)
            {
                if (!runner.TryGetPlayerObject(player, out var playerObject) || playerObject == null)
                {
                    Debug.LogWarning($"[PlayerSpawner] No lifecycle-owned player object available yet for {player}.");
                    continue;
                }

                ConfigureExistingPlayerObject(player, playerObject, gameplay: true);
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

        private void HandleLifecyclePlayerObjectCommitted(FusionPlayerObjectCommit commit)
        {
            var runner = _bootstrap?.Runner;
            if (runner == null || !runner.IsServer || commit.PlayerObject == null)
            {
                return;
            }

            var gameplay = SceneManager.GetActiveScene().name == LobbyManager.GameSceneName;
            ConfigureExistingPlayerObject(commit.Player, commit.PlayerObject, gameplay);
        }

        private void ConfigureExistingPlayerObject(PlayerRef player, NetworkObject playerObject, bool gameplay)
        {
            if (!player.IsValid || playerObject == null)
            {
                Debug.LogError("[PlayerSpawner] Cannot place an invalid lifecycle-owned player object.");
                return;
            }

            var slot = GetOrAssignSlot(player);
            var pose = gameplay ? GetGameplaySpawnPose(slot) : GetFallbackPose(slot);
            if (playerObject.TryGetComponent<LobbyPlayerState>(out var state))
            {
                state.InitializeAuthoritativeSelection(state.TeamId, state.ToolId, gameplay);
            }

            if (!TryTeleportExistingPlayer(playerObject, pose))
            {
                Debug.LogWarning($"[PlayerSpawner] Could not teleport lifecycle-owned player object for {player}; object={playerObject.Id}.");
            }

            Debug.Log(
                $"[PlayerSpawner] Placed {player} object={playerObject.Id}, slot={slot}, " +
                $"inputAuthority={playerObject.InputAuthority}, stateAuthority=Host, gameplay={gameplay}.");
        }

        private bool TryTeleportExistingPlayer(NetworkObject playerObject, SpawnPose pose)
        {
            if (!playerObject.HasStateAuthority)
            {
                return false;
            }

            if (playerObject.TryGetComponent<NetworkCharacterController>(out var characterController))
            {
                characterController.Teleport(pose.Position, pose.Rotation);
                return true;
            }

            if (playerObject.TryGetComponent<NetworkTransform>(out var networkTransform))
            {
                networkTransform.Teleport(pose.Position);
                return true;
            }

            return false;
        }

        private void TryAttachLifecycle(NetworkRunner runner)
        {
            if (runner == null)
            {
                return;
            }

            var lifecycle = runner.GetComponent<FusionPlayerLifecycle>();
            if (lifecycle == null || lifecycle == _subscribedLifecycle)
            {
                return;
            }

            DetachLifecycle();
            _subscribedLifecycle = lifecycle;
            _subscribedLifecycle.PlayerObjectCommitted += HandleLifecyclePlayerObjectCommitted;
        }

        private void DetachLifecycle()
        {
            if (_subscribedLifecycle == null)
            {
                return;
            }

            _subscribedLifecycle.PlayerObjectCommitted -= HandleLifecyclePlayerObjectCommitted;
            _subscribedLifecycle = null;
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
