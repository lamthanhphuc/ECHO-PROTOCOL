using System;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Spatial.Strategic
{
    [Serializable]
    public sealed class StalkerSmartPatrolSettings
    {
        [SerializeField, Min(0.1f)] private float occupancySampleIntervalSeconds = 0.5f;
        [SerializeField, Min(0f)] private float occupancyInformationDelaySeconds = 1.5f;

        [SerializeField, Range(0f, 1f)] private float playerDensityWeight = 0.35f;
        [SerializeField, Range(0f, 1f)] private float lingerWeight = 0.10f;
        [SerializeField, Range(0f, 1f)] private float noiseWeight = 0.25f;
        [SerializeField, Range(0f, 1f)] private float objectiveWeight = 0.10f;
        [SerializeField, Range(0f, 1f)] private float lastKnownWeight = 0.20f;

        [SerializeField, Min(0.01f)] private float densitySaturation = 1.25f;
        [SerializeField, Range(0f, 1f)] private float hiddenPlayerInfluenceMultiplier = 0.25f;
        [SerializeField, Min(0.01f)] private float lingerFullSeconds = 25f;

        [SerializeField, Min(0.01f)] private float noiseHalfLifeSeconds = 8f;
        [SerializeField, Min(0.01f)] private float lastKnownHalfLifeSeconds = 10f;
        [SerializeField, Min(0.01f)] private float objectiveHalfLifeSeconds = 15f;
        [SerializeField, Range(0f, 1f)] private float ring1Propagation = 0.55f;
        [SerializeField, Range(0f, 1f)] private float ring2Propagation = 0.25f;

        [SerializeField, Min(0.01f)] private float explorationFullTimeSeconds = 45f;
        [SerializeField, Range(0f, 1f)] private float recentVisitPenalty = 0.30f;
        [SerializeField, Min(0.01f)] private float recentVisitDecaySeconds = 15f;
        [SerializeField, Range(0f, 1f)] private float repeatVisitPenalty = 0.15f;
        [SerializeField, Range(0f, 1f)] private float recentPressurePenalty = 0.25f;
        [SerializeField, Min(0f)] private float distancePenaltyPerHop = 0.04f;

        [SerializeField, Min(0.01f)] private float routeEfficiencyFullHopCount = 6f;
        [SerializeField, Min(0.01f)] private float recentRepeatNormalizationCount = 3f;

        [SerializeField, Min(1)] private int topK = 3;
        [SerializeField, Min(0.01f)] private float softmaxTemperature = 0.20f;
        [SerializeField, Range(0f, 1f)] private float explorationProbability = 0.08f;

        [SerializeField, Min(0)] private int minimumPeripheralSweepsBeforeHotRoom = 1;
        [SerializeField, Min(0f)] private float maxPeripheralApproachSeconds = 12f;

        [SerializeField, Range(0f, 1f)] private float pressureBuildThreshold = 0.30f;
        [SerializeField, Range(0f, 1f)] private float pressureModeThreshold = 0.85f;
        [SerializeField, Min(0.01f)] private float pressureBuildSeconds = 30f;
        [SerializeField, Min(0.01f)] private float pressureDecaySeconds = 10f;

        [SerializeField, Min(0f)] private float postChaseCooldownSeconds = 18f;
        [SerializeField, Min(0f)] private float postAttackCooldownSeconds = 22f;
        [SerializeField, Min(0.01f)] private float sameRoomPressureCooldownSeconds = 15f;

        public float OccupancySampleIntervalSeconds => Mathf.Max(0.1f, occupancySampleIntervalSeconds);
        public float OccupancyInformationDelaySeconds => Mathf.Max(0f, occupancyInformationDelaySeconds);
        public float PlayerDensityWeight => Mathf.Clamp01(playerDensityWeight);
        public float LingerWeight => Mathf.Clamp01(lingerWeight);
        public float NoiseWeight => Mathf.Clamp01(noiseWeight);
        public float ObjectiveWeight => Mathf.Clamp01(objectiveWeight);
        public float LastKnownWeight => Mathf.Clamp01(lastKnownWeight);
        public float DensitySaturation => Mathf.Max(0.01f, densitySaturation);
        public float HiddenPlayerInfluenceMultiplier => Mathf.Clamp01(hiddenPlayerInfluenceMultiplier);
        public float LingerFullSeconds => Mathf.Max(0.01f, lingerFullSeconds);
        public float NoiseHalfLifeSeconds => Mathf.Max(0.01f, noiseHalfLifeSeconds);
        public float LastKnownHalfLifeSeconds => Mathf.Max(0.01f, lastKnownHalfLifeSeconds);
        public float ObjectiveHalfLifeSeconds => Mathf.Max(0.01f, objectiveHalfLifeSeconds);
        public float Ring1Propagation => Mathf.Clamp01(ring1Propagation);
        public float Ring2Propagation => Mathf.Clamp01(ring2Propagation);
        public float ExplorationFullTimeSeconds => Mathf.Max(0.01f, explorationFullTimeSeconds);
        public float RecentVisitPenalty => Mathf.Clamp01(recentVisitPenalty);
        public float RecentVisitDecaySeconds => Mathf.Max(0.01f, recentVisitDecaySeconds);
        public float RepeatVisitPenalty => Mathf.Clamp01(repeatVisitPenalty);
        public float RecentPressurePenalty => Mathf.Clamp01(recentPressurePenalty);
        public float DistancePenaltyPerHop => Mathf.Max(0f, distancePenaltyPerHop);
        public float RouteEfficiencyFullHopCount => Mathf.Max(0.01f, routeEfficiencyFullHopCount);
        public float RecentRepeatNormalizationCount => Mathf.Max(0.01f, recentRepeatNormalizationCount);
        public int TopK => Math.Max(1, topK);
        public float SoftmaxTemperature => Mathf.Max(0.01f, softmaxTemperature);
        public float ExplorationProbability => Mathf.Clamp01(explorationProbability);
        public int MinimumPeripheralSweepsBeforeHotRoom => Math.Max(0, minimumPeripheralSweepsBeforeHotRoom);
        public float MaxPeripheralApproachSeconds => Mathf.Max(0f, maxPeripheralApproachSeconds);
        public float PressureBuildThreshold => Mathf.Clamp01(pressureBuildThreshold);
        public float PressureModeThreshold => Mathf.Clamp01(pressureModeThreshold);
        public float PressureBuildSeconds => Mathf.Max(0.01f, pressureBuildSeconds);
        public float PressureDecaySeconds => Mathf.Max(0.01f, pressureDecaySeconds);
        public float PostChaseCooldownSeconds => Mathf.Max(0f, postChaseCooldownSeconds);
        public float PostAttackCooldownSeconds => Mathf.Max(0f, postAttackCooldownSeconds);
        public float SameRoomPressureCooldownSeconds => Mathf.Max(0.01f, sameRoomPressureCooldownSeconds);
    }
}
