using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private InputActionAsset inputActions;

    private CharacterController _controller;
    private InputAction _moveAction;
    private Vector3 _velocity;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();

        if (inputActions != null)
        {
            _moveAction = inputActions.FindActionMap("Player").FindAction("Move");
        }
    }

    private void OnEnable()
    {
        _moveAction?.Enable();
    }

    private void OnDisable()
    {
        _moveAction?.Disable();
    }

    private void Update()
    {
        Vector2 input = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
        Vector3 move = new Vector3(input.x, 0f, input.y);

        if (move.sqrMagnitude > 1f)
        {
            move.Normalize();
        }

        _controller.Move(move * (moveSpeed * Time.deltaTime));

        if (_controller.isGrounded && _velocity.y < 0f)
        {
            _velocity.y = -2f;
        }

        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }
}
