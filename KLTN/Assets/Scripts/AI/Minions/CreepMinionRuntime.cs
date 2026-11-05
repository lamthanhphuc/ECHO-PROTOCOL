using System;
using System.Collections.Generic;
using EchoProtocol.AI.Listener.Noise;
using EchoProtocol.AI.Stalker;
using EchoProtocol.AI.Stalker.Spatial;
using EchoProtocol.MatchFlow;
using EchoProtocol.Networking;
using EchoProtocol.Networking.Authority;
using Fusion;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace EchoProtocol.AI.Minions
{
    public enum CreepMinionState { Roam = 0, Track = 1, Harass = 2, Flee = 3 }
    public enum CreepMinionRole { Original = 0, Disruptor = 1, Catfish = 2 }
    public enum FishcatEncounterDuty { ActiveHunter = 0, Ambusher = 1, Roamer = 2 }
    public enum CreepMinionAttackKind
    {
        None = 0,
        ShootSlow = 1,
        StealTool = 2,
        StealCore = 3,
        MeleeSlow = 4
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class CreepMinionRuntime : NetworkBehaviour
    {
        private static readonly HashSet<CreepMinionRuntime> PurgeFishcats = new HashSet<CreepMinionRuntime>();
        [Networked] public int EncounterDutyValue { get; private set; }
        private Vector3 _purgeActivityFocus;
        private bool _hasPurgeActivityFocus;
        private float _lastNearPlayerAt;
        private float _nextPurgePatrolAt;

        public void SetPurgeEncounter(FishcatEncounterDuty duty, Vector3 activityPosition)
        {
            if (!IsPurgeCatfish || Object == null || !Object.HasStateAuthority) return;
            EncounterDutyValue = (int)duty;
            // A coarse area anchor, never a replacement for visual detection.
            _purgeActivityFocus = new Vector3(Mathf.Round(activityPosition.x / 12f) * 12f,
                activityPosition.y, Mathf.Round(activityPosition.z / 12f) * 12f);
            _hasPurgeActivityFocus = true;
        }

        private int OtherHunters(PlayerRef player)
        {
            int count = 0;
            foreach (var minion in PurgeFishcats)
                if (minion != null && minion != this && minion.Runner == Runner
                    && minion.Object != null && minion.Object.IsValid && !minion.IsDying
                    && minion.TargetPlayer == player) count++;
            return count;
        }

        private bool HasPurgeUvContributor()
        {
            foreach (var player in Runner.ActivePlayers)
                if (Runner.TryGetPlayerObject(player, out var obj) && obj != null
                    && obj.TryGetComponent<NetworkPlayerFlashlight>(out var uv)
                    && uv.ResonanceActive && uv.ResonanceTarget == this) return true;
            return false;
        }

        public static bool IsPositionObserved(NetworkRunner runner, Vector3 point, Transform ignoredRoot = null)
        {
            foreach (var player in runner.ActivePlayers)
            {
                if (!runner.TryGetPlayerObject(player, out var obj) || obj == null || !obj.IsValid
                    || !obj.TryGetComponent<LobbyPlayerState>(out var lobby) || !lobby.IsGameplayPlayer) continue;
                Vector3 eye = obj.transform.position + Vector3.up * 1.6f;
                Vector3 offset = point - eye;
                if (offset.sqrMagnitude < 9f) return true;
                if (offset.sqrMagnitude > 120f * 120f) continue;
                float pitch = obj.TryGetComponent<NetworkPlayerMovement>(out var movement) ? movement.CurrentPitch : 0f;
                Vector3 forward = Quaternion.Euler(pitch, obj.transform.eulerAngles.y, 0f) * Vector3.forward;
                if (Vector3.Angle(forward, offset) > 75f) continue;
                bool blocked = false;
                foreach (var hit in Physics.RaycastAll(eye, offset.normalized, offset.magnitude,
                             Physics.DefaultRaycastLayers & ~(1 << 4), QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.transform.IsChildOf(obj.transform)
                        || ignoredRoot != null && hit.collider.transform.IsChildOf(ignoredRoot)) continue;
                    // Players/minions can move before the next frame: do not use them as cover.
                    if (hit.collider.GetComponentInParent<NetworkPlayerMovement>() != null
                        || hit.collider.GetComponentInParent<CreepMinionRuntime>() != null) continue;
                    blocked = true;
                    break;
                }
                if (!blocked) return true;
            }
            return false;
        }

        public bool CanRecyclePurgeFishcat()
        {
            if (!IsPurgeCatfish || IsDying || State != CreepMinionState.Roam || TargetPlayer.IsRealPlayer
                || !ReplicatedDistractionTimer.ExpiredOrNotRunning(Runner) || !AttackRecovery.ExpiredOrNotRunning(Runner)
                || Time.time - _lastVisiblePlayerAt < 90f || Time.time - _lastNearPlayerAt < 90f
                || HasPurgeUvContributor() || FindBestVisibleTarget().IsRealPlayer) return false;
            foreach (var player in Runner.ActivePlayers)
                if (Runner.TryGetPlayerObject(player, out var obj) && obj != null && obj.IsValid
                    && obj.TryGetComponent<LobbyPlayerState>(out var lobby) && lobby.IsGameplayPlayer
                    && Vector3.Distance(transform.position, obj.transform.position) < 65f) return false;
            return !IsPositionObserved(Runner, transform.position + Vector3.up, transform)
                && !IsPositionObserved(Runner, transform.position + Vector3.up * 2f, transform);
        }

        private bool TryPurgePatrolDestination(out Vector3 destination)
        {
            destination = transform.position;
            if (!IsPurgeCatfish || !_hasPurgeActivityFocus) return false;
            bool stale = Time.time - _lastVisiblePlayerAt >= 50f;
            float radius = stale ? 10f : EncounterDutyValue == (int)FishcatEncounterDuty.Ambusher ? 8f
                : EncounterDutyValue == (int)FishcatEncounterDuty.ActiveHunter ? 16f : 32f;
            for (int i = 0; i < 12; i++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * radius;
                Vector3 candidate = _purgeActivityFocus + new Vector3(offset.x, 0f, offset.y);
                if (_catfishOnLand && EncounterDutyValue == (int)FishcatEncounterDuty.Ambusher
                    && FloodedWaterBoundary.TryGetSwimSurface(candidate, out _, .1f)
                    && CrossesMap2Water(candidate) && !IsMovementStepBlocked(candidate))
                {
                    _swimDestination = candidate;
                    _catfishOnLand = false;
                    _catfishWasInWater = false;
                    _catfishPatrolCrossing = true;
                    destination = candidate;
                    return true;
                }
                if (!_catfishOnLand && FloodedWaterBoundary.TryGetSwimSurface(candidate, out _))
                { destination = candidate; return true; }
                if (_catfishOnLand && NavMesh.SamplePosition(candidate, out var hit, 3f, _agent.areaMask))
                {
                    var path = new NavMeshPath();
                    if (NavMesh.CalculatePath(transform.position, hit.position, _agent.areaMask, path)
                        && path.status == NavMeshPathStatus.PathComplete)
                    { destination = hit.position; return true; }
                }
            }
            return false;
        }
        private const float ResonanceDeathSeconds = 1f;
        private const float PlayerSeekTimeoutSeconds = 20f;
        private const float ToolDiveHeight = 0.35f;

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
        [SerializeField, Min(0.5f)] private float meleeRange = 2.2f;
        [SerializeField] private float shootRange = 10f;
        [SerializeField] private float stealToolRange = 3f;
        [SerializeField, Min(1f)] private float attackDamage = 20f;
        [SerializeField, Min(0f)] private float attackKnockbackSpeed = 10f;
        [SerializeField, Min(0.05f)] private float attackKnockbackSeconds = 0.3f;
        [SerializeField, Min(0.1f)] private float attackCooldownSeconds = 0.65f;
        [SerializeField, Min(0.1f)] private float attackRecoverySeconds = 3f;
        [SerializeField, Min(0.5f)] private float toolFleeSeconds = 1.6f;

        [Header("Hit Effects")]
        [SerializeField] private float slowMultiplier = 0.65f;
        [SerializeField] private float slowDurationSeconds = 3f;
        [SerializeField] private float coreCarryDistance = 12f;
        [SerializeField] private float coreCarryTimeoutSeconds = 3f;
        [SerializeField] private float coreCarryHeight = 0.65f;
        [SerializeField, Min(1f)] private float fleeMinDistance = 4f;
        [SerializeField, Min(1f)] private float stolenToolDropMinDistance = 12f;
        [SerializeField, Min(1f)] private float stolenToolDropMaxDistance = 45f;

        [Header("Alert")]
        [SerializeField] private float alertCooldownSeconds = 4f;
        [SerializeField, Min(0.05f)] private float alertRetrySeconds = 0.15f;

        [Header("Presentation")]
        [SerializeField, Min(0.1f)] private float animationSpeed = 1.6f;
        [SerializeField, Min(0.1f)] private float attackAnimationLockSeconds = 0.35f;

        [Header("Resonance counterplay")]
        [SerializeField, Min(0.1f)]
        private float deathVanishSeconds = 0.5f;
        [SerializeField, Min(0f)] private float catfishDeathPoseHoldSeconds = 1f;
        [SerializeField, Min(0.1f)] private float catfishDeathFadeSeconds = 1.5f;
        [SerializeField] private float noiseMakerDistractionSeconds = 12f;
        [SerializeField] private float noiseMakerArrivalDistance = 1.5f;

        [Networked] public int StateValue { get; private set; }
        [Networked] public int ZoneValue { get; private set; }
        [Networked] public int RoleValue { get; private set; }
        [Networked] public int VisualVariantValue { get; private set; }
        [Networked] public PlayerRef TargetPlayer { get; private set; }
        [Networked] public NetworkId StolenCoreId { get; private set; }
        [Networked] public NetworkId StolenToolId { get; private set; }
        [Networked] private int ToolDivePhase { get; set; }
        [Networked] private TickTimer ToolDiveTimer { get; set; }
        [Networked] private float ReplicatedDiveDuration { get; set; }
        [Networked] private float ReplicatedDiveStartHeight { get; set; }
        [Networked] private NetworkBool ReplicatedToolContactResolved { get; set; }
        private Transform _toolHand;
        private float _renderFlightHeight = -1f;
        [Networked] public NetworkBool IsMoving { get; private set; }
        [Networked] public uint AttackSequence { get; private set; }
        [Networked] public uint ConfirmedHitSequence { get; private set; }
        [Networked] public int AttackKindValue { get; private set; }
        [Networked] public float FlightHeightValue { get; private set; }
        [Networked] public uint AlertSequence { get; private set; }
        [Networked] private TickTimer AttackCooldown { get; set; }
        [Networked] private TickTimer AttackRecovery { get; set; }
        [Networked] private TickTimer AlertCooldown { get; set; }
        [Networked] private TickTimer FleeTimer { get; set; }

        [Networked] public NetworkBool IsDying { get; private set; }
        [Networked] private TickTimer DeathTimer { get; set; }

        public CreepMinionAttackKind AttackKind =>
            AttackKindValue >= 0 && AttackKindValue <= 4
                ? (CreepMinionAttackKind)AttackKindValue
                : CreepMinionAttackKind.None;

        public CreepMinionState State => StateValue >= 0 && StateValue <= 3
            ? (CreepMinionState)StateValue : CreepMinionState.Roam;
        public CreepMinionRole Role => RoleValue >= 0 && RoleValue <= 2
            ? (CreepMinionRole)RoleValue : CreepMinionRole.Original;
        private bool IsPurgeCatfish => Role == CreepMinionRole.Catfish;
        private bool IsDisruptor => Role == CreepMinionRole.Disruptor || IsPurgeCatfish;
        private float DisruptorAttackRange => Mathf.Min(meleeRange, 2f);
        private bool IsCatfish => IsPurgeCatfish || IsDisruptor && SceneManager.GetActiveScene().name == LobbyMapCatalog.ExplorationScene;
        private bool IsMap2Flyer => !IsDisruptor && SceneManager.GetActiveScene().name == LobbyMapCatalog.ExplorationScene;
        public RegionSemanticZone Zone => ZoneValue == (int)RegionSemanticZone.Zone01
            || ZoneValue == (int)RegionSemanticZone.Zone02
            ? (RegionSemanticZone)ZoneValue : RegionSemanticZone.Unknown;

        private NavMeshAgent _agent;
        private StalkerNavigationController _navigation;
        private Animator[] _animators;
        private HostRuntimeNoiseService _noiseService;
        [Networked] private float ReplicatedTrackSeconds { get; set; }
        [Networked] private float ReplicatedLostSightSeconds { get; set; }
        [Networked] private float ReplicatedFlashlightExposureSeconds { get; set; }
        [Networked] private float ReplicatedResonanceGrace { get; set; }
        private TickTimer _spearContactTimer;
        private PlayerRef _spearVictim;
        [Networked] public Vector3 ConfirmedHitPosition { get; private set; }
        private int _resonanceContributors;
        private float ResonanceSpeedMultiplier => _resonanceContributors > 0 ? .6f : 1f;
        [Networked] public float ResonanceProgress { get; private set; }
        [Networked] public NetworkBool IsUvStunned { get; private set; }
        [Networked] private NetworkBool ReplicatedSeekingPlayer { get; set; }
        private float _lastVisiblePlayerAt;
        private bool _seekingPlayer;
        private float _distractionUntil;
        private Vector3 _catfishSearchPosition;
        private float _nextCatfishSearchAt;
        [Networked] private NetworkBool ReplicatedToolDiveApproaching { get; set; }
        [Networked] private NetworkBool ReplicatedToolDiveStealRequested { get; set; }
        [Networked] private TickTimer ReplicatedToolDiveApproachTimer { get; set; }
        private Vector3 _distractionPoint;
        private float _nextTargetRefreshAt;
        private float _nextRoamRetargetAt;
        [Networked] private TickTimer ReplicatedDistractionTimer { get; set; }
        [Networked] private Vector3 ReplicatedLastKnownTargetPosition { get; set; }
        [Networked] private Vector3 ReplicatedFleeDestination { get; set; }
        [Networked] private int ReplicatedToolRouteRetries { get; set; }
        [Networked] private Vector3 ReplicatedToolVictimPosition { get; set; }
        [Networked] private Vector3 ReplicatedDistractionPoint { get; set; }
        private long _alertOrdinal;
        private uint _renderedAttackSequence;
        private uint _renderedAlertSequence;
        private float _actionAnimationUntil;
        private string _currentLocomotionAnimation;
        private bool _deathAnimationPlayed;
        private bool _catfishDeathPoseFrozen;
        public float DeathVisualAlpha { get; private set; } = 1f;
        private Material[] _catfishFadeMaterials;
        private Color[] _catfishFadeBaseColors;
        private bool _stalkerAlertDeliveredForTarget;
        private Transform _visualRoot;
        private Vector3 _visualBaseLocalPosition;
        private Collider _bodyCollider;
        private Vector3 _bodyColliderBaseCenter;
        private Renderer[] _visualRenderers;
        private float _nextCeilingProbeAt;
        private float _visualTopOffset;
        private Vector3 _swimDestination;
        private Vector3 _catfishLandEntry;
        private bool _hasCatfishLandEntry;
        private bool _catfishOnLand;
        public bool IsOnLand => !FloodedWaterBoundary.TryGetSwimSurface(transform.position, out _, 0.1f);
        private bool _catfishWasInWater;
        private bool _catfishPatrolCrossing;
        private Vector3 _map2FlightDestination;
        private bool _map2Flying;
        private bool _map2FlightEnteredWater;
        private float _recoveryDetourUntil;
        private Vector3 _recoveryFlightDestination;
        private readonly NavigationProgressMonitor _stationaryRecovery = new NavigationProgressMonitor(
            new NavigationProgressSettings(.25f, .05f, .05f, 4.75f, 5f));

        public void ConfigureBeforeSpawn(RegionSemanticZone zone) => ZoneValue = (int)zone;

        public void ConfigureBeforeSpawn(RegionSemanticZone zone, CreepMinionRole role, int visualVariant)
        {
            ZoneValue = (int)zone;
            RoleValue = (int)role;
            VisualVariantValue = Mathf.Clamp(visualVariant, 0, 29);
        }

        public override void Spawned()
        {
            _agent = GetComponent<NavMeshAgent>();
            ApplyVisualVariant();
            _animators = GetComponentsInChildren<Animator>();
            foreach (var animator in _animators)
            {
                if (animator != null)
                {
                    animator.speed = animationSpeed;
                }
            }
            _bodyCollider = GetComponent<Collider>();
            IgnorePlayerBodyCollisions();
            if (IsDisruptor)
            {
                _agent.radius = 0.65f;
                if (IsCatfish)
                {
                    float bakedRadius = NavMesh.GetSettingsByID(_agent.agentTypeID).agentRadius;
                    if (bakedRadius > 0f) _agent.radius = bakedRadius;
                }
                _agent.height = 1f;
                if (_bodyCollider is CapsuleCollider capsule)
                {
                    capsule.center = Vector3.up * 0.5f;
                    capsule.radius = Mathf.Min(_agent.radius, 0.5f);
                    capsule.height = _agent.height;
                }
            }
            if (_bodyCollider is CapsuleCollider bodyCapsule)
                _bodyColliderBaseCenter = bodyCapsule.center;
            _renderedAttackSequence = AttackSequence;
            _renderedAlertSequence = AlertSequence;
            _deathAnimationPlayed = false;
            _catfishDeathPoseFrozen = false;
            DeathVisualAlpha = 1f;
            _lastVisiblePlayerAt = Time.time;
            _lastNearPlayerAt = Time.time;
            _nextPurgePatrolAt = 0f;
            _hasPurgeActivityFocus = false;
            if (IsPurgeCatfish && Object.HasStateAuthority) PurgeFishcats.Add(this);
            _seekingPlayer = false;
            _hasCatfishLandEntry = false;
            _catfishOnLand = IsCatfish
                && !FloodedWaterBoundary.TryGetSwimSurface(transform.position, out _, 0.1f);
            _catfishWasInWater = !_catfishOnLand;
            _catfishPatrolCrossing = false;

            _visualRoot ??= transform.Find("Visual");
            if (_visualRoot != null)
            {
                _visualBaseLocalPosition = _visualRoot.localPosition;
                _visualRenderers = _visualRoot.GetComponentsInChildren<Renderer>(true);
                if (TryGetVisualBounds(out var visualBounds) && visualBounds.size.y > 0.01f)
                {
                    _visualRoot.localScale *= 1f / visualBounds.size.y;
                    if (TryGetVisualBounds(out visualBounds))
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

            if (!NetworkBootstrap.IsHostMigrationResumeActive)
            {
                var authority = MatchAuthorityRuntime.Instance;
                if (authority != null &&
                    authority.TryGetRemoteFixedBalance(out var snapshot))
                {
                    roamSpeed = snapshot.minion.roamSpeed;
                    trackSpeed = snapshot.minion.trackSpeed;
                    attackDamage = snapshot.minion.attackDamage;
                    Debug.Log(
                        $"[RemoteFixed] Minion applied: roam={roamSpeed}, " +
                        $"track={trackSpeed}, damage={attackDamage}");
                }
            }

            if (!global::EchoProtocol.Networking.NetworkBootstrap.IsHostMigrationResumeActive)
            {
                IsDying = false;
                IsUvStunned = false;
                ReplicatedSeekingPlayer = false;
                DeathTimer = TickTimer.None;
                StateValue = (int)CreepMinionState.Roam;
                FlightHeightValue = IsDisruptor ? 0f : flightHeight;
                TargetPlayer = PlayerRef.None;
                StolenCoreId = default;
                StolenToolId = default;
                ToolDivePhase = 0;
                ToolDiveTimer = TickTimer.None;
                AttackCooldown = TickTimer.None;
                AttackRecovery = TickTimer.None;
                AlertCooldown = TickTimer.None;
                FleeTimer = TickTimer.None;
            }
            if (global::EchoProtocol.Networking.NetworkBootstrap.IsHostMigrationResumeActive)
            {
                _seekingPlayer = ReplicatedSeekingPlayer;
                Debug.Log(
                    $"[HM_AI_MINION_RESTORE]|id={Object.Id}|state={State}|target={TargetPlayer}|core={StolenCoreId}|tool={StolenToolId}|flee={ReplicatedFleeDestination}|divePhase={ToolDivePhase}|diveResolved={ReplicatedToolContactResolved}|noiseActive={!ReplicatedDistractionTimer.ExpiredOrNotRunning(Runner)}");
            }
            _noiseService = HostRuntimeNoiseService.EnsureExists(MatchAuthorityRuntime.Instance);
            _noiseService.RuntimeNoiseAccepted += HandleRuntimeNoiseAccepted;
            _agent.enabled = false;
            _agent.updatePosition = true;
            _agent.updateRotation = true;
            _agent.angularSpeed = 1080f;
            _agent.acceleration = 40f;
            _agent.stoppingDistance = IsDisruptor ? 1.8f : 0.4f;
            if ((!IsPurgeCatfish || _catfishOnLand) && !TryActivateAgent())
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
            if (IsMovementStepBlocked(hit.position, IsDisruptor ? 0f : FlightHeightValue)) return false;
            transform.position = hit.position;
            _agent.enabled = true;
            if (!_agent.isOnNavMesh)
            {
                _agent.enabled = false;
                return false;
            }

            _navigation ??= new StalkerNavigationController(_agent);
            _navigation.ClearDestinationCache();
            _navigation.SetAuthoritativeLocomotion(true);
// #if UNITY_EDITOR || DEVELOPMENT_BUILD
//             Debug.Log($"[CREEP_SPAWN][READY] id={Object.Id} zone={Zone} position={transform.position}", this);
// #endif
            return true;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _spearContactTimer = TickTimer.None;
            ReleaseCatfishFadeMaterials();
            PurgeFishcats.Remove(this);
            _navigation?.SetAuthoritativeLocomotion(false);
            _navigation = null;
            if (_noiseService != null) _noiseService.RuntimeNoiseAccepted -= HandleRuntimeNoiseAccepted;
            if (hasState && Object.HasStateAuthority)
            {
                ReleaseStolenToolAuthoritative();
                ReleaseStolenCoreAuthoritative();
            }
        }

        private void HandleRuntimeNoiseAccepted(RuntimeNoiseEvent noiseEvent)
        {
            if (!Object.HasStateAuthority || noiseEvent.NoiseType != RuntimeNoiseType.NOISE_MAKER
                || StolenCoreId.IsValid || Vector3.Distance(transform.position, noiseEvent.WorldPosition) > noiseEvent.HearingRadius)
            {
                return;
            }
            ReplicatedDistractionPoint = noiseEvent.WorldPosition;
            ReplicatedDistractionTimer = TickTimer.CreateFromSeconds(Runner, noiseMakerDistractionSeconds);
            ResetTracking();
            StateValue = (int)CreepMinionState.Roam;
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority || _agent == null) return;

            try
            {

            // Dying thực sự phải đứng trước
            // mọi NavMesh/AI activation.
            if (IsDying)
            {
                _spearContactTimer = TickTimer.None;
                IsMoving = false;
                if (DeathTimer.Expired(Runner))
                {
                    Runner.Despawn(Object);
                }
                return;
            }

            if (IsPurgeCatfish)
            {
                foreach (var player in Runner.ActivePlayers)
                    if (Runner.TryGetPlayerObject(player, out var nearby) && nearby != null && nearby.IsValid
                        && nearby.TryGetComponent<LobbyPlayerState>(out var lobby) && lobby.IsGameplayPlayer
                        && Vector3.Distance(transform.position, nearby.transform.position) < 30f)
                        _lastNearPlayerAt = Time.time;
                if (TargetPlayer.IsRealPlayer && OtherHunters(TargetPlayer) >= PurgeFishcatBalance.HuntersPerPlayer)
                    ResetTracking();
            }

            _resonanceContributors=0;
            foreach(var player in Runner.ActivePlayers)
                if(Runner.TryGetPlayerObject(player,out var playerObject) && playerObject!=null
                    && playerObject.TryGetComponent<NetworkPlayerFlashlight>(out var emitter)
                    && emitter.ResonanceActive && emitter.ResonanceTarget==this) _resonanceContributors++;
            bool illuminated = _resonanceContributors>0;
            IsUvStunned = illuminated;

            var exposure=ResonanceRules.TickExposure(new ResonanceExposureState { Seconds=ReplicatedFlashlightExposureSeconds,Grace=ReplicatedResonanceGrace },_resonanceContributors,Runner.DeltaTime);
            // PURGE has a longer burn threshold than the shared exposure rule's two-second cap.
            if (IsPurgeCatfish && illuminated)
                exposure.Seconds = ReplicatedFlashlightExposureSeconds + Runner.DeltaTime * ResonanceRules.ContributionSpeed(_resonanceContributors);
            ReplicatedFlashlightExposureSeconds=exposure.Seconds;
            ReplicatedResonanceGrace=exposure.Grace;

#if false
            if (illuminated)
            {
                // Debug.Log(
                    // $"[CREEP_FLASHLIGHT][EXPOSURE] " +
                    // $"id={Object.Id} " +
                    // $"exposure={ReplicatedFlashlightExposureSeconds:F2}/" +
                    // $"{flashlightKillExposureSeconds:F2}",
                    // this);
            }
#endif

            float deathExposureSeconds = IsPurgeCatfish && NetworkMatchState.Instance != null
                ? NetworkMatchState.Instance.PurgeFishcatUvDeathSeconds : ResonanceDeathSeconds;
            ResonanceProgress=Mathf.Clamp01(ReplicatedFlashlightExposureSeconds/deathExposureSeconds);
            if (ReplicatedFlashlightExposureSeconds >= deathExposureSeconds)
            {
                BeginFlashlightDeath();
                return;
            }
            if (IsUvStunned)
            {
                _spearContactTimer = TickTimer.None;
                if (_agent.enabled && _agent.isOnNavMesh)
                {
                    _agent.isStopped = true;
                    _agent.ResetPath();
                }
                IsMoving = false;
                return;
            }
            TickSpearContact();
            // Authoritative locomotion owns the root; the agent only calculates paths.
            if (_agent.enabled && _agent.isOnNavMesh) _agent.isStopped = true;

            if (IsCatfish && !_catfishOnLand)
            {
                TickCatfishSwim();
                return;
            }
            if (_map2Flying)
            {
                TickMap2Flight();
                return;
            }

            if (!_agent.enabled && !TryActivateAgent())
            {
                IsMoving = false;
                return;
            }
            if (!_agent.isOnNavMesh && !TryActivateAgent())
            {
                IsMoving = false;
                return;
            }

            if (ToolDivePhase == 0 && !IsDisruptor) UpdateFlightHeightAuthoritative();

            if (ToolDivePhase != 0)
            {
                TickToolDive();
                TickNavigationLocomotion(Runner.DeltaTime);
                IsMoving = _navigation.AuthoritativeMoveSpeed > 0.2f;
                return;
            }
            if (State == CreepMinionState.Flee)
            {
                _agent.speed = fleeSpeed * ResonanceSpeedMultiplier;
                _navigation.TrySetDestination(ReplicatedFleeDestination);
                if (StolenCoreId.IsValid && Runner.TryFindObject(StolenCoreId, out var coreObject)
                    && coreObject != null && coreObject.TryGetComponent<NetworkPickupItem>(out var core))
                {
                    core.TryUpdateMonsterCarryPoseAuthoritative(Object.Id, CarryPosition(), transform.rotation);
                }
                bool reachedDropPoint = Vector3.Distance(transform.position, ReplicatedFleeDestination) <= 1f;
                if (StolenToolId.IsValid && !reachedDropPoint && FleeTimer.Expired(Runner))
                {
                    // A blocked route must not drop the tool back in the victim's room.
                    _navigation.RequestDestination(ReplicatedFleeDestination, NavigationRequestIntent.RecoveryRepath);
                    if (ReplicatedToolRouteRetries++ < 2 && TryFindSabotageDropPosition(ReplicatedToolVictimPosition, out var alternative))
                        ReplicatedFleeDestination = alternative;
                    else
                    {
                        // No reachable destination remains: return the tool to a recoverable
                        // world pickup instead of holding it indefinitely.
                        ReleaseStolenToolAuthoritative();
                    }
                    FleeTimer = TickTimer.CreateFromSeconds(Runner, 15f);
                }
                if (reachedDropPoint || !StolenToolId.IsValid && FleeTimer.Expired(Runner))
                {
                    ReleaseStolenToolAuthoritative();
                    ReleaseStolenCoreAuthoritative();
                }
            }
            else if (!ReplicatedDistractionTimer.ExpiredOrNotRunning(Runner) && !StolenCoreId.IsValid)
            {
                _agent.speed = trackSpeed * ResonanceSpeedMultiplier;
                TryNavigateOrFly(ReplicatedDistractionPoint);
                if (Vector3.Distance(transform.position, ReplicatedDistractionPoint) <= noiseMakerArrivalDistance)
                    ReplicatedDistractionTimer = TickTimer.None;
            }
            else
            {
                ReplicatedDistractionTimer = TickTimer.None;
                UpdateTargetAndMovement();
            }
            TickNavigationLocomotion(Runner.DeltaTime);
            _navigation.TickProgress(Runner.DeltaTime);
            var execution = _navigation.GetExecutionStatus();
            if (execution == NavigationExecutionStatus.Failed
                || execution == NavigationExecutionStatus.NoProgress
                || execution == NavigationExecutionStatus.Stuck)
            {
                RecoverFromBlockedNavigation();
            }
            IsMoving = _navigation.AuthoritativeMoveSpeed > 0.2f;
            }
            finally { TickStationaryRecovery(); }
        }

        private void TickStationaryRecovery()
        {
            if (!Object || !Object.IsValid) return;
            if (IsDying || IsUvStunned || ToolDivePhase != 0 || !AttackRecovery.ExpiredOrNotRunning(Runner)
                || (TryGetEligiblePlayer(TargetPlayer, out var target, out _, out _)
                    && Vector3.Distance(transform.position, target.transform.position) <= (IsDisruptor ? DisruptorAttackRange : trackStandOffDistance)
                    && HasLineOfSight(target)))
            { _stationaryRecovery.Reset(); return; }
            _stationaryRecovery.Observe(transform.position, 0f, Runner.DeltaTime);
            if (_stationaryRecovery.State != NavigationProgressState.Stuck) return;
            _stationaryRecovery.Reset();
            _nextRoamRetargetAt = _nextTargetRefreshAt = 0f;
            _distractionUntil = 0f;
            ReplicatedDistractionTimer = TickTimer.None;
            _hasCatfishLandEntry = _catfishPatrolCrossing = false;
            if ((IsCatfish && !_catfishOnLand) || _map2Flying)
            {
                // A sideways, collision-checked detour breaks repeated attempts into the same bank or wall.
                for (int i = 0; i < 8; i++)
                {
                    float angle = (i + UnityEngine.Random.Range(0f, 1f)) * Mathf.PI / 4f;
                    var candidate = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 2f;
                    if (IsMovementStepBlocked(candidate, IsCatfish ? 0f : FlightHeightValue)) continue;
                    if (IsCatfish && !FloodedWaterBoundary.TryGetSwimSurface(candidate, out _, .1f)) continue;
                    if (IsCatfish) { _swimDestination = candidate; _catfishPatrolCrossing = true; _recoveryDetourUntil = Time.time + 2f; }
                    else
                    {
                        _recoveryFlightDestination = _map2FlightDestination;
                        _map2FlightDestination = candidate;
                        _recoveryDetourUntil = Time.time + 2f;
                    }
                    return;
                }
                return;
            }
            _navigation ??= new StalkerNavigationController(_agent);
            var destination = target != null ? target.transform.position
                : _navigation.TryGetActiveDestination(out var active) ? active : transform.position;
            if (_navigation.TryRecoverLocalPosition(destination)) _navigation.SetAuthoritativeLocomotion(true);
            RecoverFromBlockedNavigation();
        }

        private void TickCatfishSwim()
        {
            if (_agent.enabled) _agent.enabled = false;
            bool currentlyInWater = FloodedWaterBoundary.TryGetSwimSurface(
                transform.position, out float surfaceY, 0.1f);
            if (!currentlyInWater && !_catfishPatrolCrossing && TryActivateAgent())
            {
                _catfishOnLand = true;
                _catfishWasInWater = _hasCatfishLandEntry = false;
                IsMoving = false;
                return;
            }
            if (IsPurgeCatfish && currentlyInWater) _catfishPatrolCrossing = false;
            _catfishWasInWater |= currentlyInWater;
            float swimY = currentlyInWater ? ResolveSwimHeight(surfaceY) : surfaceY - 0.9f;
            if (currentlyInWater && transform.position.y < swimY - 0.05f)
            {
                Vector3 risePosition = transform.position + Vector3.up * Mathf.Min(Runner.DeltaTime * 1.5f, swimY - transform.position.y);
                if (IsMovementStepBlocked(risePosition)) { IsMoving = false; return; }
                transform.position = risePosition;
                IsMoving = true;
                return;
            }

            Vector3 destination;
            if (ReplicatedDistractionTimer.ExpiredOrNotRunning(Runner) && Time.time >= _nextTargetRefreshAt)
            {
                _nextTargetRefreshAt = Time.time + targetRefreshSeconds;
                var visible = FindBestVisibleTarget();
                if (visible.IsRealPlayer)
                {
                    TargetPlayer = visible;
                    _lastVisiblePlayerAt = Time.time;
                    _seekingPlayer = false;
                    ReplicatedSeekingPlayer = false;
                    ReplicatedLastKnownTargetPosition = Runner.GetPlayerObject(visible).transform.position;
                }
                else if (!IsPurgeCatfish && Time.time - _lastVisiblePlayerAt >= PlayerSeekTimeoutSeconds)
                {
                    TargetPlayer = FindClosestEligibleTarget();
                    _seekingPlayer = TargetPlayer.IsRealPlayer;
                    ReplicatedSeekingPlayer = _seekingPlayer;
                    if (!_seekingPlayer) _lastVisiblePlayerAt = Time.time;
                }
            }
            if (!ReplicatedDistractionTimer.ExpiredOrNotRunning(Runner) && !StolenCoreId.IsValid)
            {
                StateValue = (int)CreepMinionState.Roam;
                destination = ReplicatedDistractionPoint;
                if (Vector3.Distance(transform.position, destination) <= noiseMakerArrivalDistance)
                {
                    _distractionUntil = 0f;
                    ReplicatedDistractionTimer = TickTimer.None;
                }
            }
            else if (TryGetEligiblePlayer(TargetPlayer, out var target, out _, out var life))
            {
                StateValue = (int)CreepMinionState.Harass;
                destination = IsPurgeCatfish ? ReplicatedLastKnownTargetPosition : target.transform.position;
                if (HasLineOfSight(target))
                {
                    destination = target.transform.position;
                    ReplicatedLastKnownTargetPosition = destination;
                    _lastVisiblePlayerAt = Time.time;
                    _seekingPlayer = false;
                    ReplicatedSeekingPlayer = false;
                    if (Vector3.Distance(transform.position, destination) <= meleeRange)
                        AttackPlayer(target, life);
                }
                else if (IsPurgeCatfish && Time.time - _lastVisiblePlayerAt > 4f)
                {
                    ResetTracking();
                    _swimDestination = transform.position;
                    _nextRoamRetargetAt = 0f;
                    StateValue = (int)CreepMinionState.Roam;
                }
            }
            else
            {
                StateValue = (int)CreepMinionState.Roam;
                if (!_catfishPatrolCrossing
                    && ((!IsPurgeCatfish && (_swimDestination - transform.position).sqrMagnitude < 2f)
                        || Time.time >= _nextRoamRetargetAt))
                {
                    _nextRoamRetargetAt = Time.time + (!IsPurgeCatfish ? 3f
                        : EncounterDutyValue == (int)FishcatEncounterDuty.Ambusher ? 12f : 6f);
                    Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
                    Vector3 next = transform.position + new Vector3(direction.x, 0f, direction.y) * 8f;
                    if (TryPurgePatrolDestination(out var directed)) _swimDestination = directed;
                    else if (FloodedWaterBoundary.TryGetSwimSurface(next, out _)) _swimDestination = next;
                }
                destination = _swimDestination;
            }

            if (IsPurgeCatfish && TargetPlayer.IsRealPlayer && ReplicatedDistractionTimer.ExpiredOrNotRunning(Runner)
                && Time.time - _lastVisiblePlayerAt > 0.1f)
                destination = CatfishSearchPosition();
            if (Time.time < _recoveryDetourUntil) destination = _swimDestination;
            Vector3 horizontal = Vector3.ProjectOnPlane(destination - transform.position, Vector3.up);
            if (horizontal.sqrMagnitude < 0.2f) { IsMoving = false; return; }
            Vector3 step = horizontal.normalized * Mathf.Min(Runner.DeltaTime * trackSpeed * ResonanceSpeedMultiplier, horizontal.magnitude);
            Vector3 nextPosition = transform.position + step;
            bool enteringShallowWater = FloodedWaterBoundary.TryGetSwimSurface(nextPosition, out _, 0.1f);
            bool approachingLand = _catfishWasInWater && (!enteringShallowWater
                || !FloodedWaterBoundary.TryGetSwimSurface(destination, out _, .1f));
            // shortcut: bridge Map 2's disconnected water/land meshes; author NavMeshLinks if the gap grows beyond 6m.
            if (approachingLand && !_hasCatfishLandEntry
                && NavMesh.SamplePosition(nextPosition, out var landHit, 6f, _agent.areaMask)
                && Vector3.Dot(
                    Vector3.ProjectOnPlane(landHit.position - transform.position, Vector3.up),
                    horizontal) > 0f
                && !IsMovementStepBlocked(landHit.position))
            {
                _catfishLandEntry = landHit.position;
                _hasCatfishLandEntry = true;
            }
            if (_hasCatfishLandEntry)
            {
                float landingStep = Runner.DeltaTime * trackSpeed * ResonanceSpeedMultiplier;
                Vector3 landingDirection = _catfishLandEntry - transform.position;
                Vector3 landingForward = Vector3.ProjectOnPlane(landingDirection, Vector3.up);
                Vector3 landingNext = Vector3.MoveTowards(transform.position, _catfishLandEntry, landingStep);
                if (IsMovementStepBlocked(landingNext))
                {
                    _hasCatfishLandEntry = false;
                    _nextRoamRetargetAt = 0f;
                    IsMoving = false;
                    return;
                }
                transform.position = landingNext;
                if (landingForward.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation,
                        Quaternion.LookRotation(landingForward),
                        720f * Runner.DeltaTime);
                if ((transform.position - _catfishLandEntry).sqrMagnitude <= 0.0025f)
                {
                    transform.position = _catfishLandEntry;
                    _hasCatfishLandEntry = false;
                    _catfishOnLand = true;
                    _catfishWasInWater = false;
                    _catfishPatrolCrossing = false;
                }
                IsMoving = true;
                return;
            }
            bool enteringLand = NavMesh.SamplePosition(nextPosition, out _, 1.5f, _agent.areaMask);
            if ((!enteringShallowWater && !enteringLand)
                || (enteringShallowWater
                    && FloodedWaterBoundary.TryGetSwimSurface(nextPosition, out _)
                    && Physics.Raycast(transform.position + Vector3.up * 0.75f, step.normalized,
                    step.magnitude + 0.4f, Physics.DefaultRaycastLayers & ~(1 << 4), QueryTriggerInteraction.Ignore)))
            {
                if (_seekingPlayer) CancelForcedPlayerSeek();
                IsMoving = false;
                return;
            }
            if (IsMovementStepBlocked(nextPosition))
            {
                _nextRoamRetargetAt = 0f;
                IsMoving = false;
                return;
            }
            transform.position = nextPosition;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(step), 720f * Runner.DeltaTime);
            IsMoving = true;
        }

        private Vector3 CatfishSearchPosition()
        {
            if (Vector3.ProjectOnPlane(transform.position - ReplicatedLastKnownTargetPosition, Vector3.up).sqrMagnitude > 9f)
                return ReplicatedLastKnownTargetPosition;
            if (Time.time >= _nextCatfishSearchAt)
            {
                _nextCatfishSearchAt = Time.time + 0.9f;
                _catfishSearchPosition = ReplicatedLastKnownTargetPosition;
                Vector2 offset = UnityEngine.Random.insideUnitCircle * 2.5f;
                Vector3 candidate = ReplicatedLastKnownTargetPosition + new Vector3(offset.x, 0f, offset.y);
                if (!_catfishOnLand && FloodedWaterBoundary.TryGetSwimSurface(candidate, out _))
                    _catfishSearchPosition = candidate;
                else if (_catfishOnLand && NavMesh.SamplePosition(candidate, out var hit, 1f, _agent.areaMask))
                    _catfishSearchPosition = hit.position;
            }
            return _catfishSearchPosition;
        }

        private float ResolveSwimHeight(float surfaceY)
        {
            float height = surfaceY - .9f;
            var origin = new Vector3(transform.position.x, surfaceY + .1f, transform.position.z);
            foreach (var hit in Physics.RaycastAll(origin, Vector3.down, 2f,
                         Physics.DefaultRaycastLayers & ~(1 << 4), QueryTriggerInteraction.Ignore))
            {
                if (hit.normal.y < .65f || hit.point.y > surfaceY + .05f
                    || !GameplayEnvironmentPhysics.IsEnvironment(hit.collider)
                    || hit.collider.GetComponentInParent<CreepMinionRuntime>() != null) continue;
                height = Mathf.Max(height, hit.point.y + .05f);
            }
            return height;
        }

        private void TickNavigationLocomotion(float deltaSeconds)
        {
            if (!_navigation.IsUsable || !_agent.hasPath || _agent.pathPending)
            {
                _navigation.TickAuthoritativeLocomotion(deltaSeconds);
                return;
            }
            Vector3 toCorner = Vector3.ProjectOnPlane(_agent.steeringTarget - transform.position, Vector3.up);
            float distance = Mathf.Min(toCorner.magnitude, _agent.speed * deltaSeconds);
            float height = IsDisruptor ? 0f : FlightHeightValue;
            Vector3 nextPosition = transform.position + toCorner.normalized * distance;
            var filter = new NavMeshQueryFilter { agentTypeID = _agent.agentTypeID, areaMask = _agent.areaMask };
            if (NavMesh.SamplePosition(nextPosition, out var nextSurface, .8f, filter)
                && Vector2.Distance(new Vector2(nextPosition.x, nextPosition.z),
                    new Vector2(nextSurface.position.x, nextSurface.position.z)) < .15f)
                nextPosition = nextSurface.position;
            bool blocked = IsNavigationStepBlocked(nextPosition, height);
            // NavMesh movement does not resolve collisions against the minion's elevated body.
            _navigation.TickAuthoritativeLocomotion(blocked ? 0f : deltaSeconds);
        }

        private bool IsNavigationStepBlocked(Vector3 nextPosition, float heightOffset)
        {
            if (!IsMovementStepBlocked(nextPosition, heightOffset)) return false;
            float climb = NavMesh.GetSettingsByID(_agent.agentTypeID).agentClimb;
            if (nextPosition.y > transform.position.y + .05f
                && nextPosition.y - transform.position.y <= climb + .01f
                && !IsBodyPlacementBlocked(nextPosition, heightOffset)) return false;
            // The NavMesh agent resolves baked steps; the body sweep must allow the same step height.
            return climb <= 0f
                || IsMovementStepBlocked(transform.position + Vector3.up * climb, heightOffset)
                || IsMovementStepBlocked(nextPosition, heightOffset + climb);
        }

        private bool IsBodyPlacementBlocked(Vector3 nextPosition, float heightOffset)
        {
            if (!(_bodyCollider is CapsuleCollider capsule)) return true;
            Vector3 scale = transform.lossyScale;
            float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float segment = Mathf.Max(0f, capsule.height * Mathf.Abs(scale.y) * .5f - radius);
            Vector3 center = nextPosition + transform.TransformVector(_bodyColliderBaseCenter) + Vector3.up * heightOffset;
            Vector3 bodyPosition = transform.position + center - transform.TransformPoint(capsule.center);
            foreach (var other in Physics.OverlapCapsule(center - Vector3.up * segment, center + Vector3.up * segment,
                         radius, Physics.DefaultRaycastLayers & ~(1 << 4), QueryTriggerInteraction.Ignore))
            {
                if (other.transform.IsChildOf(transform) || other.GetComponentInParent<NetworkPlayerMovement>() != null) continue;
                if (Physics.ComputePenetration(capsule, bodyPosition, transform.rotation,
                        other, other.transform.position, other.transform.rotation, out var separation, out float depth)
                    && depth > .05f && separation.y < .65f) return true;
            }
            return false;
        }

        private bool IsMovementStepBlocked(Vector3 nextPosition, float heightOffset = 0f)
        {
            Vector3 delta = nextPosition - transform.position;
            if (delta.sqrMagnitude < 0.000001f) return false;
            var capsule = _bodyCollider as CapsuleCollider;
            Vector3 scale = transform.lossyScale;
            float radius = capsule != null ? capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) : 0.3f;
            float halfSegment = capsule != null ? Mathf.Max(0f, capsule.height * Mathf.Abs(scale.y) * 0.5f - radius) : 0f;
            Vector3 center = transform.position + (capsule != null ? transform.TransformVector(_bodyColliderBaseCenter) : Vector3.up * 0.9f)
                + Vector3.up * heightOffset;
            bool hitBackfaces = Physics.queriesHitBackfaces;
            try
            {
                // Thin house meshes must block entry from either side.
                Physics.queriesHitBackfaces = true;
                foreach (var hit in Physics.CapsuleCastAll(center - Vector3.up * halfSegment, center + Vector3.up * halfSegment,
                             radius, delta.normalized, delta.magnitude + 0.05f,
                             Physics.DefaultRaycastLayers & ~(1 << 4), QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.transform.IsChildOf(transform)) continue;
                    if (hit.collider.GetComponentInParent<NetworkPlayerMovement>() != null) continue;
                    Vector3 bodyPosition = capsule != null
                        ? transform.position + center - transform.TransformPoint(capsule.center) : transform.position;
                    if (StalkerNavigationController.IsMovementHitBlocking(hit, capsule, bodyPosition, delta)) return true;
                }
                return false;
            }
            finally { Physics.queriesHitBackfaces = hitBackfaces; }
        }

        private void UpdateTargetAndMovement()
        {
            if (Time.time >= _nextTargetRefreshAt)
            {
                _nextTargetRefreshAt = Time.time + targetRefreshSeconds;
                var best = FindBestVisibleTarget();
                if (best.IsRealPlayer)
                {
                    _lastVisiblePlayerAt = Time.time;
                    _seekingPlayer = false;
                    ReplicatedSeekingPlayer = false;
                    if (best != TargetPlayer)
                    {
                        TargetPlayer = best;
                        _stalkerAlertDeliveredForTarget = false;
                        ReplicatedTrackSeconds = 0f;
                        ReplicatedLostSightSeconds = 0f;
                        StateValue = (int)CreepMinionState.Track;
                    }
                }
                else if (!IsPurgeCatfish && Time.time - _lastVisiblePlayerAt >= PlayerSeekTimeoutSeconds)
                {
                    TargetPlayer = FindClosestEligibleTarget();
                    _seekingPlayer = TargetPlayer.IsRealPlayer;
                    ReplicatedSeekingPlayer = _seekingPlayer;
                    if (!_seekingPlayer) _lastVisiblePlayerAt = Time.time;
                }
            }

            if (!TryGetEligiblePlayer(TargetPlayer, out var targetObject, out var targetLobby, out var lifeState))
            {
                if (_seekingPlayer) CancelForcedPlayerSeek();
                else ResetTracking();
                Roam();
                return;
            }
            IgnorePlayerBodyCollision(targetObject);

            if (!HasLineOfSight(targetObject))
            {
                if (IsPurgeCatfish)
                {
                    ReplicatedLostSightSeconds += Runner.DeltaTime;
                    if (ReplicatedLostSightSeconds >= 4f)
                    {
                        ResetTracking();
                        Roam();
                    }
                    else
                    {
                        StateValue = (int)CreepMinionState.Track;
                        _agent.speed = trackSpeed * ResonanceSpeedMultiplier;
                        TrySeekPlayerOnNavMesh(CatfishSearchPosition());
                    }
                    return;
                }
                if (IsDisruptor)
                {
                    StateValue = (int)CreepMinionState.Harass;
                    _agent.speed = harassSpeed * ResonanceSpeedMultiplier;
                    if (!TrySeekPlayerOnNavMesh(targetObject.transform.position))
                    {
                        _navigation.Stop();
                        if (_seekingPlayer)
                        {
                            CancelForcedPlayerSeek();
                            Roam();
                        }
                    }
                    return;
                }
                if (_seekingPlayer)
                {
                    StateValue = (int)CreepMinionState.Track;
                    _agent.speed = trackSpeed * ResonanceSpeedMultiplier;
                    if (!TrySeekPlayerOnNavMesh(targetObject.transform.position))
                    {
                        CancelForcedPlayerSeek();
                        Roam();
                    }
                    return;
                }
                ReplicatedLostSightSeconds += Runner.DeltaTime;
                if (ReplicatedLostSightSeconds > lostSightGraceSeconds)
                {
                    ResetTracking();
                    Roam();
                }
                else
                {
                    _agent.speed = trackSpeed * ResonanceSpeedMultiplier;
                    TryNavigateOrFly(ReplicatedLastKnownTargetPosition);
                }
                return;
            }

            _lastVisiblePlayerAt = Time.time;
            _seekingPlayer = false;
            ReplicatedSeekingPlayer = false;
            ReplicatedLastKnownTargetPosition = targetObject.transform.position;
            ReplicatedLostSightSeconds = 0f;
            if (IsDisruptor) StateValue = (int)CreepMinionState.Harass;
            if (State != CreepMinionState.Harass)
            {
                StateValue = (int)CreepMinionState.Track;
                _agent.speed = trackSpeed * ResonanceSpeedMultiplier;
                if (Vector3.Distance(transform.position, ReplicatedLastKnownTargetPosition) > trackStandOffDistance)
                    TryNavigateOrFly(ReplicatedLastKnownTargetPosition);
                else if (_agent.hasPath)
                    _agent.ResetPath();
            }

            TickStalkerAlertObservation(targetObject);

            if (State == CreepMinionState.Harass)
            {
                _agent.speed = harassSpeed * ResonanceSpeedMultiplier;
            }

            float distance = IsDisruptor
                ? Vector3.ProjectOnPlane(targetObject.transform.position - transform.position, Vector3.up).magnitude
                : Vector3.Distance(transform.position, targetObject.transform.position);
            bool wantsCloseAttack =
                targetLobby != null
                && (targetLobby.CarriedCoreId.IsValid
                    || (targetLobby.ToolId >= 1
                        && targetLobby.ToolId <= 6));
            float actionRange = IsDisruptor
                ? DisruptorAttackRange
                : wantsCloseAttack ? Mathf.Min(stealToolRange, 3f) : shootRange;
            if (IsDisruptor)
            {
                if (!TrySeekPlayerOnNavMesh(targetObject.transform.position))
                    _navigation.Stop();
            }
            else if (distance > actionRange)
                TryNavigateOrFly(targetObject.transform.position);
            else if (_agent.hasPath)
                _agent.ResetPath();

            if (distance <= actionRange && AttackCooldown.ExpiredOrNotRunning(Runner))
                AttackPlayer(targetObject, lifeState);
        }

        private void TickStalkerAlertObservation(NetworkObject target)
        {
            if (_stalkerAlertDeliveredForTarget || !HasLineOfSight(target)) return;
            if (State == CreepMinionState.Harass)
            {
                if (AlertCooldown.ExpiredOrNotRunning(Runner)) EmitStalkerAlert();
                return;
            }
            ReplicatedTrackSeconds += Runner.DeltaTime;
            var stage = NetworkMatchState.Instance != null ? NetworkMatchState.Instance.Zone2Stage : default;
            float required = Zone == RegionSemanticZone.Zone02
                && (stage == Zone2MissionStage.SecurityHoldReady || stage == Zone2MissionStage.SecurityHold)
                ? zone2SecurityTrackRequiredSeconds : trackRequiredSeconds;
            if (ReplicatedTrackSeconds >= required && AlertCooldown.ExpiredOrNotRunning(Runner)) EmitStalkerAlert();
        }

        private bool TrySeekPlayerOnNavMesh(Vector3 playerPosition)
        {
            return NavMesh.SamplePosition(playerPosition, out var hit, 4f, _agent.areaMask)
                && TryNavigateOrFly(hit.position);
        }

        private bool TryNavigateOrFly(Vector3 destination)
        {
            if (_navigation.EvaluateDestination(destination).IsComplete)
                return _navigation.TrySetDestination(destination);
            if (!IsMap2Flyer || !CrossesMap2Water(destination) || IsFlightBlocked(destination)) return false;
            _map2FlightDestination = destination;
            _map2FlightEnteredWater = false;
            _map2Flying = true;
            TickMap2Flight();
            return true;
        }

        private bool CrossesMap2Water(Vector3 destination)
        {
            for (int i = 1; i <= 16; i++)
                if (FloodedWaterBoundary.TryGetSwimSurface(
                    Vector3.Lerp(transform.position, destination, i / 16f), out _, 0.1f))
                    return true;
            return false;
        }

        private bool IsFlightBlocked(Vector3 destination)
        {
            Vector3 origin = transform.position;
            return IsMovementStepBlocked(new Vector3(destination.x, origin.y, destination.z), FlightHeightValue);
        }

        private void RecoverFromBlockedNavigation()
        {
            _navigation.Stop();
            if (State == CreepMinionState.Flee)
            {
                ReleaseStolenToolAuthoritative();
                ReleaseStolenCoreAuthoritative();
                return;
            }

            if (_seekingPlayer) CancelForcedPlayerSeek();
            StateValue = (int)CreepMinionState.Roam;
            _nextRoamRetargetAt = 0f;
            Roam();
        }

        private void TickMap2Flight()
        {
            if (_agent.enabled) _agent.enabled = false;
            if (_recoveryDetourUntil > 0f && Time.time >= _recoveryDetourUntil)
            {
                _map2FlightDestination = _recoveryFlightDestination;
                _recoveryDetourUntil = 0f;
            }
            Vector3 horizontal = Vector3.ProjectOnPlane(_map2FlightDestination - transform.position, Vector3.up);
            if (horizontal.sqrMagnitude <= 0.04f)
            {
                if (Time.time < _recoveryDetourUntil) { IsMoving = false; return; }
                _map2Flying = false;
                IsMoving = false;
                TryActivateAgent();
                return;
            }

            float distance = Mathf.Min(trackSpeed * ResonanceSpeedMultiplier * Runner.DeltaTime, horizontal.magnitude);
            Vector3 direction = horizontal.normalized;
            if (IsFlightBlocked(transform.position + direction * (distance + 0.2f)))
            {
                IsMoving = false;
                return;
            }

            Vector3 next = transform.position + direction * distance;
            bool overWater = FloodedWaterBoundary.TryGetSwimSurface(next, out _, 0.1f);
            _map2FlightEnteredWater |= overWater;
            if (_map2FlightEnteredWater && !overWater
                && NavMesh.SamplePosition(next, out var landing, 2.5f, _agent.areaMask)
                && !IsMovementStepBlocked(landing.position, FlightHeightValue))
            {
                transform.position = landing.position;
                _map2Flying = false;
                TryActivateAgent();
            }
            else
            {
                transform.position = next;
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(direction), 720f * Runner.DeltaTime);
            IsMoving = true;
        }

        private void Roam()
        {
            StateValue = (int)CreepMinionState.Roam;
            _agent.speed = roamSpeed * ResonanceSpeedMultiplier;
            if (IsPurgeCatfish && _hasPurgeActivityFocus)
            {
                if (Time.time >= _nextPurgePatrolAt)
                {
                    _nextPurgePatrolAt = Time.time + (EncounterDutyValue == (int)FishcatEncounterDuty.Ambusher ? 12f : 6f);
                    if (TryPurgePatrolDestination(out var directed))
                    {
                        if (!_catfishOnLand) { TickCatfishSwim(); return; }
                        _navigation.TrySetDestination(directed);
                    }
                }
                return;
            }

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
            _nextRoamRetargetAt = Time.time + 1f;
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

                if (CrossesMap2Water(hit.position))
                {
                    if (IsMap2Flyer && !IsFlightBlocked(hit.position))
                    {
                        _map2FlightDestination = hit.position;
                        _map2FlightEnteredWater = false;
                        _map2Flying = true;
                        TickMap2Flight();
                        return true;
                    }
                    if (IsCatfish)
                    {
                        _swimDestination = hit.position;
                        _catfishOnLand = false;
                        _catfishWasInWater = false;
                        _catfishPatrolCrossing = true;
                        TickCatfishSwim();
                        return true;
                    }
                }

                bool hasCompletePath = NavMesh.CalculatePath(
                    originHit.position, hit.position, _agent.areaMask, path)
                    && path.status == NavMeshPathStatus.PathComplete;
                if (!hasCompletePath)
                {
                    if (IsMap2Flyer && TryNavigateOrFly(hit.position)) return true;
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

                _navigation.TrySetDestination(hit.position);

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
            if (TryGetEligiblePlayer(TargetPlayer, out var current, out _, out _)
                && (!IsPurgeCatfish || OtherHunters(TargetPlayer) < PurgeFishcatBalance.HuntersPerPlayer)
                && HasLineOfSight(current))
                return TargetPlayer;

            PlayerRef best = PlayerRef.None;
            float bestScore = float.NegativeInfinity;
            foreach (var player in Runner.ActivePlayers)
            {
                if (!TryGetEligiblePlayer(player, out var obj, out var lobby, out _) || !CanSeePlayer(obj)) continue;
                if (IsPurgeCatfish && OtherHunters(player) >= PurgeFishcatBalance.HuntersPerPlayer) continue;
                float score = -Vector3.Distance(transform.position, obj.transform.position);
                if (IsPurgeCatfish) score -= OtherHunters(player) * 12f;
                if (!IsPurgeCatfish && lobby.CarriedCoreId.IsValid)
                    score += 500f;
                else if (!IsPurgeCatfish && lobby.ToolId >= 1 && lobby.ToolId <= 6)
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

        private PlayerRef FindClosestEligibleTarget()
        {
            PlayerRef best = PlayerRef.None;
            float bestDistance = float.PositiveInfinity;
            foreach (var player in Runner.ActivePlayers)
            {
                if (!TryGetEligiblePlayer(player, out var obj, out _, out _)) continue;
                float distance = (obj.transform.position - transform.position).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
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

        private void IgnorePlayerBodyCollisions()
        {
            if (_bodyCollider == null || Runner == null) return;
            foreach (var player in Runner.ActivePlayers)
                if (Runner.TryGetPlayerObject(player, out var playerObject))
                    IgnorePlayerBodyCollision(playerObject);
        }

        private void IgnorePlayerBodyCollision(NetworkObject playerObject)
        {
            if (_bodyCollider == null || playerObject == null) return;
            foreach (var playerCollider in playerObject.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(_bodyCollider, playerCollider, true);
        }

        private bool CanSeePlayer(NetworkObject playerObject)
        {
            if (playerObject == null) return false;
            var offset = playerObject.transform.position - transform.position;
            if (offset.magnitude > visionRange || Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(offset, Vector3.up)) > visionHalfAngle)
                return false;
            return HasLineOfSight(playerObject);
        }

        private bool HasLineOfSight(NetworkObject playerObject)
        {
            if (playerObject == null) return false;
            if (IsPurgeCatfish && Vector3.Distance(transform.position, playerObject.transform.position) > visionRange)
                return false;
            var origin = IsDisruptor
                ? transform.position + Vector3.up * 0.75f
                : transform.position + Vector3.up * (FlightHeightValue > 0.01f ? FlightHeightValue : flightHeight);
            var destination = playerObject.transform.position + Vector3.up;
            var ray = destination - origin;
            if (ray.sqrMagnitude < 0.001f) return true;
            var hits = Physics.RaycastAll(origin, ray.normalized, ray.magnitude, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (IsCatfish && hit.collider.gameObject.layer == 4) continue;
                return hit.collider.transform.IsChildOf(playerObject.transform);
            }
            return true;
        }

        private void EmitStalkerAlert()
        {
            _stalkerAlertDeliveredForTarget = TrySendStalkerAlert();
            ReplicatedTrackSeconds = 0f;
            StateValue = (int)CreepMinionState.Harass;
            AlertCooldown = TickTimer.CreateFromSeconds(
                Runner,
                _stalkerAlertDeliveredForTarget ? alertCooldownSeconds : alertRetrySeconds);
        }

        private bool TrySendStalkerAlert()
        {
            bool accepted = _noiseService != null && _noiseService.TryAccept(
                TargetPlayer,
                RuntimeNoiseType.MINION_ALERT,
                new RuntimeNoiseSourceOccurrenceKey($"minion-alert:{Object.Id}", ++_alertOrdinal),
                ReplicatedLastKnownTargetPosition,
                out _);

            if (accepted) AlertSequence++;
#if false
            if (!accepted)
                // Debug.LogWarning($"[CREEP_ALERT][REJECTED] target={TargetPlayer} zone={Zone} position={ReplicatedLastKnownTargetPosition}", this);
            else
                // Debug.Log($"[CREEP_ALERT][ACCEPTED] target={TargetPlayer} zone={Zone} position={ReplicatedLastKnownTargetPosition}", this);
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
                || !AttackCooldown.ExpiredOrNotRunning(Runner)
                || !AttackRecovery.ExpiredOrNotRunning(Runner))
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

            float distance = IsDisruptor
                ? Vector3.ProjectOnPlane(playerObject.transform.position - transform.position, Vector3.up).magnitude
                : Vector3.Distance(transform.position, playerObject.transform.position);
            if (IsDisruptor)
            {
                if (IsPurgeCatfish && (_resonanceContributors > 0
                    || Mathf.Abs(playerObject.transform.position.y - transform.position.y) > 2.5f)) return;
                if (distance > DisruptorAttackRange || !HasLineOfSight(playerObject)) return;
                var disruptorMovement = playerObject.GetComponent<NetworkPlayerMovement>();
                if (disruptorMovement == null) return;
                CommitAttack(CreepMinionAttackKind.MeleeSlow);
                if (IsPurgeCatfish)
                {
                    _spearVictim = TargetPlayer;
                    _spearContactTimer = TickTimer.CreateFromSeconds(Runner, SpearContactSeconds());
                    return;
                }
                bool damaged = life.TryApplyAuthoritativeDamage(attackDamage, "MINION_DISRUPTOR", transform.position);
                if (damaged) disruptorMovement.TryApplySlowAuthoritative(slowMultiplier, IsPurgeCatfish ? 1.25f : slowDurationSeconds);
                if (!IsPurgeCatfish) disruptorMovement.TryApplyKnockbackAuthoritative(
                    playerObject.transform.position - transform.position,
                    attackKnockbackSpeed,
                    attackKnockbackSeconds);
                if (!IsPurgeCatfish && NetworkMatchState.Instance != null
                    && NetworkMatchState.Instance.InterruptZone2OperationsAuthoritative(TargetPlayer))
                    disruptorMovement.InterruptZone2InteractionAuthoritative(0.85f);
                return;
            }
            if (distance <= shootRange)
            {
                bool canSteal = distance <= Mathf.Min(stealToolRange, 3f)
                    && lobby != null
                    && (lobby.CarriedCoreId.IsValid || lobby.ToolId > 0)
                    && interactor != null;
                BeginToolDive(playerObject, canSteal);
            }
        }

        private void CommitAttack(CreepMinionAttackKind kind)
        {
            AttackKindValue = (int)kind;
            AttackSequence++;
            AttackCooldown = TickTimer.CreateFromSeconds(
                Runner,
                attackCooldownSeconds);
            AttackRecovery = TickTimer.CreateFromSeconds(
                Runner,
                attackRecoverySeconds);
            RpcPlayAttackPresentation((int)kind);
        }

        private float SpearContactSeconds()
        {
            if (_animators != null)
                foreach (var animator in _animators)
                    if (animator != null && animator.gameObject.activeInHierarchy && animator.runtimeAnimatorController != null)
                        foreach (var clip in animator.runtimeAnimatorController.animationClips)
                            if (clip != null && clip.name.IndexOf("Attack01", StringComparison.OrdinalIgnoreCase) >= 0)
                                return clip.length * .5f / Mathf.Max(.1f, animator.speed);
            return .5f / Mathf.Max(.1f, animationSpeed);
        }

        private void TickSpearContact()
        {
            if (!_spearContactTimer.IsRunning || !_spearContactTimer.Expired(Runner)) return;
            _spearContactTimer = TickTimer.None;
            if (!IsPurgeCatfish || IsDying || IsUvStunned || _resonanceContributors > 0
                || TargetPlayer != _spearVictim || NetworkMatchState.Instance != null && NetworkMatchState.Instance.IsEnded
                || !TryGetEligiblePlayer(_spearVictim, out var target, out _, out var life)
                || Mathf.Abs(target.transform.position.y - transform.position.y) > 2.5f
                || Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up).magnitude > DisruptorAttackRange
                || !HasLineOfSight(target)) return;
            if (!life.TryApplyPurgeFishcatDamage(transform.position)) return;
            ConfirmedHitPosition = target.transform.position + Vector3.up;
            ConfirmedHitSequence++;
            target.GetComponent<NetworkPlayerMovement>()?.TryApplySlowAuthoritative(slowMultiplier, 1.25f);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcPlayAttackPresentation(int kindValue)
        {
            PlayAttackPresentation((CreepMinionAttackKind)kindValue);
        }

        private float StealClipSeconds()
        {
            if (_animators != null)
                foreach (var animator in _animators)
                    if (animator != null && animator.runtimeAnimatorController != null)
                        foreach (var clip in animator.runtimeAnimatorController.animationClips)
                            if (clip != null && clip.name.IndexOf("Attack02", StringComparison.OrdinalIgnoreCase) >= 0)
                                return clip.length / Mathf.Max(0.1f, animationSpeed) + 0.05f;
            return 1f / Mathf.Max(0.1f, animationSpeed) + 0.05f;
        }

        private void BeginToolDive(NetworkObject target, bool stealRequested)
        {
            ReplicatedToolVictimPosition = target.transform.position;
            Vector3 chosenFleeDestination = transform.position;
            if (stealRequested
                && !TryFindSabotageDropPosition(target.transform.position, out chosenFleeDestination)
                && !TryFindFleeDestination(target.transform.position, out chosenFleeDestination))
            {
                AttackCooldown = TickTimer.CreateFromSeconds(Runner, 1f);
                return;
            }
            ReplicatedToolDiveStealRequested = stealRequested;
            ReplicatedDiveStartHeight = Mathf.Max(ToolDiveHeight, FlightHeightValue);
            ReplicatedDiveDuration = Mathf.Max(0.45f, (ReplicatedDiveStartHeight - ToolDiveHeight) / 8f);
            ReplicatedFleeDestination = chosenFleeDestination;
            ToolDivePhase = 1;
            ReplicatedToolDiveApproaching = true;
            ReplicatedToolDiveApproachTimer = TickTimer.CreateFromSeconds(Runner, 2f);
            ReplicatedToolContactResolved = false;
            ToolDiveTimer = TickTimer.None;
            AttackCooldown = TickTimer.CreateFromSeconds(Runner, ReplicatedDiveDuration + StealClipSeconds() + attackCooldownSeconds);
        }

        private void TickToolDive()
        {
            if (!TryGetEligiblePlayer(TargetPlayer, out var target, out var lobby, out var life))
            {
                if (StolenToolId.IsValid)
                {
                    FlightHeightValue = ToolDiveHeight;
                    if (!ToolDiveTimer.Expired(Runner)) return;
                    BeginFleeTo(ReplicatedFleeDestination, 12f);
                }
                ToolDivePhase = 0;
                ReplicatedToolDiveStealRequested = false;
                _agent.stoppingDistance = 0.4f;
                return;
            }
            TickStalkerAlertObservation(target);
            _agent.speed = harassSpeed * ResonanceSpeedMultiplier;
            _agent.stoppingDistance = 0.75f;
            Vector3 playerForward = Vector3.ProjectOnPlane(target.transform.forward, Vector3.up).normalized;
            if (playerForward.sqrMagnitude < 0.01f) playerForward = Vector3.forward;
            Vector3 frontPosition = target.transform.position + playerForward * 1.35f;
            if (NavMesh.SamplePosition(frontPosition, out var frontHit, 1.5f, _agent.areaMask))
                frontPosition = frontHit.position;
            _navigation.TrySetDestination(frontPosition);
            IsMoving = _agent.velocity.sqrMagnitude > 0.05f;
            if (ReplicatedToolDiveApproaching)
            {
                float frontDistance = Vector3.ProjectOnPlane(
                    transform.position - frontPosition, Vector3.up).magnitude;
                if (frontDistance > 1.4f)
                {
                    if (!ReplicatedToolDiveApproachTimer.Expired(Runner)) return;
                    ReplicatedToolDiveApproaching = false;
                    ReplicatedToolDiveApproachTimer = TickTimer.None;
                    ToolDivePhase = 0;
                    ReplicatedToolDiveStealRequested = false;
                    _agent.stoppingDistance = 0.4f;
                    return;
                }
                ReplicatedToolDiveApproaching = false;
                ReplicatedToolDiveApproachTimer = TickTimer.None;
                ToolDiveTimer = TickTimer.CreateFromSeconds(Runner, ReplicatedDiveDuration);
            }
            Vector3 facePlayer = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up);
            if (facePlayer.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, Quaternion.LookRotation(facePlayer), 1080f * Runner.DeltaTime);
            if (ToolDivePhase == 1)
            {
                float t = 1f - (ToolDiveTimer.RemainingTime(Runner) ?? 0f) / ReplicatedDiveDuration;
                FlightHeightValue = Mathf.Lerp(ReplicatedDiveStartHeight, ToolDiveHeight, Mathf.SmoothStep(0f, 1f, t));
                if (!ToolDiveTimer.Expired(Runner)) return;
                ToolDivePhase = 2;
                var attackKind = ReplicatedToolDiveStealRequested
                    ? (lobby.CarriedCoreId.IsValid ? CreepMinionAttackKind.StealCore : CreepMinionAttackKind.StealTool)
                    : CreepMinionAttackKind.ShootSlow;
                CommitAttack(attackKind);
                ToolDiveTimer = TickTimer.CreateFromSeconds(Runner, StealClipSeconds());
            }
            FlightHeightValue = ToolDiveHeight;
            if (!ReplicatedToolContactResolved && (ToolDiveTimer.RemainingTime(Runner) ?? 0f) <= StealClipSeconds() * 0.45f)
            {
                ReplicatedToolContactResolved = true;
                if (Vector3.Distance(transform.position, target.transform.position) <= 2.5f
                    && HasLineOfSight(target))
                {
                    if (ReplicatedToolDiveStealRequested)
                    {
                        var interactor = target.GetComponent<NetworkPlayerInteractor>();
                        if (lobby.CarriedCoreId.IsValid) TryStealCore(target, lobby, interactor);
                        else if (interactor != null && interactor.TryTransferTeamToolToMonster(Object.Id, out var pickupId))
                            StolenToolId = pickupId;
                    }
                    else
                    {
                        var movement = target.GetComponent<NetworkPlayerMovement>();
                        if (movement != null)
                        {
                            movement.TryApplySlowAuthoritative(slowMultiplier, slowDurationSeconds);
                            life.TryApplyAuthoritativeDamage(attackDamage, "MINION", transform.position);
                            movement.TryApplyKnockbackAuthoritative(
                                target.transform.position - transform.position,
                                attackKnockbackSpeed,
                                attackKnockbackSeconds);
                        }
                    }
                }
            }
            if (!ToolDiveTimer.Expired(Runner)) return;
            ToolDivePhase = 0;
            ReplicatedToolDiveStealRequested = false;
            _agent.stoppingDistance = 0.4f;
            if (StolenToolId.IsValid)
            {
                ReplicatedToolRouteRetries = 0;
                BeginFleeTo(ReplicatedFleeDestination, Mathf.Max(toolFleeSeconds, 15f));
            }
        }

        public void GetToolCarryPose(out Vector3 position, out Quaternion rotation)
        {
            if (_toolHand == null)
            {
                foreach (var bone in (_visualRoot != null ? _visualRoot : transform)
                    .GetComponentsInChildren<Transform>(true))
                {
                    string name = bone.name.ToLowerInvariant();
                    if ((name.Contains("hand") || name.Contains("wrist")) && (name.Contains("right") || name.EndsWith("_r") || name.EndsWith(".r")))
                    { _toolHand = bone; break; }
                }
            }
            position = _toolHand != null ? _toolHand.position : (_visualRoot != null ? _visualRoot.position : CarryPosition());
            rotation = _toolHand != null ? _toolHand.rotation : transform.rotation;
        }

        private void ReleaseStolenToolAuthoritative()
        {
            if (!StolenToolId.IsValid) return;
            if (Runner.TryFindObject(StolenToolId, out var pickup) && pickup.TryGetComponent<NetworkInteractable>(out var item))
            {
                Vector3 point = NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas)
                    ? hit.position + Vector3.up * 0.1f : transform.position;
                item.EndMonsterCarryAuthoritative(point);
            }
            StolenToolId = default;
        }

        private bool TryFindSabotageDropPosition(Vector3 playerPosition, out Vector3 position)
        {
            position = default;
            if (!NavMesh.SamplePosition(playerPosition, out var playerHit, 2f, NavMesh.AllAreas)) return false;
            if (!NavMesh.SamplePosition(transform.position, out var startHit, 2f, NavMesh.AllAreas)) return false;
            var rooms = FindObjectsByType<RegionDefinition>();
            var playerRoom = ResolveDropRoom(rooms, playerHit.position);
            if (playerRoom == null) return false;
            var path = new NavMeshPath();
            float minDistance = Mathf.Max(12f, stolenToolDropMinDistance);
            float maxDistance = Mathf.Max(minDistance + 10f, stolenToolDropMaxDistance);
            for (int i = 0; i < 48; i++)
            {
                Vector2 direction = UnityEngine.Random.insideUnitCircle;
                if (direction.sqrMagnitude < 0.01f) continue;
                direction.Normalize();
                float distance = UnityEngine.Random.Range(minDistance, maxDistance);
                var candidate = playerPosition + new Vector3(direction.x, 0f, direction.y) * distance;
                if (!NavMesh.SamplePosition(candidate, out var hit, 2.5f, NavMesh.AllAreas)) continue;
                float actualDistance = Vector3.Distance(playerPosition, hit.position);
                if (actualDistance < minDistance || actualDistance > maxDistance) continue;
                var dropRoom = ResolveDropRoom(rooms, hit.position);
                if (dropRoom == null || dropRoom.RegionId == playerRoom.RegionId) continue;
                if (!NavMesh.CalculatePath(startHit.position, hit.position, NavMesh.AllAreas, path)
                    || path.status != NavMeshPathStatus.PathComplete) continue;
                if (CalculatePathLength(path) < 18f) continue;

                position = hit.position + Vector3.up * 0.05f;
                return true;
            }

            return false;
        }

        private static RegionDefinition ResolveDropRoom(RegionDefinition[] rooms, Vector3 point)
        {
            RegionDefinition best = null;
            float smallestArea = float.PositiveInfinity;
            foreach (var room in rooms)
            {
                if (room == null || !room.RegionId.IsValid) continue;
                var bounds = room.ToBakeData().WorldBounds;
                if (Mathf.Abs(point.y - bounds.center.y) > 2f) continue;
                var projected = new Vector3(point.x, bounds.center.y, point.z);
                float area = bounds.size.x * bounds.size.z;
                if (bounds.Contains(projected) && area < smallestArea) { best = room; smallestArea = area; }
            }
            return best;
        }

        private bool TryStealCore(NetworkObject playerObject, LobbyPlayerState lobby, NetworkPlayerInteractor interactor)
        {
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
            // A successful forced drop consumes this hit's one side effect even if carry cannot begin.
            if (!core.TryBeginMonsterCarryAuthoritative(Object.Id, CarryPosition(), transform.rotation))
            {
#if false
                // Debug.LogWarning($"[CREEP_STEAL_CORE][CARRY_FAILED] core={coreId} monster={Object.Id}", this);
#endif
                return true;
            }
            StolenCoreId = coreId;
            BeginFlee(playerObject.transform.position, coreCarryTimeoutSeconds);
#if false
            // Debug.Log($"[CREEP_STEAL_CORE][SUCCESS] core={coreId} destination={ReplicatedFleeDestination}", this);
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
            ToolDivePhase = 0;
            ReleaseStolenToolAuthoritative();

            // Nếu đang giữ Core thì trả Core trước,
            // không để Core biến mất cùng Minion.
            if (StolenCoreId.IsValid)
                ReleaseStolenCoreAuthoritative();

            IsDying = true;
            ResetTracking();

            ReplicatedDistractionTimer = TickTimer.None;
            ReplicatedFlashlightExposureSeconds = 0f;

            AttackCooldown = TickTimer.None;
            AttackRecovery = TickTimer.None;
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

            float deathSeconds = DeathClipSeconds();
            if (IsCatfish) deathSeconds += catfishDeathPoseHoldSeconds + catfishDeathFadeSeconds;
            DeathTimer = TickTimer.CreateFromSeconds(Runner, deathSeconds);

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
            if (!TryFindFleeDestination(threatPosition, out var chosenFleeDestination))
            {
                chosenFleeDestination = transform.position;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // Debug.LogWarning($"[CREEP_FLEE][NO_DESTINATION] id={Object.Id} position={transform.position}", this);
#endif
            }

            ReplicatedFleeDestination = chosenFleeDestination;
            _agent.speed = fleeSpeed * ResonanceSpeedMultiplier;
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
                ReplicatedFleeDestination = hit.position;
            }
            else
            {
                ReplicatedFleeDestination = destination;
            }

            _agent.speed = fleeSpeed * ResonanceSpeedMultiplier;
            StateValue = (int)CreepMinionState.Flee;
            FleeTimer = TickTimer.CreateFromSeconds(Runner, seconds);
            _navigation.TrySetDestination(ReplicatedFleeDestination);
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
            _seekingPlayer = false;
            ReplicatedSeekingPlayer = false;
            ReplicatedTrackSeconds = 0f;
            ReplicatedLostSightSeconds = 0f;
            _stalkerAlertDeliveredForTarget = false;
            _nextCatfishSearchAt = 0f;
        }

        private void CancelForcedPlayerSeek()
        {
            ResetTracking();
            _lastVisiblePlayerAt = Time.time;
            _nextRoamRetargetAt = 0f;
            _swimDestination = transform.position;
        }



        public override void Render()
        {
            // Lazy-init visual root.
            if (_visualRoot == null)
            {
                _visualRoot = transform.Find("Visual");
                if (_visualRoot != null)
                {
                    _visualBaseLocalPosition = _visualRoot.localPosition;
                }
            }

            if (IsDying)
            {
                if (!_deathAnimationPlayed)
                {
                    _deathAnimationPlayed = true;
                    _currentLocomotionAnimation = null;
                    CrossFade("Die");
                }
                if (IsCatfish) RenderCatfishDeath();
                return;
            }

            if (_visualRoot != null && !IsDying)
            {
                float bob = IsDisruptor ? 0f : Mathf.Sin(Time.time * flightBobSpeed) * flightBobAmplitude;
                float resolvedHeight = IsDisruptor ? 0f : FlightHeightValue > 0f
                    ? FlightHeightValue
                    : flightHeight;
                float parentScaleY =
                    _visualRoot.parent != null
                        ? Mathf.Max(
                            0.001f,
                            Mathf.Abs(
                                _visualRoot.parent.lossyScale.y))
                        : 1f;
                if (_renderFlightHeight < 0f) _renderFlightHeight = resolvedHeight;
                _renderFlightHeight = Mathf.MoveTowards(_renderFlightHeight, resolvedHeight, Time.deltaTime * FlightHeightChangeSpeed);
                float localHeight = _renderFlightHeight / parentScaleY;
                float localBob = bob / parentScaleY;
                _visualRoot.localPosition =
                    _visualBaseLocalPosition
                    + Vector3.up * (localHeight + localBob);
                if (!IsDisruptor && _bodyCollider is CapsuleCollider capsule)
                    capsule.center = _bodyColliderBaseCenter + Vector3.up * localHeight;
            }

            if (_animators == null || _animators.Length == 0) _animators = GetComponentsInChildren<Animator>();
            if (IsUvStunned)
            {
                _renderedAttackSequence = AttackSequence;
                _actionAnimationUntil = 0f;
                if (_currentLocomotionAnimation != "GetHit")
                {
                    _currentLocomotionAnimation = "GetHit";
                    CrossFade("GetHit");
                }
                return;
            }
            if (AttackSequence != _renderedAttackSequence)
            {
                _renderedAttackSequence = AttackSequence;
                PlayAttackPresentation(AttackKind);
                return;
            }
            if (Time.time < _actionAnimationUntil) return;
            string desired = !IsMoving ? "Idle"
                : State == CreepMinionState.Roam ? "Walk" : "Run";
            if (desired == _currentLocomotionAnimation) return;
            _currentLocomotionAnimation = desired;
            CrossFade(desired);
        }

        private void PlayAttackPresentation(CreepMinionAttackKind kind)
        {
            if (_animators == null || _animators.Length == 0)
                _animators = GetComponentsInChildren<Animator>();
            _currentLocomotionAnimation = null;
            PlayAction(kind == CreepMinionAttackKind.MeleeSlow ? "Attack01" : "Attack2");
            _actionAnimationUntil = Time.time + Mathf.Max(attackAnimationLockSeconds, StealClipSeconds());
        }

        private const float FlightHeightChangeSpeed = 12f;

        private void UpdateFlightHeightAuthoritative()
        {
            if (Time.time < _nextCeilingProbeAt)
                return;

            _nextCeilingProbeAt = Time.time + 0.05f;
            Vector3 toCorner = Vector3.ProjectOnPlane(_agent.steeringTarget - transform.position, Vector3.up);
            float lookAhead = flightHeight * _agent.speed / FlightHeightChangeSpeed + _agent.radius + ceilingClearance;
            Vector3 next = transform.position + toCorner.normalized * Mathf.Min(lookAhead, toCorner.magnitude);
            FlightHeightValue = Mathf.MoveTowards(FlightHeightValue, ResolveFlightHeight(next), FlightHeightChangeSpeed * 0.05f);
        }

        private float ResolveFlightHeight(Vector3 lookAheadPosition)
        {
            float visualTopOffset = _visualTopOffset;
            float height = Mathf.Max(flightHeight, 0.3f);

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

                height = Mathf.Max(
                    0.3f,
                    0.1f
                    + hit.distance
                    - ceilingClearance
                    - visualTopOffset
                    - flightBobAmplitude);
                break;
            }

            // Descend before the doorway rather than flying through the wall above it.
            if (IsMovementStepBlocked(lookAheadPosition, height) && !IsMovementStepBlocked(lookAheadPosition, 0.3f))
                return 0.3f;
            return height;
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

        private void CrossFade(string state)
        {
            foreach (var animator in _animators)
                if (animator != null) animator.CrossFade(state, 0.05f);
        }

        private void RenderCatfishDeath()
        {
            float fadeSeconds = Mathf.Max(0.1f, catfishDeathFadeSeconds);
            float remaining = DeathTimer.RemainingTime(Runner) ?? 0f;
            if (!_catfishDeathPoseFrozen && remaining <= catfishDeathPoseHoldSeconds + fadeSeconds)
            {
                _catfishDeathPoseFrozen = true;
                foreach (var animator in _animators)
                {
                    if (animator == null) continue;
                    animator.Play("Die", 0, 0.999f);
                    animator.Update(0f);
                    animator.speed = 0f;
                }
            }

            if (remaining > fadeSeconds) return;
            EnsureCatfishFadeMaterials();
            float opacity = Mathf.Clamp01(remaining / fadeSeconds);
            DeathVisualAlpha = opacity;
            for (int i = 0; i < _catfishFadeMaterials.Length; i++)
            {
                var material = _catfishFadeMaterials[i];
                if (material == null) continue;
                var color = _catfishFadeBaseColors[i];
                color.a *= opacity;
                material.SetColor("_BaseColor", color);
            }
        }

        private void EnsureCatfishFadeMaterials()
        {
            if (_catfishFadeMaterials != null) return;
            var materials = new List<Material>();
            var colors = new List<Color>();
            if (_visualRoot != null)
                foreach (var renderer in _visualRoot.GetComponentsInChildren<Renderer>(false))
                {
                    var originals = renderer.sharedMaterials;
                    var instances = new Material[originals.Length];
                    for (int i = 0; i < originals.Length; i++)
                    {
                        var original = originals[i];
                        if (original == null) continue;
                        var material = new Material(original);
                        material.SetOverrideTag("RenderType", "Transparent");
                        material.SetFloat("_Surface", 1f);
                        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        material.SetFloat("_ZWrite", 0f);
                        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                        instances[i] = material;
                        materials.Add(material);
                        colors.Add(material.GetColor("_BaseColor"));
                    }
                    renderer.materials = instances;
                }
            _catfishFadeMaterials = materials.ToArray();
            _catfishFadeBaseColors = colors.ToArray();
        }

        private void ReleaseCatfishFadeMaterials()
        {
            if (_catfishFadeMaterials == null) return;
            foreach (var material in _catfishFadeMaterials)
                if (material != null) Destroy(material);
            _catfishFadeMaterials = null;
            _catfishFadeBaseColors = null;
        }

        private void PlayAction(string state)
        {
            int stateHash = Animator.StringToHash(state);
            foreach (var animator in _animators)
                if (animator != null && animator.HasState(0, stateHash))
                    animator.Play(stateHash, 0, 0f);
        }

        private float DeathClipSeconds()
        {
            if (_animators != null)
                foreach (var animator in _animators)
                    if (animator != null && animator.runtimeAnimatorController != null)
                        foreach (var clip in animator.runtimeAnimatorController.animationClips)
                            if (clip != null && clip.name.IndexOf("Die", StringComparison.OrdinalIgnoreCase) >= 0)
                                return clip.length / Mathf.Max(0.1f, animationSpeed) + 0.05f;
            return Mathf.Max(0.1f, deathVanishSeconds);
        }

        private void ApplyVisualVariant()
        {
            var variants = transform.Find("VisualVariants");
            if (variants == null) return;
            int selected = IsCatfish ? 16 + VisualVariantValue % 2
                : IsDisruptor ? VisualVariantValue % 15 + 1 : 0;
            for (int i = 0; i < variants.childCount; i++)
                variants.GetChild(i).gameObject.SetActive(i == selected);
            _visualRoot = selected < variants.childCount ? variants.GetChild(selected) : null;
        }
    }
}
