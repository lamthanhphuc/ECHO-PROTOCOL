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

        private void Awake()
        {
            _interactAction = _inputActions?.FindActionMap("Player", false)?.FindAction("Interact", false);
            _dropCoreAction = new InputAction("DropCore", InputActionType.Button, "<Keyboard>/g");
            _teamToolAction = new InputAction("UseTeamTool", InputActionType.Button, "<Keyboard>/t");
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
            if (Object == null || !Object.HasInputAuthority) return;
            if (!GetComponent<LobbyPlayerState>().IsGameplayPlayer) return;

            if (_dropCoreAction?.WasPerformedThisFrame() == true)
            {
                RequestDropCarriedCore();
            }
            if (_teamToolAction?.WasPerformedThisFrame() == true) RequestUseTeamTool();
            if (_helpPingAction?.WasPerformedThisFrame() == true) RequestHelpPing();

            if (_interactAction?.WasPerformedThisFrame() != true) return;

            if (TryDetectCandidate(out var candidate))
            {
                RequestInteraction(candidate);
                return;
            }

            if (TryDetectReviveCandidate(out var lifeState))
            {
                RequestRevive(lifeState);
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
            RpcRequestUseTeamTool(NextSequence());
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
            var origin = _rayOrigin != null ? _rayOrigin : transform;

            if (Physics.Raycast(
                    origin.position,
                    origin.forward,
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
            var origin = _rayOrigin != null ? _rayOrigin : transform;
            if (Physics.Raycast(
                    origin.position,
                    origin.forward,
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

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcRequestRevive(NetworkId targetId, uint sequence, RpcInfo info = default)
        {
            if (!TryResolveRequester(info.Source, out var requester)
                || ValidateRequester(requester, sequence) != InteractionValidationResult.Accepted)
            {
                return;
            }

            if (Runner.TryFindObject(targetId, out var targetObject)
                && targetObject.TryGetComponent<NetworkPlayerLifeState>(out var lifeState)
                && Vector3.SqrMagnitude(transform.position - lifeState.transform.position)
                    <= _localDetectionDistance * _localDetectionDistance)
            {
                lifeState.TryRevive(requester);
            }

            if (sequence > LastProcessedSequence) LastProcessedSequence = sequence;
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
            var candidate = transform.position + transform.forward * 1.25f;
            var rayOrigin = candidate + Vector3.up * 1.5f;
            position = Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out var hit,
                3f,
                ~0,
                QueryTriggerInteraction.Ignore)
                ? hit.point + Vector3.up * 0.25f
                : candidate;
            rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcRequestUseTeamTool(uint sequence, RpcInfo info = default)
        {
            if (!TryResolveRequester(info.Source, out var requester)
                || ValidateRequester(requester, sequence) != InteractionValidationResult.Accepted)
            {
                return;
            }

            var state = GetComponent<LobbyPlayerState>();
            if (state.ToolId > 0 && TeamToolCooldown.ExpiredOrNotRunning(Runner))
            {
                var toolType = ToolTypeFor(state.ToolId);
                if (toolType != null)
                {
                    TeamToolOrdinal++;
                    MatchAuthorityRuntime.Instance?.RecordTeamToolUsed(
                        requester,
                        $"player:{Object.Id}:tool:{TeamToolOrdinal}",
                        toolType);
                    TeamToolCooldown = TickTimer.CreateFromSeconds(Runner, 5f);
                    if (toolType == "NOISE_MAKER")
                    {
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
                }
            }

            if (sequence > LastProcessedSequence) LastProcessedSequence = sequence;
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
            if (lifeState != null && lifeState.Status == NetworkPlayerLifeStatus.Downed
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
