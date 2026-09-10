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
    public sealed class NetworkSlidingDoor : NetworkInteractable, INetworkDoorStateProvider, INetworkTraversalBlocker, IInteractable
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

        [Header("Door Jammer")]
        [SerializeField] private Transform _jammerMount;
        [SerializeField] private NetworkObject _doorJammerPrefab;

        [Header("Initial state")]
        [SerializeField] private bool _startsLocked;

        private Vector3 _leftClosedPosition;
        private Vector3 _rightClosedPosition;
        private float _visualOpenAmount;
        private float _targetOpenAmount;
        private bool _positionsCached;
        private NetworkDoorState _offlineState = NetworkDoorState.Closed;
        private bool _offlineBroken;

        private bool IsOnline => Object != null && Object.IsValid && Runner != null && Runner.IsRunning;

        public NetworkDoorState CurrentState => IsOnline ? State : _offlineState;

        NetworkDoorState INetworkDoorStateProvider.State => CurrentState;

        public bool IsBroken => IsOnline ? Broken : _offlineBroken;
        public bool HasActiveJammer => TryGetActiveJammer(out _);
        public bool BlocksTraversal => DoorBlocksTraversal || HasActiveJammer;
        public bool DoorBlocksTraversal => !IsBroken && CurrentState != NetworkDoorState.Open;
        public bool CanMonsterOpen => !IsBroken && CurrentState != NetworkDoorState.Locked;
        public NetworkObject DoorJammerPrefab => _doorJammerPrefab;

        [Networked, OnChangedRender(nameof(ApplyReplicatedState))]
        public NetworkDoorState State { get; private set; }

        [Networked, OnChangedRender(nameof(ApplyReplicatedState))]
        private NetworkBool Broken { get; set; }

        [Networked] public NetworkId ActiveJammerId { get; private set; }

        public override string InteractionPrompt => CurrentState switch
        {
            _ when IsBroken => "Door broken",
            NetworkDoorState.Locked => "Door locked",
            NetworkDoorState.Open => "Close door",
            _ => "Open door",
        };

        string IInteractable.InteractionPrompt => InteractionPrompt;

        public bool CanInteract(GameObject interactor)
        {
            if (IsBroken) return false;
            if (CurrentState == NetworkDoorState.Locked) return false;
            if (interactor == null) return true;

            var origin = InteractionOrigin != null ? InteractionOrigin.position : transform.position;
            var sqrDistance = (interactor.transform.position - origin).sqrMagnitude;
            var maxDist = Mathf.Max(InteractionDistance, 3.5f);
            return sqrDistance <= maxDist * maxDist;
        }

        public void Interact(GameObject interactor)
        {
            if (IsOnline)
            {
                var networkInteractor = interactor != null ? interactor.GetComponent<NetworkPlayerInteractor>() : null;
                if (networkInteractor != null && networkInteractor.Object != null && networkInteractor.Object.HasInputAuthority)
                {
                    if (networkInteractor.CurrentCandidate != this)
                    {
                        networkInteractor.RequestInteraction(this);
                    }
                    return;
                }

                if (Object.HasStateAuthority)
                {
                    var context = new InteractionContext(networkInteractor, this, Runner.LocalPlayer);
                    if (ValidateInteraction(context) == InteractionValidationResult.Accepted)
                    {
                        ExecuteAuthoritative(context);
                    }
                }
                return;
            }

            ToggleOffline();
        }

        private void ToggleOffline()
        {
            if (_offlineState == NetworkDoorState.Locked) return;

            _offlineState = _offlineState == NetworkDoorState.Open
                ? NetworkDoorState.Closed
                : NetworkDoorState.Open;

            ApplyOfflineState();
        }

        private void ApplyOfflineState()
        {
            _targetOpenAmount = _offlineState == NetworkDoorState.Open ? 1f : 0f;

            if (_blockingCollider != null)
            {
                _blockingCollider.enabled = DoorBlocksTraversal;
            }

            StateChanged?.Invoke(this, _offlineState);
        }

        private void Awake()
        {
            CacheClosedPositions();
            _offlineState = _startsLocked ? NetworkDoorState.Locked : NetworkDoorState.Closed;
            _offlineBroken = false;
            _targetOpenAmount = _offlineState == NetworkDoorState.Open ? 1f : 0f;
            _visualOpenAmount = _targetOpenAmount;

            if (_blockingCollider != null)
            {
                _blockingCollider.enabled = DoorBlocksTraversal;
            }

            ApplyVisuals(SmoothStep(_visualOpenAmount));
        }

        private void Update()
        {
            if (IsOnline) return;

            if (!_positionsCached) return;

            var duration = Mathf.Max(0.01f, _animationDuration);
            _visualOpenAmount = Mathf.MoveTowards(
                _visualOpenAmount,
                _targetOpenAmount,
                Time.deltaTime / duration);

            ApplyVisuals(SmoothStep(_visualOpenAmount));
        }

        public override void Spawned()
        {
            CacheClosedPositions();

            if (Object.HasStateAuthority)
            {
                State = _startsLocked ? NetworkDoorState.Locked : NetworkDoorState.Closed;
                Broken = false;
                ActiveJammerId = default;
            }

            _offlineState = State;
            _offlineBroken = Broken;
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
            if (IsBroken)
            {
                return false;
            }

            if (!IsOnline)
            {
                _offlineState = locked ? NetworkDoorState.Locked : NetworkDoorState.Closed;
                ApplyOfflineState();
                return true;
            }

            if (!Object.HasStateAuthority)
            {
                return false;
            }

            State = locked ? NetworkDoorState.Locked : NetworkDoorState.Closed;
            ApplyReplicatedState();
            return true;
        }

        public bool TryOpenForMonsterAuthoritative()
        {
            if (!CanMonsterOpen)
            {
                return false;
            }

            if (!IsOnline)
            {
                if (_offlineState == NetworkDoorState.Open)
                {
                    return true;
                }

                _offlineState = NetworkDoorState.Open;
                ApplyOfflineState();
                return true;
            }

            if (!Object.HasStateAuthority)
            {
                return false;
            }

            if (State == NetworkDoorState.Open)
            {
                return true;
            }

            State = NetworkDoorState.Open;
            ApplyReplicatedState();
            return true;
        }

        public bool TryBreakAuthoritative()
        {
            if (!IsOnline)
            {
                if (_offlineBroken)
                {
                    return true;
                }

                _offlineBroken = true;
                _offlineState = NetworkDoorState.Open;
                ApplyOfflineState();
                return true;
            }

            if (!Object.HasStateAuthority)
            {
                return false;
            }

            if (IsBroken)
            {
                return true;
            }

            Broken = true;
            State = NetworkDoorState.Open;
            ApplyReplicatedState();
            return true;
        }

        public bool CanAcceptJammer()
        {
            return IsBroken && !HasActiveJammer;
        }

        public bool TryGetJammerPlacement(out Vector3 position, out Quaternion rotation)
        {
            if (_jammerMount != null)
            {
                position = _jammerMount.position;
                rotation = _jammerMount.rotation;
                return true;
            }

            position = transform.position;
            rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            return true;
        }

        public bool TryAttachJammerAuthoritative(NetworkDoorJammer jammer)
        {
            if (!IsOnline || !Object.HasStateAuthority || jammer == null || jammer.Object == null || !jammer.Object.Id.IsValid)
            {
                return false;
            }

            if (HasActiveJammer)
            {
                return ActiveJammerId == jammer.Object.Id;
            }

            if (!CanAcceptJammer() || !jammer.InitializeAuthoritative(Object.Id))
            {
                return false;
            }

            ActiveJammerId = jammer.Object.Id;
            ApplyReplicatedState();
            return true;
        }

        public bool TryClearJammerAuthoritative(NetworkDoorJammer jammer)
        {
            if (!IsOnline || !Object.HasStateAuthority || jammer == null || !ActiveJammerId.IsValid)
            {
                return false;
            }

            if (jammer.Object == null || ActiveJammerId != jammer.Object.Id)
            {
                return false;
            }

            ActiveJammerId = default;
            ApplyReplicatedState();
            return true;
        }

        protected override InteractionValidationResult ValidateCurrentState(in InteractionContext context)
        {
            if (IsBroken)
            {
                return InteractionValidationResult.InvalidTargetState;
            }

            return CurrentState == NetworkDoorState.Locked
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
            _offlineState = State;
            _offlineBroken = Broken;
            _targetOpenAmount = State == NetworkDoorState.Open ? 1f : 0f;

            // Closing blocks immediately. Opening becomes traversable as soon as the
            // authoritative state changes; the panels then catch up visually.
            if (_blockingCollider != null)
            {
                _blockingCollider.enabled = DoorBlocksTraversal;
            }

            StateChanged?.Invoke(this, State);
        }

        private bool TryGetActiveJammer(out NetworkDoorJammer jammer)
        {
            jammer = null;
            if (!IsOnline
                || !ActiveJammerId.IsValid
                || Runner == null
                || !Runner.TryFindObject(ActiveJammerId, out var jammerObject)
                || jammerObject == null
                || !jammerObject.TryGetComponent(out jammer))
            {
                return false;
            }

            return jammer.IsActive;
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
