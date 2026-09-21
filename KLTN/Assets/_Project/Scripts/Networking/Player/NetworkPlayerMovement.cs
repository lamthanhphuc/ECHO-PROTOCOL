using EchoProtocol.AI.Listener.Noise;
using EchoProtocol.Diagnostics;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using EchoProtocol.Networking.Authority;

namespace EchoProtocol.Networking
{
    public struct NetworkPlayerInput : INetworkInput
    {
        public Vector2 Move;
        public float LookYaw;
        public float LookPitch;
        public NetworkBool JumpPressed;
        public NetworkBool SprintHeld;
        public NetworkBool CrouchHeld;
    }

    [RequireComponent(typeof(NetworkCharacterController))]
    public sealed class NetworkPlayerMovement : NetworkBehaviour
    {
        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField, Min(0f)] private float _walkSpeed = 4f;
        [SerializeField, Min(0f)] private float _sprintSpeed = 7f;
        [SerializeField] private bool _allowJump = false;
        [SerializeField, Min(1f)] private float _maxStamina = 100f;
        [SerializeField, Min(0f)] private float _sprintStaminaDrainPerSecond = 25f;
        [SerializeField, Min(0f)] private float _staminaRegenPerSecond = 18f;
        [SerializeField, Min(0f)] private float _minStaminaToSprint = 5f;

        private NetworkCharacterController _controller;
        private CharacterController _unityCharacterController;
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
        private InputAction _crouchAction;
        private PlayerCamera _playerCamera;
        private PlayerUpperBodyAim _upperBodyAim;
        private NetworkBootstrap _bootstrap;
        private bool _isSceneLoadDoneSubscribed;
        private TickTimer _nextMovementNoise;
        private RuntimeNoiseType _lastMovementNoiseType;
        private bool _hasLastMovementNoiseType;
        private bool _lastCoreCarryWasStabilized;
        private Vector3 _offlineVelocity;
        private Vector2 _offlineAnimationMoveInput;
        private bool _offlineAnimationSprinting;
        private bool _offlineAnimationCrouching;
        private float _offlineCurrentStamina;

        [SerializeField, Min(0f)] private float _standingHeight = 2f;
        [SerializeField, Min(0f)] private float _crouchHeight = 1.2f;
        [SerializeField, Min(0f)] private float _downedHeight = 0.75f;
        [SerializeField, Min(0f)] private float _crouchTransitionSpeed = 10f;

        [Networked] private float NetworkCurrentStamina { get; set; }
        [Networked] private float LookPitch { get; set; }
        [Networked] private float AnimationMoveX { get; set; }
        [Networked] private float AnimationMoveY { get; set; }
        [Networked] private NetworkBool AnimationSprintHeld { get; set; }
        [Networked] public NetworkBool IsHidden { get; set; }
        [Networked] public ulong CurrentHideSpotId { get; set; }
        [Networked] public NetworkBool IsCrouching { get; set; }

        public float CurrentPitch => LookPitch;
        public float MaxStamina => _maxStamina;
        public float CurrentStamina
        {
            get
            {
                if (Runner == null || Object == null || !Object.IsValid)
                {
                    return _offlineCurrentStamina;
                }

                return NetworkCurrentStamina;
            }
        }

        public Vector2 AnimationMoveInput
        {
            get
            {
                if (Runner == null || Object == null || !Object.IsValid)
                {
                    return _offlineAnimationMoveInput;
                }

                return new Vector2(AnimationMoveX, AnimationMoveY);
            }
        }

        public bool IsAnimationSprinting
        {
            get
            {
                if (Runner == null || Object == null || !Object.IsValid)
                {
                    return _offlineAnimationSprinting;
                }

                return AnimationSprintHeld;
            }
        }

        public bool IsAnimationCrouching
        {
            get
            {
                if (Runner == null || Object == null || !Object.IsValid)
                {
                    return _offlineAnimationCrouching;
                }

                return IsCrouching;
            }
        }

        public bool CanSprintInDirection(Vector2 moveInput)
        {
            // Backward movement (pressing S, moveInput.y < -0.01f) does not allow sprint and stays at normal walk speed
            return moveInput.y >= -0.01f;
        }

        private void Awake()
        {
            _controller = GetComponent<NetworkCharacterController>();
            _unityCharacterController = GetComponent<CharacterController>();
            _offlineCurrentStamina = _maxStamina;
            ApplyCharacterControllerDimensions(_standingHeight, immediate: true);

            if (_inputActions != null)
            {
                var playerMap = _inputActions.FindActionMap("Player", false);
                _moveAction = playerMap?.FindAction("Move", false);
                _jumpAction = playerMap?.FindAction("Jump", false);
                _sprintAction = playerMap?.FindAction("Sprint", false);
                _crouchAction = playerMap?.FindAction("Crouch", false);
            }

            if (_moveAction == null)
            {
                _moveAction = new InputAction("Move", InputActionType.Value);
                _moveAction.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/w")
                    .With("Down", "<Keyboard>/s")
                    .With("Left", "<Keyboard>/a")
                    .With("Right", "<Keyboard>/d");
            }
            if (_jumpAction == null)
            {
                _jumpAction = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            }
            if (_sprintAction == null)
            {
                _sprintAction = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            }
            if (_crouchAction == null)
            {
                _crouchAction = new InputAction("Crouch", InputActionType.Button, "<Keyboard>/c");
            }
        }

        private void Start()
        {
            if (Runner == null || Object == null || !Object.IsValid)
            {
                _moveAction?.Enable();
                _jumpAction?.Enable();
                _sprintAction?.Enable();
                _crouchAction?.Enable();
                BindLocalPlayerCameraIfNeeded();
            }
        }

        private void Update()
        {
            if (Runner == null || Object == null || !Object.IsValid)
            {
                HandleOfflineMovement();
            }
        }

        private void HandleOfflineMovement()
        {
            if (_unityCharacterController == null)
            {
                _unityCharacterController = GetComponent<CharacterController>();
                if (_unityCharacterController == null) return;
            }

            if (_playerCamera == null)
            {
                BindLocalPlayerCameraIfNeeded();
            }

            Vector2 moveInput = _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
            bool sprintHeld = _sprintAction?.IsPressed() ?? false;
            bool jumpPressed = _jumpAction?.WasPressedThisFrame() ?? false;
            bool isCarryingCoreOffline = IsCarryingCore();
            _offlineAnimationCrouching = !isCarryingCoreOffline && IsCrouchPressed();

            Vector3 localDirection = new Vector3(moveInput.x, 0f, moveInput.y);
            if (localDirection.sqrMagnitude > 1f)
            {
                localDirection.Normalize();
            }

            bool isSprintMoving = !isCarryingCoreOffline
                && !_offlineAnimationCrouching
                && sprintHeld
                && _offlineCurrentStamina > _minStaminaToSprint
                && CanSprintInDirection(moveInput)
                && localDirection.sqrMagnitude > 0.01f;
            float speed = _offlineAnimationCrouching ? _walkSpeed * 0.55f : isSprintMoving ? _sprintSpeed : _walkSpeed;

            _offlineAnimationMoveInput = new Vector2(localDirection.x, localDirection.z);
            _offlineAnimationSprinting = isSprintMoving;

            var offlineLifeState = GetComponent<NetworkPlayerLifeState>();
            bool isOfflineDowned = offlineLifeState != null && offlineLifeState.IsDowned;
            ApplyCharacterControllerDimensions(
                isOfflineDowned ? _downedHeight : _offlineAnimationCrouching ? _crouchHeight : _standingHeight,
                immediate: false);

            float yaw = _playerCamera != null ? _playerCamera.Yaw : transform.eulerAngles.y;
            Quaternion lookRotation = Quaternion.Euler(0f, yaw, 0f);
            transform.rotation = lookRotation;

            Vector3 worldDirection = lookRotation * localDirection * speed;

            if (_unityCharacterController.isGrounded)
            {
                _offlineVelocity.y = -2f;
                if (_allowJump && jumpPressed)
                {
                    _offlineVelocity.y = 5f;
                }
            }
            else
            {
                _offlineVelocity.y += -9.81f * Time.deltaTime;
            }

            Vector3 finalMotion = (worldDirection + Vector3.up * _offlineVelocity.y) * Time.deltaTime;
            _unityCharacterController.Move(finalMotion);

            UpdateStaminaOffline(isSprintMoving);
        }

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                IsHidden = false;
                CurrentHideSpotId = 0UL;
                NetworkCurrentStamina = _maxStamina;
            }

            if (!Object.HasInputAuthority) return;

            var hiding = GetComponent<PlayerHidingController>();
            if (hiding != null && hiding.IsHidden)
            {
                hiding.ExitHiding();
            }

            _bootstrap = NetworkBootstrap.Instance;
            if (_bootstrap != null && !_isSceneLoadDoneSubscribed)
            {
                _bootstrap.NetworkSceneLoadDone += HandleNetworkSceneLoadDone;
                _isSceneLoadDoneSubscribed = true;
            }

            BindLocalPlayerCameraIfNeeded();

            _moveAction?.Enable();
            _jumpAction?.Enable();
            _sprintAction?.Enable();
            _crouchAction?.Enable();
            _bootstrap?.RegisterLocalInputProvider(Object, ReadLocalInput);
            RuntimeLog.Log(
                RuntimeLogCategory.PlayerMovement,
                $"[NetworkMovement] Local input provider registered for {Object.InputAuthority}.");
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (_bootstrap != null && _isSceneLoadDoneSubscribed)
            {
                _bootstrap.NetworkSceneLoadDone -= HandleNetworkSceneLoadDone;
                _isSceneLoadDoneSubscribed = false;
            }

            _bootstrap?.UnregisterLocalInputProvider(Object);
            _moveAction?.Disable();
            _jumpAction?.Disable();
            _sprintAction?.Disable();
            _crouchAction?.Disable();
        }

        public override void FixedUpdateNetwork()
        {
            var lobbyState = GetComponent<LobbyPlayerState>();
            if (lobbyState != null && lobbyState.Object != null && lobbyState.Object.IsValid)
            {
                if (!lobbyState.IsGameplayPlayer && SceneManager.GetActiveScene().name != LobbyManager.GameSceneName)
                {
                    return;
                }
            }

            var lifeState = GetComponent<NetworkPlayerLifeState>();
            if (lifeState != null && !lifeState.CanMove)
            {
                AnimationMoveX = AnimationMoveY = 0f;
                AnimationSprintHeld = false;
                UpdateStaminaAuthoritative(false);
                return;
            }

            var hidingController = GetComponent<PlayerHidingController>();
            if (IsHidden || (hidingController != null && hidingController.IsHidden))
            {
                AnimationMoveX = 0f;
                AnimationMoveY = 0f;
                AnimationSprintHeld = false;
                UpdateStaminaAuthoritative(false);
                return;
            }

            if (!GetInput(out NetworkPlayerInput input))
            {
                UpdateStaminaAuthoritative(false);
                return;
            }

            var localDirection = new Vector3(input.Move.x, 0f, input.Move.y);
            if (localDirection.sqrMagnitude > 1f)
            {
                localDirection.Normalize();
            }
            AnimationMoveX = localDirection.x;
            AnimationMoveY = localDirection.z;

            var lookRotation = Quaternion.Euler(0f, input.LookYaw, 0f);
            var direction = lookRotation * localDirection;

            bool isCarryingCore = lobbyState != null && lobbyState.Object != null && lobbyState.Object.IsValid && lobbyState.CarriedCoreId.IsValid;
            bool canInitiateAction = lifeState == null || lifeState.CanInitiateAction;
            bool wantsCrouch = input.CrouchHeld && canInitiateAction && !isCarryingCore;
            if (Object.HasStateAuthority)
            {
                IsCrouching = wantsCrouch;
            }

            bool effectiveCrouch = Object.HasStateAuthority ? wantsCrouch : IsCrouching;

            var isSprintMoving =
                canInitiateAction &&
                !effectiveCrouch &&
                !isCarryingCore &&
                input.SprintHeld &&
                NetworkCurrentStamina > _minStaminaToSprint &&
                CanSprintInDirection(input.Move) &&
                direction.sqrMagnitude > 0.01f;
            AnimationSprintHeld = isSprintMoving;
            UpdateStaminaAuthoritative(isSprintMoving);

            var baseSpeed = effectiveCrouch
                ? _walkSpeed * 0.55f
                : isSprintMoving
                ? _sprintSpeed
                : _walkSpeed;
            var lobbyPlayer = lobbyState;
            var coreCarryMultiplier = 1f;
            if (isCarryingCore)
            {
                coreCarryMultiplier = lobbyPlayer.IsCoreStabilized ? 0.9f : 0.72f;
            }
            _controller.maxSpeed = baseSpeed * (lifeState?.MovementSpeedMultiplier ?? 1f) * coreCarryMultiplier;

            _controller.Move(direction);

            transform.rotation = lookRotation;
            LookPitch = Mathf.Clamp(input.LookPitch, -85f, 85f);

            var isMoving =
                canInitiateAction
                && direction.sqrMagnitude > 0.01f;

            if (Object.HasStateAuthority && !isMoving)
            {
                _hasLastMovementNoiseType = false;
            }

            if (Object.HasStateAuthority && isMoving)
            {
                var type = isCarryingCore
                    ? RuntimeNoiseType.CORE_CARRY
                    : effectiveCrouch
                        ? RuntimeNoiseType.CROUCH
                        : isSprintMoving
                            ? RuntimeNoiseType.SPRINT
                            : RuntimeNoiseType.WALK;

                var coreCarryIsStabilized =
                    type == RuntimeNoiseType.CORE_CARRY
                    && lobbyPlayer != null
                    && lobbyPlayer.IsCoreStabilized;

                var movementNoiseProfileChanged =
                    !_hasLastMovementNoiseType
                    || type != _lastMovementNoiseType
                    || (type == RuntimeNoiseType.CORE_CARRY
                        && coreCarryIsStabilized != _lastCoreCarryWasStabilized);

                if (movementNoiseProfileChanged
                    || _nextMovementNoise.ExpiredOrNotRunning(Runner))
                {
                    HostRuntimeNoiseService
                        .EnsureExists(MatchAuthorityRuntime.Instance)
                        .TryAccept(
                            Object.InputAuthority,
                            type,
                            RuntimeNoiseSourceOccurrenceKey.ForMovement(
                                Object.Id.ToString(),
                                type,
                                Runner.Tick.Raw),
                            transform.position,
                            out _);

                    var noiseInterval =
                        GetMovementNoiseInterval(
                            type,
                            coreCarryIsStabilized);

                    _nextMovementNoise =
                        TickTimer.CreateFromSeconds(
                            Runner,
                            noiseInterval);

                    _lastMovementNoiseType = type;
                    _lastCoreCarryWasStabilized = coreCarryIsStabilized;
                    _hasLastMovementNoiseType = true;
                }
            }
            if (_allowJump
                && (lifeState == null || lifeState.CanInitiateAction)
                && input.JumpPressed
                && _controller.Grounded)
            {
                _controller.Jump();
            }

            bool isDowned = lifeState != null && lifeState.IsDowned;
            ApplyCharacterControllerDimensions(
                isDowned ? _downedHeight : effectiveCrouch ? _crouchHeight : _standingHeight,
                immediate: false);
        }

        private void UpdateStaminaAuthoritative(bool isSprinting)
        {
            if (!Object.HasStateAuthority) return;

            float delta = Runner != null ? Runner.DeltaTime : Time.deltaTime;
            float stamina = NetworkCurrentStamina <= 0f && !isSprinting
                ? NetworkCurrentStamina
                : Mathf.Clamp(NetworkCurrentStamina, 0f, _maxStamina);
            stamina += (isSprinting ? -_sprintStaminaDrainPerSecond : _staminaRegenPerSecond) * delta;
            NetworkCurrentStamina = Mathf.Clamp(stamina, 0f, _maxStamina);
            if (NetworkCurrentStamina <= 0f)
            {
                AnimationSprintHeld = false;
            }
        }

        private void UpdateStaminaOffline(bool isSprinting)
        {
            float delta = Time.deltaTime;
            _offlineCurrentStamina += (isSprinting ? -_sprintStaminaDrainPerSecond : _staminaRegenPerSecond) * delta;
            _offlineCurrentStamina = Mathf.Clamp(_offlineCurrentStamina, 0f, _maxStamina);
            if (_offlineCurrentStamina <= 0f)
            {
                _offlineAnimationSprinting = false;
            }
        }

        private static float GetMovementNoiseInterval(
            RuntimeNoiseType type,
            bool coreCarryIsStabilized)
        {
            switch (type)
            {
                case RuntimeNoiseType.SPRINT:
                    return 0.25f;

                case RuntimeNoiseType.WALK:
                    return 0.45f;

                case RuntimeNoiseType.CROUCH:
                    return 0.80f;

                case RuntimeNoiseType.CORE_CARRY:
                    return coreCarryIsStabilized
                        ? 1.00f
                        : 0.40f;

                default:
                    return 0.50f;
            }
        }

        [Rpc(RpcSources.InputAuthority | RpcSources.StateAuthority, RpcTargets.StateAuthority)]
        public void RpcRequestTeleport(Vector3 position, Quaternion rotation)
        {
            if (_controller != null)
            {
                _controller.Teleport(position, rotation);
            }
            else
            {
                transform.SetPositionAndRotation(position, rotation);
            }
            Physics.SyncTransforms();
        }

        [Rpc(RpcSources.InputAuthority | RpcSources.StateAuthority, RpcTargets.StateAuthority)]
        public void RpcRequestSetHiding(
            NetworkBool isHidden,
            ulong hideSpotId,
            Vector3 position,
            Quaternion rotation)
        {
            IsHidden = isHidden;
            CurrentHideSpotId = isHidden
                ? hideSpotId
                : 0UL;

            if (_controller != null)
            {
                _controller.Teleport(position, rotation);
            }
            else
            {
                transform.SetPositionAndRotation(position, rotation);
            }
            Physics.SyncTransforms();
        }

        public bool TryForceExitHidingAuthoritative(
            Vector3 position,
            Quaternion rotation)
        {
            if (Runner == null
                || Object == null
                || !Object.IsValid
                || !Object.HasStateAuthority)
            {
                return false;
            }

            IsHidden = false;
            CurrentHideSpotId = 0UL;

            if (_controller != null)
            {
                _controller.Teleport(position, rotation);
            }
            else
            {
                transform.SetPositionAndRotation(position, rotation);
            }

            Physics.SyncTransforms();

            if (Object.InputAuthority.IsValid)
            {
                RpcForceExitHidingLocal(Object.InputAuthority);
            }

            return true;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        private void RpcForceExitHidingLocal(
            [RpcTarget] PlayerRef targetPlayer)
        {
            var hiding = GetComponent<PlayerHidingController>();
            if (hiding != null && hiding.IsHidden)
            {
                hiding.ExitHiding();
            }
        }

        public void TeleportAuthoritative(Vector3 position, Quaternion rotation)
        {
            if (Runner != null && Object != null && Object.IsValid)
            {
                if (Object.HasStateAuthority)
                {
                    if (_controller != null)
                    {
                        _controller.Teleport(position, rotation);
                    }
                    else
                    {
                        transform.SetPositionAndRotation(position, rotation);
                    }
                    Physics.SyncTransforms();
                }
                else
                {
                    RpcRequestTeleport(position, rotation);

                    if (_unityCharacterController != null)
                    {
                        bool wasEnabled = _unityCharacterController.enabled;
                        _unityCharacterController.enabled = false;
                        transform.SetPositionAndRotation(position, rotation);
                        _unityCharacterController.enabled = wasEnabled;
                    }
                    else
                    {
                        transform.SetPositionAndRotation(position, rotation);
                    }
                    Physics.SyncTransforms();
                }
            }
            else
            {
                if (_unityCharacterController != null)
                {
                    bool wasEnabled = _unityCharacterController.enabled;
                    _unityCharacterController.enabled = false;
                    transform.SetPositionAndRotation(position, rotation);
                    _unityCharacterController.enabled = wasEnabled;
                }
                else
                {
                    transform.SetPositionAndRotation(position, rotation);
                }
                Physics.SyncTransforms();
            }
        }

        public override void Render()
        {
            if (_upperBodyAim == null)
            {
                _upperBodyAim = GetComponentInChildren<PlayerUpperBodyAim>(true);
            }

            if (_upperBodyAim == null)
            {
                return;
            }

            if (Object.HasInputAuthority)
            {
                if (_playerCamera == null)
                {
                    BindLocalPlayerCameraIfNeeded();
                }

                if (_playerCamera != null)
                {
                    _upperBodyAim.ClearExternalAim();
                    return;
                }
            }

            Vector3 aimOrigin = transform.position + Vector3.up * 1.55f;
            Vector3 aimForward = Quaternion.Euler(LookPitch, transform.eulerAngles.y, 0f) * Vector3.forward;
            _upperBodyAim.SetExternalAim(aimOrigin, aimForward);
        }

        private void HandleNetworkSceneLoadDone(NetworkRunner runner)
        {
            BindLocalPlayerCameraIfNeeded();
            if (Object != null && Object.IsValid && Object.HasInputAuthority)
            {
                _moveAction?.Enable();
                _jumpAction?.Enable();
                _sprintAction?.Enable();
                _crouchAction?.Enable();
                _bootstrap?.RegisterLocalInputProvider(Object, ReadLocalInput);
            }
        }

        private void BindLocalPlayerCameraIfNeeded()
        {
            if (Object != null && Object.IsValid && !Object.HasInputAuthority) return;

            var playerCamera = FindLocalPlayerCamera();
            if (playerCamera == null) return;
            _playerCamera = playerCamera;

            playerCamera.SetTarget(transform);
            RuntimeLog.Log(
                RuntimeLogCategory.PlayerMovement,
                $"[NetworkMovement] Bound local PlayerCamera to player.");
        }

        private static PlayerCamera FindLocalPlayerCamera()
        {
            var mainCamera = Camera.main;
            if (mainCamera != null && mainCamera.TryGetComponent(out PlayerCamera playerCamera))
            {
                return playerCamera;
            }

            return UnityEngine.Object.FindAnyObjectByType<PlayerCamera>(FindObjectsInactive.Exclude);
        }

        private void ApplyCharacterControllerDimensions(float targetHeight, bool immediate)
        {
            if (_unityCharacterController == null) return;

            targetHeight = Mathf.Max(0.1f, targetHeight);
            if (immediate)
            {
                _unityCharacterController.height = targetHeight;
            }
            else
            {
                float deltaTime = Runner != null ? Runner.DeltaTime : Time.deltaTime;
                _unityCharacterController.height = Mathf.Lerp(
                    _unityCharacterController.height,
                    targetHeight,
                    _crouchTransitionSpeed * deltaTime);
            }

            _unityCharacterController.center = Vector3.up * ((_unityCharacterController.height - _standingHeight) * 0.5f);
        }

        private NetworkPlayerInput ReadLocalInput()
        {
            if (!Object.HasInputAuthority) return default;
            var life = GetComponent<NetworkPlayerLifeState>();
            if (life != null && !life.CanMove) return default;

            var hiding = GetComponent<PlayerHidingController>();
            bool isHidden = IsHidden || (hiding != null && hiding.IsHidden);
            if (isHidden || EchoProtocol.Voice.VoiceSettingsPanel.IsOpen)
            {
                return new NetworkPlayerInput
                {
                    Move = Vector2.zero,
                    LookYaw = _playerCamera != null ? _playerCamera.Yaw : transform.eulerAngles.y,
                    LookPitch = _playerCamera != null ? _playerCamera.Pitch : 0f,
                    JumpPressed = false,
                    SprintHeld = false,
                    CrouchHeld = false,
                };
            }

            return new NetworkPlayerInput
            {
                Move = _moveAction?.ReadValue<Vector2>() ?? Vector2.zero,
                LookYaw = _playerCamera != null
                    ? _playerCamera.Yaw
                    : transform.eulerAngles.y,
                LookPitch = _playerCamera != null
                    ? _playerCamera.Pitch
                    : 0f,
                JumpPressed = _allowJump && (_jumpAction?.WasPressedThisFrame() ?? false),
                SprintHeld = _sprintAction?.IsPressed() ?? false,
                CrouchHeld = !IsCarryingCore() && IsCrouchPressed(),
            };
        }

        private bool IsCarryingCore()
        {
            var lobbyState = GetComponent<LobbyPlayerState>();
            if (lobbyState != null && lobbyState.Object != null && lobbyState.Object.IsValid)
            {
                return lobbyState.CarriedCoreId.IsValid;
            }

            var legacyCarrier = GetComponent<PlayerEnergyCoreCarrier>() ?? GetComponentInParent<PlayerEnergyCoreCarrier>();
            return legacyCarrier != null && legacyCarrier.IsCarrying;
        }

        private bool IsCrouchPressed()
        {
            if (_crouchAction != null && _crouchAction.IsPressed())
            {
                return true;
            }

            var keyboard = Keyboard.current;
            return keyboard != null
                && (keyboard.cKey.isPressed
                    || keyboard.leftCtrlKey.isPressed
                    || keyboard.rightCtrlKey.isPressed);
        }
    }
}
