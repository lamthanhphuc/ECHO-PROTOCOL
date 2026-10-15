using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using EchoProtocol.AI.AED;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Networking;
using EchoProtocol.TeamTools;
using EchoProtocol.Tools.Scanner;
using EchoProtocol.AI.Stalker;
using EchoProtocol.AI.Stalker.Spatial;
using EchoProtocol.AI.Stalker.Special;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class ExportAEDV02Scene
{
    public static string Run()
    {
        const string path = "Assets/Scenes/SciFi.unity";
        var scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened && NavMesh.CalculateTriangulation().indices.Length != 0) throw new InvalidOperationException("Another scene contributes NavMesh; export must be isolated.");
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var roots = scene.GetRootGameObjects();
            var points = roots.SelectMany(x => x.GetComponentsInChildren<TeamToolSpawnPoint>(true)).ToArray();
            if (points.Length < 15) throw new InvalidOperationException("Required TeamTool content is missing.");
            var authored = roots.SelectMany(x => x.GetComponentsInChildren<AEDZoneEntryMarker>(true)).ToArray();
            var entries = Enumerable.Range(1, 3).Select(zone => authored.Single(x => x.Zone == zone).transform.position).ToArray();
            var markers = entries.Select((x, i) => Marker("Z" + (i + 1) + "_ENTRY", i + 1, "ENTRY", x)).ToList();
            var stalkerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/StalkerNetwork.prefab");
            var controller = stalkerPrefab.GetComponent<StalkerController>();
            var matchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Network/NetworkMatchState.prefab");
            var matchState = matchPrefab != null ? matchPrefab.GetComponent<NetworkMatchState>() : null;
            if (matchState == null) throw new InvalidOperationException("Production NetworkMatchState prefab is missing.");
            var spacing = new SerializedObject(matchState).FindProperty("_teamToolSpawnMinimumSpacing");
            if (spacing == null || !AEDV02PilotPolicy.Finite(spacing.floatValue) || spacing.floatValue < 0)
                throw new InvalidOperationException("Production TeamTool minimum spacing is unavailable.");
            var graphAsset = new SerializedObject(controller).FindProperty("regionGraphAsset").objectReferenceValue as RegionGraphAsset;
            if (graphAsset == null) throw new InvalidOperationException("Production Stalker semantic graph is missing.");
            var graph = graphAsset.BuildRuntimeGraph();
            var spatial = NavMeshSpatialGraphBuilder.Build();
            if (RegionGraph.Validate(graph, spatial) != RegionGraphValidationFailure.None)
                throw new InvalidOperationException("Production Stalker semantic graph does not match the loaded mesh.");
            int ZoneAt(Vector3 position)
            {
                var node = spatial.Nodes.OrderBy(x => (x.Position - position).sqrMagnitude).ThenBy(x => x.Id).First();
                return graph.TryGetNodeSemanticMetadata(node.Id, out var metadata) && (int)metadata.Zone is >= 1 and <= 3 ? (int)metadata.Zone : 0;
            }
            foreach (var spot in roots.SelectMany(x => x.GetComponentsInChildren<global::HidingSpot>(true)))
            {
                if (spot.InspectPoint == null || spot.ExitPoint == null || spot.transform.parent?.GetComponentInParent<global::HidingSpot>() != null) continue;
                if (!NavMesh.SamplePosition(spot.InspectPoint.position, out var inspect, 1.5f, NavMesh.AllAreas)) continue;
                int zone = ZoneAt(inspect.position);
                if (zone == 0) continue;
                var marker = Marker(AEDV02SceneSource.MarkerId(spot.transform), zone, "CABINET", inspect.position);
                var colliders = spot.GetComponentsInChildren<Collider>(true);
                if (colliders.Length == 0) continue;
                var bounds = colliders[0].bounds; foreach (var collider in colliders.Skip(1)) bounds.Encapsulate(collider.bounds);
                marker.InteractionBounds = new[] { (double)bounds.min.x, bounds.min.y, bounds.min.z, bounds.max.x, bounds.max.y, bounds.max.z };
                marker.HideSpotId = spot.StableId.ToString(); markers.Add(marker);
            }
            foreach (var entry in roots.SelectMany(x => x.GetComponentsInChildren<StalkerJumpEntryPoint>(true)))
            {
                int zone = ZoneAt(entry.EntryPosition);
                if (zone != 0 && entry.JumpInAllowed) markers.Add(Marker(AEDV02SceneSource.MarkerId(entry.transform), zone, "AMBUSH_ENTRY", entry.EntryPosition));
            }
            var vision = stalkerPrefab.GetComponent<StalkerVisionSensor>();
            double maximumVisionRange = new SerializedObject(vision).FindProperty("visionDistance").floatValue;
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerNetwork.prefab");
            double maximumInteractionDistance = playerPrefab.GetComponent<global::PlayerInteraction>().InteractionDistance;
            markers.AddRange(points.Select(x => Marker(AEDV02SceneSource.MarkerId(x.transform), (int)x.Zone, "TOOL", x.transform.position,
                new[] { 1, 2, 3, 4, 6 }.Where(x.Allows).ToArray())));
            RegionSemanticKind KindAt(Vector3 position)
            {
                var node = spatial.Nodes.OrderBy(x => (x.Position - position).sqrMagnitude).ThenBy(x => x.Id).First();
                return graph.TryGetNodeSemanticMetadata(node.Id, out var metadata) ? metadata.Kind : RegionSemanticKind.Unknown;
            }
            foreach (int zone in Enumerable.Range(1, 3))
            {
                int firstRoom = points.Where(x => (int)x.Zone == zone && KindAt(x.transform.position) == RegionSemanticKind.Room)
                    .Select(x => new { Point = x, Meters = x.TryGetReachablePosition(entries[zone - 1], out _, out var distance) ? distance : float.PositiveInfinity })
                    .Where(x => !float.IsInfinity(x.Meters)).OrderBy(x => x.Meters).ThenBy(x => x.Point.RoomId)
                    .Select(x => x.Point.RoomId).FirstOrDefault();
                foreach (var point in points.Where(x => (int)x.Zone == zone))
                {
                    var marker = markers.Single(x => x.Id == AEDV02SceneSource.MarkerId(point.transform));
                    var kind = KindAt(point.transform.position);
                    marker.ResourceRole = kind == RegionSemanticKind.Route ? "MainRoute" : kind != RegionSemanticKind.Room ? "Unclassified" :
                        point.RoomId == firstRoom ? "FirstRoom" : "SideRoom";
                }
            }
            foreach (var pickup in roots.SelectMany(x => x.GetComponentsInChildren<NetworkInteractable>(true)).Where(x => x is NetworkTeamToolPickup or NetworkToolPickup))
            {
                var nearest = points.OrderBy(x => (x.transform.position - pickup.transform.position).sqrMagnitude).First();
                int toolId = pickup is NetworkTeamToolPickup teamTool ? teamTool.ToolId : ((NetworkToolPickup)pickup).ToolId;
                markers.Add(Marker(AEDV02SceneSource.MarkerId(pickup.transform), (int)nearest.Zone, "TOOL", pickup.transform.position, new[] { toolId }));
            }
            var doors = roots.SelectMany(x => x.GetComponentsInChildren<MonoBehaviour>(true)).Where(x => x is INetworkDoorStateProvider)
                .Select(x => (Id: AEDV02SceneSource.MarkerId(x.transform), Bounds: DoorBounds(x))).ToList();
            var gates = AEDV02SceneSource.Zone2ExitGates(scene);
            if (gates.Length != 2) throw new InvalidOperationException("Production Zone2 gates are missing.");
            foreach (var gate in gates)
            {
                var wall = gate.parent.Find("LP_Bay_Door_Wall_snaps");
                var blocker = wall?.GetComponents<BoxCollider>().OrderByDescending(x => x.size.z).FirstOrDefault();
                if (blocker == null) throw new InvalidOperationException("Zone2 gate passage collider is missing.");
                doors.Add((AEDV02SceneSource.MarkerId(gate), BoxBounds(blocker)));
            }
            var triangulation = NavMesh.CalculateTriangulation();
            if (triangulation.indices.Length == 0) throw new InvalidOperationException("No baked NavMesh loaded.");
            var cells = new List<AEDV02SceneManifestData.Cell>();
            var edges = new Dictionary<string, List<AEDV02SceneManifestData.Cell>>(StringComparer.Ordinal);
            for (int i = 0; i < triangulation.indices.Length; i += 3)
            {
                var cell = new AEDV02SceneManifestData.Cell { Id = i / 3,
                    A = Position(triangulation.vertices[triangulation.indices[i]]), B = Position(triangulation.vertices[triangulation.indices[i + 1]]),
                    C = Position(triangulation.vertices[triangulation.indices[i + 2]]) };
                var model = new AEDV02NavCell { Id = cell.Id, A = cell.A, B = cell.B, C = cell.C };
                if (!model.IsValid()) continue;
                // Blocking footprints apply inside a triangle too, not only between triangle centres.
                cell.DoorIds = doors.Where(x => model.IntersectsBounds(Position(x.Bounds.min), Position(x.Bounds.max))).Select(x => x.Id).ToArray();
                cells.Add(cell);
                foreach (var pair in new[] { (cell.A, cell.B), (cell.B, cell.C), (cell.C, cell.A) })
                {
                    string a = Key(pair.Item1), b = Key(pair.Item2);
                    var key = string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;
                    if (!edges.TryGetValue(key, out var touching)) edges.Add(key, touching = new List<AEDV02SceneManifestData.Cell>());
                    touching.Add(cell);
                }
            }
            var links = new List<AEDV02SceneManifestData.Link>();
            foreach (var portal in edges.Values.Where(x => x.Count > 1))
                foreach (var a in portal) foreach (var b in portal.Where(x => x.Id != a.Id))
                {
                    var start = Center(a); var end = Center(b); var delta = end - start;
                    var crossed = doors.Where(x => x.Bounds.IntersectRay(new Ray(start, delta.normalized), out float distance) && distance <= delta.magnitude)
                        .Select(x => x.Id).ToArray();
                    links.Add(new AEDV02SceneManifestData.Link { From = a.Id, To = b.Id, DoorIds = crossed });
                }
            var data = new AEDV02SceneManifestData { SceneVersion = "SCIFI_NAV_REV02_" + AssetDatabase.AssetPathToGUID(path),
                ScenePath = path, MaximumVisionRange = maximumVisionRange, MaximumCabinetInteractionDistance = maximumInteractionDistance,
                MinimumToolSpacing = spacing.floatValue,
                DoorIds = doors.Select(x => x.Id).Distinct().ToArray(), Markers = markers.ToArray(), NavCells = cells.ToArray(),
                NavLinks = links.GroupBy(x => (x.From, x.To)).Select(x => x.First()).ToArray() };
            if (!data.ToModel().IsValid()) throw new InvalidOperationException("Exported graph failed independent structural validation.");
            var json = JsonUtility.ToJson(data);
            var resource = Path.GetFullPath(Path.Combine(Application.dataPath, "Resources/AED/Rev02SceneManifest.json"));
            var backend = Path.GetFullPath(Path.Combine(Application.dataPath, "../../EchoProtocol.Backend/src/EchoProtocol.Api/Content/AEDV02/Rev02SceneManifest.json"));
            foreach (var target in new[] { resource, backend })
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.WriteAllText(target, json, new UTF8Encoding(false));
            }
            AssetDatabase.Refresh();
            return "fingerprint=" + AEDV02Plan.Hash(json) + " markers=" + markers.Count + " triangles=" + cells.Count + " links=" + data.NavLinks.Length + " doors=" + doors.Count;
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
    private static string Key(double[] p) => string.Join(",", p.Select(x => Math.Round(x, 4).ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
    private static double[] Position(Vector3 p) => new[] { (double)p.x, p.y, p.z };
    private static Vector3 Center(AEDV02SceneManifestData.Cell c) => new Vector3((float)((c.A[0] + c.B[0] + c.C[0]) / 3),
        (float)((c.A[1] + c.B[1] + c.C[1]) / 3), (float)((c.A[2] + c.B[2] + c.C[2]) / 3));
    private static AEDV02SceneManifestData.Marker Marker(string id, int zone, string kind, Vector3 p, int[] tools = null) =>
        new AEDV02SceneManifestData.Marker { Id = id, Zone = zone, Kind = kind, AllowedToolIds = tools ?? Array.Empty<int>(), X = p.x, Y = p.y, Z = p.z };
    private static Bounds DoorBounds(MonoBehaviour door)
    {
        var blocker = new SerializedObject(door).FindProperty("_blockingCollider")?.objectReferenceValue as BoxCollider;
        if (blocker != null) return BoxBounds(blocker);
        var colliders = door.GetComponentsInChildren<Collider>(true).Where(x => !x.isTrigger).ToArray();
        if (colliders.Length == 0) throw new InvalidOperationException("Door has no authored traversal collider: " + door.name);
        var result = colliders[0].bounds; foreach (var collider in colliders.Skip(1)) result.Encapsulate(collider.bounds);
        return result;
    }
    private static Bounds BoxBounds(BoxCollider blocker)
    {
        var bounds = new Bounds(blocker.transform.TransformPoint(blocker.center), Vector3.zero);
        foreach (int x in new[] { -1, 1 }) foreach (int y in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
            bounds.Encapsulate(blocker.transform.TransformPoint(blocker.center + Vector3.Scale(blocker.size * .5f, new Vector3(x, y, z))));
        return bounds;
    }
}
