using System;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

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
        [SerializeField] private NavMeshObstacle _traversalObstacle;

        [Header("Door Jammer")]
        [SerializeField] private Transform _jammerMount;
        [SerializeField] private NetworkObject _doorJammerPrefab;

        [Header("Audio")]
        [SerializeField] private AudioClip _doorBreakClip;
        [SerializeField] private AudioClip _jammerDeployClip;

        [Header("Initial state")]
        [SerializeField] private bool _startsLocked;
        [SerializeField] private bool _startsBroken;

        public bool StartsBroken => _startsBroken;

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

        private NetworkDoorJammer _offlineJammer;

        private bool HasDoorJammerTool(GameObject interactor)
        {
            if (interactor != null)
            {
                var lobbyState = interactor.GetComponentInParent<LobbyPlayerState>();
                if (lobbyState != null && lobbyState.Object != null && lobbyState.Object.IsValid && lobbyState.Object.Id.IsValid && lobbyState.Runner != null && lobbyState.Runner.IsRunning)
                {
                    if (lobbyState.ToolId == 4) return true;
                }

                var inventory = interactor.GetComponentInParent<PlayerInventory>();
                if (inventory != null && inventory.TeamToolSlot != null)
                {
                    string id = (inventory.TeamToolSlot.ItemId ?? string.Empty).ToLowerInvariant();
                    string name = (inventory.TeamToolSlot.DisplayName ?? string.Empty).ToLowerInvariant();
                    if (id.Contains("plank") || id.Contains("jammer") || name.Contains("plank") || name.Contains("jammer"))
                    {
                        return true;
                    }
                }
            }

            foreach (var interactorComp in FindObjectsByType<NetworkPlayerInteractor>(FindObjectsInactive.Exclude))
            {
                if (interactorComp.Object != null && interactorComp.Object.IsValid && interactorComp.Object.Id.IsValid && interactorComp.Runner != null && interactorComp.Runner.IsRunning && interactorComp.Object.HasInputAuthority)
                {
                    var state = interactorComp.GetComponent<LobbyPlayerState>();
                    if (state != null && state.Object != null && state.Object.IsValid && state.Object.Id.IsValid && state.Runner != null && state.Runner.IsRunning && state.ToolId == 4) return true;
                }
            }

            foreach (var localInv in FindObjectsByType<PlayerInventory>(FindObjectsInactive.Exclude))
            {
                if (localInv.TeamToolSlot != null)
                {
                    string id = (localInv.TeamToolSlot.ItemId ?? string.Empty).ToLowerInvariant();
                    string name = (localInv.TeamToolSlot.DisplayName ?? string.Empty).ToLowerInvariant();
                    if (id.Contains("plank") || id.Contains("jammer") || name.Contains("plank") || name.Contains("jammer"))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public override string InteractionPrompt
        {
            get
            {
                if (IsBroken)
                {
                    if (CanAcceptJammer())
                    {
                        return HasDoorJammerTool(null)
                            ? "Chắn ván cửa [E] / [Chuột trái]"
                            : "Cửa hỏng (Cần Ván gỗ)";
                    }
                    return "Door broken";
                }

                return CurrentState switch
                {
                    NetworkDoorState.Locked => "Door locked",
                    NetworkDoorState.Open => "Close door",
                    _ => "Open door",
                };
            }
        }

        string IInteractable.InteractionPrompt => InteractionPrompt;

        public bool CanInteract(GameObject interactor)
        {
            if (IsBroken)
            {
                if (!CanAcceptJammer() || !HasDoorJammerTool(interactor))
                {
                    return false;
                }
            }
            else if (CurrentState == NetworkDoorState.Locked)
            {
                return false;
            }

            if (interactor == null) return true;

            var origin = InteractionOrigin != null ? InteractionOrigin.position : transform.position;
            var sqrDistance = (interactor.transform.position - origin).sqrMagnitude;
            var maxDist = Mathf.Max(InteractionDistance, 3.5f);
            return sqrDistance <= maxDist * maxDist;
        }

        public void Interact(GameObject interactor)
        {
            if (IsBroken && CanAcceptJammer() && HasDoorJammerTool(interactor))
            {
                if (IsOnline)
                {
                    var networkInteractor = interactor != null ? interactor.GetComponentInParent<NetworkPlayerInteractor>() : null;
                    if (networkInteractor != null && networkInteractor.Object != null && networkInteractor.Object.HasInputAuthority)
                    {
                        networkInteractor.RequestUseTeamTool();
                        return;
                    }
                }

                DeployJammerOffline(interactor);
                return;
            }

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

        public bool DeployJammerOffline(GameObject interactor)
        {
            if (!CanAcceptJammer()) return false;

            TryGetJammerPlacement(out var position, out var rotation);
            var prefab = _doorJammerPrefab != null
                ? _doorJammerPrefab.gameObject
                : Resources.Load<GameObject>("Network/PF_DoorJammer");

            if (prefab == null) return false;

            var jammerGo = Instantiate(prefab, position, rotation);
            var jammer = jammerGo.GetComponent<NetworkDoorJammer>();
            if (jammer != null)
            {
                jammer.InitializeOffline();
                _offlineJammer = jammer;
            }

            if (interactor != null)
            {
                var inv = interactor.GetComponentInParent<PlayerInventory>();
                if (inv != null && inv.TeamToolSlot != null)
                {
                    inv.TryRemove(inv.TeamToolSlot);
                }

                var state = interactor.GetComponentInParent<LobbyPlayerState>();
                if (state != null && state.Object != null && state.Object.IsValid && state.Object.Id.IsValid && state.Runner != null && state.Runner.IsRunning)
                {
                    state.SetGameplayToolId(0);
                }
            }

            PlayJammerDeployAudio();
            return true;
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

            SynchronizeTraversalBlocking();

            ApplyVisuals(SmoothStep(_targetOpenAmount));

            StateChanged?.Invoke(this, _offlineState);
        }

        private void Awake()
        {
            CacheClosedPositions();
            _offlineState = _startsBroken
                ? NetworkDoorState.Open
                : (_startsLocked ? NetworkDoorState.Locked : NetworkDoorState.Closed);
            _offlineBroken = _startsBroken;
            _targetOpenAmount = _offlineState == NetworkDoorState.Open ? 1f : 0f;
            _visualOpenAmount = _targetOpenAmount;

            SynchronizeTraversalBlocking();

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
                State = _startsBroken
                    ? NetworkDoorState.Open
                    : (_startsLocked ? NetworkDoorState.Locked : NetworkDoorState.Closed);
                Broken = _startsBroken;
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
                PlayBreakAudio();
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
            RpcPlayBreakAudio();
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
            RpcPlayJammerDeployAudio();
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
            SynchronizeTraversalBlocking();

            ApplyVisuals(SmoothStep(_targetOpenAmount));

            StateChanged?.Invoke(this, State);
        }

        private void SynchronizeTraversalBlocking()
        {
            var blocksTraversal = DoorBlocksTraversal;

            if (_blockingCollider != null)
            {
                _blockingCollider.enabled = DoorBlocksTraversal;
            }

            if (_traversalObstacle != null)
            {
                _traversalObstacle.enabled = blocksTraversal;
            }
        }

        private bool TryGetActiveJammer(out NetworkDoorJammer jammer)
        {
            if (_offlineJammer != null)
            {
                jammer = _offlineJammer;
                return jammer.IsActive;
            }

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
            if (IsBroken)
            {
                if (_leftDoor != null && _leftDoor.gameObject.activeSelf)
                {
                    _leftDoor.gameObject.SetActive(false);
                }

                if (_rightDoor != null && _rightDoor.gameObject.activeSelf)
                {
                    _rightDoor.gameObject.SetActive(false);
                }

                return;
            }

            CacheClosedPositions();

            if (!_positionsCached)
            {
                return;
            }

            if (_leftDoor != null && !_leftDoor.gameObject.activeSelf)
            {
                _leftDoor.gameObject.SetActive(true);
            }

            if (_rightDoor != null && !_rightDoor.gameObject.activeSelf)
            {
                _rightDoor.gameObject.SetActive(true);
            }

            if (_leftDoor != null)
            {
                _leftDoor.localPosition = Vector3.LerpUnclamped(
                    _leftClosedPosition,
                    _leftClosedPosition + _leftOpenOffset,
                    openAmount);
            }

            if (_rightDoor != null)
            {
                _rightDoor.localPosition = Vector3.LerpUnclamped(
                    _rightClosedPosition,
                    _rightClosedPosition + _rightOpenOffset,
                    openAmount);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcPlayBreakAudio()
        {
            PlayBreakAudio();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcPlayJammerDeployAudio()
        {
            PlayJammerDeployAudio();
        }

        private void PlayBreakAudio()
        {
            if (_doorBreakClip != null)
            {
                AudioSource.PlayClipAtPoint(_doorBreakClip, transform.position);
            }
            else EchoProtocol.Audio.GameAudioRuntime.AtPoint("stalker/door_break", transform.position);
        }

        private void PlayJammerDeployAudio()
        {
            if (_jammerDeployClip != null)
            {
                AudioSource.PlayClipAtPoint(_jammerDeployClip, transform.position);
            }
            else EchoProtocol.Audio.GameAudioRuntime.AtPoint("door/door_jammer_deploy", transform.position);
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
