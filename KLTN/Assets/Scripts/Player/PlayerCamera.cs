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
    private float _pitch;
    private float _currentEyeHeight;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        _playerMovement = target != null ? target.GetComponent<PlayerMovement>() : null;
    }

    private void Awake()
    {
        _currentEyeHeight = eyeHeight;
        _playerMovement = target != null ? target.GetComponent<PlayerMovement>() : null;

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

        Vector2 lookInput = _lookAction != null ? _lookAction.ReadValue<Vector2>() : Vector2.zero;
        float yaw = lookInput.x * mouseSensitivity;
        float pitch = lookInput.y * mouseSensitivity;

        target.Rotate(Vector3.up, yaw, Space.World);
        _pitch = Mathf.Clamp(_pitch - pitch, minPitch, maxPitch);

        float targetEyeHeight = _playerMovement != null && _playerMovement.IsCrouching ? crouchEyeHeight : eyeHeight;
        _currentEyeHeight = Mathf.Lerp(_currentEyeHeight, targetEyeHeight, eyeHeightTransitionSpeed * Time.deltaTime);

        transform.position = target.position + Vector3.up * _currentEyeHeight;
        transform.rotation = Quaternion.Euler(_pitch, target.eulerAngles.y, 0f);
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
}
