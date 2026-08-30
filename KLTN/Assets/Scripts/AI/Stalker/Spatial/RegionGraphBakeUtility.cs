using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common.Spatial;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Spatial
{
    public enum RegionGraphBakeFailure
    {
        None,
        MissingSpatialGraph,
        InvalidRegionId,
        DuplicateRegionId,
        SpatialNodeMatchedByZeroRegions,
        SpatialNodeMatchedByMultipleRegions,
        InvalidRegionEdge,
        RegionEdgeMissingRegion,
        MalformedDoorId,
        NodeToRegionMapSizeMismatch,
        InvalidDanglingNodeMapping,
        SpatialGraphCompatibilityMismatch
    }

    public readonly struct RegionGraphBakeDiagnostic
    {
        public RegionGraphBakeDiagnostic(RegionGraphBakeFailure failure, int spatialNodeId, RegionId regionId, RegionId otherRegionId)
        {
            Failure = failure;
            SpatialNodeId = spatialNodeId;
            RegionId = regionId;
            OtherRegionId = otherRegionId;
        }

        public RegionGraphBakeFailure Failure { get; }
        public int SpatialNodeId { get; }
        public RegionId RegionId { get; }
        public RegionId OtherRegionId { get; }
        public bool IsSuccess => Failure == RegionGraphBakeFailure.None;
        public static RegionGraphBakeDiagnostic Success => new RegionGraphBakeDiagnostic(RegionGraphBakeFailure.None, -1, RegionId.Invalid, RegionId.Invalid);
    }

    public readonly struct RegionGraphBakeResult
    {
        public RegionGraphBakeResult(RegionGraph graph, RegionGraphBakeDiagnostic diagnostic)
        {
            Graph = graph;
            Diagnostic = diagnostic;
        }

        public RegionGraph Graph { get; }
        public RegionGraphBakeDiagnostic Diagnostic { get; }
        public bool Succeeded => Diagnostic.IsSuccess && Graph != null;
    }

    public readonly struct RegionDefinitionBakeData
    {
        public RegionDefinitionBakeData(RegionId regionId, Bounds worldBounds)
        {
            RegionId = regionId;
            WorldBounds = worldBounds;
        }

        public RegionId RegionId { get; }
        public Bounds WorldBounds { get; }
        public bool Contains(Vector3 worldPosition) => WorldBounds.Contains(worldPosition);
    }

    public readonly struct RegionEdgeBakeData
    {
        private readonly int _doorIdValue;

        public RegionEdgeBakeData(RegionId fromRegionId, RegionId toRegionId, DoorId doorId)
        {
            FromRegionId = fromRegionId;
            ToRegionId = toRegionId;
            _doorIdValue = doorId.IsValid ? doorId.Value : 0;
        }

        public RegionEdgeBakeData(RegionId fromRegionId, RegionId toRegionId, int rawDoorIdValue)
        {
            FromRegionId = fromRegionId;
            ToRegionId = toRegionId;
            _doorIdValue = rawDoorIdValue;
        }

        public RegionId FromRegionId { get; }
        public RegionId ToRegionId { get; }
        public DoorId DoorId => _doorIdValue > 0 ? new DoorId(_doorIdValue) : DoorId.Invalid;
        public bool IsDoorIdMalformed => _doorIdValue < 0;
    }

    public static class RegionGraphBakeUtility
    {
        public static RegionGraphBakeResult Bake(
            NavMeshSpatialGraph spatialGraph,
            IReadOnlyList<RegionDefinitionBakeData> definitions,
            IReadOnlyList<RegionEdgeBakeData> edges,
            int definitionVersion)
        {
            if (spatialGraph == null || spatialGraph.IsEmpty)
            {
                return Fail(RegionGraphBakeFailure.MissingSpatialGraph);
            }

            var regionIds = new HashSet<RegionId>();
            for (var i = 0; i < (definitions?.Count ?? 0); i++)
            {
                var regionId = definitions[i].RegionId;
                if (!regionId.IsValid)
                {
                    return Fail(RegionGraphBakeFailure.InvalidRegionId, -1, regionId);
                }

                if (!regionIds.Add(regionId))
                {
                    return Fail(RegionGraphBakeFailure.DuplicateRegionId, -1, regionId);
                }
            }

            var nodeToRegionMap = new RegionId[spatialGraph.NodeCount];
            for (var nodeIndex = 0; nodeIndex < spatialGraph.Nodes.Count; nodeIndex++)
            {
                var node = spatialGraph.Nodes[nodeIndex];
                var matchedRegionId = RegionId.Invalid;
                var matchCount = 0;
                for (var regionIndex = 0; regionIndex < (definitions?.Count ?? 0); regionIndex++)
                {
                    var definition = definitions[regionIndex];
                    if (!definition.Contains(node.Position))
                    {
                        continue;
                    }

                    if (matchCount == 0)
                    {
                        matchedRegionId = definition.RegionId;
                    }

                    matchCount++;
                }

                if (matchCount == 0)
                {
                    return Fail(RegionGraphBakeFailure.SpatialNodeMatchedByZeroRegions, node.Id);
                }

                if (matchCount > 1)
                {
                    return Fail(RegionGraphBakeFailure.SpatialNodeMatchedByMultipleRegions, node.Id, matchedRegionId);
                }

                nodeToRegionMap[node.Id] = matchedRegionId;
            }

            var edgeBuckets = new Dictionary<RegionId, List<RegionEdge>>();
            foreach (var regionId in regionIds)
            {
                edgeBuckets.Add(regionId, new List<RegionEdge>());
            }

            for (var i = 0; i < (edges?.Count ?? 0); i++)
            {
                var edge = edges[i];
                if (!edge.FromRegionId.IsValid || !edge.ToRegionId.IsValid || edge.FromRegionId == edge.ToRegionId)
                {
                    return Fail(RegionGraphBakeFailure.InvalidRegionEdge, -1, edge.FromRegionId, edge.ToRegionId);
                }

                if (!regionIds.Contains(edge.FromRegionId) || !regionIds.Contains(edge.ToRegionId))
                {
                    return Fail(RegionGraphBakeFailure.RegionEdgeMissingRegion, -1, edge.FromRegionId, edge.ToRegionId);
                }

                if (edge.IsDoorIdMalformed)
                {
                    return Fail(RegionGraphBakeFailure.MalformedDoorId, -1, edge.FromRegionId, edge.ToRegionId);
                }

                edgeBuckets[edge.FromRegionId].Add(new RegionEdge(edge.ToRegionId, edge.DoorId));
            }

            var regionNodes = new List<RegionNode>(regionIds.Count);
            var sortedRegionIds = new List<RegionId>(regionIds);
            sortedRegionIds.Sort();
            for (var i = 0; i < sortedRegionIds.Count; i++)
            {
                var regionId = sortedRegionIds[i];
                regionNodes.Add(new RegionNode(regionId, edgeBuckets[regionId]));
            }

            var graph = new RegionGraph(
                regionNodes,
                nodeToRegionMap,
                spatialGraph.CompatibilityIdentity,
                definitionVersion);
            var validation = RegionGraph.Validate(graph, spatialGraph);
            if (validation != RegionGraphValidationFailure.None)
            {
                return Fail(ToBakeFailure(validation));
            }

            return new RegionGraphBakeResult(graph, RegionGraphBakeDiagnostic.Success);
        }

        public static RegionGraphBakeDiagnostic ValidateRuntimeGraph(RegionGraph graph, NavMeshSpatialGraph spatialGraph)
        {
            return ToBakeDiagnostic(RegionGraph.Validate(graph, spatialGraph));
        }

        private static RegionGraphBakeResult Fail(
            RegionGraphBakeFailure failure,
            int spatialNodeId = -1,
            RegionId regionId = default,
            RegionId otherRegionId = default)
        {
            return new RegionGraphBakeResult(
                null,
                new RegionGraphBakeDiagnostic(failure, spatialNodeId, regionId, otherRegionId));
        }

        private static RegionGraphBakeDiagnostic ToBakeDiagnostic(RegionGraphValidationFailure failure)
        {
            return new RegionGraphBakeDiagnostic(ToBakeFailure(failure), -1, RegionId.Invalid, RegionId.Invalid);
        }

        private static RegionGraphBakeFailure ToBakeFailure(RegionGraphValidationFailure failure)
        {
            switch (failure)
            {
                case RegionGraphValidationFailure.None:
                    return RegionGraphBakeFailure.None;
                case RegionGraphValidationFailure.InvalidRegionId:
                    return RegionGraphBakeFailure.InvalidRegionId;
                case RegionGraphValidationFailure.DuplicateRegionId:
                    return RegionGraphBakeFailure.DuplicateRegionId;
                case RegionGraphValidationFailure.InvalidRegionEdge:
                    return RegionGraphBakeFailure.InvalidRegionEdge;
                case RegionGraphValidationFailure.RegionEdgeMissingRegion:
                    return RegionGraphBakeFailure.RegionEdgeMissingRegion;
                case RegionGraphValidationFailure.DanglingDoorId:
                    return RegionGraphBakeFailure.MalformedDoorId;
                case RegionGraphValidationFailure.NodeToRegionMapSizeMismatch:
                    return RegionGraphBakeFailure.NodeToRegionMapSizeMismatch;
                case RegionGraphValidationFailure.InvalidDanglingNodeMapping:
                    return RegionGraphBakeFailure.InvalidDanglingNodeMapping;
                case RegionGraphValidationFailure.SpatialGraphCompatibilityMismatch:
                    return RegionGraphBakeFailure.SpatialGraphCompatibilityMismatch;
                default:
                    return RegionGraphBakeFailure.InvalidDanglingNodeMapping;
            }
        }
    }
}
