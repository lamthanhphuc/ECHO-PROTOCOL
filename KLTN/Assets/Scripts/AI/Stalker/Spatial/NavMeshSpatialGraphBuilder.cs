using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace EchoProtocol.AI.Stalker.Spatial
{
    public static class NavMeshSpatialGraphBuilder
    {
        private const float VertexWeldEpsilon = 0.001f;
        private const float VertexWeldEpsilonSqr = VertexWeldEpsilon * VertexWeldEpsilon;

        // Compared against cross.sqrMagnitude, which is four times squared triangle area.
        private const float DegenerateTriangleAreaSqrEpsilon = 0.000001f;

        public static NavMeshSpatialGraph Build()
        {
            return Build(NavMesh.CalculateTriangulation());
        }

        public static NavMeshSpatialGraph Build(NavMeshTriangulation triangulation)
        {
            var vertices = triangulation.vertices;
            var indices = triangulation.indices;
            var areas = triangulation.areas;

            if (vertices == null || vertices.Length == 0 || indices == null || indices.Length < 3)
            {
                return new NavMeshSpatialGraph(Array.Empty<SpatialNode>());
            }

            var nodeBuildData = new List<NodeBuildData>(indices.Length / 3);
            var neighborSets = new List<HashSet<int>>(indices.Length / 3);
            var edgeMap = new Dictionary<EdgeKey, List<int>>();
            var rawToWeldedVertexIds = BuildWeldedVertexMap(vertices);

            for (var triangleStart = 0; triangleStart + 2 < indices.Length; triangleStart += 3)
            {
                var vertexIndex0 = indices[triangleStart];
                var vertexIndex1 = indices[triangleStart + 1];
                var vertexIndex2 = indices[triangleStart + 2];

                if (!IsValidTriangle(vertices, vertexIndex0, vertexIndex1, vertexIndex2))
                {
                    continue;
                }

                var nodeId = nodeBuildData.Count;
                var triangleIndex = triangleStart / 3;
                var area = areas != null && triangleIndex >= 0 && triangleIndex < areas.Length
                    ? areas[triangleIndex]
                    : 0;

                nodeBuildData.Add(new NodeBuildData(
                    nodeId,
                    GetCentroid(vertices, vertexIndex0, vertexIndex1, vertexIndex2),
                    area,
                    triangleIndex,
                    vertexIndex0,
                    vertexIndex1,
                    vertexIndex2));

                neighborSets.Add(new HashSet<int>());

                var weldedVertexId0 = rawToWeldedVertexIds[vertexIndex0];
                var weldedVertexId1 = rawToWeldedVertexIds[vertexIndex1];
                var weldedVertexId2 = rawToWeldedVertexIds[vertexIndex2];

                // Preserve raw-valid nodes, but skip adjacency for triangles collapsed by welding.
                if (weldedVertexId0 == weldedVertexId1
                    || weldedVertexId1 == weldedVertexId2
                    || weldedVertexId2 == weldedVertexId0)
                {
                    continue;
                }

                ConnectEdge(edgeMap, neighborSets, new EdgeKey(weldedVertexId0, weldedVertexId1), nodeId);
                ConnectEdge(edgeMap, neighborSets, new EdgeKey(weldedVertexId1, weldedVertexId2), nodeId);
                ConnectEdge(edgeMap, neighborSets, new EdgeKey(weldedVertexId2, weldedVertexId0), nodeId);
            }

            if (nodeBuildData.Count == 0)
            {
                return new NavMeshSpatialGraph(Array.Empty<SpatialNode>());
            }

            var nodes = new SpatialNode[nodeBuildData.Count];
            for (var i = 0; i < nodeBuildData.Count; i++)
            {
                var data = nodeBuildData[i];
                nodes[i] = new SpatialNode(
                    data.Id,
                    data.Position,
                    data.Area,
                    data.TriangleIndex,
                    data.VertexIndex0,
                    data.VertexIndex1,
                    data.VertexIndex2,
                    neighborSets[i]);
            }

            return new NavMeshSpatialGraph(nodes);
        }

        private static int[] BuildWeldedVertexMap(Vector3[] vertices)
        {
            var rawToWeldedVertexIds = new int[vertices.Length];
            var canonicalPositions = new List<Vector3>(vertices.Length);
            var cells = new Dictionary<VertexCellKey, List<int>>();

            for (var rawVertexIndex = 0; rawVertexIndex < vertices.Length; rawVertexIndex++)
            {
                var position = vertices[rawVertexIndex];
                var cell = VertexCellKey.FromPosition(position);
                var weldedVertexId = FindExistingWeldedVertex(cells, canonicalPositions, cell, position);

                if (weldedVertexId < 0)
                {
                    weldedVertexId = canonicalPositions.Count;
                    canonicalPositions.Add(position);
                    AddWeldedVertexToCell(cells, cell, weldedVertexId);
                }

                rawToWeldedVertexIds[rawVertexIndex] = weldedVertexId;
            }

            return rawToWeldedVertexIds;
        }

        private static int FindExistingWeldedVertex(
            Dictionary<VertexCellKey, List<int>> cells,
            List<Vector3> canonicalPositions,
            VertexCellKey cell,
            Vector3 position)
        {
            for (var zOffset = -1; zOffset <= 1; zOffset++)
            {
                for (var yOffset = -1; yOffset <= 1; yOffset++)
                {
                    for (var xOffset = -1; xOffset <= 1; xOffset++)
                    {
                        var neighborCell = new VertexCellKey(
                            cell.X + xOffset,
                            cell.Y + yOffset,
                            cell.Z + zOffset);

                        if (!cells.TryGetValue(neighborCell, out var weldedVertexIds))
                        {
                            continue;
                        }

                        for (var i = 0; i < weldedVertexIds.Count; i++)
                        {
                            var weldedVertexId = weldedVertexIds[i];
                            if ((canonicalPositions[weldedVertexId] - position).sqrMagnitude <= VertexWeldEpsilonSqr)
                            {
                                return weldedVertexId;
                            }
                        }
                    }
                }
            }

            return -1;
        }

        private static void AddWeldedVertexToCell(
            Dictionary<VertexCellKey, List<int>> cells,
            VertexCellKey cell,
            int weldedVertexId)
        {
            if (!cells.TryGetValue(cell, out var weldedVertexIds))
            {
                weldedVertexIds = new List<int>(1);
                cells.Add(cell, weldedVertexIds);
            }

            weldedVertexIds.Add(weldedVertexId);
        }

        private static void ConnectEdge(
            Dictionary<EdgeKey, List<int>> edgeMap,
            List<HashSet<int>> neighborSets,
            EdgeKey edge,
            int nodeId)
        {
            if (!edgeMap.TryGetValue(edge, out var previousNodeIds))
            {
                previousNodeIds = new List<int>(1);
                edgeMap.Add(edge, previousNodeIds);
            }

            for (var i = 0; i < previousNodeIds.Count; i++)
            {
                var neighborId = previousNodeIds[i];
                if (neighborId == nodeId || neighborId < 0 || neighborId >= neighborSets.Count)
                {
                    continue;
                }

                neighborSets[nodeId].Add(neighborId);
                neighborSets[neighborId].Add(nodeId);
            }

            previousNodeIds.Add(nodeId);
        }

        private static bool IsValidTriangle(Vector3[] vertices, int index0, int index1, int index2)
        {
            if (index0 < 0 || index0 >= vertices.Length
                || index1 < 0 || index1 >= vertices.Length
                || index2 < 0 || index2 >= vertices.Length)
            {
                return false;
            }

            if (index0 == index1 || index1 == index2 || index2 == index0)
            {
                return false;
            }

            var edge0 = vertices[index1] - vertices[index0];
            var edge1 = vertices[index2] - vertices[index0];
            return Vector3.Cross(edge0, edge1).sqrMagnitude > DegenerateTriangleAreaSqrEpsilon;
        }

        private static Vector3 GetCentroid(Vector3[] vertices, int index0, int index1, int index2)
        {
            return (vertices[index0] + vertices[index1] + vertices[index2]) / 3f;
        }

        private readonly struct EdgeKey : IEquatable<EdgeKey>
        {
            private readonly int _a;
            private readonly int _b;

            public EdgeKey(int indexA, int indexB)
            {
                if (indexA <= indexB)
                {
                    _a = indexA;
                    _b = indexB;
                }
                else
                {
                    _a = indexB;
                    _b = indexA;
                }
            }

            public bool Equals(EdgeKey other)
            {
                return _a == other._a && _b == other._b;
            }

            public override bool Equals(object obj)
            {
                return obj is EdgeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (_a * 397) ^ _b;
                }
            }
        }

        private readonly struct VertexCellKey : IEquatable<VertexCellKey>
        {
            public VertexCellKey(int x, int y, int z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public int X { get; }
            public int Y { get; }
            public int Z { get; }

            public static VertexCellKey FromPosition(Vector3 position)
            {
                return new VertexCellKey(
                    Mathf.FloorToInt(position.x / VertexWeldEpsilon),
                    Mathf.FloorToInt(position.y / VertexWeldEpsilon),
                    Mathf.FloorToInt(position.z / VertexWeldEpsilon));
            }

            public bool Equals(VertexCellKey other)
            {
                return X == other.X && Y == other.Y && Z == other.Z;
            }

            public override bool Equals(object obj)
            {
                return obj is VertexCellKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = X;
                    hashCode = (hashCode * 397) ^ Y;
                    hashCode = (hashCode * 397) ^ Z;
                    return hashCode;
                }
            }
        }

        private readonly struct NodeBuildData
        {
            public NodeBuildData(
                int id,
                Vector3 position,
                int area,
                int triangleIndex,
                int vertexIndex0,
                int vertexIndex1,
                int vertexIndex2)
            {
                Id = id;
                Position = position;
                Area = area;
                TriangleIndex = triangleIndex;
                VertexIndex0 = vertexIndex0;
                VertexIndex1 = vertexIndex1;
                VertexIndex2 = vertexIndex2;
            }

            public int Id { get; }
            public Vector3 Position { get; }
            public int Area { get; }
            public int TriangleIndex { get; }
            public int VertexIndex0 { get; }
            public int VertexIndex1 { get; }
            public int VertexIndex2 { get; }
        }
    }
}
