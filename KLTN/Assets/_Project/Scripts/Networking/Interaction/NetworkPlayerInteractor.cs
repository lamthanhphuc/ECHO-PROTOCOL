using System;
using EchoProtocol.AI.Listener.Noise;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using EchoProtocol.Networking.Authority;

namespace EchoProtocol.Networking
{
    /// <summary>Owned-player command gateway. All interaction RPCs pass through this behaviour.</summary>
    public sealed class NetworkPlayerInteractor : NetworkBehaviour
    {
        public static event Action<InteractionRequestResult> LocalRequestCompleted;

        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private Transform _rayOrigin;
        [SerializeField, Min(0.1f)] private float _localDetectionDistance = 3f;
        [SerializeField] private LayerMask _interactionLayers = ~0;
        [SerializeField] private GameObject _noiseMakerBeaconPrefab; // Gán DistressBeaconDeployed prefab trong Inspector

        [Networked] private uint LastProcessedSequence { get; set; }
        [Networked] private TickTimer TeamToolCooldown { get; set; }
        [Networked] private TickTimer HelpPingCooldown { get; set; }
        [Networked] private uint TeamToolOrdinal { get; set; }
        [Networked] private uint HelpPingOrdinal { get; set; }

        private InputAction _interactAction;
        private InputAction _dropCoreAction;
        private InputAction _teamToolAction;
        private InputAction _helpPingAction;
        private uint _nextSequence;

        public NetworkInteractable CurrentCandidate { get; private set; }

        private void Awake()
        {
            _interactAction = _inputActions?.FindActionMap("Player", false)?.FindAction("Interact", false);
            _dropCoreAction = new InputAction("DropCore", InputActionType.Button, "<Keyboard>/g");
            _teamToolAction = new InputAction("UseTeamTool", InputActionType.Button);
            _teamToolAction.AddBinding("<Keyboard>/t");
            _teamToolAction.AddBinding("<Mouse>/leftButton");
            _helpPingAction = new InputAction("HelpPing", InputActionType.Button, "<Keyboard>/h");
        }

        public override void Spawned()
        {
            if (!Object.HasInputAuthority) return;
            _interactAction?.Enable();
            _dropCoreAction?.Enable();
            _teamToolAction?.Enable();
            _helpPingAction?.Enable();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            CurrentCandidate = null;
            _interactAction?.Disable();
            _dropCoreAction?.Disable();
            _teamToolAction?.Disable();
            _helpPingAction?.Disable();
        }

        private void OnDestroy()
        {
            _dropCoreAction?.Dispose();
            _teamToolAction?.Dispose();
            _helpPingAction?.Dispose();
        }

        private void Update()
        {
            if (Object == null || !Object.HasInputAuthority)
            {
                CurrentCandidate = null;
                return;
            }
            if (!GetComponent<LobbyPlayerState>().IsGameplayPlayer)
            {
                CurrentCandidate = null;
                return;
            }

            if (_helpPingAction?.WasPerformedThisFrame() == true) RequestHelpPing();

            var lifeState = GetComponent<NetworkPlayerLifeState>();
            if (lifeState != null && !lifeState.CanInitiateAction)
            {
                CurrentCandidate = null;
                return;
            }

            CurrentCandidate = TryDetectCandidate(out var candidate) ? candidate : null;

            if (_dropCoreAction?.WasPerformedThisFrame() == true)
            {
                RequestDropCarriedCore();
            }
            if (_teamToolAction?.WasPerformedThisFrame() == true) RequestUseTeamTool();

            if (_interactAction?.WasPressedThisFrame() != true) return;

            if (CurrentCandidate != null)
            {
                RequestInteraction(CurrentCandidate);
                return;
            }

            if (TryDetectReviveCandidate(out var targetLifeState))
            {
                RequestRevive(targetLifeState);
            }
        }

        public bool RequestRevive(NetworkPlayerLifeState target)
        {
            if (!Object.HasInputAuthority || target == null || target.Object == null)
            {
                return false;
            }

            RpcRequestRevive(target.Object.Id, NextSequence());
            return true;
        }

        public bool RequestDropCarriedCore()
        {
            if (!Object.HasInputAuthority) return false;
            RpcRequestDropCarriedCore(NextSequence());
            return true;
        }

        public bool RequestUseTeamTool()
        {
            if (!Object.HasInputAuthority) return false;
            var playerState = GetComponent<LobbyPlayerState>();
            if (playerState != null && playerState.CarriedCoreId.IsValid) return false;
            var scanner = GetComponent<EchoProtocol.Tools.Scanner.NetworkFieldScanner>();
            if (scanner != null && scanner.IsScannerEquipped())
            {
                return scanner.RequestScan();
            }

            if (playerState != null && playerState.ToolId == 1)
            {
                if (scanner != null)
                {
                    return scanner.RequestScan();
                }
            }

            var targetId = default(NetworkId);
            if (playerState != null
                && playerState.ToolId == 4
                && TryDetectLocalDoorJammerTargetIntent(out var doorTargetId))
            {
                targetId = doorTargetId;
            }

            RpcRequestUseTeamTool(NextSequence(), targetId);
            return true;
        }

        public bool RequestHelpPing()
        {
            if (!Object.HasInputAuthority) return false;
            RpcRequestHelpPing(NextSequence());
            return true;
        }

        public bool RequestInteraction(NetworkInteractable target)
        {
            if (!Object.HasInputAuthority)
            {
                CompleteLocally(default, 0, InteractionValidationResult.NotInputAuthority);
                return false;
            }
            if (target == null || target.Object == null)
            {
                CompleteLocally(default, 0, InteractionValidationResult.InvalidTarget);
                return false;
            }

            var command = new InteractionCommand(target.Object.Id, NextSequence());
            RpcRequestInteraction(command.TargetId, command.Sequence);
            Debug.Log($"[Interaction] Sent target={command.TargetId}, sequence={command.Sequence}.");
            return true;
        }

        private bool TryDetectCandidate(out NetworkInteractable candidate)
        {
            var ray = GetLocalDetectionRay();

            if (Physics.Raycast(
                    ray,
                    out var hit,
                    _localDetectionDistance,
                    _interactionLayers,
                    QueryTriggerInteraction.Collide))
            {
                candidate = hit.collider.GetComponentInParent<NetworkInteractable>();
                return candidate != null;
            }

            candidate = null;
            return false;
        }

        private bool TryDetectReviveCandidate(out NetworkPlayerLifeState lifeState)
        {
            var ray = GetLocalDetectionRay();
            if (Physics.Raycast(
                    ray,
                    out var hit,
                    _localDetectionDistance,
                    _interactionLayers,
                    QueryTriggerInteraction.Collide))
            {
                lifeState = hit.collider.GetComponentInParent<NetworkPlayerLifeState>();
                return lifeState != null && lifeState.Object != Object;
            }

            lifeState = null;
            return false;
        }

        private bool TryDetectLocalDoorJammerTargetIntent(out NetworkId targetId)
        {
            targetId = default;
            var ray = GetLocalDetectionRay();
            if (!Physics.Raycast(
                    ray,
                    out var hit,
                    _localDetectionDistance,
                    _interactionLayers,
                    QueryTriggerInteraction.Collide))
            {
                return false;
            }

            var door = hit.collider.GetComponentInParent<NetworkSlidingDoor>();
            if (door == null || door.Object == null || !door.Object.Id.IsValid)
            {
                return false;
            }

            targetId = door.Object.Id;
            return true;
        }

        private Ray GetLocalDetectionRay()
        {
            if (_rayOrigin != null)
            {
                return new Ray(_rayOrigin.position, _rayOrigin.forward);
            }

            var mainCamera = Camera.main;
            return mainCamera != null
                ? mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
                : new Ray(transform.position, transform.forward);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcRequestRevive(NetworkId targetId, uint sequence, RpcInfo info = default)
        {
            if (!TryResolveRequester(info.Source, out var requester))
            {
                return;
            }

            var result = ValidateRequester(requester, sequence);
            if (result == InteractionValidationResult.Accepted)
            {
                if (!Runner.TryFindObject(targetId, out var targetObject)
                    || targetObject == null
                    || !targetObject.TryGetComponent<NetworkPlayerLifeState>(out var targetLifeState))
                {
                    result = InteractionValidationResult.InvalidTarget;
                }
                else if (!targetLifeState.TryStartRevive(requester))
                {
                    result = Vector3.SqrMagnitude(transform.position - targetLifeState.transform.position)
                             > _localDetectionDistance * _localDetectionDistance
                        ? InteractionValidationResult.OutOfRange
                        : InteractionValidationResult.InvalidTargetState;
                }
            }

            if (sequence > LastProcessedSequence) LastProcessedSequence = sequence;
            Debug.Log(
                $"[LifeState] Revive request reviver={requester}, target={targetId}, " +
                $"sequence={sequence}, result={result}.");
            RpcInteractionResult(requester, targetId, sequence, (int)result);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcRequestDropCarriedCore(uint sequence, RpcInfo info = default)
        {
            if (!TryResolveRequester(info.Source, out var requester))
            {
                return;
            }

            var result = ValidateRequester(requester, sequence);
            var playerState = GetComponent<LobbyPlayerState>();
            var coreId = playerState != null ? playerState.CarriedCoreId : default;
            if (result == InteractionValidationResult.Accepted
                && coreId.IsValid
                && Runner.TryFindObject(coreId, out var coreObject)
                && coreObject.TryGetComponent<NetworkPickupItem>(out var core))
            {
                GetAuthoritativeDropPose(out var dropPosition, out var dropRotation);
                result = core.TryDrop(requester, dropPosition, dropRotation)
                    ? InteractionValidationResult.Accepted
                    : InteractionValidationResult.InvalidTargetState;
            }
            else if (result == InteractionValidationResult.Accepted)
            {
                result = InteractionValidationResult.InvalidTarget;
            }

            if (sequence > LastProcessedSequence) LastProcessedSequence = sequence;
            RpcInteractionResult(requester, coreId, sequence, (int)result);
        }

        private void GetAuthoritativeDropPose(out Vector3 position, out Quaternion rotation)
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (flatForward == Vector3.zero) flatForward = transform.forward;
            var candidate = transform.position + flatForward * 1.25f;
            var rayOrigin = candidate + Vector3.up * 1.5f;
            position = Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out var hit,
                4f,
                ~0,
                QueryTriggerInteraction.Ignore)
                ? hit.point + Vector3.up * 0.05f
                : candidate;
            rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            rotation = Quaternion.Euler(0f, transform.eulerAngles.y + 180f, 0f);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcRequestUseTeamTool(uint sequence, NetworkId targetId, RpcInfo info = default)
        {
            if (!TryResolveRequester(info.Source, out var requester)
                || ValidateRequester(requester, sequence) != InteractionValidationResult.Accepted)
            {
                return;
            }

            var state = GetComponent<LobbyPlayerState>();
            if (state != null && state.CarriedCoreId.IsValid)
            {
                return;
            }

            if (state != null && state.ToolId > 0 && TeamToolCooldown.ExpiredOrNotRunning(Runner))
            {
                var toolType = ToolTypeFor(state.ToolId);
                if (toolType != null)
                {
                    if (toolType == "DOOR_JAMMER")
                    {
                        var jammerResult = TryDeployDoorJammerAuthoritative(requester, state, targetId);
                        if (sequence > LastProcessedSequence) LastProcessedSequence = sequence;
                        RpcInteractionResult(requester, targetId, sequence, (int)jammerResult);
                        return;
                    }

                    TeamToolOrdinal++;
                    MatchAuthorityRuntime.Instance?.RecordTeamToolUsed(
                        requester,
                        $"player:{Object.Id}:tool:{TeamToolOrdinal}",
                        toolType);
                    TeamToolCooldown = TickTimer.CreateFromSeconds(Runner, 5f);
                    if (toolType == "NOISE_MAKER")
                    {
                        // Spawn beacon tại sàn phía trước player (3m)
                        GetAuthoritativeDropPose(out var beaconPos, out var beaconRot);
                        // Điều chỉnh forward xa hơn một chút cho throw feel
                        beaconPos = transform.position
                            + Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized * 3f;
                        // Floor-snap
                        if (Physics.Raycast(beaconPos + Vector3.up * 1.5f, Vector3.down, out var bHit, 4f,
                            ~0, QueryTriggerInteraction.Ignore))
                        {
                            beaconPos = bHit.point + Vector3.up * 0.05f;
                        }

                        var prefabToSpawn = _noiseMakerBeaconPrefab;
                        if (prefabToSpawn == null)
                        {
                            prefabToSpawn = Resources.Load<GameObject>("DistressBeaconDeployed");
                        }

                        if (prefabToSpawn != null)
                        {
                            var beaconGo = Instantiate(prefabToSpawn, beaconPos, Quaternion.identity);
                            var beacon = beaconGo.GetComponent<NoiseMakerBeacon>();
                            if (beacon != null)
                            {
                                beacon.Initialize(
                                    requester,
                                    Object.Id.ToString(),
                                    (long)TeamToolOrdinal);
                            }
                        }
                        else
                        {
                            // Fallback: phát 1 noise trực tiếp nếu chưa assign prefab
                            HostRuntimeNoiseService.EnsureExists(MatchAuthorityRuntime.Instance)
                                .TryAccept(
                                    requester,
                                    RuntimeNoiseType.NOISE_MAKER,
                                    RuntimeNoiseSourceOccurrenceKey.ForTeamTool(
                                        Object.Id.ToString(),
                                        toolType,
                                        sequence),
                                    transform.position,
                                    out _);
                        }

                        // Consumes tool from player state & inventory
                        ConsumeGameplayTeamTool(state);
                    }
                }
            }

            if (sequence > LastProcessedSequence) LastProcessedSequence = sequence;
        }

        private InteractionValidationResult TryDeployDoorJammerAuthoritative(
            PlayerRef requester,
            LobbyPlayerState state,
            NetworkId targetId)
        {
            if (!Object.HasStateAuthority || state == null || state.ToolId != 4)
            {
                return InteractionValidationResult.InvalidRequester;
            }

            var targetResult = TryResolveDoorJammerTargetAuthoritative(targetId, out var door);
            if (targetResult != InteractionValidationResult.Accepted)
            {
                return targetResult;
            }

            if (!door.CanAcceptJammer())
            {
                return InteractionValidationResult.InvalidTargetState;
            }

            var prefab = door.DoorJammerPrefab != null
                ? door.DoorJammerPrefab
                : Resources.Load<NetworkObject>("Network/PF_DoorJammer");
            if (prefab == null || Runner == null)
            {
                return InteractionValidationResult.InvalidTarget;
            }

            door.TryGetJammerPlacement(out var position, out var rotation);
            var jammerObject = Runner.Spawn(prefab, position, rotation);
            if (jammerObject == null || !jammerObject.TryGetComponent<NetworkDoorJammer>(out var jammer))
            {
                if (jammerObject != null)
                {
                    Runner.Despawn(jammerObject);
                }

                return InteractionValidationResult.InvalidTarget;
            }

            if (!door.TryAttachJammerAuthoritative(jammer))
            {
                Runner.Despawn(jammerObject);
                return InteractionValidationResult.InvalidTargetState;
            }

            TeamToolOrdinal++;
            MatchAuthorityRuntime.Instance?.RecordTeamToolUsed(
                requester,
                $"player:{Object.Id}:tool:{TeamToolOrdinal}",
                "DOOR_JAMMER");
            TeamToolCooldown = TickTimer.CreateFromSeconds(Runner, 5f);
            ConsumeGameplayTeamTool(state);
            return InteractionValidationResult.Accepted;
        }

        private InteractionValidationResult TryResolveDoorJammerTargetAuthoritative(
            NetworkId targetId,
            out NetworkSlidingDoor door)
        {
            door = null;
            if (Runner == null
                || !targetId.IsValid
                || !Runner.TryFindObject(targetId, out var targetObject)
                || targetObject == null
                || !targetObject.TryGetComponent(out door))
            {
                return InteractionValidationResult.InvalidTarget;
            }

            if (!IsDoorJammerTargetInAuthoritativeRange(door))
            {
                return InteractionValidationResult.OutOfRange;
            }

            return HasUnobstructedDoorJammerInteraction(door)
                ? InteractionValidationResult.Accepted
                : InteractionValidationResult.InvalidTarget;
        }

        private bool IsDoorJammerTargetInAuthoritativeRange(NetworkSlidingDoor door)
        {
            var playerPosition = transform.position;
            var targetPoint = GetClosestDoorInteractionPoint(door, playerPosition);
            return Vector3.SqrMagnitude(targetPoint - playerPosition)
                   <= _localDetectionDistance * _localDetectionDistance;
        }

        private bool HasUnobstructedDoorJammerInteraction(NetworkSlidingDoor door)
        {
            var origin = GetAuthoritativeInteractionOrigin();
            var targetPoint = GetClosestDoorInteractionPoint(door, origin);
            var direction = targetPoint - origin;
            var distance = direction.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                return true;
            }

            var hits = Physics.RaycastAll(
                origin,
                direction / distance,
                distance,
                _interactionLayers,
                QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (var i = 0; i < hits.Length; i++)
            {
                var hitCollider = hits[i].collider;
                if (hitCollider == null || IsSelfCollider(hitCollider))
                {
                    continue;
                }

                return hitCollider.GetComponentInParent<NetworkSlidingDoor>() == door;
            }

            return true;
        }

        private Vector3 GetAuthoritativeInteractionOrigin()
        {
            return _rayOrigin != null
                ? _rayOrigin.position
                : transform.position + Vector3.up * 1.5f;
        }

        private Vector3 GetClosestDoorInteractionPoint(NetworkSlidingDoor door, Vector3 origin)
        {
            var colliders = door.GetComponentsInChildren<Collider>(true);
            var hasCollider = false;
            var closestPoint = door.transform.position;
            var closestDistance = float.PositiveInfinity;
            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider == null || collider.isTrigger)
                {
                    continue;
                }

                var point = collider.ClosestPoint(origin);
                var distance = Vector3.SqrMagnitude(point - origin);
                if (!hasCollider || distance < closestDistance)
                {
                    hasCollider = true;
                    closestDistance = distance;
                    closestPoint = point;
                }
            }

            return closestPoint;
        }

        private bool IsSelfCollider(Collider candidate)
        {
            return candidate.transform == transform || candidate.transform.IsChildOf(transform);
        }

        private void ConsumeGameplayTeamTool(LobbyPlayerState state)
        {
            state.SetGameplayToolId(0);
            var inv = GetComponent<PlayerInventory>();
            if (inv != null && inv.TeamToolSlot != null)
            {
                inv.TryRemove(inv.TeamToolSlot);
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcRequestHelpPing(uint sequence, RpcInfo info = default)
        {
            if (!TryResolveRequester(info.Source, out var requester)
                || ValidateRequester(requester, sequence) != InteractionValidationResult.Accepted)
            {
                return;
            }

            var lifeState = GetComponent<NetworkPlayerLifeState>();
            if (lifeState != null && lifeState.IsDowned
                && HelpPingCooldown.ExpiredOrNotRunning(Runner))
            {
                HelpPingOrdinal++;
                MatchAuthorityRuntime.Instance?.RecordHelpPingUsed(
                    requester,
                    $"player:{Object.Id}:help-ping:{HelpPingOrdinal}",
                    transform.position);
                HelpPingCooldown = TickTimer.CreateFromSeconds(Runner, 3f);
            }

            if (sequence > LastProcessedSequence) LastProcessedSequence = sequence;
        }

        private static string ToolTypeFor(int toolId)
        {
            switch (toolId)
            {
                case 1: return "FIELD_SCANNER";
                case 2: return "NOISE_MAKER";
                case 3: return "FIRST_AID_KIT";
                case 4: return "DOOR_JAMMER";
                default: return null;
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcRequestInteraction(NetworkId targetId, uint sequence, RpcInfo info = default)
        {
            if (!TryResolveRequester(info.Source, out var requester))
            {
                Debug.LogWarning(
                    $"[Interaction] Rejected request from {info.Source}; owner is {Object.InputAuthority}.");
                return;
            }

            var result = ValidateRequester(requester, sequence);
            NetworkInteractable target = null;

            if (result == InteractionValidationResult.Accepted &&
                (!Runner.TryFindObject(targetId, out var targetObject) ||
                 !targetObject.TryGetComponent(out target)))
            {
                result = InteractionValidationResult.InvalidTarget;
            }

            if (result == InteractionValidationResult.Accepted)
            {
                var context = new InteractionContext(this, target, requester);
                result = target.ValidateInteraction(context);
                if (result == InteractionValidationResult.Accepted)
                {
                    target.ExecuteAuthoritative(context);
                    if (target.EmitsRuntimeInteractionNoise)
                    {
                        HostRuntimeNoiseService.EnsureExists(MatchAuthorityRuntime.Instance)
                            .TryAccept(
                                requester,
                                RuntimeNoiseType.INTERACTION,
                                RuntimeNoiseSourceOccurrenceKey.ForInteraction(
                                    Object.Id.ToString(),
                                    sequence),
                                target.RuntimeInteractionNoiseOrigin,
                                out _);
                    }
                }
            }

            // Consume every new sequence, including rejected commands, so it cannot be replayed later.
            if (sequence > LastProcessedSequence) LastProcessedSequence = sequence;

            Debug.Log(
                $"[Interaction] Requester={requester}, target={targetId}, sequence={sequence}, result={result}.");
            RpcInteractionResult(requester, targetId, sequence, (int)result);
        }

        private bool TryResolveRequester(PlayerRef source, out PlayerRef requester)
        {
            return RpcRequesterResolver.TryResolveEffectiveRequester(
                source,
                Object.InputAuthority,
                Object.HasStateAuthority,
                Object.HasInputAuthority,
                out requester);
        }

        private InteractionValidationResult ValidateRequester(PlayerRef source, uint sequence)
        {
            if (!Object.HasStateAuthority || !source.IsValid || source != Object.InputAuthority)
            {
                return InteractionValidationResult.InvalidRequester;
            }
            if (!Runner.TryGetPlayerObject(source, out var ownedPlayer) || ownedPlayer != Object)
            {
                return InteractionValidationResult.InvalidRequester;
            }

            var playerState = GetComponent<LobbyPlayerState>();
            if (playerState == null || !playerState.IsGameplayPlayer)
            {
                return InteractionValidationResult.InvalidRequester;
            }
            return sequence == 0 || sequence <= LastProcessedSequence
                ? InteractionValidationResult.DuplicateRequest
                : InteractionValidationResult.Accepted;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        private void RpcInteractionResult(
            [RpcTarget] PlayerRef targetPlayer,
            NetworkId targetId,
            uint sequence,
            int result)
        {
            CompleteLocally(targetId, sequence, (InteractionValidationResult)result);
        }

        private uint NextSequence()
        {
            _nextSequence++;
            if (_nextSequence == 0) _nextSequence = 1;
            return _nextSequence;
        }

        private static void CompleteLocally(
            NetworkId targetId,
            uint sequence,
            InteractionValidationResult result)
        {
            LocalRequestCompleted?.Invoke(new InteractionRequestResult(targetId, sequence, result));
        }
    }
}
