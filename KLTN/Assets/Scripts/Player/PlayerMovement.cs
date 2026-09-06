using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float sprintSpeed = 7f;
    [SerializeField] private float crouchSpeed = 2f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float sprintForwardInputThreshold = 0.75f;
    [SerializeField] private float sprintSideInputTolerance = 0.2f;

    [Header("Crouch")]
    [SerializeField] private float standingHeight = 2f;
    [SerializeField] private float crouchHeight = 1.2f;
    [SerializeField] private float standingRadius = 0.42f;
    [SerializeField] private float crouchRadius = 0.42f;
    [SerializeField] private float crouchTransitionSpeed = 10f;
    [SerializeField] private LayerMask standUpObstructionMask = ~0;
    [SerializeField] private QueryTriggerInteraction standUpTriggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Stamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float sprintStaminaDrainPerSecond = 25f;
    [SerializeField] private float staminaRegenPerSecond = 18f;
    [SerializeField] private float minStaminaToSprint = 5f;

    private CharacterController _controller;
    private InputAction _moveAction;
    private InputAction _sprintAction;
    private InputAction _crouchAction;
    private Vector3 _velocity;
    private float _currentStamina;
    private float _externalSpeedMultiplier = 1f;
    private bool _isSprinting;
    private bool _isCrouching;
    private bool _isSprintBlocked;
    private Vector2 _moveInput;

    public float CurrentStamina => _currentStamina;
    public float MaxStamina => maxStamina;
    public bool IsSprinting => _isSprinting;
    public bool IsCrouching => _isCrouching;
    public bool IsSprintBlocked => _isSprintBlocked;
    public Vector2 MoveInput => _moveInput;
    public bool HasForwardSprintInput =>
        _moveInput.y >= sprintForwardInputThreshold && Mathf.Abs(_moveInput.x) <= sprintSideInputTolerance;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _currentStamina = maxStamina;

        if (inputActions != null)
        {
            InputActionMap playerMap = inputActions.FindActionMap("Player", false);
            _moveAction = playerMap?.FindAction("Move", false);
            _sprintAction = playerMap?.FindAction("Sprint", false);
            _crouchAction = playerMap?.FindAction("Crouch", false);
        }
    }

    private void OnEnable()
    {
        _moveAction?.Enable();
        _sprintAction?.Enable();
        _crouchAction?.Enable();
    }

    private void OnDisable()
    {
        _moveAction?.Disable();
        _sprintAction?.Disable();
        _crouchAction?.Disable();
    }

    private void Update()
    {
        Vector2 input = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
        _moveInput = input.sqrMagnitude > 1f ? input.normalized : input;

        Vector3 move = transform.right * _moveInput.x + transform.forward * _moveInput.y;

        if (move.sqrMagnitude > 1f)
        {
            move.Normalize();
        }

        bool wantsSprint = _sprintAction != null && _sprintAction.IsPressed();
        bool wantsCrouch = _crouchAction != null && _crouchAction.IsPressed();
        _isCrouching = wantsCrouch || (_isCrouching && !CanStandUp());
        _isSprinting = CanSprint(wantsSprint, move);

        float speed = GetCurrentSpeed();
        _controller.Move(move * (speed * Time.deltaTime));

        UpdateStamina();
        UpdateCrouchHeight();

        if (_controller.isGrounded && _velocity.y < 0f)
        {
            _velocity.y = -2f;
        }

        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }

    private bool CanSprint(bool wantsSprint, Vector3 move)
    {
        if (_isSprintBlocked || !wantsSprint || _isCrouching || move.sqrMagnitude <= 0.01f || !HasForwardSprintInput)
        {
            return false;
        }

        return _currentStamina > minStaminaToSprint;
    }

    private float GetCurrentSpeed()
    {
        if (_isCrouching)
        {
            return crouchSpeed;
        }

        float baseSpeed = _isSprinting ? sprintSpeed : walkSpeed;
        return baseSpeed * _externalSpeedMultiplier;
    }

    private void UpdateStamina()
    {
        if (_isSprinting)
        {
            _currentStamina -= sprintStaminaDrainPerSecond * Time.deltaTime;
        }
        else
        {
            _currentStamina += staminaRegenPerSecond * Time.deltaTime;
        }

        _currentStamina = Mathf.Clamp(_currentStamina, 0f, maxStamina);
        if (_currentStamina <= 0f)
        {
            _isSprinting = false;
        }
    }

    private void UpdateCrouchHeight()
    {
        float targetHeight = _isCrouching ? crouchHeight : standingHeight;
        float targetRadius = _isCrouching ? crouchRadius : standingRadius;
        _controller.height = Mathf.Lerp(_controller.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);
        _controller.radius = Mathf.Lerp(_controller.radius, targetRadius, crouchTransitionSpeed * Time.deltaTime);
        _controller.center = Vector3.up * ((_controller.height - standingHeight) * 0.5f);
    }

    private bool CanStandUp()
    {
        if (_controller == null || _controller.height >= standingHeight - 0.02f)
        {
            return true;
        }

        float radius = Mathf.Max(0.05f, standingRadius * 0.95f);
        Vector3 standingCenter = transform.position;
        Vector3 bottom = standingCenter + Vector3.up * (-standingHeight * 0.5f + radius);
        Vector3 top = standingCenter + Vector3.up * (standingHeight * 0.5f - radius);

        return !Physics.CheckCapsule(
            bottom,
            top,
            radius,
            standUpObstructionMask,
            standUpTriggerInteraction);
    }

    public void SetExternalSpeedMultiplier(float multiplier)
    {
        _externalSpeedMultiplier = Mathf.Max(0f, multiplier);
    }

    public void SetSprintBlocked(bool blocked)
    {
        _isSprintBlocked = blocked;
        if (_isSprintBlocked)
        {
            _isSprinting = false;
        }
    }
}
