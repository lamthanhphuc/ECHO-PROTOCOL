using System.Collections.Generic;
using EchoProtocol.Networking;
using EchoProtocol.Tools.Scanner;
using Fusion;
using UnityEngine;

namespace EchoProtocol.TeamTools
{
    public static class TeamToolWorldSpawn
    {
        private static readonly int[] RequiredToolIds =
        {
            LobbyPlayerState.FieldScannerToolId,
            LobbyPlayerState.NoiseMakerToolId,
            LobbyPlayerState.FirstAidKitToolId,
            LobbyPlayerState.DoorJammerToolId,
            LobbyPlayerState.CoreStabilizerToolId,
        };

        public const int RequiredToolCountPerZone = 5;

        private readonly struct SpawnPlan
        {
            public SpawnPlan(TeamToolSpawnPoint point, Vector3 position, NetworkObject prefab, int toolId)
            {
                Point = point;
                Position = position;
                Prefab = prefab;
                ToolId = toolId;
            }

            public TeamToolSpawnPoint Point { get; }
            public Vector3 Position { get; }
            public NetworkObject Prefab { get; }
            public int ToolId { get; }
        }

        public static int SpawnInitial(NetworkRunner runner, TeamToolPickupCatalog catalog,
            int zone1Count, int zone2Count, float minimumSpacing)
        {
            if (runner == null || catalog == null || !runner.IsServer) return 0;

            var points = Object.FindObjectsByType<TeamToolSpawnPoint>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            return SpawnZone(runner, catalog, points, TeamToolSpawnZone.Zone1,
                       Mathf.Max(RequiredToolCountPerZone, zone1Count), minimumSpacing)
                + SpawnZone(runner, catalog, points, TeamToolSpawnZone.Zone2,
                    Mathf.Max(RequiredToolCountPerZone, zone2Count), minimumSpacing);
        }

        private static int SpawnZone(NetworkRunner runner, TeamToolPickupCatalog catalog,
            TeamToolSpawnPoint[] points, TeamToolSpawnZone zone, int targetCount, float spacing)
        {
            var used = new HashSet<TeamToolSpawnPoint>();
            var positions = new List<Vector3>();
            var roomUseCount = new Dictionary<int, int>();
            var required = new List<int>(RequiredToolIds);
            var requiredPlans = new List<SpawnPlan>(RequiredToolCountPerZone);
            Shuffle(required);

            foreach (int toolId in required)
            {
                if (TryPlanTool(catalog, points, zone, toolId, used, positions,
                        roomUseCount, spacing, out var plan)) requiredPlans.Add(plan);
                else
                {
                    Debug.LogError($"[TeamToolWorldSpawn] Zone {zone} could not plan all required Team Tools. Missing id={toolId}. Check room spawn points.");
                    return 0;
                }
            }

            int spawned = 0;
            var requiredObjects = new List<NetworkObject>(requiredPlans.Count);
            foreach (var plan in requiredPlans)
            {
                var spawnedObject = SpawnPlannedTool(runner, plan);
                if (spawnedObject == null)
                {
                    foreach (var requiredObject in requiredObjects)
                        if (requiredObject != null) runner.Despawn(requiredObject);
                    Debug.LogError($"[TeamToolWorldSpawn] Zone {zone} failed to spawn required Team Tool id={plan.ToolId}. Required batch rolled back.");
                    return 0;
                }
                requiredObjects.Add(spawnedObject);
                spawned++;
            }

            for (int extra = RequiredToolCountPerZone; extra < targetCount; extra++)
            {
                var extras = new List<int>(RequiredToolIds);
                Shuffle(extras);
                bool added = false;
                foreach (int toolId in extras)
                {
                    if (!TryPlanTool(catalog, points, zone, toolId, used, positions,
                            roomUseCount, spacing, out var plan)) continue;
                    if (SpawnPlannedTool(runner, plan) == null) continue;
                    spawned++;
                    added = true;
                    break;
                }
                if (!added) break;
            }
            return spawned;
        }

        private static bool TryPlanTool(TeamToolPickupCatalog catalog,
            TeamToolSpawnPoint[] points, TeamToolSpawnZone zone, int toolId,
            HashSet<TeamToolSpawnPoint> used, List<Vector3> positions,
            Dictionary<int, int> roomUseCount, float spacing, out SpawnPlan plan)
        {
            plan = default;
            var prefab = catalog.GetPrefab(toolId);
            if (!IsCorrectTool(prefab, toolId)) return false;

            var candidates = new List<TeamToolSpawnPoint>();
            int minimumRoomUse = int.MaxValue;
            foreach (var point in points)
            {
                if (point == null || point.Zone != zone || point.RoomId <= 0 || used.Contains(point)
                    || !point.Allows(toolId) || !point.TryGetSpawnPosition(out var position)
                    || !IsFarEnough(position, positions, spacing)
                    || HasNearbyWorldTeamTool(position, spacing)) continue;

                roomUseCount.TryGetValue(point.RoomId, out int count);
                if (count < minimumRoomUse)
                {
                    minimumRoomUse = count;
                    candidates.Clear();
                }
                if (count == minimumRoomUse) candidates.Add(point);
            }

            while (candidates.Count > 0)
            {
                int index = Random.Range(0, candidates.Count);
                var point = candidates[index];
                candidates.RemoveAt(index);
                if (!point.TryGetSpawnPosition(out var position)) continue;

                used.Add(point);
                positions.Add(position);
                roomUseCount.TryGetValue(point.RoomId, out int roomCount);
                roomUseCount[point.RoomId] = roomCount + 1;
                plan = new SpawnPlan(point, position, prefab, toolId);
                return true;
            }
            return false;
        }

        private static NetworkObject SpawnPlannedTool(NetworkRunner runner, SpawnPlan plan)
        {
            var rotation = plan.ToolId == LobbyPlayerState.FieldScannerToolId
                ? Quaternion.Euler(90f, plan.Point.transform.eulerAngles.y, 0f)
                : plan.Point.transform.rotation;
            var spawned = runner.Spawn(plan.Prefab, plan.Position, rotation);
            if (spawned == null) return null;
            if (IsCorrectTool(spawned, plan.ToolId)) return spawned;

            runner.Despawn(spawned);
            return null;
        }

        private static bool IsCorrectTool(NetworkObject prefab, int toolId)
        {
            if (prefab == null) return false;
            if (prefab.TryGetComponent<NetworkTeamToolPickup>(out var teamTool))
                return teamTool.ToolId == toolId;
            return prefab.TryGetComponent<NetworkToolPickup>(out var tool) && tool.ToolId == toolId;
        }

        private static bool IsFarEnough(Vector3 position, List<Vector3> positions, float spacing)
        {
            foreach (var other in positions)
                if ((other - position).sqrMagnitude < spacing * spacing) return false;
            return true;
        }

        private static bool HasNearbyWorldTeamTool(Vector3 position, float radius)
        {
            if (radius <= 0f) return false;
            foreach (var collider in Physics.OverlapSphere(position, radius,
                         Physics.AllLayers, QueryTriggerInteraction.Collide))
            {
                if (collider.GetComponentInParent<NetworkTeamToolPickup>() != null
                    || collider.GetComponentInParent<NetworkToolPickup>() != null) return true;
            }
            return false;
        }

        private static void Shuffle(List<int> values)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int swap = Random.Range(0, i + 1);
                (values[i], values[swap]) = (values[swap], values[i]);
            }
        }
    }
}
