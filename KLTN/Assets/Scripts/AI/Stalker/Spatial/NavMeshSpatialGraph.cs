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
        }

        public IReadOnlyList<SpatialNode> Nodes => _readOnlyNodes;
        public int NodeCount => _nodes.Length;
        public int EdgeCount { get; }
        public bool IsEmpty => _nodes.Length == 0;

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
    }
}
