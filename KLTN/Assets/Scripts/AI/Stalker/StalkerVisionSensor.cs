using System;
using System.Collections.Generic;
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
        private Transform temporaryCandidate;
        private UnityEngine.Object temporaryCandidateOwner;

        public bool IsCandidateVisible => isCandidateVisible;
        public Vector3 LastObservedPosition => lastObservedPosition;
        public Transform Candidate => temporaryCandidate != null ? temporaryCandidate : candidate;

        public bool TrySetTemporaryCandidate(Transform target, UnityEngine.Object owner)
        {
            if (target == null || owner == null) return false;
            if (temporaryCandidateOwner != null && temporaryCandidateOwner != owner) return false;
            temporaryCandidate = target;
            temporaryCandidateOwner = owner;
            return true;
        }

        public void ClearTemporaryCandidate(UnityEngine.Object owner)
        {
            if (owner == null || temporaryCandidateOwner != owner) return;
            temporaryCandidate = null;
            temporaryCandidateOwner = null;
        }

        private void Update()
        {
            RefreshVisibility();
        }

        public bool RefreshVisibility()
        {
            isCandidateVisible = TryGetVisibleCandidate(out lastObservedPosition);
            return isCandidateVisible;
        }

        public bool TryGetVisibleCandidate(out Vector3 observedPosition)
        {
            observedPosition = default;

            if (!TryEvaluateCandidate(Candidate, out var observation))
            {
                return false;
            }

            observedPosition = observation.ObservedPosition;
            return true;
        }

        public bool TryEvaluateCandidate(
            Transform targetCandidate,
            out StalkerPhysicalVisionObservation observation)
        {
            return TryEvaluateCandidate(targetCandidate, targetCandidate, out observation);
        }

        public bool TryEvaluateCandidate(
            Transform targetSample,
            Transform targetHierarchyRoot,
            out StalkerPhysicalVisionObservation observation)
        {
            observation = default;

            if (visionOrigin == null
                || targetSample == null
                || targetHierarchyRoot == null
                || visionDistance <= 0f
                || visionAngle <= 0f)
            {
                return false;
            }

            if (targetSample != targetHierarchyRoot && !targetSample.IsChildOf(targetHierarchyRoot))
            {
                return false;
            }

            var originPosition = visionOrigin.position;
            var candidatePosition = targetSample.position;
            if (!TryGetVisibleDirection(originPosition, candidatePosition, false, out var observedDirection, out var distance))
            {
                return false;
            }

            if (HasLineOfSightBlocker(targetHierarchyRoot, originPosition, observedDirection, distance))
            {
                return false;
            }

            observation = new StalkerPhysicalVisionObservation(
                targetSample,
                candidatePosition,
                observedDirection,
                distance);
            return true;
        }

        public bool CanSeePoint(Vector3 worldPoint)
        {
            if (visionOrigin == null || visionDistance <= 0f || visionAngle <= 0f)
            {
                return false;
            }

            var originPosition = visionOrigin.position;
            if (!TryGetVisibleDirection(originPosition, worldPoint, true, out var direction, out var distance))
            {
                return false;
            }

            return distance <= Mathf.Epsilon || !HasLineOfSightBlocker(null, originPosition, direction, distance);
        }

        public Vector3 GetObservationPointForGroundPoint(Vector3 groundPoint)
        {
            if (visionOrigin == null)
            {
                return groundPoint;
            }

            var eyeHeight = visionOrigin.position.y - transform.position.y;
            return groundPoint + Vector3.up * eyeHeight;
        }

        public int CollectVisibleCandidates(
            IReadOnlyList<Transform> candidates,
            List<StalkerPhysicalVisionObservation> results)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();

            for (var i = 0; i < candidates.Count; i++)
            {
                var targetCandidate = candidates[i];
                if (targetCandidate == null)
                {
                    continue;
                }

                if (TryEvaluateCandidate(targetCandidate, out var observation))
                {
                    results.Add(observation);
                }
            }

            return results.Count;
        }

        private bool TryGetVisibleDirection(
            Vector3 originPosition,
            Vector3 targetPosition,
            bool allowOriginPoint,
            out Vector3 direction,
            out float distance)
        {
            direction = default;
            distance = 0f;

            var toTarget = targetPosition - originPosition;
            var sqrDistance = toTarget.sqrMagnitude;
            var maxSqrDistance = visionDistance * visionDistance;

            if (sqrDistance > maxSqrDistance)
            {
                return false;
            }

            if (sqrDistance <= Mathf.Epsilon)
            {
                return allowOriginPoint;
            }

            var angleToTarget = Vector3.Angle(visionOrigin.forward, toTarget);
            if (angleToTarget > visionAngle * 0.5f)
            {
                return false;
            }

            distance = Mathf.Sqrt(sqrDistance);
            direction = toTarget.normalized;
            return true;
        }

        private bool HasLineOfSightBlocker(
            Transform targetHierarchyRoot,
            Vector3 originPosition,
            Vector3 direction,
            float distance)
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
                if (ShouldIgnoreHit(hitTransform, targetHierarchyRoot))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private bool ShouldIgnoreHit(Transform hitTransform, Transform targetHierarchyRoot)
        {
            if (hitTransform == null)
            {
                return true;
            }

            if (targetHierarchyRoot != null
                && (hitTransform == targetHierarchyRoot || hitTransform.IsChildOf(targetHierarchyRoot)))
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

            if (Candidate == null)
            {
                return;
            }

            Gizmos.color = visibleNow ? Color.green : Color.red;
            Gizmos.DrawLine(origin.position, Candidate.position);

            if (visibleNow)
            {
                Gizmos.DrawWireSphere(observedPosition, 0.25f);
            }
        }
    }
}
