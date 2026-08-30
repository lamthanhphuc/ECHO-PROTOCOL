using System;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Spatial
{
    public sealed class SpatialPatrolMemory
    {
        private readonly CoverageMemory _coverageMemory;

        public SpatialPatrolMemory(int nodeCount)
        {
            _coverageMemory = new CoverageMemory(Mathf.Max(0, nodeCount));
        }

        public SpatialPatrolMemory(CoverageMemory coverageMemory)
        {
            _coverageMemory = coverageMemory ?? throw new ArgumentNullException(nameof(coverageMemory));
        }

        public void MarkVisited(int nodeId, float currentTime)
        {
            if (!IsValidNodeId(nodeId))
            {
                return;
            }

            _coverageMemory.RecordPhysicalNodeArrival(nodeId, currentTime);
        }

        public float GetNormalizedStaleness(int nodeId, float currentTime, float horizon)
        {
            if (!IsValidNodeId(nodeId))
            {
                return 0f;
            }

            if (!_coverageMemory.WasNodeVisited(nodeId))
            {
                return 1f;
            }

            var safeHorizon = Mathf.Max(0.0001f, horizon);
            return Mathf.Clamp01((currentTime - _coverageMemory.GetNodeLastVisitedTime(nodeId)) / safeHorizon);
        }

        private bool IsValidNodeId(int nodeId)
        {
            return nodeId >= 0 && nodeId < _coverageMemory.NodeCount;
        }
    }
}
