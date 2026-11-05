using System;
using UnityEngine;

namespace EchoProtocol.AI.AED
{
    [DisallowMultipleComponent]
    public sealed class AEDV02SafeRoomMarker : MonoBehaviour
    {
        [SerializeField, Range(1, 3)] private int zone = 1;
        [SerializeField] private Vector3 localCenter;
        [SerializeField] private Vector3 localSize = Vector3.one;
        [SerializeField, HideInInspector] private string stableTransformIdentity;

        public int Zone => zone;
        public string StableTransformIdentity => stableTransformIdentity;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(stableTransformIdentity))
                stableTransformIdentity = Guid.NewGuid().ToString("N");
        }

        public bool TryGetWorldBounds(out Vector3 min, out Vector3 max)
        {
            min = max = default;
            if (zone is < 1 or > 3 || string.IsNullOrWhiteSpace(stableTransformIdentity) ||
                !Finite(localCenter) || !Finite(localSize) ||
                localSize.x <= 0 || localSize.y <= 0 || localSize.z <= 0)
                return false;

            var half = localSize * .5f;
            min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        var corner = transform.TransformPoint(localCenter + Vector3.Scale(half, new Vector3(x, y, z)));
                        if (!Finite(corner)) return false;
                        min = Vector3.Min(min, corner);
                        max = Vector3.Max(max, corner);
                    }
            return min.x < max.x && min.y < max.y && min.z < max.z;
        }

        private static bool Finite(Vector3 value) =>

            Finite(value.x) && Finite(value.y) && Finite(value.z);
        private void OnDrawGizmosSelected()
        {
            if (!TryGetWorldBounds(out var min, out var max))
                return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(
                (min + max) * 0.5f,
                max - min
            );
        }
        private static bool Finite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
