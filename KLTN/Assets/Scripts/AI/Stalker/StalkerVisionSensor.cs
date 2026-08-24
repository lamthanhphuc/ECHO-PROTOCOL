using UnityEngine;

namespace EchoProtocol.AI.Stalker
{
    public sealed class StalkerVisionSensor : MonoBehaviour
    {
        [SerializeField] private Transform visionOrigin;
        [SerializeField] private Transform candidate;
        [SerializeField] private float visionDistance = 15f;
        [SerializeField] private float visionAngle = 90f;
        [SerializeField] private LayerMask losBlockerMask = Physics.DefaultRaycastLayers;

        [Header("Debug Runtime")]
        [SerializeField] private bool isCandidateVisible;
        [SerializeField] private Vector3 lastObservedPosition;

        public bool IsCandidateVisible => isCandidateVisible;
        public Vector3 LastObservedPosition => lastObservedPosition;
        public Transform Candidate => candidate;

        private void Update()
        {
            isCandidateVisible = TryGetVisibleCandidate(out lastObservedPosition);
        }

        public bool TryGetVisibleCandidate(out Vector3 observedPosition)
        {
            observedPosition = default;

            if (visionOrigin == null || candidate == null || visionDistance <= 0f || visionAngle <= 0f)
            {
                return false;
            }

            var originPosition = visionOrigin.position;
            var candidatePosition = candidate.position;
            var toCandidate = candidatePosition - originPosition;
            var sqrDistance = toCandidate.sqrMagnitude;
            var maxSqrDistance = visionDistance * visionDistance;

            if (sqrDistance > maxSqrDistance || sqrDistance <= Mathf.Epsilon)
            {
                return false;
            }

            var angleToCandidate = Vector3.Angle(visionOrigin.forward, toCandidate);
            if (angleToCandidate > visionAngle * 0.5f)
            {
                return false;
            }

            var distance = Mathf.Sqrt(sqrDistance);
            if (HasLineOfSightBlocker(originPosition, toCandidate.normalized, distance))
            {
                return false;
            }

            observedPosition = candidatePosition;
            return true;
        }

        private bool HasLineOfSightBlocker(Vector3 originPosition, Vector3 direction, float distance)
        {
            var hits = Physics.RaycastAll(
                originPosition,
                direction,
                distance,
                losBlockerMask,
                QueryTriggerInteraction.Ignore);

            if (hits.Length == 0)
            {
                return false;
            }

            System.Array.Sort(hits, CompareHitDistance);

            for (var i = 0; i < hits.Length; i++)
            {
                var hitTransform = hits[i].transform;
                if (ShouldIgnoreHit(hitTransform))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private bool ShouldIgnoreHit(Transform hitTransform)
        {
            if (hitTransform == null)
            {
                return true;
            }

            if (hitTransform == candidate || hitTransform.IsChildOf(candidate))
            {
                return true;
            }

            return hitTransform == transform || hitTransform.IsChildOf(transform);
        }

        private static int CompareHitDistance(RaycastHit left, RaycastHit right)
        {
            return left.distance.CompareTo(right.distance);
        }

        private void OnDrawGizmosSelected()
        {
            var origin = visionOrigin != null ? visionOrigin : transform;
            var visibleNow = TryGetVisibleCandidate(out var observedPosition);

            Gizmos.color = visibleNow ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(origin.position, visionDistance);

            var halfAngle = visionAngle * 0.5f;
            var leftDirection = Quaternion.AngleAxis(-halfAngle, Vector3.up) * origin.forward;
            var rightDirection = Quaternion.AngleAxis(halfAngle, Vector3.up) * origin.forward;

            Gizmos.DrawLine(origin.position, origin.position + leftDirection.normalized * visionDistance);
            Gizmos.DrawLine(origin.position, origin.position + rightDirection.normalized * visionDistance);

            if (candidate == null)
            {
                return;
            }

            Gizmos.color = visibleNow ? Color.green : Color.red;
            Gizmos.DrawLine(origin.position, candidate.position);

            if (visibleNow)
            {
                Gizmos.DrawWireSphere(observedPosition, 0.25f);
            }
        }
    }
}
