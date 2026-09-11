using System;
using System.Collections.Generic;
using EchoProtocol.AI.Listener.Noise;
using EchoProtocol.Tools.Scanner;
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
        [SerializeField] private NetworkObject _fieldScannerPickupPrefab;
        [SerializeField] private NetworkObject _noiseMakerPickupPrefab;
        [SerializeField] private NetworkObject _firstAidPickupPrefab;
        [SerializeField] private NetworkObject _doorJammerPickupPrefab;
        [SerializeField] private GameObject _noiseMakerBeaconPrefab; // Gán DistressBeaconDeployed prefab trong Inspector
        [SerializeField] private AudioClip _coreStabilizerPulseClip;

        [Networked] private uint LastProcessedSequence { get; set; }
        [Networked] private TickTimer TeamToolCooldown { get; set; }
        [Networked] private TickTimer HelpPingCooldown { get; set; }
        [Networked] private uint TeamToolOrdinal { get; set; }
        [Networked] private uint HelpPingOrdinal { get; set; }

        private TickTimer _stabilizerScanTimer;
        private readonly List<LobbyPlayerState> _stabilizedAllies = new List<LobbyPlayerState>();

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
            _teamToolAction.AddBinding("<Mouse>/leftButton");
            _helpPingAction = new InputAction("HelpPing", InputActionType.Button, "<Keyboard>/h");
        }

        private void Start()
        {
            if (Object == null || !Object.IsValid)
            {
                _interactAction?.Enable();
                _dropCoreAction?.Enable();
                _teamToolAction?.Enable();
                _helpPingAction?.Enable();
            }
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
            ClearStabilizedAllies();
        }

        private void OnDestroy()
        {
            _dropCoreAction?.Dispose();
            _teamToolAction?.Dispose();
            _helpPingAction?.Dispose();
            ClearStabilizedAllies();
        }

        public override void FixedUpdateNetwork()
        {
            if (Object != null && Object.HasStateAuthority)
            {
                UpdateCoreStabilizerAuthoritative();
            }
        }

        private void Update()
        {
            bool isOnline = Runner != null && Runner.IsRunning && Object != null && Object.IsValid;
            var playerState = GetComponent<LobbyPlayerState>();
            if (isOnline && (!Object.HasInputAuthority || (playerState != null && playerState.Object != null && playerState.Object.IsValid && !playerState.IsGameplayPlayer)))
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
                RequestDropCarriedItem();
            }
            if (_teamToolAction?.WasPerformedThisFrame() == true) RequestUseTeamTool();

            if (_interactAction?.WasPressedThisFrame() != true) return;

            if (CurrentCandidate != null)
            {
                if (CurrentCandidate is NetworkSlidingDoor brokenDoor && brokenDoor.CanAcceptJammer())
                {
                    if (playerState != null && playerState.Object != null && playerState.Object.IsValid && playerState.Object.Id.IsValid && playerState.Runner != null && playerState.Runner.IsRunning && playerState.ToolId == 4)
                    {
                        RequestUseTeamTool();
                        return;
                    }
                }

                RequestInteraction(CurrentCandidate);
                return;
            }

            if (TryDetectReviveCandidate(out var targetLifeState))
            {
                // Only FAK-equipped players can revive teammates.
                var ps = GetComponent<LobbyPlayerState>();
                if (ps != null && ps.ToolId == 3)
                    RequestRevive(targetLifeState);
            }
        }

        public bool RequestRevive(NetworkPlayerLifeState target)
        {
            if (Object == null || !Object.HasInputAuthority || target == null || target.Object == null)
            {
                return false;
            }

            RpcRequestRevive(target.Object.Id, NextSequence());
            return true;
        }

        public bool RequestDropCarriedCore()
        {
            if (Object == null || !Object.HasInputAuthority) return false;
            RpcRequestDropCarriedCore(NextSequence());
            return true;
        }

        public bool RequestDropCarriedItem()
        {
            if (!Object.HasInputAuthority) return false;

            var playerState = GetComponent<LobbyPlayerState>();
            if (playerState != null && playerState.CarriedCoreId.IsValid)
            {
                return RequestDropCarriedCore();
            }

            if (playerState != null && playerState.ToolId >= 1 && playerState.ToolId <= 4)
            {
                return RequestDropTeamTool();
            }

            return false;
        }

        public bool RequestDropTeamTool()
        {
            if (!Object.HasInputAuthority) return false;

            var playerState = GetComponent<LobbyPlayerState>();
            if (playerState == null
                || playerState.CarriedCoreId.IsValid
                || playerState.ToolId < 1
                || playerState.ToolId > 4)
            {
                return false;
            }

            RpcRequestDropTeamTool(NextSequence());
            return true;
        }

        public bool RequestUseTeamTool()
        {
            bool isOnline = Runner != null && Runner.IsRunning && Object != null && Object.IsValid;
            if (isOnline && !Object.HasInputAuthority) return false;

            var playerState = GetComponent<LobbyPlayerState>();
            if (isOnline && playerState != null && playerState.CarriedCoreId.IsValid) return false;
            var scanner = GetComponent<EchoProtocol.Tools.Scanner.NetworkFieldScanner>();
            if (scanner != null && scanner.IsScannerEquipped())
            {
                return scanner.RequestScan();
            }

            int toolId = (isOnline && playerState != null && playerState.ToolId > 0) ? playerState.ToolId : 0;
            var inv = GetComponent<PlayerInventory>();
            if (toolId == 0 && inv != null && inv.TeamToolSlot != null)
            {
                toolId = PlayerInventory.ResolveToolId(inv.TeamToolSlot);
            }

            if (toolId == 1 && scanner != null)
            {
                return scanner.RequestScan();
            }

            if (!isOnline)
            {
                return ExecuteTeamToolOffline(toolId, inv, playerState);
            }

            var targetId = default(NetworkId);
            if (playerState != null
                && playerState.ToolId == 4
                && TryDetectLocalDoorJammerTargetIntent(out var doorTargetId))
            {
                targetId = doorTargetId;
            }
            else if (playerState != null && playerState.ToolId == 3)
            {
                if (TryDetectReviveCandidate(out var allyLifeState) && allyLifeState != null && allyLifeState.Object != null)
                {
                    targetId = allyLifeState.Object.Id;
                }
                else
                {
                    // No downed ally in range — FAK cannot be used on self or alive allies.
                    return false;
                }
            }

            RpcRequestUseTeamTool(NextSequence(), targetId);
            return true;
        }

        private bool ExecuteTeamToolOffline(int toolId, PlayerInventory inv, LobbyPlayerState playerState)
        {
            string toolType = ToolTypeFor(toolId);
            if (toolType == "FIRST_AID_KIT")
            {
                var downState = GetComponent<PlayerDownState>();
                if (downState != null && downState.Health < 100f)
                {
                    downState.ApplyHeal(100f);
                    if (inv != null && inv.TeamToolSlot != null)
                    {
                        inv.TryRemove(inv.TeamToolSlot);
                    }
                    return true;
                }
                return false;
            }
            else if (toolType == "CORE_STABILIZER")
            {
                if (_coreStabilizerPulseClip != null)
                {
                    AudioSource.PlayClipAtPoint(_coreStabilizerPulseClip, transform.position);
                }
                return true;
            }
            else if (toolType == "DOOR_JAMMER")
            {
                if (CurrentCandidate is NetworkSlidingDoor door && door.CanAcceptJammer())
                {
                    door.DeployJammerOffline(gameObject);
                    if (playerState != null) playerState.SetGameplayToolId(0);
                    if (inv != null && inv.TeamToolSlot != null) inv.TryRemove(inv.TeamToolSlot);
                    return true;
                }
            }
            return false;
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
            if (Physics.Raycast(
                    ray,
                    out var hit,
                    _localDetectionDistance,
                    _interactionLayers,
                    QueryTriggerInteraction.Collide))
            {
                var door = hit.collider.GetComponentInParent<NetworkSlidingDoor>();
                if (door != null && door.Object != null && door.Object.Id.IsValid)
                {
                    targetId = door.Object.Id;
                    return true;
                }
            }

            var hits = Physics.SphereCastAll(ray, 0.5f, _localDetectionDistance, _interactionLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits.Length; i++)
            {
                var door = hits[i].collider.GetComponentInParent<NetworkSlidingDoor>();
                if (door != null && door.Object != null && door.Object.Id.IsValid && door.CanAcceptJammer())
                {
                    targetId = door.Object.Id;
                    return true;
                }
            }

            return false;
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
                // Server-side guard: only FAK holders may revive.
                var requesterState = GetComponent<LobbyPlayerState>();
                if (requesterState == null || requesterState.ToolId != 3)
                {
                    result = InteractionValidationResult.InvalidRequester;
                }
                else if (!Runner.TryFindObject(targetId, out var targetObject)
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

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcRequestDropTeamTool(uint sequence, RpcInfo info = default)
        {
            if (!TryResolveRequester(info.Source, out var requester))
            {
                return;
            }

            var result = ValidateRequester(requester, sequence);
            var state = GetComponent<LobbyPlayerState>();
            var targetId = default(NetworkId);
            if (result == InteractionValidationResult.Accepted)
            {
                var lifeState = GetComponent<NetworkPlayerLifeState>();
                if (lifeState != null && !lifeState.CanInitiateAction)
                {
                    result = InteractionValidationResult.InvalidRequester;
                }
                else if (state == null || state.CarriedCoreId.IsValid)
                {
                    result = InteractionValidationResult.InvalidTargetState;
                }
                else if (state.ToolId < 1 || state.ToolId > 4)
                {
                    result = InteractionValidationResult.InvalidTargetState;
                }
                else if (!TrySpawnDroppedTeamToolAuthoritative(state.ToolId, out targetId))
                {
                    result = InteractionValidationResult.InvalidTarget;
                }
                else
                {
                    state.SetGameplayToolId(0);
                }
            }

            if (sequence > LastProcessedSequence) LastProcessedSequence = sequence;
            RpcInteractionResult(requester, targetId, sequence, (int)result);
        }

        private bool TrySpawnDroppedTeamToolAuthoritative(int toolId, out NetworkId pickupId)
        {
            pickupId = default;
            var prefab = TeamToolPickupPrefabFor(toolId);
            if (prefab == null || Runner == null)
            {
                return false;
            }

            GetAuthoritativeDropPose(out var dropPosition, out var dropRotation);
            var pickupObject = Runner.Spawn(prefab, dropPosition, dropRotation);
            var validPickup = pickupObject != null
                && ((pickupObject.TryGetComponent<NetworkTeamToolPickup>(out var teamToolPickup)
                        && teamToolPickup.ToolId == toolId)
                    || (pickupObject.TryGetComponent<NetworkToolPickup>(out var scannerPickup)
                        && scannerPickup.ToolId == toolId));
            if (!validPickup)
            {
                if (pickupObject != null)
                {
                    Runner.Despawn(pickupObject);
                }

                return false;
            }

            pickupId = pickupObject.Id;
            return true;
        }

        private NetworkObject TeamToolPickupPrefabFor(int toolId)
        {
            switch (toolId)
            {
                case 1: return _fieldScannerPickupPrefab;
                case 2: return _noiseMakerPickupPrefab;
                case 3: return _firstAidPickupPrefab;
                case 4: return _doorJammerPickupPrefab;
                default: return null;
            }
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
                    if (toolType == "FIELD_SCANNER")
                    {
                        if (sequence > LastProcessedSequence) LastProcessedSequence = sequence;
                        RpcInteractionResult(
                            requester,
                            targetId,
                            sequence,
                            (int)InteractionValidationResult.InvalidTargetState);
                        return;
                    }

                    if (toolType == "FIRST_AID_KIT")
                    {
                        var fakResult = TryUseFirstAidKitAuthoritative(requester, state, targetId);
                        if (sequence > LastProcessedSequence) LastProcessedSequence = sequence;
                        RpcInteractionResult(requester, targetId, sequence, (int)fakResult);
                        return;
                    }

                    if (toolType == "NOISE_MAKER")
                    {
                        var noiseMakerResult = TryUseNoiseMakerAuthoritative(requester, state);
                        if (sequence > LastProcessedSequence) LastProcessedSequence = sequence;
                        RpcInteractionResult(requester, targetId, sequence, (int)noiseMakerResult);
                        return;
                    }

                    if (toolType == "DOOR_JAMMER")
                    {
                        var jammerResult = TryDeployDoorJammerAuthoritative(requester, state, targetId);
                        if (sequence > LastProcessedSequence) LastProcessedSequence = sequence;
                        RpcInteractionResult(requester, targetId, sequence, (int)jammerResult);
                        return;
                    }

                    if (toolType == "CORE_STABILIZER")
                    {
                        TeamToolOrdinal++;
                        MatchAuthorityRuntime.Instance?.RecordTeamToolUsed(
                            requester,
                            $"player:{Object.Id}:tool:{TeamToolOrdinal}",
                            toolType);
                        TeamToolCooldown = TickTimer.CreateFromSeconds(Runner, 5f);
                        float radius = 2.5f;
                        var hits = Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Collide);
                        bool stabilizedAny = false;
                        for (int i = 0; i < hits.Length; i++)
                        {
                            var h = hits[i];
                            if (h == null || h.transform == transform || h.transform.IsChildOf(transform)) continue;
                            var carrierState = h.GetComponentInParent<LobbyPlayerState>();
                            if (carrierState != null && carrierState.Object != null && carrierState.Object.IsValid && carrierState.CarriedCoreId.IsValid)
                            {
                                carrierState.SetCoreStabilizedAuthoritative(true);
                                stabilizedAny = true;
                            }
                        }

                        if (_coreStabilizerPulseClip != null)
                        {
                            AudioSource.PlayClipAtPoint(_coreStabilizerPulseClip, transform.position);
                        }

                        RpcPlayCoreStabilizerFeedback(transform.position, stabilizedAny);
                    }
                }
            }

            if (sequence > LastProcessedSequence) LastProcessedSequence = sequence;
        }

        private InteractionValidationResult TryUseFirstAidKitAuthoritative(
            PlayerRef requester,
            LobbyPlayerState state,
            NetworkId targetId)
        {
            if (!Object.HasStateAuthority || state == null || state.ToolId != 3)
            {
                return InteractionValidationResult.InvalidRequester;
            }

            var selfLifeState = GetComponent<NetworkPlayerLifeState>();
            if (selfLifeState == null || !NetworkPlayerLifeStateRules.CanInitiateAction(selfLifeState.Status))
            {
                return InteractionValidationResult.InvalidRequester;
            }

            NetworkPlayerLifeState targetLife = null;
            if (targetId.IsValid && targetId != Object.Id && Runner != null && Runner.TryFindObject(targetId, out var targetObj) && targetObj != null)
            {
                targetLife = targetObj.GetComponent<NetworkPlayerLifeState>();
            }

            // Case 1: Target ally is Downed → Revive ally (FAK is the only way to revive)
            if (targetLife != null && targetLife.Status == NetworkPlayerLifeStatus.Downed)
            {
                if (Vector3.Distance(transform.position, targetLife.transform.position) <= 4f)
                {
                    if (targetLife.TryStartRevive(requester))
                    {
                        TeamToolOrdinal++;
                        MatchAuthorityRuntime.Instance?.RecordTeamToolUsed(
                            requester,
                            $"player:{Object.Id}:tool:{TeamToolOrdinal}",
                            "FIRST_AID_KIT");
                        TeamToolCooldown = TickTimer.CreateFromSeconds(Runner, 2f);
                        ConsumeGameplayTeamTool(state);
                        return InteractionValidationResult.Accepted;
                    }
                }
                return InteractionValidationResult.OutOfRange;
            }

            // FAK can only revive Downed allies — no heal on alive targets or self.
            return InteractionValidationResult.InvalidTargetState;
        }

        private InteractionValidationResult TryUseNoiseMakerAuthoritative(
            PlayerRef requester,
            LobbyPlayerState state)
        {
            if (!Object.HasStateAuthority || state == null || state.ToolId != 2)
            {
                return InteractionValidationResult.InvalidRequester;
            }

            if (Runner == null)
            {
                return InteractionValidationResult.InvalidTarget;
            }

            GetAuthoritativeDropPose(out var beaconPos, out _);
            var flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (flatForward == Vector3.zero)
            {
                flatForward = transform.forward;
            }

            beaconPos = transform.position + flatForward * 3f;
            if (Physics.Raycast(
                    beaconPos + Vector3.up * 1.5f,
                    Vector3.down,
                    out var bHit,
                    4f,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                beaconPos = bHit.point + Vector3.up * 0.05f;
            }

            var networkPrefab = _noiseMakerBeaconPrefab != null
                ? _noiseMakerBeaconPrefab.GetComponent<NetworkObject>()
                : null;
            if (networkPrefab == null)
            {
                return InteractionValidationResult.InvalidTarget;
            }

            var beaconObject = Runner.Spawn(
                networkPrefab,
                beaconPos,
                Quaternion.identity);
            if (beaconObject == null
                || !beaconObject.TryGetComponent<NoiseMakerBeacon>(out var beacon))
            {
                if (beaconObject != null)
                {
                    Runner.Despawn(beaconObject);
                }

                return InteractionValidationResult.InvalidTarget;
            }

            TeamToolOrdinal++;
            beacon.Initialize(
                requester,
                Object.Id.ToString(),
                (long)TeamToolOrdinal);
            MatchAuthorityRuntime.Instance?.RecordTeamToolUsed(
                requester,
                $"player:{Object.Id}:tool:{TeamToolOrdinal}",
                "NOISE_MAKER");
            TeamToolCooldown = TickTimer.CreateFromSeconds(Runner, 5f);
            ConsumeGameplayTeamTool(state);
            return InteractionValidationResult.Accepted;
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
                if (collider == null || (collider.isTrigger && !collider.gameObject.name.Contains("Interaction")))
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
                case 6: return "CORE_STABILIZER";
                default: return null;
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcPlayCoreStabilizerFeedback(Vector3 position, NetworkBool stabilizedAny)
        {
            if (_coreStabilizerPulseClip != null)
            {
                AudioSource.PlayClipAtPoint(_coreStabilizerPulseClip, position);
            }
        }

        private void UpdateCoreStabilizerAuthoritative()
        {
            var state = GetComponent<LobbyPlayerState>();
            bool hasStabilizer = state != null && state.IsGameplayPlayer && state.ToolId == 6;

            if (!hasStabilizer)
            {
                ClearStabilizedAllies();
                return;
            }

            if (!_stabilizerScanTimer.ExpiredOrNotRunning(Runner)) return;
            _stabilizerScanTimer = TickTimer.CreateFromSeconds(Runner, 0.25f);

            float radius = 2.5f;
            var hits = Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Collide);
            var currentAllies = new HashSet<LobbyPlayerState>();

            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h == null || h.transform == transform || h.transform.IsChildOf(transform)) continue;
                var carrierState = h.GetComponentInParent<LobbyPlayerState>();
                if (carrierState != null && carrierState.Object != null && carrierState.Object.IsValid && carrierState.CarriedCoreId.IsValid)
                {
                    currentAllies.Add(carrierState);
                    carrierState.SetCoreStabilizedAuthoritative(true);
                }
            }

            for (int i = _stabilizedAllies.Count - 1; i >= 0; i--)
            {
                var ally = _stabilizedAllies[i];
                if (ally == null || !currentAllies.Contains(ally))
                {
                    if (ally != null && ally.Object != null && ally.Object.IsValid)
                    {
                        ally.SetCoreStabilizedAuthoritative(false);
                    }
                    _stabilizedAllies.RemoveAt(i);
                }
            }

            foreach (var ally in currentAllies)
            {
                if (!_stabilizedAllies.Contains(ally))
                {
                    _stabilizedAllies.Add(ally);
                }
            }
        }

        private void ClearStabilizedAllies()
        {
            if (_stabilizedAllies == null || _stabilizedAllies.Count == 0) return;
            for (int i = 0; i < _stabilizedAllies.Count; i++)
            {
                if (_stabilizedAllies[i] != null && _stabilizedAllies[i].Object != null && _stabilizedAllies[i].Object.IsValid)
                {
                    _stabilizedAllies[i].SetCoreStabilizedAuthoritative(false);
                }
            }
            _stabilizedAllies.Clear();
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
