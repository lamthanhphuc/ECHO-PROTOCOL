using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoProtocol.Tools.Scanner
{
    public static class FieldScannerMotionDetector
    {
        private struct CandidateMatch : IComparable<CandidateMatch>
        {
            public IMotionScannable Target;
            public float Distance;
            public RelativeDirectionSector Direction;
            public MotionBlipIntensity Intensity;

            public int CompareTo(CandidateMatch other)
            {
                // Nearest distance first
                int cmp = Distance.CompareTo(other.Distance);
                if (cmp != 0) return cmp;
                // Deterministic tie-break
                int idA = Target != null ? Target.TargetId : 0;
                int idB = other.Target != null ? other.Target.TargetId : 0;
                return idA.CompareTo(idB);
            }
        }

        public static MotionScanResult Evaluate(
            Vector3 origin,
            Vector3 forward,
            IEnumerable<IMotionScannable> targets,
            FieldScannerTuning tuning = null)
        {
            tuning = tuning ?? FieldScannerTuning.Default;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            if (forward == Vector3.zero) forward = Vector3.forward;

            if (targets == null)
            {
                return MotionScanResult.Empty;
            }

            List<CandidateMatch> validMatches = new List<CandidateMatch>();

            foreach (var target in targets)
            {
                if (target == null || !target.IsActiveTarget)
                {
                    continue;
                }

                if (target.CurrentSpeed < tuning.MovingSpeedThreshold)
                {
                    // Stationary or nearly stationary - ignore
                    continue;
                }

                Vector3 diff = target.WorldPosition - origin;
                float distance = diff.magnitude;
                if (distance > tuning.MotionRange)
                {
                    // Outside range
                    continue;
                }

                validMatches.Add(new CandidateMatch
                {
                    Target = target,
                    Distance = distance,
                    Direction = FieldScannerCoreDetector.ResolveDirectionSector(forward, diff),
                    Intensity = ResolveIntensity(distance, tuning)
                });
            }

            if (validMatches.Count == 0)
            {
                return MotionScanResult.Empty;
            }

            validMatches.Sort();

            MotionScanResult result = new MotionScanResult
            {
                HasMotion = true,
                BlipCount = Mathf.Min(validMatches.Count, tuning.MaxMotionTargets)
            };

            for (int i = 0; i < result.BlipCount; i++)
            {
                var match = validMatches[i];
                result.SetBlip(i, new MotionBlip
                {
                    IsValid = true,
                    Direction = match.Direction,
                    Intensity = match.Intensity,
                    Distance = match.Distance,
                    TargetId = match.Target != null ? match.Target.TargetId : 0
                });
            }

            return result;
        }

        public static MotionBlipIntensity ResolveIntensity(float distance, FieldScannerTuning tuning)
        {
            if (distance > tuning.MotionRange || distance < 0f) return MotionBlipIntensity.None;
            if (distance <= tuning.MotionCriticalMaxDistance) return MotionBlipIntensity.Critical;
            if (distance <= tuning.MotionStrongMaxDistance) return MotionBlipIntensity.Strong;
            if (distance <= tuning.MotionMediumMaxDistance) return MotionBlipIntensity.Medium;
            return MotionBlipIntensity.Weak;
        }
    }
}

