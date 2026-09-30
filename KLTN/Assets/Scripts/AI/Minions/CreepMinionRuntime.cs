using System;
using EchoProtocol.AI.Listener.Noise;
using EchoProtocol.AI.Stalker;
using EchoProtocol.AI.Stalker.Spatial;
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

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class CreepMinionRuntime : NetworkBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float roamSpeed = 3.5f;
        [SerializeField] private float trackSpeed = 4.5f;
        [SerializeField] private float harassSpeed = 6f;
        [SerializeField] private float fleeSpeed = 7f;
        [SerializeField] private float roamRadius = 8f;
        [SerializeField] private float targetRefreshSeconds = 0.35f;

        [Header("Perception")]
        [SerializeField] private float visionRange = 16f;
        [SerializeField] private float visionHalfAngle = 80f;
        [SerializeField] private float trackRequiredSeconds = 5f;
        [SerializeField] private float zone2SecurityTrackRequiredSeconds = 3f;
        [SerializeField] private float lostSightGraceSeconds = 2.5f;
        [FormerlySerializedAs("shadowStopDistance")]
        [SerializeField, Min(0.5f)] private float trackStandOffDistance = 2.5f;
        [SerializeField, Min(0f)] private float roamArrivalSlack = 0.25f;

        [Header("Combat")]
        [SerializeField] private float attackRange = 1.6f;
        [SerializeField] private float attackCooldownSeconds = 2.2f;
        [SerializeField] private float nonLethalDamage = 8f;

        [Header("Hit Effects")]
        [SerializeField] private float slowMultiplier = 0.65f;
        [SerializeField] private float slowDurationSeconds = 3f;
        [SerializeField] private float teamToolDropChance = 0.25f;
        [SerializeField] private float coreStealChance = 1f;
        [SerializeField] private float coreCarryDistance = 10f;
        [SerializeField] private float coreCarryTimeoutSeconds = 6f;
        [SerializeField] private float coreCarryHeight = 0.65f;
        [SerializeField, Min(1f)] private float stolenToolDropMinDistance = 6f;
        [SerializeField, Min(1f)] private float stolenToolDropMaxDistance = 10f;

        [Header("Alert")]
        [SerializeField] private float alertCooldownSeconds = 20f;

        [Header("Counterplay")]
        [SerializeField] private float flashlightRepelRange = 12f;
        [SerializeField] private float flashlightExposureRequiredSeconds = 0.75f;
        [SerializeField] private float flashlightFleeSeconds = 4f;
        [SerializeField] private float noiseMakerDistractionSeconds = 6f;
        [SerializeField] private float noiseMakerArrivalDistance = 1.5f;

        [Networked] public int StateValue { get; private set; }
        [Networked] public int ZoneValue { get; private set; }
        [Networked] public PlayerRef TargetPlayer { get; private set; }
        [Networked] public NetworkId StolenCoreId { get; private set; }
        [Networked] public NetworkBool IsMoving { get; private set; }
        [Networked] public uint AttackSequence { get; private set; }
        [Networked] public uint AlertSequence { get; private set; }
        [Networked] private TickTimer AttackCooldown { get; set; }
        [Networked] private TickTimer AlertCooldown { get; set; }
        [Networked] private TickTimer FleeTimer { get; set; }

        public CreepMinionState State => StateValue >= 0 && StateValue <= 3
            ? (CreepMinionState)StateValue : CreepMinionState.Roam;
        public RegionSemanticZone Zone => ZoneValue == (int)RegionSemanticZone.Zone01
            || ZoneValue == (int)RegionSemanticZone.Zone02
            ? (RegionSemanticZone)ZoneValue : RegionSemanticZone.Unknown;

        private NavMeshAgent _agent;
        private StalkerNavigationController _navigation;
        private Animator[] _animators;
        private HostRuntimeNoiseService _noiseService;
        private float _trackSeconds;
        private float _lostSightSeconds;
        private float _flashlightExposureSeconds;
        private bool _flashlightRepelledDuringFlee;
        private float _nextTargetRefreshAt;
        private float _nextRoamRetargetAt;
        private float _distractionUntil;
        private Vector3 _lastKnownTargetPosition;
        private Vector3 _fleeDestination;
        private Vector3 _distractionPoint;
        private long _alertOrdinal;
        private uint _renderedAttackSequence;
        private uint _renderedAlertSequence;
        private float _actionAnimationUntil;
        private string _currentLocomotionAnimation;

        public void ConfigureBeforeSpawn(RegionSemanticZone zone) => ZoneValue = (int)zone;

        public override void Spawned()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animators = GetComponentsInChildren<Animator>(true);
            _renderedAttackSequence = AttackSequence;
            _renderedAlertSequence = AlertSequence;
            if (!Object.HasStateAuthority)
            {
                _agent.enabled = false;
                return;
            }

            StateValue = (int)CreepMinionState.Roam;
            TargetPlayer = PlayerRef.None;
            StolenCoreId = default;
            AttackCooldown = TickTimer.None;
            AlertCooldown = TickTimer.None;
            FleeTimer = TickTimer.None;
            _noiseService = HostRuntimeNoiseService.EnsureExists(MatchAuthorityRuntime.Instance);
            _noiseService.RuntimeNoiseAccepted += HandleRuntimeNoiseAccepted;
            _agent.enabled = true;
            _agent.updatePosition = true;
            _agent.updateRotation = true;
            _agent.angularSpeed = 720f;
            _agent.acceleration = 24f;
            _agent.stoppingDistance = 0.6f;
            _navigation = new StalkerNavigationController(_agent);
            _navigation.SetAuthoritativeLocomotion(true);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _navigation?.SetAuthoritativeLocomotion(false);
            _navigation = null;
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
            _distractionPoint = noiseEvent.WorldPosition;
            _distractionUntil = Time.time + noiseMakerDistractionSeconds;
            ResetTracking();
            StateValue = (int)CreepMinionState.Roam;
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority || _agent == null || !_agent.enabled) return;
            if (!_agent.isOnNavMesh
                && (_navigation == null || !_navigation.TryReattachToNearestNavMesh(2f)))
            {
                IsMoving = false;
                return;
            }

            Vector3 flashlightSource = default;
            if ((State != CreepMinionState.Flee || (StolenCoreId.IsValid && !_flashlightRepelledDuringFlee))
                && TryGetFlashlightSource(out flashlightSource))
                _flashlightExposureSeconds += Runner.DeltaTime;
            else
                _flashlightExposureSeconds = 0f;
            if (_flashlightExposureSeconds >= flashlightExposureRequiredSeconds)
            {
                _flashlightExposureSeconds = 0f;
                ResetTracking();
                BeginFlee(flashlightSource, flashlightFleeSeconds);
                _flashlightRepelledDuringFlee = true;
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
                    _distractionUntil = 0f;
            }
            else
            {
                _distractionUntil = 0f;
                UpdateTargetAndMovement();
            }
            _navigation.TickAuthoritativeLocomotion(Runner.DeltaTime);
            IsMoving = _navigation.AuthoritativeMoveSpeed > 0.2f;
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
                    _trackSeconds = 0f;
                    _lostSightSeconds = 0f;
                    StateValue = (int)CreepMinionState.Track;
                }
            }

            if (!TryGetEligiblePlayer(TargetPlayer, out var targetObject, out _, out var lifeState))
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
                _agent.speed = harassSpeed;
                _agent.SetDestination(_lastKnownTargetPosition);
                if (Vector3.Distance(transform.position, _lastKnownTargetPosition) <= attackRange
                    && AttackCooldown.ExpiredOrNotRunning(Runner))
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
            if (stillTravelling && Time.time < _nextRoamRetargetAt) return;
            _nextRoamRetargetAt = Time.time + UnityEngine.Random.Range(3f, 6f);
            var offset = UnityEngine.Random.insideUnitSphere * roamRadius;
            offset.y = 0f;
            if (NavMesh.SamplePosition(transform.position + offset, out var hit, 3f, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
        }

        private PlayerRef FindBestVisibleTarget()
        {
            PlayerRef best = PlayerRef.None;
            float bestScore = float.NegativeInfinity;
            foreach (var player in Runner.ActivePlayers)
            {
                if (!TryGetEligiblePlayer(player, out var obj, out var lobby, out _) || !CanSeePlayer(obj)) continue;
                float score = -Vector3.Distance(transform.position, obj.transform.position);
                if (Zone == RegionSemanticZone.Zone01 && lobby.CarriedCoreId.IsValid) score += 30f;
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
                && (!obj.TryGetComponent<NetworkPlayerMovement>(out var movement) || !movement.IsHidden);
        }

        private bool CanSeePlayer(NetworkObject playerObject)
        {
            if (playerObject == null) return false;
            var offset = playerObject.transform.position - transform.position;
            if (offset.magnitude > visionRange || Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(offset, Vector3.up)) > visionHalfAngle)
                return false;
            var origin = transform.position + Vector3.up * 0.7f;
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
            if (_noiseService == null || !_noiseService.TryAccept(
                    TargetPlayer,
                    RuntimeNoiseType.MINION_ALERT,
                    new RuntimeNoiseSourceOccurrenceKey($"minion-alert:{Object.Id}", ++_alertOrdinal),
                    _lastKnownTargetPosition,
                    out _))
            {
                return;
            }

            AlertSequence++;
            AlertCooldown = TickTimer.CreateFromSeconds(Runner, alertCooldownSeconds);
            _trackSeconds = 0f;
            StateValue = (int)CreepMinionState.Harass;
        }

        private void AttackPlayer(NetworkObject playerObject, NetworkPlayerLifeState life)
        {
            if (!life.TryApplyAuthoritativeNonLethalDamage(nonLethalDamage, "CREEP_MINION", transform.position)) return;
            AttackSequence++;
            AttackCooldown = TickTimer.CreateFromSeconds(Runner, attackCooldownSeconds);
            var lobby = playerObject.GetComponent<LobbyPlayerState>();
            var interactor = playerObject.GetComponent<NetworkPlayerInteractor>();
            if (Zone == RegionSemanticZone.Zone01 && lobby != null && lobby.CarriedCoreId.IsValid
                && UnityEngine.Random.value <= coreStealChance && TryStealCore(playerObject, lobby, interactor)) return;
            if (lobby != null && lobby.ToolId >= 1 && lobby.ToolId <= 6
                && UnityEngine.Random.value <= teamToolDropChance && interactor != null
                && TryRelocateTeamTool(playerObject, interactor)) return;
            playerObject.GetComponent<NetworkPlayerMovement>()
                ?.TryApplySlowAuthoritative(slowMultiplier, slowDurationSeconds);
        }

        private bool TryRelocateTeamTool(NetworkObject playerObject, NetworkPlayerInteractor interactor)
        {
            return TryFindSabotageDropPosition(playerObject.transform.position, out var dropPosition)
                && interactor.DropTeamToolAuthoritative(TargetPlayer, dropPosition);
        }

        private bool TryFindSabotageDropPosition(Vector3 playerPosition, out Vector3 position)
        {
            position = default;
            var away = Vector3.ProjectOnPlane(transform.position - playerPosition, Vector3.up);
            if (away.sqrMagnitude < 0.01f)
                away = Vector3.ProjectOnPlane(UnityEngine.Random.insideUnitSphere, Vector3.up);
            away.Normalize();

            for (int i = 0; i < 8; i++)
            {
                var direction = Quaternion.Euler(0f, UnityEngine.Random.Range(-60f, 60f), 0f) * away;
                float distance = UnityEngine.Random.Range(
                    stolenToolDropMinDistance,
                    Mathf.Max(stolenToolDropMinDistance + 0.5f, stolenToolDropMaxDistance));
                if (!NavMesh.SamplePosition(transform.position + direction * distance, out var hit, 3f, NavMesh.AllAreas)
                    || Vector3.Distance(playerPosition, hit.position) < stolenToolDropMinDistance)
                    continue;

                position = hit.position + Vector3.up * 0.05f;
                return true;
            }

            return false;
        }

        private bool TryStealCore(NetworkObject playerObject, LobbyPlayerState lobby, NetworkPlayerInteractor interactor)
        {
            var coreId = lobby.CarriedCoreId;
            if (interactor == null || !coreId.IsValid || !Runner.TryFindObject(coreId, out var obj)
                || obj == null || !obj.TryGetComponent<NetworkPickupItem>(out var core)
                || !interactor.DropCarriedCoreAuthoritative(TargetPlayer)) return false;
            // A successful forced drop consumes this hit's one side effect even if carry cannot begin.
            if (!core.TryBeginMonsterCarryAuthoritative(Object.Id, CarryPosition(), transform.rotation)) return true;
            StolenCoreId = coreId;
            BeginFlee(playerObject.transform.position, coreCarryTimeoutSeconds);
            return true;
        }

        private Vector3 CarryPosition() => transform.position + Vector3.up * coreCarryHeight + transform.forward * 0.2f;

        private void BeginFlee(Vector3 threatPosition, float seconds)
        {
            var away = Vector3.ProjectOnPlane(transform.position - threatPosition, Vector3.up).normalized;
            if (away.sqrMagnitude < 0.01f) away = UnityEngine.Random.insideUnitSphere.normalized;
            var candidate = transform.position + away * coreCarryDistance
                + new Vector3(UnityEngine.Random.Range(-2f, 2f), 0f, UnityEngine.Random.Range(-2f, 2f));
            if (NavMesh.SamplePosition(candidate, out var hit, 4f, NavMesh.AllAreas)
                || NavMesh.SamplePosition(transform.position + UnityEngine.Random.insideUnitSphere * roamRadius,
                    out hit, 4f, NavMesh.AllAreas))
                _fleeDestination = hit.position;
            else
                _fleeDestination = transform.position;
            _agent.speed = fleeSpeed;
            StateValue = (int)CreepMinionState.Flee;
            FleeTimer = TickTimer.CreateFromSeconds(Runner, seconds);
            _flashlightRepelledDuringFlee = false;
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
            _flashlightRepelledDuringFlee = false;
            ResetTracking();
            StateValue = (int)CreepMinionState.Roam;
        }

        private void ResetTracking()
        {
            TargetPlayer = PlayerRef.None;
            _trackSeconds = 0f;
            _lostSightSeconds = 0f;
        }

        private bool TryGetFlashlightSource(out Vector3 source)
        {
            source = default;
            foreach (var player in Runner.ActivePlayers)
            {
                if (!Runner.TryGetPlayerObject(player, out var obj) || obj == null
                    || !obj.TryGetComponent<NetworkPlayerFlashlight>(out var light) || !light.IsEmittingLight) continue;
                var beam = light.BeamTransform;
                if (beam == null) continue;
                var ray = transform.position + Vector3.up * 0.6f - beam.position;
                if (ray.magnitude > Mathf.Min(light.BeamRange, flashlightRepelRange)
                    || Vector3.Angle(beam.forward, ray) > light.BeamSpotAngle * 0.5f) continue;
                var hits = Physics.RaycastAll(beam.position, ray.normalized, ray.magnitude + 0.5f,
                    ~0, QueryTriggerInteraction.Ignore);
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var hit in hits)
                {
                    if (hit.collider.transform.IsChildOf(obj.transform)) continue;
                    if (hit.collider.transform.IsChildOf(transform))
                    {
                        source = obj.transform.position;
                        return true;
                    }
                    break;
                }
            }
            return false;
        }

        public override void Render()
        {
            if (_animators == null || _animators.Length == 0) _animators = GetComponentsInChildren<Animator>(true);
            if (AlertSequence != _renderedAlertSequence)
            {
                _renderedAlertSequence = AlertSequence;
                CrossFade("Roar");
                _actionAnimationUntil = Time.time + 0.8f;
                return;
            }
            if (AttackSequence != _renderedAttackSequence)
            {
                _renderedAttackSequence = AttackSequence;
                CrossFade("Bite");
                _actionAnimationUntil = Time.time + 0.8f;
                return;
            }
            if (Time.time < _actionAnimationUntil) return;
            var desired = IsMoving ? "Walk" : "Idle";
            if (desired == _currentLocomotionAnimation) return;
            _currentLocomotionAnimation = desired;
            CrossFade(desired);
        }

        private void CrossFade(string state)
        {
            foreach (var animator in _animators)
                if (animator != null) animator.CrossFade(state, 0.05f);
        }
    }
}
