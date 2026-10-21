#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.AI.Stalker.Spatial.Editor
{
    public static class Map2RegionGraphAuthoring
    {
        public const string AssetPath = "Assets/AI/Stalker/Map2/AI_Stalker_Map2_RegionGraph.asset";
        public const string HuntAssetPath = "Assets/AI/Stalker/Map2/AI_Stalker_Map2_Hunt_RegionGraph.asset";

        [MenuItem("Echo Protocol/AI/Stalker/Bake Map2 Region Graph")]
        public static void Bake()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || (scene.path != "Assets/Scenes/Scene_A.unity" && scene.path != "Assets/Scenes/Scene_B.unity"))
                throw new InvalidOperationException("Open Scene_A or Scene_B in Edit Mode before baking its RegionGraph.");
            string assetPath = scene.path == "Assets/Scenes/Scene_B.unity" ? HuntAssetPath : AssetPath;
            if (UnityEngine.Object.FindObjectsByType<Unity.AI.Navigation.NavMeshSurface>()
                .Any(surface => surface.enabled && surface.navMeshData != null && surface.gameObject.scene != scene))
                throw new InvalidOperationException("Unload other scenes with NavMesh surfaces before baking Map2.");
            var marker = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>())
                .Single(item => item.name == "MonsterSpawn_Stalker_EMPTY");
            if (!UnityEngine.AI.NavMesh.SamplePosition(marker.position, out var spawn, 1f, UnityEngine.AI.NavMesh.AllAreas))
                throw new InvalidOperationException("The Map2 Stalker spawn marker must be on NavMesh within 1m.");

            var graph = NavMeshSpatialGraphBuilder.Build();
            var definitions = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RegionDefinition>()).ToArray();
            var path = new UnityEngine.AI.NavMeshPath();
            var report = BuildReport(graph, definitions, spawn.position, position =>
                UnityEngine.AI.NavMesh.CalculatePath(spawn.position, position, UnityEngine.AI.NavMesh.AllAreas, path)
                && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete);
            if (!report.CanBakeRuntimeAsset || RegionGraph.Validate(report.RuntimeGraph, graph) != RegionGraphValidationFailure.None)
                throw new InvalidOperationException(report.ToDisplayString());

            if (!AssetDatabase.IsValidFolder("Assets/AI/Stalker/Map2"))
                AssetDatabase.CreateFolder("Assets/AI/Stalker", "Map2");
            var asset = AssetDatabase.LoadAssetAtPath<RegionGraphAsset>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<RegionGraphAsset>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            asset.ConfigureFromRuntimeGraph(report.RuntimeGraph, graph.NodeCount);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            Debug.Log($"[Map2] Baked {report.RuntimeSemanticRegionCount} regions for {graph.NodeCount} NavMesh nodes: {assetPath}");
        }

        public static FullStationRegionGraphAuthoringCore.DryRunReport BuildReport(
            NavMeshSpatialGraph graph, RegionDefinition[] definitions, Vector3 spawn, Func<Vector3, bool> canReach)
        {
            if (graph == null || graph.IsEmpty) throw new ArgumentException("Map2 requires a baked NavMesh.", nameof(graph));
            if (definitions == null || definitions.Length == 0) throw new ArgumentException("Map2 requires RegionDefinitions.", nameof(definitions));
            if (canReach == null) throw new ArgumentNullException(nameof(canReach));
            var ordered = definitions.OrderBy(region => region.RegionId.Value).ToArray();
            if (ordered.Any(region => !region.RegionId.IsValid) || ordered.Select(region => region.RegionId).Distinct().Count() != ordered.Length)
                throw new ArgumentException("RegionDefinition IDs must be positive and unique.", nameof(definitions));

            // Disconnected NavMesh islands retain the existing Isolated semantics.
            var start = graph.Nodes.OrderBy(node => (node.Position - spawn).sqrMagnitude).First();
            var reachable = new HashSet<int> { start.Id };
            var queue = new Queue<int>();
            queue.Enqueue(start.Id);
            while (queue.Count > 0)
            {
                foreach (var neighbor in graph.Nodes[queue.Dequeue()].NeighborIds)
                    if (reachable.Add(neighbor)) queue.Enqueue(neighbor);
            }
            // Welded triangle adjacency alone does not prove a complete native NavMesh path.
            var rejected = reachable.Where(id => !canReach(graph.Nodes[id].Position)).OrderBy(id => id).ToArray();
            reachable.ExceptWith(rejected);

            var sources = new List<FullStationRegionGraphAuthoringCore.SemanticSource>();
            foreach (var region in ordered)
            {
                SemanticKind kind;
                if (region.name.StartsWith("Room_", StringComparison.Ordinal)) kind = SemanticKind.Room;
                else if (region.name.StartsWith("Route_", StringComparison.Ordinal)) kind = SemanticKind.Route;
                else throw new ArgumentException($"Region '{region.name}' must start with Room_ or Route_.");
                var nodes = graph.Nodes.Where(node => reachable.Contains(node.Id) && region.ContainsWorldPoint(node.Position))
                    .Select(node => node.Id).ToArray();
                if (nodes.Length == 0) continue;
                sources.Add(new FullStationRegionGraphAuthoringCore.SemanticSource(sources.Count, SemanticZone.Zone01,
                    kind, "Map2_Regions/" + region.name, region.ToBakeData().WorldBounds.center, FloorEvidenceKind.FloorEvidence, nodes));
            }
            if (rejected.Length > 0)
                sources.Add(new FullStationRegionGraphAuthoringCore.SemanticSource(sources.Count, SemanticZone.Isolated,
                    SemanticKind.IsolatedIsland, "Map2_Regions/UnreachableNavMesh", spawn, FloorEvidenceKind.IsolatedIsland, rejected));
            var report = FullStationRegionGraphAuthoringCore.BuildDryRunReport(graph, sources,
                FullStationRegionGraphAuthoringCore.BuildOptions.Default);
            if (report.RuntimeGraph != null)
            {
                var regions = report.RuntimeGraph.Regions.ToDictionary(region => region.Id);
                report.RuntimeEdges.RemoveAll(edge => regions[edge.FromRegionId].SemanticMetadata.Zone != regions[edge.ToRegionId].SemanticMetadata.Zone);
                report.RuntimeGraph = new RegionGraph(regions.Values.Select(region => new RegionNode(region.Id,
                    region.Edges.Where(edge => regions[edge.ToRegionId].SemanticMetadata.Zone == region.SemanticMetadata.Zone).ToArray(),
                    region.SemanticMetadata)).OrderBy(region => region.Id.Value).ToArray(), report.RuntimeNodeToRegion,
                    graph.CompatibilityIdentity, report.RuntimeGraph.DefinitionVersion);
            }
            return report;
        }
    }
}
#endif
