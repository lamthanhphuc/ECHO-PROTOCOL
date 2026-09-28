using System.Collections.Generic;
using EchoProtocol.Networking;
using EchoProtocol.Tools.Scanner;
using Fusion;
using UnityEngine;

namespace EchoProtocol.TeamTools
{
    public static class TeamToolWorldSpawn
    {
        private static readonly int[] OptionalToolIds =
        {
            LobbyPlayerState.FieldScannerToolId,
            LobbyPlayerState.NoiseMakerToolId,
            LobbyPlayerState.DoorJammerToolId,
            LobbyPlayerState.CoreStabilizerToolId,
        };

        public static int SpawnInitial(NetworkRunner runner, TeamToolPickupCatalog catalog,
            int zone1Count, int zone2Count, float minimumSpacing)
        {
            if (runner == null || catalog == null || !runner.IsServer) return 0;

            var points = Object.FindObjectsByType<TeamToolSpawnPoint>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            return SpawnZone(runner, catalog, points, TeamToolSpawnZone.Zone1, zone1Count, minimumSpacing)
                + SpawnZone(runner, catalog, points, TeamToolSpawnZone.Zone2, zone2Count, minimumSpacing);
        }

        private static int SpawnZone(NetworkRunner runner, TeamToolPickupCatalog catalog,
            TeamToolSpawnPoint[] points, TeamToolSpawnZone zone, int count, float spacing)
        {
            if (count <= 0) return 0;

            var used = new HashSet<TeamToolSpawnPoint>();
            var positions = new List<Vector3>();
            if (!TrySpawnTool(runner, catalog, points, zone, LobbyPlayerState.FirstAidKitToolId,
                    used, positions, spacing)) return 0;

            int spawned = 1;
            var remaining = new List<int>(OptionalToolIds);
            while (spawned < count && remaining.Count > 0)
            {
                int index = Random.Range(0, remaining.Count);
                int toolId = remaining[index];
                remaining.RemoveAt(index);
                if (TrySpawnTool(runner, catalog, points, zone, toolId, used, positions, spacing)) spawned++;
            }
            return spawned;
        }

        private static bool TrySpawnTool(NetworkRunner runner, TeamToolPickupCatalog catalog,
            TeamToolSpawnPoint[] points, TeamToolSpawnZone zone, int toolId,
            HashSet<TeamToolSpawnPoint> used, List<Vector3> positions, float spacing)
        {
            var prefab = catalog.GetPrefab(toolId);
            if (!IsCorrectTool(prefab, toolId)) return false;

            var candidates = new List<TeamToolSpawnPoint>();
            foreach (var point in points)
            {
                if (point == null || point.Zone != zone || used.Contains(point)
                    || !point.Allows(toolId) || !point.TryGetSpawnPosition(out var position)
                    || !IsFarEnough(position, positions, spacing)
                    || HasNearbyWorldTeamTool(position, spacing)) continue;
                candidates.Add(point);
            }

            while (candidates.Count > 0)
            {
                int index = Random.Range(0, candidates.Count);
                var point = candidates[index];
                candidates.RemoveAt(index);
                if (!point.TryGetSpawnPosition(out var position)) continue;

                var rotation = toolId == LobbyPlayerState.FieldScannerToolId
                    ? Quaternion.Euler(90f, point.transform.eulerAngles.y, 0f)
                    : point.transform.rotation;
                var spawned = runner.Spawn(prefab, position, rotation);
                if (spawned == null) continue;

                used.Add(point);
                positions.Add(position);
                return true;
            }
            return false;
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
    }
}
