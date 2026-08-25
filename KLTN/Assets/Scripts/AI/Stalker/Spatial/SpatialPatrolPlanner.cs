using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace EchoProtocol.AI.Stalker.Spatial
{
    public sealed class SpatialPatrolPlanner
    {
        private const float SamplePositionRadius = 1f;
        private const float ScoreEpsilon = 0.000001f;

        private readonly NavMeshSpatialGraph _graph;
        private readonly SpatialPatrolMemory _memory;
        private readonly int _candidateBfsDepth;
        private readonly float _stalenessHorizon;
        private readonly float _stalenessWeight;
        private readonly float _connectivityWeight;
        private readonly float _immediateBacktrackPenalty;
        private readonly int _maxDegree;

        public SpatialPatrolPlanner(
            NavMeshSpatialGraph graph,
            SpatialPatrolMemory memory,
            int candidateBfsDepth,
            float stalenessHorizon,
            float stalenessWeight,
            float connectivityWeight,
            float immediateBacktrackPenalty)
        {
            _graph = graph;
            _memory = memory;
            _candidateBfsDepth = Mathf.Max(1, candidateBfsDepth);
            _stalenessHorizon = Mathf.Max(0.0001f, stalenessHorizon);
            _stalenessWeight = stalenessWeight;
            _connectivityWeight = connectivityWeight;
            _immediateBacktrackPenalty = immediateBacktrackPenalty;
            _maxDegree = CalculateMaxDegree(graph);
        }

        public bool CanPlan => _graph != null && !_graph.IsEmpty && _memory != null;

        public bool TryResolveNearestNode(Vector3 worldPosition, out int nodeId)
        {
            nodeId = -1;

            if (!CanPlan)
            {
                return false;
            }

            var queryPosition = worldPosition;
            if (NavMesh.SamplePosition(worldPosition, out var hit, SamplePositionRadius, NavMesh.AllAreas))
            {
                queryPosition = hit.position;
            }

            var bestSqrDistance = float.PositiveInfinity;
            var nodes = _graph.Nodes;
            for (var i = 0; i < nodes.Count; i++)
            {
                var sqrDistance = (nodes[i].Position - queryPosition).sqrMagnitude;
                if (sqrDistance >= bestSqrDistance)
                {
                    continue;
                }

                bestSqrDistance = sqrDistance;
                nodeId = nodes[i].Id;
            }

            return nodeId >= 0;
        }

        public bool TrySelectDestination(
            int currentNodeId,
            int previousNodeId,
            float currentTime,
            out SpatialPatrolPlan plan)
        {
            plan = default;

            if (!CanPlan || !_graph.TryGetNode(currentNodeId, out _))
            {
                return false;
            }

            var candidates = GetCandidateNodeIds(currentNodeId);
            if (candidates.Count == 0)
            {
                AddFallbackCandidates(currentNodeId, candidates);
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            var bestNodeId = -1;
            var bestScore = float.NegativeInfinity;

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidateNodeId = candidates[i];
                if (!_graph.TryGetNode(candidateNodeId, out var candidateNode))
                {
                    continue;
                }

                var score = CalculateScore(candidateNode, previousNodeId, currentTime);
                if (IsBetterCandidate(candidateNodeId, score, bestNodeId, bestScore))
                {
                    bestNodeId = candidateNodeId;
                    bestScore = score;
                }
            }

            if (bestNodeId < 0 || !_graph.TryGetNode(bestNodeId, out var bestNode))
            {
                return false;
            }

            plan = new SpatialPatrolPlan(bestNode, bestScore, candidates.Count);
            return true;
        }

        private List<int> GetCandidateNodeIds(int currentNodeId)
        {
            var candidates = new List<int>();
            var visited = new bool[_graph.NodeCount];
            var queue = new Queue<NodeDepth>();

            visited[currentNodeId] = true;
            queue.Enqueue(new NodeDepth(currentNodeId, 0));

            while (queue.Count > 0)
            {
                var item = queue.Dequeue();
                if (item.Depth >= _candidateBfsDepth || !_graph.TryGetNode(item.NodeId, out var node))
                {
                    continue;
                }

                var neighbors = node.NeighborIds;
                for (var i = 0; i < neighbors.Count; i++)
                {
                    var neighborId = neighbors[i];
                    if (neighborId < 0 || neighborId >= visited.Length || visited[neighborId])
                    {
                        continue;
                    }

                    visited[neighborId] = true;
                    candidates.Add(neighborId);
                    queue.Enqueue(new NodeDepth(neighborId, item.Depth + 1));
                }
            }

            return candidates;
        }

        private void AddFallbackCandidates(int currentNodeId, List<int> candidates)
        {
            var nodes = _graph.Nodes;
            for (var i = 0; i < nodes.Count; i++)
            {
                var nodeId = nodes[i].Id;
                if (nodeId == currentNodeId)
                {
                    continue;
                }

                candidates.Add(nodeId);
            }
        }

        private float CalculateScore(SpatialNode node, int previousNodeId, float currentTime)
        {
            var staleness = _memory.GetNormalizedStaleness(node.Id, currentTime, _stalenessHorizon);
            var connectivity = _maxDegree > 0 ? (float)node.NeighborIds.Count / _maxDegree : 0f;
            var backtrack = node.Id == previousNodeId ? 1f : 0f;

            return (_stalenessWeight * staleness)
                + (_connectivityWeight * connectivity)
                - (_immediateBacktrackPenalty * backtrack);
        }

        private static bool IsBetterCandidate(int candidateNodeId, float score, int bestNodeId, float bestScore)
        {
            if (bestNodeId < 0)
            {
                return true;
            }

            if (score > bestScore + ScoreEpsilon)
            {
                return true;
            }

            return Mathf.Abs(score - bestScore) <= ScoreEpsilon && candidateNodeId < bestNodeId;
        }

        private static int CalculateMaxDegree(NavMeshSpatialGraph graph)
        {
            if (graph == null || graph.IsEmpty)
            {
                return 0;
            }

            var maxDegree = 0;
            var nodes = graph.Nodes;
            for (var i = 0; i < nodes.Count; i++)
            {
                maxDegree = Mathf.Max(maxDegree, nodes[i].NeighborIds.Count);
            }

            return maxDegree;
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

    public readonly struct SpatialPatrolPlan
    {
        public SpatialPatrolPlan(SpatialNode destinationNode, float score, int candidateCount)
        {
            DestinationNode = destinationNode;
            Score = score;
            CandidateCount = candidateCount;
        }

        public SpatialNode DestinationNode { get; }
        public float Score { get; }
        public int CandidateCount { get; }
    }
}
