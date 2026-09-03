using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking
{
    /// <summary>Base validation shared by every authoritative network interaction target.</summary>
    public abstract class NetworkInteractable : NetworkBehaviour, IAuthoritativeNetworkInteractable
    {
        [SerializeField, Min(0.1f)] private float _interactionDistance = 3f;
        [SerializeField, Min(0)] private int _requiredToolId;
        [SerializeField, Min(0f)] private float _cooldownSeconds = 0.25f;
        [SerializeField] private Transform _interactionOrigin;
        [SerializeField] private bool _emitsRuntimeInteractionNoise;
        [SerializeField] private Transform _runtimeInteractionNoiseOrigin;

        [Networked] private TickTimer Cooldown { get; set; }

        public float InteractionDistance => _interactionDistance;
        public Transform InteractionOrigin => _interactionOrigin != null ? _interactionOrigin : transform;
        public virtual bool EmitsRuntimeInteractionNoise => _emitsRuntimeInteractionNoise;
        public Vector3 RuntimeInteractionNoiseOrigin =>
            _runtimeInteractionNoiseOrigin != null
                ? _runtimeInteractionNoiseOrigin.position
                : InteractionOrigin.position;

        public InteractionValidationResult ValidateInteraction(in InteractionContext context)
        {
            if (!Object.HasStateAuthority) return InteractionValidationResult.InvalidTarget;

            var sqrDistance = (context.Requester.transform.position - InteractionOrigin.position).sqrMagnitude;
            if (sqrDistance > _interactionDistance * _interactionDistance)
            {
                return InteractionValidationResult.OutOfRange;
            }

            if (_requiredToolId > 0 && context.PlayerState.ToolId != _requiredToolId)
            {
                return InteractionValidationResult.MissingRequiredTool;
            }

            if (Cooldown.IsRunning && !Cooldown.Expired(Runner))
            {
                return InteractionValidationResult.OnCooldown;
            }

            return ValidateCurrentState(context);
        }

        public void ExecuteAuthoritative(in InteractionContext context)
        {
            if (!Object.HasStateAuthority)
            {
                Debug.LogError($"[Interaction] Non-authority attempted to execute target {Object.Id}.");
                return;
            }

            ExecuteInteraction(context);
            Cooldown = _cooldownSeconds > 0f
                ? TickTimer.CreateFromSeconds(Runner, _cooldownSeconds)
                : TickTimer.None;
        }

        protected virtual InteractionValidationResult ValidateCurrentState(in InteractionContext context)
        {
            return InteractionValidationResult.Accepted;
        }

        protected abstract void ExecuteInteraction(in InteractionContext context);
    }
}
