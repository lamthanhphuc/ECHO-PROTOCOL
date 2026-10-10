using System;
using EchoProtocol.AI.Listener.Noise;
using EchoProtocol.AI.Stalker;
using EchoProtocol.AI.Stalker.Spatial;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.MatchFlow;
using EchoProtocol.Networking;
using EchoProtocol.Networking.Authority;
using Fusion;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace EchoProtocol.AI.Minions
{
    public enum CreepMinionState { Roam = 0, Track = 1, Harass = 2, Flee = 3 }
    public enum CreepMinionAttackKind
    {
        None = 0,
        ShootSlow = 1,
        StealTool = 2,
        StealCore = 3
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class CreepMinionRuntime : NetworkBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float roamSpeed = 5.5f;
        [SerializeField] private float trackSpeed = 8f;
        [SerializeField] private float harassSpeed = 10f;
        [SerializeField] private float fleeSpeed = 12f;

        [SerializeField, Min(5f)]
        private float roamRadius = 28f;

        [SerializeField, Min(2f)]
        private float roamMinPathDistance = 10f;

        [SerializeField, Range(4, 24)]
        private int roamDestinationAttempts = 16;

        [SerializeField] private float targetRefreshSeconds = 0.08f;

        [Header("Perception")]
        [SerializeField] private float visionRange = 20f;
        [SerializeField] private float visionHalfAngle = 95f;
        [SerializeField] private float trackRequiredSeconds = 0.45f;
        [SerializeField] private float zone2SecurityTrackRequiredSeconds = 0.25f;
        [SerializeField] private float lostSightGraceSeconds = 0.75f;
        [FormerlySerializedAs("shadowStopDistance")]
        [SerializeField, Min(0.5f)] private float trackStandOffDistance = 1.5f;
        [SerializeField, Min(0f)] private float roamArrivalSlack = 0.15f;

        [Header("Flying")]
        [SerializeField] private float flightHeight = 6f;
        [SerializeField] private float flightBobAmplitude = 0.05f;
        [SerializeField] private float flightBobSpeed = 4f;
        [SerializeField, Min(0f)] private float ceilingClearance = 0.03f;
        [SerializeField, Min(1f)] private float ceilingProbeDistance = 40f;
        [SerializeField] private Color visualTint = new Color(0.42f, 0.42f, 0.42f, 1f);

        [Header("Combat")]
        [SerializeField] private float shootRange = 10f;
        [SerializeField] private float stealToolRange = 30f;
        [SerializeField, Min(0.1f)] private float attackCooldownSeconds = 0.65f;
        [SerializeField, Min(0.5f)] private float toolFleeSeconds = 1.6f;

        [Header("Hit Effects")]
        [SerializeField] private float slowMultiplier = 0.65f;
        [SerializeField] private float slowDurationSeconds = 3f;
        [SerializeField] private float coreCarryDistance = 12f;
        [SerializeField] private float coreCarryTimeoutSeconds = 3f;
        [SerializeField] private float coreCarryHeight = 0.65f;
        [SerializeField, Min(1f)] private float fleeMinDistance = 4f;
        [SerializeField, Min(1f)] private float stolenToolDropMinDistance = 6f;
        [SerializeField, Min(1f)] private float stolenToolDropMaxDistance = 30f;

        [Header("Alert")]
        [SerializeField] private float alertCooldownSeconds = 4f;
        [SerializeField, Min(0.05f)] private float alertRetrySeconds = 0.15f;

        [Header("Presentation")]
        [SerializeField, Min(0.1f)] private float animationSpeed = 1.6f;
        [SerializeField, Min(0.1f)] private float attackAnimationLockSeconds = 0.35f;

        [Header("Counterplay")]
        [FormerlySerializedAs("flashlightRepelRange")]
        [SerializeField, Min(1f)]
        private float flashlightKillRange = 25f;

        [FormerlySerializedAs("flashlightExposureRequiredSeconds")]
        [SerializeField, Min(0.1f)]
        private float flashlightKillExposureSeconds = 1f;

        [SerializeField, Min(0f)]
        private float flashlightExposureDecayPerSecond = 2f;

        [SerializeField, Min(0.1f)]
        private float deathVanishSeconds = 0.5f;
        [SerializeField] private float noiseMakerDistractionSeconds = 6f;
        [SerializeField] private float noiseMakerArrivalDistance = 1.5f;

        [Networked] public int StateValue { get; private set; }
        [Networked] public int ZoneValue { get; private set; }
        [Networked] public PlayerRef TargetPlayer { get; private set; }
        [Networked] public NetworkId StolenCoreId { get; private set; }
        [Networked] public NetworkBool IsMoving { get; private set; }
        [Networked] public uint AttackSequence { get; private set; }
        [Networked] public int AttackKindValue { get; private set; }
        [Networked] public float FlightHeightValue { get; private set; }
        [Networked] public uint AlertSequence { get; private set; }
        [Networked] private TickTimer AttackCooldown { get; set; }
        [Networked] private TickTimer AlertCooldown { get; set; }
        [Networked] private TickTimer FleeTimer { get; set; }

        [Networked] public NetworkBool IsDying { get; private set; }
        [Networked] private TickTimer DeathTimer { get; set; }

        public CreepMinionAttackKind AttackKind =>
            AttackKindValue >= 0 && AttackKindValue <= 3
                ? (CreepMinionAttackKind)AttackKindValue
                : CreepMinionAttackKind.None;

        public CreepMinionState State => StateValue >= 0 && StateValue <= 3
            ? (CreepMinionState)StateValue : CreepMinionState.Roam;
        public RegionSemanticZone Zone => ZoneValue == (int)RegionSemanticZone.Zone01
            || ZoneValue == (int)RegionSemanticZone.Zone02
            || ZoneValue == (int)RegionSemanticZone.Zone03
            ? (RegionSemanticZone)ZoneValue : RegionSemanticZone.Unknown;

        private NavMeshAgent _agent;
        private StalkerNavigationController _navigation;
        private Animator[] _animators;
        private HostRuntimeNoiseService _noiseService;
        private float _trackSeconds;
        private float _lostSightSeconds;
        private float _flashlightExposureSeconds;
        private float _nextTargetRefreshAt;
        private float _nextRoamRetargetAt;
        private float _distractionUntil;
        private Vector3 _lastKnownTargetPosition;
        private Vector3 _fleeDestination;
        private Vector3 _distractionPoint;
        private long _alertOrdinal;
        private long _attackAttemptOrdinal;
        private uint _renderedAttackSequence;
        private uint _renderedAlertSequence;
        private float _actionAnimationUntil;
        private string _currentLocomotionAnimation;
        private bool _stalkerAlertDeliveredForTarget;
        private string _noiseMakerSourceEventId;
        private PlayerRef _noiseMakerSourcePlayer;
        private PlayerRef _noiseMakerThreatTarget;
        private bool _noiseMakerReactionRecorded;
        private Transform _visualRoot;
        private Vector3 _visualInitialScale;
        private Vector3 _visualBaseLocalPosition;
        private Collider _bodyCollider;
        private Renderer[] _visualRenderers;
        private float _nextCeilingProbeAt;
        private float _visualTopOffset;

        public void ConfigureBeforeSpawn(RegionSemanticZone zone) => ZoneValue = (int)zone;

        public override void Spawned()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animators = GetComponentsInChildren<Animator>(true);
            foreach (var animator in _animators)
            {
                if (animator != null)
                {
                    animator.speed = animationSpeed;
                }
            }
            _bodyCollider = GetComponent<Collider>();
            _renderedAttackSequence = AttackSequence;
            _renderedAlertSequence = AlertSequence;

            _visualRoot = transform.Find("Visual");
            if (_visualRoot != null)
            {
                _visualInitialScale = _visualRoot.localScale;
                _visualBaseLocalPosition = _visualRoot.localPosition;
                _visualRenderers = _visualRoot.GetComponentsInChildren<Renderer>(true);
                if (TryGetVisualBounds(out var visualBounds))
                {
                    _visualTopOffset = visualBounds.max.y - _visualRoot.position.y;
                }

                foreach (var renderer in _visualRenderers)
                {
                    if (renderer == null)
                        continue;

                    var properties = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(properties);
                    properties.SetFloat("_OutlineWidth", 0f);
                    properties.SetFloat("_RimIntensity", 0f);
                    properties.SetColor("_RimColor", Color.black);
                    properties.SetColor("_RimColor1", Color.black);
                    properties.SetColor("_RimColor2", Color.black);
                    properties.SetColor("_Color", visualTint);
                    renderer.SetPropertyBlock(properties);
                }
            }

            if (!Object.HasStateAuthority)
            {
                _agent.enabled = false;
                return;
            }

            IsDying = false;
            DeathTimer = TickTimer.None;
            StateValue = (int)CreepMinionState.Roam;
            FlightHeightValue = flightHeight;
            TargetPlayer = PlayerRef.None;
            _noiseMakerSourceEventId = null;
            _noiseMakerSourcePlayer = PlayerRef.None;
            _noiseMakerThreatTarget = PlayerRef.None;
            _noiseMakerReactionRecorded = false;
            StolenCoreId = default;
            AttackCooldown = TickTimer.None;
            AlertCooldown = TickTimer.None;
            FleeTimer = TickTimer.None;
            _noiseService = HostRuntimeNoiseService.EnsureExists(MatchAuthorityRuntime.Instance);
            _noiseService.RuntimeNoiseAccepted += HandleRuntimeNoiseAccepted;
            _agent.enabled = false;
            _agent.updatePosition = true;
            _agent.updateRotation = true;
            _agent.angularSpeed = 1080f;
            _agent.acceleration = 40f;
            _agent.stoppingDistance = 0.4f;
            if (!TryActivateAgent())
            {
                // Runtime log intentionally disabled.
            }
        }

        private bool TryActivateAgent()
        {
            if (_agent.enabled && _agent.isOnNavMesh)
            {
                _navigation ??= new StalkerNavigationController(_agent);
                _navigation.SetAuthoritativeLocomotion(true);
                return true;
            }

            if (_agent.enabled) _agent.enabled = false;
            if (!NavMesh.SamplePosition(transform.position, out var hit, 2f, _agent.areaMask)) return false;
            transform.position = hit.position;
            _agent.enabled = true;
            if (!_agent.isOnNavMesh)
            {
                _agent.enabled = false;
                return false;
            }

            _navigation ??= new StalkerNavigationController(_agent);
            _navigation.SetAuthoritativeLocomotion(true);
// #if UNITY_EDITOR || DEVELOPMENT_BUILD
//             Debug.Log($"[CREEP_SPAWN][READY] id={Object.Id} zone={Zone} position={transform.position}", this);
// #endif
            return true;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _navigation?.SetAuthoritativeLocomotion(false);
            _navigation = null;
            if (hasState && runner != null && runner.IsServer
                && Object != null && Object.HasStateAuthority)
                MatchAuthorityRuntime.Instance?.RecordMinionDespawn(
                    Object.Id, runner.Tick.Raw);
            if (_noiseService != null) _noiseService.RuntimeNoiseAccepted -= HandleRuntimeNoiseAccepted;
            if (hasState && Object.HasStateAuthority) ReleaseStolenCoreAuthoritative();
        }

        private void HandleRuntimeNoiseAccepted(RuntimeNoiseEvent noiseEvent)
        {
            if (!Object.HasStateAuthority || noiseEvent.NoiseType != RuntimeNoiseType.NOISE_MAKER
                || StolenCoreId.IsValid || Vector3.Distance(transform.position, noiseEvent.WorldPosition) > noiseEvent.HearingRadius)
            {
                return;
            }
            bool activeThreat = (State == CreepMinionState.Track
                || State == CreepMinionState.Harass)
                && TargetPlayer.IsRealPlayer && !IsDying;
            var threatTarget = TargetPlayer;
            var authority = MatchAuthorityRuntime.Instance;
            var sourcePlayer = ResolveNoiseSourcePlayer(noiseEvent.SourcePlayerId);
            _noiseMakerSourceEventId = null;
            _noiseMakerSourcePlayer = PlayerRef.None;
            _noiseMakerThreatTarget = PlayerRef.None;
            _noiseMakerReactionRecorded = false;
            if (activeThreat && sourcePlayer.IsValid
                && !string.IsNullOrWhiteSpace(noiseEvent.NoiseEventId))
                authority?.RecordMinionFact(AEDMinionFactKindV1.NoiseMakerOpportunity,
                    Object.Id, $"noisemaker-opportunity:{noiseEvent.NoiseEventId}",
                    Runner.Tick.Raw, sourcePlayer, threatTarget,
                    effectKind: "ACTIVE_THREAT_DIVERSION",
                    sourceEventId: noiseEvent.NoiseEventId);
            if (activeThreat && sourcePlayer.IsValid
                && !string.IsNullOrWhiteSpace(noiseEvent.NoiseEventId))
            {
                _noiseMakerSourceEventId = noiseEvent.NoiseEventId;
                _noiseMakerSourcePlayer = sourcePlayer;
                _noiseMakerThreatTarget = threatTarget;
                _noiseMakerReactionRecorded = false;
            }
            else if (activeThreat)
                authority?.MarkMinionEvidenceIncomplete();
            _distractionPoint = noiseEvent.WorldPosition;
            _distractionUntil = Time.time + noiseMakerDistractionSeconds;
            ResetTracking();
            StateValue = (int)CreepMinionState.Roam;
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority || _agent == null) return;

            // Dying thực sự phải đứng trước
            // mọi NavMesh/AI activation.
            if (IsDying)
            {
                IsMoving = false;
                if (DeathTimer.Expired(Runner))
                {
                    Runner.Despawn(Object);
                }
                return;
            }

            if (!_agent.enabled && !TryActivateAgent())
            {
                IsMoving = false;
                return;
            }
            if (!_agent.isOnNavMesh
                && (_navigation == null || !_navigation.TryReattachToNearestNavMesh(2f)))
            {
                IsMoving = false;
                return;
            }

            UpdateFlightHeightAuthoritative();

            bool illuminated =
                TryGetFlashlightSource(out _);

            if (illuminated)
            {
                _flashlightExposureSeconds =
                    Mathf.Min(
                        flashlightKillExposureSeconds,
                        _flashlightExposureSeconds
                        + Runner.DeltaTime);
            }
            else
            {
                _flashlightExposureSeconds =
                    Mathf.Max(
                        0f,
                        _flashlightExposureSeconds
                        - Runner.DeltaTime
                          * flashlightExposureDecayPerSecond);
            }

#if false
            if (illuminated)
            {
                // Debug.Log(
                    // $"[CREEP_FLASHLIGHT][EXPOSURE] " +
                    // $"id={Object.Id} " +
                    // $"exposure={_flashlightExposureSeconds:F2}/" +
                    // $"{flashlightKillExposureSeconds:F2}",
                    // this);
            }
#endif

            if (_flashlightExposureSeconds
                >= flashlightKillExposureSeconds)
            {
                BeginFlashlightDeath();
                return;
            }

            if (State == CreepMinionState.Flee)
            {
                _agent.speed = fleeSpeed;
                _agent.SetDestination(_fleeDestination);
                if (StolenCoreId.IsValid && Runner.TryFindObject(StolenCoreId, out var coreObject)
                    && coreObject != null && coreObject.TryGetComponent<NetworkPickupItem>(out var core))
                {
                    core.TryUpdateMonsterCarryPoseAuthoritative(Object.Id, CarryPosition(), transform.rotation);
                }
                if (Vector3.Distance(transform.position, _fleeDestination) <= 1f || FleeTimer.Expired(Runner))
                    ReleaseStolenCoreAuthoritative();
            }
            else if (_distractionUntil > Time.time && !StolenCoreId.IsValid)
            {
                _agent.speed = trackSpeed;
                _agent.SetDestination(_distractionPoint);
                if (Vector3.Distance(transform.position, _distractionPoint) <= noiseMakerArrivalDistance)
                {
                    if (!_noiseMakerReactionRecorded
                        && !string.IsNullOrWhiteSpace(_noiseMakerSourceEventId)
                        && _noiseMakerSourcePlayer.IsValid
                        && _noiseMakerThreatTarget.IsRealPlayer)
                    {
                        MatchAuthorityRuntime.Instance?.RecordMinionFact(
                            AEDMinionFactKindV1.NoiseMakerReaction, Object.Id,
                            $"noisemaker-reaction:{_noiseMakerSourceEventId}",
                            Runner.Tick.Raw, _noiseMakerSourcePlayer, _noiseMakerThreatTarget,
                            effectKind: "ACTIVE_THREAT_DIVERSION",
                            accepted: true, sourceEventId: _noiseMakerSourceEventId);
                        _noiseMakerReactionRecorded = true;
                    }
                    _distractionUntil = 0f;
                }
            }
            else
            {
                _distractionUntil = 0f;
                UpdateTargetAndMovement();
            }
            _navigation.TickAuthoritativeLocomotion(Runner.DeltaTime);
            IsMoving = _navigation.AuthoritativeMoveSpeed > 0.2f;
            PublishAEDSnapshot();
        }

        private void UpdateTargetAndMovement()
        {
            if (Time.time >= _nextTargetRefreshAt)
            {
                _nextTargetRefreshAt = Time.time + targetRefreshSeconds;
                var best = FindBestVisibleTarget();
                if (best.IsRealPlayer && best != TargetPlayer)
                {
                    TargetPlayer = best;
                    _stalkerAlertDeliveredForTarget = false;
                    _trackSeconds = 0f;
                    _lostSightSeconds = 0f;
                    StateValue = (int)CreepMinionState.Track;
                }
            }

            if (!TryGetEligiblePlayer(TargetPlayer, out var targetObject, out var targetLobby, out var lifeState))
            {
                ResetTracking();
                Roam();
                return;
            }

            if (!CanSeePlayer(targetObject))
            {
                _lostSightSeconds += Runner.DeltaTime;
                if (_lostSightSeconds > lostSightGraceSeconds)
                {
                    ResetTracking();
                    Roam();
                }
                else
                {
                    _agent.speed = trackSpeed;
                    _agent.SetDestination(_lastKnownTargetPosition);
                }
                return;
            }

            _lastKnownTargetPosition = targetObject.transform.position;
            _lostSightSeconds = 0f;
            if (State != CreepMinionState.Harass)
            {
                StateValue = (int)CreepMinionState.Track;
                _trackSeconds += Runner.DeltaTime;
                _agent.speed = trackSpeed;
                if (Vector3.Distance(transform.position, _lastKnownTargetPosition) > trackStandOffDistance)
                    _agent.SetDestination(_lastKnownTargetPosition);
                else if (_agent.hasPath)
                    _agent.ResetPath();

                var stage = NetworkMatchState.Instance != null ? NetworkMatchState.Instance.Zone2Stage : default;
                float required = Zone == RegionSemanticZone.Zone02
                    && (stage == Zone2MissionStage.SecurityHoldReady || stage == Zone2MissionStage.SecurityHold)
                    ? zone2SecurityTrackRequiredSeconds : trackRequiredSeconds;
                if (_trackSeconds >= required && AlertCooldown.ExpiredOrNotRunning(Runner)) EmitStalkerAlert();
            }

            if (State == CreepMinionState.Harass)
            {
                if (!_stalkerAlertDeliveredForTarget && AlertCooldown.ExpiredOrNotRunning(Runner))
                {
                    _stalkerAlertDeliveredForTarget = TrySendStalkerAlert();
                    AlertCooldown = TickTimer.CreateFromSeconds(
                        Runner,
                        _stalkerAlertDeliveredForTarget ? alertCooldownSeconds : alertRetrySeconds);
                }

                _agent.speed = harassSpeed;
                float distance = Vector3.Distance(transform.position, targetObject.transform.position);
                bool wantsCloseAttack =
                    targetLobby != null
                    && (targetLobby.CarriedCoreId.IsValid
                        || (targetLobby.ToolId >= 1
                            && targetLobby.ToolId <= 6));
                float actionRange = wantsCloseAttack ? stealToolRange : shootRange;
                if (distance > actionRange)
                    _agent.SetDestination(targetObject.transform.position);
                else if (_agent.hasPath)
                    _agent.ResetPath();

                if (distance <= actionRange && AttackCooldown.ExpiredOrNotRunning(Runner))
                    AttackPlayer(targetObject, lifeState);
            }
        }

        private void Roam()
        {
            StateValue = (int)CreepMinionState.Roam;
            _agent.speed = roamSpeed;

            bool stillTravelling = _agent.pathPending
                || (_agent.hasPath
                    && _agent.remainingDistance > _agent.stoppingDistance + roamArrivalSlack);

            // Ponytail:
            // đã có destination hợp lệ thì đi hết.
            // Không đổi target sau 3–6 giây giữa đường.
            if (stillTravelling)
            {
                return;
            }

            // Chỉ dùng timer như retry delay
            // nếu chưa tìm được destination.
            if (Time.time < _nextRoamRetargetAt)
            {
                return;
            }

            if (TrySetLongRangeRoamDestination())
            {
                return;
            }

            // Không tìm được đường thì thử lại sau 1 giây.
            _nextRoamRetargetAt = Time.time + 0.15f;
        }

        private bool TrySetLongRangeRoamDestination()
        {
            if (!NavMesh.SamplePosition(transform.position, out var originHit, 2f, _agent.areaMask))
            {
                return false;
            }

            var path = new NavMeshPath();

            for (int attempt = 0; attempt < roamDestinationAttempts; attempt++)
            {
                Vector2 direction = UnityEngine.Random.insideUnitCircle;
                if (direction.sqrMagnitude < 0.01f)
                {
                    continue;
                }

                direction.Normalize();

                float distance = UnityEngine.Random.Range(roamMinPathDistance, roamRadius);

                Vector3 candidate = transform.position
                    + new Vector3(direction.x, 0f, direction.y) * distance;

                if (!NavMesh.SamplePosition(candidate, out var hit, 3f, _agent.areaMask))
                {
                    continue;
                }

                if (!NavMesh.CalculatePath(originHit.position, hit.position, _agent.areaMask, path)
                    || path.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                // Candidate có thể nhìn xa nhưng NavMesh
                // thực tế chỉ dẫn Minion đi vài mét.
                // Kiểm tra chiều dài route thật.
                float pathLength = CalculatePathLength(path);

                if (pathLength < roamMinPathDistance)
                {
                    continue;
                }

                _agent.SetDestination(hit.position);

#if false
                // Debug.Log(
                    $"[CREEP_ROAM][DESTINATION] " +
                    $"id={Object.Id} " +
                    $"pathLength={pathLength:F1} " +
                    $"destination={hit.position}",
                    this);
#endif

                return true;
            }

#if false
            // Debug.LogWarning(
                $"[CREEP_ROAM][NO_LONG_PATH] " +
                $"id={Object.Id} " +
                $"position={transform.position}",
                this);
#endif

            return false;
        }

        private static float CalculatePathLength(NavMeshPath path)
        {
            if (path == null || path.corners == null || path.corners.Length < 2)
            {
                return 0f;
            }

            float length = 0f;

            for (int i = 1; i < path.corners.Length; i++)
            {
                length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            }

            return length;
        }

        private PlayerRef FindBestVisibleTarget()
        {
            PlayerRef best = PlayerRef.None;
            float bestScore = float.NegativeInfinity;
            foreach (var player in Runner.ActivePlayers)
            {
                if (!TryGetEligiblePlayer(player, out var obj, out var lobby, out _) || !CanSeePlayer(obj)) continue;
                float score = -Vector3.Distance(transform.position, obj.transform.position);
                if (lobby.CarriedCoreId.IsValid)
                    score += 500f;
                else if (lobby.ToolId >= 1 && lobby.ToolId <= 6)
                    score += 250f;
                var match = NetworkMatchState.Instance;
                if (Zone == RegionSemanticZone.Zone02 && match != null)
                {
                    if (match.Zone2Stage == Zone2MissionStage.RepairRelays
                        && (player == match.RelayA1Operator || player == match.RelayA2Operator
                            || player == match.RelayB1Operator || player == match.RelayB2Operator)) score += 1000f;
                    if ((match.Zone2Stage == Zone2MissionStage.SecurityHoldReady || match.Zone2Stage == Zone2MissionStage.SecurityHold)
                        && (player == match.SecurityHoldOperator || player == match.SecurityHoldOperator2
                            || player == match.SecurityHoldOperator3 || player == match.SecurityHoldOperator4)) score += 1000f;
                }
                if (score <= bestScore) continue;
                bestScore = score;
                best = player;
            }
            return best;
        }

        private bool TryGetEligiblePlayer(PlayerRef player, out NetworkObject obj, out LobbyPlayerState lobby,
            out NetworkPlayerLifeState life)
        {
            obj = null;
            lobby = null;
            life = null;
            return player.IsRealPlayer && Runner.TryGetPlayerObject(player, out obj) && obj != null && obj.IsValid
                && obj.TryGetComponent(out lobby) && lobby.IsGameplayPlayer
                && obj.TryGetComponent(out life) && life.Status == NetworkPlayerLifeStatus.Alive
                && (!obj.TryGetComponent<NetworkPlayerMovement>(out var movement) || !movement.IsHidden)
                && (!obj.TryGetComponent<LobbyPlayerState>(out var playerState) || !playerState.IsStabilizerBuffed);
        }

        private bool CanSeePlayer(NetworkObject playerObject)
        {
            if (playerObject == null) return false;
            var offset = playerObject.transform.position - transform.position;
            if (offset.magnitude > visionRange || Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(offset, Vector3.up)) > visionHalfAngle)
                return false;
            float currentFlightHeight =
                FlightHeightValue > 0.01f
                    ? FlightHeightValue
                    : flightHeight;
            var origin = transform.position + Vector3.up * currentFlightHeight;
            var destination = playerObject.transform.position + Vector3.up;
            var ray = destination - origin;
            var hits = Physics.RaycastAll(origin, ray.normalized, ray.magnitude, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                return hit.collider.transform.IsChildOf(playerObject.transform);
            }
            return true;
        }

        private void EmitStalkerAlert()
        {
            _stalkerAlertDeliveredForTarget = TrySendStalkerAlert();
            _trackSeconds = 0f;
            StateValue = (int)CreepMinionState.Harass;
            AlertCooldown = TickTimer.CreateFromSeconds(
                Runner,
                _stalkerAlertDeliveredForTarget ? alertCooldownSeconds : alertRetrySeconds);
        }

        private bool TrySendStalkerAlert()
        {
            var ordinal = ++_alertOrdinal;
            var occurrence = $"minion-alert:{Object.Id}:{ordinal}";
            RuntimeNoiseEvent noiseEvent = default;
            bool accepted = _noiseService != null && _noiseService.TryAccept(
                TargetPlayer,
                RuntimeNoiseType.MINION_ALERT,
                new RuntimeNoiseSourceOccurrenceKey($"minion-alert:{Object.Id}", ordinal),
                _lastKnownTargetPosition,
                out noiseEvent);

            var authority = MatchAuthorityRuntime.Instance;
            authority?.RecordMinionFact(AEDMinionFactKindV1.AlertAttempted,
                Object.Id, occurrence, Runner.Tick.Raw, TargetPlayer,
                effectKind: "MINION_ALERT", accepted: accepted,
                sourceEventId: accepted ? noiseEvent.NoiseEventId : null);
            if (accepted && !string.IsNullOrWhiteSpace(noiseEvent.NoiseEventId))
                authority?.RecordMinionFact(AEDMinionFactKindV1.AlertAccepted,
                    Object.Id, occurrence, Runner.Tick.Raw, TargetPlayer,
                    effectKind: "MINION_ALERT", accepted: true,
                    sourceEventId: noiseEvent.NoiseEventId);
            else if (accepted)
                authority?.MarkMinionEvidenceIncomplete();

            if (accepted) AlertSequence++;
#if false
            if (!accepted)
                // Debug.LogWarning($"[CREEP_ALERT][REJECTED] target={TargetPlayer} zone={Zone} position={_lastKnownTargetPosition}", this);
            else
                // Debug.Log($"[CREEP_ALERT][ACCEPTED] target={TargetPlayer} zone={Zone} position={_lastKnownTargetPosition}", this);
#endif
            return accepted;
        }

        private void AttackPlayer(
            NetworkObject playerObject,
            NetworkPlayerLifeState life)
        {
            if (playerObject == null
                || life == null
                || Runner == null
                || !Runner.IsRunning
                || !AttackCooldown.ExpiredOrNotRunning(Runner))
            {
                return;
            }

            // Ponytail:
            // AttackPlayer tự bảo vệ cooldown,
            // không phụ thuộc hoàn toàn vào caller.
            if (!AttackCooldown
                .ExpiredOrNotRunning(Runner))
            {
                return;
            }

            // Commit cooldown ngay khi một attack attempt
            // hợp lệ bắt đầu.
            //
            // Nếu life-state thay đổi trong cùng tick
            // và damage bị reject, minion cũng không spam
            // attack lại mỗi network tick.
            var lobby =
                playerObject.GetComponent<
                    LobbyPlayerState>();

            var interactor =
                playerObject.GetComponent<
                    NetworkPlayerInteractor>();

#if false
            bool _hasCore = lobby != null && lobby.CarriedCoreId.IsValid;
            int  _toolId  = lobby != null ? lobby.ToolId : 0;
            // Debug.Log(
                $"[CREEP_ATTACK][HIT] " +
                $"target={TargetPlayer} " +
                $"zone={Zone} " +
                $"core={_hasCore} " +
                $"tool={_toolId}",
                this);
#endif

            float distance = Vector3.Distance(transform.position, playerObject.transform.position);
            if (distance <= stealToolRange
                && lobby != null
                && lobby.CarriedCoreId.IsValid
                && interactor != null
                )
            {
                var ordinal = ++_attackAttemptOrdinal;
                var attempt = $"minion-attack:{Object.Id}:{ordinal}";
                var coreId = lobby.CarriedCoreId.ToString();
                var success = TryStealCore(playerObject, lobby, interactor,
                    out var forcedDrop, out var stolen);
                var authority = MatchAuthorityRuntime.Instance;
                authority?.RecordMinionFact(AEDMinionFactKindV1.AttackAttempted,
                    Object.Id, attempt, Runner.Tick.Raw, TargetPlayer,
                    objectId: coreId, effectKind: "CORE", attemptOrdinal: ordinal,
                    accepted: forcedDrop);
                if (forcedDrop)
                    authority?.RecordMinionFact(AEDMinionFactKindV1.CoreForcedDrop,
                        Object.Id, attempt, Runner.Tick.Raw, TargetPlayer,
                        objectId: coreId, effectKind: "CORE_FORCED_DROP",
                        attemptOrdinal: ordinal, accepted: true);
                if (stolen)
                    authority?.RecordMinionFact(AEDMinionFactKindV1.CoreStolen,
                        Object.Id, attempt, Runner.Tick.Raw, TargetPlayer,
                        objectId: coreId, effectKind: "CORE_STOLEN",
                        attemptOrdinal: ordinal, accepted: true);
                if (success)
                {
                    CommitAttack(CreepMinionAttackKind.StealCore);
                    return;
                }
            }

            if (distance <= stealToolRange
                && lobby != null
                && lobby.ToolId >= 1
                && lobby.ToolId <= 6
                && interactor != null)
            {
                var ordinal = ++_attackAttemptOrdinal;
                var attempt = $"minion-attack:{Object.Id}:{ordinal}";
                var toolType = lobby.ToolId.ToString();
                bool relocated = TryRelocateTeamTool(playerObject,
                    interactor, out Vector3 toolDropPosition);
                var authority = MatchAuthorityRuntime.Instance;
                authority?.RecordMinionFact(AEDMinionFactKindV1.AttackAttempted,
                    Object.Id, attempt, Runner.Tick.Raw, TargetPlayer,
                    objectId: $"tool-type:{toolType}", effectKind: "TOOL",
                    attemptOrdinal: ordinal, accepted: relocated,
                    position: relocated ? toolDropPosition : default);
                if (relocated)
                {
                    authority?.RecordMinionFact(AEDMinionFactKindV1.ToolRelocated,
                        Object.Id, attempt, Runner.Tick.Raw, TargetPlayer,
                        objectId: $"tool-type:{toolType}", effectKind: "TOOL_RELOCATED",
                        attemptOrdinal: ordinal, accepted: true,
                        position: toolDropPosition);
                    CommitAttack(CreepMinionAttackKind.StealTool);
                    BeginFleeTo(toolDropPosition, toolFleeSeconds);
                    return;
                }
            }

            if (distance > shootRange)
                return;

            var slowOrdinal = ++_attackAttemptOrdinal;
            var slowAttempt = $"minion-attack:{Object.Id}:{slowOrdinal}";

            var movement =
                playerObject.GetComponent<
                    NetworkPlayerMovement>();

            bool slowApplied = movement != null && movement.TryApplySlowAuthoritative(
                slowMultiplier, slowDurationSeconds);
            var minionAuthority = MatchAuthorityRuntime.Instance;
            minionAuthority?.RecordMinionFact(AEDMinionFactKindV1.AttackAttempted,
                Object.Id, slowAttempt, Runner.Tick.Raw, TargetPlayer,
                effectKind: "SLOW", attemptOrdinal: slowOrdinal,
                accepted: slowApplied);
            if (!slowApplied) return;
            minionAuthority?.RecordMinionFact(AEDMinionFactKindV1.SlowApplied,
                Object.Id, slowAttempt, Runner.Tick.Raw, TargetPlayer,
                effectKind: "SLOW_APPLIED", attemptOrdinal: slowOrdinal,
                accepted: true);

            CommitAttack(CreepMinionAttackKind.ShootSlow);
        }

        private void CommitAttack(CreepMinionAttackKind kind)
        {
            AttackKindValue = (int)kind;
            AttackSequence++;
            AttackCooldown = TickTimer.CreateFromSeconds(
                Runner,
                attackCooldownSeconds);
        }

        private bool TryRelocateTeamTool(
            NetworkObject playerObject,
            NetworkPlayerInteractor interactor,
            out Vector3 dropPosition)
        {
            dropPosition = default;
            if (!TryFindSabotageDropPosition(
                    playerObject.transform.position,
                    out dropPosition))
            {
                return false;
            }

            bool success = interactor.DropTeamToolAuthoritative(
                TargetPlayer,
                dropPosition);
#if false
            // Debug.Log($"[CREEP_STEAL_TOOL] target={TargetPlayer} success={success} drop={dropPosition}", this);
#endif
            return success;
        }

        private bool TryFindSabotageDropPosition(Vector3 playerPosition, out Vector3 position)
        {
            position = default;
            if (!NavMesh.SamplePosition(playerPosition, out var playerHit, 2f, NavMesh.AllAreas)) return false;

            var path = new NavMeshPath();
            for (int i = 0; i < 8; i++)
            {
                Vector2 direction = UnityEngine.Random.insideUnitCircle;
                if (direction.sqrMagnitude < 0.01f) continue;
                direction.Normalize();
                float distance = UnityEngine.Random.Range(stolenToolDropMinDistance, stolenToolDropMaxDistance);
                var candidate = playerPosition + new Vector3(direction.x, 0f, direction.y) * distance;
                if (!NavMesh.SamplePosition(candidate, out var hit, 2.5f, NavMesh.AllAreas)) continue;
                float actualDistance = Vector3.Distance(playerPosition, hit.position);
                if (actualDistance < stolenToolDropMinDistance || actualDistance > stolenToolDropMaxDistance) continue;
                if (!NavMesh.CalculatePath(playerHit.position, hit.position, NavMesh.AllAreas, path)
                    || path.status != NavMeshPathStatus.PathComplete) continue;

                position = hit.position + Vector3.up * 0.05f;
                return true;
            }

            return false;
        }

        private bool TryStealCore(NetworkObject playerObject, LobbyPlayerState lobby,
            NetworkPlayerInteractor interactor, out bool forcedDrop, out bool stolen)
        {
            forcedDrop = false;
            stolen = false;
            var coreId = lobby.CarriedCoreId;
            if (interactor == null || !coreId.IsValid) return false;
            if (!Runner.TryFindObject(coreId, out var obj)
                || obj == null || !obj.TryGetComponent<NetworkPickupItem>(out var core))
            {
#if false
                // Debug.LogWarning($"[CREEP_STEAL_CORE][NO_OBJECT] id={coreId}", this);
#endif
                return false;
            }
            if (!interactor.DropCarriedCoreAuthoritative(TargetPlayer))
            {
#if false
                // Debug.LogWarning($"[CREEP_STEAL_CORE][DROP_FAILED] target={TargetPlayer} id={coreId}", this);
#endif
                return false;
            }
            forcedDrop = true;
            // A successful forced drop consumes this hit's one side effect even if carry cannot begin.
            if (!core.TryBeginMonsterCarryAuthoritative(Object.Id, CarryPosition(), transform.rotation))
            {
#if false
                // Debug.LogWarning($"[CREEP_STEAL_CORE][CARRY_FAILED] core={coreId} monster={Object.Id}", this);
#endif
                return true;
            }
            StolenCoreId = coreId;
            stolen = true;
            BeginFlee(playerObject.transform.position, coreCarryTimeoutSeconds);
#if false
            // Debug.Log($"[CREEP_STEAL_CORE][SUCCESS] core={coreId} destination={_fleeDestination}", this);
#endif
            return true;
        }

        private Vector3 CarryPosition()
        {
            float height =
                FlightHeightValue > 0f
                    ? FlightHeightValue
                    : flightHeight;

            return transform.position
                + Vector3.up * (height + coreCarryHeight)
                + transform.forward * 0.2f;
        }

        private void BeginFlashlightDeath()
        {
            if (!Object.HasStateAuthority || IsDying) return;

            MatchAuthorityRuntime.Instance?.RecordMinionFact(
                AEDMinionFactKindV1.TeamDeathReceipt, Object.Id,
                $"minion-team-death:{Object.Id}", Runner.Tick.Raw,
                effectKind: "FLASHLIGHT_DEATH", accepted: true);

            // Nếu đang giữ Core thì trả Core trước,
            // không để Core biến mất cùng Minion.
            if (StolenCoreId.IsValid)
                ReleaseStolenCoreAuthoritative();

            IsDying = true;
            ResetTracking();

            _distractionUntil = 0f;
            _flashlightExposureSeconds = 0f;

            AttackCooldown = TickTimer.None;
            AlertCooldown = TickTimer.None;
            FleeTimer = TickTimer.None;
            IsMoving = false;

            if (_bodyCollider != null)
                _bodyCollider.enabled = false;

            if (_agent != null && _agent.enabled)
            {
                if (_agent.isOnNavMesh) _agent.ResetPath();
                _agent.enabled = false;
            }

            DeathTimer = TickTimer.CreateFromSeconds(
                Runner,
                Mathf.Max(0.1f, deathVanishSeconds));

#if false
            // Debug.Log(
                $"[CREEP_FLASHLIGHT][DEATH] " +
                $"id={Object.Id} " +
                $"zone={Zone}",
                this);
#endif
        }

        private void BeginFlee(Vector3 threatPosition, float seconds)
        {
            if (!TryFindFleeDestination(threatPosition, out _fleeDestination))
            {
                _fleeDestination = transform.position;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // Debug.LogWarning($"[CREEP_FLEE][NO_DESTINATION] id={Object.Id} position={transform.position}", this);
#endif
            }

            _agent.speed = fleeSpeed;
            StateValue = (int)CreepMinionState.Flee;
            FleeTimer = TickTimer.CreateFromSeconds(Runner, seconds);
        }

        private void BeginFleeTo(
            Vector3 destination,
            float seconds)
        {
            if (NavMesh.SamplePosition(
                    destination,
                    out var hit,
                    2f,
                    NavMesh.AllAreas))
            {
                _fleeDestination = hit.position;
            }
            else
            {
                _fleeDestination = destination;
            }

            _agent.speed = fleeSpeed;
            StateValue = (int)CreepMinionState.Flee;
            FleeTimer = TickTimer.CreateFromSeconds(Runner, seconds);
            _agent.SetDestination(_fleeDestination);
        }

        private bool TryFindFleeDestination(Vector3 threatPosition, out Vector3 destination)
        {
            destination = transform.position;
            if (!NavMesh.SamplePosition(transform.position, out var originHit, 2f, NavMesh.AllAreas)) return false;

            var away = Vector3.ProjectOnPlane(transform.position - threatPosition, Vector3.up);
            if (away.sqrMagnitude < 0.01f)
            {
                Vector2 random = UnityEngine.Random.insideUnitCircle.normalized;
                away = new Vector3(random.x, 0f, random.y);
            }
            else
            {
                away.Normalize();
            }

            var path = new NavMeshPath();
            for (int attempt = 0; attempt < 16; attempt++)
            {
                Vector3 direction = Quaternion.Euler(0f, UnityEngine.Random.Range(-40f, 40f), 0f) * away;
                float distance = UnityEngine.Random.Range(fleeMinDistance, coreCarryDistance);
                Vector3 candidate = transform.position + direction * distance;
                if (!NavMesh.SamplePosition(candidate, out var hit, 2.5f, NavMesh.AllAreas)) continue;
                if (Vector3.Distance(originHit.position, hit.position) < fleeMinDistance) continue;
                if (!NavMesh.CalculatePath(originHit.position, hit.position, NavMesh.AllAreas, path)
                    || path.status != NavMeshPathStatus.PathComplete) continue;

                destination = hit.position;
                return true;
            }

            return false;
        }

        public void ReleaseStolenCoreAuthoritative()
        {
            if (Object == null || !Object.IsValid || !Object.HasStateAuthority) return;
            if (StolenCoreId.IsValid && Runner.TryFindObject(StolenCoreId, out var obj)
                && obj != null && obj.TryGetComponent<NetworkPickupItem>(out var core))
            {
                var position = NavMesh.SamplePosition(transform.position, out var hit, 1.5f, NavMesh.AllAreas)
                    ? hit.position + Vector3.up * 0.1f : transform.position;
                core.TryEndMonsterCarryAuthoritative(Object.Id, position, Quaternion.identity);
            }
            StolenCoreId = default;
            FleeTimer = TickTimer.None;
            ResetTracking();
            StateValue = (int)CreepMinionState.Roam;
        }

        private void ResetTracking()
        {
            TargetPlayer = PlayerRef.None;
            _trackSeconds = 0f;
            _lostSightSeconds = 0f;
            _stalkerAlertDeliveredForTarget = false;
        }

        private bool TryGetFlashlightSource(
            out Vector3 source)
        {
            source = default;
            bool anySource = false;

            foreach (var player
                     in Runner.ActivePlayers)
            {
                if (!Runner.TryGetPlayerObject(
                        player,
                        out var playerObject)
                    || playerObject == null
                    || !playerObject.TryGetComponent<
                        NetworkPlayerFlashlight>(
                        out var flashlight)
                    || !flashlight.IsEmittingLight)
                {
                    continue;
                }

                Transform beam =
                    flashlight.BeamTransform;

                if (beam == null)
                {
                    continue;
                }

                // Ponytail:
                // vị trí đèn có thể reuse từ child hiện tại,
                // nhưng hướng gameplay phải lấy từ state authoritative,
                // không lấy rotation do Render/LateUpdate điều khiển.
                Vector3 beamOrigin =
                    beam.position;

                Vector3 beamForward =
                    beam.forward;

                if (playerObject.TryGetComponent<
                        NetworkPlayerMovement>(
                        out var movement))
                {
                    beamForward =
                        Quaternion.Euler(
                            movement.CurrentPitch,
                            playerObject.transform.eulerAngles.y,
                            0f)
                        * Vector3.forward;
                }

                // Aim vào chính giữa collider Minion.
                Vector3 targetPoint =
                    _bodyCollider != null
                        ? _bodyCollider.bounds.center
                        : transform.position
                          + Vector3.up * 0.6f;

                Vector3 ray =
                    targetPoint - beamOrigin;

                float distance =
                    ray.magnitude;

                if (distance <= 0.01f)
                {
                    continue;
                }

                float maxRange =
                    Mathf.Min(
                        flashlight.BeamRange,
                        flashlightKillRange);

                if (distance > maxRange)
                {
                    continue;
                }

                float angle =
                    Vector3.Angle(
                        beamForward,
                        ray);

                float halfAngle =
                    flashlight.BeamSpotAngle
                    * 0.5f;

                if (angle > halfAngle)
                {
                    continue;
                }

                var hits =
                    Physics.RaycastAll(
                        beamOrigin,
                        ray.normalized,
                        distance + 0.5f,
                        ~0,
                        QueryTriggerInteraction.Ignore);

                Array.Sort(
                    hits,
                    (left, right) =>
                        left.distance.CompareTo(
                            right.distance));

                foreach (var hit in hits)
                {
                    // Bỏ collider của chính Player
                    // đang cầm flashlight.
                    if (hit.collider.transform
                        .IsChildOf(
                            playerObject.transform))
                    {
                        continue;
                    }

                    // Collider đầu tiên sau Player là Minion
                    // => flashlight thực sự có line of sight.
                    var hitMinion =
                        hit.collider
                            .GetComponentInParent<
                                CreepMinionRuntime>();

                    if (hitMinion == this)
                    {
                        if (!anySource)
                            source = playerObject.transform.position;
                        anySource = true;
                        MatchAuthorityRuntime.Instance?.RecordMinionFact(
                            AEDMinionFactKindV1.FlashlightContribution,
                            Object.Id,
                            $"flashlight:{Object.Id}:{player.PlayerId}:{Runner.Tick.Raw}",
                            Runner.Tick.Raw, player, effectKind: "FLASHLIGHT_BEAM",
                            accepted: true, seconds: Runner.DeltaTime);

#if false
                        // Debug.Log(
                            $"[CREEP_FLASHLIGHT][HIT] " +
                            $"id={Object.Id} " +
                            $"state={State} " +
                            $"player={player} " +
                            $"distance={distance:F1}/" +
                            $"{maxRange:F1} " +
                            $"angle={angle:F1}/" +
                            $"{halfAngle:F1}",
                            this);
#endif

                        break;
                    }

                    // Vật khác chắn beam.
                    break;
                }
            }

            return anySource;
        }

        private void PublishAEDSnapshot()
        {
            if (Runner == null || !Runner.IsServer || Object == null
                || !Object.HasStateAuthority || !Object.IsValid)
                return;
            MatchAuthorityRuntime.Instance?.RecordMinionSnapshot(Object.Id,
                Zone.ToString(), State.ToString(), TargetPlayer,
                Runner.Tick.Raw, Runner.TickRate);
        }

        private PlayerRef ResolveNoiseSourcePlayer(string sourcePlayerId)
        {
            if (Runner == null || string.IsNullOrWhiteSpace(sourcePlayerId))
                return PlayerRef.None;
            foreach (var player in Runner.ActivePlayers)
                if (string.Equals(player.ToString(), sourcePlayerId,
                        StringComparison.Ordinal))
                    return player;
            return PlayerRef.None;
        }

        public override void Render()
        {
            // Lazy-init visual root.
            if (_visualRoot == null)
            {
                _visualRoot = transform.Find("Visual");
                if (_visualRoot != null)
                {
                    _visualInitialScale = _visualRoot.localScale;
                    _visualBaseLocalPosition = _visualRoot.localPosition;
                }
            }

            if (IsDying)
            {
                float remaining = DeathTimer.RemainingTime(Runner) ?? 0f;
                float duration = Mathf.Max(0.1f, deathVanishSeconds);
                float normalized = 1f - Mathf.Clamp01(remaining / duration);
                float scale = 1f - normalized; // 1 → 0

                if (_visualRoot != null)
                    _visualRoot.localScale = _visualInitialScale * scale;

                return;
            }

            if (_visualRoot != null && !IsDying)
            {
                float bob = Mathf.Sin(Time.time * flightBobSpeed) * flightBobAmplitude;
                float resolvedHeight = FlightHeightValue > 0f
                    ? FlightHeightValue
                    : flightHeight;
                float parentScaleY =
                    _visualRoot.parent != null
                        ? Mathf.Max(
                            0.001f,
                            Mathf.Abs(
                                _visualRoot.parent.lossyScale.y))
                        : 1f;
                float localHeight = resolvedHeight / parentScaleY;
                float localBob = bob / parentScaleY;
                _visualRoot.localPosition =
                    _visualBaseLocalPosition
                    + Vector3.up * (localHeight + localBob);
            }

            if (_animators == null || _animators.Length == 0) _animators = GetComponentsInChildren<Animator>(true);
            if (AttackSequence != _renderedAttackSequence)
            {
                _renderedAttackSequence = AttackSequence;
                    CrossFade(
                        AttackKind == CreepMinionAttackKind.StealTool
                        || AttackKind == CreepMinionAttackKind.StealCore
                        ? "Attack2"
                        : "Shoot");
                _actionAnimationUntil = Time.time + attackAnimationLockSeconds;
                return;
            }
            if (Time.time < _actionAnimationUntil) return;
            const string desired = "Run";
            if (desired == _currentLocomotionAnimation) return;
            _currentLocomotionAnimation = desired;
            CrossFade(desired);
        }

        private void UpdateFlightHeightAuthoritative()
        {
            if (Time.time < _nextCeilingProbeAt)
                return;

            _nextCeilingProbeAt = Time.time + 0.05f;
            FlightHeightValue = ResolveFlightHeight();
        }

        private float ResolveFlightHeight()
        {
            float visualTopOffset = _visualTopOffset;

            if (_visualRoot != null
                && TryGetVisualBounds(out var visualBounds))
            {
                visualTopOffset = Mathf.Max(
                    0f,
                    visualBounds.max.y - _visualRoot.position.y);
            }

            var hits = Physics.RaycastAll(
                transform.position + Vector3.up * 0.1f,
                Vector3.up,
                ceilingProbeDistance,
                ~0,
                QueryTriggerInteraction.Ignore);

            Array.Sort(
                hits,
                (left, right) =>
                    left.distance.CompareTo(right.distance));
            foreach (var hit in hits)
            {
                if (hit.collider == null
                    || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                return Mathf.Max(
                    0.3f,
                    0.1f
                    + hit.distance
                    - ceilingClearance
                    - visualTopOffset
                    - flightBobAmplitude);
            }

            return Mathf.Max(
                flightHeight,
                0.3f);
        }

        private bool TryGetVisualBounds(out Bounds bounds)
        {
            bounds = default;
            bool found = false;

            foreach (var renderer in _visualRenderers)
            {
                if (renderer == null)
                    continue;

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found;
        }

        private void LateUpdate()
        {
            if (!(_bodyCollider is CapsuleCollider capsule)
                || !TryGetVisualBounds(out var bounds))
            {
                return;
            }

            capsule.center = transform.InverseTransformPoint(bounds.center);
            capsule.height = Mathf.Max(
                capsule.radius * 2f,
                bounds.size.y / Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.y)));
        }

        private void CrossFade(string state)
        {
            foreach (var animator in _animators)
                if (animator != null) animator.CrossFade(state, 0.05f);
        }
    }
}
