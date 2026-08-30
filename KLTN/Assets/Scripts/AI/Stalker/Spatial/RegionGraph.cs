using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common.Spatial;

namespace EchoProtocol.AI.Stalker.Spatial
{
    public enum RegionGraphValidationFailure
    {
        None,
        MissingGraph,
        InvalidRegionId,
        DuplicateRegionId,
        InvalidRegionEdge,
        RegionEdgeMissingRegion,
        DanglingDoorId,
        NodeToRegionMapSizeMismatch,
        InvalidDanglingNodeMapping,
        SpatialGraphCompatibilityMismatch,
        UnreachableRegion
    }

    public enum RegionGraphFallbackReason
    {
        None,
        MissingRegionGraph,
        MalformedRegionGraph,
        SpatialGraphCompatibilityMismatch,
        InvalidNodeToRegionMap,
        NoReachableRegionObjective,
        NoCompleteLocalPath
    }

    public sealed class RegionGraph
    {
        private readonly RegionNode[] _regions;
        private readonly RegionId[] _nodeToRegionMap;
        private readonly Dictionary<RegionId, int> _regionIndexById;
        private readonly bool[] _regionEnabled;
        private readonly bool[] _edgeOpen;

        public RegionGraph(
            IReadOnlyList<RegionNode> regions,
            IReadOnlyList<RegionId> nodeToRegionMap,
            SpatialGraphCompatibilityIdentity compatibilityIdentity,
            int definitionVersion)
        {
            _regions = new RegionNode[regions?.Count ?? 0];
            _regionIndexById = new Dictionary<RegionId, int>(_regions.Length);
            for (var i = 0; i < _regions.Length; i++)
            {
                var region = regions[i];
                _regions[i] = region;
                if (region.Id.IsValid && !_regionIndexById.ContainsKey(region.Id))
                {
                    _regionIndexById.Add(region.Id, i);
                }
            }

            _nodeToRegionMap = new RegionId[nodeToRegionMap?.Count ?? 0];
            for (var i = 0; i < _nodeToRegionMap.Length; i++)
            {
                _nodeToRegionMap[i] = nodeToRegionMap[i];
            }

            _regionEnabled = new bool[_regions.Length];
            for (var i = 0; i < _regionEnabled.Length; i++)
            {
                _regionEnabled[i] = true;
            }

            var edgeCount = 0;
            for (var i = 0; i < _regions.Length; i++)
            {
                edgeCount += _regions[i].Edges.Count;
            }

            _edgeOpen = new bool[edgeCount];
            for (var i = 0; i < _edgeOpen.Length; i++)
            {
                _edgeOpen[i] = true;
            }

            CompatibilityIdentity = compatibilityIdentity;
            DefinitionVersion = definitionVersion;
        }

        public IReadOnlyList<RegionNode> Regions => _regions;
        public int NodeMappingCount => _nodeToRegionMap.Length;
        public SpatialGraphCompatibilityIdentity CompatibilityIdentity { get; }
        public int DefinitionVersion { get; }

        public bool TryGetRegionForNode(int spatialNodeId, out RegionId regionId)
        {
            if (spatialNodeId >= 0 && spatialNodeId < _nodeToRegionMap.Length)
            {
                regionId = _nodeToRegionMap[spatialNodeId];
                return regionId.IsValid && ContainsRegion(regionId);
            }

            regionId = RegionId.Invalid;
            return false;
        }

        public bool ContainsRegion(RegionId regionId)
        {
            return regionId.IsValid && _regionIndexById.ContainsKey(regionId);
        }

        public bool IsRegionEnabled(RegionId regionId)
        {
            return TryGetRegionIndex(regionId, out var index) && _regionEnabled[index];
        }

        public bool TrySetRegionEnabled(RegionId regionId, bool enabled)
        {
            if (!TryGetRegionIndex(regionId, out var index))
            {
                return false;
            }

            _regionEnabled[index] = enabled;
            return true;
        }

        public bool TrySetEdgeOpen(RegionId from, RegionId to, bool open)
        {
            if (!TryGetEdgeIndex(from, to, out var edgeIndex))
            {
                return false;
            }

            _edgeOpen[edgeIndex] = open;
            return true;
        }

        public bool IsEdgeTraversable(RegionId from, RegionId to)
        {
            if (!IsRegionEnabled(from) || !IsRegionEnabled(to))
            {
                return false;
            }

            return TryGetEdgeIndex(from, to, out var edgeIndex) && _edgeOpen[edgeIndex];
        }

        public bool TryGetRouteHopCost(RegionId from, RegionId to, out int hopCost)
        {
            hopCost = -1;
            if (!IsRegionEnabled(from) || !IsRegionEnabled(to))
            {
                return false;
            }

            if (from == to)
            {
                hopCost = 0;
                return true;
            }

            var distances = new int[_regions.Length];
            for (var i = 0; i < distances.Length; i++)
            {
                distances[i] = -1;
            }

            if (!TryGetRegionIndex(from, out var startIndex) || !TryGetRegionIndex(to, out var targetIndex))
            {
                return false;
            }

            var queue = new Queue<int>();
            distances[startIndex] = 0;
            queue.Enqueue(startIndex);
            while (queue.Count > 0)
            {
                var regionIndex = queue.Dequeue();
                var region = _regions[regionIndex];
                for (var i = 0; i < region.Edges.Count; i++)
                {
                    var edge = region.Edges[i];
                    if (!IsEdgeTraversable(region.Id, edge.ToRegionId)
                        || !TryGetRegionIndex(edge.ToRegionId, out var neighborIndex)
                        || distances[neighborIndex] >= 0)
                    {
                        continue;
                    }

                    distances[neighborIndex] = distances[regionIndex] + 1;
                    if (neighborIndex == targetIndex)
                    {
                        hopCost = distances[neighborIndex];
                        return true;
                    }

                    queue.Enqueue(neighborIndex);
                }
            }

            return false;
        }

        public bool TryGetNextRegionOnRoute(RegionId from, RegionId to, out RegionId nextRegionId)
        {
            nextRegionId = RegionId.Invalid;
            if (from == to)
            {
                nextRegionId = to;
                return IsRegionEnabled(from);
            }

            if (!TryGetRegionIndex(from, out var fromIndex) || !TryGetRegionIndex(to, out _))
            {
                return false;
            }

            var bestHop = RegionId.Invalid;
            var bestCost = int.MaxValue;
            var edges = _regions[fromIndex].Edges;
            for (var i = 0; i < edges.Count; i++)
            {
                var candidate = edges[i].ToRegionId;
                if (!IsEdgeTraversable(from, candidate)
                    || !TryGetRouteHopCost(candidate, to, out var cost))
                {
                    continue;
                }

                if (cost < bestCost || (cost == bestCost && candidate.CompareTo(bestHop) < 0))
                {
                    bestHop = candidate;
                    bestCost = cost;
                }
            }

            nextRegionId = bestHop;
            return nextRegionId.IsValid;
        }

        private bool TryGetRegionIndex(RegionId regionId, out int index)
        {
            return _regionIndexById.TryGetValue(regionId, out index);
        }

        private bool TryGetEdgeIndex(RegionId from, RegionId to, out int edgeIndex)
        {
            edgeIndex = 0;
            if (!TryGetRegionIndex(from, out var fromIndex))
            {
                edgeIndex = -1;
                return false;
            }

            for (var regionIndex = 0; regionIndex < fromIndex; regionIndex++)
            {
                edgeIndex += _regions[regionIndex].Edges.Count;
            }

            var edges = _regions[fromIndex].Edges;
            for (var i = 0; i < edges.Count; i++)
            {
                if (edges[i].ToRegionId == to)
                {
                    edgeIndex += i;
                    return true;
                }
            }

            edgeIndex = -1;
            return false;
        }

        public static RegionGraphValidationFailure Validate(RegionGraph graph, NavMeshSpatialGraph spatialGraph)
        {
            if (graph == null)
            {
                return RegionGraphValidationFailure.MissingGraph;
            }

            if (spatialGraph == null
                || !spatialGraph.CompatibilityIdentity.IsValid
                || graph.CompatibilityIdentity != spatialGraph.CompatibilityIdentity)
            {
                return RegionGraphValidationFailure.SpatialGraphCompatibilityMismatch;
            }

            if (graph._nodeToRegionMap.Length != spatialGraph.NodeCount)
            {
                return RegionGraphValidationFailure.NodeToRegionMapSizeMismatch;
            }

            var seen = new HashSet<RegionId>();
            for (var i = 0; i < graph._regions.Length; i++)
            {
                var regionId = graph._regions[i].Id;
                if (!regionId.IsValid)
                {
                    return RegionGraphValidationFailure.InvalidRegionId;
                }

                if (!seen.Add(regionId))
                {
                    return RegionGraphValidationFailure.DuplicateRegionId;
                }
            }

            for (var nodeId = 0; nodeId < graph._nodeToRegionMap.Length; nodeId++)
            {
                var mappedRegionId = graph._nodeToRegionMap[nodeId];
                if (!mappedRegionId.IsValid || !seen.Contains(mappedRegionId))
                {
                    return RegionGraphValidationFailure.InvalidDanglingNodeMapping;
                }
            }

            for (var i = 0; i < graph._regions.Length; i++)
            {
                var edges = graph._regions[i].Edges;
                for (var edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
                {
                    var edge = edges[edgeIndex];
                    if (!edge.ToRegionId.IsValid)
                    {
                        return RegionGraphValidationFailure.InvalidRegionEdge;
                    }

                    if (!seen.Contains(edge.ToRegionId))
                    {
                        return RegionGraphValidationFailure.RegionEdgeMissingRegion;
                    }
                }
            }

            return RegionGraphValidationFailure.None;
        }
    }

    public sealed class RegionNode
    {
        private readonly RegionEdge[] _edges;
        private readonly IReadOnlyList<RegionEdge> _readOnlyEdges;

        public RegionNode(RegionId id, IReadOnlyCollection<RegionEdge> edges)
        {
            Id = id;
            _edges = new RegionEdge[edges?.Count ?? 0];
            if (edges != null)
            {
                var index = 0;
                foreach (var edge in edges)
                {
                    _edges[index] = edge;
                    index++;
                }
            }

            Array.Sort(_edges, (left, right) => left.ToRegionId.CompareTo(right.ToRegionId));
            _readOnlyEdges = Array.AsReadOnly(_edges);
        }

        public RegionId Id { get; }
        public IReadOnlyList<RegionEdge> Edges => _readOnlyEdges;
    }

    public readonly struct RegionEdge
    {
        public RegionEdge(RegionId toRegionId, DoorId doorId)
        {
            ToRegionId = toRegionId;
            DoorId = doorId;
        }

        public RegionId ToRegionId { get; }
        public DoorId DoorId { get; }
    }
}
