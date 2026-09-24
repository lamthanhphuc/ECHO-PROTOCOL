using System.Collections.Generic;
using EchoProtocol.AI.Common;
using EchoProtocol.AI.Stalker.Networking;
using EchoProtocol.Diagnostics;
using EchoProtocol.Networking;
using EchoProtocol.Player;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

namespace EchoProtocol.AI.Stalker.Special
{
    [DisallowMultipleComponent]
    public sealed class StalkerSpecialEncounterRuntime : MonoBehaviour
    {
        [SerializeField] private StalkerSpecialEncounterSettings settings = new StalkerSpecialEncounterSettings();
        [SerializeField] private StalkerController controller;
        [SerializeField] private NetworkTransform networkTransform;
        [SerializeField] private NavMeshAgent navMeshAgent;
        [SerializeField] private StalkerJumpEntryRegistry jumpEntryRegistry;

        private readonly List<Transform> _eligibleAlivePlayerRoots = new List<Transform>();
        private readonly Dictionary<Transform, float> _recentPressureByPlayerRoot =
            new Dictionary<Transform, float>();
        private StalkerDownedPlayerFact _pendingFact;
        private StalkerSpecialEncounterPhase _phase;
        private AiSimulationTime _cooldownUntil = AiSimulationTime.Invalid;
        private float _phaseElapsed;
        private uint _sequenceOrdinal;

        private Vector3 _jumpInPosition;
        private Vector3? _lastJumpInPosition;

        //
        // Duration is calculated independently for every
        // Special Encounter based on the current distance
        // to the remaining alive Players.
        //
        private float _hiddenTransferDurationSeconds;

        private Vector3 _downedPlayerPosition;
        private Vector3 _downedPlayerForward;

        private bool _presentationVisible = true;
        private bool _ownsControllerOverride;
        private bool _returnToPatrolAfterJumpIn;

        public bool IsActive => _phase != StalkerSpecialEncounterPhase.None;
        public StalkerSpecialEncounterPhase Phase => _phase;
        public float PhaseElapsed => _phaseElapsed;
        public uint SequenceOrdinal => _sequenceOrdinal;
        public bool PresentationVisible => _presentationVisible;
        public float PhaseProgress01 => GetPhaseDuration(_phase) <= 0f ? 1f : Mathf.Clamp01(_phaseElapsed / GetPhaseDuration(_phase));

        private void Awake()
        {
            ResolveDependencies();
        }

        public void Arm(StalkerDownedPlayerFact fact)
        {
            if (!fact.IsValid || IsActive)
            {
                return;
            }

            ResolveDependencies();

            if (settings == null
                || !settings.Enabled
                || controller == null
                || IsCoolingDown(fact.ResolvedAt))
            {
                return;
            }

            if (!controller.CanStartSpecialEncounter(
                    settings.MaxDirectorPressureForStart))
            {
                RuntimeLog.Log(
                    RuntimeLogCategory.StalkerCombat,
                    "[STK_SPECIAL][SKIP] reason=director-pacing");

                return;
            }

            _pendingFact = fact;

            RuntimeLog.Log(
                RuntimeLogCategory.StalkerCombat,
                "[STK_SPECIAL][ARM]");
        }

        public void TickAuthoritative(
            NetworkRunner runner,
            FusionPlayerLifecycle lifecycle,
            AiSimulationStep step)
        {
            ResolveDependencies();
            if (settings == null || controller == null || !step.IsValid)
            {
                return;
            }

            if (!settings.Enabled)
            {
                if (IsActive)
                {
                    Abort(
                        "special-disabled",
                        step.Time,
                        settings.FailedAttemptBackoffSeconds);
                }

                return;
            }

            if (!IsActive)
            {
                TryBegin(runner, lifecycle, step);
                return;
            }

            _phaseElapsed += step.DeltaSeconds;
            switch (_phase)
            {
                case StalkerSpecialEncounterPhase.ApproachDownedPlayer:
                    if (lifecycle != null
                        && !IsPendingVictimStillDowned(lifecycle))
                    {
                        Abort(
                            "downed-player-revived",
                            step.Time,
                            settings.FailedAttemptBackoffSeconds);

                        break;
                    }

                    controller.FaceSpecialEncounterPoint(
                        _downedPlayerPosition,
                        720f * step.DeltaSeconds);

                    if (controller.HasArrivedAtSpecialEncounterDestination(
                            settings.StagingArrivalTolerance))
                    {
                        controller.StopSpecialEncounterNavigation();

                        controller.FaceSpecialEncounterPoint(
                            _downedPlayerPosition,
                            180f);

                        SetPhase(
                            StalkerSpecialEncounterPhase.Sniff);
                    }
                    else if (_phaseElapsed >= settings.ApproachTimeoutSeconds)
                    {
                        Abort(
                            "approach-timeout",
                            step.Time,
                            settings.FailedAttemptBackoffSeconds);
                    }

                    break;

                case StalkerSpecialEncounterPhase.Sniff:
                    if (lifecycle != null
                        && !IsPendingVictimStillDowned(lifecycle))
                    {
                        Abort(
                            "downed-player-revived",
                            step.Time,
                            settings.FailedAttemptBackoffSeconds);

                        break;
                    }

                    controller.FaceSpecialEncounterPoint(
                        _downedPlayerPosition,
                        720f * step.DeltaSeconds);

                    if (_phaseElapsed
                        >= settings.SpecialSniffDurationSeconds)
                    {
                        SetPhase(
                            StalkerSpecialEncounterPhase.JumpOut);
                    }

                    break;
                case StalkerSpecialEncounterPhase.JumpOut:
                    if (_phaseElapsed
                        >= settings.JumpOutDurationSeconds)
                    {
                        //
                        // The monster disappears first.
                        //
                        _presentationVisible =
                            false;

                        //
                        // Calculate a virtual whole-map travel time from
                        // the Stalker's CURRENT position to the nearest
                        // eligible alive Player.
                        //
                        _hiddenTransferDurationSeconds =
                            ResolveHiddenTransferDuration(
                                out var travelDistance);

                        RuntimeLog.Log(
                            RuntimeLogCategory.StalkerCombat,
                            $"[STK_SPECIAL][TRANSFER_BEGIN] " +
                            $"distance={travelDistance:F1}m " +
                            $"duration={_hiddenTransferDurationSeconds:F2}s");

                        SetPhase(
                            StalkerSpecialEncounterPhase
                                .HiddenTransfer);
                    }

                    break;
                case StalkerSpecialEncounterPhase.HiddenTransfer:
                    if (_phaseElapsed
                        >= _hiddenTransferDurationSeconds)
                    {
                        //
                        // Players may have moved, died, disconnected,
                        // or changed rooms while the Stalker was hidden.
                        //
                        // Rebuild the authoritative alive-player list now.
                        //
                        if (!RefreshEligibleAlivePlayerRoots(
                                lifecycle,
                                _pendingFact.PlayerId,
                                step.Time))
                        {
                            AbortHiddenTransfer(
                                "missing-player-registry-after-transfer",
                                step.Time);

                            break;
                        }

                        if (_eligibleAlivePlayerRoots.Count
                            < settings.MinimumOtherAlivePlayers)
                        {
                            AbortHiddenTransfer(
                                "not-enough-alive-players-after-transfer",
                                step.Time);

                            break;
                        }

                        //
                        // IMPORTANT:
                        // Do NOT use a Jump-In position selected several
                        // seconds ago.
                        //
                        // Resolve a fresh entry around the Players'
                        // CURRENT positions.
                        //
                        if (!StalkerJumpEntrySelector.TrySelectWithFairness(
                                jumpEntryRegistry,
                                _eligibleAlivePlayerRoots,
                                _recentPressureByPlayerRoot,
                                _pendingFact.PlayerId,
                                step.Time,
                                settings,
                                _lastJumpInPosition,
                                out _jumpInPosition))
                        {
                            AbortHiddenTransfer(
                                "no-valid-entry-after-transfer",
                                step.Time);

                            break;
                        }

                        if (!TryTeleportToJumpIn())
                        {
                            AbortHiddenTransfer(
                                "jump-transfer-failed",
                                step.Time);

                            break;
                        }

                        _presentationVisible =
                            true;

                        SetPhase(
                            StalkerSpecialEncounterPhase
                                .JumpIn);
                    }

                    break;
                case StalkerSpecialEncounterPhase.JumpIn:
                    if (_phaseElapsed >= settings.JumpInDurationSeconds)
                    {
                        if (_returnToPatrolAfterJumpIn)
                        {
                            Cleanup();
                        }
                        else
                        {
                            SetPhase(StalkerSpecialEncounterPhase.ReactionLock);
                        }
                    }
                    break;
                case StalkerSpecialEncounterPhase.ReactionLock:
                    if (_phaseElapsed >= settings.ReactionLockSeconds)
                    {
                        Complete(step.Time);
                    }
                    break;
            }
        }

        private void TryBegin(
            NetworkRunner runner,
            FusionPlayerLifecycle lifecycle,
            AiSimulationStep step)
        {
            if (!_pendingFact.IsValid
                || runner == null
                || lifecycle == null)
            {
                return;
            }

            //
            // The sequence may take ownership only from the attack
            // RECOVER boundary.
            //
            // ATTACK is allowed to finish naturally if execution order
            // ever reaches this point before EnterRecover().
            //
            if (controller.CurrentState != StalkerState.RECOVER)
            {
                if (controller.CurrentState != StalkerState.ATTACK)
                {
                    _pendingFact = default;
                }

                return;
            }

            var entityRegistry =
                lifecycle.EntityRegistry;

            if (entityRegistry == null)
            {
                Abort(
                    "missing-player-registry",
                    step.Time,
                    settings.FailedAttemptBackoffSeconds);

                return;
            }

            //
            // Revalidate the actual victim at execution time.
            //
            // The Down fact may have been valid when the Bite landed,
            // but the sequence must not begin if that player has since
            // been revived, eliminated, removed, or otherwise stopped
            // being Downed.
            //
            if (!entityRegistry.TryGetEntity(
                    _pendingFact.PlayerId,
                    out var downedIdentity)
                || downedIdentity == null
                || downedIdentity.EntityRoot == null
                || !downedIdentity.TryGetComponent<
                    NetworkPlayerLifeState>(
                        out var downedLifeState)
                || downedLifeState == null
                || downedLifeState.Status
                    != NetworkPlayerLifeStatus.Downed)
            {
                Abort(
                    "downed-player-no-longer-downed",
                    step.Time,
                    settings.FailedAttemptBackoffSeconds);

                return;
            }

            //
            // Use the victim's CURRENT authoritative root position
            // rather than the old attack-hit position.
            //
            var downedPlayerPosition =
                downedIdentity.EntityRoot.position;

            var downedPlayerForward =
                downedIdentity.EntityRoot.forward;

            downedPlayerForward.y = 0f;

            if (downedPlayerForward.sqrMagnitude
                <= 0.0001f)
            {
                downedPlayerForward =
                    transform.position
                    - downedPlayerPosition;

                downedPlayerForward.y = 0f;
            }

            if (downedPlayerForward.sqrMagnitude
                <= 0.0001f)
            {
                downedPlayerForward =
                    Vector3.forward;
            }

            downedPlayerForward.Normalize();

            _downedPlayerPosition =
                downedPlayerPosition;

            _downedPlayerForward =
                downedPlayerForward;

            if (!RefreshEligibleAlivePlayerRoots(
                    lifecycle,
                    _pendingFact.PlayerId,
                    step.Time))
            {
                Abort(
                    "missing-player-registry",
                    step.Time,
                    settings.FailedAttemptBackoffSeconds);

                return;
            }

            if (!IsDownedPlayerIsolated(
                    downedPlayerPosition))
            {
                Abort(
                    "downed-player-not-isolated",
                    step.Time,
                    settings.FailedAttemptBackoffSeconds);

                return;
            }

            if (_eligibleAlivePlayerRoots.Count
                < settings.MinimumOtherAlivePlayers)
            {
                Abort(
                    "not-enough-alive-players",
                    step.Time,
                    settings.FailedAttemptBackoffSeconds);

                return;
            }

            if (!TryResolveApproachPoint(
                    downedPlayerPosition,
                    downedPlayerForward,
                    out var approachPoint))
            {
                Abort(
                    "no-valid-approach",
                    step.Time,
                    settings.FailedAttemptBackoffSeconds);

                return;
            }

            _sequenceOrdinal++;

            if (_sequenceOrdinal == 0)
            {
                _sequenceOrdinal = 1;
            }

            _presentationVisible = true;

            controller.BeginSpecialEncounterOverride();
            _ownsControllerOverride = true;

            if (!controller.TrySetSpecialEncounterDestination(
                    approachPoint))
            {
                Abort(
                    "approach-navigation-failed",
                    step.Time,
                    settings.FailedAttemptBackoffSeconds);

                return;
            }

            SetPhase(
                StalkerSpecialEncounterPhase
                    .ApproachDownedPlayer);

            RuntimeLog.Log(
                RuntimeLogCategory.StalkerCombat,
                "[STK_SPECIAL][BEGIN]");
        }

        private bool RefreshEligibleAlivePlayerRoots(
           FusionPlayerLifecycle lifecycle,
           PlayerId excludedPlayerId,
           AiSimulationTime now)
        {
            _eligibleAlivePlayerRoots.Clear();
            _recentPressureByPlayerRoot.Clear();

            if (lifecycle == null)
            {
                return false;
            }

            var identityRegistry =
                lifecycle.IdentityRegistry;

            var entityRegistry =
                lifecycle.EntityRegistry;

            if (identityRegistry == null
                || entityRegistry == null)
            {
                return false;
            }

            var activeIds =
                new List<PlayerId>();

            identityRegistry.CollectActivePlayerIds(
                activeIds);

            for (var i = 0;
                 i < activeIds.Count;
                 i++)
            {
                var playerId =
                    activeIds[i];

                if (playerId
                    == excludedPlayerId)
                {
                    continue;
                }

                if (!entityRegistry.TryGetEntity(
                        playerId,
                        out var identity)
                    || identity == null
                    || identity.EntityRoot == null
                    || !identity.TryGetComponent<
                        NetworkPlayerLifeState>(
                            out var lifeState)
                    || lifeState == null
                    || lifeState.Status
                        != NetworkPlayerLifeStatus.Alive)
                {
                    continue;
                }

                if (controller != null
                    && !controller.CanPursueCoreCarrierAt(
                        identity.EntityRoot.position))
                {
                    continue;
                }

                _eligibleAlivePlayerRoots.Add(
                    identity.EntityRoot);

                _recentPressureByPlayerRoot[identity.EntityRoot] =
                    controller != null
                        ? controller.GetRecentTargetPressure01(
                            playerId,
                            now,
                            settings.RecentPlayerPressureWindowSeconds)
                        : 0f;
            }

            return true;
        }

        private bool IsDownedPlayerIsolated(Vector3 downedPosition)
        {
            var isolationSqr = settings.IsolationRadius * settings.IsolationRadius;
            for (var i = 0; i < _eligibleAlivePlayerRoots.Count; i++)
            {
                var root = _eligibleAlivePlayerRoots[i];
                if (root != null && (root.position - downedPosition).sqrMagnitude < isolationSqr)
                {
                    return false;
                }
            }

            return true;
        }

        private float ResolveHiddenTransferDuration(
            out float travelDistance)
        {
            travelDistance =
                0f;

            var nearestDistance =
                float.PositiveInfinity;

            for (var i = 0;
                 i < _eligibleAlivePlayerRoots.Count;
                 i++)
            {
                var player =
                    _eligibleAlivePlayerRoots[i];

                if (player == null)
                {
                    continue;
                }

                var delta =
                    player.position
                    - transform.position;

                //
                // Travel time is based on horizontal world
                // distance. Height difference should not make
                // the virtual transfer unnecessarily long.
                //
                delta.y = 0f;

                var distance =
                    delta.magnitude;

                if (distance
                    < nearestDistance)
                {
                    nearestDistance =
                        distance;
                }
            }

            if (float.IsPositiveInfinity(
                    nearestDistance))
            {
                return
                    settings.HiddenTransferMinSeconds;
            }

            travelDistance =
                nearestDistance;

            var duration =
                settings.HiddenTransferDelaySeconds
                + nearestDistance
                / settings.HiddenTransferVirtualSpeed;

            return Mathf.Clamp(
                duration,
                settings.HiddenTransferMinSeconds,
                settings.HiddenTransferMaxSeconds);
        }

        private bool TryResolveApproachPoint(
            Vector3 downedPosition,
            Vector3 downedForward,
            out Vector3 approachPoint)
        {
            approachPoint = default;

            downedForward.y = 0f;

            if (downedForward.sqrMagnitude
                <= 0.0001f)
            {
                downedForward =
                    transform.position
                    - downedPosition;

                downedForward.y = 0f;
            }

            if (downedForward.sqrMagnitude
                <= 0.0001f)
            {
                downedForward =
                    Vector3.forward;
            }

            downedForward.Normalize();

            var candidateAngles =
                new[]
                {
                    0f,
                    -45f,
                    45f,
                    -90f,
                    90f
                };

            var areaMask =
                navMeshAgent != null
                    ? navMeshAgent.areaMask
                    : NavMesh.AllAreas;

            var path =
                new NavMeshPath();

            for (var i = 0;
                 i < candidateAngles.Length;
                 i++)
            {
                var direction =
                    Quaternion.AngleAxis(
                        candidateAngles[i],
                        Vector3.up)
                    * downedForward;

                direction.y = 0f;

                if (direction.sqrMagnitude
                    <= 0.0001f)
                {
                    continue;
                }

                direction.Normalize();

                var candidate =
                    downedPosition
                    + direction
                    * settings.SniffApproachDistance;

                if (!NavMesh.SamplePosition(
                        candidate,
                        out var hit,
                        settings.SniffApproachSampleRadius,
                        areaMask))
                {
                    continue;
                }

                path.ClearCorners();

                if (!NavMesh.CalculatePath(
                        transform.position,
                        hit.position,
                        areaMask,
                        path))
                {
                    continue;
                }

                if (path.status
                    != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                approachPoint =
                    hit.position;

                return true;
            }

            return false;
        }

        private bool TryTeleportToJumpIn()
        {
            if (networkTransform == null)
            {
                return false;
            }

            //
            // Resolve the final position against the same NavMesh
            // area mask used by the Stalker's agent.
            //
            var areaMask =
                navMeshAgent != null
                    ? navMeshAgent.areaMask
                    : NavMesh.AllAreas;

            if (!NavMesh.SamplePosition(
                    _jumpInPosition,
                    out var navMeshHit,
                    settings.DynamicNavMeshSampleRadius,
                    areaMask))
            {
                return false;
            }

            var teleportPosition =
                navMeshHit.position;

            if (!controller.CanPursueCoreCarrierAt(teleportPosition))
            {
                return false;
            }

            if (!TryResolveJumpInRotation(
                    teleportPosition,
                    out var teleportRotation))
            {
                return false;
            }

            //
            // Pre-commit NavMesh synchronization.
            //
            // HiddenTransfer presentation is already invisible here,
            // so moving the local authoritative NavMeshAgent first
            // cannot create a visible pop.
            //
            // More importantly, if Warp fails we return BEFORE
            // committing the Fusion NetworkTransform teleport.
            //
            if (navMeshAgent != null
                && navMeshAgent.enabled)
            {
                if (!navMeshAgent.Warp(
                        teleportPosition))
                {
                    return false;
                }

                navMeshAgent.ResetPath();
            }

            //
            // Warp succeeded (or no active agent exists), so commit
            // the authoritative network teleport.
            //
            networkTransform.Teleport(
                teleportPosition,
                teleportRotation);

            Physics.SyncTransforms();

            _jumpInPosition =
                teleportPosition;
            _lastJumpInPosition = teleportPosition;

            return true;
        }

        private bool TryResolveJumpInRotation(
            Vector3 jumpPosition,
            out Quaternion jumpRotation)
        {
            jumpRotation =
                transform.rotation;

            Transform bestTarget =
                null;

            var bestDistanceSqr =
                float.PositiveInfinity;

            for (var i = 0;
                 i < _eligibleAlivePlayerRoots.Count;
                 i++)
            {
                var playerRoot =
                    _eligibleAlivePlayerRoots[i];

                if (playerRoot == null)
                {
                    continue;
                }

                var delta =
                    playerRoot.position
                    - jumpPosition;

                delta.y = 0f;

                var distanceSqr =
                    delta.sqrMagnitude;

                if (distanceSqr <= 0.0001f
                    || distanceSqr >= bestDistanceSqr)
                {
                    continue;
                }

                bestDistanceSqr =
                    distanceSqr;

                bestTarget =
                    playerRoot;
            }

            if (bestTarget == null)
            {
                return false;
            }

            var direction =
                bestTarget.position
                - jumpPosition;

            direction.y = 0f;

            if (direction.sqrMagnitude
                <= 0.0001f)
            {
                return false;
            }

            direction.Normalize();

            jumpRotation =
                Quaternion.LookRotation(
                    direction,
                    Vector3.up);

            return true;
        }

        private bool IsPendingVictimStillDowned(
            FusionPlayerLifecycle lifecycle)
        {
            if (!_pendingFact.IsValid
                || lifecycle == null
                || lifecycle.EntityRegistry == null)
            {
                return false;
            }

            return lifecycle.EntityRegistry.TryGetEntity(
                    _pendingFact.PlayerId,
                    out var identity)
                && identity != null
                && identity.TryGetComponent<NetworkPlayerLifeState>(
                    out var lifeState)
                && lifeState != null
                && lifeState.Status == NetworkPlayerLifeStatus.Downed;
        }

        private void SetPhase(StalkerSpecialEncounterPhase phase)
        {
            _phase = phase;
            _phaseElapsed = 0f;
            RuntimeLog.Log(RuntimeLogCategory.StalkerCombat, $"[STK_SPECIAL][{phase.ToString().ToUpperInvariant()}]");
        }

        private void Complete(AiSimulationTime now)
        {
            _cooldownUntil = AddSeconds(now, settings.CooldownSeconds);

            if (now.IsValid)
            {
                controller?.RecordSpecialEncounterCompleted(
                    now.Seconds,
                    settings.PostSpecialDirectorCooldownSeconds);
            }

            Cleanup();
            RuntimeLog.Log(RuntimeLogCategory.StalkerCombat, "[STK_SPECIAL][COMPLETE]");
        }

        private void Abort(
            string reason,
            AiSimulationTime now,
            float backoffSeconds)
        {
            bool jumpAlreadyStarted =
                _phase == StalkerSpecialEncounterPhase.JumpOut
                || _phase == StalkerSpecialEncounterPhase.HiddenTransfer
                || _phase == StalkerSpecialEncounterPhase.JumpIn
                || _phase == StalkerSpecialEncounterPhase.ReactionLock;

            float cooldown = jumpAlreadyStarted
                ? Mathf.Max(backoffSeconds, settings.CooldownSeconds)
                : backoffSeconds;

            _cooldownUntil = AddSeconds(now, cooldown);

            Cleanup();

            RuntimeLog.Log(
                RuntimeLogCategory.StalkerCombat,
                $"[STK_SPECIAL][ABORT] reason={reason}");
        }

        private void AbortHiddenTransfer(string reason, AiSimulationTime now)
        {
            _cooldownUntil = AddSeconds(
                now,
                Mathf.Max(settings.FailedAttemptBackoffSeconds, settings.CooldownSeconds));
            _returnToPatrolAfterJumpIn = true;
            _presentationVisible = true;
            SetPhase(StalkerSpecialEncounterPhase.JumpIn);
            RuntimeLog.Log(
                RuntimeLogCategory.StalkerCombat,
                $"[STK_SPECIAL][ABORT] reason={reason}");
        }

        private void Cleanup()
        {
            _pendingFact = default;

            _phase =
                StalkerSpecialEncounterPhase.None;

            _phaseElapsed = 0f;

            _jumpInPosition =
                default;

            _hiddenTransferDurationSeconds =
                0f;

            _downedPlayerPosition =
                default;

            _downedPlayerForward =
                default;

            _eligibleAlivePlayerRoots.Clear();
            _recentPressureByPlayerRoot.Clear();

            _returnToPatrolAfterJumpIn = false;
            _presentationVisible = true;

            if (_ownsControllerOverride)
            {
                _ownsControllerOverride = false;
                controller?.EndSpecialEncounterOverrideToPatrol();
            }
        }

        private bool IsCoolingDown(AiSimulationTime now)
        {
            //
            // Cooldown is a real-duration rule, not an AiSimulationTime
            // total-order rule.
            //
            // AiSimulationTime.CompareTo() orders by Tick first and
            // Seconds second. AddSeconds() intentionally advances only
            // the Seconds deadline because this runtime does not own the
            // simulation tick rate.
            //
            // Therefore CompareTo() would make the cooldown expire as soon
            // as the simulation advances to the next tick.
            //
            // Compare the monotonic Seconds clock directly instead.
            //
            return _cooldownUntil.IsValid
                && now.IsValid
                && now.Seconds < _cooldownUntil.Seconds;
        }

        private static AiSimulationTime AddSeconds(AiSimulationTime time, float seconds)
        {
            return !time.IsValid
                ? AiSimulationTime.Invalid
                : new AiSimulationTime(time.Tick, time.Seconds + Mathf.Max(0f, seconds));
        }

        private float GetPhaseDuration(StalkerSpecialEncounterPhase phase)
        {
            return phase switch
            {
                StalkerSpecialEncounterPhase.ApproachDownedPlayer => settings.ApproachTimeoutSeconds,
                StalkerSpecialEncounterPhase.Sniff => settings.SpecialSniffDurationSeconds,
                StalkerSpecialEncounterPhase.JumpOut => settings.JumpOutDurationSeconds,
                StalkerSpecialEncounterPhase.HiddenTransfer => Mathf.Max(
                    0.01f,
                    _hiddenTransferDurationSeconds),
                StalkerSpecialEncounterPhase.JumpIn => settings.JumpInDurationSeconds,
                StalkerSpecialEncounterPhase.ReactionLock => settings.ReactionLockSeconds,
                _ => 0f,
            };
        }

        private void ResolveDependencies()
        {
            //
            // Keep runtime-added components safe even when an older
            // prefab/scene serialized the nested settings field as null.
            //
            if (settings == null)
            {
                settings =
                    new StalkerSpecialEncounterSettings();
            }

            if (controller == null)
            {
                controller =
                    GetComponent<StalkerController>();
            }

            if (networkTransform == null)
            {
                networkTransform =
                    GetComponent<NetworkTransform>();
            }

            if (navMeshAgent == null)
            {
                navMeshAgent =
                    GetComponent<NavMeshAgent>();
            }

            if (jumpEntryRegistry == null)
            {
                jumpEntryRegistry =
                    GetComponent<StalkerJumpEntryRegistry>();
            }
        }

        private void OnDisable()
        {
            if (IsActive)
            {
                Cleanup();
            }
        }
    }
}
