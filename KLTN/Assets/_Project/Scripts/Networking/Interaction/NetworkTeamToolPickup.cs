using Fusion;
using QuickOutline;
using UnityEngine;
using UnityEngine.Rendering;

namespace EchoProtocol.Networking
{
    [DisallowMultipleComponent]
    public sealed class NetworkTeamToolPickup : NetworkInteractable
    {
        private const float MaxOutlineVisibleDistance = 20f;

        [SerializeField, Range(1, 6)] private int _toolId = 2;
        [SerializeField] private string _toolDisplayName = "Team Tool";

        private Outline _outline;
        private bool _visualsActive;

        private static readonly Color TeamToolOutlineColor =
            new Color(1f, 0.05f, 0.05f, 1f);

        [Networked, OnChangedRender(nameof(OnConsumedChanged))] private NetworkBool IsConsumed { get; set; }
        [Networked, OnChangedRender(nameof(ApplyReplicatedPose))] public Vector3 WorldPosition { get; private set; }
        [Networked, OnChangedRender(nameof(ApplyReplicatedPose))] public Quaternion WorldRotation { get; private set; }
        [Networked] public int RemainingUses { get; private set; }

        public int ToolId => _toolId;

        public override string InteractionPrompt =>
            string.IsNullOrWhiteSpace(_toolDisplayName)
                ? "Pick Up Team Tool"
                : $"Pick Up {_toolDisplayName}";

        public override void Spawned()
        {
            EnsureOutline();
            ConfigureWorldPickupShadows();

            if (Object.HasStateAuthority)
            {
                IsConsumed = false;
                WorldPosition = transform.position;
                WorldRotation = transform.rotation;

                if (RemainingUses <= 0
                    && (_toolId == LobbyPlayerState.NoiseMakerToolId
                        || _toolId == LobbyPlayerState.DoorJammerToolId))
                {
                    RemainingUses =
                        LobbyPlayerState.MultiUseTeamToolUses;
                }
            }

            ApplyReplicatedPose();
            SetVisualsAndCollidersActive(!IsConsumed);
        }

        private void EnsureOutline()
        {
            if (_outline == null)
            {
                _outline = GetComponent<Outline>();

                if (_outline == null)
                {
                    _outline = gameObject.AddComponent<Outline>();
                }
            }

            _outline.OutlineMode = Outline.Mode.OutlineVisible;
            _outline.OutlineColor = TeamToolOutlineColor;
            _outline.OutlineWidth = 4f;
            _outline.UpdateMaterialProperties();
        }

        private void ApplyReplicatedPose()
        {
            if (WorldPosition != Vector3.zero)
            {
                transform.SetPositionAndRotation(WorldPosition, WorldRotation);
            }
        }

        private void OnConsumedChanged()
        {
            SetVisualsAndCollidersActive(!IsConsumed);
        }

        private void ConfigureWorldPickupShadows()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private void SetVisualsAndCollidersActive(bool active)
        {
            _visualsActive = active;

            foreach (var c in GetComponentsInChildren<Collider>(true))
            {
                c.enabled = active;
            }

            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = active;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            foreach (var light in GetComponentsInChildren<Light>(true))
            {
                light.enabled = active;
            }

            EnsureOutline();
            RefreshOutlineVisibility();
        }

        private void Update()
        {
            RefreshOutlineVisibility();
        }

        private void RefreshOutlineVisibility()
        {
            EnsureOutline();

            Camera viewer = Camera.main;
            if (!_visualsActive || viewer == null)
            {
                _outline.enabled = false;
                return;
            }

            float maxDistanceSqr =
                MaxOutlineVisibleDistance
                * MaxOutlineVisibleDistance;

            _outline.enabled =
                (viewer.transform.position - transform.position)
                .sqrMagnitude <= maxDistanceSqr;
        }

        protected override InteractionValidationResult ValidateCurrentState(
            in InteractionContext context)
        {
            if (IsConsumed || _toolId < 1 || _toolId > 6)
            {
                return InteractionValidationResult.InvalidTargetState;
            }

            var playerState = context.PlayerState;
            if (playerState == null || !playerState.IsGameplayPlayer)
            {
                return InteractionValidationResult.InvalidRequester;
            }

            return playerState.ToolId == 0
                ? InteractionValidationResult.Accepted
                : InteractionValidationResult.InvalidTargetState;
        }

        protected override void ExecuteInteraction(in InteractionContext context)
        {
            if (IsConsumed || !Object.HasStateAuthority)
            {
                return;
            }

            var playerState = context.PlayerState;
            if (playerState == null
                || !playerState.IsGameplayPlayer
                || playerState.ToolId != 0
                || _toolId < 1
                || _toolId > 6)
            {
                return;
            }

            IsConsumed = true;
            SetVisualsAndCollidersActive(false);
            playerState.SetGameplayToolId(
                _toolId,
                RemainingUses);
        }

        public void SetRemainingUsesAuthoritative(
            int remainingUses)
        {
            if (Object == null
                || !Object.IsValid
                || !Object.HasStateAuthority)
            {
                return;
            }

            RemainingUses =
                _toolId == LobbyPlayerState.NoiseMakerToolId
                || _toolId == LobbyPlayerState.DoorJammerToolId
                    ? Mathf.Clamp(
                        remainingUses,
                        1,
                        LobbyPlayerState.MultiUseTeamToolUses)
                    : 0;
        }

        public override void FixedUpdateNetwork()
        {
            if (IsConsumed)
            {
                foreach (var c in GetComponentsInChildren<Collider>(true))
                {
                    if (c.enabled) c.enabled = false;
                }
            }

            // DO NOT call Runner.Despawn(Object) on scene-placed network objects.
            // SetVisualsAndCollidersActive(false) already disables visuals and colliders across all clients.
        }
    }
}
