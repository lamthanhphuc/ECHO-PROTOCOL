using System;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Spatial
{
    public sealed class SpatialCoverageMemory
    {
        private const int RecentHistoryCapacity = 5;

        private readonly float[] _confidenceValues;
        private readonly float[] _confidenceUpdateTimes;
        private readonly bool[] _everExactVisited;
        private readonly bool[] _cycleExactVisited;
        private readonly int[] _exactVisitCounts;
        private readonly float[] _lastExactVisitTimes;
        private readonly int[] _recentHistory = new int[RecentHistoryCapacity];

        private int _recentHistoryCount;

        public SpatialCoverageMemory(int nodeCount)
        {
            if (nodeCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nodeCount), "Node count cannot be negative.");
            }

            NodeCount = nodeCount;
            _confidenceValues = new float[nodeCount];
            _confidenceUpdateTimes = new float[nodeCount];
            _everExactVisited = new bool[nodeCount];
            _cycleExactVisited = new bool[nodeCount];
            _exactVisitCounts = new int[nodeCount];
            _lastExactVisitTimes = new float[nodeCount];

            for (var i = 0; i < _lastExactVisitTimes.Length; i++)
            {
                _lastExactVisitTimes[i] = -1f;
            }

            for (var i = 0; i < _recentHistory.Length; i++)
            {
                _recentHistory[i] = -1;
            }
        }

        public int NodeCount { get; }
        public int LifetimeCoveredNodeCount { get; private set; }
        public int CycleCoveredNodeCount { get; private set; }
        public int CoverageCycleIndex { get; private set; }
        public float LifetimeCoverage => NodeCount > 0 ? (float)LifetimeCoveredNodeCount / NodeCount : 0f;
        public float CycleCoverage => NodeCount > 0 ? (float)CycleCoveredNodeCount / NodeCount : 0f;
        public bool IsCoverageCycleComplete => NodeCount > 0 && CycleCoveredNodeCount == NodeCount;
        public int RecentHistoryCount => _recentHistoryCount;

        public bool BeginNextCoverageCycle()
        {
            if (!IsCoverageCycleComplete)
            {
                return false;
            }

            for (var i = 0; i < _cycleExactVisited.Length; i++)
            {
                _cycleExactVisited[i] = false;
            }

            CycleCoveredNodeCount = 0;
            CoverageCycleIndex++;
            return true;
        }

        public SpatialCoverageVisitResult MarkExactVisited(int nodeId, float currentTime)
        {
            if (!IsValidNodeId(nodeId))
            {
                return SpatialCoverageVisitResult.Invalid(nodeId);
            }

            var wasFirstLifetimeVisit = !_everExactVisited[nodeId];
            if (wasFirstLifetimeVisit)
            {
                _everExactVisited[nodeId] = true;
                LifetimeCoveredNodeCount++;
            }

            var wasFirstCycleVisit = !_cycleExactVisited[nodeId];
            if (wasFirstCycleVisit)
            {
                _cycleExactVisited[nodeId] = true;
                CycleCoveredNodeCount++;
            }

            _exactVisitCounts[nodeId]++;
            _lastExactVisitTimes[nodeId] = currentTime;
            PushRecentHistory(nodeId);

            return new SpatialCoverageVisitResult(
                true,
                nodeId,
                wasFirstLifetimeVisit,
                wasFirstCycleVisit,
                _exactVisitCounts[nodeId]);
        }

        public void InjectConfidence(int nodeId, float injection, float currentTime, float halfLife)
        {
            if (!IsValidNodeId(nodeId))
            {
                return;
            }

            var currentConfidence = GetConfidence(nodeId, currentTime, halfLife);
            var nextConfidence = Mathf.Max(currentConfidence, Mathf.Clamp01(injection));

            _confidenceValues[nodeId] = nextConfidence;
            _confidenceUpdateTimes[nodeId] = Mathf.Max(currentTime, _confidenceUpdateTimes[nodeId]);
        }

        public float GetConfidence(int nodeId, float currentTime, float halfLife)
        {
            if (!IsValidNodeId(nodeId))
            {
                return 0f;
            }

            var storedConfidence = Mathf.Clamp01(_confidenceValues[nodeId]);
            var elapsed = Mathf.Max(0f, currentTime - _confidenceUpdateTimes[nodeId]);

            if (halfLife <= 0f)
            {
                return elapsed <= 0f ? storedConfidence : 0f;
            }

            var decay = Mathf.Pow(2f, -elapsed / halfLife);
            return Mathf.Clamp01(storedConfidence * decay);
        }

        public float GetCoverageNeed(int nodeId, float currentTime, float halfLife)
        {
            if (!IsValidNodeId(nodeId))
            {
                return 0f;
            }

            return 1f - GetConfidence(nodeId, currentTime, halfLife);
        }

        public bool WasEverExactVisited(int nodeId)
        {
            return IsValidNodeId(nodeId) && _everExactVisited[nodeId];
        }

        public bool WasExactVisitedThisCycle(int nodeId)
        {
            return IsValidNodeId(nodeId) && _cycleExactVisited[nodeId];
        }

        public int GetExactVisitCount(int nodeId)
        {
            return IsValidNodeId(nodeId) ? _exactVisitCounts[nodeId] : 0;
        }

        public float GetLastExactVisitTime(int nodeId)
        {
            return IsValidNodeId(nodeId) ? _lastExactVisitTimes[nodeId] : -1f;
        }

        public float GetRecentHistoryPenalty(int nodeId)
        {
            if (!IsValidNodeId(nodeId))
            {
                return 0f;
            }

            for (var i = 0; i < _recentHistoryCount; i++)
            {
                if (_recentHistory[i] == nodeId)
                {
                    return (RecentHistoryCapacity - i) / (float)RecentHistoryCapacity;
                }
            }

            return 0f;
        }

        public bool TryGetRecentHistoryNode(int historyIndex, out int nodeId)
        {
            if (historyIndex >= 0 && historyIndex < _recentHistoryCount)
            {
                nodeId = _recentHistory[historyIndex];
                return true;
            }

            nodeId = -1;
            return false;
        }

        private void PushRecentHistory(int nodeId)
        {
            var existingIndex = -1;
            for (var i = 0; i < _recentHistoryCount; i++)
            {
                if (_recentHistory[i] == nodeId)
                {
                    existingIndex = i;
                    break;
                }
            }

            if (existingIndex == 0)
            {
                return;
            }

            var shiftLimit = existingIndex > 0 ? existingIndex : Mathf.Min(_recentHistoryCount, RecentHistoryCapacity - 1);
            for (var i = shiftLimit; i > 0; i--)
            {
                _recentHistory[i] = _recentHistory[i - 1];
            }

            _recentHistory[0] = nodeId;
            if (existingIndex < 0 && _recentHistoryCount < RecentHistoryCapacity)
            {
                _recentHistoryCount++;
            }
        }

        private bool IsValidNodeId(int nodeId)
        {
            return nodeId >= 0 && nodeId < NodeCount;
        }
    }

    public readonly struct SpatialCoverageVisitResult
    {
        public SpatialCoverageVisitResult(
            bool isValid,
            int nodeId,
            bool wasFirstLifetimeVisit,
            bool wasFirstCycleVisit,
            int visitCount)
        {
            IsValid = isValid;
            NodeId = nodeId;
            WasFirstLifetimeVisit = wasFirstLifetimeVisit;
            WasFirstCycleVisit = wasFirstCycleVisit;
            VisitCount = visitCount;
        }

        public bool IsValid { get; }
        public int NodeId { get; }
        public bool WasFirstLifetimeVisit { get; }
        public bool WasFirstCycleVisit { get; }
        public int VisitCount { get; }

        public static SpatialCoverageVisitResult Invalid(int nodeId)
        {
            return new SpatialCoverageVisitResult(false, nodeId, false, false, 0);
        }
    }
}
