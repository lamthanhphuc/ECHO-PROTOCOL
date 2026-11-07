using System;
using System.Collections.Generic;
using System.Linq;

using EchoProtocol.AI.AED;
using EchoProtocol.AI.Common.AED;
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
using Random = UnityEngine.Random;

namespace EchoProtocol.Networking
{
    // Shared by authoritative placement and the lobby presentation lights.
    public static class LobbyLineupLayout
    {
        public const float Spacing = 1.4f;
        public static Vector3 Position(int slot) => new Vector3(-3.35f + Mathf.Clamp(slot,0,3)*Spacing,1f,-5.5f);
    }

    /// <summary>Host-authoritative gameplay placement coordinator for lifecycle-owned player objects.</summary>
    public sealed class PlayerSpawner : MonoBehaviour
    {
        private const int SupportedPlayerCount = 4;
        private const int TargetSectorBoxCount = 2;
        private const float FallbackSpacing = 2.5f;

        [SerializeField] private NetworkBootstrap _bootstrap;
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
        [SerializeField] private NetworkObject _map2LizardPrefab;
        [SerializeField] private RegionGraphAsset _map2RegionGraph;
        [SerializeField] private RegionGraphAsset _map2HuntRegionGraph;

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

            // Map 2 - Flooded Grounds: spawn Creep Minion
            // using the existing Zone01 minion behaviour.
            if (SceneManager.GetActiveScene().name ==
                LobbyMapCatalog.ExplorationScene)
            {
                if (runner == null ||
                    !runner.IsServer ||
                    !runner.IsRunning ||
                    _bootstrap.State != NetworkSessionState.InMatch)
                    return;

                PruneInvalidAuthoritativeWorldStateReferences();

                if (!IsValidNetworkObject(_monsterInstance))
                    return;

                if (Time.time < _nextMinionSpawnCheckAt)
                    return;

                _nextMinionSpawnCheckAt =
                    Time.time + _minionSpawnCheckInterval;

                _zone2MinionsActive = false;
                MaintainCreepMinionPopulation(runner);
                return;
            }

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
                || (!AlienHuntMission.Instance && SceneManager.GetActiveScene().name != LobbyManager.GameSceneName)) return;

            EnsureZone3Stalker(runner, matchState);

            var phase = matchState.CurrentPhase;
            if (((phase == NetworkMatchPhase.Zone3FindFrigate || phase == NetworkMatchPhase.Zone3PushFrigate)
                 && !HasAdaptiveV02Plan() && RemoteFixedBalanceSnapshot.GameplayValue("minionCapZone3", -1f) < 0f)
                || phase == NetworkMatchPhase.FinalHunt
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
                + (AlienHuntMission.Instance ? 1f : _minionSpawnCheckInterval);

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
            if (_bootstrap.Runner != null && _bootstrap.Runner.IsRunning
                && SceneManager.GetActiveScene().name == NetworkBootstrap.LobbySceneName)
                HandleNetworkSceneLoadDone(_bootstrap.Runner);
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

            var gameplay = LobbyMapCatalog.IsGameplayScene(SceneManager.GetActiveScene().name);
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
            if (NetworkBootstrap.IsHostMigrationReconciliationPending)
            {
                TryAttachLifecycle(runner);

                _spawnSlots.Clear();
                foreach (var player in runner.ActivePlayers)
                {
                    GetOrAssignSlot(player);
                }

                // Fusion may signal SceneLoadDone BEFORE it invokes the
                // snapshot resume callback. That callback performs the
                // authoritative world adoption after all snapshot objects
                // have been spawned; NEVER spawn or adopt the world here.
                RuntimeLog.Log(
                    RuntimeLogCategory.PlayerSpawner,
                    "PSP|MIGRATION_SCENE_READY_DEFERRED|awaiting_snapshot_restore");
                return;
            }
            if (!LobbyMapCatalog.IsGameplayScene(SceneManager.GetActiveScene().name))
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

            // A delayed scene-ready callback after a Runner handoff must
            // reuse an already-restored MatchState. Spawning a second one
            // makes the next Host migration fail as duplicate authority.
            var existingMatchStates =
                FindObjectsByType<NetworkMatchState>(
                    FindObjectsInactive.Include);

            foreach (var existing in existingMatchStates)
            {
                if (existing == null
                    || existing.Runner != runner
                    || existing.Object == null
                    || !existing.Object.IsValid
                    || !existing.Object.HasStateAuthority)
                {
                    continue;
                }

                RuntimeLog.Log(
                    RuntimeLogCategory.PlayerSpawner,
                    $"PSP|WORLD_STATE_ALREADY_EXISTS|object={existing.Object.Id}|adopting_without_respawn");

                if (!AdoptMigratedAuthoritativeWorldState(runner))
                {
                    Debug.LogError(
                        "[PlayerSpawner] Existing authoritative world state could not be adopted. Not spawning another NetworkMatchState.");
                }

                return;
            }

            EnsureWorldStateExamples(runner);
        }

        public bool AdoptMigratedAuthoritativeWorldState(
            NetworkRunner runner)
        {
            if (runner == null
                || !runner.IsRunning
                || !runner.IsServer
                || SceneManager.GetActiveScene().name
                    != LobbyManager.GameSceneName)
            {
                return false;
            }

            ClearAuthoritativeWorldStateReferences();

            NetworkMatchState migratedMatch = null;

            var matchStates =
                FindObjectsByType<NetworkMatchState>(
                    FindObjectsInactive.Include);

            for (int i = 0; i < matchStates.Length; i++)
            {
                var candidate = matchStates[i];

                if (candidate == null
                    || candidate.Runner != runner
                    || candidate.Object == null
                    || !candidate.Object.IsValid
                    || !candidate.Object.HasStateAuthority)
                {
                    continue;
                }

                if (migratedMatch != null
                    && migratedMatch != candidate)
                {
                    Debug.LogError(
                        $"[PlayerSpawner] Host Migration found multiple authoritative NetworkMatchState objects on the same Runner. First={migratedMatch.Object.Id}, Conflicting={candidate.Object.Id}. Refusing to discard either state silently.");
                    return false;
                }

                migratedMatch = candidate;
            }

            if (migratedMatch == null)
            {
                Debug.LogError("[PlayerSpawner] Host Migration restored no authoritative NetworkMatchState.");
                return false;
            }

            _matchStateInstance =
                migratedMatch.Object;


            // Restore SectorBox references from the replicated objective
            // source first, then retain every other valid box.
            var sectorBoxes =
                FindObjectsByType<NetworkSectorBox>(
                    FindObjectsInactive.Include);

            System.Array.Sort(
                sectorBoxes,
                (left, right) =>
                    string.CompareOrdinal(
                        left != null ? left.name : string.Empty,
                        right != null ? right.name : string.Empty));

            for (int i = 0; i < sectorBoxes.Length; i++)
            {
                var box = sectorBoxes[i];

                if (box == null
                    || box.Object == null
                    || !box.Object.IsValid
                    || !box.Object.HasStateAuthority)
                {
                    continue;
                }

                _sectorBoxInstances.Add(box.Object);
            }

            if (migratedMatch.ObjectiveSourceId.IsValid
                && runner.TryFindObject(
                    migratedMatch.ObjectiveSourceId,
                    out var objectiveObject)
                && objectiveObject != null
                && objectiveObject.IsValid
                && objectiveObject.TryGetComponent<NetworkSectorBox>(
                    out _))
            {
                _sectorBoxInstance =
                    objectiveObject;
            }
            else if (_sectorBoxInstances.Count > 0)
            {
                _sectorBoxInstance =
                    _sectorBoxInstances[0];
            }


            // EscapeDoorId is already replicated by NetworkMatchState.
            if (migratedMatch.EscapeDoorId.IsValid
                && runner.TryFindObject(
                    migratedMatch.EscapeDoorId,
                    out var escapeDoorObject)
                && escapeDoorObject != null
                && escapeDoorObject.IsValid
                && escapeDoorObject.TryGetComponent<NetworkDoor>(
                    out _))
            {
                _doorInstance =
                    escapeDoorObject;
            }


            var puzzles =
                FindObjectsByType<NetworkPowerPuzzle>(
                    FindObjectsInactive.Include);

            for (int i = 0; i < puzzles.Length; i++)
            {
                var puzzle = puzzles[i];

                if (puzzle != null
                    && puzzle.Object != null
                    && puzzle.Object.IsValid
                    && puzzle.Object.HasStateAuthority)
                {
                    _powerPuzzleInstance =
                        puzzle.Object;
                    break;
                }
            }


            var stations =
                FindObjectsByType<NetworkPowerPuzzleStation>(
                    FindObjectsInactive.Include);

            System.Array.Sort(
                stations,
                (left, right) =>
                    (left != null ? left.InputId : int.MaxValue)
                        .CompareTo(
                            right != null
                                ? right.InputId
                                : int.MaxValue));

            for (int i = 0; i < stations.Length; i++)
            {
                var station = stations[i];

                if (station != null
                    && station.Object != null
                    && station.Object.IsValid
                    && station.Object.HasStateAuthority)
                {
                    _powerPuzzleStationInstances.Add(
                        station.Object);
                }
            }


            // NetworkPickupItem is the authoritative Energy Core runtime
            // object in this flow. Preserve carried/placed cores as-is.
            var cores =
                FindObjectsByType<NetworkPickupItem>(
                    FindObjectsInactive.Include);

            System.Array.Sort(
                cores,
                (left, right) =>
                    string.CompareOrdinal(
                        left != null ? left.name : string.Empty,
                        right != null ? right.name : string.Empty));

            for (int i = 0; i < cores.Length; i++)
            {
                var core = cores[i];

                if (core != null
                    && core.Object != null
                    && core.Object.IsValid
                    && core.Object.HasStateAuthority)
                {
                    _energyCoreInstances.Add(
                        core.Object);
                }
            }


            // Scene-placed NetworkSectorBox objects can receive new NetworkIds
            // after a scene reload. The snapshot still contains old IDs in
            // ObjectiveSourceId and in previously placed Energy Cores.
            if (!RestoreMigratedSectorReferences(runner, migratedMatch))
            {
                Debug.LogError("P04N_CORE_SECTOR_RESTORE_FAILED|refusing_partial_migration_world");
                return false;
            }

            // Never silently accept a missing, duplicate, or origin-reset
            // Core snapshot as a successfully restored authoritative world.
            if (!ValidateMigratedCoreState(migratedMatch))
            {
                return false;
            }

            var minions =
                FindObjectsByType<CreepMinionRuntime>(
                    FindObjectsInactive.Include);

            for (int i = 0; i < minions.Length; i++)
            {
                var minion = minions[i];

                if (minion != null
                    && minion.Object != null
                    && minion.Object.IsValid
                    && minion.Object.HasStateAuthority)
                {
                    _creepMinionInstances.Add(
                        minion.Object);
                }
            }


            // Zone transitions despawn the previous Stalker. Therefore
            // the surviving Stalker can be reattached from match phase
            // without inventing a new runtime identity.
            NetworkObject migratedStalker = null;

            var stalkers =
                FindObjectsByType<StalkerController>(
                    FindObjectsInactive.Include);

            for (int i = 0; i < stalkers.Length; i++)
            {
                var stalker = stalkers[i];
                var stalkerObject =
                    stalker != null
                        ? stalker.GetComponentInParent<NetworkObject>()
                        : null;

                if (!IsValidNetworkObject(stalkerObject)
                    || !stalkerObject.HasStateAuthority)
                {
                    continue;
                }

                if (migratedStalker == null)
                {
                    migratedStalker =
                        stalkerObject;
                }
            }

            if (migratedStalker != null)
            {
                switch (migratedMatch.CurrentPhase)
                {
                    case NetworkMatchPhase.CoreObjective:
                        _monsterInstance =
                            migratedStalker;
                        break;

                    case NetworkMatchPhase.Zone2Objective:
                    case NetworkMatchPhase.Puzzle:
                    case NetworkMatchPhase.SecurityHold:
                        _zone2MonsterInstance =
                            migratedStalker;
                        _zone2MonsterSpawned = true;
                        break;

                    default:
                        _zone3MonsterInstance =
                            migratedStalker;
                        _zone3MonsterSpawned = true;
                        break;
                }
            }

            _zone2MinionsActive =
                migratedMatch.CurrentPhase ==
                    NetworkMatchPhase.Zone2Objective
                || migratedMatch.CurrentPhase ==
                    NetworkMatchPhase.Puzzle
                || migratedMatch.CurrentPhase ==
                    NetworkMatchPhase.SecurityHold;

            _zone3SpawnFailureLogged = false;
            _nextMinionSpawnCheckAt =
                Time.time + _minionSpawnCheckInterval;

            migratedMatch
                .ReconcileAfterHostMigrationAuthoritative();

            RuntimeLog.Log(
                RuntimeLogCategory.PlayerSpawner,
                $"PSP|MIGRATION_WORLD_ADOPT|match={_matchStateInstance.Id}|phase={migratedMatch.CurrentPhase}|cores={_energyCoreInstances.Count}|sectorBoxes={_sectorBoxInstances.Count}|stations={_powerPuzzleStationInstances.Count}|minions={_creepMinionInstances.Count}|stalker={(migratedStalker != null ? migratedStalker.Id.ToString() : "none")}");

            LogMigrationGameplayAudit(runner, migratedMatch);
            return true;
        }

        // Restore the references, not the Core objects. Match scene-local
        // SectorBoxes to already-placed Core sockets with a strict distance
        // bound and a one-to-one map from old box ID to new box instance.
        private bool RestoreMigratedSectorReferences(NetworkRunner runner, NetworkMatchState match)
        {
            if (runner == null || match == null || match.Object == null
                || !match.Object.IsValid || !match.Object.HasStateAuthority
                || _sectorBoxInstances.Count == 0)
            {
                return false;
            }

            var boxes = new List<NetworkSectorBox>();
            foreach (var boxObject in _sectorBoxInstances)
            {
                if (boxObject == null || !boxObject.IsValid
                    || !boxObject.HasStateAuthority
                    || !boxObject.TryGetComponent<NetworkSectorBox>(out var box))
                {
                    return false;
                }
                boxes.Add(box);
            }

            var placements = new List<(NetworkPickupItem Core, NetworkSectorBox Box)>();
            var oldToNew = new Dictionary<NetworkId, NetworkSectorBox>();
            var occupiedSlots = new HashSet<string>();
            var expectedCounts = new int[boxes.Count];

            foreach (var coreObject in _energyCoreInstances)
            {
                if (coreObject == null || !coreObject.IsValid
                    || !coreObject.TryGetComponent<NetworkPickupItem>(out var core))
                {
                    return false;
                }
                if (core.State != NetworkItemState.Placed)
                {
                    continue;
                }
                if (!core.PlacedSectorId.IsValid || core.PlacementSlot < 0)
                {
                    Debug.LogError($"P04N_CORE_BAD_PLACEMENT|core={coreObject.Id}|sector={core.PlacedSectorId}|slot={core.PlacementSlot}");
                    return false;
                }

                int bestIndex = -1;
                float bestSquaredDistance = float.MaxValue;
                float secondSquaredDistance = float.MaxValue;
                for (int i = 0; i < boxes.Count; i++)
                {
                    if (!boxes[i].TryGetPlacementPoseForMigration(core.PlacementSlot, out var socketPosition))
                    {
                        continue;
                    }
                    float sqrDist = (core.WorldPosition - socketPosition).sqrMagnitude;
                    if (sqrDist < bestSquaredDistance)
                    {
                        secondSquaredDistance = bestSquaredDistance;
                        bestSquaredDistance = sqrDist;
                        bestIndex = i;
                    }
                    else if (sqrDist < secondSquaredDistance)
                    {
                        secondSquaredDistance = sqrDist;
                    }
                }

                // WorldPosition is the exact socket pose saved by TryPlace.
                // A mismatch greater than 1m, or two equally good candidates,
                // indicates we cannot safely infer the old scene-object ID.
                if (bestIndex < 0 || bestSquaredDistance > 1.0f
                    || secondSquaredDistance - bestSquaredDistance < 0.01f)
                {
                    Debug.LogError($"P04N_CORE_SOCKET_UNRESOLVED|core={coreObject.Id}|oldSector={core.PlacedSectorId}|slot={core.PlacementSlot}|bestSqr={bestSquaredDistance}|secondSqr={secondSquaredDistance}");
                    return false;
                }

                var resolvedBox = boxes[bestIndex];
                if (oldToNew.TryGetValue(core.PlacedSectorId, out var earlierBox)
                    && earlierBox != resolvedBox)
                {
                    Debug.LogError($"P04N_CORE_SECTOR_CONFLICT|oldSector={core.PlacedSectorId}|first={earlierBox.Object.Id}|other={resolvedBox.Object.Id}");
                    return false;
                }
                oldToNew[core.PlacedSectorId] = resolvedBox;

                string slotKey = bestIndex + ":" + core.PlacementSlot;
                if (!occupiedSlots.Add(slotKey))
                {
                    Debug.LogError($"P04N_CORE_DUPLICATE_SLOT|core={coreObject.Id}|slot={slotKey}");
                    return false;
                }
                placements.Add((core, resolvedBox));
                expectedCounts[bestIndex] = Mathf.Max(expectedCounts[bestIndex], core.PlacementSlot + 1);
            }

            // Placement slots must be contiguous: an orphaned slot is not an
            // acceptable substitute for a restored Core count.
            for (int i = 0; i < boxes.Count; i++)
            {
                for (int slot = 0; slot < expectedCounts[i]; slot++)
                {
                    if (!occupiedSlots.Contains(i + ":" + slot))
                    {
                        Debug.LogError($"P04N_CORE_SLOT_GAP|sector={boxes[i].Object.Id}|slot={slot}");
                        return false;
                    }
                }
            }

            NetworkSectorBox objectiveBox = null;
            var previousObjectiveId = match.ObjectiveSourceId;
            if (previousObjectiveId.IsValid
                && runner.TryFindObject(previousObjectiveId, out var currentObjective)
                && currentObjective != null && currentObjective.IsValid
                && currentObjective.TryGetComponent<NetworkSectorBox>(out var currentObjectiveBox)
                && boxes.Contains(currentObjectiveBox))
            {
                objectiveBox = currentObjectiveBox;
            }
            else if (previousObjectiveId.IsValid
                     && oldToNew.TryGetValue(previousObjectiveId, out var mappedObjective))
            {
                objectiveBox = mappedObjective;
            }
            else
            {
                // The normal fresh-match objective source is the first
                // scene box ordered by name (see RegisterExistingNetworkSectorBoxes).
                // If no placed Core identifies the old source, use that same
                // deterministic scene ordering instead of an arbitrary ID.
                objectiveBox = boxes[0];
            }

            if (objectiveBox == null) return false;

            for (int i = 0; i < boxes.Count; i++)
            {
                if (!boxes[i].TryRestoreMigratedPlacementStateAuthoritative(
                        match.Object.Id, expectedCounts[i]))
                {
                    Debug.LogError($"P04N_SECTOR_RESTORE_REJECT|sector={boxes[i].Object.Id}|current={boxes[i].PlacedCoreCount}|expected={expectedCounts[i]}");
                    return false;
                }
            }

            foreach (var placement in placements)
            {
                if (!placement.Core.TryRebindPlacedSectorAfterMigration(
                        placement.Core.PlacedSectorId, placement.Box.Object.Id))
                {
                    Debug.LogError($"P04N_CORE_REBIND_REJECT|core={placement.Core.Object.Id}|sector={placement.Box.Object.Id}");
                    return false;
                }
            }

            // InitializeAuthoritative only changes ObjectiveSourceId and
            // EscapeDoorId; it does NOT reset match status or mission progress.
            if (match.ObjectiveSourceId != objectiveBox.Object.Id)
            {
                match.InitializeAuthoritative(objectiveBox.Object.Id, match.EscapeDoorId);
            }
            _sectorBoxInstance = objectiveBox.Object;

            // Final authoritative guard: all restored placements and the
            // objective source must point to live scene objects and MatchState.
            if (match.ObjectiveSourceId != objectiveBox.Object.Id)
            {
                return false;
            }
            foreach (var box in boxes)
            {
                if (box.MatchStateId != match.Object.Id) return false;
            }
            foreach (var placement in placements)
            {
                if (placement.Core.PlacedSectorId != placement.Box.Object.Id) return false;
            }

            RuntimeLog.Log(RuntimeLogCategory.PlayerSpawner,
                $"P04N_CORE_SECTOR_REBOUND|match={match.Object.Id}|objective={objectiveBox.Object.Id}|boxes={boxes.Count}|placed={placements.Count}|mappedOldSectors={oldToNew.Count}|result=PASS");
            return true;
        }

        // Read-only migration invariant. Never spawn replacement Cores or
        // teleport invalid snapshots; the session must fail closed instead.
        private bool ValidateMigratedCoreState(NetworkMatchState match)
        {
            var expected = Mathf.Clamp(_energyCoreCount, 1, 4);
            var count = _energyCoreInstances.Count;
            var countValid = count <= expected
                && (match.CurrentPhase != NetworkMatchPhase.CoreObjective
                    || count == expected);
            var seen = new HashSet<NetworkId>();
            var badIdentity = 0;
            var badPose = 0;

            foreach (var coreObject in _energyCoreInstances)
            {
                if (!IsValidNetworkObject(coreObject)
                    || !seen.Add(coreObject.Id)
                    || !coreObject.TryGetComponent<NetworkPickupItem>(out var item))
                {
                    badIdentity++;
                    continue;
                }

                if (item.State == NetworkItemState.Carried)
                {
                    // Carried Cores follow their verified holder. The last
                    // world pose may be unavailable until the holder reconnects.
                    continue;
                }

                var pose = item.WorldPosition;
                var rotation = item.WorldRotation;
                var missingRotation = rotation.x == 0f && rotation.y == 0f
                    && rotation.z == 0f && rotation.w == 0f;
                var missingAvailableSpawn = item.State == NetworkItemState.Available
                    && pose == Vector3.zero;
                if (missingRotation || missingAvailableSpawn)
                {
                    badPose++;
                    Debug.LogError(
                        $"HM_CORE_RESTORE_BAD_POSE|core={coreObject.Id}|state={item.State}|worldPos={pose}|worldRot={rotation}|transformPos={coreObject.transform.position}");
                }
            }

            var valid = countValid && badIdentity == 0 && badPose == 0;
            RuntimeLog.Log(RuntimeLogCategory.Inventory,
                $"HM_CORE_RESTORE_INVARIANT|phase={match.CurrentPhase}|expected={expected}|actual={count}|badIdentity={badIdentity}|badPose={badPose}|result={(valid ? "PASS" : "FAIL")}");
            if (!valid)
            {
                Debug.LogError(
                    $"Host Migration Core snapshot failed integrity checks: phase={match.CurrentPhase}, count={count}/{expected}, badIdentity={badIdentity}, badPose={badPose}. Refusing to adopt a corrupted world.");
            }

            return valid;
        }

        // Diagnostic-only: no gameplay state is written by this method.
        // Called once after world adoption and migration reconciliation.
        private void LogMigrationGameplayAudit(NetworkRunner runner, NetworkMatchState match)
        {
            if (runner == null || match == null
                || !RuntimeLog.IsEnabled(RuntimeLogCategory.PlayerSpawner))
            {
                return;
            }

            var unresolved = 0;
            var carriedCoreCount = 0;
            var playerCount = 0;
            var players = FindObjectsByType<LobbyPlayerState>(FindObjectsInactive.Include);

            foreach (var player in players)
            {
                if (player == null || player.Runner != runner
                    || player.Object == null || !player.Object.IsValid
                    || !player.Object.HasStateAuthority)
                {
                    continue;
                }

                playerCount++;
                var playerObject = player.Object;
                var hasLife = player.TryGetComponent<NetworkPlayerLifeState>(out var life)
                    && life != null && life.Object != null && life.Object.IsValid;
                RuntimeLog.Log(RuntimeLogCategory.PlayerSpawner,
                    $"HM_AUDIT_PLAYER|id={playerObject.Id}|authority={playerObject.InputAuthority}|disconnected={player.Disconnected}|pos={player.transform.position}|coreId={player.CarriedCoreId}|tool={player.ToolId}|toolUses={player.TeamToolUsesRemaining}|health={(hasLife ? life.Health.ToString("F1") : "NA")}|life={(hasLife ? life.Status.ToString() : "NA")}|reviver={(hasLife ? life.Reviver.ToString() : "NA")}");

                if (player.CarriedCoreId.IsValid)
                {
                    NetworkObject ownedCore;
                    if (!runner.TryFindObject(player.CarriedCoreId, out ownedCore)
                        || ownedCore == null || !ownedCore.IsValid
                        || !ownedCore.TryGetComponent<NetworkPickupItem>(out var core)
                        || core.State != NetworkItemState.Carried)
                    {
                        unresolved++;
                        Debug.LogWarning($"HM_AUDIT_REF_UNRESOLVED|kind=PlayerCarriedCore|player={playerObject.Id}|core={player.CarriedCoreId}|note=may_need_reconnect_mapping");
                    }
                }
            }

            foreach (var coreObject in _energyCoreInstances)
            {
                if (coreObject == null || !coreObject.IsValid
                    || !coreObject.TryGetComponent<NetworkPickupItem>(out var item))
                {
                    continue;
                }

                if (item.State == NetworkItemState.Carried)
                {
                    carriedCoreCount++;
                }

                RuntimeLog.Log(RuntimeLogCategory.Inventory,
                    $"HM_AUDIT_CORE|id={coreObject.Id}|state={item.State}|holder={item.Holder}|monster={item.MonsterCarrierId}|sector={item.PlacedSectorId}|slot={item.PlacementSlot}|ordinal={item.TransitionOrdinal}|pos={item.WorldPosition}");

                if (item.PlacedSectorId.IsValid)
                {
                    NetworkObject sector;
                    if (!runner.TryFindObject(item.PlacedSectorId, out sector)
                        || sector == null || !sector.IsValid
                        || !sector.TryGetComponent<NetworkSectorBox>(out _))
                    {
                        unresolved++;
                        Debug.LogWarning($"HM_AUDIT_REF_UNRESOLVED|kind=CoreSector|core={coreObject.Id}|sector={item.PlacedSectorId}");
                    }
                }
            }

            if (match.ObjectiveSourceId.IsValid)
            {
                NetworkObject objective;
                if (!runner.TryFindObject(match.ObjectiveSourceId, out objective)
                    || objective == null || !objective.IsValid
                    || !objective.TryGetComponent<NetworkSectorBox>(out _))
                {
                    unresolved++;
                    Debug.LogWarning($"HM_AUDIT_REF_UNRESOLVED|kind=ObjectiveSource|id={match.ObjectiveSourceId}");
                }
            }

            if (match.EscapeDoorId.IsValid)
            {
                NetworkObject exitDoor;
                if (!runner.TryFindObject(match.EscapeDoorId, out exitDoor)
                    || exitDoor == null || !exitDoor.IsValid
                    || !exitDoor.TryGetComponent<NetworkDoor>(out _))
                {
                    unresolved++;
                    Debug.LogWarning($"HM_AUDIT_REF_UNRESOLVED|kind=EscapeDoor|id={match.EscapeDoorId}");
                }
            }

            var slidingDoors = FindObjectsByType<NetworkSlidingDoor>(FindObjectsInactive.Include);
            foreach (var door in slidingDoors)
            {
                if (door == null || door.Runner != runner
                    || door.Object == null || !door.Object.IsValid
                    || !door.ActiveJammerId.IsValid)
                {
                    continue;
                }

                NetworkObject jammer;
                if (!runner.TryFindObject(door.ActiveJammerId, out jammer)
                    || jammer == null || !jammer.IsValid
                    || !jammer.TryGetComponent<NetworkDoorJammer>(out _))
                {
                    unresolved++;
                    Debug.LogWarning($"HM_AUDIT_REF_UNRESOLVED|kind=DoorJammer|door={door.Object.Id}|jammer={door.ActiveJammerId}");
                }
            }

            RuntimeLog.Log(RuntimeLogCategory.MatchState,
                $"HM_AUDIT_MATCH|id={match.Object.Id}|status={match.Status}|phase={match.CurrentPhase}|phaseOrdinal={match.PhaseOrdinal}|relayMask={match.RelayCompletionMask}|securityStage={match.Zone2Stage}|securityCompleted={match.SecurityHoldCompleted}|securityParticipants={match.SecurityHoldParticipantCount}|puzzleCompleted={match.PowerPuzzleCompleted}|matchSeconds={match.MatchRemainingSeconds:F1}|frigateInitialized={match.Zone3ConvoyInitialized}|frigateFuel={match.Zone3FuelPointsRemaining}|frigateCurrentPoint={match.Zone3ConvoyCurrentPoint}|frigateTargetPoint={match.Zone3ConvoyTargetPoint}|frigateWaiting={match.Zone3ConvoyWaitingForRoute}|frigatePos={match.Zone3FrigatePosition}|zone3Pusher={match.Zone3Pusher}|refuel={match.Zone3RefuelOperator}|charge={match.Zone3ChargeOperator}|chargeProgress={match.Zone3ChargeAccumulatedSeconds:F1}");

            var zone3 = Zone3MissionDirector.Instance;
            if (zone3 != null && zone3.Convoy != null && match.Zone3ConvoyInitialized)
            {
                RuntimeLog.Log(RuntimeLogCategory.MatchState,
                    $"HM_AUDIT_ZONE3|replicatedPose={match.Zone3FrigatePosition}|scenePose={zone3.Convoy.transform.position}|poseDelta={Vector3.Distance(match.Zone3FrigatePosition, zone3.Convoy.transform.position):F2}|sceneFuel={zone3.Convoy.FuelPointsRemaining}|networkFuel={match.Zone3FuelPointsRemaining}|sceneCurrent={zone3.Convoy.CurrentPoint}|networkCurrent={match.Zone3ConvoyCurrentPoint}");
            }

            RuntimeLog.Log(RuntimeLogCategory.PlayerSpawner,
                $"HM_AUDIT_SUMMARY|players={playerCount}|cores={_energyCoreInstances.Count}|carriedCores={carriedCoreCount}|sectorBoxes={_sectorBoxInstances.Count}|stations={_powerPuzzleStationInstances.Count}|minions={_creepMinionInstances.Count}|unresolvedRefs={unresolved}|note=diagnostic_only_not_migration_pass");
        }

        public bool RebuildMigratedLobbyPlacementAuthoritative(
            NetworkRunner runner)
        {
            if (runner == null
                || !runner.IsRunning
                || !runner.IsServer
                || SceneManager.GetActiveScene().name
                    != NetworkBootstrap.LobbySceneName)
            {
                return false;
            }

            _spawnSlots.Clear();

            var placedPlayers = 0;

            foreach (var player in
                     runner.ActivePlayers)
            {
                if (!runner.TryGetPlayerObject(
                        player,
                        out var playerObject)
                    || playerObject == null
                    || !playerObject.IsValid)
                {
                    Debug.LogError(
                        $"[PlayerSpawner] Migrated Lobby PlayerObject missing for {player}.");

                    return false;
                }

                var slot =
                    GetOrAssignSlot(
                        player);

                if (!TryTeleportExistingPlayer(
                        playerObject,
                        GetLobbySpawnPose(slot),
                        projectToGround: false))
                {
                    Debug.LogError(
                        $"[PlayerSpawner] Could not place migrated Lobby player {player} in slot {slot}.");

                    return false;
                }

                placedPlayers++;
            }

            RuntimeLog.Log(
                RuntimeLogCategory.PlayerSpawner,
                $"PSP|MIGRATION_LOBBY_PLACEMENT|players={placedPlayers}|uniqueSlots={_spawnSlots.Count}");

            return placedPlayers > 0;
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
            if (AlienHuntMission.Instance != null)
            {
                if (_map2HuntRegionGraph != null && _map2LizardPrefab != null)
                    EnsureInitialStalker(runner, RegionSemanticZone.Zone01, _map2HuntRegionGraph, _map2LizardPrefab);
                else
                    Debug.LogError("[PlayerSpawner] Scene_B Stalker requires its Lizard prefab and scene RegionGraph.");
                if (_matchStateInstance && _monsterInstance)
                    _matchStateInstance.GetComponent<NetworkMatchState>().BindPurgeBoss(_monsterInstance);
                BindAuthoritativeWorldState();
                return;
            }
            if (_powerPuzzlePrefab == null)
            {
                _powerPuzzlePrefab = Resources.Load<NetworkObject>("Network/NetworkPowerPuzzle");
            }
            if (_powerPuzzleStationPrefab == null)
            {
                _powerPuzzleStationPrefab = Resources.Load<NetworkObject>("Network/NetworkPowerPuzzleStation");
            }
            if (_doorInstance == null && _doorPrefab != null && !Zone3MissionDirector.IsSciFiSceneLoaded && !FloodedMapMission.IsLoaded)
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
            if (SceneManager.GetActiveScene().name == LobbyMapCatalog.ExplorationScene)
            {
                if (_map2RegionGraph != null && _map2LizardPrefab != null)
                    EnsureInitialStalker(runner, RegionSemanticZone.Zone01, _map2RegionGraph, _map2LizardPrefab);
                else
                    Debug.LogError("[PlayerSpawner] Map2 Stalker requires its Lizard prefab and scene RegionGraph.");
            }
            else EnsureInitialStalker(runner, RegionSemanticZone.Zone01);
            BindAuthoritativeWorldState();
        }

        private void EnsureInitialStalker(
            NetworkRunner runner,
            RegionSemanticZone zone,
            RegionGraphAsset graph = null,
            NetworkObject prefab = null)
        {
            prefab ??= _monsterPrefab;
            if (_monsterInstance == null && prefab != null)

            {
                var stalkerSpawn = GameObject.Find("MonsterSpawn_Stalker_EMPTY");
                bool valid = AlienHuntMission.Instance
                    ? TryGetPurgeBossSpawn(stalkerSpawn ? stalkerSpawn.transform : null, out var spawnPosition)
                    : TryGetStalkerSpawnPosition(stalkerSpawn ? stalkerSpawn.transform : null, out spawnPosition);
                if (valid)
                {
                    _monsterInstance = SpawnStalker(runner, spawnPosition, stalkerSpawn.transform.rotation, zone, graph, prefab);
                }
            }
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
            if (AlienHuntMission.Instance != null)
            {
                MaintainPurgeFishcatPopulation(runner);
                return;
            }
            var difficulty = MatchAuthorityRuntime.Instance != null
                ? MatchAuthorityRuntime.Instance.Difficulty : MatchDifficulty.Normal;
            var profile = MatchDifficultyProfiles.Get(difficulty);
            var authority = MatchAuthorityRuntime.Instance;
            if (difficulty == MatchDifficulty.Normal && authority != null &&
                authority.RequestedScenarioResolutionMode == ScenarioResolutionMode.Adaptive &&
                AEDv2Authority.TryGetV02(authority.MatchId, out var v02, out _))
                profile = AEDv2GameplayBridge.ToV02NormalDifficultyProfile(v02);
            else if (difficulty == MatchDifficulty.Normal && authority != null &&
                authority.TryGetMatchId(out var aedMatchId) &&
                AEDv2Authority.TryGetApplied(aedMatchId, out var plan, out _))
                profile = AEDv2GameplayBridge.ToNormalDifficultyProfile(plan);
            bool map2 = SceneManager.GetActiveScene().name == LobbyMapCatalog.ExplorationScene;
            float zone3Cap = RemoteFixedBalanceSnapshot.GameplayValue("minionCapZone3", -1);
            var zone = _zone3MonsterSpawned && (HasAdaptiveV02Plan() || zone3Cap >= 0)
                ? RegionSemanticZone.Zone03 : (_zone2MinionsActive ? RegionSemanticZone.Zone02 : RegionSemanticZone.Zone01);
            int cap = zone == RegionSemanticZone.Zone01 ? profile.Zone1MinionCap : zone == RegionSemanticZone.Zone02 ? profile.Zone2MinionCap : profile.Zone3MinionCap;
            if (zone == RegionSemanticZone.Zone03 && zone3Cap >= 0) cap = (int)zone3Cap;
            else if (zone != RegionSemanticZone.Zone03)
                cap = (int)RemoteFixedBalanceSnapshot.GameplayValue(zone == RegionSemanticZone.Zone01 ? "minionCapZone1" : "minionCapZone2", cap);
            _v02ObservedMinionZone = (int)zone;
            _v02ObservedMinionCap = cap;
            _v02ObservedMinionRevision = authority != null && AEDv2Authority.TryGetV02(authority.MatchId, out _, out var observedRevision) ? observedRevision : 0;
            int originalCount = 0;
            int disruptorCount = 0;

            foreach (var obj in _creepMinionInstances)
            {
                if (!IsValidNetworkObject(obj)
                    || !obj.TryGetComponent<CreepMinionRuntime>(out var minion)
                    || minion.Zone != zone
                    || minion.IsDying)
                {
                    continue;
                }

                if (minion.Role == CreepMinionRole.Disruptor) disruptorCount++;
                else originalCount++;
            }
            int count = originalCount + disruptorCount;
            bool disruptor = disruptorCount < originalCount;
            if (count >= cap) return;

            if (map2 && disruptor)
            {
                if (TryFindCatfishSpawnPosition(runner, out var catfishPosition))
                {
                    SpawnCreepMinion(runner, catfishPosition, zone, true);
                    return;
                }
                if (TryGetCreepMinionSpawnAnchor(zone, out var fallbackStalker)
                    && TryFindCreepMinionSpawnPosition(runner, fallbackStalker, out var landPosition))
                    SpawnCreepMinion(runner, landPosition, zone, true);
                return;
            }

            if (!TryGetCreepMinionSpawnAnchor(zone, out var stalker)) return;
            if (!TryFindCreepMinionSpawnPosition(runner, stalker, out var position)) return;
            if (zone == RegionSemanticZone.Zone03 && _v02ObservedMinionRevision > 0)
            {
                var state = NetworkMatchState.Instance;
                if (authority == null || !authority.V02Zone3SpawnEntitled || state == null ||
                    state.CurrentPhase != NetworkMatchPhase.Zone3FindFrigate || !state.IsV02SafeWindow() ||
                    !AEDV02SceneSource.TryLoad(out var manifest, out _) || !AEDV02SceneSource.TryGetDoorSources(runner, state, out var doors) ||
                    !manifest.HasReachableGeometry(new[] { (double)stalker.transform.position.x, stalker.transform.position.y, stalker.transform.position.z },
                        new[] { (double)position.x, position.y, position.z }, doors)) return;
            }
            SpawnCreepMinion(runner, position, zone, map2 ? false : disruptor);
        }

        private sealed class PurgeActivityGroup
        {
            public Vector3 Position;
            public int Members;
        }

        private List<PurgeActivityGroup> GetPurgeActivityGroups(NetworkRunner runner)
        {
            var groups = new List<PurgeActivityGroup>();
            var path = new NavMeshPath();
            foreach (var player in runner.ActivePlayers)
            {
                if (!runner.TryGetPlayerObject(player, out var obj) || !IsValidNetworkObject(obj)
                    || !obj.TryGetComponent<LobbyPlayerState>(out var lobby) || !lobby.IsGameplayPlayer
                    || !obj.TryGetComponent<NetworkPlayerLifeState>(out var life) || life.Status != NetworkPlayerLifeStatus.Alive) continue;
                Vector3 position = obj.transform.position;
                PurgeActivityGroup group = null;
                foreach (var candidate in groups)
                    if (Vector3.Distance(candidate.Position, position) <= 22f
                        && NavMesh.SamplePosition(candidate.Position, out var a, 12f, NavMesh.AllAreas)
                        && NavMesh.SamplePosition(position, out var b, 12f, NavMesh.AllAreas)
                        && NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path)
                        && path.status == NavMeshPathStatus.PathComplete)
                    { group = candidate; break; }
                if (group == null) groups.Add(new PurgeActivityGroup { Position = position, Members = 1 });
                else group.Members++;
            }
            return groups;
        }

        private void MaintainPurgeFishcatPopulation(NetworkRunner runner)
        {
            var match = NetworkMatchState.Instance;
            if (match == null || match.IsEnded || !runner.IsServer) return;
            var groups = GetPurgeActivityGroups(runner);
            if (groups.Count == 0) return;
            var fish = new List<CreepMinionRuntime>();
            foreach (var obj in _creepMinionInstances)
                if (IsValidNetworkObject(obj) && obj.TryGetComponent<CreepMinionRuntime>(out var minion)
                    && minion.Role == CreepMinionRole.Catfish && !minion.IsDying) fish.Add(minion);

            // Assign duties across reachable player groups, without granting an AI a player target.
            if (Time.time >= _nextPurgeEncounterAt)
            {
                _nextPurgeEncounterAt = Time.time + 3f;
                var assigned = new int[groups.Count];
                for (int i = 0; i < fish.Count; i++)
                {
                    int best = 0;
                    for (int g = 1; g < groups.Count; g++)
                        if (assigned[g] / (float)groups[g].Members < assigned[best] / (float)groups[best].Members) best = g;
                    assigned[best]++;
                    fish[i].SetPurgeEncounter((FishcatEncounterDuty)(i % 3), groups[best].Position);
                    if (fish[i].CanRecyclePurgeFishcat())
                    {
                        runner.Despawn(fish[i].Object);
                        _purgeRefillSchedule.IsDue(Time.time, fish.Count - 1, match.PurgeMinionCap, match.PurgeMinionSpawnCooldown);
                        return; // Replacement uses the same refill cooldown as a UV kill.
                    }
                }
            }
            if (!_purgeRefillSchedule.IsDue(Time.time, fish.Count, match.PurgeMinionCap, match.PurgeMinionSpawnCooldown)) return;

            // Prefer the least populated active area per participating player.
            groups.Sort((a, b) => PurgeAreaPressure(fish, a).CompareTo(PurgeAreaPressure(fish, b)));
            foreach (var group in groups)
            {
                Vector3 position;
                bool found = TryFindCatfishSpawnPosition(runner, out position, group.Position);
                if (!found && NavMesh.SamplePosition(group.Position, out var anchor, 12f, NavMesh.AllAreas))
                    found = TryFindCreepMinionSpawnPositionInRing(runner, anchor.position,
                        _minionSpawnMinDistanceFromPlayer, _minionSpawnFallbackMaxDistanceFromStalker, 48, out position);
                if (!found) continue;
                var spawned = SpawnFishcatOnly(runner, position);
                if (spawned == null) continue;
                spawned.GetComponent<CreepMinionRuntime>().SetPurgeEncounter((FishcatEncounterDuty)(fish.Count % 3), group.Position);
                _purgeRefillSchedule.Spawned(Time.time);
                return; // Never fill a whole tier in one check.
            }
        }

        private static float PurgeAreaPressure(List<CreepMinionRuntime> fish, PurgeActivityGroup group)
        {
            int count = 0;
            foreach (var minion in fish)
                if (Vector3.Distance(minion.transform.position, group.Position) < 40f) count++;
            return count / (float)group.Members;
        }

        private NetworkObject SpawnFishcatOnly(NetworkRunner runner, Vector3 position) =>
            SpawnCreepMinion(runner, position, RegionSemanticZone.Zone01, true);

        private bool TryFindCatfishSpawnPosition(NetworkRunner runner, out Vector3 position, Vector3? focus = null)
        {
            position = default;
            if (!FloodedWaterBoundary.TryGetWaterBounds(out var water)) return false;
            var blockers = new List<Collider>();
            var playBounds = default(Bounds);
            foreach (var collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (collider == null || !collider.enabled || !collider.name.StartsWith("_BLOCKERCUBE")) continue;
                if (blockers.Count == 0) playBounds = collider.bounds;
                else playBounds.Encapsulate(collider.bounds);
                blockers.Add(collider);
            }
            if (blockers.Count == 0)
            {
                if (!focus.HasValue) return false;
                playBounds = water;
            }
            float minX = Mathf.Max(water.min.x, playBounds.min.x);
            float maxX = Mathf.Min(water.max.x, playBounds.max.x);
            float minZ = Mathf.Max(water.min.z, playBounds.min.z);
            float maxZ = Mathf.Min(water.max.z, playBounds.max.z);
            if (minX >= maxX || minZ >= maxZ) return false;
            var playerPositions = new List<Vector3>();
            foreach (var player in runner.ActivePlayers)
                if (runner.TryGetPlayerObject(player, out var playerObject)
                    && IsValidNetworkObject(playerObject)
                    && playerObject.TryGetComponent<LobbyPlayerState>(out var lobby)
                    && lobby.IsGameplayPlayer
                    && playerObject.TryGetComponent<NetworkPlayerLifeState>(out var life)
                    && life.Status == NetworkPlayerLifeStatus.Alive)
                    playerPositions.Add(playerObject.transform.position);
            if (playerPositions.Count == 0) return false;
            if (focus.HasValue) { playerPositions.Clear(); playerPositions.Add(focus.Value); }
            for (int attempt = 0; attempt < 72; attempt++)
            {
                Vector2 direction = Random.insideUnitCircle.normalized;
                float distance = Random.Range(
                    _minionSpawnMinDistanceFromPlayer,
                    Mathf.Max(_minionSpawnMinDistanceFromPlayer + 1f, _minionSpawnFallbackMaxDistanceFromStalker));
                Vector3 candidate = playerPositions[attempt % playerPositions.Count]
                    + new Vector3(direction.x, 0f, direction.y) * distance;
                if (candidate.x < minX || candidate.x > maxX || candidate.z < minZ || candidate.z > maxZ) continue;
                if (!FloodedWaterBoundary.TryGetSwimSurface(candidate, out float surfaceY, 2.6f)) continue;
                if (blockers.Count > 0 && !IsInsideBlockerBoundary(blockers, candidate, surfaceY, maxX + 1f)) continue;
                candidate.y = surfaceY - 2.5f;
                if (focus.HasValue && Physics.CheckCapsule(candidate + Vector3.up * .4f,
                        candidate + Vector3.up * 1.4f, .6f, Physics.DefaultRaycastLayers & ~(1 << 4), QueryTriggerInteraction.Ignore)) continue;
                if (!IsCatfishSpawnInPlayerArea(runner, candidate, surfaceY, focus)) continue;
                if (!IsCreepMinionSpawnFarFromPlayers(runner, candidate)) continue;
                if (focus.HasValue && (CreepMinionRuntime.IsPositionObserved(runner, candidate + Vector3.up * 2f)
                    || CreepMinionRuntime.IsPositionObserved(runner, new Vector3(candidate.x, surfaceY + .5f, candidate.z)))) continue;
                bool overlaps = false;
                foreach (var minion in _creepMinionInstances)
                    if (IsValidNetworkObject(minion)
                        && (new Vector2(minion.transform.position.x - candidate.x, minion.transform.position.z - candidate.z)).sqrMagnitude < 4f)
                    { overlaps = true; break; }
                if (overlaps) continue;
                position = candidate;
                return true;
            }
            return false;
        }

        private static bool IsInsideBlockerBoundary(
            List<Collider> blockers, Vector3 point, float surfaceY, float rayEndX)
        {
            var ray = new Ray(new Vector3(point.x, surfaceY + 1f, point.z), Vector3.right);
            float distance = rayEndX - point.x;
            int crossings = 0;
            foreach (var blocker in blockers)
                if (blocker.Raycast(ray, out _, distance)) crossings++;
            return crossings % 2 == 1;
        }

        private static bool IsCatfishSpawnInPlayerArea(NetworkRunner runner, Vector3 candidate, float surfaceY, Vector3? focus = null)
        {
            if (!NavMesh.SamplePosition(candidate, out var shore, 12f, NavMesh.AllAreas)) return false;
            var start = new Vector3(candidate.x, surfaceY + 1f, candidate.z);
            var end = shore.position + Vector3.up;
            var ray = end - start;
            foreach (var hit in Physics.RaycastAll(start, ray.normalized, ray.magnitude,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (hit.collider != null && hit.collider.name.StartsWith("_BLOCKERCUBE")) return false;

            var path = new NavMeshPath();
            if (focus.HasValue)
                return NavMesh.SamplePosition(focus.Value, out var focusHit, 4f, NavMesh.AllAreas)
                    && NavMesh.CalculatePath(focusHit.position, shore.position, NavMesh.AllAreas, path)
                    && path.status == NavMeshPathStatus.PathComplete;
            foreach (var player in runner.ActivePlayers)
                if (runner.TryGetPlayerObject(player, out var playerObject)
                    && playerObject != null
                    && NavMesh.SamplePosition(playerObject.transform.position, out var playerHit, 3f, NavMesh.AllAreas)
                    && NavMesh.CalculatePath(playerHit.position, shore.position, NavMesh.AllAreas, path)
                    && path.status == NavMeshPathStatus.PathComplete)
                    return true;
            return false;

        }

        private static bool HasAdaptiveV02Plan()
        {
            var authority = MatchAuthorityRuntime.Instance;
            return authority != null
                && authority.RequestedScenarioResolutionMode == ScenarioResolutionMode.Adaptive
                && AEDv2Authority.TryGetV02(authority.MatchId, out _, out _);
        }

        private int _v02ObservedMinionZone;
        private int _v02ObservedMinionCap;
        private int _v02ObservedMinionRevision;
        public bool TryReadV02MinionCap(Guid matchId, int revision, AEDV02Key key, out double value, out string[] objectIds)
        {
            value = 0; objectIds = Array.Empty<string>();
            int zone = key == AEDV02Key.Zone1MinionCap ? 1 : key == AEDV02Key.Zone2MinionCap ? 2 : key == AEDV02Key.Zone3AdaptiveMinionCap ? 3 : 0;
            if (zone == 0 || MatchAuthorityRuntime.Instance?.MatchId != matchId || _v02ObservedMinionRevision != revision || _v02ObservedMinionZone != zone) return false;
            value = _v02ObservedMinionCap;
            objectIds = _creepMinionInstances.Where(x => IsValidNetworkObject(x) && x.TryGetComponent<CreepMinionRuntime>(out var minion) &&
                (int)minion.Zone == zone && !minion.IsDying).Select(x => x.Id.ToString()).ToArray();
            return key != AEDV02Key.Zone3AdaptiveMinionCap || value != 1 || objectIds.Length != 0;
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
            var authority = MatchAuthorityRuntime.Instance;
            bool adaptiveV02 = authority != null &&
                authority.RequestedScenarioResolutionMode == ScenarioResolutionMode.Adaptive &&
                AEDv2Authority.TryGetV02(authority.MatchId, out _, out _);
            AEDV02SceneManifest safeRoomManifest = null;
            if (adaptiveV02 && (!AEDV02SceneSource.TryLoad(out safeRoomManifest, out _) || safeRoomManifest == null))
                return false;

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

                if (adaptiveV02 &&
                    (!safeRoomManifest.TryIsInsideSafeRoom(hit.position.x, hit.position.y, hit.position.z, out var inside) ||
                     inside ||
                     !safeRoomManifest.TryPathIntersectsSafeRoom(
                         path.corners.Select(x => new[] { (double)x.x, x.y, x.z }).ToArray(), out var routeIntersects) ||
                     routeIntersects))
                {
                    continue;
                }

                if (!IsCreepMinionSpawnFarFromPlayers(runner, hit.position))
                {
                    continue;
                }
                if (AlienHuntMission.Instance != null)
                {
                    if (CreepMinionRuntime.IsPositionObserved(runner, hit.position + Vector3.up)
                        || CreepMinionRuntime.IsPositionObserved(runner, hit.position + Vector3.up * 2f)) continue;
                    if (Physics.CheckCapsule(hit.position + Vector3.up * .7f, hit.position + Vector3.up * 1.3f,
                            .6f, Physics.DefaultRaycastLayers & ~(1 << 4), QueryTriggerInteraction.Ignore)) continue;
                }

                bool overlapsMinion = false;
                foreach (var minion in _creepMinionInstances)
                {
                    if (IsValidNetworkObject(minion)
                        && (minion.transform.position - hit.position).sqrMagnitude < 4f)
                    {
                        overlapsMinion = true;
                        break;
                    }
                }
                if (overlapsMinion) continue;

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

        private int _nextCreepMinionVisualVariant;
        private readonly PurgeFishcatRefillSchedule _purgeRefillSchedule = new PurgeFishcatRefillSchedule();
        private float _nextPurgeEncounterAt;

        private NetworkObject SpawnCreepMinion(NetworkRunner runner, Vector3 position, RegionSemanticZone zone, bool disruptor)
        {
            var prefab = ResolveCreepMinionPrefab();
            if (prefab == null) return null;
            var spawned = runner.Spawn(prefab, position, Quaternion.identity, PlayerRef.None,
                (_, obj) =>
                {
                    // Map2 uses the same scene agent type for its Stalker and minions.
                    if ((AlienHuntMission.Instance != null || SceneManager.GetActiveScene().name == LobbyMapCatalog.ExplorationScene)
                        && _map2LizardPrefab != null && obj.TryGetComponent<NavMeshAgent>(out var agent))
                        agent.agentTypeID = _map2LizardPrefab.GetComponent<NavMeshAgent>().agentTypeID;
                    obj.GetComponent<CreepMinionRuntime>()?.ConfigureBeforeSpawn(
                    zone,
                    AlienHuntMission.Instance != null ? CreepMinionRole.Catfish
                        : disruptor ? CreepMinionRole.Disruptor : CreepMinionRole.Original,
                    AlienHuntMission.Instance != null ? 0 : _nextCreepMinionVisualVariant);
                });
            if (!IsValidNetworkObject(spawned)) return null;
            if (!spawned.TryGetComponent<CreepMinionRuntime>(out _))
            {
                Debug.LogError("[PlayerSpawner] Creep Minion prefab has no CreepMinionRuntime.");
                runner.Despawn(spawned);
                return null;
            }
            _creepMinionInstances.Add(spawned);
            if (disruptor) _nextCreepMinionVisualVariant = (_nextCreepMinionVisualVariant + 1) % 30;
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

        private NetworkObject SpawnStalker(
            NetworkRunner runner,
            Vector3 position,
            Quaternion rotation,
            RegionSemanticZone zone,
            RegionGraphAsset graph = null,
            NetworkObject prefab = null)
        {
            var spawned = runner.Spawn(prefab != null ? prefab : _monsterPrefab, position, rotation, PlayerRef.None,
                (_, obj) =>
                {
                    var controller = obj.GetComponent<StalkerController>();
                    if (controller == null) return;
                    if (graph != null) controller.ConfigureRegionGraph(graph, zone);
                    else controller.ConfigurePatrolZone(zone);
                });

            if (spawned != null)
            {
                RuntimeLog.Log(RuntimeLogCategory.PlayerSpawner,
                    $"[PlayerSpawner] Spawned {zone} Stalker {spawned.Id} at {position}.");
            }
            return spawned;
        }

        private static bool TryGetPurgeBossSpawn(Transform marker,out Vector3 position)
        {
            position=default; var mission=AlienHuntMission.Instance;
            if(!mission || !marker)return false;
            var tri=NavMesh.CalculateTriangulation();var path=new NavMeshPath();
            float best=float.PositiveInfinity;
            for(int i=0;i+2<tri.indices.Length;i+=9)
            {
                var center=(tri.vertices[tri.indices[i]]+tri.vertices[tri.indices[i+1]]+tri.vertices[tri.indices[i+2]])/3f;
                float score=(center-marker.position).sqrMagnitude;if(score>=best)continue;
                if(!NavMesh.SamplePosition(center,out var hit,2,NavMesh.AllAreas))continue;
                if(!NavMesh.CalculatePath(hit.position,mission.Actors[0].Home,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                bool blocked=false;
                foreach(var c in Physics.OverlapCapsule(hit.position+Vector3.up*.65f,hit.position+Vector3.up*1.8f,.5f,
                    Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                    if(!(c is TerrainCollider)&&GameplayEnvironmentPhysics.IsEnvironment(c)){blocked=true;break;}
                if(blocked)continue;
                best=score;position=hit.position;
            }
            if(float.IsPositiveInfinity(best))Debug.LogError("[Purge] No connected clear boss spawn surface found.");
            return !float.IsPositiveInfinity(best);
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

            var gameplay = LobbyMapCatalog.IsGameplayScene(SceneManager.GetActiveScene().name);
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

            RemoteFixedBalanceSnapshot remoteBalance = null;
            if (gameplay)
            {
                var authority = MatchAuthorityRuntime.Instance;
                if (authority != null)
                    authority.TryGetRemoteFixedBalance(out remoteBalance);
            }

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
                if (gameplay && toolId != 0 && playerObject.HasStateAuthority && _bootstrap?.Runner != null)
                    MatchAuthorityRuntime.Instance?.TryRecordPhase8LobbyLoadout(playerObject, _bootstrap.Runner.Tick.Raw);
            }

            if (playerObject.TryGetComponent<NetworkPlayerLifeState>(out var lifeState) &&
                playerObject.HasStateAuthority)
            {
                lifeState.ApplyRemoteFixedBalanceAuthoritative(remoteBalance);
                lifeState.ResetForMatchAuthoritative();
            }

            if (playerObject.TryGetComponent<NetworkPlayerInteractor>(out var interactor) && playerObject.HasStateAuthority && gameplay)
            {
                interactor.ResetForMatchAuthoritative();
            }

            playerObject.TryGetComponent<NetworkPlayerMovement>(out var movement);
            if (movement != null && playerObject.HasStateAuthority)
            {
                movement.ApplyRemoteFixedBalanceAuthoritative(remoteBalance);
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
                state?.MarkGameplaySpawnReadyAuthoritative();
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
            // Stable world slots leave the right-hand mission panel unobstructed on every peer.
            var position = LobbyLineupLayout.Position(slot);
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
            _nextCreepMinionVisualVariant = 0;
            _purgeRefillSchedule.Reset();
            _nextPurgeEncounterAt = 0f;
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
