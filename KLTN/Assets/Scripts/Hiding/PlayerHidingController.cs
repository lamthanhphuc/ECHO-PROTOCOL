using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerHidingController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private EchoProtocol.Networking.NetworkPlayerMovement networkMovement;
    [SerializeField] private PlayerInteraction interaction;
    [SerializeField] private EchoProtocol.Networking.NetworkPlayerInteractor networkInteractor;

    [Header("Settings")]
    [SerializeField] private float hidingYawLimitDegrees = 45f;

    private InputAction _interactAction;
    private CharacterController _characterController;
    private Fusion.NetworkCharacterController _networkCharacterController;
    private PlayerCamera _playerCameraController;
    private HidingSpot _currentSpot;
    private int _enteredFrame = -1;

    public bool IsHidden => _currentSpot != null;
    public HidingSpot CurrentSpot => _currentSpot;

    private void Awake()
    {
        if (movement == null)
        {
            movement = GetComponent<PlayerMovement>();
        }
        if (networkMovement == null)
        {
            networkMovement = GetComponent<EchoProtocol.Networking.NetworkPlayerMovement>();
        }

        if (interaction == null)
        {
            interaction = GetComponent<PlayerInteraction>();
        }
        if (networkInteractor == null)
        {
            networkInteractor = GetComponent<EchoProtocol.Networking.NetworkPlayerInteractor>();
        }

        _characterController = GetComponent<CharacterController>();
        _networkCharacterController = GetComponent<Fusion.NetworkCharacterController>();

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        _playerCameraController = playerCamera != null ? playerCamera.GetComponent<PlayerCamera>() : null;
        BindInput();
    }

    private void OnEnable()
    {
        if (_interactAction == null)
        {
            BindInput();
        }

        if (_interactAction != null)
        {
            _interactAction.performed += OnInteractPerformed;
            _interactAction.Enable();
        }
    }

    private void OnDisable()
    {
        if (_interactAction != null)
        {
            _interactAction.performed -= OnInteractPerformed;
            _interactAction.Disable();
        }

        if (_playerCameraController != null)
        {
            _playerCameraController.ClearYawLimit();
            _playerCameraController.UnlockPitch();
        }
    }

    public bool EnterHiding(HidingSpot spot)
    {
        if (spot == null || IsHidden || !spot.TryOccupy(this))
        {
            return false;
        }

        _currentSpot = spot;
        _enteredFrame = Time.frameCount;

        if (movement != null)
        {
            movement.enabled = false;
        }

        MoveToHidingPoint(spot.HidePoint);

        if (_playerCameraController != null)
        {
            float limit = spot.YawLimitDegrees > 0f ? spot.YawLimitDegrees : hidingYawLimitDegrees;
            _playerCameraController.SetYawLimit(spot.HidePoint.eulerAngles.y, limit);
            _playerCameraController.LockPitch(0f);
        }

        return true;
    }

    public void ExitHiding()
    {
        if (_currentSpot == null)
        {
            return;
        }

        HidingSpot exitingSpot = _currentSpot;
        Transform exitPoint = exitingSpot.ExitPoint;
        _currentSpot = null;
        exitingSpot.Release(this);

        if (_playerCameraController != null)
        {
            _playerCameraController.ClearYawLimit();
            _playerCameraController.UnlockPitch();
        }

        if (exitPoint != null)
        {
            MoveToHidingPoint(exitPoint);
        }

        if (movement != null)
        {
            movement.enabled = true;
        }

        if (_playerCameraController != null)
        {
            _playerCameraController.SetTarget(transform);
            _playerCameraController.enabled = true;
            _playerCameraController.ClearYawLimit();
            _playerCameraController.UnlockPitch();
        }
    }

    private void BindInput()
    {
        if (inputActions == null)
        {
            return;
        }

        InputActionMap playerMap = inputActions.FindActionMap("Player", false);
        _interactAction = playerMap?.FindAction("Interact", false);
    }

    private void MoveToHidingPoint(Transform point)
    {
        if (point == null)
        {
            return;
        }

        Quaternion targetRot = Quaternion.Euler(0f, point.eulerAngles.y, 0f);

        if (networkMovement != null)
        {
            networkMovement.TeleportAuthoritative(point.position, targetRot);
            UpdateCameraPose(point);
            return;
        }

        bool canNetworkTeleport = _networkCharacterController != null
            && _networkCharacterController.Object != null
            && _networkCharacterController.Object.IsValid
            && _networkCharacterController.StateBufferIsValid
            && _networkCharacterController.Object.HasStateAuthority;

        if (canNetworkTeleport)
        {
            try
            {
                _networkCharacterController.Teleport(point.position, targetRot);
                Physics.SyncTransforms();
                UpdateCameraPose(point);
                return;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PlayerHidingController] Network teleport failed, falling back to direct transform move: {ex.Message}");
            }
        }

        bool controllerWasEnabled = _characterController != null && _characterController.enabled;
        if (_characterController != null)
        {
            _characterController.enabled = false;
        }

        transform.position = point.position;
        transform.rotation = targetRot;

        if (_characterController != null)
        {
            _characterController.enabled = controllerWasEnabled;
        }

        Physics.SyncTransforms();
        UpdateCameraPose(point);
    }

    private void UpdateCameraPose(Transform point)
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (playerCamera != null && _playerCameraController == null)
        {
            _playerCameraController = playerCamera.GetComponent<PlayerCamera>();
        }

        if (_playerCameraController != null)
        {
            _playerCameraController.SetRotation(point.eulerAngles.y, 0f);
            if (IsHidden && _currentSpot != null)
            {
                float limit = _currentSpot.YawLimitDegrees > 0f ? _currentSpot.YawLimitDegrees : hidingYawLimitDegrees;
                _playerCameraController.SetYawLimit(point.eulerAngles.y, limit);
                _playerCameraController.LockPitch(0f);
            }
            else
            {
                _playerCameraController.ClearYawLimit();
                _playerCameraController.UnlockPitch();
            }
        }
        else if (playerCamera != null)
        {
            float eyeHeight = 1.65f;
            playerCamera.transform.SetPositionAndRotation(
                point.position + Vector3.up * eyeHeight,
                Quaternion.Euler(0f, point.eulerAngles.y, 0f));
        }
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (IsHidden && Time.frameCount > _enteredFrame)
        {
            ExitHiding();
        }
    }
}
