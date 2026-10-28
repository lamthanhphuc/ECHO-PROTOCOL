using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Spatial
{
    public sealed class NavMeshSpatialGraph
    {
        private readonly SpatialNode[] _nodes;
        private readonly IReadOnlyList<SpatialNode> _readOnlyNodes;
        private readonly int[] _nodeIndicesByX;

        public NavMeshSpatialGraph(IReadOnlyList<SpatialNode> nodes)
        {
            _nodes = new SpatialNode[nodes?.Count ?? 0];

            var directedEdgeCount = 0;
            for (var i = 0; i < _nodes.Length; i++)
            {
                var node = nodes[i];
                _nodes[i] = node;
                directedEdgeCount += node.NeighborIds.Count;
            }

            _readOnlyNodes = Array.AsReadOnly(_nodes);
            _nodeIndicesByX = new int[_nodes.Length];
            for (var i = 0; i < _nodes.Length; i++) _nodeIndicesByX[i] = i;
            Array.Sort(_nodeIndicesByX, (left, right) => _nodes[left].Position.x.CompareTo(_nodes[right].Position.x));
            EdgeCount = directedEdgeCount / 2;
            CompatibilityIdentity = CalculateCompatibilityIdentity(_nodes);
        }

        public IReadOnlyList<SpatialNode> Nodes => _readOnlyNodes;
        public int NodeCount => _nodes.Length;
        public int EdgeCount { get; }
        public bool IsEmpty => _nodes.Length == 0;
        public SpatialGraphCompatibilityIdentity CompatibilityIdentity { get; }

        public bool TryResolveNearestNode(Vector3 position, out int nodeId)
        {
            nodeId = -1;
            var lower = 0;
            var upper = _nodeIndicesByX.Length;
            while (lower < upper)
            {
                var middle = lower + (upper - lower) / 2;
                if (_nodes[_nodeIndicesByX[middle]].Position.x < position.x) lower = middle + 1;
                else upper = middle;
            }

            var left = lower - 1;
            var right = lower;
            var bestIndex = -1;
            var bestSqrDistance = float.PositiveInfinity;
            while (left >= 0 || right < _nodeIndicesByX.Length)
            {
                var leftDelta = left >= 0 ? position.x - _nodes[_nodeIndicesByX[left]].Position.x : float.PositiveInfinity;
                var rightDelta = right < _nodeIndicesByX.Length ? _nodes[_nodeIndicesByX[right]].Position.x - position.x : float.PositiveInfinity;
                var leftBound = leftDelta * leftDelta;
                var rightBound = rightDelta * rightDelta;
                // X distance is a lower bound on 3D distance; retain equal-distance nodes for the original tie rule.
                if (leftBound > bestSqrDistance && rightBound > bestSqrDistance) break;
                var index = left >= 0 && (right >= _nodeIndicesByX.Length || leftBound <= rightBound)
                    ? _nodeIndicesByX[left--] : _nodeIndicesByX[right++];
                var sqrDistance = (_nodes[index].Position - position).sqrMagnitude;
                if (sqrDistance < bestSqrDistance || (sqrDistance == bestSqrDistance && index < bestIndex))
                {
                    bestSqrDistance = sqrDistance;
                    bestIndex = index;
                    nodeId = _nodes[index].Id;
                }
            }
            return nodeId >= 0;
        }

        public bool TryGetNode(int id, out SpatialNode node)
        {
            if (id >= 0 && id < _nodes.Length && _nodes[id].Id == id)
            {
                node = _nodes[id];
                return true;
            }

            node = null;
            return false;
        }

        private static SpatialGraphCompatibilityIdentity CalculateCompatibilityIdentity(IReadOnlyList<SpatialNode> nodes)
        {
            const ulong fnvOffset = 14695981039346656037UL;
            const ulong fnvPrime = 1099511628211UL;

            var hash = fnvOffset;
            AddInt(ref hash, nodes?.Count ?? 0, fnvPrime);
            if (nodes != null)
            {
                for (var i = 0; i < nodes.Count; i++)
                {
                    var node = nodes[i];
                    AddInt(ref hash, node.Id, fnvPrime);
                    AddInt(ref hash, Quantize(node.Position.x), fnvPrime);
                    AddInt(ref hash, Quantize(node.Position.y), fnvPrime);
                    AddInt(ref hash, Quantize(node.Position.z), fnvPrime);
                    AddInt(ref hash, node.Area, fnvPrime);
                    AddInt(ref hash, node.TriangleIndex, fnvPrime);
                    AddInt(ref hash, node.VertexIndex0, fnvPrime);
                    AddInt(ref hash, node.VertexIndex1, fnvPrime);
                    AddInt(ref hash, node.VertexIndex2, fnvPrime);
                    AddInt(ref hash, node.NeighborIds.Count, fnvPrime);
                    for (var neighborIndex = 0; neighborIndex < node.NeighborIds.Count; neighborIndex++)
                    {
                        AddInt(ref hash, node.NeighborIds[neighborIndex], fnvPrime);
                    }
                }
            }

            return new SpatialGraphCompatibilityIdentity(hash == 0UL ? 1UL : hash);
        }

        private static int Quantize(float value)
        {
            return (int)Math.Round(value * 1000f, MidpointRounding.AwayFromZero);
        }

        private static void AddInt(ref ulong hash, int value, ulong prime)
        {
            unchecked
            {
                hash ^= (uint)value;
                hash *= prime;
            }
        }
    }
}
