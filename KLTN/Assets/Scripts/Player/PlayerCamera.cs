using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float eyeHeight = 1.65f;
    [SerializeField] private float crouchEyeHeight = 1.3f;
    [SerializeField] private float downedEyeHeight = 0.75f;
    [SerializeField] private float eyeHeightTransitionSpeed = 10f;
    [SerializeField] private float minPitch = -45f;
    [SerializeField] private float maxPitch = 45f;
    [SerializeField] private float cameraForwardOffset = 0.13f;
    [SerializeField] private float crouchCameraRightOffset = 0.2f;
    [SerializeField] private float downedCameraRightOffset = 0.1f;
    [SerializeField] private float downedCameraForwardOffset = 0.4f;
    [SerializeField] private float nearClipPlane = 0.03f;
    [SerializeField] private bool lockCursorOnEnable = true;

    private InputAction _lookAction;
    private PlayerMovement _playerMovement;
    private EchoProtocol.Networking.NetworkPlayerMovement _networkMovement;
    private CharacterController _characterController;
    private EchoProtocol.Networking.NetworkPlayerLifeState _networkLifeState;
    private PlayerDownState _playerDownState;
    private float _pitch;
    private float _yaw;
    private float _currentEyeHeight;
    private float _currentRightOffset;
    private float? _forcedEyeHeight;
    private float? _clampedYawCenter;
    private float _clampedYawRange;
    private float? _lockedPitch;

    public float Yaw => _yaw;
    public float Pitch => _pitch;
    public Transform Target => target;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        _playerMovement = target != null ? target.GetComponent<PlayerMovement>() : null;
        _networkMovement = target != null ? target.GetComponent<EchoProtocol.Networking.NetworkPlayerMovement>() : null;
        _characterController = target != null ? target.GetComponent<CharacterController>() : null;
        _networkLifeState = target != null ? target.GetComponent<EchoProtocol.Networking.NetworkPlayerLifeState>() : null;
        _playerDownState = target != null ? target.GetComponent<PlayerDownState>() : null;

        if (target != null)
        {
            _yaw = target.eulerAngles.y;
        }

        Camera cameraComponent = GetComponent<Camera>();
        if (cameraComponent != null)
        {
            cameraComponent.nearClipPlane = Mathf.Max(0.01f, nearClipPlane);
        }
    }

    public bool AutoFindTarget()
    {
        // 1. Uu tien tim NetworkPlayerMovement
        var netMovements = Object.FindObjectsByType<EchoProtocol.Networking.NetworkPlayerMovement>(FindObjectsInactive.Exclude);
        foreach (var nm in netMovements)
        {
            if (nm != null && nm.gameObject.activeInHierarchy)
            {
                if (nm.Object == null || !nm.Object.IsValid || nm.Object.HasInputAuthority)
                {
                    SetTarget(nm.transform);
                    return true;
                }
            }
        }

        // 2. Tim GameObject co Tag Player
        var players = GameObject.FindGameObjectsWithTag("Player");
        foreach (var p in players)
        {
            if (p != null && p.activeInHierarchy)
            {
                SetTarget(p.transform);
                return true;
            }
        }

        // 3. Tim PlayerMovement
        var pm = Object.FindAnyObjectByType<PlayerMovement>();
        if (pm != null && pm.gameObject.activeInHierarchy)
        {
            SetTarget(pm.transform);
            return true;
        }

        return false;
    }

    private void Awake()
    {
        _currentEyeHeight = eyeHeight;

        Camera cameraComponent = GetComponent<Camera>();
        if (cameraComponent != null)
        {
            cameraComponent.nearClipPlane = Mathf.Max(0.01f, nearClipPlane);
        }

        if (inputActions != null)
        {
            InputActionMap playerMap = inputActions.FindActionMap("Player", false);
            _lookAction = playerMap?.FindAction("Look", false);
        }

        if (_lookAction == null)
        {
            _lookAction = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
        }

        if (target == null || !target.gameObject.activeInHierarchy)
        {
            AutoFindTarget();
        }
        else
        {
            SetTarget(target);
        }
    }

    private void OnEnable()
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            AutoFindTarget();
        }

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
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            AutoFindTarget();
            if (target == null)
            {
                return;
            }
        }

        Vector2 lookInput = _lookAction != null
            ? _lookAction.ReadValue<Vector2>()
            : Vector2.zero;

        if (_networkLifeState != null && (_networkLifeState.IsCaught || _networkLifeState.IsEliminated))
            lookInput = Vector2.zero;

        float yawDelta = lookInput.x * mouseSensitivity;
        float pitchDelta = lookInput.y * mouseSensitivity;

        if (_clampedYawCenter.HasValue)
        {
            float targetYaw = _yaw + yawDelta;
            float offset = Mathf.DeltaAngle(_clampedYawCenter.Value, targetYaw);
            offset = Mathf.Clamp(offset, -_clampedYawRange, _clampedYawRange);
            _yaw = Mathf.Repeat(_clampedYawCenter.Value + offset, 360f);
        }
        else
        {
            _yaw = Mathf.Repeat(_yaw + yawDelta, 360f);
        }

        if (_lockedPitch.HasValue)
        {
            _pitch = _lockedPitch.Value;
        }
        else
        {
            _pitch = Mathf.Clamp(_pitch - pitchDelta, minPitch, maxPitch);
        }

        if (_networkLifeState == null && target != null)
        {
            _networkLifeState = target.GetComponent<EchoProtocol.Networking.NetworkPlayerLifeState>();
        }
        if (_networkMovement == null && target != null)
        {
            _networkMovement = target.GetComponent<EchoProtocol.Networking.NetworkPlayerMovement>();
        }
        if (_playerMovement == null && target != null)
        {
            _playerMovement = target.GetComponent<PlayerMovement>();
        }
        if (_playerDownState == null && target != null)
        {
            _playerDownState = target.GetComponent<PlayerDownState>();
        }

        bool isDowned = (_networkLifeState != null && _networkLifeState.IsDowned)
            || (_playerDownState != null && _playerDownState.IsDowned);
        bool isCrouching = (_playerMovement != null && _playerMovement.IsCrouching)
            || (_networkMovement != null && _networkMovement.IsAnimationCrouching);

        float targetEyeHeight =
            _forcedEyeHeight ??
            (isDowned
                ? downedEyeHeight
                : (isCrouching
                    ? crouchEyeHeight
                    : eyeHeight));
        float targetRightOffset = isDowned
            ? downedCameraRightOffset
            : isCrouching
                ? crouchCameraRightOffset
                : 0f;
        float targetForwardOffset = isDowned
            ? downedCameraForwardOffset
            : cameraForwardOffset;

        _currentEyeHeight = Mathf.Lerp(
            _currentEyeHeight,
            targetEyeHeight,
            eyeHeightTransitionSpeed * Time.deltaTime);
        _currentRightOffset = Mathf.Lerp(
            _currentRightOffset,
            targetRightOffset,
            eyeHeightTransitionSpeed * Time.deltaTime);

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
            Vector3.up * (feetYOffset + _currentEyeHeight) +
            Quaternion.Euler(0f, _yaw, 0f)
            * (Vector3.forward * Mathf.Max(0f, targetForwardOffset)
               + Vector3.right * _currentRightOffset);

        transform.rotation =
            Quaternion.Euler(_pitch, _yaw, 0f);

        if (target != null)
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

    public void SetRotation(float yaw, float pitch = 0f)
    {
        _yaw = Mathf.Repeat(yaw, 360f);
        if (_clampedYawCenter.HasValue)
        {
            float offset = Mathf.DeltaAngle(_clampedYawCenter.Value, _yaw);
            offset = Mathf.Clamp(offset, -_clampedYawRange, _clampedYawRange);
            _yaw = Mathf.Repeat(_clampedYawCenter.Value + offset, 360f);
        }

        if (_lockedPitch.HasValue)
        {
            _pitch = _lockedPitch.Value;
        }
        else
        {
            _pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        if (target != null)
        {
            target.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }
    }

    public void LockPitch(float pitch = 0f)
    {
        _lockedPitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        _pitch = _lockedPitch.Value;
    }

    public void UnlockPitch()
    {
        _lockedPitch = null;
    }

    public void SetYawLimit(float centerYaw, float maxOffsetDegrees)
    {
        _clampedYawCenter = Mathf.Repeat(centerYaw, 360f);
        _clampedYawRange = Mathf.Max(1f, Mathf.Abs(maxOffsetDegrees));

        float offset = Mathf.DeltaAngle(_clampedYawCenter.Value, _yaw);
        offset = Mathf.Clamp(offset, -_clampedYawRange, _clampedYawRange);
        _yaw = Mathf.Repeat(_clampedYawCenter.Value + offset, 360f);

        if (target != null)
        {
            target.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }
    }

    public void ClearYawLimit()
    {
        _clampedYawCenter = null;
    }

    private void OnValidate()
    {
        Camera cameraComponent = GetComponent<Camera>();
        if (cameraComponent != null)
        {
            cameraComponent.nearClipPlane = Mathf.Max(0.01f, nearClipPlane);
        }
    }
}
