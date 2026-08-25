using UnityEngine;

namespace EchoProtocol.AI.Stalker.Spatial
{
    public sealed class SpatialPatrolMemory
    {
        private readonly float[] _lastVisitedTimes;
        private readonly bool[] _visited;

        public SpatialPatrolMemory(int nodeCount)
        {
            var safeNodeCount = Mathf.Max(0, nodeCount);
            _lastVisitedTimes = new float[safeNodeCount];
            _visited = new bool[safeNodeCount];
        }

        public void MarkVisited(int nodeId, float currentTime)
        {
            if (!IsValidNodeId(nodeId))
            {
                return;
            }

            _lastVisitedTimes[nodeId] = currentTime;
            _visited[nodeId] = true;
        }

        public float GetNormalizedStaleness(int nodeId, float currentTime, float horizon)
        {
            if (!IsValidNodeId(nodeId))
            {
                return 0f;
            }

            if (!_visited[nodeId])
            {
                return 1f;
            }

            var safeHorizon = Mathf.Max(0.0001f, horizon);
            return Mathf.Clamp01((currentTime - _lastVisitedTimes[nodeId]) / safeHorizon);
        }

        private bool IsValidNodeId(int nodeId)
        {
            return nodeId >= 0 && nodeId < _lastVisitedTimes.Length;
        }
    }
}
