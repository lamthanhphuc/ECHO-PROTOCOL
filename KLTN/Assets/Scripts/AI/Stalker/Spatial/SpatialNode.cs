using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Spatial
{
    public sealed class SpatialNode
    {
        private readonly int[] _neighborIds;
        private readonly IReadOnlyList<int> _readOnlyNeighborIds;

        public SpatialNode(
            int id,
            Vector3 position,
            int area,
            int triangleIndex,
            int vertexIndex0,
            int vertexIndex1,
            int vertexIndex2,
            IReadOnlyCollection<int> neighborIds)
        {
            Id = id;
            Position = position;
            Area = area;
            TriangleIndex = triangleIndex;
            VertexIndex0 = vertexIndex0;
            VertexIndex1 = vertexIndex1;
            VertexIndex2 = vertexIndex2;

            _neighborIds = new int[neighborIds?.Count ?? 0];
            if (neighborIds != null)
            {
                var index = 0;
                foreach (var neighborId in neighborIds)
                {
                    _neighborIds[index] = neighborId;
                    index++;
                }
            }

            Array.Sort(_neighborIds);
            _readOnlyNeighborIds = Array.AsReadOnly(_neighborIds);
        }

        public int Id { get; }
        public Vector3 Position { get; }
        public int Area { get; }
        public int TriangleIndex { get; }
        public int VertexIndex0 { get; }
        public int VertexIndex1 { get; }
        public int VertexIndex2 { get; }
        public IReadOnlyList<int> NeighborIds => _readOnlyNeighborIds;
    }
}
