using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Spatial
{
    public enum ConfidencePatrolPlanningMode
    {
        LocalCoverage,
        GlobalCoverage
    }

    public sealed class ConfidenceSpatialPatrolPlanner
    {
        private const float ScoreEpsilon = 0.000001f;

        private readonly NavMeshSpatialGraph _graph;
        private readonly SpatialCoverageMemory _memory;
        private readonly int _localBfsDepth;
        private readonly float _confidenceHalfLife;
        private readonly float _coverageWeight;
        private readonly float _connectivityWeight;
        private readonly float _recentHistoryWeight;
        private readonly float _immediateBacktrackWeight;
        private readonly int _stagnationPlanningSteps;
        private readonly float _visitDepth0Injection;
        private readonly float _visitDepth1Injection;
        private readonly float _visitDepth2Injection;
        private readonly int _maxDegree;

        public ConfidenceSpatialPatrolPlanner(
            NavMeshSpatialGraph graph,
            SpatialCoverageMemory memory,
            int localBfsDepth,
            float confidenceHalfLife,
            float coverageWeight,
            float connectivityWeight,
            float recentHistoryWeight,
            float immediateBacktrackWeight,
            int stagnationPlanningSteps,
            float visitDepth0Injection,
            float visitDepth1Injection,
            float visitDepth2Injection)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            if (memory == null)
            {
                throw new ArgumentNullException(nameof(memory));
            }

            if (memory.NodeCount != graph.NodeCount)
            {
                throw new ArgumentException("Coverage memory node count must match graph node count.", nameof(memory));
            }

            _graph = graph;
            _memory = memory;
            _localBfsDepth = Mathf.Max(1, localBfsDepth);
            _confidenceHalfLife = Mathf.Max(0.0001f, confidenceHalfLife);
            _coverageWeight = Mathf.Max(0f, coverageWeight);
            _connectivityWeight = Mathf.Max(0f, connectivityWeight);
            _recentHistoryWeight = Mathf.Max(0f, recentHistoryWeight);
            _immediateBacktrackWeight = Mathf.Max(0f, immediateBacktrackWeight);
            _stagnationPlanningSteps = Mathf.Max(1, stagnationPlanningSteps);
            _visitDepth0Injection = Mathf.Clamp01(visitDepth0Injection);
            _visitDepth1Injection = Mathf.Clamp01(visitDepth1Injection);
            _visitDepth2Injection = Mathf.Clamp01(visitDepth2Injection);
            _maxDegree = CalculateMaxDegree(graph);

            Mode = ConfidencePatrolPlanningMode.LocalCoverage;
            GlobalCoverageTargetNodeId = -1;
        }

        public ConfidencePatrolPlanningMode Mode { get; private set; }
        public int GlobalCoverageTargetNodeId { get; private set; }
        public int PlanningStepCount { get; private set; }
        public int PlanningStepsSinceNewCycleNode { get; private set; }
        public int GlobalInterventionCount { get; private set; }
        public int LastCandidateCount { get; private set; }
        public float LastSelectedScore { get; private set; }
        public float LastSelectedCoverageNeed { get; private set; }

        public ConfidencePatrolVisitResult RecordExactVisit(int nodeId, float currentTime)
        {
            var visitResult = _memory.MarkExactVisited(nodeId, currentTime);
            if (!visitResult.IsValid)
            {
                return new ConfidencePatrolVisitResult(visitResult, false, Mode, GlobalCoverageTargetNodeId);
            }

            InjectVisitConfidence(nodeId, currentTime);

            if (visitResult.WasFirstCycleVisit)
            {
                PlanningStepsSinceNewCycleNode = 0;
            }

            var clearedGlobalTarget = nodeId == GlobalCoverageTargetNodeId;
            if (clearedGlobalTarget)
            {
                ClearGlobalTarget();
            }

            return new ConfidencePatrolVisitResult(visitResult, clearedGlobalTarget, Mode, GlobalCoverageTargetNodeId);
        }

        public bool TrySelectDestination(
            int currentNodeId,
            int previousNodeId,
            float currentTime,
            out ConfidencePatrolPlanResult result)
        {
            PlanningStepCount++;
            result = ConfidencePatrolPlanResult.Invalid(Mode, GlobalCoverageTargetNodeId);
            ClearLastSelectionDebug();

            if (_graph.IsEmpty || !_graph.TryGetNode(currentNodeId, out _))
            {
                return false;
            }

            if (_graph.NodeCount <= 1)
            {
                return false;
            }

            PrepareCoverageCycleForPlanning();

            if (Mode == ConfidencePatrolPlanningMode.GlobalCoverage)
            {
                if (TrySelectGlobalDestination(currentNodeId, currentTime, out result))
                {
                    ApplyLastSelectionDebug(result);
                    return true;
                }

                return false;
            }

            PlanningStepsSinceNewCycleNode++;

            if (PlanningStepsSinceNewCycleNode >= _stagnationPlanningSteps)
            {
                EnterGlobalCoverage();
                if (TrySelectGlobalDestination(currentNodeId, currentTime, out result))
                {
                    ApplyLastSelectionDebug(result);
                    return true;
                }

                return false;
            }

            if (TrySelectLocalDestination(currentNodeId, previousNodeId, currentTime, out result))
            {
                ApplyLastSelectionDebug(result);
                return true;
            }

            EnterGlobalCoverage();
            if (TrySelectGlobalDestination(currentNodeId, currentTime, out result))
            {
                ApplyLastSelectionDebug(result);
                return true;
            }

            return false;
        }

        private void PrepareCoverageCycleForPlanning()
        {
            if (!_memory.IsCoverageCycleComplete)
            {
                return;
            }

            if (_memory.BeginNextCoverageCycle())
            {
                ClearGlobalTarget();
                PlanningStepsSinceNewCycleNode = 0;
            }
        }

        private bool TrySelectLocalDestination(
            int currentNodeId,
            int previousNodeId,
            float currentTime,
            out ConfidencePatrolPlanResult result)
        {
            result = ConfidencePatrolPlanResult.Invalid(Mode, GlobalCoverageTargetNodeId);
            var candidates = GetLocalCandidateNodeIds(currentNodeId);
            if (candidates.Count == 0)
            {
                LastCandidateCount = 0;
                return false;
            }

            var bestNodeId = -1;
            var bestScore = float.NegativeInfinity;
            var bestCoverageNeed = 0f;
            var bestRecentHistoryPenalty = 0f;

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidateNodeId = candidates[i];
                if (!_graph.TryGetNode(candidateNodeId, out var candidateNode))
                {
                    continue;
                }

                var coverageNeed = _memory.GetCoverageNeed(candidateNodeId, currentTime, _confidenceHalfLife);
                var recentHistoryPenalty = _memory.GetRecentHistoryPenalty(candidateNodeId);
                var score = CalculateLocalScore(candidateNode, previousNodeId, coverageNeed, recentHistoryPenalty);

                if (IsBetterLocalCandidate(
                    candidateNodeId,
                    score,
                    coverageNeed,
                    recentHistoryPenalty,
                    bestNodeId,
                    bestScore,
                    bestCoverageNeed,
                    bestRecentHistoryPenalty))
                {
                    bestNodeId = candidateNodeId;
                    bestScore = score;
                    bestCoverageNeed = coverageNeed;
                    bestRecentHistoryPenalty = recentHistoryPenalty;
                }
            }

            if (bestNodeId < 0)
            {
                return false;
            }

            result = new ConfidencePatrolPlanResult(
                true,
                bestNodeId,
                Mode,
                GlobalCoverageTargetNodeId,
                bestScore,
                bestCoverageNeed,
                candidates.Count);

            return true;
        }

        private bool TrySelectGlobalDestination(
            int currentNodeId,
            float currentTime,
            out ConfidencePatrolPlanResult result)
        {
            result = ConfidencePatrolPlanResult.Invalid(Mode, GlobalCoverageTargetNodeId);

            if (!IsValidNodeId(GlobalCoverageTargetNodeId)
                || _memory.WasExactVisitedThisCycle(GlobalCoverageTargetNodeId)
                || !TryGetNextHopTowardTarget(currentNodeId, GlobalCoverageTargetNodeId, out var nextHopNodeId, out var pathEdgeCount))
            {
                GlobalCoverageTargetNodeId = -1;
                if (!TrySelectGlobalTarget(currentNodeId, currentTime, out var targetNodeId, out var eligibleTargetCount))
                {
                    ClearGlobalTarget();
                    LastCandidateCount = eligibleTargetCount;
                    return false;
                }

                GlobalCoverageTargetNodeId = targetNodeId;
                LastCandidateCount = eligibleTargetCount;

                if (!TryGetNextHopTowardTarget(currentNodeId, GlobalCoverageTargetNodeId, out nextHopNodeId, out pathEdgeCount))
                {
                    ClearGlobalTarget();
                    return false;
                }
            }
            else
            {
                // Persistent global target follow-up: CandidateCount is the remaining graph path edge count.
                LastCandidateCount = pathEdgeCount;
            }

            var coverageNeed = _memory.GetCoverageNeed(GlobalCoverageTargetNodeId, currentTime, _confidenceHalfLife);
            result = new ConfidencePatrolPlanResult(
                true,
                nextHopNodeId,
                Mode,
                GlobalCoverageTargetNodeId,
                0f,
                coverageNeed,
                LastCandidateCount);

            return true;
        }

        private bool TrySelectGlobalTarget(
            int currentNodeId,
            float currentTime,
            out int targetNodeId,
            out int eligibleTargetCount)
        {
            targetNodeId = -1;
            eligibleTargetCount = 0;

            if (!TryCalculateDistances(currentNodeId, out var distances))
            {
                return false;
            }

            var bestCoverageNeed = 0f;
            var bestDistance = int.MaxValue;
            var bestCyclicKey = int.MaxValue;

            for (var nodeId = 0; nodeId < _graph.NodeCount; nodeId++)
            {
                if (nodeId == currentNodeId)
                {
                    continue;
                }

                if (!IsEligibleGlobalTarget(nodeId, distances))
                {
                    continue;
                }

                eligibleTargetCount++;

                var coverageNeed = _memory.GetCoverageNeed(nodeId, currentTime, _confidenceHalfLife);
                var distance = distances[nodeId];
                var cyclicKey = GetCoverageCycleCyclicKey(nodeId);

                if (IsBetterGlobalTarget(
                    nodeId,
                    coverageNeed,
                    distance,
                    cyclicKey,
                    targetNodeId,
                    bestCoverageNeed,
                    bestDistance,
                    bestCyclicKey))
                {
                    targetNodeId = nodeId;
                    bestCoverageNeed = coverageNeed;
                    bestDistance = distance;
                    bestCyclicKey = cyclicKey;
                }
            }

            return targetNodeId >= 0;
        }

        private bool IsEligibleGlobalTarget(int nodeId, int[] distances)
        {
            return IsValidNodeId(nodeId)
                && distances[nodeId] >= 0
                && !_memory.WasExactVisitedThisCycle(nodeId);
        }

        private List<int> GetLocalCandidateNodeIds(int currentNodeId)
        {
            var candidates = new List<int>();
            var visited = new bool[_graph.NodeCount];
            var queue = new Queue<NodeDepth>();

            visited[currentNodeId] = true;
            queue.Enqueue(new NodeDepth(currentNodeId, 0));

            while (queue.Count > 0)
            {
                var item = queue.Dequeue();
                if (item.Depth >= _localBfsDepth || !_graph.TryGetNode(item.NodeId, out var node))
                {
                    continue;
                }

                var neighbors = node.NeighborIds;
                for (var i = 0; i < neighbors.Count; i++)
                {
                    var neighborId = neighbors[i];
                    if (!IsValidNodeId(neighborId) || visited[neighborId])
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

        private void InjectVisitConfidence(int nodeId, float currentTime)
        {
            var visited = new bool[_graph.NodeCount];
            var queue = new Queue<NodeDepth>();

            visited[nodeId] = true;
            queue.Enqueue(new NodeDepth(nodeId, 0));

            while (queue.Count > 0)
            {
                var item = queue.Dequeue();
                var injection = GetVisitInjectionForDepth(item.Depth);
                if (injection > 0f)
                {
                    _memory.InjectConfidence(item.NodeId, injection, currentTime, _confidenceHalfLife);
                }

                if (item.Depth >= 2 || !_graph.TryGetNode(item.NodeId, out var node))
                {
                    continue;
                }

                var neighbors = node.NeighborIds;
                for (var i = 0; i < neighbors.Count; i++)
                {
                    var neighborId = neighbors[i];
                    if (!IsValidNodeId(neighborId) || visited[neighborId])
                    {
                        continue;
                    }

                    visited[neighborId] = true;
                    queue.Enqueue(new NodeDepth(neighborId, item.Depth + 1));
                }
            }
        }

        private bool TryGetNextHopTowardTarget(int currentNodeId, int targetNodeId, out int nextHopNodeId, out int pathEdgeCount)
        {
            nextHopNodeId = -1;
            pathEdgeCount = 0;

            if (!IsValidNodeId(currentNodeId) || !IsValidNodeId(targetNodeId))
            {
                return false;
            }

            if (currentNodeId == targetNodeId)
            {
                return false;
            }

            var previous = new int[_graph.NodeCount];
            for (var i = 0; i < previous.Length; i++)
            {
                previous[i] = -1;
            }

            var queue = new Queue<int>();
            previous[currentNodeId] = currentNodeId;
            queue.Enqueue(currentNodeId);

            while (queue.Count > 0)
            {
                var nodeId = queue.Dequeue();
                if (nodeId == targetNodeId)
                {
                    break;
                }

                if (!_graph.TryGetNode(nodeId, out var node))
                {
                    continue;
                }

                var neighbors = node.NeighborIds;
                for (var i = 0; i < neighbors.Count; i++)
                {
                    var neighborId = neighbors[i];
                    if (!IsValidNodeId(neighborId) || previous[neighborId] >= 0)
                    {
                        continue;
                    }

                    previous[neighborId] = nodeId;
                    queue.Enqueue(neighborId);
                }
            }

            if (previous[targetNodeId] < 0)
            {
                return false;
            }

            var cursor = targetNodeId;
            pathEdgeCount = 0;
            while (previous[cursor] != currentNodeId)
            {
                cursor = previous[cursor];
                pathEdgeCount++;
            }

            nextHopNodeId = cursor;
            pathEdgeCount++;
            return true;
        }

        private bool TryCalculateDistances(int startNodeId, out int[] distances)
        {
            distances = new int[_graph.NodeCount];
            for (var i = 0; i < distances.Length; i++)
            {
                distances[i] = -1;
            }

            if (!IsValidNodeId(startNodeId))
            {
                return false;
            }

            var queue = new Queue<int>();
            distances[startNodeId] = 0;
            queue.Enqueue(startNodeId);

            while (queue.Count > 0)
            {
                var nodeId = queue.Dequeue();
                if (!_graph.TryGetNode(nodeId, out var node))
                {
                    continue;
                }

                var neighbors = node.NeighborIds;
                for (var i = 0; i < neighbors.Count; i++)
                {
                    var neighborId = neighbors[i];
                    if (!IsValidNodeId(neighborId) || distances[neighborId] >= 0)
                    {
                        continue;
                    }

                    distances[neighborId] = distances[nodeId] + 1;
                    queue.Enqueue(neighborId);
                }
            }

            return true;
        }

        private float CalculateLocalScore(
            SpatialNode node,
            int previousNodeId,
            float coverageNeed,
            float recentHistoryPenalty)
        {
            var normalizedConnectivity = _maxDegree > 0 ? (float)node.NeighborIds.Count / _maxDegree : 0f;
            var immediateBacktrack = node.Id == previousNodeId ? 1f : 0f;

            return (_coverageWeight * coverageNeed)
                + (_connectivityWeight * normalizedConnectivity)
                - (_recentHistoryWeight * recentHistoryPenalty)
                - (_immediateBacktrackWeight * immediateBacktrack);
        }

        private bool IsBetterLocalCandidate(
            int candidateNodeId,
            float score,
            float coverageNeed,
            float recentHistoryPenalty,
            int bestNodeId,
            float bestScore,
            float bestCoverageNeed,
            float bestRecentHistoryPenalty)
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

            if (coverageNeed > bestCoverageNeed + ScoreEpsilon)
            {
                return true;
            }

            if (coverageNeed < bestCoverageNeed - ScoreEpsilon)
            {
                return false;
            }

            if (recentHistoryPenalty < bestRecentHistoryPenalty - ScoreEpsilon)
            {
                return true;
            }

            if (recentHistoryPenalty > bestRecentHistoryPenalty + ScoreEpsilon)
            {
                return false;
            }

            return candidateNodeId < bestNodeId;
        }

        private bool IsBetterGlobalTarget(
            int candidateNodeId,
            float coverageNeed,
            int distance,
            int cyclicKey,
            int bestNodeId,
            float bestCoverageNeed,
            int bestDistance,
            int bestCyclicKey)
        {
            if (bestNodeId < 0)
            {
                return true;
            }

            if (coverageNeed > bestCoverageNeed + ScoreEpsilon)
            {
                return true;
            }

            if (coverageNeed < bestCoverageNeed - ScoreEpsilon)
            {
                return false;
            }

            if (distance < bestDistance)
            {
                return true;
            }

            if (distance > bestDistance)
            {
                return false;
            }

            if (cyclicKey < bestCyclicKey)
            {
                return true;
            }

            if (cyclicKey > bestCyclicKey)
            {
                return false;
            }

            return candidateNodeId < bestNodeId;
        }

        private void EnterGlobalCoverage()
        {
            if (Mode == ConfidencePatrolPlanningMode.GlobalCoverage)
            {
                return;
            }

            Mode = ConfidencePatrolPlanningMode.GlobalCoverage;
            GlobalInterventionCount++;
        }

        private void ClearGlobalTarget()
        {
            GlobalCoverageTargetNodeId = -1;
            Mode = ConfidencePatrolPlanningMode.LocalCoverage;
        }

        private void ClearLastSelectionDebug()
        {
            LastCandidateCount = 0;
            LastSelectedScore = 0f;
            LastSelectedCoverageNeed = 0f;
        }

        private void ApplyLastSelectionDebug(ConfidencePatrolPlanResult result)
        {
            LastCandidateCount = result.CandidateCount;
            LastSelectedScore = result.Score;
            LastSelectedCoverageNeed = result.CoverageNeed;
        }

        private int GetCoverageCycleCyclicKey(int nodeId)
        {
            if (_graph.NodeCount == 0)
            {
                return 0;
            }

            var offset = _memory.CoverageCycleIndex % _graph.NodeCount;
            return (nodeId - offset + _graph.NodeCount) % _graph.NodeCount;
        }

        private float GetVisitInjectionForDepth(int depth)
        {
            switch (depth)
            {
                case 0:
                    return _visitDepth0Injection;
                case 1:
                    return _visitDepth1Injection;
                case 2:
                    return _visitDepth2Injection;
                default:
                    return 0f;
            }
        }

        private bool IsValidNodeId(int nodeId)
        {
            return nodeId >= 0 && nodeId < _graph.NodeCount;
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

    public readonly struct ConfidencePatrolPlanResult
    {
        public ConfidencePatrolPlanResult(
            bool isValid,
            int destinationNodeId,
            ConfidencePatrolPlanningMode planningMode,
            int globalCoverageTargetNodeId,
            float score,
            float coverageNeed,
            int candidateCount)
        {
            IsValid = isValid;
            DestinationNodeId = destinationNodeId;
            PlanningMode = planningMode;
            GlobalCoverageTargetNodeId = globalCoverageTargetNodeId;
            Score = score;
            CoverageNeed = coverageNeed;
            CandidateCount = candidateCount;
        }

        public bool IsValid { get; }
        public int DestinationNodeId { get; }
        public ConfidencePatrolPlanningMode PlanningMode { get; }
        public int GlobalCoverageTargetNodeId { get; }
        public float Score { get; }
        public float CoverageNeed { get; }

        // Local mode: local candidate count. New global target: eligible target count.
        // Persistent global target: remaining path edge count to the target.
        public int CandidateCount { get; }

        public static ConfidencePatrolPlanResult Invalid(
            ConfidencePatrolPlanningMode planningMode,
            int globalCoverageTargetNodeId)
        {
            return new ConfidencePatrolPlanResult(false, -1, planningMode, globalCoverageTargetNodeId, 0f, 0f, 0);
        }
    }

    public readonly struct ConfidencePatrolVisitResult
    {
        public ConfidencePatrolVisitResult(
            SpatialCoverageVisitResult coverageVisitResult,
            bool clearedGlobalTarget,
            ConfidencePatrolPlanningMode planningMode,
            int globalCoverageTargetNodeId)
        {
            CoverageVisitResult = coverageVisitResult;
            ClearedGlobalTarget = clearedGlobalTarget;
            PlanningMode = planningMode;
            GlobalCoverageTargetNodeId = globalCoverageTargetNodeId;
        }

        public SpatialCoverageVisitResult CoverageVisitResult { get; }
        public bool ClearedGlobalTarget { get; }
        public ConfidencePatrolPlanningMode PlanningMode { get; }
        public int GlobalCoverageTargetNodeId { get; }
    }
}
