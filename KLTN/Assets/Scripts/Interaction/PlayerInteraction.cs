using System;
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
    private string _currentPrompt = string.Empty;

    public event Action<string> PromptChanged;

    public IInteractable CurrentInteractable => _currentInteractable;
    public string CurrentPrompt => _currentPrompt;

    private void Awake()
    {
        if (raycastCamera == null)
        {
            raycastCamera = Camera.main;
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

        SetCurrentInteractable(null);
    }

    private void Update()
    {
        UpdateCurrentInteractable();
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

    private void UpdateCurrentInteractable()
    {
        if (raycastCamera == null)
        {
            raycastCamera = Camera.main;
        }

        if (raycastCamera == null)
        {
            SetCurrentInteractable(null);
            return;
        }

        Ray ray = raycastCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableLayers, triggerInteraction))
        {
            SetCurrentInteractable(null);
            return;
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
        if (_currentInteractable != null && _currentInteractable.CanInteract(gameObject))
        {
            _currentInteractable.Interact(gameObject);
            UpdateCurrentInteractable();
        }
    }
}
