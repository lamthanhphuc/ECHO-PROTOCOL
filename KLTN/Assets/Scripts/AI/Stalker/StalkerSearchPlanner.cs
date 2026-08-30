using EchoProtocol.AI.Stalker.Spatial;
using UnityEngine;

namespace EchoProtocol.AI.Stalker
{
    public enum SearchCandidateRejectReason
    {
        None,
        OutsideSearchRadius,
        AlreadyVisited,
        Duplicate,
        InvalidNode,
        DestinationInvalid,
        PathPartial,
        PathInvalid,
        Disconnected,
        DoorBlocked,
        RegionInvalid,
        SameAsCurrentDestination
    }

    public delegate NavigationEvaluationStatus SearchPathEvaluator(Vector3 destination);

    public sealed class StalkerSearchPlanner
    {
        private const float ScoreEpsilon = 0.000001f;
        private readonly NavMeshSpatialGraph _spatialGraph;
        private readonly RegionGraph _regionGraph;
        private readonly CoverageMemory _coverageMemory;
        private readonly SearchPathEvaluator _pathEvaluator;

        public StalkerSearchPlanner(
            NavMeshSpatialGraph spatialGraph,
            RegionGraph regionGraph,
            CoverageMemory coverageMemory,
            SearchPathEvaluator pathEvaluator)
        {
            _spatialGraph = spatialGraph;
            _regionGraph = regionGraph;
            _coverageMemory = coverageMemory;
            _pathEvaluator = pathEvaluator;
        }

        public SearchCandidateRejectReason LastRejectReason { get; private set; }

        public bool TrySelectCandidate(
            StalkerSearchContext context,
            float searchRadius,
            int currentNodeId,
            int previousNodeId,
            out SearchCandidateSelection selection)
        {
            selection = default;
            LastRejectReason = SearchCandidateRejectReason.None;

            if (context == null || _spatialGraph == null || _spatialGraph.IsEmpty)
            {
                LastRejectReason = SearchCandidateRejectReason.Disconnected;
                return false;
            }

            var radiusSqr = Mathf.Max(0f, searchRadius) * Mathf.Max(0f, searchRadius);
            var bestNodeId = -1;
            var bestScore = float.NegativeInfinity;
            var bestVisitCount = int.MaxValue;

            var nodes = _spatialGraph.Nodes;
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                var rejectReason = ClassifyCandidate(node, context, radiusSqr, currentNodeId);
                if (rejectReason != SearchCandidateRejectReason.None)
                {
                    LastRejectReason = rejectReason;
                    continue;
                }

                var pathStatus = _pathEvaluator != null ? _pathEvaluator(node.Position) : NavigationEvaluationStatus.Complete;
                if (pathStatus != NavigationEvaluationStatus.Complete)
                {
                    LastRejectReason = ToRejectReason(pathStatus);
                    continue;
                }

                var score = Score(node, context, previousNodeId);
                var visitCount = _coverageMemory?.GetNodeVisitCount(node.Id) ?? 0;
                if (IsBetter(node.Id, score, visitCount, bestNodeId, bestScore, bestVisitCount))
                {
                    bestNodeId = node.Id;
                    bestScore = score;
                    bestVisitCount = visitCount;
                }
            }

            if (bestNodeId < 0 || !_spatialGraph.TryGetNode(bestNodeId, out var bestNode))
            {
                return false;
            }

            selection = new SearchCandidateSelection(bestNode, bestScore);
            return true;
        }

        private SearchCandidateRejectReason ClassifyCandidate(
            SpatialNode node,
            StalkerSearchContext context,
            float radiusSqr,
            int currentNodeId)
        {
            if (node == null || node.Id < 0)
            {
                return SearchCandidateRejectReason.InvalidNode;
            }

            if (node.Id == currentNodeId || context.CurrentCandidateNodeId == node.Id)
            {
                return SearchCandidateRejectReason.SameAsCurrentDestination;
            }

            if ((node.Position - context.SearchOriginLKP).sqrMagnitude > radiusSqr)
            {
                return SearchCandidateRejectReason.OutsideSearchRadius;
            }

            if (context.HasVisitedSearchNode(node.Id))
            {
                return SearchCandidateRejectReason.AlreadyVisited;
            }

            if (context.HasAttemptedCandidate(node.Id))
            {
                return SearchCandidateRejectReason.Duplicate;
            }

            if (_regionGraph != null && !_regionGraph.TryGetRegionForNode(node.Id, out _))
            {
                return SearchCandidateRejectReason.RegionInvalid;
            }

            return SearchCandidateRejectReason.None;
        }

        private float Score(SpatialNode node, StalkerSearchContext context, int previousNodeId)
        {
            var directionToNode = node.Position - context.SearchOriginLKP;
            var alignment = directionToNode.sqrMagnitude > 0.0001f
                ? Mathf.Max(0f, Vector3.Dot(context.SearchOriginDirection, directionToNode.normalized))
                : 0f;
            var novelty = _coverageMemory != null && !_coverageMemory.WasNodeVisited(node.Id) ? 2f : 0f;
            var connectivity = node.NeighborIds.Count * 0.1f;
            var travelPenalty = directionToNode.magnitude * 0.01f;
            var backtrackPenalty = node.Id == previousNodeId ? 1f : 0f;
            var recentPenalty = _coverageMemory?.GetRecentNodeFrequency(node.Id) ?? 0;
            return alignment + novelty + connectivity - travelPenalty - backtrackPenalty - recentPenalty;
        }

        private static bool IsBetter(int nodeId, float score, int visitCount, int bestNodeId, float bestScore, int bestVisitCount)
        {
            if (bestNodeId < 0)
            {
                return true;
            }

            if (score > bestScore + ScoreEpsilon)
            {
                return true;
            }

            if (score < bestScore - ScoreEpsilon)
            {
                return false;
            }

            if (visitCount != bestVisitCount)
            {
                return visitCount < bestVisitCount;
            }

            return nodeId < bestNodeId;
        }

        private static SearchCandidateRejectReason ToRejectReason(NavigationEvaluationStatus status)
        {
            switch (status)
            {
                case NavigationEvaluationStatus.DestinationInvalid:
                    return SearchCandidateRejectReason.DestinationInvalid;
                case NavigationEvaluationStatus.Partial:
                    return SearchCandidateRejectReason.PathPartial;
                case NavigationEvaluationStatus.Invalid:
                case NavigationEvaluationStatus.AgentUnavailable:
                case NavigationEvaluationStatus.AgentNotOnNavMesh:
                    return SearchCandidateRejectReason.PathInvalid;
                default:
                    return SearchCandidateRejectReason.None;
            }
        }
    }

    public readonly struct SearchCandidateSelection
    {
        public SearchCandidateSelection(SpatialNode destinationNode, float score)
        {
            DestinationNode = destinationNode;
            Score = score;
        }

        public SpatialNode DestinationNode { get; }
        public float Score { get; }
    }
}
