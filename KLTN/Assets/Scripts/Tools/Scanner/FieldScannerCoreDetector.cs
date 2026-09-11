using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoProtocol.Tools.Scanner
{
    public interface ICoreScanCandidate
    {
        int TargetId { get; }
        Vector3 WorldPosition { get; }
        bool IsAvailableInWorld { get; }
    }

    public static class FieldScannerCoreDetector
    {
        public static CoreScanResult Evaluate(
            Vector3 origin,
            Vector3 forward,
            IEnumerable<ICoreScanCandidate> candidates,
            FieldScannerTuning tuning = null,
            Func<Vector3, Vector3, bool> isOccludedFunc = null)
        {
            tuning = tuning ?? FieldScannerTuning.Default;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            if (forward == Vector3.zero) forward = Vector3.forward;

            CoreScanResult bestResult = CoreScanResult.Empty;
            float bestScore = -1f;

            if (candidates == null)
            {
                return CoreScanResult.Empty;
            }

            foreach (var candidate in candidates)
            {
                if (candidate == null || !candidate.IsAvailableInWorld)
                {
                    continue;
                }

                Vector3 diff = candidate.WorldPosition - origin;
                float distance = diff.magnitude;
                if (distance > tuning.CoreRange)
                {
                    continue;
                }

                bool isOccluded = false;
                if (isOccludedFunc != null)
                {
                    isOccluded = isOccludedFunc(origin, candidate.WorldPosition);
                }
                else
                {
                    isOccluded = CheckPhysicsOcclusion(origin, candidate.WorldPosition);
                }

                float occlusionMultiplier = isOccluded ? tuning.OccludedSignalMultiplier : 1.0f;
                float distanceAttenuation = Mathf.Clamp01(1f - (distance / tuning.CoreRange));
                float score = distanceAttenuation * occlusionMultiplier;

                // Deterministic comparison
                bool isBetter = false;
                if (score > bestScore + 0.0001f)
                {
                    isBetter = true;
                }
                else if (Mathf.Abs(score - bestScore) <= 0.0001f)
                {
                    // Tie-break: smaller distance
                    if (distance < bestResult.RawDistance - 0.001f)
                    {
                        isBetter = true;
                    }
                    else if (Mathf.Abs(distance - bestResult.RawDistance) <= 0.001f)
                    {
                        // Final deterministic tie-break: smaller TargetId
                        if (candidate.TargetId < bestResult.TargetId)
                        {
                            isBetter = true;
                        }
                    }
                }

                if (isBetter)
                {
                    bestScore = score;
                    bestResult = new CoreScanResult
                    {
                        HasTarget = true,
                        SignalBars = ResolveSignalBars(distance, tuning),
                        Direction = ResolveDirectionSector(forward, diff),
                        RawDistance = distance,
                        SignalScore = score,
                        TargetId = candidate.TargetId
                    };
                }
            }

            return bestResult;
        }

        public static ScannerSignalStrength ResolveSignalBars(float distance, FieldScannerTuning tuning)
        {
            if (distance > tuning.CoreRange || distance < 0f) return ScannerSignalStrength.None;
            if (distance <= tuning.CoreBand4MaxDistance) return ScannerSignalStrength.Bar4;
            if (distance <= tuning.CoreBand3MaxDistance) return ScannerSignalStrength.Bar3;
            if (distance <= tuning.CoreBand2MaxDistance) return ScannerSignalStrength.Bar2;
            return ScannerSignalStrength.Bar1;
        }

        public static RelativeDirectionSector ResolveDirectionSector(Vector3 forward, Vector3 directionToTarget)
        {
            Vector3 flatDir = Vector3.ProjectOnPlane(directionToTarget, Vector3.up).normalized;
            if (flatDir == Vector3.zero) return RelativeDirectionSector.Front;

            float signedAngle = Vector3.SignedAngle(forward, flatDir, Vector3.up);

            // Angle in [-180, 180]
            if (signedAngle >= -22.5f && signedAngle <= 22.5f) return RelativeDirectionSector.Front;
            if (signedAngle > 22.5f && signedAngle <= 67.5f) return RelativeDirectionSector.FrontRight;
            if (signedAngle > 67.5f && signedAngle <= 112.5f) return RelativeDirectionSector.Right;
            if (signedAngle > 112.5f && signedAngle <= 157.5f) return RelativeDirectionSector.BackRight;
            if (signedAngle > 157.5f || signedAngle < -157.5f) return RelativeDirectionSector.Back;
            if (signedAngle >= -157.5f && signedAngle < -112.5f) return RelativeDirectionSector.BackLeft;
            if (signedAngle >= -112.5f && signedAngle < -67.5f) return RelativeDirectionSector.Left;
            return RelativeDirectionSector.FrontLeft;
        }

        private static bool CheckPhysicsOcclusion(Vector3 origin, Vector3 targetPos)
        {
            Vector3 diff = targetPos - origin;
            float dist = diff.magnitude;
            if (dist <= 0.01f) return false;

            Ray ray = new Ray(origin, diff / dist);
            // Ignore triggers and query environment layers (Default layer 0 or environment)
            int layerMask = ~LayerMask.GetMask("Ignore Raycast", "UI");
            if (Physics.Raycast(ray, out RaycastHit hit, dist - 0.1f, layerMask, QueryTriggerInteraction.Ignore))
            {
                // Hit something before reaching target
                return true;
            }

            return false;
        }
    }
}

