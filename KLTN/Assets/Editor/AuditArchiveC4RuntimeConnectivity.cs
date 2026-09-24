using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common.Spatial;
using EchoProtocol.AI.Stalker.Spatial;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class AuditArchiveC4RuntimeConnectivity
{
    private const string AssetPath =
        "Assets/AI/Stalker/Station/AI_Stalker_FullStation_RegionGraph.asset";

    [MenuItem("Tools/ECHO/Stalker/Audit Archive C4 Connectivity")]
    private static void Run()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("[STK_CONNECT] Enter Play Mode and select StalkerNetwork(Clone).");
            return;
        }

        var selected = Selection.activeGameObject;
        var agent = selected != null ? selected.GetComponent<NavMeshAgent>() : null;
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            Debug.LogError("[STK_CONNECT] Select a spawned StalkerNetwork(Clone) with an active NavMeshAgent on NavMesh.");
            return;
        }

        var asset = AssetDatabase.LoadAssetAtPath<RegionGraphAsset>(AssetPath);
        if (asset == null)
        {
            Debug.LogError("[STK_CONNECT] RegionGraph asset not found.");
            return;
        }

        var spatial = NavMeshSpatialGraphBuilder.Build();
        var graph = asset.BuildRuntimeGraph();

        if (graph.CompatibilityIdentity != spatial.CompatibilityIdentity
            || graph.NodeMappingCount != spatial.NodeCount)
        {
            Debug.LogError(
                $"[STK_CONNECT] STALE_GRAPH: baked={graph.CompatibilityIdentity} " +
                $"live={spatial.CompatibilityIdentity} bakedMap={graph.NodeMappingCount} " +
                $"liveNodes={spatial.NodeCount}. STOP: do not compare stale node IDs.");
            return;
        }

        var origin = agent.transform.position;
        int nearestNodeId = -1;
        float nearestDistanceSq = float.PositiveInfinity;
        for (int i = 0; i < spatial.NodeCount; i++)
        {
            var node = spatial.Nodes[i];
            float distanceSq = (node.Position - origin).sqrMagnitude;
            if (distanceSq < nearestDistanceSq)
            {
                nearestDistanceSq = distanceSq;
                nearestNodeId = node.Id;
            }
        }

        if (!graph.TryGetRegionForNode(nearestNodeId, out var startRegion))
        {
            Debug.LogError("[STK_CONNECT] Unable to map Stalker position to a region.");
            return;
        }

        Debug.Log(
            $"[STK_CONNECT] START pos={origin} nearestNode={nearestNodeId} " +
            $"nearestDistance={Mathf.Sqrt(nearestDistanceSq):F2} region={startRegion.Value} " +
            $"liveNodes={spatial.NodeCount}. Testing Zone01 ROOM regions only.");

        if (startRegion.Value != 31)
        {
            Debug.LogWarning(
                "[STK_CONNECT] Stalker is not in Archive region 31. " +
                "Results describe its CURRENT component, not the Archive C4 failure.");
        }

        int graphDisconnectedUnityComplete = 0;
        int graphDisconnectedUnityNoComplete = 0;
        int graphConnectedUnityNoComplete = 0;
        int tested = 0;

        foreach (var region in graph.Regions)
        {
            var meta = region.SemanticMetadata;
            if (!meta.HasMetadata
                || meta.Zone != RegionSemanticZone.Zone01
                || meta.Kind != RegionSemanticKind.Room
                || region.Id == startRegion
                || !graph.TryGetSpatialNodeIdsForRegion(region.Id, out var nodeIds)
                || nodeIds.Count == 0)
            {
                continue;
            }

            bool graphRoute = graph.TryGetRouteHopCost(startRegion, region.Id, out _);
            // Check up to 8 distinct centroids per semantic region. This is a
            // diagnostic sample, NOT proof that every point in a room is reachable.
            var sorted = new List<int>(nodeIds);
            sorted.Sort((a, b) =>
                (spatial.Nodes[a].Position - origin).sqrMagnitude.CompareTo(
                    (spatial.Nodes[b].Position - origin).sqrMagnitude));

            bool unityComplete = false;
            string bestStatus = "None";
            int sampleNodeId = -1;
            int count = Mathf.Min(8, sorted.Count);
            for (int i = 0; i < count; i++)
            {
                int nodeId = sorted[i];
                var target = spatial.Nodes[nodeId].Position;
                var path = new NavMeshPath();
                bool accepted = agent.CalculatePath(target, path);
                if (sampleNodeId < 0)
                {
                    bestStatus = $"accepted={accepted} status={path.status}";
                    sampleNodeId = nodeId;
                }

                if (accepted && path.status == NavMeshPathStatus.PathComplete)
                {
                    // Read-only diagnostic for the two Unity-reachable rooms absent from the graph.
                    if (!graphRoute && (region.Id.Value == 32 || region.Id.Value == 33))
                    {
                        TraceMissingRoute(path, spatial, graph, startRegion, region.Id, agent.areaMask);
                    }

                    unityComplete = true;
                    bestStatus = "PathComplete";
                    sampleNodeId = nodeId;
                    break;
                }
            }

            tested++;
            if (!graphRoute && unityComplete) graphDisconnectedUnityComplete++;
            if (!graphRoute && !unityComplete) graphDisconnectedUnityNoComplete++;
            if (graphRoute && !unityComplete) graphConnectedUnityNoComplete++;

            Debug.Log(
                $"[STK_CONNECT] targetRegion={region.Id.Value} room={meta.SourcePath} " +
                $"graphRoute={graphRoute} unityComplete={unityComplete} " +
                $"sampled={count} sampleNode={sampleNodeId} unityResult={bestStatus}");
        }

        Debug.Log(
            $"[STK_CONNECT] SUMMARY testedRoomRegions={tested} " +
            $"GRAPH_MISSING_BUT_UNITY_COMPLETE={graphDisconnectedUnityComplete} " +
            $"GRAPH_AND_UNITY_NO_COMPLETE={graphDisconnectedUnityNoComplete} " +
            $"GRAPH_ROUTE_BUT_UNITY_NO_COMPLETE={graphConnectedUnityNoComplete}. " +
            "A no-complete result is limited to sampled points; no scene/asset was changed.");

    }

    // Approximate the first graph-disconnected point along Unity's complete path.
    // Nearest triangle centroid is diagnostic only; do not create graph edges from it.
    private static void TraceMissingRoute(
        NavMeshPath path, NavMeshSpatialGraph spatial, RegionGraph graph,
        RegionId startRegion, RegionId targetRegion, int areaMask)
    {
        int previousRegion = -1;
        int previousNode = -1;
        Vector3 previousSample = Vector3.zero;
        float previousCentroidOffset = 0f;
        bool previousReachable = false;
        bool hasPreviousSample = false;
        bool seamLogged = false;
        int printed = 0;
        var corners = path.corners;
        Debug.Log($"[STK_TRACE] target={targetRegion.Value} corners={corners.Length}");
        for (int segment = 0; segment + 1 < corners.Length; segment++)
        {
            // Sample at 0.25m along the actual Unity path to narrow down the seam.
            int steps = Mathf.Max(1, Mathf.CeilToInt(
                Vector3.Distance(corners[segment], corners[segment + 1]) / 0.25f));
            for (int step = 0; step <= steps; step++)
            {
                var point = Vector3.Lerp(corners[segment], corners[segment + 1],
                    (float)step / steps);
                if (!NavMesh.SamplePosition(point, out var hit, 0.5f, areaMask))
                    continue;

                int nearestNode = -1;
                float nearestSq = float.PositiveInfinity;
                for (int n = 0; n < spatial.NodeCount; n++)
                {
                    var node = spatial.Nodes[n];
                    if (Mathf.Abs(node.Position.y - hit.position.y) > 1f)
                        continue; // Avoid mapping this path to a different floor.
                    float distanceSq = (node.Position - hit.position).sqrMagnitude;
                    if (distanceSq >= nearestSq) continue;
                    nearestSq = distanceSq;
                    nearestNode = n;
                }

                if (nearestNode < 0 ||
                    !graph.TryGetRegionForNode(nearestNode, out var currentRegion))
                    continue;

                bool reachable = graph.TryGetRouteHopCost(startRegion, currentRegion, out _);
                float centroidOffset = Mathf.Sqrt(nearestSq);
                if (!seamLogged && hasPreviousSample && previousReachable && !reachable)
                {
                    // Diagnostic only: nearest centroid can misclassify a boundary.
                    // Never turn these two node IDs into a navigation edge directly.
                    Debug.Log($"[STK_SEAM] target={targetRegion.Value} segment={segment} " +
                        $"previousSample={previousSample} previousNode={previousNode} " +
                        $"previousRegion={previousRegion} previousOffset={previousCentroidOffset:F2} " +
                        $"firstUnreachableSample={hit.position} firstUnreachableNode={nearestNode} " +
                        $"firstUnreachableRegion={currentRegion.Value} firstUnreachableOffset={centroidOffset:F2} " +
                        $"sampleDistance={Vector3.Distance(previousSample, hit.position):F2} " +
                        "approximateOnly=True");
                    seamLogged = true;
                }

                if (currentRegion.Value != previousRegion)
                {
                    Debug.Log($"[STK_TRACE] target={targetRegion.Value} segment={segment} " +
                        $"sample={hit.position} node={nearestNode} region={currentRegion.Value} " +
                        $"graphReachable={reachable} centroidOffset={centroidOffset:F2}");
                    if (++printed >= 40) return; // Bound log volume.
                }

                hasPreviousSample = true;
                previousSample = hit.position;
                previousNode = nearestNode;
                previousRegion = currentRegion.Value;
                previousCentroidOffset = centroidOffset;
                previousReachable = reachable;
            }
        }
    }
}
