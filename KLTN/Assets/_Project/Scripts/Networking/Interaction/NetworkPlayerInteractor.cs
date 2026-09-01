using System;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

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

        private InputAction _interactAction;
        private uint _nextSequence;

        private void Awake()
        {
            _interactAction = _inputActions?.FindActionMap("Player", false)?.FindAction("Interact", false);
        }

        public override void Spawned()
        {
            if (Object.HasInputAuthority) _interactAction?.Enable();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _interactAction?.Disable();
        }

        private void Update()
        {
            if (Object == null || !Object.HasInputAuthority || _interactAction?.WasPerformedThisFrame() != true) return;
            if (!GetComponent<LobbyPlayerState>().IsGameplayPlayer) return;

            if (TryDetectCandidate(out var candidate))
            {
                RequestInteraction(candidate);
            }
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
