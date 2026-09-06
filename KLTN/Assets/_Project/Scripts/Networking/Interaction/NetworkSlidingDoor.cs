using System;
using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking
{
    /// <summary>
    /// Host-authoritative two-panel sliding door. Fusion replicates semantic state only;
    /// every peer derives the smooth visual presentation locally.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkSlidingDoor : NetworkInteractable, INetworkDoorStateProvider
    {
        public static event Action<NetworkSlidingDoor, NetworkDoorState> StateChanged;

        [Header("Door panels")]
        [SerializeField] private Transform _leftDoor;
        [SerializeField] private Transform _rightDoor;
        [SerializeField] private Vector3 _leftOpenOffset = new Vector3(0f, 0f, 1.2f);
        [SerializeField] private Vector3 _rightOpenOffset = new Vector3(0f, 0f, -1.2f);
        [SerializeField, Min(0.01f)] private float _animationDuration = 0.85f;

        [Header("Collision")]
        [SerializeField] private Collider _blockingCollider;

        [Header("Initial state")]
        [SerializeField] private bool _startsLocked;

        private Vector3 _leftClosedPosition;
        private Vector3 _rightClosedPosition;
        private float _visualOpenAmount;
        private float _targetOpenAmount;
        private bool _positionsCached;

        [Networked, OnChangedRender(nameof(ApplyReplicatedState))]
        public NetworkDoorState State { get; private set; }

        public override string InteractionPrompt => State switch
        {
            NetworkDoorState.Locked => "Door locked",
            NetworkDoorState.Open => "Close door",
            _ => "Open door",
        };

        private void Awake()
        {
            CacheClosedPositions();
        }

        public override void Spawned()
        {
            CacheClosedPositions();

            if (Object.HasStateAuthority)
            {
                State = _startsLocked ? NetworkDoorState.Locked : NetworkDoorState.Closed;
            }

            ApplyReplicatedStateImmediate();
        }

        public override void Render()
        {
            if (!_positionsCached)
            {
                return;
            }

            var duration = Mathf.Max(0.01f, _animationDuration);
            _visualOpenAmount = Mathf.MoveTowards(
                _visualOpenAmount,
                _targetOpenAmount,
                Time.deltaTime / duration);

            ApplyVisuals(SmoothStep(_visualOpenAmount));
        }

        public bool SetLockedAuthoritative(bool locked)
        {
            if (!Object.HasStateAuthority)
            {
                return false;
            }

            State = locked ? NetworkDoorState.Locked : NetworkDoorState.Closed;
            ApplyReplicatedState();
            return true;
        }

        protected override InteractionValidationResult ValidateCurrentState(in InteractionContext context)
        {
            return State == NetworkDoorState.Locked
                ? InteractionValidationResult.InvalidTargetState
                : InteractionValidationResult.Accepted;
        }

        protected override void ExecuteInteraction(in InteractionContext context)
        {
            State = State == NetworkDoorState.Open
                ? NetworkDoorState.Closed
                : NetworkDoorState.Open;

            ApplyReplicatedState();
            Debug.Log($"[NetworkSlidingDoor] {context.Player} changed door {Object.Id} to {State}.");
        }

        private void CacheClosedPositions()
        {
            if (_positionsCached || _leftDoor == null || _rightDoor == null)
            {
                return;
            }

            _leftClosedPosition = _leftDoor.localPosition;
            _rightClosedPosition = _rightDoor.localPosition;
            _positionsCached = true;
        }

        private void ApplyReplicatedState()
        {
            _targetOpenAmount = State == NetworkDoorState.Open ? 1f : 0f;

            // Closing blocks immediately. Opening becomes traversable as soon as the
            // authoritative state changes; the panels then catch up visually.
            if (_blockingCollider != null)
            {
                _blockingCollider.enabled = State != NetworkDoorState.Open;
            }

            StateChanged?.Invoke(this, State);
        }

        private void ApplyReplicatedStateImmediate()
        {
            ApplyReplicatedState();
            _visualOpenAmount = _targetOpenAmount;
            ApplyVisuals(SmoothStep(_visualOpenAmount));
        }

        private void ApplyVisuals(float openAmount)
        {
            if (!_positionsCached)
            {
                return;
            }

            _leftDoor.localPosition = Vector3.LerpUnclamped(
                _leftClosedPosition,
                _leftClosedPosition + _leftOpenOffset,
                openAmount);
            _rightDoor.localPosition = Vector3.LerpUnclamped(
                _rightClosedPosition,
                _rightClosedPosition + _rightOpenOffset,
                openAmount);
        }

        private static float SmoothStep(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private void OnValidate()
        {
            _animationDuration = Mathf.Max(0.01f, _animationDuration);

            if (_leftDoor != null && _leftDoor == _rightDoor)
            {
                Debug.LogWarning("[NetworkSlidingDoor] Left and right door references must be different.", this);
            }
        }
    }
}
