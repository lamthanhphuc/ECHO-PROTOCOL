using System;
using System.Collections.Generic;
using System.Linq;
using EchoProtocol.AI.AED;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Networking;
using EchoProtocol.Networking.Authority;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

namespace EchoProtocol.TeamTools
{
    public static partial class TeamToolWorldSpawn
    {
        private static AEDV02SupplyLedger _v02Supply;
        private static NetworkRunner _v02Runner;
        private static readonly Dictionary<int, List<SpawnPlan>> V02Reservations = new Dictionary<int, List<SpawnPlan>>();
        private static readonly Dictionary<int, List<SpawnPlan>> V02NormalReservations = new Dictionary<int, List<SpawnPlan>>();
        private static readonly Dictionary<TeamToolSpawnPoint, double[][]> V02EntryPaths = new Dictionary<TeamToolSpawnPoint, double[][]>();
        private static AEDV02ProposedReservation[] _v02GradePreview;
        public static bool PreviewV02ResourceGrade(NetworkRunner runner, TeamToolPickupCatalog catalog, Guid match, int grade, float spacing, out AEDV02ProposedReservation[] proposed)
        {
            _v02GradePreview = null;
            bool ready = TryPrepareV02Grade(runner, catalog, match, grade, spacing, false);
            proposed = _v02GradePreview; return ready && proposed != null;
        }
        public static AEDV02SupplySource ReadV02Supply(NetworkRunner runner, Guid match)
        {
            if (_v02Supply == null || _v02Supply.MatchId != match || _v02Runner != runner ||
                V02Reservations.Values.SelectMany(x => x).Any(x => x.Point == null)) return null;
            return new AEDV02SupplySource { InitialOptionalBudget = _v02Supply.InitialOptionalBudget,
                OptionalBudget = _v02Supply.OptionalBudget, OptionalMaterialized = _v02Supply.OptionalMaterialized,
                ReservationGrade = _v02Supply.ResourceGrade,
                SpawnedZones = Enumerable.Range(1, 3).Where(_v02Supply.HasSpawned).ToArray(),
                Reservations = V02Reservations.Where(x => !_v02Supply.HasSpawned(x.Key))
                    .SelectMany(x => x.Value.Take(5 + _v02Supply.OptionalForZone(x.Key)).Select(item => new AEDV02SupplyReservation
                    { Zone = x.Key, ToolId = item.ToolId, MarkerId = AEDV02SceneSource.MarkerId(item.Point.transform),
                        EntryPath = V02EntryPaths.TryGetValue(item.Point, out var path) ? path : Array.Empty<double[]>() })).ToArray() };
        }
        public static bool CanSetV02SupplyBudget(NetworkRunner runner, Guid match, int budget)
        {
            if (_v02Supply == null || _v02Supply.MatchId != match || _v02Runner != runner) return false;
            var authority = MatchAuthorityRuntime.Instance;
            if (authority?.HasAutomaticBalance != true) return _v02Supply.CanSetOptionalBudget(budget);
            return authority.TryResolveV02ToolMask(budget, AEDv2Authority.V02InitialOptionalBudget, out int mask) &&
                _v02Supply.CanSetOptionalMask(mask) && Enumerable.Range(1, 3).All(zone =>
                    _v02Supply.OptionalForZone(zone) == AEDV02Plan.OptionalForZone(mask, zone) ||
                    AEDV02Plan.OptionalForZone(mask, zone) == 0 || V02NormalReservations[zone].Count > RequiredToolCountPerZone);
        }
        private static bool IsV02Occupied(Vector3 position, IEnumerable<Transform> actors) => actors.Any(actor =>
            actor.GetComponentsInChildren<Collider>(true).Any(body => body.enabled && !body.isTrigger && body.bounds.Contains(position)) ||
            actor.TryGetComponent<CharacterController>(out var capsule) && new Bounds(actor.TransformPoint(capsule.center),
                Vector3.Scale(new Vector3(capsule.radius * 2, capsule.height, capsule.radius * 2), actor.lossyScale)).Contains(position));
        public static void ResetV02(Guid match)
        {
            if (_v02Supply != null && match != Guid.Empty && match != _v02Supply.MatchId) return;
            _v02Supply = null; _v02Runner = null; V02Reservations.Clear(); V02NormalReservations.Clear(); V02EntryPaths.Clear(); V02SupportActions.Clear(); _v02GradePreview = null;
        }
        public static bool CanApplyV02ResourceGrade(NetworkRunner runner, TeamToolPickupCatalog catalog, Guid match, int grade, float spacing) =>
            TryPrepareV02Grade(runner, catalog, match, grade, spacing, false);
        private static bool TryPrepareV02Grade(NetworkRunner runner, TeamToolPickupCatalog catalog, Guid match, int grade, float spacing, bool commit)
        {
            if (_v02Supply == null || _v02Supply.MatchId != match || _v02Runner != runner || !_v02Supply.CanSetResourceGrade(grade) ||
                catalog == null || !AEDV02SceneSource.TryLoad(out var manifest, out _) || !AEDV02SceneSource.TryGetDoorSources(runner, NetworkMatchState.Instance, out var doors)) return false;
            if (grade == _v02Supply.ResourceGrade) return true;
            var available = UnityEngine.Object.FindObjectsByType<TeamToolSpawnPoint>(FindObjectsInactive.Exclude)
                .Where(x => x.gameObject.scene.path == manifest.ScenePath).ToDictionary(x => AEDV02SceneSource.MarkerId(x.transform));
            var replacements = new Dictionary<int, List<SpawnPlan>>();
            var paths = new Dictionary<TeamToolSpawnPoint, double[][]>();
            foreach (var zone in Enumerable.Range(1, 3).Where(x => !_v02Supply.HasSpawned(x)))
            {
                var entry = manifest.Markers.Single(x => x.Id == "Z" + zone + "_ENTRY");
                var entryPosition = new Vector3((float)entry.X, (float)entry.Y, (float)entry.Z);
                var candidates = new List<AEDV02PlacementCandidate>();
                foreach (var marker in manifest.Markers.Where(x => x.Kind == "TOOL" && x.Zone == zone && available.ContainsKey(x.Id)))
                {
                    if (!manifest.TryIsInsideSafeRoom(marker.X, marker.Y, marker.Z, out var inside) || inside) continue;
                    var point = available[marker.Id];
                    if (!point.TryGetReachablePosition(entryPosition, out var position, out var meters) || HasNearbyWorldTeamTool(position, spacing) ||
                        !TryV02EntryPath(entryPosition, position, out var corners) || !manifest.TryValidatePlacementPath(corners, doors, out _)) continue;
                    candidates.Add(new AEDV02PlacementCandidate { Marker = marker, Path = corners, PathMeters = meters });
                }
                var types = V02Reservations[zone].Take(5 + _v02Supply.OptionalForZone(zone)).Select(x => x.ToolId).ToArray();
                AEDV02SupplyReservation[] reservations;
                if (grade == 0)
                {
                    var original = V02NormalReservations[zone].Take(types.Length).ToArray();
                    if (original.Any(x => x.Point == null || !candidates.Any(c => c.Marker.Id == AEDV02SceneSource.MarkerId(x.Point.transform)))) return false;
                    reservations = original.Select(x => new AEDV02SupplyReservation { Zone = zone, ToolId = x.ToolId,
                        MarkerId = AEDV02SceneSource.MarkerId(x.Point.transform),
                        EntryPath = candidates.Single(c => c.Marker.Id == AEDV02SceneSource.MarkerId(x.Point.transform)).Path }).ToArray();
                }
                else
                {
                    var fallback = grade == -1 && MatchAuthorityRuntime.Instance?.HasAutomaticBalance == true
                        ? V02Reservations[zone].Take(types.Length).Select(item => new AEDV02SupplyReservation
                        { Zone = zone, ToolId = item.ToolId, MarkerId = AEDV02SceneSource.MarkerId(item.Point.transform) }).ToArray()
                        : null;
                    if (!AEDV02ResourcePlacement.TryPlan(zone, grade, types, candidates, spacing, out reservations, fallback)) return false;
                }
                var plans = new List<SpawnPlan>();
                foreach (var reservation in reservations)
                {
                    var point = available[reservation.MarkerId];
                    if (!point.TryGetSpawnPosition(out var position) || !IsCorrectTool(catalog.GetPrefab(reservation.ToolId), reservation.ToolId)) return false;
                    plans.Add(new SpawnPlan(point, position, catalog.GetPrefab(reservation.ToolId), reservation.ToolId)); paths[point] = reservation.EntryPath;
                }
                for (int i = 0; i < plans.Count; i++)
                    for (int j = i + 1; j < plans.Count; j++)
                        if (Vector3.Distance(plans[i].Position, plans[j].Position) < spacing) return false;
                // Required entries remain first so optional reductions cannot remove a required type.
                var ordered = new List<SpawnPlan>();
                foreach (int type in RequiredToolIds) { int index = plans.FindIndex(x => x.ToolId == type); ordered.Add(plans[index]); plans.RemoveAt(index); }
                ordered.AddRange(plans); replacements[zone] = ordered;
            }
            if (commit)
            {
                foreach (var replacement in replacements) V02Reservations[replacement.Key] = replacement.Value;
                V02EntryPaths.Clear(); foreach (var path in paths) V02EntryPaths.Add(path.Key, path.Value);
                return _v02Supply.TrySetResourceGrade(grade);
            }
            _v02GradePreview = replacements.SelectMany(x => x.Value.Select(p => new AEDV02ProposedReservation {
                Zone = x.Key, ToolId = p.ToolId, MarkerId = AEDV02SceneSource.MarkerId(p.Point.transform) })).ToArray();
            return true;
        }
        private static bool TryV02EntryPath(Vector3 from, Vector3 to, out double[][] corners)
        {
            corners = Array.Empty<double[]>();
            if (!NavMesh.SamplePosition(from, out var start, 2, NavMesh.AllAreas) || !NavMesh.SamplePosition(to, out var end, 1.5f, NavMesh.AllAreas)) return false;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            corners = path.corners.Select(x => new[] { (double)x.x, x.y, x.z }).ToArray(); return corners.Length > 0;
        }
        public static bool TrySpawnV02Zone(NetworkRunner runner, TeamToolPickupCatalog catalog, Guid match, AEDV02Plan plan,
            int zone, float spacing)
        {
            var authority = MatchAuthorityRuntime.Instance;
            if (authority == null || !authority.HasStateAuthority || authority.MatchId != match ||
                authority.Difficulty != EchoProtocol.Gameplay.MatchDifficulty.Normal ||
                authority.RequestedScenarioResolutionMode != ScenarioResolutionMode.Adaptive ||
                ScenarioConfigAuthorityRuntime.Instance?.RuntimeSettings?.PacingV02GameplayEnabled != true ||
                !AEDv2Authority.TryGetV02(match, out var approved, out _) || approved.Fingerprint() != plan?.Fingerprint()) return false;
            if (runner == null || !runner.IsServer || catalog == null || match == Guid.Empty || plan == null || zone < 1 || zone > 3 ||
                !AEDV02SceneSource.TryLoad(out var manifest, out _) ||
                !UnityEngine.SceneManagement.SceneManager.GetSceneByPath(manifest.ScenePath).isLoaded) return false;
            bool relative = authority.HasAutomaticBalance;
            int tier = (int)plan.Get(AEDV02Key.OptionalToolBudget);
            int mask = 0;
            if (relative && !authority.TryResolveV02ToolMask(tier, AEDv2Authority.V02InitialOptionalBudget, out mask)) return false;
            if (_v02Supply == null || _v02Supply.MatchId != match || _v02Runner != runner)
            {
                ResetV02(Guid.Empty);
                _v02Supply = new AEDV02SupplyLedger(match, relative ? AEDv2Authority.V02InitialOptionalBudget : tier,
                    relative ? mask : (int?)null); _v02Runner = runner;
                var points = UnityEngine.Object.FindObjectsByType<TeamToolSpawnPoint>(FindObjectsInactive.Exclude)
                    .Where(x => x.gameObject.scene.path == manifest.ScenePath).ToArray();
                for (int reservedZone = 1; reservedZone <= 3; reservedZone++)
                {
                    int count = RequiredToolCountPerZone + _v02Supply.OptionalForZone(reservedZone);
                    if (!(relative && TryBuildZonePlan(catalog, points, (TeamToolSpawnZone)reservedZone,
                            RequiredToolCountPerZone + 1, spacing, true, out var reserved)) &&
                        !TryBuildZonePlan(catalog, points, (TeamToolSpawnZone)reservedZone, count, spacing, true, out reserved))
                    { ResetV02(match); return false; }
                    V02Reservations.Add(reservedZone, reserved);
                    V02NormalReservations.Add(reservedZone, new List<SpawnPlan>(reserved));
                }
                if (relative && NetworkMatchState.Instance is { } state)
                {
                    for (int restored = 1; restored <= 3; restored++)
                    {
                        int bit = 1 << (restored - 1);
                        if ((state.AEDV02SupplySpawnedMask & bit) != 0 &&
                            !_v02Supply.TryRecordZoneSpawn(restored, RequiredToolCountPerZone,
                                (state.AEDV02SupplyMaterializedMask & bit) == 0 ? 0 : 1))
                        { ResetV02(match); return false; }
                    }
                }
            }
            if (relative && _v02Supply.ResourceGrade == 0)
                foreach (var pair in V02NormalReservations)
                    if (!_v02Supply.HasSpawned(pair.Key)) V02Reservations[pair.Key] = pair.Value;
            if (!(relative ? _v02Supply.TrySetOptionalMask(mask) : _v02Supply.TrySetOptionalBudget(tier))) return false;
            if (!TryPrepareV02Grade(runner, catalog, match, (int)plan.Get(AEDV02Key.ResourceGrade), spacing, true)) return false;
            if (_v02Supply.HasSpawned(zone)) return true;
            var plans = V02Reservations[zone].Take(RequiredToolCountPerZone + _v02Supply.OptionalForZone(zone)).ToArray();
            if (!AEDV02SceneSource.TryGetDoorSources(runner, NetworkMatchState.Instance, out var doors)) return false;
            var entry = manifest.Markers.Single(x => x.Id == "Z" + zone + "_ENTRY");
            var entryPosition = new Vector3((float)entry.X, (float)entry.Y, (float)entry.Z);
            var actors = UnityEngine.Object.FindObjectsByType<NetworkBehaviour>()
                .Where(x => x.Runner == runner && (x is NetworkPlayerLifeState || x is EchoProtocol.AI.Stalker.Networking.StalkerFusionRuntime ||
                    x is EchoProtocol.AI.Minions.CreepMinionRuntime)).Select(x => x.transform).ToArray();
            bool Occupied(Vector3 position) => IsV02Occupied(position, actors);
            bool CanPlace(TeamToolSpawnPoint point)
            {
                if (point == null) return false;
                var marker = manifest.Markers.SingleOrDefault(x => x.Id == AEDV02SceneSource.MarkerId(point.transform) &&
                    x.Kind == "TOOL" && x.Zone == zone);
                if (marker == null || !manifest.TryIsInsideSafeRoom(marker.X, marker.Y, marker.Z, out var inside) || inside ||
                    !point.TryGetReachablePosition(entryPosition, out var position, out _) ||
                    !manifest.TryIsInsideSafeRoom(position.x, position.y, position.z, out inside) || inside ||
                    HasNearbyWorldTeamTool(position, spacing) || Occupied(position) ||
                    !TryV02EntryPath(entryPosition, position, out var path) ||
                    !manifest.TryValidatePlacementPath(path, doors, out _)) return false;
                return true;
            }
            if (plans.Any(x => !CanPlace(x.Point)))
            {
                if (_v02Supply.ResourceGrade != 0) return false;
                var available = UnityEngine.Object.FindObjectsByType<TeamToolSpawnPoint>(FindObjectsInactive.Exclude)
                    .Where(x => x.gameObject.scene.path == manifest.ScenePath && (int)x.Zone == zone && CanPlace(x)).ToArray();
                if (!TryBuildZonePlan(catalog, available, (TeamToolSpawnZone)zone, RequiredToolCountPerZone + _v02Supply.OptionalForZone(zone),
                    spacing, true, out var replacement)) return false;
                V02Reservations[zone] = replacement; plans = replacement.ToArray();
            }
            foreach (var item in plans)
                if (item.Point == null || !item.Point.TryGetReachablePosition(entryPosition, out var current, out _) ||
                    (current - item.Position).sqrMagnitude > .0001f || HasNearbyWorldTeamTool(current, spacing) ||
                    Occupied(current) ||
                    !manifest.TryIsInsideSafeRoom(current.x, current.y, current.z, out var inside) || inside ||
                    !TryV02EntryPath(entryPosition, current, out var currentPath) ||
                    !manifest.TryValidatePlacementPath(currentPath, doors, out _)) return false;
            var spawned = new List<NetworkObject>();
            foreach (var item in plans)
            {
                var instance = SpawnPlannedTool(runner, item);
                if (instance == null) { RollbackSpawned(runner, spawned); return false; }
                spawned.Add(instance);
            }
            if (!_v02Supply.TryRecordZoneSpawn(zone, RequiredToolCountPerZone, plans.Length - RequiredToolCountPerZone))
            { RollbackSpawned(runner, spawned); return false; }
            if (relative) NetworkMatchState.Instance?.RecordV02SupplySpawn(zone, plans.Length > RequiredToolCountPerZone);
            for (int i = 0; i < spawned.Count; i++)
                MatchAuthorityRuntime.Instance?.RecordPhase8ToolSpawn(spawned[i].Id, plans[i].ToolId, zone, runner.Tick.Raw);
            Debug.Log($"[AED_V02][ZONE_TOOL_SPAWN] match={match:D} zone={zone} required=5 optional={plans.Length - 5} budget={_v02Supply.OptionalBudget}");
            return true;
        }
    }
}
