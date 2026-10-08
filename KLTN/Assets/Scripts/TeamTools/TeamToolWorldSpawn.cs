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

        private static readonly int[] AEDSupportIds =
        {
            LobbyPlayerState.FirstAidKitToolId,
            LobbyPlayerState.NoiseMakerToolId,
            LobbyPlayerState.DoorJammerToolId,
        };

        public const int RequiredToolCountPerZone = 5;

        private readonly struct SpawnPlan
        {
            public SpawnPlan(
                TeamToolSpawnPoint point,
                Vector3 position,
                NetworkObject prefab,
                int toolId)
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

        public static bool TrySpawnInitial(
            NetworkRunner runner,
            TeamToolPickupCatalog catalog,
            int zone1Count,
            int zone2Count,
            int zone3Count,
            float minimumSpacing,
            bool useAEDSupportPool = false)
        {
            if (runner == null
                || catalog == null
                || !runner.IsServer)
            {
                return false;
            }

            TeamToolSpawnPoint[] points =
                Object.FindObjectsByType<
                    TeamToolSpawnPoint>(
                    FindObjectsInactive.Exclude);

            int resolvedZone1Count =
                Mathf.Max(
                    RequiredToolCountPerZone,
                    zone1Count);

            int resolvedZone2Count =
                Mathf.Max(
                    RequiredToolCountPerZone,
                    zone2Count);

            int resolvedZone3Count =
                Mathf.Max(
                    RequiredToolCountPerZone,
                    zone3Count);

            if (!TryBuildZonePlan(
                    catalog,
                    points,
                    TeamToolSpawnZone.Zone1,
                    resolvedZone1Count,
                    minimumSpacing,
                    useAEDSupportPool,
                    out var zone1Plans))
            {
                Debug.LogError(
                    "[TeamToolWorldSpawn] " +
                    "Zone1 planning failed. " +
                    "No runtime Team Tools were spawned.");

                return false;
            }

            if (!TryBuildZonePlan(
                    catalog,
                    points,
                    TeamToolSpawnZone.Zone2,
                    resolvedZone2Count,
                    minimumSpacing,
                    useAEDSupportPool,
                    out var zone2Plans))
            {
                Debug.LogError(
                    "[TeamToolWorldSpawn] " +
                    "Zone2 planning failed. " +
                    "No runtime Team Tools were spawned.");

                return false;
            }

            if (!TryBuildZonePlan(
                    catalog,
                    points,
                    TeamToolSpawnZone.Zone3,
                    resolvedZone3Count,
                    minimumSpacing,
                    useAEDSupportPool,
                    out var zone3Plans))
            {
                Debug.LogError(
                    "[TeamToolWorldSpawn] " +
                    "Zone3 planning failed. " +
                    "No runtime Team Tools were spawned.");

                return false;
            }

            var completePlan =
                new List<SpawnPlan>(
                    zone1Plans.Count
                    + zone2Plans.Count
                    + zone3Plans.Count);

            completePlan.AddRange(zone1Plans);
            completePlan.AddRange(zone2Plans);
            completePlan.AddRange(zone3Plans);

            var spawnedObjects =
                new List<NetworkObject>(
                    completePlan.Count);

            for (int i = 0;
                 i < completePlan.Count;
                 i++)
            {
                SpawnPlan plan =
                    completePlan[i];

                NetworkObject spawned =
                    SpawnPlannedTool(
                        runner,
                        plan);

                if (spawned != null)
                {
                    spawnedObjects.Add(spawned);
                    continue;
                }

                RollbackSpawned(
                    runner,
                    spawnedObjects);

                Debug.LogError(
                    "[TeamToolWorldSpawn] " +
                    $"Batch spawn failed at " +
                    $"toolId={plan.ToolId}, " +
                    $"zone={plan.Point.Zone}, " +
                    $"room={plan.Point.RoomId}. " +
                    "All runtime Team Tools " +
                    "were rolled back.");

                return false;
            }

            return true;
        }

        private static bool TryBuildZonePlan(
            TeamToolPickupCatalog catalog,
            TeamToolSpawnPoint[] points,
            TeamToolSpawnZone zone,
            int targetCount,
            float spacing,
            bool useAEDSupportPool,
            out List<SpawnPlan> plans)
        {
            plans =
                new List<SpawnPlan>(
                    targetCount);

            var usedPoints =
                new HashSet<TeamToolSpawnPoint>();

            var positions =
                new List<Vector3>();

            var roomUseCount =
                new Dictionary<int, int>();

            var required =
                new List<int>(
                    RequiredToolIds);

            Shuffle(required);

            for (int i = 0;
                 i < required.Count;
                 i++)
            {
                int toolId =
                    required[i];

                if (!TryPlanTool(
                        catalog,
                        points,
                        zone,
                        toolId,
                        usedPoints,
                        positions,
                        roomUseCount,
                        spacing,
                        out var plan))
                {
                    Debug.LogError(
                        "[TeamToolWorldSpawn] " +
                        $"{zone} cannot plan " +
                        $"required toolId={toolId}.");

                    plans.Clear();
                    return false;
                }

                plans.Add(plan);
            }

            while (plans.Count < targetCount)
            {
                var extras =
                    new List<int>(
                        useAEDSupportPool ? AEDSupportIds : RequiredToolIds);

                Shuffle(extras);

                bool added = false;

                for (int i = 0;
                     i < extras.Count;
                     i++)
                {
                    int toolId =
                        extras[i];

                    if (!TryPlanTool(
                            catalog,
                            points,
                            zone,
                            toolId,
                            usedPoints,
                            positions,
                            roomUseCount,
                            spacing,
                            out var plan))
                    {
                        continue;
                    }

                    plans.Add(plan);
                    added = true;
                    break;
                }

                if (added)
                {
                    continue;
                }

                // An optional AED support item must never invalidate the
                // guaranteed five tools or roll back a whole zone spawn.
                if (useAEDSupportPool)
                {
                    Debug.LogWarning("[TeamToolWorldSpawn] Optional AED support slots unavailable; retaining guaranteed tools.");
                    return plans.Count >= RequiredToolCountPerZone;
                }

                Debug.LogError(
                    "[TeamToolWorldSpawn] " +
                    $"{zone} planned " +
                    $"{plans.Count}/{targetCount} " +
                    "Team Tools.");

                plans.Clear();
                return false;
            }

            return plans.Count == targetCount;
        }

        private static bool TryPlanTool(
            TeamToolPickupCatalog catalog,
            TeamToolSpawnPoint[] points,
            TeamToolSpawnZone zone,
            int toolId,
            HashSet<TeamToolSpawnPoint> usedPoints,
            List<Vector3> positions,
            Dictionary<int, int> roomUseCount,
            float spacing,
            out SpawnPlan plan)
        {
            plan = default;

            NetworkObject prefab =
                catalog.GetPrefab(toolId);

            if (!IsCorrectTool(
                    prefab,
                    toolId))
            {
                return false;
            }

            var candidates =
                new List<TeamToolSpawnPoint>();

            int minimumRoomUse =
                int.MaxValue;

            for (int i = 0;
                 i < points.Length;
                 i++)
            {
                TeamToolSpawnPoint point =
                    points[i];

                if (point == null
                    || point.Zone != zone
                    || point.RoomId <= 0
                    || usedPoints.Contains(point)
                    || !point.Allows(toolId)
                    || !point.TryGetSpawnPosition(
                        out Vector3 position)
                    || !IsFarEnough(
                        position,
                        positions,
                        spacing)
                    || HasNearbyWorldTeamTool(
                        position,
                        spacing))
                {
                    continue;
                }

                roomUseCount.TryGetValue(
                    point.RoomId,
                    out int count);

                if (count < minimumRoomUse)
                {
                    minimumRoomUse = count;
                    candidates.Clear();
                }

                if (count == minimumRoomUse)
                {
                    candidates.Add(point);
                }
            }

            while (candidates.Count > 0)
            {
                int index =
                    Random.Range(
                        0,
                        candidates.Count);

                TeamToolSpawnPoint point =
                    candidates[index];

                candidates.RemoveAt(index);

                if (!point.TryGetSpawnPosition(
                        out Vector3 position))
                {
                    continue;
                }

                usedPoints.Add(point);
                positions.Add(position);

                roomUseCount.TryGetValue(
                    point.RoomId,
                    out int roomCount);

                roomUseCount[point.RoomId] =
                    roomCount + 1;

                plan =
                    new SpawnPlan(
                        point,
                        position,
                        prefab,
                        toolId);

                return true;
            }

            return false;
        }

        private static NetworkObject
            SpawnPlannedTool(
                NetworkRunner runner,
                SpawnPlan plan)
        {
            Quaternion rotation =
                plan.ToolId ==
                LobbyPlayerState.FieldScannerToolId
                    ? Quaternion.Euler(
                        90f,
                        plan.Point.transform
                            .eulerAngles.y,
                        0f)
                    : plan.Point.transform.rotation;

            NetworkObject spawned =
                runner.Spawn(
                    plan.Prefab,
                    plan.Position,
                    rotation);

            if (spawned == null)
            {
                return null;
            }

            if (IsCorrectTool(
                    spawned,
                    plan.ToolId))
            {
                return spawned;
            }

            runner.Despawn(spawned);
            return null;
        }

        private static void RollbackSpawned(
            NetworkRunner runner,
            List<NetworkObject> spawnedObjects)
        {
            for (int i =
                     spawnedObjects.Count - 1;
                 i >= 0;
                 i--)
            {
                NetworkObject spawned =
                    spawnedObjects[i];

                if (spawned == null
                    || spawned.Runner != runner)
                {
                    continue;
                }

                runner.Despawn(spawned);
            }

            spawnedObjects.Clear();
        }

        private static bool IsCorrectTool(
            NetworkObject prefab,
            int toolId)
        {
            if (prefab == null)
            {
                return false;
            }

            if (prefab.TryGetComponent<
                    NetworkTeamToolPickup>(
                    out var teamTool))
            {
                return teamTool.ToolId ==
                    toolId;
            }

            return prefab.TryGetComponent<
                       NetworkToolPickup>(
                       out var tool)
                   && tool.ToolId ==
                   toolId;
        }

        private static bool IsFarEnough(
            Vector3 position,
            List<Vector3> positions,
            float spacing)
        {
            float sqrSpacing =
                spacing * spacing;

            for (int i = 0;
                 i < positions.Count;
                 i++)
            {
                if ((positions[i]
                     - position)
                    .sqrMagnitude
                    < sqrSpacing)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool
            HasNearbyWorldTeamTool(
                Vector3 position,
                float radius)
        {
            if (radius <= 0f)
            {
                return false;
            }

            Collider[] colliders =
                Physics.OverlapSphere(
                    position,
                    radius,
                    Physics.AllLayers,
                    QueryTriggerInteraction.Collide);

            for (int i = 0;
                 i < colliders.Length;
                 i++)
            {
                Collider collider =
                    colliders[i];

                if (collider == null)
                {
                    continue;
                }

                if (collider.GetComponentInParent<
                        NetworkTeamToolPickup>()
                    != null)
                {
                    return true;
                }

                if (collider.GetComponentInParent<
                        NetworkToolPickup>()
                    != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Shuffle(
            List<int> values)
        {
            for (int i =
                     values.Count - 1;
                 i > 0;
                 i--)
            {
                int swap =
                    Random.Range(
                        0,
                        i + 1);

                (values[i], values[swap]) =
                    (values[swap], values[i]);
            }
        }
    }
}
