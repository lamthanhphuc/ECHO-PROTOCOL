using System;
using System.Collections.Generic;

namespace EchoProtocol.AI.Stalker.Spatial
{
    public sealed class NavMeshSpatialGraph
    {
        private readonly SpatialNode[] _nodes;
        private readonly IReadOnlyList<SpatialNode> _readOnlyNodes;

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
            EdgeCount = directedEdgeCount / 2;
            CompatibilityIdentity = CalculateCompatibilityIdentity(_nodes);
        }

        public IReadOnlyList<SpatialNode> Nodes => _readOnlyNodes;
        public int NodeCount => _nodes.Length;
        public int EdgeCount { get; }
        public bool IsEmpty => _nodes.Length == 0;
        public SpatialGraphCompatibilityIdentity CompatibilityIdentity { get; }

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
