using System;
using System.Collections.Generic;
using System.Linq;
using EchoProtocol.AI.AED;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Networking;
using EchoProtocol.Networking.Authority;
using Fusion;
using UnityEngine;

namespace EchoProtocol.TeamTools
{
    public static partial class TeamToolWorldSpawn
    {
        private static AEDV02SupplyLedger _v02Supply;
        private static NetworkRunner _v02Runner;
        private static readonly Dictionary<int, List<SpawnPlan>> V02Reservations = new Dictionary<int, List<SpawnPlan>>();
        public static AEDV02SupplySource ReadV02Supply(NetworkRunner runner, Guid match)
        {
            if (_v02Supply == null || _v02Supply.MatchId != match || _v02Runner != runner ||
                V02Reservations.Values.SelectMany(x => x).Any(x => x.Point == null)) return null;
            return new AEDV02SupplySource { InitialOptionalBudget = _v02Supply.InitialOptionalBudget,
                OptionalBudget = _v02Supply.OptionalBudget, OptionalMaterialized = _v02Supply.OptionalMaterialized,
                SpawnedZones = Enumerable.Range(1, 3).Where(_v02Supply.HasSpawned).ToArray(),
                Reservations = V02Reservations.Where(x => !_v02Supply.HasSpawned(x.Key))
                    .SelectMany(x => x.Value.Take(5 + _v02Supply.OptionalForZone(x.Key)).Select(item => new AEDV02SupplyReservation
                    { Zone = x.Key, ToolId = item.ToolId, MarkerId = AEDV02SceneSource.MarkerId(item.Point.transform) })).ToArray() };
        }
        public static bool CanSetV02SupplyBudget(NetworkRunner runner, Guid match, int budget) =>
            _v02Supply != null && _v02Supply.MatchId == match && _v02Runner == runner && _v02Supply.CanSetOptionalBudget(budget);
        private static bool IsV02Occupied(Vector3 position, IEnumerable<Transform> actors) => actors.Any(actor =>
            actor.GetComponentsInChildren<Collider>(true).Any(body => body.enabled && !body.isTrigger && body.bounds.Contains(position)) ||
            actor.TryGetComponent<CharacterController>(out var capsule) && new Bounds(actor.TransformPoint(capsule.center),
                Vector3.Scale(new Vector3(capsule.radius * 2, capsule.height, capsule.radius * 2), actor.lossyScale)).Contains(position));
        public static void ResetV02(Guid match)
        {
            if (_v02Supply != null && match != Guid.Empty && match != _v02Supply.MatchId) return;
            _v02Supply = null; _v02Runner = null; V02Reservations.Clear();
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
                plan.Get(AEDV02Key.ResourceGrade) != 0 || !AEDV02SceneSource.TryLoad(out var manifest, out _) ||
                !UnityEngine.SceneManagement.SceneManager.GetSceneByPath(manifest.ScenePath).isLoaded) return false;
            if (_v02Supply == null || _v02Supply.MatchId != match || _v02Runner != runner)
            {
                ResetV02(Guid.Empty);
                _v02Supply = new AEDV02SupplyLedger(match, (int)plan.Get(AEDV02Key.OptionalToolBudget)); _v02Runner = runner;
                var points = UnityEngine.Object.FindObjectsByType<TeamToolSpawnPoint>(FindObjectsInactive.Exclude)
                    .Where(x => x.gameObject.scene.path == manifest.ScenePath).ToArray();
                for (int reservedZone = 1; reservedZone <= 3; reservedZone++)
                {
                    int count = RequiredToolCountPerZone + _v02Supply.OptionalForZone(reservedZone);
                    if (!TryBuildZonePlan(catalog, points, (TeamToolSpawnZone)reservedZone, count, spacing, true, out var reserved))
                    { ResetV02(match); return false; }
                    V02Reservations.Add(reservedZone, reserved);
                }
            }
            if (!_v02Supply.TrySetOptionalBudget((int)plan.Get(AEDV02Key.OptionalToolBudget))) return false;
            if (_v02Supply.HasSpawned(zone)) return true;
            var plans = V02Reservations[zone].Take(RequiredToolCountPerZone + _v02Supply.OptionalForZone(zone)).ToArray();
            if (!AEDV02SceneSource.TryGetDoorSources(runner, NetworkMatchState.Instance, out var doors)) return false;
            var entry = manifest.Markers.Single(x => x.Id == "Z" + zone + "_ENTRY");
            var entryPosition = new Vector3((float)entry.X, (float)entry.Y, (float)entry.Z);
            var actors = UnityEngine.Object.FindObjectsByType<NetworkBehaviour>()
                .Where(x => x.Runner == runner && (x is NetworkPlayerLifeState || x is EchoProtocol.AI.Stalker.Networking.StalkerFusionRuntime ||
                    x is EchoProtocol.AI.Minions.CreepMinionRuntime)).Select(x => x.transform).ToArray();
            bool Occupied(Vector3 position) => IsV02Occupied(position, actors);
            bool CanPlace(TeamToolSpawnPoint point) => point != null && point.TryGetReachablePosition(entryPosition, out var position, out _) &&
                !HasNearbyWorldTeamTool(position, spacing) && !Occupied(position) &&
                manifest.HasReachableGeometry(new[] { entry.X, entry.Y, entry.Z }, new[] { (double)position.x, position.y, position.z }, doors);
            if (plans.Any(x => !CanPlace(x.Point)))
            {
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
                    !manifest.HasReachableGeometry(new[] { entry.X, entry.Y, entry.Z }, new[] { (double)current.x, current.y, current.z }, doors)) return false;
            var spawned = new List<NetworkObject>();
            foreach (var item in plans)
            {
                var instance = SpawnPlannedTool(runner, item);
                if (instance == null) { RollbackSpawned(runner, spawned); return false; }
                spawned.Add(instance);
            }
            if (!_v02Supply.TryRecordZoneSpawn(zone, RequiredToolCountPerZone, plans.Length - RequiredToolCountPerZone))
            { RollbackSpawned(runner, spawned); return false; }
            for (int i = 0; i < spawned.Count; i++)
                MatchAuthorityRuntime.Instance?.RecordPhase8ToolSpawn(spawned[i].Id, plans[i].ToolId, zone, runner.Tick.Raw);
            Debug.Log($"[AED_V02][ZONE_TOOL_SPAWN] match={match:D} zone={zone} required=5 optional={plans.Length - 5} budget={_v02Supply.OptionalBudget}");
            return true;
        }
    }
}
