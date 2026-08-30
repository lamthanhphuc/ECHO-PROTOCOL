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
            selection = default;
            if (_spatialGraph == null
                || _regionGraph == null
                || _coverageMemory == null
                || !_spatialGraph.TryGetNode(currentNodeId, out _)
                || !desiredRegionId.IsValid)
            {
                return false;
            }

            var candidates = GetCandidates(currentNodeId, desiredRegionId);
            var bestNodeId = -1;
            var bestScore = float.NegativeInfinity;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidateNodeId = candidates[i];
                if (!_spatialGraph.TryGetNode(candidateNodeId, out var node))
                {
                    continue;
                }

                if (_pathValidator != null && !_pathValidator(node.Position))
                {
                    continue;
                }

                var score = Score(node, previousNodeId);
                if (bestNodeId < 0
                    || score > bestScore + ScoreEpsilon
                    || (Mathf.Abs(score - bestScore) <= ScoreEpsilon && candidateNodeId < bestNodeId))
                {
                    bestNodeId = candidateNodeId;
                    bestScore = score;
                }
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
