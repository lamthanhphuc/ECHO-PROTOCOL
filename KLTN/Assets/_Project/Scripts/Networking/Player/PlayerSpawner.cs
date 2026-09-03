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
        [Header("Authoritative Gameplay World")]
        [SerializeField] private NetworkObject _doorPrefab;
        [SerializeField] private NetworkObject _pickupItemPrefab;
        [SerializeField, Range(1, 4)] private int _energyCoreCount = 3;
        [SerializeField] private Vector3 _energyCoreSpawnOrigin = new Vector3(2f, 0.5f, 2.5f);
        [SerializeField, Min(0.5f)] private float _energyCoreSpawnSpacing = 1.25f;
        [SerializeField] private NetworkObject _sectorBoxPrefab;
        [SerializeField] private NetworkObject _powerPuzzlePrefab;
        [SerializeField] private NetworkObject _powerPuzzleStationPrefab;
        [SerializeField] private NetworkObject _monsterPrefab;

        private readonly Dictionary<PlayerRef, int> _spawnSlots = new Dictionary<PlayerRef, int>();
        private FusionPlayerLifecycle _subscribedLifecycle;
        private NetworkObject _doorInstance;
        private readonly List<NetworkObject> _energyCoreInstances = new List<NetworkObject>();
        private NetworkObject _sectorBoxInstance;
        private NetworkObject _powerPuzzleInstance;
        private readonly List<NetworkObject> _powerPuzzleStationInstances = new List<NetworkObject>();
        private NetworkObject _monsterInstance;

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
            if (SceneManager.GetActiveScene().name != LobbyManager.GameSceneName) return;

            DisableLegacyObjectiveMutators();
            if (!runner.IsServer) return;

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
            if (_sectorBoxPrefab == null)
            {
                _sectorBoxPrefab = Resources.Load<NetworkObject>("Network/NetworkSectorBox");
            }
            if (_powerPuzzlePrefab == null)
            {
                _powerPuzzlePrefab = Resources.Load<NetworkObject>("Network/NetworkPowerPuzzle");
            }
            if (_powerPuzzleStationPrefab == null)
            {
                _powerPuzzleStationPrefab = Resources.Load<NetworkObject>("Network/NetworkPowerPuzzleStation");
            }
            if (_doorInstance == null && _doorPrefab != null)
            {
                _doorInstance = runner.Spawn(_doorPrefab, new Vector3(0f, 1f, 2.5f), Quaternion.identity);
                Debug.Log($"[PlayerSpawner] Spawned authoritative door {_doorInstance.Id}.");
            }
            while (_energyCoreInstances.Count < _energyCoreCount && _pickupItemPrefab != null)
            {
                var index = _energyCoreInstances.Count;
                var position = GetEnergyCoreSpawnPosition(index);
                var core = runner.Spawn(_pickupItemPrefab, position, Quaternion.identity);
                _energyCoreInstances.Add(core);
                Debug.Log($"[PlayerSpawner] Spawned authoritative Energy Core {index + 1}/{_energyCoreCount}: {core.Id}.");
            }
            if (_sectorBoxInstance == null && _sectorBoxPrefab != null)
            {
                var sectorPose = GetSectorBoxPose();
                _sectorBoxInstance = runner.Spawn(
                    _sectorBoxPrefab,
                    sectorPose.Position,
                    sectorPose.Rotation);
                Debug.Log($"[PlayerSpawner] Spawned authoritative Sector Box {_sectorBoxInstance.Id}.");
            }
            EnsurePowerPuzzle(runner);
            if (_monsterInstance == null && _monsterPrefab != null)
            {
                _monsterInstance = runner.Spawn(_monsterPrefab, new Vector3(0f, 0f, 8f), Quaternion.identity);
                Debug.Log($"[PlayerSpawner] Spawned host-authoritative monster {_monsterInstance.Id}.");
            }
        }

        private void EnsurePowerPuzzle(NetworkRunner runner)
        {
            if (_sectorBoxInstance == null || _powerPuzzlePrefab == null) return;

            if (_powerPuzzleInstance == null)
            {
                _powerPuzzleInstance = runner.Spawn(_powerPuzzlePrefab, Vector3.zero, Quaternion.identity);
                if (_powerPuzzleInstance.TryGetComponent<NetworkPowerPuzzle>(out var puzzle))
                {
                    puzzle.InitializeAuthoritative(_sectorBoxInstance.Id);
                }
                Debug.Log($"[PlayerSpawner] Spawned authoritative Power Puzzle {_powerPuzzleInstance.Id}.");
            }

            if (_powerPuzzleStationPrefab == null) return;
            var stationCount = _powerPuzzleInstance.TryGetComponent<NetworkPowerPuzzle>(out var state)
                ? state.StationCount
                : 2;
            while (_powerPuzzleStationInstances.Count < stationCount)
            {
                var inputId = _powerPuzzleStationInstances.Count;
                var pose = GetPowerPuzzleStationPose(inputId);
                var stationObject = runner.Spawn(_powerPuzzleStationPrefab, pose.Position, pose.Rotation);
                if (stationObject.TryGetComponent<NetworkPowerPuzzleStation>(out var station))
                {
                    station.InitializeAuthoritative(_powerPuzzleInstance.Id, inputId, pose.UseFallbackVisual);
                }
                _powerPuzzleStationInstances.Add(stationObject);
            }
        }

        private SpawnPose GetPowerPuzzleStationPose(int inputId)
        {
            var stations = FindObjectsByType<PowerPuzzleStation>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            System.Array.Sort(stations, (left, right) =>
            {
                var typeOrder = left.StationType.CompareTo(right.StationType);
                return typeOrder != 0
                    ? typeOrder
                    : string.CompareOrdinal(left.name, right.name);
            });
            if (inputId >= 0 && inputId < stations.Length && stations[inputId] != null)
            {
                return new SpawnPose(
                    stations[inputId].transform.position,
                    stations[inputId].transform.rotation,
                    useFallbackVisual: false);
            }

            var sectorPosition = _sectorBoxInstance != null
                ? _sectorBoxInstance.transform.position
                : Vector3.zero;
            return new SpawnPose(
                sectorPosition + new Vector3((inputId - 0.5f) * 1.5f, 0f, 2f),
                Quaternion.identity,
                useFallbackVisual: true);
        }

        private Vector3 GetEnergyCoreSpawnPosition(int index)
        {
            var container = GameObject.Find("EnergyCoreSpawnCandidates");
            if (container != null && index >= 0 && index < container.transform.childCount)
            {
                return container.transform.GetChild(index).position;
            }

            return _energyCoreSpawnOrigin + Vector3.right * (_energyCoreSpawnSpacing * index);
        }

        private static SpawnPose GetSectorBoxPose()
        {
            var sectorBoxes = FindObjectsByType<SectorBox>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (sectorBoxes.Length > 0 && sectorBoxes[0] != null)
            {
                return new SpawnPose(sectorBoxes[0].transform.position, sectorBoxes[0].transform.rotation);
            }

            return new SpawnPose(new Vector3(-2f, 0.75f, 2.5f), Quaternion.identity);
        }

        private static void DisableLegacyObjectiveMutators()
        {
            foreach (var legacyCore in FindObjectsByType<EnergyCorePickup>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                legacyCore.enabled = false;
            }

            foreach (var legacySector in FindObjectsByType<SectorBox>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                legacySector.enabled = false;
            }

            foreach (var legacyProgress in FindObjectsByType<EnergyCoreObjectiveProgress>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                legacyProgress.enabled = false;
            }

            foreach (var legacyPuzzle in FindObjectsByType<PowerPuzzleController>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                legacyPuzzle.SetNetworkAuthorityPresentationOnly(true);
                legacyPuzzle.enabled = false;
            }

            foreach (var legacyStation in FindObjectsByType<PowerPuzzleStation>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                legacyStation.SetNetworkAuthorityPresentationOnly(true);
                foreach (var stationCollider in legacyStation.GetComponentsInChildren<Collider>())
                {
                    stationCollider.enabled = false;
                }
                legacyStation.enabled = false;
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
                _energyCoreInstances.Clear();
                _sectorBoxInstance = null;
                _powerPuzzleInstance = null;
                _powerPuzzleStationInstances.Clear();
                _monsterInstance = null;
            }
        }

        private readonly struct SpawnPose
        {
            public SpawnPose(Vector3 position, Quaternion rotation, bool useFallbackVisual = false)
            {
                Position = position;
                Rotation = rotation;
                UseFallbackVisual = useFallbackVisual;
            }

            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public bool UseFallbackVisual { get; }
        }
    }
}
