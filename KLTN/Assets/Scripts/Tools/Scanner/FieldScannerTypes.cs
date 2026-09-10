using System;
using UnityEngine;

namespace EchoProtocol.Tools.Scanner
{
    public enum FieldScannerMode
    {
        Core = 0,
        Motion = 1
    }

    public enum ScannerSignalStrength
    {
        None = 0,
        Bar1 = 1,
        Bar2 = 2,
        Bar3 = 3,
        Bar4 = 4
    }

    public enum MotionBlipIntensity
    {
        None = 0,
        Weak = 1,
        Medium = 2,
        Strong = 3,
        Critical = 4
    }

    public enum RelativeDirectionSector
    {
        Front = 0,
        FrontRight = 1,
        Right = 2,
        BackRight = 3,
        Back = 4,
        BackLeft = 5,
        Left = 6,
        FrontLeft = 7
    }

    [Serializable]
    public struct CoreScanResult
    {
        public bool HasTarget;
        public ScannerSignalStrength SignalBars;
        public RelativeDirectionSector Direction;
        public float RawDistance;
        public float SignalScore;
        public int TargetId;

        public static CoreScanResult Empty => new CoreScanResult
        {
            HasTarget = false,
            SignalBars = ScannerSignalStrength.None,
            Direction = RelativeDirectionSector.Front,
            RawDistance = float.MaxValue,
            SignalScore = 0f,
            TargetId = 0
        };
    }

    [Serializable]
    public struct MotionBlip
    {
        public bool IsValid;
        public RelativeDirectionSector Direction;
        public MotionBlipIntensity Intensity;
        public float Distance;
        public int TargetId;

        public static MotionBlip Empty => new MotionBlip
        {
            IsValid = false,
            Direction = RelativeDirectionSector.Front,
            Intensity = MotionBlipIntensity.None,
            Distance = float.MaxValue,
            TargetId = 0
        };
    }

    [Serializable]
    public struct MotionScanResult
    {
        public bool HasMotion;
        public int BlipCount;
        public MotionBlip Blip0;
        public MotionBlip Blip1;
        public MotionBlip Blip2;

        public MotionBlip GetBlip(int index)
        {
            switch (index)
            {
                case 0: return Blip0;
                case 1: return Blip1;
                case 2: return Blip2;
                default: return MotionBlip.Empty;
            }
        }

        public void SetBlip(int index, MotionBlip blip)
        {
            switch (index)
            {
                case 0: Blip0 = blip; break;
                case 1: Blip1 = blip; break;
                case 2: Blip2 = blip; break;
            }
        }

        public static MotionScanResult Empty => new MotionScanResult
        {
            HasMotion = false,
            BlipCount = 0,
            Blip0 = MotionBlip.Empty,
            Blip1 = MotionBlip.Empty,
            Blip2 = MotionBlip.Empty
        };
    }

    [Serializable]
    public sealed class FieldScannerTuning
    {
        // Upgraded gameplay detection ranges: 50m for Core, 35m for Motion
        public float CoreRange = 50f;
        public float MotionRange = 35f;
        public float ActiveDuration = 10.0f; // 10s active realtime scan upon activation
        public float ScanCooldown = 60.0f; // 1 minute reactivation cooldown
        public float ResultLifetime = 10.0f;
        public float MovingSpeedThreshold = 0.15f;
        public float OccludedSignalMultiplier = 0.6f;
        public int MaxMotionTargets = 3;

        // Core range bands (0 - 50m)
        public float CoreBand4MaxDistance = 8f;
        public float CoreBand3MaxDistance = 18f;
        public float CoreBand2MaxDistance = 32f;
        public float CoreBand1MaxDistance = 50f;

        // Motion range bands (0 - 35m)
        public float MotionCriticalMaxDistance = 6f;
        public float MotionStrongMaxDistance = 15f;
        public float MotionMediumMaxDistance = 25f;
        public float MotionWeakMaxDistance = 35f;

        public static FieldScannerTuning Default => new FieldScannerTuning
        {
            CoreRange = 20f,
            MotionRange = 15f,
            ActiveDuration = 10.0f,
            ScanCooldown = 4f,
            ResultLifetime = 2.5f,
            MovingSpeedThreshold = 0.2f,
            OccludedSignalMultiplier = 0.6f,
            MaxMotionTargets = 3,
            CoreBand4MaxDistance = 4f,
            CoreBand3MaxDistance = 8f,
            CoreBand2MaxDistance = 12f,
            CoreBand1MaxDistance = 20f,
            MotionCriticalMaxDistance = 3f,
            MotionStrongMaxDistance = 7f,
            MotionMediumMaxDistance = 12f,
            MotionWeakMaxDistance = 15f
        };
    }
}

