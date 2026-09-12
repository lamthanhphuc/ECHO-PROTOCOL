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
        private const int TargetSectorBoxCount = 2;
        private const float FallbackSpacing = 2.5f;

        [SerializeField] private NetworkBootstrap _bootstrap;
        [Header("Lobby Lineup")]
        [Tooltip("First player's position for the Lobby camera at (0, 1, -10). Leaves the left side for the lobby panel.")]
        [SerializeField] private Vector3 _lobbySpawnOrigin = new Vector3(0.15f, 1f, -5.5f);
        [SerializeField, Min(1f)] private float _lobbySpawnSpacing = 1.1f;
        [Header("Authoritative Gameplay World")]
        [SerializeField] private NetworkObject _doorPrefab;
        [SerializeField] private NetworkObject _pickupItemPrefab;
        [SerializeField, Range(1, 4)] private int _energyCoreCount = 4;
        [SerializeField] private Vector3 _energyCoreSpawnOrigin = new Vector3(2f, 0.5f, 2.5f);
        [SerializeField, Min(0.5f)] private float _energyCoreSpawnSpacing = 1.25f;
        [SerializeField, Min(0f)] private float _energyCoreSpawnSurfaceOffset = 0.35f;
        [SerializeField] private NetworkObject _sectorBoxPrefab;
        [SerializeField] private NetworkObject _matchStatePrefab;
        [SerializeField] private NetworkObject _powerPuzzlePrefab;
        [SerializeField] private NetworkObject _powerPuzzleStationPrefab;
        [SerializeField] private NetworkObject _monsterPrefab;

        private readonly Dictionary<PlayerRef, int> _spawnSlots = new Dictionary<PlayerRef, int>();
        private FusionPlayerLifecycle _subscribedLifecycle;
        private NetworkObject _doorInstance;
        private readonly List<NetworkObject> _energyCoreInstances = new List<NetworkObject>();
        private readonly List<SpawnPose> _selectedEnergyCoreSpawnPoses = new List<SpawnPose>();
        private NetworkObject _sectorBoxInstance;
        private readonly List<NetworkObject> _sectorBoxInstances = new List<NetworkObject>();
        private NetworkObject _matchStateInstance;
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
            EnsureGameplayHUD();
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
            if (_matchStatePrefab == null)
            {
                _matchStatePrefab = Resources.Load<NetworkObject>("Network/NetworkMatchState");
            }
            if (_sectorBoxPrefab == null)
            {
                _sectorBoxPrefab = Resources.Load<NetworkObject>("Network/NetworkSectorBox");
            }
            if (_matchStateInstance == null && _matchStatePrefab != null)
            {
                _matchStateInstance = runner.Spawn(_matchStatePrefab, Vector3.zero, Quaternion.identity);
                Debug.Log($"[PlayerSpawner] Spawned authoritative Match State {_matchStateInstance.Id}.");
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
                var pose = GetEnergyCoreSpawnPose(index);
                var core = runner.Spawn(
                    _pickupItemPrefab,
                    pose.Position,
                    pose.Rotation,
                    PlayerRef.None,
                    (_, spawnedObject) =>
                    {
                        if (spawnedObject != null
                            && spawnedObject.TryGetComponent<NetworkPickupItem>(out var pickup))
                        {
                            pickup.InitializeAuthoritativePose(pose.Position, pose.Rotation);
                        }
                    });
                if (core != null)
                {
                    core.name = $"EnergyCore_Network_{index + 1:00}";
                    if (core.TryGetComponent<NetworkPickupItem>(out var pickup))
                    {
                        pickup.InitializeAuthoritativePose(pose.Position, pose.Rotation);
                    }
                }
                _energyCoreInstances.Add(core);
                Debug.Log($"[PlayerSpawner] Spawned authoritative Energy Core {index + 1}/{_energyCoreCount}: {(core != null ? core.Id.ToString() : "null")} at {pose.Position}.");
            }
            if (_sectorBoxPrefab != null && _sectorBoxInstances.Count == 0)
            {
                var sceneBoxes = GetOrderedSceneSectorBoxes();
                if (sceneBoxes.Count > 0)
                {
                    var count = Mathf.Min(TargetSectorBoxCount, sceneBoxes.Count);
                    for (int i = 0; i < count; i++)
                    {
                        var box = sceneBoxes[i];
                        var boxInstance = runner.Spawn(
                            _sectorBoxPrefab,
                            box.transform.position,
                            box.transform.rotation);
                        _sectorBoxInstances.Add(boxInstance);
                        Debug.Log($"[PlayerSpawner] Spawned authoritative Sector Box {i + 1}/{count} from scene marker '{box.name}': {boxInstance.Id}.");
                    }

                    _sectorBoxInstance = _sectorBoxInstances[0];
                }
                else
                {
                    var sectorPose = GetSectorBoxPose();
                    _sectorBoxInstance = runner.Spawn(
                        _sectorBoxPrefab,
                        sectorPose.Position,
                        sectorPose.Rotation);
                    _sectorBoxInstances.Add(_sectorBoxInstance);
                    Debug.Log($"[PlayerSpawner] Spawned fallback authoritative Sector Box {_sectorBoxInstance.Id}.");
                }
            }
            EnsurePowerPuzzle(runner);
            if (_monsterInstance == null && _monsterPrefab != null)
            {
                var stalkerSpawn = GameObject.Find("MonsterSpawn_Stalker_EMPTY");
                var spawnPosition = stalkerSpawn != null
                    ? stalkerSpawn.transform.position
                    : new Vector3(0f, 0f, 8f);
                var spawnRotation = stalkerSpawn != null
                    ? stalkerSpawn.transform.rotation
                    : Quaternion.identity;

                _monsterInstance = runner.Spawn(
                    _monsterPrefab,
                    spawnPosition,
                    spawnRotation);

                Debug.Log(
                    $"[PlayerSpawner] Spawned host-authoritative monster {_monsterInstance.Id} " +
                    $"at {spawnPosition} markerFound={stalkerSpawn != null}.");
            }
            BindAuthoritativeWorldState();
        }

        private void BindAuthoritativeWorldState()
        {
            if (_matchStateInstance == null || _doorInstance == null || _sectorBoxInstance == null)
            {
                return;
            }

            if (_matchStateInstance.TryGetComponent<NetworkMatchState>(out var matchState))
            {
                matchState.InitializeAuthoritative(_sectorBoxInstance.Id, _doorInstance.Id);
            }
            if (_doorInstance.TryGetComponent<NetworkDoor>(out var door))
            {
                door.InitializeAuthoritative(_matchStateInstance.Id);
            }
            for (int i = 0; i < _sectorBoxInstances.Count; i++)
            {
                if (_sectorBoxInstances[i] != null && _sectorBoxInstances[i].TryGetComponent<NetworkSectorBox>(out var sectorBox))
                {
                    sectorBox.InitializeAuthoritative(_matchStateInstance.Id);
                }
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
            var stations = FindObjectsByType<PowerPuzzleStation>(FindObjectsInactive.Include);
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

        private SpawnPose GetEnergyCoreSpawnPose(int index)
        {
            if (_selectedEnergyCoreSpawnPoses.Count == 0)
            {
                SelectEnergyCoreSpawnPoses();
            }

            if (index >= 0 && index < _selectedEnergyCoreSpawnPoses.Count)
            {
                return _selectedEnergyCoreSpawnPoses[index];
            }

            return new SpawnPose(ProjectToGround(
                _energyCoreSpawnOrigin + Vector3.right * (_energyCoreSpawnSpacing * index),
                _energyCoreSpawnSurfaceOffset),
                Quaternion.identity,
                useFallbackVisual: true);
        }

        private void SelectEnergyCoreSpawnPoses()
        {
            _selectedEnergyCoreSpawnPoses.Clear();

            var candidates = GetOrderedEnergyCoreSpawnCandidates();
            if (candidates.Count == 0)
            {
                Debug.LogWarning("[PlayerSpawner] No EnergyCore_C* spawn candidates found; using fallback Energy Core line spawn.");
                return;
            }

            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int swapIndex = Random.Range(0, i + 1);
                (candidates[i], candidates[swapIndex]) = (candidates[swapIndex], candidates[i]);
            }

            var selectedCount = Mathf.Min(_energyCoreCount, candidates.Count);
            for (int i = 0; i < selectedCount; i++)
            {
                var candidate = candidates[i];
                _selectedEnergyCoreSpawnPoses.Add(new SpawnPose(candidate.position, candidate.rotation));
                Debug.Log($"[PlayerSpawner] Selected Energy Core spawn candidate '{candidate.name}' at {candidate.position}.");
            }

            Debug.Log($"[PlayerSpawner] Selected {_selectedEnergyCoreSpawnPoses.Count}/{candidates.Count} Energy Core spawn candidates.");
        }

        private static List<Transform> GetOrderedEnergyCoreSpawnCandidates()
        {
            var candidates = new List<Transform>();
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                {
                    continue;
                }

                var roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    foreach (var candidate in roots[rootIndex].GetComponentsInChildren<Transform>(true))
                    {
                        if (IsEnergyCoreSpawnCandidate(candidate))
                        {
                            candidates.Add(candidate);
                        }
                    }
                }
            }

            candidates.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            return candidates;
        }

        private static bool IsEnergyCoreSpawnCandidate(Transform candidate)
        {
            return candidate != null
                && candidate.name.StartsWith("EnergyCore_C", System.StringComparison.OrdinalIgnoreCase)
                && !candidate.name.StartsWith("EnergyCore_Network", System.StringComparison.OrdinalIgnoreCase);
        }

        private static SpawnPose GetSectorBoxPose()
        {
            var sectorBoxes = GetOrderedSceneSectorBoxes();
            if (sectorBoxes.Count > 0)
            {
                return new SpawnPose(sectorBoxes[0].transform.position, sectorBoxes[0].transform.rotation);
            }

            return new SpawnPose(new Vector3(-2f, 0.75f, 2.5f), Quaternion.identity);
        }

        private static List<SectorBox> GetOrderedSceneSectorBoxes()
        {
            var sectorBoxes = FindObjectsByType<SectorBox>(FindObjectsInactive.Include);
            System.Array.Sort(sectorBoxes, (left, right) => string.CompareOrdinal(left.name, right.name));
            var ordered = new List<SectorBox>(sectorBoxes.Length);
            for (int i = 0; i < sectorBoxes.Length; i++)
            {
                if (sectorBoxes[i] != null)
                {
                    ordered.Add(sectorBoxes[i]);
                }
            }

            return ordered;
        }

        private static void DisableLegacyObjectiveMutators()
        {
            foreach (var legacyCore in FindObjectsByType<EnergyCorePickup>(FindObjectsInactive.Include))
            {
                // CRITICAL MULTIPLAYER FIX: Tuyệt đối không disable đối tượng mạng (NetworkObject)
                if (legacyCore.GetComponentInParent<Fusion.NetworkObject>() != null)
                {
                    continue;
                }

                legacyCore.enabled = false;
                if (!legacyCore.name.StartsWith("EnergyCore_Network", System.StringComparison.OrdinalIgnoreCase))
                {
                    legacyCore.gameObject.SetActive(false);
                }
            }

            foreach (var rootObj in GetLoadedSceneRoots())
            {
                foreach (var t in rootObj.GetComponentsInChildren<Transform>(true))
                {
                    if (t != null
                        && (t.name.StartsWith("PF_EnergyCore_Imported", System.StringComparison.OrdinalIgnoreCase)
                            || t.name.StartsWith("EnergyCore_C", System.StringComparison.OrdinalIgnoreCase)))
                    {
                        // CRITICAL MULTIPLAYER FIX: Tuyệt đối không disable đối tượng mạng runtime đã spawn
                        if (t.GetComponentInParent<Fusion.NetworkObject>() != null)
                        {
                            continue;
                        }

                        if (!t.name.StartsWith("EnergyCore_Network", System.StringComparison.OrdinalIgnoreCase))
                        {
                            t.gameObject.SetActive(false);
                        }
                    }
                }
            }

            foreach (var rb in FindObjectsByType<Rigidbody>(FindObjectsInactive.Include))
            {
                if (rb == null || rb.GetComponent<Fusion.NetworkObject>() != null) continue;
                string n = rb.name;
                if (n.Contains("Barrel") || n.Contains("SciFiBarrel") || n.Contains("Crate") || n.Contains("Pallet") || n.Contains("Bin"))
                {
                    if (!rb.isKinematic)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                        rb.isKinematic = true;
                    }
                    rb.useGravity = false;
                }
            }

            foreach (var legacySector in FindObjectsByType<SectorBox>(FindObjectsInactive.Include))
            {
                legacySector.enabled = false;
            }

            foreach (var legacyProgress in FindObjectsByType<EnergyCoreObjectiveProgress>(FindObjectsInactive.Include))
            {
                legacyProgress.enabled = false;
            }

            foreach (var legacyPuzzle in FindObjectsByType<PowerPuzzleController>(FindObjectsInactive.Include))
            {
                legacyPuzzle.SetNetworkAuthorityPresentationOnly(true);
                legacyPuzzle.enabled = false;
            }

            foreach (var legacyStation in FindObjectsByType<PowerPuzzleStation>(FindObjectsInactive.Include))
            {
                legacyStation.SetNetworkAuthorityPresentationOnly(true);
                foreach (var stationCollider in legacyStation.GetComponentsInChildren<Collider>())
                {
                    stationCollider.enabled = false;
                }
                legacyStation.enabled = false;
            }
        }

        private static IEnumerable<GameObject> GetLoadedSceneRoots()
        {
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                {
                    continue;
                }

                var roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    yield return roots[rootIndex];
                }
            }
        }

        private static void EnsureGameplayHUD()
        {
            if (FindAnyObjectByType<EchoProtocol.UI.HUD.GameplayHUDManager>() != null) return;

            var hudPrefab = Resources.Load<GameObject>("PF_GameplayHUD_Canvas");
            if (hudPrefab == null)
            {
                hudPrefab = Resources.Load<GameObject>("Prefabs/UI/PF_GameplayHUD_Canvas");
            }

            if (hudPrefab != null)
            {
                Instantiate(hudPrefab);
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
            var pose = gameplay ? GetGameplaySpawnPose(slot) : GetLobbySpawnPose(slot);
            if (playerObject.TryGetComponent<LobbyPlayerState>(out var state))
            {
                // In gameplay, players always start unarmed (ToolId = 0) and must pick up tools in the map
                var enteringGameplay = gameplay && !state.IsGameplayPlayer;
                var toolId = enteringGameplay ? 0 : state.ToolId;
                var teamId = state.TeamId > 0 ? state.TeamId : slot;
                state.InitializeAuthoritativeSelection(teamId, toolId, gameplay);
            }

            if (playerObject.TryGetComponent<NetworkPlayerLifeState>(out var lifeState) && playerObject.HasStateAuthority && gameplay)
            {
                lifeState.ResetForMatchAuthoritative();
            }

            if (playerObject.TryGetComponent<NetworkPlayerMovement>(out var movement) && playerObject.HasStateAuthority)
            {
                movement.IsHidden = false;
            }

            if (playerObject.TryGetComponent<PlayerHidingController>(out var hiding) && hiding.IsHidden)
            {
                hiding.ExitHiding();
            }

            if (!TryTeleportExistingPlayer(playerObject, pose, gameplay))
            {
                Debug.LogWarning($"[PlayerSpawner] Could not teleport lifecycle-owned player object for {player}; object={playerObject.Id}.");
            }

            Debug.Log(
                $"[PlayerSpawner] Placed {player} object={playerObject.Id}, slot={slot}, " +
                $"inputAuthority={playerObject.InputAuthority}, stateAuthority=Host, gameplay={gameplay}.");
        }

        private bool TryTeleportExistingPlayer(NetworkObject playerObject, SpawnPose pose, bool projectToGround)
        {
            if (!playerObject.HasStateAuthority)
            {
                return false;
            }

            var position = projectToGround
                ? GetGroundedPlayerSpawnPosition(playerObject, pose.Position)
                : pose.Position;

            if (playerObject.TryGetComponent<NetworkCharacterController>(out var characterController))
            {
                characterController.Teleport(position, pose.Rotation);
                Physics.SyncTransforms();
                return true;
            }

            if (playerObject.TryGetComponent<NetworkTransform>(out var networkTransform))
            {
                networkTransform.Teleport(position, pose.Rotation);
                Physics.SyncTransforms();
                return true;
            }

            return false;
        }

        private static Vector3 GetGroundedPlayerSpawnPosition(NetworkObject playerObject, Vector3 sourcePosition)
        {
            var characterController = playerObject != null
                ? playerObject.GetComponent<CharacterController>()
                : null;
            float bottomToRootOffset = 0f;
            if (characterController != null)
            {
                bottomToRootOffset = characterController.height * 0.5f - characterController.center.y;
            }

            return ProjectToGround(sourcePosition, Mathf.Max(0f, bottomToRootOffset) + 0.03f);
        }

        private static Vector3 ProjectToGround(Vector3 sourcePosition, float surfaceOffset)
        {
            var rayStart = sourcePosition + Vector3.up * 2f;
            if (Physics.Raycast(rayStart, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore))
            {
                sourcePosition.y = hit.point.y + surfaceOffset;
                return sourcePosition;
            }

            if (sourcePosition.y < surfaceOffset)
            {
                sourcePosition.y = surfaceOffset;
            }

            return sourcePosition;
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

        private SpawnPose GetLobbySpawnPose(int slot)
        {
            // Keep all four players in one row, close to the Lobby camera and
            // to the right of the network panel. The host assigns stable slots.
            var position = _lobbySpawnOrigin + Vector3.right * (slot * _lobbySpawnSpacing);
            return new SpawnPose(position, Quaternion.Euler(0f, 180f, 0f));
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
                _selectedEnergyCoreSpawnPoses.Clear();
                _sectorBoxInstance = null;
                _sectorBoxInstances.Clear();
                _matchStateInstance = null;
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
