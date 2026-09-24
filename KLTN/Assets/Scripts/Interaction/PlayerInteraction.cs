using System;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Raycast")]
    [SerializeField] private Camera raycastCamera;
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactableLayers = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

    private InputAction _interactAction;
    private IInteractable _currentInteractable;
    private IHoldInteractable _heldInteractable;
    private string _currentPrompt = string.Empty;
    private bool _suppressInteractionPrompt;
    private float _nextSecurityHoldRefresh;

    public event Action<string> PromptChanged;

    public IInteractable CurrentInteractable => _currentInteractable;
    public string CurrentPrompt => _suppressInteractionPrompt ? string.Empty : _currentPrompt;
    public bool IsInteractHeld => _heldInteractable != null;
    public bool IsInteractionPromptSuppressed => _suppressInteractionPrompt;

    public void SetInteractionPromptSuppressed(bool suppressed)
    {
        if (_suppressInteractionPrompt == suppressed)
        {
            return;
        }

        _suppressInteractionPrompt = suppressed;
        if (suppressed)
        {
            CancelHeldInteractable();
            SetCurrentInteractable(null);
        }

        PromptChanged?.Invoke(CurrentPrompt);
    }

    private void Awake()
    {
        if (raycastCamera == null)
        {
            raycastCamera = GetRaycastCamera();
        }

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
            _interactAction.started += OnInteractStarted;
            _interactAction.performed += OnInteractPerformed;
            _interactAction.canceled += OnInteractCanceled;
            _interactAction.Enable();
        }
    }

    private void OnDisable()
    {
        if (_interactAction != null)
        {
            _interactAction.started -= OnInteractStarted;
            _interactAction.performed -= OnInteractPerformed;
            _interactAction.canceled -= OnInteractCanceled;
            _interactAction.Disable();
        }

        CancelHeldInteractable();
        SetCurrentInteractable(null);
    }

    private void Update()
    {
        if (!HasLocalControl())
        {
            SetCurrentInteractable(null);
            return;
        }

        if (_suppressInteractionPrompt)
        {
            SetCurrentInteractable(null);
            return;
        }

        UpdateCurrentInteractable();
        ValidateHeldInteractable();
        RefreshSecurityHold();
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

    private Camera GetRaycastCamera()
    {
        if (raycastCamera != null && raycastCamera.gameObject.activeInHierarchy && raycastCamera.enabled)
        {
            return raycastCamera;
        }

        // 1. Try local camera on player or its children
        Camera cam = GetComponentInChildren<Camera>();
        if (cam != null && cam.gameObject.activeInHierarchy && cam.enabled)
        {
            raycastCamera = cam;
            return raycastCamera;
        }

        // 2. Try PlayerCamera in scene
        PlayerCamera playerCam = FindAnyObjectByType<PlayerCamera>();
        if (playerCam != null)
        {
            cam = playerCam.GetComponent<Camera>();
            if (cam != null && cam.gameObject.activeInHierarchy && cam.enabled)
            {
                raycastCamera = cam;
                return raycastCamera;
            }
        }

        // 3. Fallback to Camera.main
        if (Camera.main != null && Camera.main.gameObject.activeInHierarchy && Camera.main.enabled)
        {
            raycastCamera = Camera.main;
            return raycastCamera;
        }

        Camera anyCam = FindAnyObjectByType<Camera>();
        if (anyCam != null)
        {
            raycastCamera = anyCam;
            return raycastCamera;
        }

        return null;
    }

    private void UpdateCurrentInteractable()
    {
        Camera cam = GetRaycastCamera();
        if (cam == null)
        {
            SetCurrentInteractable(null);
            return;
        }

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableLayers, triggerInteraction))
        {
            SetCurrentInteractable(null);
            return;
        }

        // If in active Fusion gameplay, yield to NetworkPlayerInteractor for NetworkInteractable or downed teammates
        if (IsActiveFusionGameplay())
        {
            if (hit.collider.GetComponentInParent<EchoProtocol.Networking.NetworkInteractable>() != null)
            {
                SetCurrentInteractable(null);
                return;
            }

            var lifeState = hit.collider.GetComponentInParent<EchoProtocol.Networking.NetworkPlayerLifeState>();
            if (lifeState != null && lifeState.IsDowned)
            {
                SetCurrentInteractable(null);
                return;
            }
        }

        IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
        if (interactable == null || !interactable.CanInteract(gameObject))
        {
            SetCurrentInteractable(null);
            return;
        }

        SetCurrentInteractable(interactable);
    }

    private void SetCurrentInteractable(IInteractable interactable)
    {
        string nextPrompt = interactable != null ? interactable.InteractionPrompt : string.Empty;
        if (ReferenceEquals(_currentInteractable, interactable) && _currentPrompt == nextPrompt)
        {
            return;
        }

        _currentInteractable = interactable;
        _currentPrompt = nextPrompt;
        PromptChanged?.Invoke(_currentPrompt);
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (!HasLocalControl())
        {
            return;
        }

        if (_suppressInteractionPrompt)
        {
            return;
        }

        if (_currentInteractable != null && _currentInteractable.CanInteract(gameObject))
        {
            // In active Fusion gameplay, NetworkPlayerInteractor handles HidingSpot
            if (IsActiveFusionGameplay() && _currentInteractable is HidingSpot)
            {
                return;
            }

            if (_currentInteractable is IHoldInteractable holdInteractable && holdInteractable.RequiresHold)
            {
                return;
            }

            _currentInteractable.Interact(gameObject);
            UpdateCurrentInteractable();
        }
    }

    private void OnInteractStarted(InputAction.CallbackContext context)
    {
        if (!HasLocalControl())
        {
            return;
        }

        if (_suppressInteractionPrompt)
        {
            return;
        }

        // In active Fusion gameplay, NetworkPlayerInteractor handles HidingSpot
        if (IsActiveFusionGameplay() && _currentInteractable is HidingSpot)
        {
            return;
        }

        IHoldInteractable holdInteractable = _currentInteractable as IHoldInteractable;
        if (holdInteractable == null || !holdInteractable.RequiresHold)
        {
            return;
        }

        if (!holdInteractable.CanInteract(gameObject))
        {
            return;
        }

        _heldInteractable = holdInteractable;
        _heldInteractable.BeginHoldInteract(gameObject);
    }

    private void OnInteractCanceled(InputAction.CallbackContext context)
    {
        CancelHeldInteractable();
    }

    private void ValidateHeldInteractable()
    {
        if (_heldInteractable == null)
        {
            return;
        }

        if (!Application.isFocused || _interactAction == null || !_interactAction.IsPressed()
            || !ReferenceEquals(_currentInteractable, _heldInteractable) || !_heldInteractable.CanInteract(gameObject))
        {
            CancelHeldInteractable();
        }
    }

    private void RefreshSecurityHold()
    {
        if (!(_heldInteractable is SecurityTerminalDownload) || Time.unscaledTime < _nextSecurityHoldRefresh) return;
        _nextSecurityHoldRefresh = Time.unscaledTime + 0.25f;
        EchoProtocol.Networking.NetworkMatchState.Instance?.RequestRefreshSecurityHold();
    }

    private void CancelHeldInteractable()
    {
        if (_heldInteractable == null)
        {
            return;
        }

        IHoldInteractable held = _heldInteractable;
        _heldInteractable = null;
        held.EndHoldInteract(gameObject);
    }

    private bool HasLocalControl()
    {
        NetworkObject networkObject = GetComponentInParent<NetworkObject>();
        return networkObject == null || !networkObject.IsValid || networkObject.HasInputAuthority;
    }

    private bool IsActiveFusionGameplay()
    {
        var networkObject = GetComponentInParent<NetworkObject>();
        var lobbyState = GetComponentInParent<EchoProtocol.Networking.LobbyPlayerState>();
        return networkObject != null
            && networkObject.IsValid
            && networkObject.Runner != null
            && networkObject.Runner.IsRunning
            && lobbyState != null
            && lobbyState.IsGameplayPlayer;
    }
}
