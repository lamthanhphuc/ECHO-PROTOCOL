using System.Collections.Generic;
using EchoProtocol.AI.Common.Spatial;

namespace EchoProtocol.AI.Stalker.Spatial
{
    public sealed class RoomSweepPlanner
    {
        private readonly NavMeshSpatialGraph _spatialGraph;
        private readonly RegionGraph _regionGraph;
        private readonly RoomSweepCoverageMemory _coverageMemory;
        private bool _useSeededVariation;
        private int _variationSeed;
        private readonly List<int> _activeProbeNodeIds = new List<int>();
        private readonly HashSet<int> _activeProbeNodeSet = new HashSet<int>();
        private readonly HashSet<int> _rejectedProbeNodeIds = new HashSet<int>();

        public RoomSweepPlanner(
            NavMeshSpatialGraph spatialGraph,
            RegionGraph regionGraph,
            RoomSweepCoverageMemory coverageMemory)
        {
            _spatialGraph = spatialGraph;
            _regionGraph = regionGraph;
            _coverageMemory = coverageMemory;
            _useSeededVariation = false;
            _variationSeed = 0;
            CurrentRegionId = RegionId.Invalid;
        }

        public RoomSweepPlanner(
            NavMeshSpatialGraph spatialGraph,
            RegionGraph regionGraph,
            RoomSweepCoverageMemory coverageMemory,
            int variationSeed)
        {
            _spatialGraph = spatialGraph;
            _regionGraph = regionGraph;
            _coverageMemory = coverageMemory;
            _useSeededVariation = true;
            _variationSeed = variationSeed;
            CurrentRegionId = RegionId.Invalid;
        }

        public void ConfigureVariation(int variationSeed)
        {
            _variationSeed = variationSeed;
            _useSeededVariation = true;
        }

        public RegionId CurrentRegionId { get; private set; }
        public bool HasActiveRegion => CurrentRegionId.IsValid;
        public int RejectedProbeCount => _rejectedProbeNodeIds.Count;

        public bool IsFullyObserved
        {
            get
            {
                if (!HasActiveRegion)
                {
                    return false;
                }

                var total = _coverageMemory.GetTotalProbeCount(CurrentRegionId);
                return total > 0 && _coverageMemory.GetObservedCount(CurrentRegionId) == total;
            }
        }

        public bool IsExhausted
        {
            get
            {
                if (!HasActiveRegion || IsFullyObserved || !_coverageMemory.TryGetUnobservedProbeNodeIds(CurrentRegionId, out var unobserved))
                {
                    return false;
                }

                if (unobserved.Count == 0)
                {
                    return false;
                }

                for (var i = 0; i < unobserved.Count; i++)
                {
                    if (!_rejectedProbeNodeIds.Contains(unobserved[i]))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public bool TryBeginRegion(RegionId regionId)
        {
            if (!IsEligibleRoomRegion(regionId))
            {
                ClearActiveRegion();
                return false;
            }

            if (!_regionGraph.TryGetSpatialNodeIdsForRegion(regionId, out var probeNodeIds))
            {
                probeNodeIds = System.Array.Empty<int>();
            }

            if (CurrentRegionId == regionId && SameActiveProbeSet(probeNodeIds))
            {
                _rejectedProbeNodeIds.Clear();
                return true;
            }

            _coverageMemory.RegisterRegion(regionId, probeNodeIds);
            CurrentRegionId = regionId;
            ReplaceActiveProbeSet(probeNodeIds);
            _rejectedProbeNodeIds.Clear();
            return true;
        }

        public bool TrySelectNextProbe(int currentSpatialNodeId, out int targetSpatialNodeId)
        {
            targetSpatialNodeId = -1;
            if (!HasActiveRegion || !_coverageMemory.TryGetUnobservedProbeNodeIds(CurrentRegionId, out var unobserved))
            {
                return false;
            }

            var candidates = new List<int>();
            for (var i = 0; i < unobserved.Count; i++)
            {
                var nodeId = unobserved[i];
                if (!_rejectedProbeNodeIds.Contains(nodeId))
                {
                    candidates.Add(nodeId);
                }
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            candidates.Sort();
            if (_spatialGraph == null || !_spatialGraph.TryGetNode(currentSpatialNodeId, out _))
            {
                targetSpatialNodeId = _useSeededVariation
                    ? SelectSeededCandidate(candidates)
                    : candidates[0];
                return true;
            }

            var distances = BuildHopDistances(currentSpatialNodeId);
            var bestNodeId = candidates[0];
            var bestDistance = int.MaxValue;
            for (var i = 0; i < candidates.Count; i++)
            {
                var nodeId = candidates[i];
                var distance = nodeId >= 0 && nodeId < distances.Length ? distances[nodeId] : -1;
                if (distance >= 0 && distance < bestDistance)
                {
                    bestDistance = distance;
                    bestNodeId = nodeId;
                }
            }

            if (!_useSeededVariation)
            {
                targetSpatialNodeId = bestNodeId;
                return true;
            }

            var selectionPool = new List<int>();
            if (bestDistance == int.MaxValue)
            {
                selectionPool.AddRange(candidates);
            }
            else
            {
                for (var i = 0; i < candidates.Count; i++)
                {
                    var nodeId = candidates[i];
                    var distance = nodeId >= 0 && nodeId < distances.Length ? distances[nodeId] : -1;
                    if (distance == bestDistance)
                    {
                        selectionPool.Add(nodeId);
                    }
                }
            }

            targetSpatialNodeId = SelectSeededCandidate(selectionPool);
            return true;
        }

        private int SelectSeededCandidate(List<int> candidates)
        {
            candidates.Sort();

            unchecked
            {
                uint hash = Mix(
                    unchecked((uint)_variationSeed)
                    ^ Mix(unchecked((uint)CurrentRegionId.Value)));

                for (var i = 0; i < candidates.Count; i++)
                {
                    hash = Mix(hash ^ unchecked((uint)candidates[i]));
                }

                return candidates[(int)(hash % (uint)candidates.Count)];
            }
        }

        private static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7feb352dU;
            value ^= value >> 15;
            value *= 0x846ca68bU;
            value ^= value >> 16;
            return value;
        }

        public bool RejectProbe(int spatialNodeId)
        {
            if (!HasActiveRegion
                || !_activeProbeNodeSet.Contains(spatialNodeId)
                || _coverageMemory.IsObserved(CurrentRegionId, spatialNodeId))
            {
                return false;
            }

            return _rejectedProbeNodeIds.Add(spatialNodeId);
        }

        public void ClearRejectedProbes()
        {
            _rejectedProbeNodeIds.Clear();
        }

        private bool IsEligibleRoomRegion(RegionId regionId)
        {
            return regionId.IsValid
                && _regionGraph != null
                && _coverageMemory != null
                && _regionGraph.ContainsRegion(regionId)
                && _regionGraph.TryGetRegionSemanticMetadata(regionId, out var metadata)
                && metadata.Kind == RegionSemanticKind.Room;
        }

        private void ClearActiveRegion()
        {
            CurrentRegionId = RegionId.Invalid;
            _activeProbeNodeIds.Clear();
            _activeProbeNodeSet.Clear();
            _rejectedProbeNodeIds.Clear();
        }

        private bool SameActiveProbeSet(IReadOnlyList<int> probeNodeIds)
        {
            if (_activeProbeNodeIds.Count != probeNodeIds.Count)
            {
                return false;
            }

            for (var i = 0; i < _activeProbeNodeIds.Count; i++)
            {
                if (_activeProbeNodeIds[i] != probeNodeIds[i])
                {
                    return false;
                }
            }

            return true;
        }

        private void ReplaceActiveProbeSet(IReadOnlyList<int> probeNodeIds)
        {
            _activeProbeNodeIds.Clear();
            _activeProbeNodeSet.Clear();
            for (var i = 0; i < probeNodeIds.Count; i++)
            {
                var nodeId = probeNodeIds[i];
                _activeProbeNodeIds.Add(nodeId);
                _activeProbeNodeSet.Add(nodeId);
            }
        }

        private int[] BuildHopDistances(int startNodeId)
        {
            var distances = new int[_spatialGraph.NodeCount];
            for (var i = 0; i < distances.Length; i++)
            {
                distances[i] = -1;
            }

            var queue = new Queue<int>();
            distances[startNodeId] = 0;
            queue.Enqueue(startNodeId);
            while (queue.Count > 0)
            {
                var nodeId = queue.Dequeue();
                if (!_spatialGraph.TryGetNode(nodeId, out var node))
                {
                    continue;
                }

                var neighbors = new List<int>(node.NeighborIds);
                neighbors.Sort();
                for (var i = 0; i < neighbors.Count; i++)
                {
                    var neighborId = neighbors[i];
                    if (neighborId < 0 || neighborId >= distances.Length || distances[neighborId] >= 0)
                    {
                        continue;
                    }

                    distances[neighborId] = distances[nodeId] + 1;
                    queue.Enqueue(neighborId);
                }
            }

            return distances;
        }
    }
}
