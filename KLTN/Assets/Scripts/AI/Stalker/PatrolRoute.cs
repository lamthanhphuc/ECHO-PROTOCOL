using System.Collections.Generic;
using UnityEngine;

namespace EchoProtocol.AI.Stalker
{
    public sealed class PatrolRoute : MonoBehaviour
    {
        private readonly List<Transform> _orderedPoints = new List<Transform>();
        private int _lastKnownChildCount = -1;

        public int PointCount
        {
            get
            {
                RefreshIfNeeded();
                return _orderedPoints.Count;
            }
        }

        private void Awake()
        {
            RefreshPoints();
        }

        private void OnTransformChildrenChanged()
        {
            RefreshPoints();
        }

        public bool TryGetPoint(int index, out Transform point)
        {
            RefreshIfNeeded();
            point = null;

            if (_orderedPoints.Count == 0)
            {
                return false;
            }

            var wrappedIndex = WrapIndex(index, _orderedPoints.Count);
            point = _orderedPoints[wrappedIndex];
            return point != null;
        }

        public bool TryGetNextValidPoint(int startIndex, out int pointIndex, out Transform point)
        {
            RefreshIfNeeded();
            pointIndex = -1;
            point = null;

            var count = _orderedPoints.Count;
            if (count == 0)
            {
                return false;
            }

            for (var offset = 0; offset < count; offset++)
            {
                var candidateIndex = WrapIndex(startIndex + offset, count);
                var candidate = _orderedPoints[candidateIndex];
                if (candidate == null)
                {
                    continue;
                }

                pointIndex = candidateIndex;
                point = candidate;
                return true;
            }

            return false;
        }

        private void RefreshIfNeeded()
        {
            if (_lastKnownChildCount != transform.childCount)
            {
                RefreshPoints();
            }
        }

        private void RefreshPoints()
        {
            _orderedPoints.Clear();

            for (var i = 0; i < transform.childCount; i++)
            {
                _orderedPoints.Add(transform.GetChild(i));
            }

            _lastKnownChildCount = transform.childCount;
        }

        private static int WrapIndex(int index, int count)
        {
            var wrapped = index % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }
    }
}
