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
        public NetworkBool JumpPressed;
        public NetworkBool SprintHeld;
    }

    [RequireComponent(typeof(NetworkCharacterController))]
    public sealed class NetworkPlayerMovement : NetworkBehaviour
    {
        [SerializeField] private InputActionAsset _inputActions;

        private NetworkCharacterController _controller;
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
        private NetworkBootstrap _bootstrap;
        private bool _isSceneLoadDoneSubscribed;
        private TickTimer _nextMovementNoise;

        private void Awake()
        {
            _controller = GetComponent<NetworkCharacterController>();
            var playerMap = _inputActions?.FindActionMap("Player", false);
            _moveAction = playerMap?.FindAction("Move", false);
            _jumpAction = playerMap?.FindAction("Jump", false);
            _sprintAction = playerMap?.FindAction("Sprint", false);
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
            if (!GetComponent<LobbyPlayerState>().IsGameplayPlayer) return;
            if (!GetInput(out NetworkPlayerInput input)) return;

            var direction = new Vector3(input.Move.x, 0f, input.Move.y);
            if (direction.sqrMagnitude > 1f) direction.Normalize();

            _controller.Move(direction);
            if (Object.HasStateAuthority && input.SprintHeld && direction.sqrMagnitude > 0.01f
                && _nextMovementNoise.ExpiredOrNotRunning(Runner))
            {
                var state = GetComponent<LobbyPlayerState>();
                var type = state.CarriedCoreId.IsValid ? "CORE_CARRY" : "SPRINT";
                HostRuntimeNoiseService.EnsureExists(MatchAuthorityRuntime.Instance)
                    .TryAccept(Object.InputAuthority, type, 0.7, transform.position, 12);
                _nextMovementNoise = TickTimer.CreateFromSeconds(Runner, 1.5f);
            }
            if (input.JumpPressed && _controller.Grounded)
            {
                _controller.Jump();
            }
        }

        private void HandleNetworkSceneLoadDone(NetworkRunner runner)
        {
            BindLocalPlayerCameraIfNeeded();
        }

        private void BindLocalPlayerCameraIfNeeded()
        {
            if (!Object.HasInputAuthority) return;
            if (SceneManager.GetActiveScene().name != LobbyManager.GameSceneName) return;

            var mainCamera = Camera.main;
            if (mainCamera == null) return;

            var playerCamera = mainCamera.GetComponent<PlayerCamera>();
            if (playerCamera == null) return;

            playerCamera.SetTarget(transform);
            Debug.Log($"[NetworkMovement] Bound local PlayerCamera to [Player:{Object.InputAuthority.PlayerId}].");
        }

        private NetworkPlayerInput ReadLocalInput()
        {
            if (!Object.HasInputAuthority) return default;
            return new NetworkPlayerInput
            {
                Move = _moveAction?.ReadValue<Vector2>() ?? Vector2.zero,
                JumpPressed = _jumpAction?.WasPressedThisFrame() ?? false,
                SprintHeld = _sprintAction?.IsPressed() ?? false,
            };
        }
    }
}
