using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common.Spatial;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Spatial
{
    [CreateAssetMenu(menuName = "Echo Protocol/AI/Stalker/Region Graph Asset")]
    public sealed class RegionGraphAsset : ScriptableObject
    {
        [SerializeField] private int definitionVersion = 1;
        [SerializeField] private ulong spatialGraphCompatibilityIdentity;
        [SerializeField] private RegionRecord[] regions = Array.Empty<RegionRecord>();
        [SerializeField] private NodeRegionRecord[] nodeToRegionMap = Array.Empty<NodeRegionRecord>();

        public SpatialGraphCompatibilityIdentity CompatibilityIdentity => new SpatialGraphCompatibilityIdentity(spatialGraphCompatibilityIdentity);

        public RegionGraph BuildRuntimeGraph()
        {
            var regionNodes = new List<RegionNode>(regions?.Length ?? 0);
            if (regions != null)
            {
                for (var i = 0; i < regions.Length; i++)
                {
                    var edges = new List<RegionEdge>(regions[i].Edges?.Length ?? 0);
                    if (regions[i].Edges != null)
                    {
                        for (var edgeIndex = 0; edgeIndex < regions[i].Edges.Length; edgeIndex++)
                        {
                            var edge = regions[i].Edges[edgeIndex];
                            edges.Add(new RegionEdge(ToRegionId(edge.ToRegionId), ToDoorId(edge.DoorId)));
                        }
                    }

                    regionNodes.Add(new RegionNode(ToRegionId(regions[i].RegionId), edges));
                }
            }

            var maxNodeId = -1;
            if (nodeToRegionMap != null)
            {
                for (var i = 0; i < nodeToRegionMap.Length; i++)
                {
                    maxNodeId = Mathf.Max(maxNodeId, nodeToRegionMap[i].SpatialNodeId);
                }
            }

            var map = new RegionId[maxNodeId + 1];
            for (var i = 0; i < map.Length; i++)
            {
                map[i] = RegionId.Invalid;
            }

            if (nodeToRegionMap != null)
            {
                for (var i = 0; i < nodeToRegionMap.Length; i++)
                {
                    var record = nodeToRegionMap[i];
                    if (record.SpatialNodeId >= 0 && record.SpatialNodeId < map.Length)
                    {
                        map[record.SpatialNodeId] = ToRegionId(record.RegionId);
                    }
                }
            }

            return new RegionGraph(regionNodes, map, CompatibilityIdentity, definitionVersion);
        }

        public void ConfigureFromRuntimeGraph(RegionGraph graph, int spatialNodeCount)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            definitionVersion = graph.DefinitionVersion;
            spatialGraphCompatibilityIdentity = graph.CompatibilityIdentity.Value;

            regions = new RegionRecord[graph.Regions.Count];
            for (var i = 0; i < graph.Regions.Count; i++)
            {
                var region = graph.Regions[i];
                var edgeRecords = new RegionEdgeRecord[region.Edges.Count];
                for (var edgeIndex = 0; edgeIndex < region.Edges.Count; edgeIndex++)
                {
                    var edge = region.Edges[edgeIndex];
                    edgeRecords[edgeIndex] = new RegionEdgeRecord
                    {
                        ToRegionId = edge.ToRegionId.Value,
                        DoorId = edge.DoorId.IsValid ? edge.DoorId.Value : 0
                    };
                }

                regions[i] = new RegionRecord
                {
                    RegionId = region.Id.Value,
                    Edges = edgeRecords
                };
            }

            nodeToRegionMap = new NodeRegionRecord[Mathf.Max(0, spatialNodeCount)];
            for (var nodeId = 0; nodeId < nodeToRegionMap.Length; nodeId++)
            {
                graph.TryGetRegionForNode(nodeId, out var regionId);
                nodeToRegionMap[nodeId] = new NodeRegionRecord
                {
                    SpatialNodeId = nodeId,
                    RegionId = regionId.IsValid ? regionId.Value : 0
                };
            }
        }

        public RegionGraphBakeDiagnostic ValidateAgainst(NavMeshSpatialGraph spatialGraph)
        {
            if (regions != null)
            {
                var seen = new HashSet<int>();
                for (var i = 0; i < regions.Length; i++)
                {
                    if (regions[i].RegionId <= 0)
                    {
                        return new RegionGraphBakeDiagnostic(RegionGraphBakeFailure.InvalidRegionId, -1, RegionId.Invalid, RegionId.Invalid);
                    }

                    if (!seen.Add(regions[i].RegionId))
                    {
                        return new RegionGraphBakeDiagnostic(RegionGraphBakeFailure.DuplicateRegionId, -1, new RegionId(regions[i].RegionId), RegionId.Invalid);
                    }

                    if (regions[i].Edges == null)
                    {
                        continue;
                    }

                    for (var edgeIndex = 0; edgeIndex < regions[i].Edges.Length; edgeIndex++)
                    {
                        var edge = regions[i].Edges[edgeIndex];
                        if (edge.ToRegionId <= 0 || edge.ToRegionId == regions[i].RegionId)
                        {
                            return new RegionGraphBakeDiagnostic(RegionGraphBakeFailure.InvalidRegionEdge, -1, new RegionId(regions[i].RegionId), RegionId.Invalid);
                        }

                        if (edge.DoorId < 0)
                        {
                            return new RegionGraphBakeDiagnostic(RegionGraphBakeFailure.MalformedDoorId, -1, new RegionId(regions[i].RegionId), new RegionId(edge.ToRegionId));
                        }
                    }
                }
            }

            return RegionGraphBakeUtility.ValidateRuntimeGraph(BuildRuntimeGraph(), spatialGraph);
        }

        public void ConfigureForTests(
            int version,
            SpatialGraphCompatibilityIdentity compatibilityIdentity,
            IReadOnlyList<RegionRecord> regionRecords,
            IReadOnlyList<NodeRegionRecord> nodeRegionRecords)
        {
            definitionVersion = version;
            spatialGraphCompatibilityIdentity = compatibilityIdentity.Value;
            regions = Copy(regionRecords);
            nodeToRegionMap = Copy(nodeRegionRecords);
        }

        private static RegionId ToRegionId(int value) => value > 0 ? new RegionId(value) : RegionId.Invalid;
        private static DoorId ToDoorId(int value) => value > 0 ? new DoorId(value) : DoorId.Invalid;

        private static RegionRecord[] Copy(IReadOnlyList<RegionRecord> source)
        {
            var copy = new RegionRecord[source?.Count ?? 0];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = source[i];
            }

            return copy;
        }

        private static NodeRegionRecord[] Copy(IReadOnlyList<NodeRegionRecord> source)
        {
            var copy = new NodeRegionRecord[source?.Count ?? 0];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = source[i];
            }

            return copy;
        }
    }

    [Serializable]
    public struct RegionRecord
    {
        public int RegionId;
        public RegionEdgeRecord[] Edges;
    }

    [Serializable]
    public struct RegionEdgeRecord
    {
        public int ToRegionId;
        public int DoorId;
    }

    [Serializable]
    public struct NodeRegionRecord
    {
        public int SpatialNodeId;
        public int RegionId;
    }
}
