using Fusion;
using EchoProtocol.AI.Listener.Noise;
using UnityEngine;

namespace EchoProtocol.Networking
{
    /// <summary>Base validation shared by every authoritative network interaction target.</summary>
    public abstract class NetworkInteractable : NetworkBehaviour, IAuthoritativeNetworkInteractable
    {
        [SerializeField, Min(0.1f)] private float _interactionDistance = 3f;
        [SerializeField] private string _interactionPrompt = "Interact";
        [SerializeField, Min(0)] private int _requiredToolId;
        [SerializeField, Min(0f)] private float _cooldownSeconds = 0.25f;
        [SerializeField] private Transform _interactionOrigin;
        [SerializeField] private bool _emitsRuntimeInteractionNoise;
        [SerializeField] private Transform _runtimeInteractionNoiseOrigin;

        [Networked] private TickTimer Cooldown { get; set; }
        [Networked] public NetworkId MonsterToolCarrierId { get; private set; }
        [Networked] private NetworkBool HasMonsterDropPose { get; set; }
        [Networked] private Vector3 MonsterDropPosition { get; set; }
        [Networked] private Quaternion MonsterDropRotation { get; set; }
        private Collider[] _carryColliders;
        private bool[] _carryColliderStates;
        private bool _carryPresentationActive;

        public void BeginMonsterCarryAuthoritative(NetworkId monsterId)
        {
            if (!Object.HasStateAuthority || !monsterId.IsValid) return;
            MonsterToolCarrierId = monsterId;
            HasMonsterDropPose = false;
            ApplyMonsterCarryPresentation();
        }

        public void EndMonsterCarryAuthoritative(Vector3 position)
        {
            if (!Object.HasStateAuthority) return;
            MonsterDropPosition = position;
            MonsterDropRotation = Quaternion.identity;
            HasMonsterDropPose = true;
            MonsterToolCarrierId = default;
            ApplyMonsterCarryPresentation();
        }

        public override void Render() => ApplyMonsterCarryPresentation();
        private void LateUpdate()
        {
            if (Object != null && Object.IsValid) ApplyMonsterCarryPresentation();
        }

        private void ApplyMonsterCarryPresentation()
        {
            bool held = MonsterToolCarrierId.IsValid;
            if (held != _carryPresentationActive)
            {
                if (held)
                {
                    _carryColliders = GetComponentsInChildren<Collider>(true);
                    _carryColliderStates = new bool[_carryColliders.Length];
                    for (int i = 0; i < _carryColliders.Length; i++)
                    {
                        _carryColliderStates[i] = _carryColliders[i].enabled;
                        _carryColliders[i].enabled = false;
                    }
                }
                else if (_carryColliders != null)
                    for (int i = 0; i < _carryColliders.Length; i++)
                        if (_carryColliders[i] != null) _carryColliders[i].enabled = _carryColliderStates[i];
                _carryPresentationActive = held;
            }
            var outline = GetComponent<QuickOutline.Outline>();
            if (outline != null && held) outline.enabled = false;
            if (!held && HasMonsterDropPose)
            {
                if (outline != null) outline.enabled = true;
                transform.SetPositionAndRotation(MonsterDropPosition, MonsterDropRotation);
                return;
            }
            if (!held) return;
            // Resolve again in LateUpdate so the held model follows the animated hand on every peer.
            if (Runner.TryFindObject(MonsterToolCarrierId, out var carrier)
                && carrier.TryGetComponent<EchoProtocol.AI.Minions.CreepMinionRuntime>(out var holder))
            {
                holder.GetToolCarryPose(out var carryPosition, out var carryRotation);
                transform.SetPositionAndRotation(carryPosition, carryRotation);
            }
        }

        public float InteractionDistance => _interactionDistance;
        public virtual string InteractionPrompt => _interactionPrompt;
        public Transform InteractionOrigin => _interactionOrigin != null ? _interactionOrigin : transform;
        public virtual bool EmitsRuntimeInteractionNoise => _emitsRuntimeInteractionNoise;
        public virtual RuntimeNoiseType RuntimeInteractionNoiseType =>
            RuntimeNoiseType.INTERACTION;
        public Vector3 RuntimeInteractionNoiseOrigin =>
            _runtimeInteractionNoiseOrigin != null
                ? _runtimeInteractionNoiseOrigin.position
                : InteractionOrigin.position;

        public InteractionValidationResult ValidateInteraction(in InteractionContext context)
        {
            if (!Object.HasStateAuthority) return InteractionValidationResult.InvalidTarget;
            if (MonsterToolCarrierId.IsValid) return InteractionValidationResult.InvalidTargetState;

            var requesterPosition = context.Requester.transform.position;
            var targetPosition = GetClosestInteractionPoint(requesterPosition);
            var sqrDistance = (requesterPosition - targetPosition).sqrMagnitude;
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

        private Vector3 GetClosestInteractionPoint(Vector3 requesterPosition)
        {
            var colliders = GetComponentsInChildren<Collider>(true);
            var hasCollider = false;
            var closestPoint = InteractionOrigin.position;
            var closestDistance = float.PositiveInfinity;

            for (var i = 0; i < colliders.Length; i++)
            {
                var candidate = colliders[i];
                if (candidate == null || !candidate.enabled)
                {
                    continue;
                }

                var point = candidate.ClosestPoint(requesterPosition);
                var distance = (requesterPosition - point).sqrMagnitude;
                if (!hasCollider || distance < closestDistance)
                {
                    hasCollider = true;
                    closestPoint = point;
                    closestDistance = distance;
                }
            }

            return closestPoint;
        }
    }
}
