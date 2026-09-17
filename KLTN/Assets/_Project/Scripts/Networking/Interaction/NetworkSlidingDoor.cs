using System;
using EchoProtocol.AI.Listener.Noise;
using EchoProtocol.Diagnostics;
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
        private const float JammerVisualReplicationGraceSeconds = 3f;

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
        [SerializeField] private bool _startsOpen = true;
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
        private GameObject _replicatedJammerVisualFallback;
        private NetworkId _visualizedJammerId;
        private NetworkId _pendingJammerVisualId;
        private float _pendingJammerVisualUntil;
        private Vector3 _pendingJammerVisualPosition;
        private Quaternion _pendingJammerVisualRotation = Quaternion.identity;
        private Transform _resolvedLeftDoor;
        private Transform _resolvedRightDoor;
        private Renderer[] _leftDoorRenderers = Array.Empty<Renderer>();
        private Renderer[] _rightDoorRenderers = Array.Empty<Renderer>();

        private bool IsOnline => Object != null && Object.IsValid && Runner != null && Runner.IsRunning;

        public NetworkDoorState CurrentState => IsOnline ? State : _offlineState;

        NetworkDoorState INetworkDoorStateProvider.State => CurrentState;

        public bool IsBroken => IsOnline ? Broken : _offlineBroken;
        public bool HasActiveJammer => TryGetActiveJammer(out _);
        public bool BlocksTraversal => DoorBlocksTraversal || HasActiveJammer;
        public bool DoorBlocksTraversal => !IsBroken && CurrentState != NetworkDoorState.Open;
        public NetworkObject DoorJammerPrefab => _doorJammerPrefab;
        public override RuntimeNoiseType RuntimeInteractionNoiseType =>
            RuntimeNoiseType.DOOR;

        [Networked, OnChangedRender(nameof(ApplyReplicatedState))]
        public NetworkDoorState State { get; private set; }

        [Networked, OnChangedRender(nameof(ApplyReplicatedState))]
        private NetworkBool Broken { get; set; }

        [Networked, OnChangedRender(nameof(ApplyReplicatedState))]
        public NetworkId ActiveJammerId { get; private set; }

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
                    if (networkInteractor == null)
                    {
                        foreach (var ni in FindObjectsByType<NetworkPlayerInteractor>(FindObjectsInactive.Exclude))
                        {
                            if (ni.Object != null && ni.Object.IsValid && ni.Object.HasInputAuthority)
                            {
                                networkInteractor = ni;
                                break;
                            }
                        }
                    }

                    if (networkInteractor != null && networkInteractor.Object != null && networkInteractor.Object.HasInputAuthority)
                    {
                        networkInteractor.RequestUseTeamTool();
                        return;
                    }

                    return;
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
            ResolveDoorVisualReferences();
            CacheClosedPositions();
            _offlineState = _startsBroken
                ? NetworkDoorState.Open
                : _startsOpen
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
            ResolveDoorVisualReferences();
            CacheClosedPositions();

            if (Object.HasStateAuthority)
            {
                State = _startsBroken
                    ? NetworkDoorState.Open
                    : _startsOpen
                        ? NetworkDoorState.Open
                    : (_startsLocked ? NetworkDoorState.Locked : NetworkDoorState.Closed);
                Broken = _startsBroken;
                ActiveJammerId = default;
            }

            _offlineState = State;
            _offlineBroken = Broken;
            ApplyReplicatedStateImmediate();
        }

        public override void FixedUpdateNetwork()
        {
            SynchronizeTraversalBlocking();
        }

        public override void Render()
        {
            SynchronizeTraversalBlocking();
            SynchronizeJammerVisualFallback();

            if (TryGetActiveJammer(out var activeJammer))
            {
                activeJammer.EnsureVisualActive();
            }

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
            TryGetJammerPlacement(out var position, out var rotation);
            RpcShowJammerDeployed(ActiveJammerId, position, rotation);
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

            var jammerId = ActiveJammerId;
            ActiveJammerId = default;
            ApplyReplicatedState();
            RpcHideJammerVisual(jammerId);
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
            RuntimeLog.Log(RuntimeLogCategory.Door, $"[NetworkSlidingDoor] {context.Player} changed door {Object.Id} to {State}.");
        }

        private void CacheClosedPositions()
        {
            ResolveDoorVisualReferences();
            var leftDoor = LeftDoorVisual;
            var rightDoor = RightDoorVisual;

            if (_positionsCached || leftDoor == null || rightDoor == null)
            {
                return;
            }

            _leftClosedPosition = leftDoor.localPosition;
            _rightClosedPosition = rightDoor.localPosition;
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
            SynchronizeJammerVisualFallback();

            StateChanged?.Invoke(this, State);
        }

        private void SynchronizeTraversalBlocking()
        {
            var blocksTraversal = BlocksTraversal;

            if (_blockingCollider != null)
            {
                _blockingCollider.enabled = blocksTraversal;
            }

            if (_traversalObstacle != null)
            {
                _traversalObstacle.enabled = blocksTraversal;
            }
        }

        public bool TryGetActiveJammer(out NetworkDoorJammer jammer)
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
                SetDoorPanelsVisible(false);
                return;
            }

            CacheClosedPositions();

            if (!_positionsCached)
            {
                return;
            }

            SetDoorPanelsVisible(true);

            var leftDoor = LeftDoorVisual;
            var rightDoor = RightDoorVisual;

            if (leftDoor != null)
            {
                leftDoor.localPosition = Vector3.LerpUnclamped(
                    _leftClosedPosition,
                    _leftClosedPosition + _leftOpenOffset,
                    openAmount);
            }

            if (rightDoor != null)
            {
                rightDoor.localPosition = Vector3.LerpUnclamped(
                    _rightClosedPosition,
                    _rightClosedPosition + _rightOpenOffset,
                    openAmount);
            }
        }

        private void ResolveDoorVisualReferences()
        {
            if (_resolvedLeftDoor == null)
            {
                _resolvedLeftDoor = _leftDoor != null ? _leftDoor : FindChildTransformByName("Door_Left");
                _leftDoorRenderers = _resolvedLeftDoor != null
                    ? _resolvedLeftDoor.GetComponentsInChildren<Renderer>(true)
                    : Array.Empty<Renderer>();
            }

            if (_resolvedRightDoor == null)
            {
                _resolvedRightDoor = _rightDoor != null ? _rightDoor : FindChildTransformByName("Door_Right");
                _rightDoorRenderers = _resolvedRightDoor != null
                    ? _resolvedRightDoor.GetComponentsInChildren<Renderer>(true)
                    : Array.Empty<Renderer>();
            }
        }

        private Transform FindChildTransformByName(string childName)
        {
            var children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (string.Equals(children[i].name, childName, StringComparison.Ordinal))
                {
                    return children[i];
                }
            }

            return null;
        }

        private Transform LeftDoorVisual => _leftDoor != null ? _leftDoor : _resolvedLeftDoor;
        private Transform RightDoorVisual => _rightDoor != null ? _rightDoor : _resolvedRightDoor;

        private void SetDoorPanelsVisible(bool visible)
        {
            ResolveDoorVisualReferences();
            SetDoorPanelVisible(LeftDoorVisual, _leftDoorRenderers, visible);
            SetDoorPanelVisible(RightDoorVisual, _rightDoorRenderers, visible);
        }

        private static void SetDoorPanelVisible(Transform panel, Renderer[] renderers, bool visible)
        {
            if (panel != null && panel.gameObject.activeSelf != visible)
            {
                panel.gameObject.SetActive(visible);
            }

            if (renderers == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = visible;
                }
            }
        }

        private void SynchronizeJammerVisualFallback()
        {
            if (!IsOnline || !ActiveJammerId.IsValid)
            {
                if (KeepPendingJammerVisualAlive())
                {
                    return;
                }

                ClearReplicatedJammerVisualFallback();
                return;
            }

            TryGetJammerPlacement(out var position, out var rotation);
            ShowJammerVisual(ActiveJammerId, position, rotation);
        }

        private void ShowJammerVisual(NetworkId jammerId, Vector3 position, Quaternion rotation)
        {
            if (!jammerId.IsValid)
            {
                ClearReplicatedJammerVisualFallback();
                return;
            }

            _pendingJammerVisualId = jammerId;
            _pendingJammerVisualUntil = Time.unscaledTime + JammerVisualReplicationGraceSeconds;
            _pendingJammerVisualPosition = position;
            _pendingJammerVisualRotation = rotation;

            if (Runner != null
                && Runner.TryFindObject(jammerId, out var jammerObject)
                && jammerObject != null
                && jammerObject.TryGetComponent<NetworkDoorJammer>(out var jammer))
            {
                jammer.transform.SetPositionAndRotation(position, rotation);

                if (jammer.IsActive)
                {
                    jammer.EnsureVisualActive();
                }
                else
                {
                    jammer.ForceActivePresentation();
                }

                ClearReplicatedJammerVisualFallback();
                return;
            }

            if (_replicatedJammerVisualFallback != null && _visualizedJammerId == jammerId)
            {
                _replicatedJammerVisualFallback.transform.SetPositionAndRotation(position, rotation);
                if (!_replicatedJammerVisualFallback.activeSelf)
                {
                    _replicatedJammerVisualFallback.SetActive(true);
                }
                return;
            }

            ClearReplicatedJammerVisualFallback();
            if (_doorJammerPrefab == null)
            {
                return;
            }

            _replicatedJammerVisualFallback = new GameObject("DoorJammer_ReplicatedVisual");
            _replicatedJammerVisualFallback.transform.SetPositionAndRotation(position, rotation);
            CopyVisualHierarchy(_doorJammerPrefab.transform, _replicatedJammerVisualFallback.transform);
            _visualizedJammerId = jammerId;
        }

        private bool KeepPendingJammerVisualAlive()
        {
            if (!_pendingJammerVisualId.IsValid || Time.unscaledTime > _pendingJammerVisualUntil)
            {
                _pendingJammerVisualId = default;
                return false;
            }

            if (Runner != null
                && Runner.TryFindObject(_pendingJammerVisualId, out var jammerObject)
                && jammerObject != null
                && jammerObject.TryGetComponent<NetworkDoorJammer>(out var jammer))
            {
                jammer.transform.SetPositionAndRotation(_pendingJammerVisualPosition, _pendingJammerVisualRotation);
                jammer.ForceActivePresentation();
                ClearReplicatedJammerVisualFallback();
                return true;
            }

            if (_replicatedJammerVisualFallback != null && _visualizedJammerId == _pendingJammerVisualId)
            {
                if (!_replicatedJammerVisualFallback.activeSelf)
                {
                    _replicatedJammerVisualFallback.SetActive(true);
                }

                return true;
            }

            return false;
        }

        private void HideJammerVisual(NetworkId jammerId)
        {
            if (!jammerId.IsValid)
            {
                return;
            }

            if (_pendingJammerVisualId == jammerId)
            {
                _pendingJammerVisualId = default;
                _pendingJammerVisualUntil = 0f;
            }

            if (_visualizedJammerId == jammerId)
            {
                ClearReplicatedJammerVisualFallback();
            }

            if (Runner != null
                && Runner.TryFindObject(jammerId, out var jammerObject)
                && jammerObject != null
                && jammerObject.TryGetComponent<NetworkDoorJammer>(out var jammer))
            {
                jammer.ClearForcedPresentation();
            }
        }

        private void ClearReplicatedJammerVisualFallback()
        {
            _visualizedJammerId = default;
            if (_replicatedJammerVisualFallback != null)
            {
                Destroy(_replicatedJammerVisualFallback);
                _replicatedJammerVisualFallback = null;
            }
        }

        private static void CopyVisualHierarchy(Transform source, Transform target)
        {
            if (source.TryGetComponent<MeshFilter>(out var meshFilter) && meshFilter.sharedMesh != null)
            {
                var copy = target.gameObject.AddComponent<MeshFilter>();
                copy.sharedMesh = meshFilter.sharedMesh;
            }

            if (source.TryGetComponent<MeshRenderer>(out var meshRenderer))
            {
                var copy = target.gameObject.AddComponent<MeshRenderer>();
                copy.sharedMaterials = meshRenderer.sharedMaterials;
                copy.shadowCastingMode = meshRenderer.shadowCastingMode;
                copy.receiveShadows = meshRenderer.receiveShadows;
                copy.enabled = meshRenderer.enabled;
            }

            for (int i = 0; i < source.childCount; i++)
            {
                var child = source.GetChild(i);
                if (child.GetComponentsInChildren<Renderer>(true).Length == 0)
                {
                    continue;
                }

                var childObject = new GameObject(child.name);
                childObject.transform.SetParent(target, false);
                childObject.transform.localPosition = child.localPosition;
                childObject.transform.localRotation = child.localRotation;
                childObject.transform.localScale = child.localScale;
                childObject.SetActive(true);
                CopyVisualHierarchy(child, childObject.transform);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcShowJammerDeployed(NetworkId jammerId, Vector3 position, Quaternion rotation)
        {
            ShowJammerVisual(jammerId, position, rotation);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcHideJammerVisual(NetworkId jammerId)
        {
            HideJammerVisual(jammerId);
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
