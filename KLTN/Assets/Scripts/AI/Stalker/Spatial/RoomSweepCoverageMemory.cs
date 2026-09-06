using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common.Spatial;

namespace EchoProtocol.AI.Stalker.Spatial
{
    public sealed class RoomSweepCoverageMemory
    {
        private readonly Dictionary<RegionId, RegionCoverage> _regions = new Dictionary<RegionId, RegionCoverage>();
        private readonly Dictionary<int, RegionId> _registeredRegionByNodeId = new Dictionary<int, RegionId>();

        public void RegisterRegion(RegionId regionId, IReadOnlyCollection<int> spatialNodeIds)
        {
            if (!regionId.IsValid)
            {
                return;
            }

            _regions.TryGetValue(regionId, out var existing);
            var uniqueNodeIds = new List<int>(spatialNodeIds?.Count ?? 0);
            if (spatialNodeIds != null)
            {
                foreach (var nodeId in spatialNodeIds)
                {
                    if (nodeId < 0)
                    {
                        continue;
                    }

                    if (_registeredRegionByNodeId.TryGetValue(nodeId, out var ownerRegion)
                        && ownerRegion != regionId)
                    {
                        continue;
                    }

                    AddUnique(uniqueNodeIds, nodeId);
                }
            }

            uniqueNodeIds.Sort();
            var uniqueNodeSet = new HashSet<int>(uniqueNodeIds);
            if (existing != null)
            {
                for (var i = 0; i < existing.ProbeNodeIds.Count; i++)
                {
                    var oldNodeId = existing.ProbeNodeIds[i];
                    if (!uniqueNodeSet.Contains(oldNodeId))
                    {
                        _registeredRegionByNodeId.Remove(oldNodeId);
                    }
                }
            }

            var observed = new HashSet<int>();
            if (existing != null)
            {
                for (var i = 0; i < uniqueNodeIds.Count; i++)
                {
                    var nodeId = uniqueNodeIds[i];
                    if (existing.ObservedNodeIds.Contains(nodeId))
                    {
                        observed.Add(nodeId);
                    }
                }
            }

            for (var i = 0; i < uniqueNodeIds.Count; i++)
            {
                _registeredRegionByNodeId[uniqueNodeIds[i]] = regionId;
            }

            _regions[regionId] = new RegionCoverage(uniqueNodeIds, observed);
        }

        public bool MarkObserved(RegionId regionId, int spatialNodeId)
        {
            return _regions.TryGetValue(regionId, out var coverage)
                && coverage.RegisteredNodeIds.Contains(spatialNodeId)
                && coverage.ObservedNodeIds.Add(spatialNodeId);
        }

        public int MarkObserved(RegionId regionId, IReadOnlyCollection<int> spatialNodeIds)
        {
            if (spatialNodeIds == null || !_regions.TryGetValue(regionId, out _))
            {
                return 0;
            }

            var newlyObserved = 0;
            foreach (var nodeId in spatialNodeIds)
            {
                if (MarkObserved(regionId, nodeId))
                {
                    newlyObserved++;
                }
            }

            return newlyObserved;
        }

        public bool IsObserved(RegionId regionId, int spatialNodeId)
        {
            return _regions.TryGetValue(regionId, out var coverage)
                && coverage.ObservedNodeIds.Contains(spatialNodeId);
        }

        public int GetObservedCount(RegionId regionId)
        {
            return _regions.TryGetValue(regionId, out var coverage) ? coverage.ObservedNodeIds.Count : 0;
        }

        public int GetTotalProbeCount(RegionId regionId)
        {
            return _regions.TryGetValue(regionId, out var coverage) ? coverage.ProbeNodeIds.Count : 0;
        }

        public int GetRemainingProbeCount(RegionId regionId)
        {
            return _regions.TryGetValue(regionId, out var coverage)
                ? coverage.ProbeNodeIds.Count - coverage.ObservedNodeIds.Count
                : 0;
        }

        public float GetCoverage01(RegionId regionId)
        {
            if (!_regions.TryGetValue(regionId, out var coverage) || coverage.ProbeNodeIds.Count == 0)
            {
                return 0f;
            }

            return (float)coverage.ObservedNodeIds.Count / coverage.ProbeNodeIds.Count;
        }

        public bool TryGetUnobservedProbeNodeIds(RegionId regionId, out IReadOnlyList<int> nodeIds)
        {
            if (!_regions.TryGetValue(regionId, out var coverage))
            {
                nodeIds = Array.Empty<int>();
                return false;
            }

            var unobserved = new List<int>();
            for (var i = 0; i < coverage.ProbeNodeIds.Count; i++)
            {
                var nodeId = coverage.ProbeNodeIds[i];
                if (!coverage.ObservedNodeIds.Contains(nodeId))
                {
                    unobserved.Add(nodeId);
                }
            }

            nodeIds = Array.AsReadOnly(unobserved.ToArray());
            return true;
        }

        public bool IsRegionCleared(RegionId regionId)
        {
            return _regions.TryGetValue(regionId, out var coverage) && coverage.IsCleared;
        }

        public void MarkRegionCleared(RegionId regionId)
        {
            if (_regions.TryGetValue(regionId, out var coverage))
            {
                coverage.IsCleared = true;
            }
        }

        public void ResetRegion(RegionId regionId)
        {
            if (!_regions.TryGetValue(regionId, out var coverage))
            {
                return;
            }

            coverage.ObservedNodeIds.Clear();
            coverage.IsCleared = false;
        }

        public void Reset()
        {
            _regions.Clear();
            _registeredRegionByNodeId.Clear();
        }

        private static void AddUnique(List<int> values, int value)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] == value)
                {
                    return;
                }
            }

            values.Add(value);
        }

        private sealed class RegionCoverage
        {
            public RegionCoverage(IReadOnlyList<int> probeNodeIds, HashSet<int> observedNodeIds)
            {
                ProbeNodeIds = Array.AsReadOnly(Copy(probeNodeIds));
                RegisteredNodeIds = new HashSet<int>(probeNodeIds);
                ObservedNodeIds = observedNodeIds ?? new HashSet<int>();
            }

            public IReadOnlyList<int> ProbeNodeIds { get; }
            public HashSet<int> RegisteredNodeIds { get; }
            public HashSet<int> ObservedNodeIds { get; }
            public bool IsCleared { get; set; }

            private static int[] Copy(IReadOnlyList<int> source)
            {
                var copy = new int[source?.Count ?? 0];
                for (var i = 0; i < copy.Length; i++)
                {
                    copy[i] = source[i];
                }

                return copy;
            }
        }
    }
}
