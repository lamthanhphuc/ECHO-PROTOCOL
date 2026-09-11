using EchoProtocol.AI.Listener.Noise;
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
    }

    [RequireComponent(typeof(NetworkCharacterController))]
    public sealed class NetworkPlayerMovement : NetworkBehaviour
    {
        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField, Min(0f)] private float _walkSpeed = 4f;
        [SerializeField, Min(0f)] private float _sprintSpeed = 7f;
        [SerializeField] private bool _allowJump = false;

        private NetworkCharacterController _controller;
        private CharacterController _unityCharacterController;
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
        private PlayerCamera _playerCamera;
        private PlayerUpperBodyAim _upperBodyAim;
        private NetworkBootstrap _bootstrap;
        private bool _isSceneLoadDoneSubscribed;
        private TickTimer _nextMovementNoise;
        private Vector3 _offlineVelocity;
        private Vector2 _offlineAnimationMoveInput;
        private bool _offlineAnimationSprinting;

        [Networked] private float LookPitch { get; set; }
        [Networked] private float AnimationMoveX { get; set; }
        [Networked] private float AnimationMoveY { get; set; }
        [Networked] private NetworkBool AnimationSprintHeld { get; set; }

        public float CurrentPitch => LookPitch;

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

        public bool CanSprintInDirection(Vector2 moveInput)
        {
            // Backward movement (pressing S, moveInput.y < -0.01f) does not allow sprint and stays at normal walk speed
            return moveInput.y >= -0.01f;
        }

        private void Awake()
        {
            _controller = GetComponent<NetworkCharacterController>();
            _unityCharacterController = GetComponent<CharacterController>();

            if (_inputActions != null)
            {
                var playerMap = _inputActions.FindActionMap("Player", false);
                _moveAction = playerMap?.FindAction("Move", false);
                _jumpAction = playerMap?.FindAction("Jump", false);
                _sprintAction = playerMap?.FindAction("Sprint", false);
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
        }

        private void Start()
        {
            if (Runner == null || Object == null || !Object.IsValid)
            {
                _moveAction?.Enable();
                _jumpAction?.Enable();
                _sprintAction?.Enable();
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

            Vector3 localDirection = new Vector3(moveInput.x, 0f, moveInput.y);
            if (localDirection.sqrMagnitude > 1f)
            {
                localDirection.Normalize();
            }

            bool isSprintMoving = sprintHeld && CanSprintInDirection(moveInput) && localDirection.sqrMagnitude > 0.01f;
            float speed = isSprintMoving ? _sprintSpeed : _walkSpeed;

            _offlineAnimationMoveInput = new Vector2(localDirection.x, localDirection.z);
            _offlineAnimationSprinting = isSprintMoving;

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
        }

        public override void Spawned()
        {
            if (!Object.HasInputAuthority) return;

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
            _bootstrap?.RegisterLocalInputProvider(Object, ReadLocalInput);
            Debug.Log($"[NetworkMovement] Local input provider registered for {Object.InputAuthority}.");
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
            if (lifeState != null && !lifeState.CanMove) return;
            if (!GetInput(out NetworkPlayerInput input)) return;

            var localDirection = new Vector3(input.Move.x, 0f, input.Move.y);
            if (localDirection.sqrMagnitude > 1f)
            {
                localDirection.Normalize();
            }
            AnimationMoveX = localDirection.x;
            AnimationMoveY = localDirection.z;

            var lookRotation = Quaternion.Euler(0f, input.LookYaw, 0f);
            var direction = lookRotation * localDirection;

            var isSprintMoving =
                (lifeState == null || lifeState.CanInitiateAction) &&
                input.SprintHeld &&
                CanSprintInDirection(input.Move) &&
                direction.sqrMagnitude > 0.01f;
            AnimationSprintHeld = isSprintMoving;

            var baseSpeed = isSprintMoving
                ? _sprintSpeed
                : _walkSpeed;
            var lobbyPlayer = GetComponent<LobbyPlayerState>();
            var coreCarryMultiplier = 1f;
            if (lobbyPlayer != null && lobbyPlayer.Object != null && lobbyPlayer.Object.IsValid && lobbyPlayer.CarriedCoreId.IsValid)
            {
                coreCarryMultiplier = lobbyPlayer.IsCoreStabilized ? 0.9f : 0.72f;
            }
            _controller.maxSpeed = baseSpeed * (lifeState?.MovementSpeedMultiplier ?? 1f) * coreCarryMultiplier;

            _controller.Move(direction);

            transform.rotation = lookRotation;
            LookPitch = Mathf.Clamp(input.LookPitch, -85f, 85f);
            if (Object.HasStateAuthority && isSprintMoving
                && _nextMovementNoise.ExpiredOrNotRunning(Runner))
            {
                var state = lobbyPlayer;
                var isCarryingCore = state != null && state.Object != null && state.Object.IsValid && state.CarriedCoreId.IsValid;
                var type = isCarryingCore
                    ? RuntimeNoiseType.CORE_CARRY
                    : RuntimeNoiseType.SPRINT;
                HostRuntimeNoiseService.EnsureExists(MatchAuthorityRuntime.Instance)
                    .TryAccept(
                        Object.InputAuthority,
                        type,
                        RuntimeNoiseSourceOccurrenceKey.ForMovement(
                            Object.Id.ToString(),
                            type,
                            Runner.Tick.Raw),
                        transform.position,
                        out _);
                float noiseInterval = (isCarryingCore && state.IsCoreStabilized) ? 4.0f : 1.5f;
                _nextMovementNoise = TickTimer.CreateFromSeconds(Runner, noiseInterval);
            }
            if (_allowJump
                && (lifeState == null || lifeState.CanInitiateAction)
                && input.JumpPressed
                && _controller.Grounded)
            {
                _controller.Jump();
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
        }

        private void BindLocalPlayerCameraIfNeeded()
        {
            if (Object != null && Object.IsValid && !Object.HasInputAuthority) return;

            var mainCamera = Camera.main;
            if (mainCamera == null) return;

            var playerCamera = mainCamera.GetComponent<PlayerCamera>();
            if (playerCamera == null) return;
            _playerCamera = playerCamera;

            playerCamera.SetTarget(transform);
            Debug.Log($"[NetworkMovement] Bound local PlayerCamera to player.");
        }

        private NetworkPlayerInput ReadLocalInput()
        {
            if (!Object.HasInputAuthority) return default;
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
            };
        }
    }
}
