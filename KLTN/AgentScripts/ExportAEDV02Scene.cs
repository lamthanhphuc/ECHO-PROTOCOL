using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using EchoProtocol.AI.AED;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Networking;
using EchoProtocol.TeamTools;
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
            var all = roots.SelectMany(x => x.GetComponentsInChildren<Transform>(true)).ToArray();
            var zone1 = all.Single(x => x.name == "AED_Z1_RESOURCE_ENTRY").position;
            var zone2 = roots.SelectMany(x => x.GetComponentsInChildren<StalkerZone2EntryTrigger>(true)).Single().transform.position;
            // Scene authoring uses the existing Zone3 teleport position; no gameplay constant is changed.
            var zone3 = new Vector3(88.66215f, 2.080028f, -399.027f);
            var entries = new[] { zone1, zone2, zone3 };
            var markers = entries.Select((x, i) => Marker("Z" + (i + 1) + "_ENTRY", i + 1, "ENTRY", x)).ToList();
            markers.AddRange(points.Select(x => Marker(AEDV02SceneSource.MarkerId(x.transform), (int)x.Zone, "TOOL", x.transform.position,
                new[] { 1, 2, 3, 4, 6 }.Where(x.Allows).ToArray())));
            foreach (var pickup in roots.SelectMany(x => x.GetComponentsInChildren<NetworkTeamToolPickup>(true)))
            {
                var nearest = points.OrderBy(x => (x.transform.position - pickup.transform.position).sqrMagnitude).First();
                markers.Add(Marker(AEDV02SceneSource.MarkerId(pickup.transform), (int)nearest.Zone, "TOOL", pickup.transform.position, new[] { pickup.ToolId }));
            }
            var doors = roots.SelectMany(x => x.GetComponentsInChildren<MonoBehaviour>(true)).Where(x => x is INetworkDoorStateProvider)
                .Select(x => (Id: AEDV02SceneSource.MarkerId(x.transform), Bounds: DoorBounds(x))).ToArray();
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
                ScenePath = path, Markers = markers.ToArray(), NavCells = cells.ToArray(), NavLinks = links.GroupBy(x => (x.From, x.To)).Select(x => x.First()).ToArray() };
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
            return "fingerprint=" + AEDV02Plan.Hash(json) + " markers=" + markers.Count + " triangles=" + cells.Count + " links=" + data.NavLinks.Length + " doors=" + doors.Length;
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
        if (blocker != null)
        {
            var bounds = new Bounds(blocker.transform.TransformPoint(blocker.center), Vector3.zero);
            foreach (int x in new[] { -1, 1 }) foreach (int y in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
                bounds.Encapsulate(blocker.transform.TransformPoint(blocker.center + Vector3.Scale(blocker.size * .5f, new Vector3(x, y, z))));
            return bounds;
        }
        var colliders = door.GetComponentsInChildren<Collider>(true).Where(x => !x.isTrigger).ToArray();
        if (colliders.Length == 0) throw new InvalidOperationException("Door has no authored traversal collider: " + door.name);
        var result = colliders[0].bounds; foreach (var collider in colliders.Skip(1)) result.Encapsulate(collider.bounds);
        return result;
    }
}
