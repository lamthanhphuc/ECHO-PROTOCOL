#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using EchoProtocol.AI.Common.Spatial;
using EchoProtocol.AI.Stalker.Spatial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.AI.Stalker.Spatial.Editor
{
    public sealed class FullStationRegionGraphAuthoringWindow : EditorWindow
    {
        private const string AssetPath = "Assets/AI/Stalker/Station/AI_Stalker_FullStation_RegionGraph.asset";
        private Vector2 _scroll;
        private FullStationRegionGraphAuthoringCore.DryRunReport _lastReport;

        [MenuItem("Echo Protocol/AI/Stalker/Full Station Region Graph Authoring")]
        public static void Open()
        {
            GetWindow<FullStationRegionGraphAuthoringWindow>("Full Station Regions");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Full Station Region Graph Authoring", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Output", AssetPath);
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Dry Run / Validate"))
                {
                    _lastReport = FullStationRegionGraphAuthoringCore.DryRunActiveScene();
                }

                using (new EditorGUI.DisabledScope(_lastReport == null || !_lastReport.IsValid))
                {
                    if (GUILayout.Button("Apply Authoring"))
                    {
                        _lastReport = FullStationRegionGraphAuthoringCore.ApplyAuthoringToActiveScene(_lastReport);
                    }

                    if (GUILayout.Button("Bake Region Graph Asset"))
                    {
                        _lastReport = FullStationRegionGraphAuthoringCore.BakeAsset(AssetPath, _lastReport);
                    }
                }
            }

            EditorGUILayout.Space();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_lastReport != null ? _lastReport.ToDisplayString() : "Run dry-run validation first.");
            EditorGUILayout.EndScrollView();
        }
    }

    public static class FullStationRegionGraphAuthoringCore
    {
        public const int MaxDecompositionDepth = 16;
        private const string NavigationName = "Navigation";
        private const string StalkerRegionsName = "StalkerRegions";
        private const string GeneratedPrefix = "Generated_StationRegion_";
        private const string ProtectedSpatialV3Path = "Assets/AI/Stalker/Phase3/AI_Stalker_SpatialV3_RegionGraph.asset";
        private const float BoundsPadding = 0.05f;
        private const float FloorVerticalTolerance = 1.25f;
        private const float SeedGeometryComparisonEpsilon = 0.001f;
        private const int DefinitionVersion = 1;

        private static readonly SemanticCatalogEntry[] Catalog =
        {
            new SemanticCatalogEntry("Zone01_ResearchStorage", "01_Start_Area_EMPTY", SemanticZone.Zone01, SemanticKind.Room),
            new SemanticCatalogEntry("Zone01_ResearchStorage", "02_Initial_Storage_C1_EMPTY", SemanticZone.Zone01, SemanticKind.Room),
            new SemanticCatalogEntry("Zone01_ResearchStorage", "03_Central_Junction", SemanticZone.Zone01, SemanticKind.Room),
            new SemanticCatalogEntry("Zone01_ResearchStorage", "04_Server_Room_C2_EMPTY", SemanticZone.Zone01, SemanticKind.Room),
            new SemanticCatalogEntry("Zone01_ResearchStorage", "05_Research_Lab_C3_EMPTY", SemanticZone.Zone01, SemanticKind.Room),
            new SemanticCatalogEntry("Zone01_ResearchStorage", "06_Archive_C4_EMPTY", SemanticZone.Zone01, SemanticKind.Room),
            new SemanticCatalogEntry("Zone01_ResearchStorage", "07_Maintenance_C5_EMPTY", SemanticZone.Zone01, SemanticKind.Room),
            new SemanticCatalogEntry("Zone01_ResearchStorage", "08_Warehouse_C6_EMPTY", SemanticZone.Zone01, SemanticKind.Room),
            new SemanticCatalogEntry("Zone01_ResearchStorage", "09_Transition_To_Zone2_EMPTY", SemanticZone.Zone01, SemanticKind.Room),
            new SemanticCatalogEntry("Zone02_PowerEngineering/Rooms", "02_Engineering_Junction_EMPTY", SemanticZone.Zone02, SemanticKind.Room),
            new SemanticCatalogEntry("Zone02_PowerEngineering/Rooms", "03_CoreReceiver_PowerHub_CR_EMPTY", SemanticZone.Zone02, SemanticKind.Room),
            new SemanticCatalogEntry("Zone02_PowerEngineering/Rooms", "04_Power_Control_PC_EMPTY", SemanticZone.Zone02, SemanticKind.Room),
            new SemanticCatalogEntry("Zone02_PowerEngineering/Rooms", "05_Distribution_Panel_DP_EMPTY", SemanticZone.Zone02, SemanticKind.Room),
            new SemanticCatalogEntry("Zone02_PowerEngineering/Rooms", "06_Service_Maintenance_Bypass_Pocket_EMPTY", SemanticZone.Zone02, SemanticKind.Room),
            new SemanticCatalogEntry("Zone02_PowerEngineering/Rooms", "07_Transition_To_Zone3_EMPTY", SemanticZone.Zone02, SemanticKind.Room),
            new SemanticCatalogEntry("Zone02_PowerEngineering", "route", SemanticZone.Zone02, SemanticKind.Route),
            new SemanticCatalogEntry("Zone03_SecurityContainment/Rooms", "02_Security_Junction_EMPTY", SemanticZone.Zone03, SemanticKind.Room),
            new SemanticCatalogEntry("Zone03_SecurityContainment/Rooms", "03_Security_Terminal_ST_EMPTY", SemanticZone.Zone03, SemanticKind.Room),
            new SemanticCatalogEntry("Zone03_SecurityContainment/Rooms", "04_Containment_Hall_EMPTY", SemanticZone.Zone03, SemanticKind.Room),
            new SemanticCatalogEntry("Zone03_SecurityContainment/Rooms", "05_Service_Emergency_Bypass_Pocket_EMPTY", SemanticZone.Zone03, SemanticKind.Room),
            new SemanticCatalogEntry("Zone03_SecurityContainment/Rooms", "06_Exit_Area_E_EMPTY", SemanticZone.Zone03, SemanticKind.Room),
            new SemanticCatalogEntry("Zone03_SecurityContainment", "Route", SemanticZone.Zone03, SemanticKind.Route)
        };

        public static DryRunReport DryRunActiveScene()
        {
            var report = new DryRunReport();
            if (!TryBuildActiveSceneContext(report, out var context))
            {
                return report;
            }

            return BuildDryRunReport(context.SpatialGraph, context.Sources, BuildOptions.Default, context.AuthoringConflicts);
        }

        public static DryRunReport ApplyAuthoringToActiveScene(DryRunReport previousReport)
        {
            if (previousReport == null || !previousReport.IsValid)
            {
                return previousReport;
            }

            var report = DryRunActiveScene();
            if (!report.IsValid || !TryFindStalkerRegionsRoot(report, out var root))
            {
                return report;
            }

            RemoveToolOwnedChildren(root);
            for (var i = 0; i < report.Regions.Count; i++)
            {
                var region = report.Regions[i];
                var go = new GameObject($"{GeneratedPrefix}{region.RegionId.Value:0000}");
                go.transform.SetParent(root.transform, false);
                go.transform.position = region.WorldBounds.center;

                var definition = go.AddComponent<RegionDefinition>();
                var so = new SerializedObject(definition);
                so.FindProperty("regionId").intValue = region.RegionId.Value;
                so.FindProperty("localBounds").boundsValue = new Bounds(Vector3.zero, region.WorldBounds.size);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            report.Messages.Add($"Applied {report.Regions.Count} tool-owned RegionDefinition objects under Navigation/StalkerRegions.");
            return report;
        }

        public static DryRunReport BakeAsset(string assetPath, DryRunReport previousReport)
        {
            if (previousReport == null || !previousReport.IsValid)
            {
                return previousReport;
            }

            var normalizedPath = assetPath.Replace('\\', '/');
            if (string.Equals(normalizedPath, ProtectedSpatialV3Path, StringComparison.OrdinalIgnoreCase))
            {
                var blocked = new DryRunReport();
                blocked.Errors.Add($"Refusing to overwrite protected asset: {ProtectedSpatialV3Path}");
                return blocked;
            }

            var report = DryRunActiveScene();
            if (!report.IsValid)
            {
                return report;
            }

            if (report.RuntimeGraph == null)
            {
                report.Errors.Add("Bake failed: missing runtime semantic RegionGraph.");
                return report;
            }

            var directory = Path.GetDirectoryName(normalizedPath);
            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                CreateAssetFolders(directory);
            }

            var asset = AssetDatabase.LoadAssetAtPath<RegionGraphAsset>(normalizedPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<RegionGraphAsset>();
                AssetDatabase.CreateAsset(asset, normalizedPath);
            }

            asset.ConfigureFromRuntimeGraph(report.RuntimeGraph, report.SpatialGraph.NodeCount);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            report.Messages.Add($"Baked RegionGraphAsset: {normalizedPath}");
            return report;
        }

        public static DryRunReport BuildDryRunReport(
            NavMeshSpatialGraph spatialGraph,
            IReadOnlyList<SemanticSource> sources,
            BuildOptions options,
            IReadOnlyList<string> authoringConflicts = null)
        {
            var report = new DryRunReport();
            report.SpatialGraph = spatialGraph;
            report.SpatialNodeCount = spatialGraph?.NodeCount ?? 0;
            report.CompatibilityIdentity = spatialGraph?.CompatibilityIdentity ?? SpatialGraphCompatibilityIdentity.Invalid;

            if (authoringConflicts != null)
            {
                report.Errors.AddRange(authoringConflicts);
            }

            if (spatialGraph == null || spatialGraph.IsEmpty)
            {
                report.Errors.Add("Missing or empty active NavMesh spatial graph.");
                return report;
            }

            var components = FindConnectedComponents(spatialGraph);
            report.ConnectedComponentCount = components.Count;
            if (sources == null || sources.Count == 0)
            {
                report.Errors.Add("No full-station semantic sources were resolved.");
                return report;
            }

            var attribution = BuildInitialAttribution(spatialGraph, sources, components, options, report);
            if (report.Errors.Count == 0)
            {
                BuildRegions(spatialGraph, attribution, sources, options, report);
                AssignDeterministicRegionIds(spatialGraph, report);
                ValidateFullCoverage(spatialGraph, report);
                report.Edges.AddRange(BuildConnectivityEdges(spatialGraph, report.NodeToRegion));
                ValidateGeometryBake(spatialGraph, report);
                BuildRuntimeSemanticGraph(spatialGraph, attribution, sources, report);
                ValidateRuntimeSemanticGraph(spatialGraph, report);
            }

            return report;
        }

        public static RegionGraphBakeResult BakeProposal(
            NavMeshSpatialGraph spatialGraph,
            IReadOnlyList<GeneratedRegion> regions,
            IReadOnlyList<RegionEdgeBakeData> edges)
        {
            var definitions = new RegionDefinitionBakeData[regions?.Count ?? 0];
            for (var i = 0; i < definitions.Length; i++)
            {
                definitions[i] = new RegionDefinitionBakeData(regions[i].RegionId, regions[i].WorldBounds);
            }

            return RegionGraphBakeUtility.Bake(spatialGraph, definitions, edges, DefinitionVersion);
        }

        private static AttributionResult BuildInitialAttribution(
            NavMeshSpatialGraph graph,
            IReadOnlyList<SemanticSource> sources,
            IReadOnlyList<ComponentInfo> components,
            BuildOptions options,
            DryRunReport report)
        {
            var result = new AttributionResult(graph.NodeCount);
            var sourceByIndex = new Dictionary<int, SemanticSource>();
            for (var i = 0; i < sources.Count; i++)
            {
                sourceByIndex.Add(sources[i].SourceIndex, sources[i]);
                report.SemanticSources.Add(new SemanticSourceReport(sources[i]));
                if (sources[i].EvidenceKind == FloorEvidenceKind.Unresolved)
                {
                    report.UnresolvedSources.Add(sources[i].SourcePath);
                    report.Errors.Add($"Unresolved semantic source: {sources[i].SourcePath}");
                }
            }

            var orderedSeedNodes = new List<SeedNode>();
            for (var sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
            {
                var source = sources[sourceIndex];
                for (var i = 0; i < source.EvidenceNodeIds.Count; i++)
                {
                    var nodeId = source.EvidenceNodeIds[i];
                    if (nodeId < 0 || nodeId >= graph.NodeCount)
                    {
                        report.Errors.Add($"Semantic source {source.SourcePath} references invalid evidence node {nodeId}.");
                        continue;
                    }

                    orderedSeedNodes.Add(new SeedNode(source.SourceIndex, nodeId));
                }
            }

            orderedSeedNodes.Sort();

            var seedClaimantsByNode = new Dictionary<int, List<int>>();
            for (var i = 0; i < orderedSeedNodes.Count; i++)
            {
                var seed = orderedSeedNodes[i];
                if (!seedClaimantsByNode.TryGetValue(seed.NodeId, out var claimants))
                {
                    claimants = new List<int>();
                    seedClaimantsByNode.Add(seed.NodeId, claimants);
                }

                if (!claimants.Contains(seed.SourceIndex))
                {
                    claimants.Add(seed.SourceIndex);
                }
            }

            var resolvedSeedOwnerByNode = new Dictionary<int, int>();
            foreach (var pair in seedClaimantsByNode)
            {
                if (pair.Value.Count <= 1)
                {
                    continue;
                }

                result.SeedOverlapNodeIds.Add(pair.Key);
                pair.Value.Sort();
                if (TryResolveSeedOverlapWinner(graph, pair.Key, pair.Value, sourceByIndex, out var winnerSourceIndex))
                {
                    resolvedSeedOwnerByNode.Add(pair.Key, winnerSourceIndex);
                    result.ResolvedSeedOverlapNodeIds.Add(pair.Key);
                }
                else
                {
                    result.UnresolvedSeedOverlapNodeIds.Add(pair.Key);
                }
            }

            var queue = new Queue<int>();
            var initializedSeedNodeIds = new HashSet<int>();
            for (var i = 0; i < orderedSeedNodes.Count; i++)
            {
                var seed = orderedSeedNodes[i];
                if (!initializedSeedNodeIds.Add(seed.NodeId))
                {
                    continue;
                }

                if (seedClaimantsByNode.TryGetValue(seed.NodeId, out var claimants) && claimants.Count > 1)
                {
                    if (!resolvedSeedOwnerByNode.TryGetValue(seed.NodeId, out var resolvedOwner))
                    {
                        continue;
                    }

                    result.OwnerByNode[seed.NodeId] = resolvedOwner;
                }
                else
                {
                    result.OwnerByNode[seed.NodeId] = seed.SourceIndex;
                }

                result.DistanceByNode[seed.NodeId] = 0;
                queue.Enqueue(seed.NodeId);
            }

            while (queue.Count > 0)
            {
                var nodeId = queue.Dequeue();
                var owner = result.OwnerByNode[nodeId];
                var distance = result.DistanceByNode[nodeId];
                if (!graph.TryGetNode(nodeId, out var node))
                {
                    continue;
                }

                for (var i = 0; i < node.NeighborIds.Count; i++)
                {
                    var neighborId = node.NeighborIds[i];
                    if (neighborId < 0 || neighborId >= graph.NodeCount)
                    {
                        continue;
                    }

                    var nextDistance = distance + 1;
                    if (result.OwnerByNode[neighborId] < 0)
                    {
                        result.OwnerByNode[neighborId] = owner;
                        result.DistanceByNode[neighborId] = nextDistance;
                        queue.Enqueue(neighborId);
                        continue;
                    }

                    if (result.OwnerByNode[neighborId] != owner && result.DistanceByNode[neighborId] == nextDistance)
                    {
                        result.BoundaryTieNodeIds.Add(neighborId);
                    }
                }
            }

            ClassifyUnclaimedComponents(graph, components, sources, options, result, report);
            CopySortedDistinct(result.SeedOverlapNodeIds, report.SeedOverlapNodeIds);
            CopySortedDistinct(result.ResolvedSeedOverlapNodeIds, report.ResolvedSeedOverlapNodeIds);
            CopySortedDistinct(result.UnresolvedSeedOverlapNodeIds, report.UnresolvedSeedOverlapNodeIds);
            CopySortedDistinct(result.BoundaryTieNodeIds, report.BoundaryTieNodeIds);
            BuildSeedEvidenceOverlapDetails(graph, sources, report);

            var multiplyMappedNodeIds = new List<int>(report.UnresolvedSeedOverlapNodeIds.Count + report.BoundaryTieNodeIds.Count);
            multiplyMappedNodeIds.AddRange(report.UnresolvedSeedOverlapNodeIds);
            multiplyMappedNodeIds.AddRange(report.BoundaryTieNodeIds);
            CopySortedDistinct(multiplyMappedNodeIds, report.MultiplyMappedNodeIds);

            for (var i = 0; i < report.UnresolvedSeedOverlapNodeIds.Count; i++)
            {
                report.Errors.Add($"Node {report.UnresolvedSeedOverlapNodeIds[i]} has a geometrically unresolved semantic seed evidence overlap.");
            }

            for (var i = 0; i < report.BoundaryTieNodeIds.Count; i++)
            {
                report.Errors.Add($"Node {report.BoundaryTieNodeIds[i]} is an equal-distance BFS semantic boundary tie.");
            }

            return result;
        }

        private static bool TryResolveSeedOverlapWinner(
            NavMeshSpatialGraph graph,
            int nodeId,
            IReadOnlyList<int> claimantSourceIndices,
            IReadOnlyDictionary<int, SemanticSource> sourceByIndex,
            out int winnerSourceIndex)
        {
            winnerSourceIndex = -1;
            if (!graph.TryGetNode(nodeId, out var node))
            {
                return false;
            }

            var candidates = new List<SeedGeometryCandidate>();
            for (var i = 0; i < claimantSourceIndices.Count; i++)
            {
                var sourceIndex = claimantSourceIndices[i];
                if (!sourceByIndex.TryGetValue(sourceIndex, out var source))
                {
                    continue;
                }

                if (TryGetBestSeedGeometryCandidate(source, nodeId, node.Position, out var candidate))
                {
                    candidates.Add(candidate);
                }
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            var best = candidates[0];
            for (var i = 1; i < candidates.Count; i++)
            {
                if (CompareSeedGeometryCandidate(candidates[i], best) < 0)
                {
                    best = candidates[i];
                }
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].SourceIndex == best.SourceIndex)
                {
                    continue;
                }

                if (CompareSeedGeometryCandidate(candidates[i], best) == 0)
                {
                    return false;
                }
            }

            winnerSourceIndex = best.SourceIndex;
            return true;
        }

        private static bool TryGetBestSeedGeometryCandidate(
            SemanticSource source,
            int nodeId,
            Vector3 nodePosition,
            out SeedGeometryCandidate best)
        {
            best = default;
            var found = false;
            var evidenceBounds = source.GetEvidenceBoundsForNode(nodeId);
            for (var i = 0; i < evidenceBounds.Count; i++)
            {
                var evidence = evidenceBounds[i];
                if (!IsSupportedByFloorBounds(nodePosition, evidence.Bounds))
                {
                    continue;
                }

                var candidate = new SeedGeometryCandidate(
                    source.SourceIndex,
                    Mathf.Min(
                        Mathf.Min(
                            nodePosition.x - evidence.Bounds.min.x,
                            evidence.Bounds.max.x - nodePosition.x),
                        Mathf.Min(
                            nodePosition.z - evidence.Bounds.min.z,
                            evidence.Bounds.max.z - nodePosition.z)),
                    Mathf.Abs(nodePosition.y - evidence.Bounds.center.y),
                    new Vector2(
                        nodePosition.x - evidence.Bounds.center.x,
                        nodePosition.z - evidence.Bounds.center.z).sqrMagnitude);

                if (!found || CompareSeedGeometryCandidate(candidate, best) < 0)
                {
                    best = candidate;
                    found = true;
                }
            }

            return found;
        }

        private static int CompareSeedGeometryCandidate(SeedGeometryCandidate a, SeedGeometryCandidate b)
        {
            if (a.HorizontalInteriorMargin > b.HorizontalInteriorMargin + SeedGeometryComparisonEpsilon)
            {
                return -1;
            }

            if (b.HorizontalInteriorMargin > a.HorizontalInteriorMargin + SeedGeometryComparisonEpsilon)
            {
                return 1;
            }

            if (a.VerticalDeltaToBoundsCenter + SeedGeometryComparisonEpsilon < b.VerticalDeltaToBoundsCenter)
            {
                return -1;
            }

            if (b.VerticalDeltaToBoundsCenter + SeedGeometryComparisonEpsilon < a.VerticalDeltaToBoundsCenter)
            {
                return 1;
            }

            if (a.HorizontalCenterDistanceSquared + SeedGeometryComparisonEpsilon < b.HorizontalCenterDistanceSquared)
            {
                return -1;
            }

            if (b.HorizontalCenterDistanceSquared + SeedGeometryComparisonEpsilon < a.HorizontalCenterDistanceSquared)
            {
                return 1;
            }

            return 0;
        }

        private static void BuildSeedEvidenceOverlapDetails(NavMeshSpatialGraph graph, IReadOnlyList<SemanticSource> sources, DryRunReport report)
        {
            for (var i = 0; i < report.SeedOverlapNodeIds.Count; i++)
            {
                var nodeId = report.SeedOverlapNodeIds[i];
                if (!graph.TryGetNode(nodeId, out var node))
                {
                    continue;
                }

                var claimants = new List<SeedEvidenceOverlapClaimant>();
                for (var sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
                {
                    var source = sources[sourceIndex];
                    if (!SourceClaimsEvidenceNode(source, nodeId))
                    {
                        continue;
                    }

                    claimants.Add(new SeedEvidenceOverlapClaimant(source, nodeId, node.Position));
                }

                claimants.Sort((a, b) => a.SourceIndex.CompareTo(b.SourceIndex));
                report.SeedEvidenceOverlapDetails.Add(new SeedEvidenceOverlapDetail(nodeId, node.Position, claimants));
            }
        }

        private static bool SourceClaimsEvidenceNode(SemanticSource source, int nodeId)
        {
            for (var i = 0; i < source.EvidenceNodeIds.Count; i++)
            {
                if (source.EvidenceNodeIds[i] == nodeId)
                {
                    return true;
                }
            }

            return false;
        }

        private static void CopySortedDistinct(List<int> source, List<int> destination)
        {
            source.Sort();
            for (var i = 0; i < source.Count; i++)
            {
                if (i > 0 && source[i] == source[i - 1])
                {
                    continue;
                }

                destination.Add(source[i]);
            }
        }

        private static void ClassifyUnclaimedComponents(
            NavMeshSpatialGraph graph,
            IReadOnlyList<ComponentInfo> components,
            IReadOnlyList<SemanticSource> sources,
            BuildOptions options,
            AttributionResult result,
            DryRunReport report)
        {
            var nextSyntheticSourceIndex = 100000;
            for (var c = 0; c < components.Count; c++)
            {
                var component = components[c];
                var unclaimed = new List<int>();
                for (var i = 0; i < component.NodeIds.Count; i++)
                {
                    var nodeId = component.NodeIds[i];
                    if (result.OwnerByNode[nodeId] < 0)
                    {
                        unclaimed.Add(nodeId);
                    }
                }

                if (unclaimed.Count == 0)
                {
                    continue;
                }

                var classification = ResolveComponentClassification(component.ComponentId, options);
                if (classification == UnclaimedComponentClassification.Unspecified)
                {
                    classification = options.DefaultUnclaimedClassification;
                }

                if (classification == UnclaimedComponentClassification.Unresolved)
                {
                    report.UnresolvedComponents.Add(component.ComponentId);
                    for (var i = 0; i < unclaimed.Count; i++)
                    {
                        report.UnresolvedNodeIds.Add(unclaimed[i]);
                        report.Errors.Add($"Unresolved spatial node {unclaimed[i]} in component {component.ComponentId}.");
                    }

                    continue;
                }

                var sourceIndex = -1;
                if (classification == UnclaimedComponentClassification.CorridorRoute)
                {
                    sourceIndex = FindNearestRouteSourceIndex(graph, component, sources);
                }

                if (sourceIndex < 0)
                {
                    sourceIndex = nextSyntheticSourceIndex++;
                    result.SyntheticSources.Add(new SemanticSource(
                        sourceIndex,
                        SemanticZone.Isolated,
                        SemanticKind.IsolatedIsland,
                        $"IsolatedNavigableIsland/Component_{component.ComponentId:0000}",
                        component.Centroid,
                        FloorEvidenceKind.IsolatedIsland,
                        new[] { component.MinNodeId }));
                    report.IsolatedIslandNodeCount += unclaimed.Count;
                }

                for (var i = 0; i < unclaimed.Count; i++)
                {
                    result.OwnerByNode[unclaimed[i]] = sourceIndex;
                    result.DistanceByNode[unclaimed[i]] = 0;
                }
            }
        }

        private static void BuildRegions(
            NavMeshSpatialGraph graph,
            AttributionResult attribution,
            IReadOnlyList<SemanticSource> sources,
            BuildOptions options,
            DryRunReport report)
        {
            var allSources = new List<SemanticSource>(sources);
            allSources.AddRange(attribution.SyntheticSources);
            allSources.Sort((a, b) => a.SourceIndex.CompareTo(b.SourceIndex));

            var nodesBySource = new Dictionary<int, List<int>>();
            for (var nodeId = 0; nodeId < attribution.OwnerByNode.Length; nodeId++)
            {
                var sourceIndex = attribution.OwnerByNode[nodeId];
                if (sourceIndex < 0)
                {
                    report.ZeroMappedNodeIds.Add(nodeId);
                    continue;
                }

                if (!nodesBySource.TryGetValue(sourceIndex, out var nodes))
                {
                    nodes = new List<int>();
                    nodesBySource.Add(sourceIndex, nodes);
                }

                nodes.Add(nodeId);
            }

            foreach (var source in allSources)
            {
                if (!nodesBySource.TryGetValue(source.SourceIndex, out var nodes) || nodes.Count == 0)
                {
                    continue;
                }

                nodes.Sort();
                CountSemanticAttribution(source, nodes.Count, report);
                var connectedGroups = SplitConnectedGroups(graph, nodes);
                connectedGroups.Sort(CompareNodeGroups);
                for (var i = 0; i < connectedGroups.Count; i++)
                {
                    DecomposeGroup(graph, attribution.OwnerByNode, source, connectedGroups[i], 0, "initial", options, report);
                }
            }
        }

        private static void DecomposeGroup(
            NavMeshSpatialGraph graph,
            IReadOnlyList<int> ownerByNode,
            SemanticSource source,
            List<int> nodeIds,
            int depth,
            string splitReason,
            BuildOptions options,
            DryRunReport report)
        {
            nodeIds.Sort();
            if (nodeIds.Count == 0)
            {
                return;
            }

            if (depth > options.MaxDecompositionDepth)
            {
                report.DecompositionFailures.Add($"Max decomposition depth exceeded for {source.SourcePath}; nodes {nodeIds[0]}..{nodeIds[nodeIds.Count - 1]}.");
                report.Errors.Add($"Decomposition failed for {source.SourcePath}: max depth exceeded.");
                return;
            }

            var connectedGroups = SplitConnectedGroups(graph, nodeIds);
            if (connectedGroups.Count > 1)
            {
                connectedGroups.Sort(CompareNodeGroups);
                for (var i = 0; i < connectedGroups.Count; i++)
                {
                    DecomposeGroup(graph, ownerByNode, source, connectedGroups[i], depth + 1, "disconnected source cluster", options, report);
                }

                return;
            }

            var bounds = BuildNodeBounds(graph, nodeIds);
            var foreign = FindNodesOutsideGroupInBounds(graph, bounds, nodeIds);
            if (foreign.Count == 0)
            {
                report.Regions.Add(new GeneratedRegion(source, nodeIds, bounds, depth, splitReason));
                return;
            }

            if (nodeIds.Count == 1)
            {
                report.ForeignNodeContamination.Add(new ForeignNodeContamination(source.SourcePath, -1, foreign[0], bounds));
                report.Errors.Add($"Cannot split single-node contaminated region for {source.SourcePath}.");
                return;
            }

            var split = ChooseSplit(graph, nodeIds, foreign);
            if (!split.IsValid)
            {
                report.DecompositionFailures.Add($"No valid split for {source.SourcePath}; nodes {nodeIds[0]}..{nodeIds[nodeIds.Count - 1]}.");
                report.Errors.Add($"Decomposition failed for {source.SourcePath}: no valid deterministic split.");
                return;
            }

            report.RegionSplitReasons.Add($"{source.SourcePath}: depth {depth}, axis {split.Axis}, pivot {FormatFloat(split.Pivot)}, reason {split.Reason}");
            var lower = new List<int>();
            var upper = new List<int>();
            for (var i = 0; i < nodeIds.Count; i++)
            {
                var coord = GetCoordinate(graph.Nodes[nodeIds[i]].Position, split.Axis);
                if (coord <= split.Pivot)
                {
                    lower.Add(nodeIds[i]);
                }
                else
                {
                    upper.Add(nodeIds[i]);
                }
            }

            if (lower.Count == 0 || upper.Count == 0)
            {
                report.DecompositionFailures.Add($"Degenerate split for {source.SourcePath} on {split.Axis} at {FormatFloat(split.Pivot)}.");
                report.Errors.Add($"Decomposition failed for {source.SourcePath}: degenerate split.");
                return;
            }

            DecomposeGroup(graph, ownerByNode, source, lower, depth + 1, split.Reason, options, report);
            DecomposeGroup(graph, ownerByNode, source, upper, depth + 1, split.Reason, options, report);
        }

        private static SplitDecision ChooseSplit(NavMeshSpatialGraph graph, IReadOnlyList<int> nodeIds, IReadOnlyList<int> foreignNodeIds)
        {
            var best = SplitDecision.Invalid;
            ConsiderAxis(graph, nodeIds, foreignNodeIds, SplitAxis.X, ref best);
            ConsiderAxis(graph, nodeIds, foreignNodeIds, SplitAxis.Z, ref best);
            ConsiderAxis(graph, nodeIds, foreignNodeIds, SplitAxis.Y, ref best);
            return best;
        }

        private static void ConsiderAxis(
            NavMeshSpatialGraph graph,
            IReadOnlyList<int> nodeIds,
            IReadOnlyList<int> foreignNodeIds,
            SplitAxis axis,
            ref SplitDecision best)
        {
            var coords = new List<NodeCoord>(nodeIds.Count);
            for (var i = 0; i < nodeIds.Count; i++)
            {
                coords.Add(new NodeCoord(nodeIds[i], GetCoordinate(graph.Nodes[nodeIds[i]].Position, axis)));
            }

            coords.Sort();
            var largestGap = -1f;
            var pivot = 0f;
            for (var i = 0; i + 1 < coords.Count; i++)
            {
                var gap = coords[i + 1].Coordinate - coords[i].Coordinate;
                if (gap > largestGap)
                {
                    largestGap = gap;
                    pivot = (coords[i].Coordinate + coords[i + 1].Coordinate) * 0.5f;
                }
            }

            if (largestGap <= 0f)
            {
                var mid = coords.Count / 2;
                if (mid <= 0 || mid >= coords.Count)
                {
                    return;
                }

                pivot = (coords[mid - 1].Coordinate + coords[mid].Coordinate) * 0.5f;
                largestGap = 0f;
            }

            var avoidsForeign = 0;
            for (var i = 0; i < foreignNodeIds.Count; i++)
            {
                var c = GetCoordinate(graph.Nodes[foreignNodeIds[i]].Position, axis);
                if (Mathf.Abs(c - pivot) <= Mathf.Max(BoundsPadding, largestGap * 0.5f))
                {
                    avoidsForeign++;
                }
            }

            var axisPreference = axis == SplitAxis.Y ? 0 : 1;
            var score = largestGap + avoidsForeign + axisPreference;
            var candidate = new SplitDecision(axis, pivot, score, largestGap > 0f ? "largest coordinate gap" : "median coordinate");
            if (!best.IsValid || candidate.Score > best.Score || (Mathf.Approximately(candidate.Score, best.Score) && axis.CompareTo(best.Axis) < 0))
            {
                best = candidate;
            }
        }

        private static void AssignDeterministicRegionIds(NavMeshSpatialGraph graph, DryRunReport report)
        {
            report.Regions.Sort((a, b) =>
            {
                var source = a.Source.SourceIndex.CompareTo(b.Source.SourceIndex);
                if (source != 0) return source;
                var y = a.MinY.CompareTo(b.MinY);
                if (y != 0) return y;
                var x = a.Centroid.x.CompareTo(b.Centroid.x);
                if (x != 0) return x;
                var z = a.Centroid.z.CompareTo(b.Centroid.z);
                if (z != 0) return z;
                return a.MinNodeId.CompareTo(b.MinNodeId);
            });

            report.NodeToRegion = new RegionId[graph.NodeCount];
            for (var i = 0; i < report.Regions.Count; i++)
            {
                var regionId = new RegionId(i + 1);
                report.Regions[i].SetRegionId(regionId);
                for (var n = 0; n < report.Regions[i].NodeIds.Count; n++)
                {
                    report.NodeToRegion[report.Regions[i].NodeIds[n]] = regionId;
                }
            }
        }

        private static void ValidateFullCoverage(NavMeshSpatialGraph graph, DryRunReport report)
        {
            var seen = new HashSet<RegionId>();
            for (var i = 0; i < report.Regions.Count; i++)
            {
                var region = report.Regions[i];
                if (!region.RegionId.IsValid)
                {
                    report.InvalidRegionIds.Add(region.RegionId);
                    report.Errors.Add($"Invalid RegionId for {region.SourcePath}.");
                }

                if (!seen.Add(region.RegionId))
                {
                    report.DuplicateRegionIds.Add(region.RegionId);
                    report.Errors.Add($"Duplicate RegionId {region.RegionId.Value}.");
                }

                if (!IsNodeSetConnected(graph, region.NodeIds))
                {
                    report.DisconnectedRegionIds.Add(region.RegionId);
                    report.Errors.Add($"Generated region {region.RegionId.Value} is disconnected.");
                }
            }

            for (var nodeId = 0; nodeId < graph.NodeCount; nodeId++)
            {
                if (nodeId >= report.NodeToRegion.Length || !report.NodeToRegion[nodeId].IsValid)
                {
                    report.ZeroMappedNodeIds.Add(nodeId);
                    report.Errors.Add($"Spatial node {nodeId} has no generated RegionId.");
                }
            }

            for (var i = 0; i < report.Regions.Count; i++)
            {
                var region = report.Regions[i];
                var foreign = FindForeignNodesInBounds(graph, region.WorldBounds, report.NodeToRegion, region.RegionId);
                for (var f = 0; f < foreign.Count; f++)
                {
                    report.ForeignNodeContamination.Add(new ForeignNodeContamination(region.SourcePath, region.RegionId.Value, foreign[f], region.WorldBounds));
                    report.Errors.Add($"Region {region.RegionId.Value} bounds contain foreign node {foreign[f]}.");
                }
            }

            report.MappedNodeCount = graph.NodeCount - report.ZeroMappedNodeIds.Count;
        }

        private static void ValidateGeometryBake(NavMeshSpatialGraph graph, DryRunReport report)
        {
            var bake = BakeProposal(graph, report.Regions, report.Edges);
            report.GeometryBakeDiagnostic = bake.Diagnostic;
            if (!bake.Succeeded)
            {
                report.Errors.Add($"RegionGraphBakeUtility rejected proposal: {bake.Diagnostic.Failure}");
                return;
            }

            if (bake.Graph.CompatibilityIdentity != graph.CompatibilityIdentity)
            {
                report.Errors.Add("Baked RegionGraph compatibility identity did not match spatial graph identity.");
            }
        }

        private static void BuildRuntimeSemanticGraph(
            NavMeshSpatialGraph graph,
            AttributionResult attribution,
            IReadOnlyList<SemanticSource> sources,
            DryRunReport report)
        {
            var allSources = new List<SemanticSource>();
            for (var i = 0; i < sources.Count; i++)
            {
                allSources.Add(sources[i]);
            }

            for (var i = 0; i < attribution.SyntheticSources.Count; i++)
            {
                allSources.Add(attribution.SyntheticSources[i]);
            }

            allSources.Sort((a, b) => a.SourceIndex.CompareTo(b.SourceIndex));
            var runtimeRegionOrdinal = 1;
            report.RuntimeNodeToRegion = new RegionId[graph.NodeCount];
            for (var sourceIndex = 0; sourceIndex < allSources.Count; sourceIndex++)
            {
                var source = allSources[sourceIndex];
                var nodeIds = new List<int>();
                for (var nodeId = 0; nodeId < attribution.OwnerByNode.Length; nodeId++)
                {
                    if (attribution.OwnerByNode[nodeId] == source.SourceIndex)
                    {
                        nodeIds.Add(nodeId);
                    }
                }

                if (nodeIds.Count == 0)
                {
                    continue;
                }

                nodeIds.Sort();
                var connectedGroups = SplitConnectedGroups(graph, nodeIds);
                connectedGroups.Sort(CompareNodeGroups);
                for (var groupIndex = 0; groupIndex < connectedGroups.Count; groupIndex++)
                {
                    var group = connectedGroups[groupIndex];
                    group.Sort();
                    var regionId = new RegionId(runtimeRegionOrdinal++);
                    for (var nodeIndex = 0; nodeIndex < group.Count; nodeIndex++)
                    {
                        report.RuntimeNodeToRegion[group[nodeIndex]] = regionId;
                    }

                    report.RuntimeRegions.Add(new RuntimeSemanticRegion(regionId, source, group));
                }
            }

            report.RuntimeEdges.AddRange(BuildConnectivityEdges(graph, report.RuntimeNodeToRegion));
            report.RuntimeSemanticRegionCount = report.RuntimeRegions.Count;
            report.RuntimeGraph = BuildRuntimeGraphFromSemanticReport(graph, report);
        }

        private static RegionGraph BuildRuntimeGraphFromSemanticReport(NavMeshSpatialGraph graph, DryRunReport report)
        {
            var edgeBuckets = new Dictionary<RegionId, List<RegionEdge>>();
            for (var i = 0; i < report.RuntimeRegions.Count; i++)
            {
                edgeBuckets.Add(report.RuntimeRegions[i].RegionId, new List<RegionEdge>());
            }

            for (var i = 0; i < report.RuntimeEdges.Count; i++)
            {
                var edge = report.RuntimeEdges[i];
                if (!edgeBuckets.TryGetValue(edge.FromRegionId, out var edges))
                {
                    continue;
                }

                edges.Add(new RegionEdge(edge.ToRegionId, edge.DoorId));
            }

            var regionNodes = new List<RegionNode>(report.RuntimeRegions.Count);
            for (var i = 0; i < report.RuntimeRegions.Count; i++)
            {
                var regionId = report.RuntimeRegions[i].RegionId;
                regionNodes.Add(new RegionNode(regionId, edgeBuckets[regionId]));
            }

            return new RegionGraph(
                regionNodes,
                report.RuntimeNodeToRegion,
                graph.CompatibilityIdentity,
                DefinitionVersion);
        }

        private static void ValidateRuntimeSemanticGraph(NavMeshSpatialGraph graph, DryRunReport report)
        {
            ValidateRuntimeSemanticMapping(graph, report);
            report.BakeDiagnostic = RegionGraphBakeUtility.ValidateRuntimeGraph(report.RuntimeGraph, graph);
            if (!report.BakeDiagnostic.IsSuccess)
            {
                report.Errors.Add($"Runtime semantic RegionGraph rejected proposal: {report.BakeDiagnostic.Failure}");
            }
        }

        private static void ValidateRuntimeSemanticMapping(NavMeshSpatialGraph graph, DryRunReport report)
        {
            if (report.RuntimeNodeToRegion == null || report.RuntimeNodeToRegion.Length != graph.NodeCount)
            {
                report.Errors.Add($"RuntimeNodeToRegion length mismatch: {report.RuntimeNodeToRegion?.Length ?? 0} / {graph.NodeCount}.");
                return;
            }

            var regionById = new Dictionary<RegionId, RuntimeSemanticRegion>();
            for (var i = 0; i < report.RuntimeRegions.Count; i++)
            {
                var region = report.RuntimeRegions[i];
                if (!region.RegionId.IsValid)
                {
                    report.RuntimeInvalidRegionIds.Add(region.RegionId);
                    report.Errors.Add($"Runtime semantic region has invalid RegionId for sourceIndex={region.SourceIndex}, {region.SourcePath}.");
                    continue;
                }

                if (regionById.ContainsKey(region.RegionId))
                {
                    report.RuntimeDuplicateRegionIds.Add(region.RegionId);
                    report.Errors.Add($"Duplicate runtime semantic RegionId {region.RegionId.Value}.");
                    continue;
                }

                regionById.Add(region.RegionId, region);
            }

            for (var nodeId = 0; nodeId < graph.NodeCount; nodeId++)
            {
                var regionId = report.RuntimeNodeToRegion[nodeId];
                if (!regionId.IsValid)
                {
                    report.RuntimeUnmappedNodeIds.Add(nodeId);
                    report.Errors.Add($"Spatial node {nodeId} has no runtime semantic RegionId.");
                    continue;
                }

                if (!regionById.ContainsKey(regionId))
                {
                    report.RuntimeDanglingNodeIds.Add(nodeId);
                    report.Errors.Add($"Spatial node {nodeId} maps to missing runtime semantic RegionId {regionId.Value}.");
                }
            }

            for (var i = 0; i < report.RuntimeRegions.Count; i++)
            {
                var region = report.RuntimeRegions[i];
                if (!region.RegionId.IsValid)
                {
                    continue;
                }

                var nodeIds = new List<int>();
                for (var nodeId = 0; nodeId < report.RuntimeNodeToRegion.Length; nodeId++)
                {
                    if (report.RuntimeNodeToRegion[nodeId] == region.RegionId)
                    {
                        nodeIds.Add(nodeId);
                    }
                }

                nodeIds.Sort();
                if (nodeIds.Count == 0)
                {
                    report.RuntimeDisconnectedRegionIds.Add(region.RegionId);
                    report.Errors.Add($"Runtime semantic RegionId {region.RegionId.Value} for sourceIndex={region.SourceIndex}, {region.SourcePath} has no mapped nodes.");
                    continue;
                }

                if (nodeIds.Count != region.SpatialNodeCount)
                {
                    report.Errors.Add($"Runtime semantic RegionId {region.RegionId.Value} for sourceIndex={region.SourceIndex}, {region.SourcePath} metadata node count {region.SpatialNodeCount} does not match mapped node count {nodeIds.Count}.");
                }

                if (nodeIds[0] != region.MinNodeId || nodeIds[nodeIds.Count - 1] != region.MaxNodeId)
                {
                    report.Errors.Add($"Runtime semantic RegionId {region.RegionId.Value} for sourceIndex={region.SourceIndex}, {region.SourcePath} metadata min/max {region.MinNodeId}/{region.MaxNodeId} does not match mapped min/max {nodeIds[0]}/{nodeIds[nodeIds.Count - 1]}.");
                }

                if (!IsNodeSetConnected(graph, nodeIds))
                {
                    report.RuntimeDisconnectedRegionIds.Add(region.RegionId);
                    report.Errors.Add($"Runtime semantic RegionId {region.RegionId.Value} for sourceIndex={region.SourceIndex}, {region.SourcePath} is disconnected.");
                }
            }

            for (var i = 0; i < report.RuntimeEdges.Count; i++)
            {
                var edge = report.RuntimeEdges[i];
                if (edge.FromRegionId == edge.ToRegionId)
                {
                    report.RuntimeSelfTransitionRegionIds.Add(edge.FromRegionId);
                    report.Errors.Add($"Runtime semantic RegionId {edge.FromRegionId.Value} has a self-transition edge.");
                }
            }
        }

        private static List<RegionEdgeBakeData> BuildConnectivityEdges(NavMeshSpatialGraph graph, IReadOnlyList<RegionId> nodeToRegion)
        {
            var keys = new SortedSet<DirectedRegionEdgeKey>();
            for (var nodeId = 0; nodeId < graph.NodeCount; nodeId++)
            {
                if (!TryGetRegion(nodeToRegion, nodeId, out var from) || !graph.TryGetNode(nodeId, out var node))
                {
                    continue;
                }

                for (var i = 0; i < node.NeighborIds.Count; i++)
                {
                    if (TryGetRegion(nodeToRegion, node.NeighborIds[i], out var to) && from != to)
                    {
                        keys.Add(new DirectedRegionEdgeKey(from, to));
                    }
                }
            }

            var edges = new List<RegionEdgeBakeData>(keys.Count);
            foreach (var key in keys)
            {
                edges.Add(new RegionEdgeBakeData(key.From, key.To, DoorId.Invalid));
            }

            return edges;
        }

        public static List<ComponentInfo> FindConnectedComponents(NavMeshSpatialGraph graph)
        {
            var components = new List<ComponentInfo>();
            if (graph == null || graph.IsEmpty)
            {
                return components;
            }

            var visited = new bool[graph.NodeCount];
            for (var nodeId = 0; nodeId < graph.NodeCount; nodeId++)
            {
                if (visited[nodeId])
                {
                    continue;
                }

                var ids = new List<int>();
                var queue = new Queue<int>();
                visited[nodeId] = true;
                queue.Enqueue(nodeId);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    ids.Add(current);
                    if (!graph.TryGetNode(current, out var node))
                    {
                        continue;
                    }

                    for (var i = 0; i < node.NeighborIds.Count; i++)
                    {
                        var neighborId = node.NeighborIds[i];
                        if (neighborId >= 0 && neighborId < visited.Length && !visited[neighborId])
                        {
                            visited[neighborId] = true;
                            queue.Enqueue(neighborId);
                        }
                    }
                }

                ids.Sort();
                components.Add(new ComponentInfo(components.Count, ids, BuildNodeBounds(graph, ids)));
            }

            return components;
        }

        private static List<List<int>> SplitConnectedGroups(NavMeshSpatialGraph graph, IReadOnlyList<int> nodeIds)
        {
            var allowed = new HashSet<int>(nodeIds);
            var visited = new HashSet<int>();
            var groups = new List<List<int>>();
            for (var i = 0; i < nodeIds.Count; i++)
            {
                var start = nodeIds[i];
                if (!visited.Add(start))
                {
                    continue;
                }

                var group = new List<int>();
                var queue = new Queue<int>();
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    var nodeId = queue.Dequeue();
                    group.Add(nodeId);
                    if (!graph.TryGetNode(nodeId, out var node))
                    {
                        continue;
                    }

                    for (var n = 0; n < node.NeighborIds.Count; n++)
                    {
                        var neighborId = node.NeighborIds[n];
                        if (allowed.Contains(neighborId) && visited.Add(neighborId))
                        {
                            queue.Enqueue(neighborId);
                        }
                    }
                }

                group.Sort();
                groups.Add(group);
            }

            return groups;
        }

        private static bool IsNodeSetConnected(NavMeshSpatialGraph graph, IReadOnlyList<int> nodeIds)
        {
            return SplitConnectedGroups(graph, nodeIds).Count == 1;
        }

        private static Bounds BuildNodeBounds(NavMeshSpatialGraph graph, IReadOnlyList<int> nodeIds)
        {
            var bounds = new Bounds(graph.Nodes[nodeIds[0]].Position, Vector3.zero);
            for (var i = 1; i < nodeIds.Count; i++)
            {
                bounds.Encapsulate(graph.Nodes[nodeIds[i]].Position);
            }

            bounds.Expand(new Vector3(BoundsPadding, BoundsPadding, BoundsPadding));
            return bounds;
        }

        private static List<int> FindForeignNodesInBounds(NavMeshSpatialGraph graph, Bounds bounds, IReadOnlyList<int> ownerByNode, int expectedOwner)
        {
            var foreign = new List<int>();
            for (var nodeId = 0; nodeId < graph.NodeCount; nodeId++)
            {
                if (ownerByNode[nodeId] != expectedOwner && bounds.Contains(graph.Nodes[nodeId].Position))
                {
                    foreign.Add(nodeId);
                }
            }

            return foreign;
        }

        private static List<int> FindForeignNodesInBounds(NavMeshSpatialGraph graph, Bounds bounds, IReadOnlyList<RegionId> nodeToRegion, RegionId expectedRegion)
        {
            var foreign = new List<int>();
            for (var nodeId = 0; nodeId < graph.NodeCount; nodeId++)
            {
                if (nodeToRegion[nodeId] != expectedRegion && bounds.Contains(graph.Nodes[nodeId].Position))
                {
                    foreign.Add(nodeId);
                }
            }

            return foreign;
        }

        private static List<int> FindNodesOutsideGroupInBounds(
            NavMeshSpatialGraph graph,
            Bounds bounds,
            IReadOnlyList<int> nodeIds)
        {
            var allowed = new HashSet<int>(nodeIds);
            var foreign = new List<int>();

            for (var nodeId = 0; nodeId < graph.NodeCount; nodeId++)
            {
                if (!allowed.Contains(nodeId) &&
                    bounds.Contains(graph.Nodes[nodeId].Position))
                {
                    foreign.Add(nodeId);
                }
            }

            return foreign;
        }

        private static bool TryBuildActiveSceneContext(DryRunReport report, out ActiveSceneContext context)
        {
            context = default;
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                report.Errors.Add("No active loaded scene.");
                return false;
            }

            var spatialGraph = NavMeshSpatialGraphBuilder.Build();
            if (spatialGraph == null || spatialGraph.IsEmpty)
            {
                report.Errors.Add("Active scene NavMesh triangulation produced an empty spatial graph.");
                return false;
            }

            if (!TryFindStalkerRegionsRoot(report, out var stalkerRegionsRoot))
            {
                return false;
            }

            var sources = ResolveSceneSemanticSources(scene, spatialGraph, report);
            var conflicts = FindExistingAuthoringConflicts(stalkerRegionsRoot);
            context = new ActiveSceneContext(spatialGraph, sources, conflicts);
            return report.Errors.Count == 0;
        }

        private static List<SemanticSource> ResolveSceneSemanticSources(Scene scene, NavMeshSpatialGraph graph, DryRunReport report)
        {
            var sources = new List<SemanticSource>();
            for (var i = 0; i < Catalog.Length; i++)
            {
                var entry = Catalog[i];
                var path = $"{entry.ParentPath}/{entry.LeafName}";
                var transform = FindTransformByPath(scene, path);
                if (transform == null)
                {
                    report.Errors.Add($"Missing semantic source: {path}");
                    sources.Add(new SemanticSource(i, entry.Zone, entry.Kind, path, Vector3.zero, FloorEvidenceKind.Unresolved, Array.Empty<int>()));
                    continue;
                }

                var evidenceNodeIds = FindFloorEvidenceNodeIds(graph, transform, out var evidenceObjectPathsByNode, out var evidenceBoundsByNode);
                var evidenceKind = FloorEvidenceKind.FloorEvidence;
                var anchor = Vector3.zero;
                if (evidenceNodeIds.Count > 0)
                {
                    anchor = graph.Nodes[evidenceNodeIds[0]].Position;
                }
                else if (TryFindNearestSpatialNode(graph, transform.position, out var nearestNodeId))
                {
                    evidenceKind = FloorEvidenceKind.TransformFallback;
                    evidenceNodeIds.Add(nearestNodeId);
                    anchor = graph.Nodes[nearestNodeId].Position;
                    report.Warnings.Add($"TransformFallback for semantic source {path}: node {nearestNodeId}.");
                }
                else
                {
                    evidenceKind = FloorEvidenceKind.Unresolved;
                    report.Errors.Add($"Unresolved semantic source: {path}");
                }

                evidenceNodeIds.Sort();
                sources.Add(new SemanticSource(i, entry.Zone, entry.Kind, path, anchor, evidenceKind, evidenceNodeIds, evidenceObjectPathsByNode, evidenceBoundsByNode));
            }

            return sources;
        }

        private static List<int> FindFloorEvidenceNodeIds(NavMeshSpatialGraph graph, Transform root)
        {
            return FindFloorEvidenceNodeIds(graph, root, out _);
        }

        private static List<int> FindFloorEvidenceNodeIds(NavMeshSpatialGraph graph, Transform root, out Dictionary<int, List<string>> evidenceObjectPathsByNode)
        {
            return FindFloorEvidenceNodeIds(graph, root, out evidenceObjectPathsByNode, out _);
        }

        private static List<int> FindFloorEvidenceNodeIds(
            NavMeshSpatialGraph graph,
            Transform root,
            out Dictionary<int, List<string>> evidenceObjectPathsByNode,
            out Dictionary<int, List<FloorEvidenceBoundsDiagnostic>> evidenceBoundsByNode)
        {
            var bounds = new List<FloorEvidenceBoundsDiagnostic>();
            CollectFloorEvidenceBounds(root, bounds);
            evidenceObjectPathsByNode = new Dictionary<int, List<string>>();
            evidenceBoundsByNode = new Dictionary<int, List<FloorEvidenceBoundsDiagnostic>>();
            var nodeIds = new List<int>();
            if (bounds.Count == 0)
            {
                return nodeIds;
            }

            for (var nodeId = 0; nodeId < graph.NodeCount; nodeId++)
            {
                var position = graph.Nodes[nodeId].Position;
                for (var i = 0; i < bounds.Count; i++)
                {
                    if (IsSupportedByFloorBounds(position, bounds[i].Bounds))
                    {
                        if (!evidenceObjectPathsByNode.TryGetValue(nodeId, out var paths))
                        {
                            paths = new List<string>();
                            evidenceObjectPathsByNode.Add(nodeId, paths);
                            evidenceBoundsByNode.Add(nodeId, new List<FloorEvidenceBoundsDiagnostic>());
                            nodeIds.Add(nodeId);
                        }

                        paths.Add(bounds[i].HierarchyPath);
                        evidenceBoundsByNode[nodeId].Add(bounds[i]);
                    }
                }
            }

            foreach (var pair in evidenceObjectPathsByNode)
            {
                pair.Value.Sort(StringComparer.Ordinal);
            }

            foreach (var pair in evidenceBoundsByNode)
            {
                pair.Value.Sort(CompareFloorEvidenceBoundsDiagnostic);
            }

            return nodeIds;
        }

        private static void CollectFloorBounds(Transform root, List<Bounds> bounds)
        {
            var evidenceBounds = new List<FloorEvidenceBoundsDiagnostic>();
            CollectFloorEvidenceBounds(root, evidenceBounds);
            for (var i = 0; i < evidenceBounds.Count; i++)
            {
                bounds.Add(evidenceBounds[i].Bounds);
            }
        }

        private static void CollectFloorEvidenceBounds(Transform root, List<FloorEvidenceBoundsDiagnostic> bounds)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                if (LooksLikeFloor(renderers[i].transform, renderers[i].bounds))
                {
                    AddFloorEvidenceBounds(bounds, renderers[i].bounds, GetHierarchyPath(renderers[i].transform));
                }
            }

            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                if (LooksLikeFloor(colliders[i].transform, colliders[i].bounds))
                {
                    AddFloorEvidenceBounds(bounds, colliders[i].bounds, GetHierarchyPath(colliders[i].transform));
                }
            }

            bounds.Sort(CompareFloorEvidenceBoundsDiagnostic);
        }

        private static void AddFloorEvidenceBounds(List<FloorEvidenceBoundsDiagnostic> bounds, Bounds candidateBounds, string hierarchyPath)
        {
            for (var i = 0; i < bounds.Count; i++)
            {
                if (bounds[i].HierarchyPath == hierarchyPath
                    && bounds[i].Bounds.center == candidateBounds.center
                    && bounds[i].Bounds.size == candidateBounds.size)
                {
                    return;
                }
            }

            bounds.Add(new FloorEvidenceBoundsDiagnostic(candidateBounds, hierarchyPath));
        }

        private static int CompareFloorEvidenceBoundsDiagnostic(FloorEvidenceBoundsDiagnostic a, FloorEvidenceBoundsDiagnostic b)
        {
            var path = string.CompareOrdinal(a.HierarchyPath, b.HierarchyPath);
            if (path != 0) return path;
            var centerX = a.Bounds.center.x.CompareTo(b.Bounds.center.x);
            if (centerX != 0) return centerX;
            var centerY = a.Bounds.center.y.CompareTo(b.Bounds.center.y);
            if (centerY != 0) return centerY;
            var centerZ = a.Bounds.center.z.CompareTo(b.Bounds.center.z);
            if (centerZ != 0) return centerZ;
            var sizeX = a.Bounds.size.x.CompareTo(b.Bounds.size.x);
            if (sizeX != 0) return sizeX;
            var sizeY = a.Bounds.size.y.CompareTo(b.Bounds.size.y);
            if (sizeY != 0) return sizeY;
            return a.Bounds.size.z.CompareTo(b.Bounds.size.z);
        }

        private static bool LooksLikeFloor(Transform transform, Bounds bounds)
        {
            if (!IsFinite(bounds) || bounds.size.sqrMagnitude <= 0f)
            {
                return false;
            }

            var name = transform.name.ToLowerInvariant();
            if (name.Contains("light")
                || name.Contains("wall")
                || name.Contains("beam")
                || name.Contains("ceiling")
                || name.Contains("pipe")
                || name.Contains("pillar")
                || name.Contains("prop")
                || name.Contains("barrel")
                || name.Contains("crate"))
            {
                return false;
            }

            return name.Contains("floor")
                || name.Contains("ground")
                || name.Contains("route")
                || name.Contains("walk")
                || name.Contains("catwalk")
                || name.Contains("incline")
                || bounds.size.y <= Mathf.Max(0.6f, Mathf.Min(bounds.size.x, bounds.size.z) * 0.25f);
        }

        private static bool IsSupportedByFloorBounds(Vector3 position, Bounds bounds)
        {
            return position.x >= bounds.min.x - BoundsPadding
                && position.x <= bounds.max.x + BoundsPadding
                && position.z >= bounds.min.z - BoundsPadding
                && position.z <= bounds.max.z + BoundsPadding
                && position.y >= bounds.min.y - FloorVerticalTolerance
                && position.y <= bounds.max.y + FloorVerticalTolerance;
        }

        private static bool TryFindStalkerRegionsRoot(DryRunReport report, out GameObject root)
        {
            root = null;
            var scene = SceneManager.GetActiveScene();
            var navigation = FindTransformByPath(scene, NavigationName);
            if (navigation == null)
            {
                report.Errors.Add("Missing required hierarchy: Navigation.");
                return false;
            }

            var stalkerRegions = navigation.Find(StalkerRegionsName);
            if (stalkerRegions == null)
            {
                report.Errors.Add("Missing required hierarchy: Navigation/StalkerRegions.");
                return false;
            }

            root = stalkerRegions.gameObject;
            return true;
        }

        private static IReadOnlyList<string> FindExistingAuthoringConflicts(GameObject stalkerRegionsRoot)
        {
            var conflicts = new List<string>();
            var definitions = stalkerRegionsRoot.GetComponentsInChildren<RegionDefinition>(true);
            for (var i = 0; i < definitions.Length; i++)
            {
                var name = definitions[i].gameObject.name;
                if (!name.StartsWith(GeneratedPrefix, StringComparison.Ordinal))
                {
                    conflicts.Add($"Existing non-tool RegionDefinition authoring conflict: {GetHierarchyPath(definitions[i].transform)}");
                }
            }

            return conflicts;
        }

        private static void RemoveToolOwnedChildren(GameObject root)
        {
            var remove = new List<GameObject>();
            for (var i = 0; i < root.transform.childCount; i++)
            {
                var child = root.transform.GetChild(i);
                if (child.name.StartsWith(GeneratedPrefix, StringComparison.Ordinal))
                {
                    remove.Add(child.gameObject);
                }
            }

            for (var i = 0; i < remove.Count; i++)
            {
                UnityEngine.Object.DestroyImmediate(remove[i]);
            }
        }

        private static Transform FindTransformByPath(Scene scene, string path)
        {
            var parts = path.Split('/');
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var current = roots[i].transform;
                if (current.name != parts[0])
                {
                    current = FindChildRecursive(current, parts[0]);
                    if (current == null)
                    {
                        continue;
                    }
                }

                var matched = true;
                for (var p = 1; p < parts.Length; p++)
                {
                    current = current.Find(parts[p]);
                    if (current == null)
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                {
                    return current;
                }
            }

            return null;
        }

        private static Transform FindChildRecursive(Transform root, string name)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }

                var nested = FindChildRecursive(child, name);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static void CreateAssetFolders(string directory)
        {
            var parts = directory.Replace('\\', '/').Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static bool TryFindNearestSpatialNode(NavMeshSpatialGraph graph, Vector3 position, out int nodeId)
        {
            nodeId = -1;
            var best = float.PositiveInfinity;
            for (var i = 0; i < graph.NodeCount; i++)
            {
                var delta = graph.Nodes[i].Position - position;
                var score = delta.sqrMagnitude;
                if (score < best)
                {
                    best = score;
                    nodeId = graph.Nodes[i].Id;
                }
            }

            return nodeId >= 0;
        }

        private static int FindNearestRouteSourceIndex(NavMeshSpatialGraph graph, ComponentInfo component, IReadOnlyList<SemanticSource> sources)
        {
            var bestSource = -1;
            var best = float.PositiveInfinity;
            for (var i = 0; i < sources.Count; i++)
            {
                if (sources[i].Kind != SemanticKind.Route)
                {
                    continue;
                }

                var delta = component.Centroid - sources[i].AnchorPosition;
                var score = delta.sqrMagnitude;
                if (score < best)
                {
                    best = score;
                    bestSource = sources[i].SourceIndex;
                }
            }

            return bestSource;
        }

        private static UnclaimedComponentClassification ResolveComponentClassification(int componentId, BuildOptions options)
        {
            for (var i = 0; i < options.ComponentClassifications.Count; i++)
            {
                if (options.ComponentClassifications[i].ComponentId == componentId)
                {
                    return options.ComponentClassifications[i].Classification;
                }
            }

            return UnclaimedComponentClassification.Unspecified;
        }

        private static int CompareNodeGroups(IReadOnlyList<int> a, IReadOnlyList<int> b)
        {
            return a[0].CompareTo(b[0]);
        }

        private static void CountSemanticAttribution(SemanticSource source, int count, DryRunReport report)
        {
            if (source.Zone == SemanticZone.Zone01) report.Zone01NodeCount += count;
            if (source.Zone == SemanticZone.Zone02) report.Zone02NodeCount += count;
            if (source.Zone == SemanticZone.Zone03) report.Zone03NodeCount += count;
            if (source.Kind == SemanticKind.Route) report.CorridorRouteNodeCount += count;
        }

        private static bool TryGetRegion(IReadOnlyList<RegionId> nodeToRegion, int nodeId, out RegionId regionId)
        {
            if (nodeToRegion != null && nodeId >= 0 && nodeId < nodeToRegion.Count && nodeToRegion[nodeId].IsValid)
            {
                regionId = nodeToRegion[nodeId];
                return true;
            }

            regionId = RegionId.Invalid;
            return false;
        }

        private static float GetCoordinate(Vector3 value, SplitAxis axis)
        {
            if (axis == SplitAxis.X) return value.x;
            if (axis == SplitAxis.Y) return value.y;
            return value.z;
        }

        private static bool IsFinite(Bounds bounds)
        {
            return IsFinite(bounds.center) && IsFinite(bounds.size);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var names = new Stack<string>();
            var current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names.ToArray());
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({FormatFloat(value.x)}, {FormatFloat(value.y)}, {FormatFloat(value.z)})";
        }

        public readonly struct BuildOptions
        {
            public BuildOptions(
                int maxDecompositionDepth,
                UnclaimedComponentClassification defaultUnclaimedClassification,
                IReadOnlyList<ComponentClassification> componentClassifications)
            {
                MaxDecompositionDepth = maxDecompositionDepth;
                DefaultUnclaimedClassification = defaultUnclaimedClassification;
                ComponentClassifications = componentClassifications ?? Array.Empty<ComponentClassification>();
            }

            public int MaxDecompositionDepth { get; }
            public UnclaimedComponentClassification DefaultUnclaimedClassification { get; }
            public IReadOnlyList<ComponentClassification> ComponentClassifications { get; }
            public static BuildOptions Default => new BuildOptions(FullStationRegionGraphAuthoringCore.MaxDecompositionDepth, UnclaimedComponentClassification.IsolatedNavigableIsland, Array.Empty<ComponentClassification>());
        }

        public readonly struct ComponentClassification
        {
            public ComponentClassification(int componentId, UnclaimedComponentClassification classification)
            {
                ComponentId = componentId;
                Classification = classification;
            }

            public int ComponentId { get; }
            public UnclaimedComponentClassification Classification { get; }
        }

        public sealed class SemanticSource
        {
            public SemanticSource(
                int sourceIndex,
                SemanticZone zone,
                SemanticKind kind,
                string sourcePath,
                Vector3 anchorPosition,
                FloorEvidenceKind evidenceKind,
                IReadOnlyList<int> evidenceNodeIds)
                : this(sourceIndex, zone, kind, sourcePath, anchorPosition, evidenceKind, evidenceNodeIds, null, null)
            {
            }

            public SemanticSource(
                int sourceIndex,
                SemanticZone zone,
                SemanticKind kind,
                string sourcePath,
                Vector3 anchorPosition,
                FloorEvidenceKind evidenceKind,
                IReadOnlyList<int> evidenceNodeIds,
                IReadOnlyDictionary<int, List<string>> evidenceObjectPathsByNode)
                : this(sourceIndex, zone, kind, sourcePath, anchorPosition, evidenceKind, evidenceNodeIds, evidenceObjectPathsByNode, null)
            {
            }

            public SemanticSource(
                int sourceIndex,
                SemanticZone zone,
                SemanticKind kind,
                string sourcePath,
                Vector3 anchorPosition,
                FloorEvidenceKind evidenceKind,
                IReadOnlyList<int> evidenceNodeIds,
                IReadOnlyDictionary<int, List<string>> evidenceObjectPathsByNode,
                IReadOnlyDictionary<int, List<FloorEvidenceBoundsDiagnostic>> evidenceBoundsByNode)
            {
                SourceIndex = sourceIndex;
                Zone = zone;
                Kind = kind;
                SourcePath = sourcePath;
                AnchorPosition = anchorPosition;
                EvidenceKind = evidenceKind;
                EvidenceNodeIds = new List<int>(evidenceNodeIds ?? Array.Empty<int>());
                EvidenceNodeIds.Sort();
                EvidenceObjectPathsByNode = new Dictionary<int, List<string>>();
                if (evidenceObjectPathsByNode != null)
                {
                    foreach (var pair in evidenceObjectPathsByNode)
                    {
                        var paths = pair.Value != null ? new List<string>(pair.Value) : new List<string>();
                        paths.Sort(StringComparer.Ordinal);
                        EvidenceObjectPathsByNode.Add(pair.Key, paths);
                    }
                }

                EvidenceBoundsByNode = new Dictionary<int, List<FloorEvidenceBoundsDiagnostic>>();
                if (evidenceBoundsByNode != null)
                {
                    foreach (var pair in evidenceBoundsByNode)
                    {
                        var evidenceBounds = new List<FloorEvidenceBoundsDiagnostic>();
                        if (pair.Value != null)
                        {
                            for (var i = 0; i < pair.Value.Count; i++)
                            {
                                var evidence = pair.Value[i];
                                AddFloorEvidenceBounds(evidenceBounds, evidence.Bounds, evidence.HierarchyPath);
                            }
                        }

                        evidenceBounds.Sort(CompareFloorEvidenceBoundsDiagnostic);
                        EvidenceBoundsByNode.Add(pair.Key, evidenceBounds);
                    }
                }
            }

            public int SourceIndex { get; }
            public SemanticZone Zone { get; }
            public SemanticKind Kind { get; }
            public string SourcePath { get; }
            public Vector3 AnchorPosition { get; }
            public FloorEvidenceKind EvidenceKind { get; }
            public List<int> EvidenceNodeIds { get; }
            public Dictionary<int, List<string>> EvidenceObjectPathsByNode { get; }
            public Dictionary<int, List<FloorEvidenceBoundsDiagnostic>> EvidenceBoundsByNode { get; }

            public IReadOnlyList<string> GetEvidenceObjectPathsForNode(int nodeId)
            {
                return EvidenceObjectPathsByNode.TryGetValue(nodeId, out var paths) ? paths : Array.Empty<string>();
            }

            public IReadOnlyList<FloorEvidenceBoundsDiagnostic> GetEvidenceBoundsForNode(int nodeId)
            {
                return EvidenceBoundsByNode.TryGetValue(nodeId, out var bounds) ? bounds : Array.Empty<FloorEvidenceBoundsDiagnostic>();
            }
        }

        public sealed class GeneratedRegion
        {
            public GeneratedRegion(SemanticSource source, IReadOnlyList<int> nodeIds, Bounds bounds, int decompositionDepth, string splitReason)
            {
                Source = source;
                SourcePath = source.SourcePath;
                NodeIds = new List<int>(nodeIds);
                NodeIds.Sort();
                WorldBounds = bounds;
                DecompositionDepth = decompositionDepth;
                SplitReason = splitReason;
                SpatialNodeCount = NodeIds.Count;
                MinNodeId = NodeIds[0];
                MaxNodeId = NodeIds[NodeIds.Count - 1];
                MinY = bounds.min.y;
                MaxY = bounds.max.y;
                Centroid = bounds.center;
                RegionId = RegionId.Invalid;
            }

            public SemanticSource Source { get; }
            public string SourcePath { get; }
            public List<int> NodeIds { get; }
            public Bounds WorldBounds { get; }
            public int DecompositionDepth { get; }
            public string SplitReason { get; }
            public int SpatialNodeCount { get; }
            public int MinNodeId { get; }
            public int MaxNodeId { get; }
            public float MinY { get; }
            public float MaxY { get; }
            public Vector3 Centroid { get; }
            public RegionId RegionId { get; private set; }
            public void SetRegionId(RegionId regionId) => RegionId = regionId;
        }

        public sealed class RuntimeSemanticRegion
        {
            public RuntimeSemanticRegion(RegionId regionId, SemanticSource source, IReadOnlyList<int> nodeIds)
            {
                RegionId = regionId;
                SourceIndex = source != null ? source.SourceIndex : -1;
                SourcePath = source != null ? source.SourcePath : string.Empty;
                Zone = source != null ? source.Zone : SemanticZone.Isolated;
                Kind = source != null ? source.Kind : SemanticKind.IsolatedIsland;
                SpatialNodeCount = nodeIds?.Count ?? 0;
                MinNodeId = SpatialNodeCount > 0 ? nodeIds[0] : -1;
                MaxNodeId = SpatialNodeCount > 0 ? nodeIds[SpatialNodeCount - 1] : -1;
            }

            public RegionId RegionId { get; }
            public int SourceIndex { get; }
            public string SourcePath { get; }
            public SemanticZone Zone { get; }
            public SemanticKind Kind { get; }
            public int SpatialNodeCount { get; }
            public int MinNodeId { get; }
            public int MaxNodeId { get; }
        }

        public sealed class DryRunReport
        {
            public NavMeshSpatialGraph SpatialGraph { get; set; }
            public int SpatialNodeCount { get; set; }
            public int ConnectedComponentCount { get; set; }
            public int MappedNodeCount { get; set; }
            public int Zone01NodeCount { get; set; }
            public int Zone02NodeCount { get; set; }
            public int Zone03NodeCount { get; set; }
            public int CorridorRouteNodeCount { get; set; }
            public int IsolatedIslandNodeCount { get; set; }
            public SpatialGraphCompatibilityIdentity CompatibilityIdentity { get; set; }
            public RegionId[] NodeToRegion { get; set; } = Array.Empty<RegionId>();
            public RegionId[] RuntimeNodeToRegion { get; set; } = Array.Empty<RegionId>();
            public RegionGraph RuntimeGraph { get; set; }
            public int RuntimeSemanticRegionCount { get; set; }
            public RegionGraphBakeDiagnostic BakeDiagnostic { get; set; } = RegionGraphBakeDiagnostic.Success;
            public RegionGraphBakeDiagnostic GeometryBakeDiagnostic { get; set; } = RegionGraphBakeDiagnostic.Success;
            public List<SemanticSourceReport> SemanticSources { get; } = new List<SemanticSourceReport>();
            public List<GeneratedRegion> Regions { get; } = new List<GeneratedRegion>();
            public List<RegionEdgeBakeData> Edges { get; } = new List<RegionEdgeBakeData>();
            public List<RuntimeSemanticRegion> RuntimeRegions { get; } = new List<RuntimeSemanticRegion>();
            public List<RegionEdgeBakeData> RuntimeEdges { get; } = new List<RegionEdgeBakeData>();
            public List<RegionId> RuntimeInvalidRegionIds { get; } = new List<RegionId>();
            public List<RegionId> RuntimeDuplicateRegionIds { get; } = new List<RegionId>();
            public List<RegionId> RuntimeDisconnectedRegionIds { get; } = new List<RegionId>();
            public List<RegionId> RuntimeSelfTransitionRegionIds { get; } = new List<RegionId>();
            public List<int> RuntimeUnmappedNodeIds { get; } = new List<int>();
            public List<int> RuntimeDanglingNodeIds { get; } = new List<int>();
            public List<int> ZeroMappedNodeIds { get; } = new List<int>();
            public List<int> MultiplyMappedNodeIds { get; } = new List<int>();
            public List<int> SeedOverlapNodeIds { get; } = new List<int>();
            public List<int> ResolvedSeedOverlapNodeIds { get; } = new List<int>();
            public List<int> UnresolvedSeedOverlapNodeIds { get; } = new List<int>();
            public List<int> BoundaryTieNodeIds { get; } = new List<int>();
            public List<SeedEvidenceOverlapDetail> SeedEvidenceOverlapDetails { get; } = new List<SeedEvidenceOverlapDetail>();
            public List<int> UnresolvedNodeIds { get; } = new List<int>();
            public List<int> UnresolvedComponents { get; } = new List<int>();
            public List<string> UnresolvedSources { get; } = new List<string>();
            public List<ForeignNodeContamination> ForeignNodeContamination { get; } = new List<ForeignNodeContamination>();
            public List<RegionId> InvalidRegionIds { get; } = new List<RegionId>();
            public List<RegionId> DuplicateRegionIds { get; } = new List<RegionId>();
            public List<RegionId> DisconnectedRegionIds { get; } = new List<RegionId>();
            public List<string> DecompositionFailures { get; } = new List<string>();
            public List<string> RegionSplitReasons { get; } = new List<string>();
            public List<string> Errors { get; } = new List<string>();
            public List<string> Warnings { get; } = new List<string>();
            public List<string> Messages { get; } = new List<string>();

            public bool IsValid => Errors.Count == 0
                && MappedNodeCount == SpatialNodeCount
                && ZeroMappedNodeIds.Count == 0
                && MultiplyMappedNodeIds.Count == 0
                && UnresolvedNodeIds.Count == 0
                && UnresolvedComponents.Count == 0
                && UnresolvedSources.Count == 0
                && ForeignNodeContamination.Count == 0
                && InvalidRegionIds.Count == 0
                && DuplicateRegionIds.Count == 0
                && DisconnectedRegionIds.Count == 0
                && RuntimeInvalidRegionIds.Count == 0
                && RuntimeDuplicateRegionIds.Count == 0
                && RuntimeDisconnectedRegionIds.Count == 0
                && RuntimeSelfTransitionRegionIds.Count == 0
                && RuntimeUnmappedNodeIds.Count == 0
                && RuntimeDanglingNodeIds.Count == 0
                && DecompositionFailures.Count == 0
                && GeometryBakeDiagnostic.IsSuccess
                && BakeDiagnostic.IsSuccess;

            public string ToDisplayString()
            {
                var lines = new List<string>
                {
                    $"Spatial graph node count: {SpatialNodeCount}",
                    $"Spatial graph compatibility identity: {CompatibilityIdentity}",
                    $"Connected component count: {ConnectedComponentCount}",
                    $"Mapped nodes / total nodes: {MappedNodeCount} / {SpatialNodeCount}",
                    $"Zero mapped nodes: {ZeroMappedNodeIds.Count}",
                    $"Multiply mapped nodes: {MultiplyMappedNodeIds.Count}",
                    $"Seed evidence overlap nodes: {SeedOverlapNodeIds.Count} [{string.Join(", ", SeedOverlapNodeIds)}]",
                    $"Resolved seed overlap nodes: {ResolvedSeedOverlapNodeIds.Count} [{string.Join(", ", ResolvedSeedOverlapNodeIds)}]",
                    $"Unresolved seed overlap nodes: {UnresolvedSeedOverlapNodeIds.Count} [{string.Join(", ", UnresolvedSeedOverlapNodeIds)}]",
                    $"Equal-distance BFS boundary tie nodes: {BoundaryTieNodeIds.Count} [{string.Join(", ", BoundaryTieNodeIds)}]",
                    $"Unresolved nodes: {UnresolvedNodeIds.Count}",
                    $"Zone01 node count: {Zone01NodeCount}",
                    $"Zone02 node count: {Zone02NodeCount}",
                    $"Zone03 node count: {Zone03NodeCount}",
                    $"corridor/route node count: {CorridorRouteNodeCount}",
                    $"isolated island node count: {IsolatedIslandNodeCount}",
                    $"Generated geometry piece count: {Regions.Count}",
                    $"Runtime semantic region count: {RuntimeSemanticRegionCount}"
                };

                lines.Add("Semantic floor evidence:");
                for (var i = 0; i < SemanticSources.Count; i++)
                {
                    var source = SemanticSources[i];
                    lines.Add($"- {source.SourcePath}: {source.EvidenceKind}, evidence nodes {source.EvidenceNodeCount}");
                }

                lines.Add("Seed Evidence Overlap Details:");
                if (SeedEvidenceOverlapDetails.Count == 0)
                {
                    lines.Add("- none");
                }
                else
                {
                    for (var i = 0; i < SeedEvidenceOverlapDetails.Count; i++)
                    {
                        var detail = SeedEvidenceOverlapDetails[i];
                        lines.Add($"- Node {detail.NodeId} at {FormatVector(detail.Position)} — {detail.ClaimantCount} claimants");
                        for (var claimantIndex = 0; claimantIndex < detail.Claimants.Count; claimantIndex++)
                        {
                            var claimant = detail.Claimants[claimantIndex];
                            lines.Add($"  - [{claimant.SourceIndex}] {claimant.Zone}/{claimant.Kind}: {claimant.SourcePath}");
                            if (claimant.SupportingEvidence.Count > 0)
                            {
                                for (var evidenceIndex = 0; evidenceIndex < claimant.SupportingEvidence.Count; evidenceIndex++)
                                {
                                    var evidence = claimant.SupportingEvidence[evidenceIndex];
                                    lines.Add($"    evidence object: {evidence.HierarchyPath}");
                                    lines.Add($"      bounds center: {FormatVector(evidence.BoundsCenter)}");
                                    lines.Add($"      bounds size: {FormatVector(evidence.BoundsSize)}");
                                    lines.Add($"      horizontal interior margin: {FormatFloat(evidence.HorizontalInteriorMargin)}m");
                                    lines.Add($"      vertical delta to bounds center: {FormatFloat(evidence.VerticalDeltaToBoundsCenter)}m");
                                }
                            }
                            else if (claimant.EvidenceObjectPaths.Count == 0)
                            {
                                lines.Add("    evidence object: none");
                            }
                            else
                            {
                                for (var pathIndex = 0; pathIndex < claimant.EvidenceObjectPaths.Count; pathIndex++)
                                {
                                    lines.Add($"    evidence object: {claimant.EvidenceObjectPaths[pathIndex]}");
                                }
                            }
                        }
                    }
                }

                lines.Add("Runtime semantic regions:");
                for (var i = 0; i < RuntimeRegions.Count; i++)
                {
                    var region = RuntimeRegions[i];
                    lines.Add($"- RegionId {region.RegionId.Value}: sourceIndex={region.SourceIndex}, {region.SourcePath}");
                    lines.Add($"  node count: {region.SpatialNodeCount}, min/max node ID: {region.MinNodeId}/{region.MaxNodeId}");
                }

                lines.Add("Generated regions:");
                for (var i = 0; i < Regions.Count; i++)
                {
                    var region = Regions[i];
                    lines.Add($"- RegionId {region.RegionId.Value}: {region.SourcePath}");
                    lines.Add($"  node count: {region.SpatialNodeCount}, min/max node ID: {region.MinNodeId}/{region.MaxNodeId}");
                    lines.Add($"  bounds center/size: {FormatVector(region.WorldBounds.center)} / {FormatVector(region.WorldBounds.size)}");
                    lines.Add($"  min/max Y: {FormatFloat(region.MinY)}/{FormatFloat(region.MaxY)}");
                    lines.Add($"  decomposition depth: {region.DecompositionDepth}, split reason: {region.SplitReason}");
                }

                lines.Add($"foreign-node contamination: {ForeignNodeContamination.Count}");
                lines.Add($"disconnected generated region: {DisconnectedRegionIds.Count}");
                lines.Add($"invalid/duplicate IDs: {InvalidRegionIds.Count + DuplicateRegionIds.Count}");
                lines.Add($"decomposition failures: {DecompositionFailures.Count}");
                lines.Add($"unresolved components: {string.Join(", ", UnresolvedComponents)}");
                lines.Add($"runtime disconnected semantic regions: {RuntimeDisconnectedRegionIds.Count}");
                lines.Add($"geometry adjacency edge count: {Edges.Count}");
                lines.Add($"runtime semantic adjacency edge count: {RuntimeEdges.Count}");
                lines.Add($"Geometry bake diagnostic: {GeometryBakeDiagnostic.Failure}");
                lines.Add($"Runtime semantic graph diagnostic: {BakeDiagnostic.Failure}");
                lines.Add($"Valid: {IsValid}");
                AppendSection(lines, "Errors", Errors);
                AppendSection(lines, "Warnings", Warnings);
                AppendSection(lines, "Messages", Messages);
                return string.Join(Environment.NewLine, lines);
            }

            private static void AppendSection(List<string> lines, string title, IReadOnlyList<string> values)
            {
                lines.Add($"{title}:");
                if (values.Count == 0)
                {
                    lines.Add("- none");
                    return;
                }

                for (var i = 0; i < values.Count; i++)
                {
                    lines.Add($"- {values[i]}");
                }
            }
        }

        public readonly struct SemanticSourceReport
        {
            public SemanticSourceReport(SemanticSource source)
            {
                SourcePath = source.SourcePath;
                Zone = source.Zone;
                Kind = source.Kind;
                EvidenceKind = source.EvidenceKind;
                EvidenceNodeCount = source.EvidenceNodeIds.Count;
            }

            public string SourcePath { get; }
            public SemanticZone Zone { get; }
            public SemanticKind Kind { get; }
            public FloorEvidenceKind EvidenceKind { get; }
            public int EvidenceNodeCount { get; }
        }

        public sealed class SeedEvidenceOverlapDetail
        {
            public SeedEvidenceOverlapDetail(int nodeId, Vector3 position, IReadOnlyList<SeedEvidenceOverlapClaimant> claimants)
            {
                NodeId = nodeId;
                Position = position;
                Claimants = new List<SeedEvidenceOverlapClaimant>(claimants ?? Array.Empty<SeedEvidenceOverlapClaimant>());
                ClaimantCount = Claimants.Count;
            }

            public int NodeId { get; }
            public Vector3 Position { get; }
            public List<SeedEvidenceOverlapClaimant> Claimants { get; }
            public int ClaimantCount { get; }
        }

        public readonly struct SeedEvidenceOverlapClaimant
        {
            public SeedEvidenceOverlapClaimant(SemanticSource source, int nodeId)
            {
                SourceIndex = source.SourceIndex;
                Zone = source.Zone;
                Kind = source.Kind;
                SourcePath = source.SourcePath;
                EvidenceKind = source.EvidenceKind;
                EvidenceObjectPaths = new List<string>(source.GetEvidenceObjectPathsForNode(nodeId));
                SupportingEvidence = new List<SeedEvidenceOverlapSupportingEvidence>();
            }

            public SeedEvidenceOverlapClaimant(SemanticSource source, int nodeId, Vector3 nodePosition)
            {
                SourceIndex = source.SourceIndex;
                Zone = source.Zone;
                Kind = source.Kind;
                SourcePath = source.SourcePath;
                EvidenceKind = source.EvidenceKind;
                EvidenceObjectPaths = new List<string>(source.GetEvidenceObjectPathsForNode(nodeId));
                SupportingEvidence = new List<SeedEvidenceOverlapSupportingEvidence>();

                var evidenceBounds = source.GetEvidenceBoundsForNode(nodeId);
                for (var i = 0; i < evidenceBounds.Count; i++)
                {
                    var evidence = evidenceBounds[i];
                    if (!IsSupportedByFloorBounds(nodePosition, evidence.Bounds))
                    {
                        continue;
                    }

                    SupportingEvidence.Add(new SeedEvidenceOverlapSupportingEvidence(evidence, nodePosition));
                }
            }

            public int SourceIndex { get; }
            public SemanticZone Zone { get; }
            public SemanticKind Kind { get; }
            public string SourcePath { get; }
            public FloorEvidenceKind EvidenceKind { get; }
            public List<string> EvidenceObjectPaths { get; }
            public List<SeedEvidenceOverlapSupportingEvidence> SupportingEvidence { get; }
        }

        public readonly struct SeedEvidenceOverlapSupportingEvidence
        {
            public SeedEvidenceOverlapSupportingEvidence(FloorEvidenceBoundsDiagnostic evidence, Vector3 nodePosition)
            {
                HierarchyPath = evidence.HierarchyPath;
                BoundsCenter = evidence.Bounds.center;
                BoundsSize = evidence.Bounds.size;
                HorizontalInteriorMargin = Mathf.Min(
                    Mathf.Min(
                        nodePosition.x - evidence.Bounds.min.x,
                        evidence.Bounds.max.x - nodePosition.x),
                    Mathf.Min(
                        nodePosition.z - evidence.Bounds.min.z,
                        evidence.Bounds.max.z - nodePosition.z));
                VerticalDeltaToBoundsCenter = Mathf.Abs(nodePosition.y - evidence.Bounds.center.y);
            }

            public string HierarchyPath { get; }
            public Vector3 BoundsCenter { get; }
            public Vector3 BoundsSize { get; }
            public float HorizontalInteriorMargin { get; }
            public float VerticalDeltaToBoundsCenter { get; }
        }

        public readonly struct ForeignNodeContamination
        {
            public ForeignNodeContamination(string sourcePath, int regionId, int foreignNodeId, Bounds bounds)
            {
                SourcePath = sourcePath;
                RegionId = regionId;
                ForeignNodeId = foreignNodeId;
                Bounds = bounds;
            }

            public string SourcePath { get; }
            public int RegionId { get; }
            public int ForeignNodeId { get; }
            public Bounds Bounds { get; }
        }

        public readonly struct ComponentInfo
        {
            public ComponentInfo(int componentId, IReadOnlyList<int> nodeIds, Bounds bounds)
            {
                ComponentId = componentId;
                NodeIds = new List<int>(nodeIds);
                Bounds = bounds;
                NodeCount = NodeIds.Count;
                MinNodeId = NodeIds[0];
                Centroid = bounds.center;
            }

            public int ComponentId { get; }
            public List<int> NodeIds { get; }
            public Bounds Bounds { get; }
            public int NodeCount { get; }
            public int MinNodeId { get; }
            public Vector3 Centroid { get; }
        }

        private sealed class AttributionResult
        {
            public AttributionResult(int nodeCount)
            {
                OwnerByNode = new int[nodeCount];
                DistanceByNode = new int[nodeCount];
                for (var i = 0; i < nodeCount; i++)
                {
                    OwnerByNode[i] = -1;
                    DistanceByNode[i] = int.MaxValue;
                }
            }

            public int[] OwnerByNode { get; }
            public int[] DistanceByNode { get; }
            public List<int> SeedOverlapNodeIds { get; } = new List<int>();
            public List<int> ResolvedSeedOverlapNodeIds { get; } = new List<int>();
            public List<int> UnresolvedSeedOverlapNodeIds { get; } = new List<int>();
            public List<int> BoundaryTieNodeIds { get; } = new List<int>();
            public List<SemanticSource> SyntheticSources { get; } = new List<SemanticSource>();
        }

        private readonly struct SeedGeometryCandidate
        {
            public SeedGeometryCandidate(
                int sourceIndex,
                float horizontalInteriorMargin,
                float verticalDeltaToBoundsCenter,
                float horizontalCenterDistanceSquared)
            {
                SourceIndex = sourceIndex;
                HorizontalInteriorMargin = horizontalInteriorMargin;
                VerticalDeltaToBoundsCenter = verticalDeltaToBoundsCenter;
                HorizontalCenterDistanceSquared = horizontalCenterDistanceSquared;
            }

            public int SourceIndex { get; }
            public float HorizontalInteriorMargin { get; }
            public float VerticalDeltaToBoundsCenter { get; }
            public float HorizontalCenterDistanceSquared { get; }
        }

        private readonly struct SeedNode : IComparable<SeedNode>
        {
            public SeedNode(int sourceIndex, int nodeId)
            {
                SourceIndex = sourceIndex;
                NodeId = nodeId;
            }

            public int SourceIndex { get; }
            public int NodeId { get; }
            public int CompareTo(SeedNode other)
            {
                var source = SourceIndex.CompareTo(other.SourceIndex);
                return source != 0 ? source : NodeId.CompareTo(other.NodeId);
            }
        }

        public readonly struct FloorEvidenceBoundsDiagnostic
        {
            public FloorEvidenceBoundsDiagnostic(Bounds bounds, string hierarchyPath)
            {
                Bounds = bounds;
                HierarchyPath = hierarchyPath ?? string.Empty;
            }

            public Bounds Bounds { get; }
            public string HierarchyPath { get; }
        }

        private readonly struct NodeCoord : IComparable<NodeCoord>
        {
            public NodeCoord(int nodeId, float coordinate)
            {
                NodeId = nodeId;
                Coordinate = coordinate;
            }

            public int NodeId { get; }
            public float Coordinate { get; }
            public int CompareTo(NodeCoord other)
            {
                var coord = Coordinate.CompareTo(other.Coordinate);
                return coord != 0 ? coord : NodeId.CompareTo(other.NodeId);
            }
        }

        private readonly struct SplitDecision
        {
            public SplitDecision(SplitAxis axis, float pivot, float score, string reason)
            {
                Axis = axis;
                Pivot = pivot;
                Score = score;
                Reason = reason;
                IsValid = true;
            }

            public SplitAxis Axis { get; }
            public float Pivot { get; }
            public float Score { get; }
            public string Reason { get; }
            public bool IsValid { get; }
            public static SplitDecision Invalid => default;
        }

        private readonly struct DirectedRegionEdgeKey : IComparable<DirectedRegionEdgeKey>
        {
            public DirectedRegionEdgeKey(RegionId from, RegionId to)
            {
                From = from;
                To = to;
            }

            public RegionId From { get; }
            public RegionId To { get; }
            public int CompareTo(DirectedRegionEdgeKey other)
            {
                var from = From.CompareTo(other.From);
                return from != 0 ? from : To.CompareTo(other.To);
            }
        }

        private readonly struct ActiveSceneContext
        {
            public ActiveSceneContext(NavMeshSpatialGraph spatialGraph, IReadOnlyList<SemanticSource> sources, IReadOnlyList<string> authoringConflicts)
            {
                SpatialGraph = spatialGraph;
                Sources = sources;
                AuthoringConflicts = authoringConflicts;
            }

            public NavMeshSpatialGraph SpatialGraph { get; }
            public IReadOnlyList<SemanticSource> Sources { get; }
            public IReadOnlyList<string> AuthoringConflicts { get; }
        }

        private readonly struct SemanticCatalogEntry
        {
            public SemanticCatalogEntry(string parentPath, string leafName, SemanticZone zone, SemanticKind kind)
            {
                ParentPath = parentPath;
                LeafName = leafName;
                Zone = zone;
                Kind = kind;
            }

            public string ParentPath { get; }
            public string LeafName { get; }
            public SemanticZone Zone { get; }
            public SemanticKind Kind { get; }
        }

        private enum SplitAxis
        {
            X,
            Z,
            Y
        }
    }

    public enum SemanticZone
    {
        Zone01,
        Zone02,
        Zone03,
        Isolated
    }

    public enum SemanticKind
    {
        Room,
        Route,
        IsolatedIsland
    }

    public enum FloorEvidenceKind
    {
        FloorEvidence,
        TransformFallback,
        IsolatedIsland,
        Unresolved
    }

    public enum UnclaimedComponentClassification
    {
        Unspecified,
        CorridorRoute,
        IsolatedNavigableIsland,
        Unresolved
    }
}
#endif
