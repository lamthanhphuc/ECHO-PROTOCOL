using System;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Special
{
    [Serializable]
    public sealed class StalkerSpecialEncounterSettings
    {
        [SerializeField] private bool enabled = true;

        [SerializeField] private float cooldownSeconds = 600f;
        [SerializeField] private float failedAttemptBackoffSeconds = 15f;

        [SerializeField] private float isolationRadius = 10f;

        [SerializeField] private float sniffApproachDistance = 2f;
        [SerializeField] private float sniffApproachSampleRadius = 1.5f;
        [SerializeField] private float stagingArrivalTolerance = 0.35f;

        [SerializeField] private float specialSniffDurationSeconds = 1.5f;
        [SerializeField] private float jumpOutDurationSeconds = 1.0f;

        //
        // Hidden transfer is now distance based.
        //
        // Total hidden time:
        //
        // baseDelay + distance / virtualTravelSpeed
        //
        // then clamped between min/max.
        //
        [SerializeField] private float hiddenTransferDelaySeconds = 0.05f;
        [SerializeField] private float hiddenTransferVirtualSpeed = 8f;
        [SerializeField] private float hiddenTransferMinSeconds = 1f;
        [SerializeField] private float hiddenTransferMaxSeconds = 8f;

        [SerializeField] private float jumpInDurationSeconds = 1.0f;
        [SerializeField] private float reactionLockSeconds = 1.5f;

        [SerializeField] private int minimumOtherAlivePlayers = 2;

        [SerializeField] private float jumpInMinDistance = 7f;
        [SerializeField] private float jumpInMaxDistance = 10f;
        [SerializeField] private float preferredJumpInDistance = 8.5f;

        [SerializeField] private float frontFacingDotThreshold = 0.5f;

        [SerializeField] private float dynamicArcHalfAngleDegrees = 60f;
        [SerializeField] private int dynamicArcSampleCount = 7;
        [SerializeField] private float dynamicNavMeshSampleRadius = 2f;

        [SerializeField] private float minimumEntryReuseSeconds = 120f;
        [SerializeField] private float groupRadius = 8f;
        [SerializeField] private float candidateLosHeight = 1.2f;

        [Header("Shared pacing / fairness")]
        [SerializeField, Range(0f, 1f)] private float maxDirectorPressureForStart = 1f;
        [SerializeField] private float postSpecialDirectorCooldownSeconds = 20f;
        [SerializeField] private float recentPlayerPressureWindowSeconds = 45f;
        [SerializeField] private float recentPlayerPressurePenalty = 3f;
        [SerializeField] private bool requireEscapeRoute = true;
        [SerializeField] private float escapeProbeDistance = 4f;
        [SerializeField] private float escapeNavMeshSampleRadius = 1.5f;
        [SerializeField] private float minimumEscapeDistanceGain = 1.25f;

        [SerializeField] private bool allowDynamicEntryFallback = true;

        public bool Enabled =>
            enabled;

        public float CooldownSeconds =>
            Mathf.Max(
                0f,
                cooldownSeconds);

        public float FailedAttemptBackoffSeconds =>
            Mathf.Max(
                0f,
                failedAttemptBackoffSeconds);

        public float IsolationRadius =>
            Mathf.Max(
                0f,
                isolationRadius);

        public float SniffApproachDistance =>
            Mathf.Max(
                0f,
                sniffApproachDistance);

        public float SniffApproachSampleRadius =>
            Mathf.Max(
                0.1f,
                sniffApproachSampleRadius);

        public float StagingArrivalTolerance =>
            Mathf.Max(
                0.01f,
                stagingArrivalTolerance);

        public float SpecialSniffDurationSeconds =>
            Mathf.Max(
                0f,
                specialSniffDurationSeconds);

        public float JumpOutDurationSeconds =>
            Mathf.Max(
                0f,
                jumpOutDurationSeconds);

        //
        // Kept for compatibility with existing serialized prefab data.
        //
        public float HiddenTransferDelaySeconds =>
            Mathf.Max(
                0f,
                hiddenTransferDelaySeconds);

        public float HiddenTransferVirtualSpeed =>
            Mathf.Max(
                0.1f,
                hiddenTransferVirtualSpeed);

        public float HiddenTransferMinSeconds =>
            Mathf.Max(
                0f,
                hiddenTransferMinSeconds);

        public float HiddenTransferMaxSeconds =>
            Mathf.Max(
                HiddenTransferMinSeconds,
                hiddenTransferMaxSeconds);

        public float JumpInDurationSeconds =>
            Mathf.Max(
                0f,
                jumpInDurationSeconds);

        public float ReactionLockSeconds =>
            Mathf.Max(
                0f,
                reactionLockSeconds);

        public int MinimumOtherAlivePlayers =>
            Mathf.Max(
                0,
                minimumOtherAlivePlayers);

        public float JumpInMinDistance =>
            Mathf.Max(
                0f,
                jumpInMinDistance);

        public float JumpInMaxDistance =>
            Mathf.Max(
                JumpInMinDistance,
                jumpInMaxDistance);

        public float PreferredJumpInDistance =>
            Mathf.Clamp(
                preferredJumpInDistance,
                JumpInMinDistance,
                JumpInMaxDistance);

        public float FrontFacingDotThreshold =>
            Mathf.Clamp(
                frontFacingDotThreshold,
                -1f,
                1f);

        public float DynamicArcHalfAngleDegrees =>
            Mathf.Clamp(
                dynamicArcHalfAngleDegrees,
                0f,
                180f);

        public int DynamicArcSampleCount =>
            Mathf.Max(
                1,
                dynamicArcSampleCount);

        public float DynamicNavMeshSampleRadius =>
            Mathf.Max(
                0.1f,
                dynamicNavMeshSampleRadius);

        public float MinimumEntryReuseSeconds =>
            Mathf.Max(
                0f,
                minimumEntryReuseSeconds);

        public float GroupRadius =>
            Mathf.Max(
                0f,
                groupRadius);

        public float CandidateLosHeight =>
            Mathf.Max(
                0f,
                candidateLosHeight);

        public float MaxDirectorPressureForStart =>
            Mathf.Clamp01(maxDirectorPressureForStart);

        public float PostSpecialDirectorCooldownSeconds =>
            Mathf.Max(0f, postSpecialDirectorCooldownSeconds);

        public float RecentPlayerPressureWindowSeconds =>
            Mathf.Max(0.1f, recentPlayerPressureWindowSeconds);

        public float RecentPlayerPressurePenalty =>
            Mathf.Max(0f, recentPlayerPressurePenalty);

        public bool RequireEscapeRoute => requireEscapeRoute;

        public float EscapeProbeDistance =>
            Mathf.Max(0.5f, escapeProbeDistance);

        public float EscapeNavMeshSampleRadius =>
            Mathf.Max(0.1f, escapeNavMeshSampleRadius);

        public float MinimumEscapeDistanceGain =>
            Mathf.Max(0f, minimumEscapeDistanceGain);

        public bool AllowDynamicEntryFallback =>
            allowDynamicEntryFallback;
    }
}
