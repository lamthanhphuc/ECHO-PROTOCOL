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
    public static string Run() => Export(false);
    public static string AuthorPilotEntriesAndExport() => Export(true);
    private static string Export(bool authorPilotEntries)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Author/export requires a stopped, idle Editor.");
        const string path = "Assets/Scenes/SciFi.unity";
        var scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (!opened && SceneManager.sceneCount != 1) throw new InvalidOperationException("Loaded SciFi export requires an isolated scene.");
        if (!opened && scene.isDirty) throw new InvalidOperationException("Preserve unsaved scene edits before author/export.");
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
            var safeRooms = roots.SelectMany(x => x.GetComponentsInChildren<AEDV02SafeRoomMarker>(true))
                .Where(x => x.gameObject.scene == scene).ToArray();
            if (safeRooms.Length == 0 || safeRooms.Any(x => string.IsNullOrWhiteSpace(x.StableTransformIdentity)) ||
                safeRooms.Select(x => x.StableTransformIdentity).Distinct(StringComparer.Ordinal).Count() != safeRooms.Length)
                throw new InvalidOperationException("SciFi must contain a non-empty registry of uniquely identified Safe Rooms.");
            foreach (var safeRoom in safeRooms)
            {
                if (safeRoom.Zone is < 1 or > 3 || !safeRoom.TryGetWorldBounds(out var min, out var max))
                    throw new InvalidOperationException("A Safe Room has an invalid zone, transform identity, or world-space bounds.");
                string id = AEDV02Plan.Hash(path + "|SAFE_ROOM|" + safeRoom.StableTransformIdentity);
                var marker = Marker(id, safeRoom.Zone, "SAFE_ROOM", (min + max) * .5f);
                marker.InteractionBounds = new[] { (double)min.x, min.y, min.z, max.x, max.y, max.z };
                markers.Add(marker);
            }
            if (markers.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != markers.Count)
                throw new InvalidOperationException("Scene marker IDs must be unique.");
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
            if (authorPilotEntries)
            {
                var root = roots.SingleOrDefault(x => x.name == "AEDRev02PilotEntries");
                if (root == null) { root = new GameObject("AEDRev02PilotEntries"); SceneManager.MoveGameObjectToScene(root, scene); }
                var mesh = NavMesh.CalculateTriangulation();
                var candidates = Enumerable.Range(0, mesh.indices.Length / 3).Select(i =>
                    (mesh.vertices[mesh.indices[i * 3]] + mesh.vertices[mesh.indices[i * 3 + 1]] + mesh.vertices[mesh.indices[i * 3 + 2]]) / 3f)
                    .Select(p => new { Position = p, Zone = ZoneAt(p) }).ToArray();
                foreach (var spot in roots.SelectMany(x => x.GetComponentsInChildren<global::HidingSpot>(true)).OrderBy(x => x.StableId))
                {
                    if (spot.InspectPoint == null || spot.ExitPoint == null || spot.transform.parent?.GetComponentInParent<global::HidingSpot>() != null ||
                        !NavMesh.SamplePosition(spot.InspectPoint.position, out var inspect, 1.5f, NavMesh.AllAreas) ||
                        !NavMesh.SamplePosition(spot.ExitPoint.position, out var exit, 1.5f, NavMesh.AllAreas)) continue;
                    int zone = ZoneAt(inspect.position);
                    if (zone is not (1 or 2)) continue;
                    var escape = new NavMeshPath();
                    if (!NavMesh.CalculatePath(exit.position, entries[zone - 1], NavMesh.AllAreas, escape) || escape.status != NavMeshPathStatus.PathComplete) continue;
                    string name = "Cabinet_" + spot.StableId;
                    var existing = root.transform.Find(name);
                    if (existing != null && (existing.GetComponent<StalkerJumpEntryPoint>() == null ||
                        existing.GetComponent<StalkerJumpEntryPoint>().StableId != "AED_REV02_" + name))
                        throw new InvalidOperationException("Preserve non-exporter entry: " + name);
                    foreach (var point in candidates.Where(x => x.Zone == zone && Vector3.Distance(x.Position, inspect.position) >= 8 && Vector3.Distance(x.Position, inspect.position) <= 12)
                        .OrderBy(x => Mathf.Abs(Vector3.Distance(x.Position, inspect.position) - 10)).Select(x => x.Position))
                    {
                        var node = spatial.Nodes.OrderBy(x => (x.Position - point).sqrMagnitude).ThenBy(x => x.Id).First();
                        if (!graph.TryGetNodeSemanticMetadata(node.Id, out var semantic) || semantic.Kind.ToString().IndexOf("Safe", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        var approach = new NavMeshPath();
                        if (!NavMesh.CalculatePath(point, inspect.position, NavMesh.AllAreas, approach) || approach.status != NavMeshPathStatus.PathComplete) continue;
                        float length = 0; for (int i = 1; i < approach.corners.Length; i++) length += Vector3.Distance(approach.corners[i - 1], approach.corners[i]);
                        if (length < 8 || length > 12) continue;
                        var authoredEntry = existing != null ? existing.gameObject : new GameObject(name); authoredEntry.transform.SetParent(root.transform, false);
                        authoredEntry.transform.position = point;
                        var facing = Vector3.ProjectOnPlane(inspect.position - point, Vector3.up);
                        if (facing.sqrMagnitude > .001f) authoredEntry.transform.rotation = Quaternion.LookRotation(facing);
                        var component = authoredEntry.GetComponent<StalkerJumpEntryPoint>() ?? authoredEntry.AddComponent<StalkerJumpEntryPoint>(); var serialized = new SerializedObject(component);
                        serialized.FindProperty("stableId").stringValue = "AED_REV02_" + name;
                        serialized.FindProperty("minPlayerDistance").floatValue = 8; serialized.FindProperty("maxPlayerDistance").floatValue = 12;
                        serialized.ApplyModifiedPropertiesWithoutUndo(); break;
                    }
                }
                if (!root.GetComponentsInChildren<StalkerJumpEntryPoint>().Any(x => ZoneAt(x.EntryPosition) == 1) ||
                    !root.GetComponentsInChildren<StalkerJumpEntryPoint>().Any(x => ZoneAt(x.EntryPosition) == 2))
                    throw new InvalidOperationException("No eligible authored Cabinet approach in both pilot zones; scene not saved.");
                // A separate root is absent from the legacy Special registry; Fixed entry selection is unchanged.
                EditorSceneManager.MarkSceneDirty(scene);
                roots = scene.GetRootGameObjects();
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
            var scanner = playerPrefab.GetComponent<NetworkFieldScanner>();
            if (scanner == null) throw new InvalidOperationException("Production Field Scanner is missing.");
            var scan = new SerializedObject(scanner).FindProperty("_tuning");
            if (scan == null) throw new InvalidOperationException("Production Field Scanner tuning is missing.");
            var jammerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Network/PF_DoorJammer.prefab");
            var jammer = jammerPrefab != null ? jammerPrefab.GetComponent<NetworkDoorJammer>() : null;
            if (jammer == null) throw new InvalidOperationException("Production Door Jammer is missing.");
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
            var data = new AEDV02SceneManifestData { SceneVersion = "SCIFI_NAV_REV02_SAFE_ROOM_" + AssetDatabase.AssetPathToGUID(path),
                ScenePath = path, MaximumVisionRange = maximumVisionRange, MaximumCabinetInteractionDistance = maximumInteractionDistance,
                MinimumToolSpacing = spacing.floatValue,
                CoreStabilizerRadius = CoreStabilizerRules.SupportRadius, CoreStabilizerDurationSeconds = CoreStabilizerRules.DurationSeconds,
                JammerBreakSeconds = jammer.BreakDurationSeconds,
                ScannerCoreRange = Math.Max(100, scan.FindPropertyRelative("CoreRange").floatValue),
                ScannerMotionRange = Math.Max(100, scan.FindPropertyRelative("MotionRange").floatValue),
                ScannerMovingThreshold = scan.FindPropertyRelative("MovingSpeedThreshold").floatValue,
                ScannerOcclusionMultiplier = scan.FindPropertyRelative("OccludedSignalMultiplier").floatValue,
                ScannerMaxTargets = scan.FindPropertyRelative("MaxMotionTargets").intValue,
                ScannerActiveSeconds = Math.Max(20, scan.FindPropertyRelative("ActiveDuration").floatValue),
                ScannerCycleSeconds = Math.Max(20, scan.FindPropertyRelative("ActiveDuration").floatValue) +
                    (scan.FindPropertyRelative("ScanCooldown").floatValue < 10 ? 30 : scan.FindPropertyRelative("ScanCooldown").floatValue),
                DoorIds = doors.Select(x => x.Id).Distinct().ToArray(), Markers = markers.ToArray(), NavCells = cells.ToArray(),
                NavLinks = links.GroupBy(x => (x.From, x.To)).Select(x => x.First()).ToArray() };
            if (!data.ToModel().IsValid()) throw new InvalidOperationException("Exported graph failed independent structural validation.");
            if (authorPilotEntries && !EditorSceneManager.SaveScene(scene)) throw new IOException("Pilot scene could not be saved.");
            var json = JsonUtility.ToJson(data);
            var resource = Path.GetFullPath(Path.Combine(Application.dataPath, "Resources/AED/Rev02SceneManifest.json"));
            var backend = Path.GetFullPath(Path.Combine(Application.dataPath, "../../EchoProtocol.Backend/src/EchoProtocol.Api/Content/AEDV02/Rev02SceneManifest.json"));
            foreach (var target in new[] { resource, backend })
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.WriteAllText(target, json, new UTF8Encoding(false));
            }
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
