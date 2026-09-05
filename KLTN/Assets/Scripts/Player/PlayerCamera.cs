using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float eyeHeight = 1.65f;
    [SerializeField] private float crouchEyeHeight = 1.05f;
    [SerializeField] private float eyeHeightTransitionSpeed = 10f;
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;
    [SerializeField] private bool lockCursorOnEnable = true;

    private InputAction _lookAction;
    private PlayerMovement _playerMovement;
    private CharacterController _characterController;
    private float _pitch;
    private float _yaw;
    private float _currentEyeHeight;
    private float? _forcedEyeHeight;

    public float Yaw => _yaw;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        _playerMovement = target != null ? target.GetComponent<PlayerMovement>() : null;
        _characterController = target != null ? target.GetComponent<CharacterController>() : null;

        if (target != null)
        {
            _yaw = target.eulerAngles.y;
        }
    }

    private void Awake()
    {
        _currentEyeHeight = eyeHeight;
        _playerMovement = target != null ? target.GetComponent<PlayerMovement>() : null;
        _characterController = target != null ? target.GetComponent<CharacterController>() : null;

        if (target != null)
        {
            _yaw = target.eulerAngles.y;
        }

        if (inputActions != null)
        {
            InputActionMap playerMap = inputActions.FindActionMap("Player", false);
            _lookAction = playerMap?.FindAction("Look", false);
        }
    }

    private void OnEnable()
    {
        _lookAction?.Enable();

        if (lockCursorOnEnable)
        {
            LockCursor();
        }
    }

    private void OnDisable()
    {
        _lookAction?.Disable();
        UnlockCursor();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnlockCursor();
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            LockCursor();
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector2 lookInput = _lookAction != null
            ? _lookAction.ReadValue<Vector2>()
            : Vector2.zero;

        float yawDelta = lookInput.x * mouseSensitivity;
        float pitchDelta = lookInput.y * mouseSensitivity;

        _yaw = Mathf.Repeat(_yaw + yawDelta, 360f);
        _pitch = Mathf.Clamp(_pitch - pitchDelta, minPitch, maxPitch);

        float targetEyeHeight =
            _forcedEyeHeight ??
            (_playerMovement != null && _playerMovement.IsCrouching
                ? crouchEyeHeight
                : eyeHeight);

        _currentEyeHeight = Mathf.Lerp(
            _currentEyeHeight,
            targetEyeHeight,
            eyeHeightTransitionSpeed * Time.deltaTime);

        // CharacterController capsule center is relative to the target pivot.
        // Eye heights are measured upwards from the capsule feet.
        float feetYOffset = 0f;

        if (_characterController != null)
        {
            feetYOffset =
                _characterController.center.y -
                (_characterController.height * 0.5f);
        }
        else
        {
            _characterController = target.GetComponent<CharacterController>();

            feetYOffset = _characterController != null
                ? _characterController.center.y -
                  (_characterController.height * 0.5f)
                : -1.0f;
        }

        transform.position =
            target.position +
            Vector3.up * (feetYOffset + _currentEyeHeight);

        // Camera owns look yaw locally.
        transform.rotation =
            Quaternion.Euler(_pitch, _yaw, 0f);

        // For local player (standalone / prototype PlayerMovement), rotate the player transform to match look yaw
        // so that forward/strafe movement and visuals/flashlight align with the camera view.
        if (_playerMovement == null && target != null)
        {
            _playerMovement = target.GetComponent<PlayerMovement>();
        }

        if (_playerMovement != null)
        {
            target.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void SetForcedEyeHeight(float height)
    {
        _forcedEyeHeight = Mathf.Max(0.05f, height);
    }

    public void ClearForcedEyeHeight()
    {
        _forcedEyeHeight = null;
    }
}
