using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerHidingController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private PlayerInteraction interaction;

    private InputAction _interactAction;
    private CharacterController _characterController;
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

        if (interaction == null)
        {
            interaction = GetComponent<PlayerInteraction>();
        }

        _characterController = GetComponent<CharacterController>();

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

        if (_playerCameraController != null)
        {
            _playerCameraController.enabled = false;
        }

        MoveToHidingPoint(spot.HidePoint);
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
            _playerCameraController.enabled = true;
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

        bool controllerWasEnabled = _characterController != null && _characterController.enabled;
        if (_characterController != null)
        {
            _characterController.enabled = false;
        }

        transform.position = point.position;
        transform.rotation = Quaternion.Euler(0f, point.eulerAngles.y, 0f);

        if (playerCamera != null)
        {
            playerCamera.transform.SetPositionAndRotation(point.position, point.rotation);
        }

        if (_characterController != null)
        {
            _characterController.enabled = controllerWasEnabled;
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
