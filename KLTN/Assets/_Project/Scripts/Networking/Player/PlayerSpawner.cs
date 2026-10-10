using System.Collections.Generic;
using EchoProtocol.AI.AED;
using EchoProtocol.AI.Minions;
using EchoProtocol.AI.Stalker;
using EchoProtocol.AI.Stalker.Spatial;
using EchoProtocol.Diagnostics;
using EchoProtocol.Gameplay;
using EchoProtocol.Networking.Authority;
using Fusion;
using UnityEngine;
using UnityEngine.AI;
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
        [SerializeField] private NetworkObject _creepMinionPrefab;
        [Header("Creep Minion Spawn")]
        [SerializeField, Min(1f)] private float _minionSpawnCheckInterval = 3f;

        // Minion xuất hiện tương đối gần Stalker.
        [SerializeField, Min(1f)] private float _minionSpawnMinDistanceFromStalker = 4f;
        [SerializeField, Min(2f)] private float _minionSpawnMaxDistanceFromStalker = 10f;
        [SerializeField, Min(5f)] private float _minionSpawnFallbackMaxDistanceFromStalker = 28f;

        // Nhưng không được xuất hiện sát Player.
        [SerializeField, Min(5f)] private float _minionSpawnMinDistanceFromPlayer = 18f;

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
        private NetworkObject _zone2MonsterInstance;
        private bool _zone2MonsterSpawned;
        private NetworkObject _zone3MonsterInstance;
        private bool _zone3MonsterSpawned;
        private bool _zone3SpawnFailureLogged;
        private readonly List<NetworkObject> _creepMinionInstances = new List<NetworkObject>();
        private float _nextMinionSpawnCheckAt;
        private bool _zone2MinionsActive;

        private bool _missingMinionPrefabLogged;

        private void Update()
        {
            var runner = _bootstrap != null ? _bootstrap.Runner : null;
            if (runner == null || !runner.IsServer || !runner.IsRunning
                || _matchStateInstance == null
                || !_matchStateInstance.TryGetComponent<NetworkMatchState>(out var matchState)) return;

            if (matchState.IsEnded)
            {
                if (IsValidNetworkObject(
                        _monsterInstance))
                {
                    runner.Despawn(
                        _monsterInstance);

                    _monsterInstance = null;
                }

                if (IsValidNetworkObject(
                        _zone2MonsterInstance))
                {
                    runner.Despawn(
                        _zone2MonsterInstance);

                    _zone2MonsterInstance = null;
                }

                if (IsValidNetworkObject(
                        _zone3MonsterInstance))
                {
                    runner.Despawn(
                        _zone3MonsterInstance);

                    _zone3MonsterInstance = null;
                }

                DespawnAllCreepMinions(
                    runner);

                return;
            }

            if (_bootstrap.State != NetworkSessionState.InMatch
                || SceneManager.GetActiveScene().name != LobbyManager.GameSceneName) return;

            EnsureZone3Stalker(runner, matchState);

            var phase = matchState.CurrentPhase;
            if (phase == NetworkMatchPhase.FinalHunt
                || phase == NetworkMatchPhase.Escape)
            {
                return;
            }

            if (Time.time
                < _nextMinionSpawnCheckAt)
            {
                return;
            }

            _nextMinionSpawnCheckAt =
                Time.time
                + _minionSpawnCheckInterval;

            MaintainCreepMinionPopulation(
                runner);
        }

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
            if (_bootstrap != null && _bootstrap.State == NetworkSessionState.InMatch) return;
            _spawnSlots.Remove(player);
        }

        private void HandleNetworkSceneLoadDone(NetworkRunner runner)
        {
            if (SceneManager.GetActiveScene().name != LobbyManager.GameSceneName)
            {
                ClearAuthoritativeWorldStateReferences();

                if (runner.IsServer)
                {
                    TryAttachLifecycle(runner);
                    foreach (var player in runner.ActivePlayers)
                    {
                        if (runner.TryGetPlayerObject(player, out var playerObject) && playerObject != null)
                        {
                            ConfigureExistingPlayerObject(player, playerObject, gameplay: false);
                            if (playerObject.TryGetComponent<LobbyPlayerState>(
                                    out var lobbyState))
                            {
                                lobbyState.ResetForLobbyAuthoritative();
                            }
                        }
                    }
                }
                return;
            }

            PruneInvalidAuthoritativeWorldStateReferences();
            if (_zone2MonsterInstance == null) _zone2MonsterSpawned = false;
            if (_zone3MonsterInstance == null) _zone3MonsterSpawned = false;
            DisableLegacyObjectiveMutators();
            EnsureGameplayHUD();
            if (!runner.IsServer) return;

            TryAttachLifecycle(runner);
            RuntimeLog.Log(
                RuntimeLogCategory.PlayerSpawner,
                "[PlayerSpawner] Gameplay scene ready. Placing lifecycle-owned player objects.");
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
            PruneInvalidAuthoritativeWorldStateReferences();

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
                RuntimeLog.Log(
                RuntimeLogCategory.PlayerSpawner,
                $"[PlayerSpawner] Spawned authoritative Match State {_matchStateInstance.Id}.");
            }
            if (_powerPuzzlePrefab == null)
            {
                _powerPuzzlePrefab = Resources.Load<NetworkObject>("Network/NetworkPowerPuzzle");
            }
            if (_powerPuzzleStationPrefab == null)
            {
                _powerPuzzleStationPrefab = Resources.Load<NetworkObject>("Network/NetworkPowerPuzzleStation");
            }
            if (_doorInstance == null && _doorPrefab != null && !Zone3MissionDirector.IsSciFiSceneLoaded)
            {
                _doorInstance = runner.Spawn(_doorPrefab, new Vector3(0f, 1f, 2.5f), Quaternion.identity);
                RuntimeLog.Log(
                RuntimeLogCategory.PlayerSpawner,
                $"[PlayerSpawner] Spawned authoritative door {_doorInstance.Id}.");
            }
            RegisterExistingNetworkSectorBoxes();
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
                if (core == null)
                {
                    Debug.LogError(
                        $"[PlayerSpawner] Failed to spawn Energy Core " +
                        $"{index + 1}/{_energyCoreCount} at {pose.Position}.");
                    break;
                }

                core.name = $"EnergyCore_Network_{index + 1:00}";
                if (core.TryGetComponent<NetworkPickupItem>(out var pickup))
                {
                    pickup.InitializeAuthoritativePose(pose.Position, pose.Rotation);
                }

                _energyCoreInstances.Add(core);
                RuntimeLog.Log(
                RuntimeLogCategory.PlayerSpawner,
                $"[PlayerSpawner] Spawned authoritative Energy Core {index + 1}/{_energyCoreCount}: {core.Id} at {pose.Position}.");
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
                        RuntimeLog.Log(
                RuntimeLogCategory.PlayerSpawner,
                $"[PlayerSpawner] Spawned authoritative Sector Box {i + 1}/{count} from scene marker '{box.name}': {boxInstance.Id}.");
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
                    RuntimeLog.Log(
                RuntimeLogCategory.PlayerSpawner,
                $"[PlayerSpawner] Spawned fallback authoritative Sector Box {_sectorBoxInstance.Id}.");
                }
            }
            // Zone 2 uses scene authorization panels with NetworkMatchState as
            // the authoritative multiplayer state. Keep the retired path dormant.
            if (_monsterInstance == null && _monsterPrefab != null)
            {
                var stalkerSpawn = GameObject.Find("MonsterSpawn_Stalker_EMPTY");
                if (TryGetStalkerSpawnPosition(stalkerSpawn != null ? stalkerSpawn.transform : null, out var spawnPosition))
                {
                    _monsterInstance = SpawnStalker(runner, spawnPosition, stalkerSpawn.transform.rotation, RegionSemanticZone.Zone01);
                }
            }
            BindAuthoritativeWorldState();
        }

        public void TrySpawnZone2Stalker(Collider entrant, Transform spawnMarker)
        {
            var runner = _bootstrap != null ? _bootstrap.Runner : null;
            var playerState = entrant != null ? entrant.GetComponentInParent<LobbyPlayerState>() : null;
            if (playerState == null)
            {
                return;
            }

            if (runner == null)
            {
                RejectZone2Spawn(playerState, "runner-missing");
                return;
            }
            if (!runner.IsServer)
            {
                RejectZone2Spawn(playerState, "not-state-authority-server");
                return;
            }
            if (!runner.IsRunning)
            {
                RejectZone2Spawn(playerState, "runner-not-running");
                return;
            }
            if (_bootstrap == null || _bootstrap.State != NetworkSessionState.InMatch)
            {
                RejectZone2Spawn(playerState, "session-not-in-match");
                return;
            }
            if (SceneManager.GetActiveScene().name != LobbyManager.GameSceneName)
            {
                RejectZone2Spawn(playerState, $"wrong-scene:{SceneManager.GetActiveScene().name}");
                return;
            }
            if (_zone2MonsterSpawned)
            {
                RejectZone2Spawn(playerState, "already-spawned-flag");
                return;
            }
            if (IsValidNetworkObject(_zone2MonsterInstance))
            {
                RejectZone2Spawn(playerState, $"existing-instance:{_zone2MonsterInstance.Id}");
                return;
            }
            if (_monsterPrefab == null)
            {
                RejectZone2Spawn(playerState, "monster-prefab-missing");
                return;
            }
            if (playerState.Object == null || !playerState.Object.IsValid)
            {
                RejectZone2Spawn(playerState, "player-network-object-invalid");
                return;
            }
            if (!playerState.IsGameplayPlayer)
            {
                RejectZone2Spawn(playerState, "player-not-gameplay-player");
                return;
            }
            if (playerState.Object.InputAuthority == PlayerRef.None)
            {
                RejectZone2Spawn(playerState, "player-input-authority-missing");
                return;
            }
            if (!runner.TryGetPlayerObject(playerState.Object.InputAuthority, out var playerObject)
                || playerObject != playerState.Object)
            {
                RejectZone2Spawn(playerState, "player-object-identity-mismatch");
                return;
            }
            if (!IsValidNetworkObject(_matchStateInstance))
            {
                RejectZone2Spawn(playerState, "match-state-instance-invalid");
                return;
            }
            if (!_matchStateInstance.TryGetComponent<NetworkMatchState>(out var matchState))
            {
                RejectZone2Spawn(playerState, "match-state-component-missing");
                return;
            }
            if (matchState.IsEnded)
            {
                RejectZone2Spawn(playerState, "match-ended");
                return;
            }
            if (!TryGetStalkerSpawnPosition(spawnMarker, out var spawnPosition))
            {
                RejectZone2Spawn(playerState, "spawn-marker-not-on-navmesh-within-1m");
                return;
            }

            var spawned = SpawnStalker(
                runner,
                spawnPosition,
                spawnMarker.rotation,
                RegionSemanticZone.Zone02);
            if (!IsValidNetworkObject(spawned))
            {
                RejectZone2Spawn(playerState, "runner-spawn-returned-invalid-object");
                _zone2MonsterInstance = null;
                return;
            }

            _zone2MonsterInstance = spawned;
            _zone2MonsterSpawned = true;
            _zone2MinionsActive = true;

            DespawnCreepMinionsForZone(
                runner,
                RegionSemanticZone.Zone01);

            if (IsValidNetworkObject(
                    _monsterInstance))
            {
                runner.Despawn(
                    _monsterInstance);

                _monsterInstance = null;
            }

            _nextMinionSpawnCheckAt =
                Time.time;
        }

        private static void RejectZone2Spawn(LobbyPlayerState playerState, string reason)
        {
            Debug.LogWarning(
                $"[STK_ZONE2][REJECT] reason={reason} player={playerState.name} " +
                $"inputAuthority={playerState.Object?.InputAuthority}.",
                playerState);
        }

        private void EnsureZone3Stalker(NetworkRunner runner, NetworkMatchState matchState)
        {
            if (runner == null || !runner.IsServer || !runner.IsRunning || matchState == null
                || matchState.IsEnded || _zone3MonsterSpawned || IsValidNetworkObject(_zone3MonsterInstance)) return;

            var phase = matchState.CurrentPhase;
            if (phase != NetworkMatchPhase.Zone3FindFrigate
                && phase != NetworkMatchPhase.Zone3PushFrigate
                && phase != NetworkMatchPhase.FinalHunt
                && phase != NetworkMatchPhase.Escape) return;

            if (_monsterPrefab == null)
            {
                LogZone3SpawnFailureOnce("monster-prefab-missing");
                return;
            }

            var marker = GameObject.Find("MonsterSpawn_Stalker_Zone3_EMPTY");
            if (marker == null)
            {
                LogZone3SpawnFailureOnce("spawn-marker-missing");
                return;
            }

            if (!TryGetStalkerSpawnPosition(marker.transform, out var position))
            {
                LogZone3SpawnFailureOnce("spawn-marker-not-on-navmesh");
                return;
            }

            var spawned = SpawnStalker(runner, position, marker.transform.rotation, RegionSemanticZone.Zone03);
            if (!IsValidNetworkObject(spawned))
            {
                LogZone3SpawnFailureOnce("runner-spawn-returned-invalid-object");
                return;
            }

            _zone3MonsterInstance = spawned;
            _zone3MonsterSpawned = true;
            _zone3SpawnFailureLogged = false;

            if (IsValidNetworkObject(
                    _zone2MonsterInstance))
            {
                runner.Despawn(
                    _zone2MonsterInstance);

                _zone2MonsterInstance = null;
            }

            DespawnCreepMinionsForZone(
                runner,
                RegionSemanticZone.Zone02);

            _zone2MinionsActive = false;

            RuntimeLog.Log(
                RuntimeLogCategory.PlayerSpawner,
                $"[STK_ZONE3][SPAWN] " +
                $"id={spawned.Id} " +
                $"position={position}.");
        }

        private void LogZone3SpawnFailureOnce(string reason)
        {
            if (_zone3SpawnFailureLogged) return;
            _zone3SpawnFailureLogged = true;
            Debug.LogWarning($"[STK_ZONE3][SPAWN_REJECT] reason={reason}");
        }

        private NetworkObject ResolveCreepMinionPrefab()
        {
            if (_creepMinionPrefab != null) return _creepMinionPrefab;
            var asset = Resources.Load<GameObject>("PF_CreepMinionNetwork");
            if (asset != null) _creepMinionPrefab = asset.GetComponent<NetworkObject>();
            if (_creepMinionPrefab == null && !_missingMinionPrefabLogged)
            {
                _missingMinionPrefabLogged = true;
                Debug.LogWarning("[PlayerSpawner] PF_CreepMinionNetwork is missing; run the Creep Minion production setup menu.");
            }
            return _creepMinionPrefab;
        }

        private void MaintainCreepMinionPopulation(NetworkRunner runner)
        {
            PruneInvalidAuthoritativeWorldStateReferences();
            var difficulty = MatchAuthorityRuntime.Instance != null
                ? MatchAuthorityRuntime.Instance.Difficulty : MatchDifficulty.Normal;
            var profile = MatchDifficultyProfiles.Get(difficulty);
            var authority = MatchAuthorityRuntime.Instance;
            if (difficulty == MatchDifficulty.Normal && authority != null &&
                authority.TryGetMatchId(out var aedMatchId) &&
                AEDv2Authority.TryGetApplied(aedMatchId, out var plan, out _))
                profile = AEDv2GameplayBridge.ToNormalDifficultyProfile(plan);
            var zone = _zone3MonsterSpawned
                ? RegionSemanticZone.Zone03
                : (_zone2MinionsActive ? RegionSemanticZone.Zone02 : RegionSemanticZone.Zone01);
            int cap = zone == RegionSemanticZone.Zone01
                ? profile.Zone1MinionCap
                : zone == RegionSemanticZone.Zone02
                    ? profile.Zone2MinionCap
                    : profile.Zone3MinionCap;
            int count = 0;
            foreach (var obj in _creepMinionInstances)
            {
                if (!IsValidNetworkObject(obj)
                    || !obj.TryGetComponent<CreepMinionRuntime>(out var minion)
                    || minion.Zone != zone
                    || minion.IsDying)
                {
                    continue;
                }

                count++;
            }
            if (count >= cap
                || !TryGetCreepMinionSpawnAnchor(zone, out var stalker)
                || !TryFindCreepMinionSpawnPosition(runner, stalker, out var position)) return;

            SpawnCreepMinion(runner, position, zone);
        }

        private bool TryGetCreepMinionSpawnAnchor(
            RegionSemanticZone zone,
            out NetworkObject stalker)
        {
            stalker = zone == RegionSemanticZone.Zone03
                ? _zone3MonsterInstance
                : (zone == RegionSemanticZone.Zone02 ? _zone2MonsterInstance : _monsterInstance);

            return IsValidNetworkObject(stalker);
        }

        private bool TryFindCreepMinionSpawnPosition(
            NetworkRunner runner,
            NetworkObject stalker,
            out Vector3 position)
        {
            position = default;

            if (!IsValidNetworkObject(stalker)
                || !NavMesh.SamplePosition(
                    stalker.transform.position,
                    out var stalkerHit,
                    2f,
                    NavMesh.AllAreas))
            {
                return false;
            }

            if (TryFindCreepMinionSpawnPositionInRing(
                    runner,
                    stalkerHit.position,
                    _minionSpawnMinDistanceFromStalker,
                    _minionSpawnMaxDistanceFromStalker,
                    24,
                    out position))
            {
                return true;
            }

            return TryFindCreepMinionSpawnPositionInRing(
                runner,
                stalkerHit.position,
                _minionSpawnMaxDistanceFromStalker,
                _minionSpawnFallbackMaxDistanceFromStalker,
                48,
                out position);
        }

        private bool TryFindCreepMinionSpawnPositionInRing(
            NetworkRunner runner,
            Vector3 stalkerPosition,
            float minDistance,
            float maxDistance,
            int attempts,
            out Vector3 position)
        {
            position = default;

            var path = new NavMeshPath();

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                Vector2 direction = Random.insideUnitCircle;

                if (direction.sqrMagnitude < 0.01f)
                {
                    continue;
                }

                direction.Normalize();

                float distance = Random.Range(minDistance, maxDistance);

                Vector3 candidate =
                    stalkerPosition
                    + new Vector3(direction.x, 0f, direction.y)
                    * distance;

                if (!NavMesh.SamplePosition(candidate, out var hit, 2.5f, NavMesh.AllAreas))
                {
                    continue;
                }

                float actualDistance = Vector3.Distance(stalkerPosition, hit.position);

                if (actualDistance < minDistance || actualDistance > maxDistance)
                {
                    continue;
                }

                if (!NavMesh.CalculatePath(stalkerPosition, hit.position, NavMesh.AllAreas, path)
                    || path.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                if (!IsCreepMinionSpawnFarFromPlayers(runner, hit.position))
                {
                    continue;
                }

                position = hit.position;
                return true;
            }

            return false;
        }

        private bool IsCreepMinionSpawnFarFromPlayers(
            NetworkRunner runner,
            Vector3 position)
        {
            float minDistanceSqr =
                _minionSpawnMinDistanceFromPlayer
                * _minionSpawnMinDistanceFromPlayer;

            foreach (var player in runner.ActivePlayers)
            {
                if (!runner.TryGetPlayerObject(player, out var playerObject)
                    || !IsValidNetworkObject(playerObject)) continue;

                if (!playerObject.TryGetComponent<LobbyPlayerState>(out var lobby)
                    || !lobby.IsGameplayPlayer) continue;

                if (!playerObject.TryGetComponent<NetworkPlayerLifeState>(out var life)
                    || life.Status != NetworkPlayerLifeStatus.Alive) continue;

                float distanceSqr =
                    (playerObject.transform.position - position).sqrMagnitude;

                if (distanceSqr < minDistanceSqr) return false;
            }

            return true;
        }

        private NetworkObject SpawnCreepMinion(NetworkRunner runner, Vector3 position, RegionSemanticZone zone)
        {
            var prefab = ResolveCreepMinionPrefab();
            if (prefab == null) return null;
            var spawned = runner.Spawn(prefab, position, Quaternion.identity, PlayerRef.None,
                (_, obj) => obj.GetComponent<CreepMinionRuntime>()?.ConfigureBeforeSpawn(zone));
            if (!IsValidNetworkObject(spawned)) return null;
            if (!spawned.TryGetComponent<CreepMinionRuntime>(out _))
            {
                Debug.LogError("[PlayerSpawner] Creep Minion prefab has no CreepMinionRuntime.");
                runner.Despawn(spawned);
                return null;
            }
            _creepMinionInstances.Add(spawned);
            Debug.Log($"[PlayerSpawner] Spawned {zone} Creep Minion {spawned.Id} at {position}.");
            return spawned;
        }

        private void DespawnCreepMinionsForZone(NetworkRunner runner, RegionSemanticZone zone)
        {
            for (int i = _creepMinionInstances.Count - 1; i >= 0; i--)
            {
                var obj = _creepMinionInstances[i];
                if (!IsValidNetworkObject(obj)) { _creepMinionInstances.RemoveAt(i); continue; }
                var runtime = obj.GetComponent<CreepMinionRuntime>();
                if (runtime == null || runtime.Zone != zone) continue;
                runtime.ReleaseStolenCoreAuthoritative();
                runner.Despawn(obj);
                _creepMinionInstances.RemoveAt(i);
            }
        }

        private void DespawnAllCreepMinions(NetworkRunner runner)
        {
            foreach (var obj in _creepMinionInstances)
            {
                if (!IsValidNetworkObject(obj)) continue;
                obj.GetComponent<CreepMinionRuntime>()?.ReleaseStolenCoreAuthoritative();
                runner.Despawn(obj);
            }
            _creepMinionInstances.Clear();
        }

        private NetworkObject SpawnStalker(NetworkRunner runner, Vector3 position, Quaternion rotation, RegionSemanticZone zone)
        {
            var spawned = runner.Spawn(_monsterPrefab, position, rotation, PlayerRef.None,
                (_, obj) => obj.GetComponent<StalkerController>()?.ConfigurePatrolZone(zone));
            if (spawned != null)
            {
                RuntimeLog.Log(RuntimeLogCategory.PlayerSpawner,
                    $"[PlayerSpawner] Spawned {zone} Stalker {spawned.Id} at {position}.");
            }
            return spawned;
        }

        private static bool TryGetStalkerSpawnPosition(Transform marker, out Vector3 position)
        {
            position = default;
            if (marker != null && NavMesh.SamplePosition(marker.position, out var hit, 1f, NavMesh.AllAreas))
            {
                position = hit.position;
                return true;
            }

            Debug.LogWarning(
                $"[PlayerSpawner] Stalker spawn marker '{(marker != null ? marker.name : "missing")}' " +
                $"is not on NavMesh within 1m at {(marker != null ? marker.position.ToString() : "missing")}.");
            return false;
        }

        private void BindAuthoritativeWorldState()
        {
            RegisterExistingNetworkSectorBoxes();
            if (_matchStateInstance == null || _sectorBoxInstance == null)
            {
                return;
            }

            if (_matchStateInstance.TryGetComponent<NetworkMatchState>(out var matchState))
            {
                var doorId = _doorInstance != null ? _doorInstance.Id : default;
                matchState.InitializeAuthoritative(_sectorBoxInstance.Id, doorId);
            }
            if (_doorInstance != null && _doorInstance.TryGetComponent<NetworkDoor>(out var door))
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
            MatchAuthorityRuntime.Instance?.RegisterCoreObjectiveSlots(
                _sectorBoxInstances, TargetSectorBoxCount);
        }

        private void RegisterExistingNetworkSectorBoxes()
        {
            var boxes = FindObjectsByType<NetworkSectorBox>(FindObjectsInactive.Include);
            System.Array.Sort(boxes, (left, right) => string.CompareOrdinal(left.name, right.name));
            for (int i = 0; i < boxes.Length; i++)
            {
                var box = boxes[i];
                if (box == null || box.Object == null || !box.Object.IsValid)
                {
                    continue;
                }

                if (!_sectorBoxInstances.Contains(box.Object))
                {
                    _sectorBoxInstances.Add(box.Object);
                    RuntimeLog.Log(
                RuntimeLogCategory.PlayerSpawner,
                $"[PlayerSpawner] Registered existing Network Sector Box '{box.name}': {box.Object.Id}.");
                }
            }

            if (_sectorBoxInstance == null && _sectorBoxInstances.Count > 0)
            {
                _sectorBoxInstance = _sectorBoxInstances[0];
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
                RuntimeLog.Log(
                RuntimeLogCategory.PlayerSpawner,
                $"[PlayerSpawner] Spawned authoritative Power Puzzle {_powerPuzzleInstance.Id}.");
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
                Debug.LogWarning(
                    "[PlayerSpawner] No CoreSpawn_C* Energy Core spawn candidates found; " +
                    "using fallback Energy Core line spawn.");
                return;
            }

            var candidatesByRoom =
                new Dictionary<string, List<Transform>>(
                    System.StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (!TryGetEnergyCoreRoomKey(
                        candidate,
                        out var roomKey))
                {
                    continue;
                }

                if (!candidatesByRoom.TryGetValue(
                        roomKey,
                        out var roomCandidates))
                {
                    roomCandidates =
                        new List<Transform>();
                    candidatesByRoom.Add(
                        roomKey,
                        roomCandidates);
                }

                roomCandidates.Add(candidate);
            }

            if (candidatesByRoom.Count == 0)
            {
                Debug.LogWarning(
                    "[PlayerSpawner] Energy Core candidates exist but no valid room key was found; " +
                    "using fallback Energy Core line spawn.");
                return;
            }

            var roomKeys =
                new List<string>(
                    candidatesByRoom.Keys);
            roomKeys.Sort(
                System.StringComparer.Ordinal);

            for (int i = roomKeys.Count - 1; i > 0; i--)
            {
                int swapIndex =
                    Random.Range(
                        0,
                        i + 1);
                (roomKeys[i], roomKeys[swapIndex]) =
                    (roomKeys[swapIndex], roomKeys[i]);
            }

            int selectedRoomCount =
                Mathf.Min(
                    _energyCoreCount,
                    roomKeys.Count);

            for (int i = 0; i < selectedRoomCount; i++)
            {
                string roomKey =
                    roomKeys[i];
                var roomCandidates =
                    candidatesByRoom[roomKey];
                int pointIndex =
                    Random.Range(
                        0,
                        roomCandidates.Count);
                var candidate =
                    roomCandidates[pointIndex];

                _selectedEnergyCoreSpawnPoses.Add(
                    new SpawnPose(
                        candidate.position,
                        candidate.rotation));
                RuntimeLog.Log(
                    RuntimeLogCategory.PlayerSpawner,
                    $"[PlayerSpawner] Selected Energy Core room={roomKey} " +
                    $"point='{candidate.name}' at {candidate.position}.");
            }

            RuntimeLog.Log(
                RuntimeLogCategory.PlayerSpawner,
                $"[PlayerSpawner] Selected " +
                $"{_selectedEnergyCoreSpawnPoses.Count}/{candidatesByRoom.Count} " +
                $"Energy Core rooms from {candidates.Count} authored points.");
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

        private static bool IsEnergyCoreSpawnCandidate(
            Transform candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            string name =
                candidate.name;

            return name.StartsWith(
                    "CoreSpawn_C",
                    System.StringComparison.OrdinalIgnoreCase)
                && name.IndexOf(
                    "_EMPTY",
                    System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryGetEnergyCoreRoomKey(
            Transform candidate,
            out string roomKey)
        {
            roomKey = null;

            if (!IsEnergyCoreSpawnCandidate(
                    candidate))
            {
                return false;
            }

            const string prefix =
                "CoreSpawn_C";
            string name =
                candidate.name;
            int roomNumberStart =
                prefix.Length;
            int roomNumberEnd =
                name.IndexOf(
                    '_',
                    roomNumberStart);

            if (roomNumberEnd <= roomNumberStart)
            {
                return false;
            }

            for (int i = roomNumberStart; i < roomNumberEnd; i++)
            {
                if (!char.IsDigit(name[i]))
                {
                    return false;
                }
            }

            roomKey =
                "C" + name.Substring(
                    roomNumberStart,
                    roomNumberEnd - roomNumberStart);
            return true;
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
                foreach (var sectorCollider in legacySector.GetComponentsInChildren<Collider>(true))
                {
                    sectorCollider.enabled = false;
                }

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
                if (legacyStation.StationType == PowerPuzzleStationType.PowerControl)
                {
                    continue;
                }

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

            if (gameplay && playerObject.TryGetComponent<LobbyPlayerState>(out var existingState)
                && existingState.IsGameplayPlayer) return;

            var slot = GetOrAssignSlot(player);
            var pose = gameplay ? GetGameplaySpawnPose(slot) : GetLobbySpawnPose(slot);
            if (playerObject.TryGetComponent<LobbyPlayerState>(out var state))
            {
                // Purchasable Team Tools selected in Lobby carry into gameplay.
// First Aid is world-pickup-only and must never be a starting loadout.
var enteringGameplay =
    gameplay && !state.IsGameplayPlayer;

var toolId =
    enteringGameplay
    && state.ToolId ==
        LobbyPlayerState.FirstAidKitToolId
        ? 0
        : state.ToolId;
                var teamId = state.TeamId > 0 ? state.TeamId : slot;
                state.InitializeAuthoritativeSelection(teamId, toolId, gameplay);
            }

            if (playerObject.TryGetComponent<NetworkPlayerLifeState>(out var lifeState) && playerObject.HasStateAuthority)
            {
                lifeState.ResetForMatchAuthoritative();
            }

            if (playerObject.TryGetComponent<NetworkPlayerInteractor>(out var interactor) && playerObject.HasStateAuthority && gameplay)
            {
                interactor.ResetForMatchAuthoritative();
            }

            playerObject.TryGetComponent<NetworkPlayerMovement>(out var movement);
            if (movement != null && playerObject.HasStateAuthority)
            {
                movement.IsHidden = false;
                movement.CurrentHideSpotId = 0UL;
            }

            if (playerObject.TryGetComponent<PlayerHidingController>(out var hiding) && hiding.IsHidden)
            {
                hiding.ExitHiding();
            }

            if (!TryTeleportExistingPlayer(playerObject, pose, gameplay))
            {
                Debug.LogWarning($"[PlayerSpawner] Could not teleport lifecycle-owned player object for {player}; object={playerObject.Id}.");
            }
            else if (gameplay && movement != null && playerObject.HasStateAuthority)
            {
                movement.ApplyGameplaySpawnViewAuthoritative(pose.Rotation);
            }

            RuntimeLog.Log(
                RuntimeLogCategory.PlayerSpawner,

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
                ClearAuthoritativeWorldStateReferences();
            }
        }

        private void ClearAuthoritativeWorldStateReferences()
        {
            _creepMinionInstances.Clear();
            _zone2MinionsActive = false;

            _nextMinionSpawnCheckAt = 0f;
            _missingMinionPrefabLogged = false;
            _doorInstance = null;
            _energyCoreInstances.Clear();
            _selectedEnergyCoreSpawnPoses.Clear();
            _sectorBoxInstance = null;
            _sectorBoxInstances.Clear();
            _matchStateInstance = null;
            _powerPuzzleInstance = null;
            _powerPuzzleStationInstances.Clear();
            _monsterInstance = null;
            _zone2MonsterInstance = null;
            _zone2MonsterSpawned = false;
            _zone3MonsterInstance = null;
            _zone3MonsterSpawned = false;
            _zone3SpawnFailureLogged = false;
        }

        private void PruneInvalidAuthoritativeWorldStateReferences()
        {
            if (!IsValidNetworkObject(_doorInstance)) _doorInstance = null;
            if (!IsValidNetworkObject(_sectorBoxInstance)) _sectorBoxInstance = null;
            if (!IsValidNetworkObject(_matchStateInstance)) _matchStateInstance = null;
            if (!IsValidNetworkObject(_powerPuzzleInstance)) _powerPuzzleInstance = null;
            if (!IsValidNetworkObject(_monsterInstance)) _monsterInstance = null;
            if (!IsValidNetworkObject(_zone2MonsterInstance)) _zone2MonsterInstance = null;
            if (!IsValidNetworkObject(_zone3MonsterInstance)) _zone3MonsterInstance = null;

            _energyCoreInstances.RemoveAll(core => !IsValidNetworkObject(core));
            _sectorBoxInstances.RemoveAll(box => !IsValidNetworkObject(box));
            _powerPuzzleStationInstances.RemoveAll(station => !IsValidNetworkObject(station));
            _creepMinionInstances.RemoveAll(obj => !IsValidNetworkObject(obj));
        }

        private static bool IsValidNetworkObject(NetworkObject obj)
        {
            return obj != null && obj.IsValid;
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
