using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common.Spatial;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Spatial
{
    public delegate bool PatrolPathValidator(Vector3 destination);

    public sealed class LocalPatrolSelector
    {
        private const float ScoreEpsilon = 0.000001f;
        private readonly NavMeshSpatialGraph _spatialGraph;
        private readonly RegionGraph _regionGraph;
        private readonly CoverageMemory _coverageMemory;
        private readonly int _maxDepth;
        private readonly PatrolPathValidator _pathValidator;

        public LocalPatrolSelector(
            NavMeshSpatialGraph spatialGraph,
            RegionGraph regionGraph,
            CoverageMemory coverageMemory,
            int maxDepth,
            PatrolPathValidator pathValidator)
        {
            _spatialGraph = spatialGraph;
            _regionGraph = regionGraph;
            _coverageMemory = coverageMemory;
            _maxDepth = Mathf.Max(1, maxDepth);
            _pathValidator = pathValidator;
        }

        public bool TrySelect(
            int currentNodeId,
            int previousNodeId,
            RegionId desiredRegionId,
            out LocalPatrolSelection selection)
        {
            return TrySelect(currentNodeId, previousNodeId, desiredRegionId, null, out selection);
        }

        public bool TrySelect(
            int currentNodeId,
            int previousNodeId,
            RegionId desiredRegionId,
            ISet<int> rejectedNodeIds,
            out LocalPatrolSelection selection)
        {
            return TrySelect(currentNodeId, previousNodeId, desiredRegionId,
                rejectedNodeIds, null, out selection, out _);
        }

        public bool TrySelect(
            int currentNodeId,
            int previousNodeId,
            RegionId desiredRegionId,
            ISet<int> rejectedNodeIds,
            Func<bool> trySpendPathCheck,
            out LocalPatrolSelection selection,
            out bool deferred)
        {
            selection = default;
            deferred = false;
            if (_spatialGraph == null
                || _regionGraph == null
                || _coverageMemory == null
                || !_spatialGraph.TryGetNode(currentNodeId, out _)
                || !desiredRegionId.IsValid)
            {
                return false;
            }

            var candidates = GetCandidates(currentNodeId, desiredRegionId);
            // Score first so lower-ranked nodes never need a native path query.
            var bestNodeId = -1;
            var bestScore = float.NegativeInfinity;
            var rankedCandidates = new List<SpatialNode>();
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidateNodeId = candidates[i];
                if (rejectedNodeIds != null && rejectedNodeIds.Contains(candidateNodeId))
                {
                    continue;
                }

                if (!_spatialGraph.TryGetNode(candidateNodeId, out var node))
                {
                    continue;
                }

                rankedCandidates.Add(node);
            }

            rankedCandidates.Sort((left, right) =>
            {
                var difference = Score(right, previousNodeId) - Score(left, previousNodeId);
                return Mathf.Abs(difference) <= ScoreEpsilon
                    ? left.Id.CompareTo(right.Id) : difference > 0f ? 1 : -1;
            });
            for (var i = 0; i < rankedCandidates.Count; i++)
            {
                var node = rankedCandidates[i];
                if (_pathValidator != null)
                {
                    if (trySpendPathCheck != null && !trySpendPathCheck())
                    {
                        deferred = true;
                        return false;
                    }
                    if (!_pathValidator(node.Position))
                    {
                        if (trySpendPathCheck != null) rejectedNodeIds?.Add(node.Id);
                        continue;
                    }
                }
                bestNodeId = node.Id;
                bestScore = Score(node, previousNodeId);
                break;
            }

            if (bestNodeId < 0 || !_spatialGraph.TryGetNode(bestNodeId, out var bestNode))
            {
                return false;
            }

            selection = new LocalPatrolSelection(bestNode, desiredRegionId, bestScore, candidates.Count);
            return true;
        }

        private List<int> GetCandidates(int currentNodeId, RegionId desiredRegionId)
        {
            var candidates = new List<int>();
            var visited = new bool[_spatialGraph.NodeCount];
            var queue = new Queue<NodeDepth>();
            visited[currentNodeId] = true;
            queue.Enqueue(new NodeDepth(currentNodeId, 0));
            while (queue.Count > 0)
            {
                var item = queue.Dequeue();
                if (item.Depth >= _maxDepth || !_spatialGraph.TryGetNode(item.NodeId, out var node))
                {
                    continue;
                }

                for (var i = 0; i < node.NeighborIds.Count; i++)
                {
                    var neighborId = node.NeighborIds[i];
                    if (neighborId < 0 || neighborId >= visited.Length || visited[neighborId])
                    {
                        continue;
                    }

                    visited[neighborId] = true;
                    if (_regionGraph.TryGetRegionForNode(neighborId, out var regionId) && regionId == desiredRegionId)
                    {
                        candidates.Add(neighborId);
                    }

                    queue.Enqueue(new NodeDepth(neighborId, item.Depth + 1));
                }
            }

            return candidates;
        }

        private float Score(SpatialNode node, int previousNodeId)
        {
            var neverVisited = _coverageMemory.WasNodeVisited(node.Id) ? 0f : 10f;
            var visitPenalty = _coverageMemory.GetNodeVisitCount(node.Id);
            var backtrackPenalty = node.Id == previousNodeId ? 2f : 0f;
            var recentPenalty = _coverageMemory.GetRecentNodeFrequency(node.Id);
            var connectivity = node.NeighborIds.Count * 0.1f;
            return neverVisited + connectivity - visitPenalty - backtrackPenalty - recentPenalty;
        }

        private readonly struct NodeDepth
        {
            public NodeDepth(int nodeId, int depth)
            {
                NodeId = nodeId;
                Depth = depth;
            }

            public int NodeId { get; }
            public int Depth { get; }
        }
    }

    public readonly struct LocalPatrolSelection
    {
        public LocalPatrolSelection(SpatialNode destinationNode, RegionId selectedRegionId, float score, int candidateCount)
        {
            DestinationNode = destinationNode;
            SelectedRegionId = selectedRegionId;
            Score = score;
            CandidateCount = candidateCount;
        }

        public SpatialNode DestinationNode { get; }
        public RegionId SelectedRegionId { get; }
        public float Score { get; }
        public int CandidateCount { get; }
    }
}
