using System;
using EchoProtocol.AI.Common.Spatial;

namespace EchoProtocol.AI.Stalker.Spatial
{
    public sealed class CoverageMemory
    {
        private const int RecentHistoryCapacity = 8;

        private readonly int[] _nodeVisitCounts;
        private readonly float[] _nodeLastVisitedTimes;
        private readonly bool[] _nodeVisited;
        private readonly RegionSlot[] _regions;
        private readonly int[] _recentNodeHistory = new int[RecentHistoryCapacity];
        private readonly RegionId[] _recentRegionHistory = new RegionId[RecentHistoryCapacity];
        private int _recentNodeHistoryCount;
        private int _recentRegionHistoryCount;

        public CoverageMemory(int nodeCount)
            : this(nodeCount, null)
        {
        }

        public CoverageMemory(int nodeCount, RegionGraph regionGraph)
        {
            if (nodeCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nodeCount), "Node count cannot be negative.");
            }

            NodeCount = nodeCount;
            RegionGraph = regionGraph;
            _nodeVisitCounts = new int[nodeCount];
            _nodeLastVisitedTimes = new float[nodeCount];
            _nodeVisited = new bool[nodeCount];
            for (var i = 0; i < _nodeLastVisitedTimes.Length; i++)
            {
                _nodeLastVisitedTimes[i] = -1f;
            }

            var regionCount = regionGraph?.Regions.Count ?? 0;
            _regions = new RegionSlot[regionCount];
            for (var i = 0; i < regionCount; i++)
            {
                _regions[i] = new RegionSlot(regionGraph.Regions[i].Id);
            }

            for (var i = 0; i < RecentHistoryCapacity; i++)
            {
                _recentNodeHistory[i] = -1;
                _recentRegionHistory[i] = RegionId.Invalid;
            }
        }

        public int NodeCount { get; }
        public RegionGraph RegionGraph { get; }
        public int RecentNodeHistoryCount => _recentNodeHistoryCount;
        public int RecentRegionHistoryCount => _recentRegionHistoryCount;

        public CoverageVisitResult RecordPhysicalNodeArrival(int nodeId, float currentTime)
        {
            if (!IsValidNode(nodeId))
            {
                return CoverageVisitResult.Invalid(nodeId);
            }

            var firstNodeVisit = !_nodeVisited[nodeId];
            _nodeVisited[nodeId] = true;
            _nodeVisitCounts[nodeId]++;
            _nodeLastVisitedTimes[nodeId] = currentTime;
            PushRecentNode(nodeId);

            var regionUpdated = false;
            var regionId = RegionId.Invalid;
            var regionVisitCount = 0;
            if (RegionGraph != null && RegionGraph.TryGetRegionForNode(nodeId, out regionId))
            {
                regionUpdated = RecordRegionArrival(regionId, currentTime, out regionVisitCount);
            }

            return new CoverageVisitResult(
                true,
                nodeId,
                firstNodeVisit,
                _nodeVisitCounts[nodeId],
                regionUpdated,
                regionId,
                regionVisitCount);
        }

        public bool WasNodeVisited(int nodeId) => IsValidNode(nodeId) && _nodeVisited[nodeId];
        public int GetNodeVisitCount(int nodeId) => IsValidNode(nodeId) ? _nodeVisitCounts[nodeId] : 0;
        public float GetNodeLastVisitedTime(int nodeId) => IsValidNode(nodeId) ? _nodeLastVisitedTimes[nodeId] : -1f;

        public int GetRegionVisitCount(RegionId regionId)
        {
            return TryGetRegionIndex(regionId, out var index) ? _regions[index].VisitCount : 0;
        }

        public float GetRegionLastVisitedTime(RegionId regionId)
        {
            return TryGetRegionIndex(regionId, out var index) ? _regions[index].LastVisitedTime : -1f;
        }

        public bool WasRegionVisited(RegionId regionId)
        {
            return TryGetRegionIndex(regionId, out var index) && _regions[index].Visited;
        }

        public int GetRecentNodeFrequency(int nodeId)
        {
            var count = 0;
            for (var i = 0; i < _recentNodeHistoryCount; i++)
            {
                if (_recentNodeHistory[i] == nodeId)
                {
                    count++;
                }
            }

            return count;
        }

        public int GetRecentRegionFrequency(RegionId regionId)
        {
            var count = 0;
            for (var i = 0; i < _recentRegionHistoryCount; i++)
            {
                if (_recentRegionHistory[i] == regionId)
                {
                    count++;
                }
            }

            return count;
        }

        public bool TryGetRecentNode(int index, out int nodeId)
        {
            if (index >= 0 && index < _recentNodeHistoryCount)
            {
                nodeId = _recentNodeHistory[index];
                return true;
            }

            nodeId = -1;
            return false;
        }

        public bool TryGetRecentRegion(int index, out RegionId regionId)
        {
            if (index >= 0 && index < _recentRegionHistoryCount)
            {
                regionId = _recentRegionHistory[index];
                return true;
            }

            regionId = RegionId.Invalid;
            return false;
        }

        private bool RecordRegionArrival(RegionId regionId, float currentTime, out int visitCount)
        {
            visitCount = 0;
            if (!TryGetRegionIndex(regionId, out var index))
            {
                return false;
            }

            _regions[index].Visited = true;
            _regions[index].VisitCount++;
            _regions[index].LastVisitedTime = currentTime;
            visitCount = _regions[index].VisitCount;
            PushRecentRegion(regionId);
            return true;
        }

        private bool IsValidNode(int nodeId) => nodeId >= 0 && nodeId < NodeCount;

        private bool TryGetRegionIndex(RegionId regionId, out int index)
        {
            for (var i = 0; i < _regions.Length; i++)
            {
                if (_regions[i].Id == regionId)
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        private void PushRecentNode(int nodeId)
        {
            for (var i = Math.Min(_recentNodeHistoryCount, RecentHistoryCapacity - 1); i > 0; i--)
            {
                _recentNodeHistory[i] = _recentNodeHistory[i - 1];
            }

            _recentNodeHistory[0] = nodeId;
            if (_recentNodeHistoryCount < RecentHistoryCapacity)
            {
                _recentNodeHistoryCount++;
            }
        }

        private void PushRecentRegion(RegionId regionId)
        {
            for (var i = Math.Min(_recentRegionHistoryCount, RecentHistoryCapacity - 1); i > 0; i--)
            {
                _recentRegionHistory[i] = _recentRegionHistory[i - 1];
            }

            _recentRegionHistory[0] = regionId;
            if (_recentRegionHistoryCount < RecentHistoryCapacity)
            {
                _recentRegionHistoryCount++;
            }
        }

        private struct RegionSlot
        {
            public RegionSlot(RegionId id)
            {
                Id = id;
                Visited = false;
                VisitCount = 0;
                LastVisitedTime = -1f;
            }

            public RegionId Id { get; }
            public bool Visited;
            public int VisitCount;
            public float LastVisitedTime;
        }
    }

    public readonly struct CoverageVisitResult
    {
        public CoverageVisitResult(
            bool isValid,
            int nodeId,
            bool wasFirstNodeVisit,
            int nodeVisitCount,
            bool regionUpdated,
            RegionId regionId,
            int regionVisitCount)
        {
            IsValid = isValid;
            NodeId = nodeId;
            WasFirstNodeVisit = wasFirstNodeVisit;
            NodeVisitCount = nodeVisitCount;
            RegionUpdated = regionUpdated;
            RegionId = regionId;
            RegionVisitCount = regionVisitCount;
        }

        public bool IsValid { get; }
        public int NodeId { get; }
        public bool WasFirstNodeVisit { get; }
        public int NodeVisitCount { get; }
        public bool RegionUpdated { get; }
        public RegionId RegionId { get; }
        public int RegionVisitCount { get; }

        public static CoverageVisitResult Invalid(int nodeId)
        {
            return new CoverageVisitResult(false, nodeId, false, 0, false, RegionId.Invalid, 0);
        }
    }
}
