using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EchoProtocol.Networking
{
    public struct NetworkPlayerInput : INetworkInput
    {
        public Vector2 Move;
        public NetworkBool JumpPressed;
    }

    [RequireComponent(typeof(NetworkCharacterController))]
    public sealed class NetworkPlayerMovement : NetworkBehaviour
    {
        [SerializeField] private InputActionAsset _inputActions;

        private NetworkCharacterController _controller;
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private NetworkBootstrap _bootstrap;

        private void Awake()
        {
            _controller = GetComponent<NetworkCharacterController>();
            var playerMap = _inputActions?.FindActionMap("Player", false);
            _moveAction = playerMap?.FindAction("Move", false);
            _jumpAction = playerMap?.FindAction("Jump", false);
        }

        public override void Spawned()
        {
            if (!Object.HasInputAuthority) return;

            _bootstrap = NetworkBootstrap.Instance;
            _moveAction?.Enable();
            _jumpAction?.Enable();
            _bootstrap?.RegisterLocalInputProvider(Object, ReadLocalInput);
            Debug.Log($"[NetworkMovement] Local input provider registered for {Object.InputAuthority}.");
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _bootstrap?.UnregisterLocalInputProvider(Object);
            _moveAction?.Disable();
            _jumpAction?.Disable();
        }

        public override void FixedUpdateNetwork()
        {
            if (!GetComponent<LobbyPlayerState>().IsGameplayPlayer) return;
            if (!GetInput(out NetworkPlayerInput input)) return;

            var direction = new Vector3(input.Move.x, 0f, input.Move.y);
            if (direction.sqrMagnitude > 1f) direction.Normalize();

            _controller.Move(direction);
            if (input.JumpPressed && _controller.Grounded)
            {
                _controller.Jump();
            }
        }

        private NetworkPlayerInput ReadLocalInput()
        {
            if (!Object.HasInputAuthority) return default;
            return new NetworkPlayerInput
            {
                Move = _moveAction?.ReadValue<Vector2>() ?? Vector2.zero,
                JumpPressed = _jumpAction?.WasPressedThisFrame() ?? false,
            };
        }
    }
}
