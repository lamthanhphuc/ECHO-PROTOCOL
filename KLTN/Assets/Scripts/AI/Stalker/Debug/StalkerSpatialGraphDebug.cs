using EchoProtocol.AI.Stalker.Spatial;
using System.Collections.Generic;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Debugging
{
    public sealed class StalkerSpatialGraphDebug : MonoBehaviour
    {
        [SerializeField] private bool drawNodes = true;
        [SerializeField] private bool drawEdges = true;
        [SerializeField] private float nodeRadius = 0.08f;

        [Header("Debug Runtime")]
        [SerializeField] private int nodeCount;
        [SerializeField] private int edgeCount;
        [SerializeField] private int connectedComponentCount;
        [SerializeField] private int isolatedNodeCount;

        private NavMeshSpatialGraph _graph;

        public int NodeCount => nodeCount;
        public int EdgeCount => edgeCount;
        public int ConnectedComponentCount => connectedComponentCount;
        public int IsolatedNodeCount => isolatedNodeCount;

        private void OnEnable()
        {
            RebuildGraph();
        }

        [ContextMenu("Rebuild Spatial Graph")]
        public void RebuildGraph()
        {
            _graph = NavMeshSpatialGraphBuilder.Build();
            nodeCount = _graph.NodeCount;
            edgeCount = _graph.EdgeCount;
            CalculateConnectivityDiagnostics(_graph, out connectedComponentCount, out isolatedNodeCount);
        }

        private void OnDrawGizmosSelected()
        {
            if (_graph == null || _graph.IsEmpty)
            {
                return;
            }

            var radius = Mathf.Max(0.001f, nodeRadius);

            if (drawEdges)
            {
                Gizmos.color = Color.cyan;
                DrawEdges();
            }

            if (drawNodes)
            {
                Gizmos.color = Color.magenta;
                DrawNodes(radius);
            }
        }

        private void DrawNodes(float radius)
        {
            var nodes = _graph.Nodes;
            for (var i = 0; i < nodes.Count; i++)
            {
                Gizmos.DrawSphere(nodes[i].Position, radius);
            }
        }

        private void DrawEdges()
        {
            var nodes = _graph.Nodes;
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                var neighbors = node.NeighborIds;

                for (var neighborIndex = 0; neighborIndex < neighbors.Count; neighborIndex++)
                {
                    var neighborId = neighbors[neighborIndex];
                    if (neighborId <= node.Id || !_graph.TryGetNode(neighborId, out var neighbor))
                    {
                        continue;
                    }

                    Gizmos.DrawLine(node.Position, neighbor.Position);
                }
            }
        }

        private static void CalculateConnectivityDiagnostics(
            NavMeshSpatialGraph graph,
            out int componentCount,
            out int isolatedCount)
        {
            componentCount = 0;
            isolatedCount = 0;

            if (graph == null || graph.IsEmpty)
            {
                return;
            }

            var visited = new bool[graph.NodeCount];
            var stack = new Stack<int>();
            var nodes = graph.Nodes;

            for (var nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
            {
                if (visited[nodeIndex])
                {
                    continue;
                }

                componentCount++;

                if (nodes[nodeIndex].NeighborIds.Count == 0)
                {
                    isolatedCount++;
                }

                visited[nodeIndex] = true;
                stack.Push(nodeIndex);

                while (stack.Count > 0)
                {
                    var currentNodeId = stack.Pop();
                    if (!graph.TryGetNode(currentNodeId, out var currentNode))
                    {
                        continue;
                    }

                    var neighbors = currentNode.NeighborIds;
                    for (var neighborIndex = 0; neighborIndex < neighbors.Count; neighborIndex++)
                    {
                        var neighborId = neighbors[neighborIndex];
                        if (neighborId < 0 || neighborId >= visited.Length || visited[neighborId])
                        {
                            continue;
                        }

                        visited[neighborId] = true;
                        stack.Push(neighborId);
                    }
                }
            }
        }
    }
}
