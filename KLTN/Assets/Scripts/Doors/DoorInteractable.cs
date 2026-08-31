using UnityEngine;
using UnityEngine.Events;

public class DoorInteractable : MonoBehaviour, IInteractable
{
    [Header("Door")]
    [SerializeField] private Transform doorPivot;
    [SerializeField] private bool startsOpen;
    [SerializeField] private bool isLocked;
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float moveSpeed = 8f;

    [Header("Collision")]
    [SerializeField] private Collider interactionCollider;
    [SerializeField] private Collider[] blockingColliders;
    [SerializeField] private bool disableBlockingCollidersWhenOpen = true;

    [Header("Prompt")]
    [SerializeField] private string openPrompt = "Open door";
    [SerializeField] private string closePrompt = "Close door";
    [SerializeField] private string lockedPrompt = "Door locked";

    [Header("Events")]
    [SerializeField] private UnityEvent opened;
    [SerializeField] private UnityEvent closed;

    private Quaternion _closedRotation;
    private Quaternion _openRotation;
    private bool _isOpen;

    public bool IsOpen => _isOpen;
    public bool IsLocked => isLocked;
    public string InteractionPrompt => isLocked ? lockedPrompt : _isOpen ? closePrompt : openPrompt;

    private void Awake()
    {
        if (doorPivot == null)
        {
            doorPivot = transform;
        }

        _closedRotation = doorPivot.localRotation;
        _openRotation = _closedRotation * Quaternion.Euler(0f, openAngle, 0f);
        SetOpenImmediate(startsOpen, false);
    }

    private void Update()
    {
        if (doorPivot == null)
        {
            return;
        }

        Quaternion targetRotation = _isOpen ? _openRotation : _closedRotation;
        doorPivot.localRotation = Quaternion.Slerp(
            doorPivot.localRotation,
            targetRotation,
            moveSpeed * Time.deltaTime);
    }

    public bool CanInteract(GameObject interactor)
    {
        return true;
    }

    public void Interact(GameObject interactor)
    {
        if (isLocked)
        {
            return;
        }

        SetOpen(!_isOpen);
    }

    public void SetLocked(bool locked)
    {
        isLocked = locked;
    }

    public void SetOpen(bool open)
    {
        if (_isOpen == open)
        {
            return;
        }

        _isOpen = open;
        ApplyBlockingColliderState();

        if (_isOpen)
        {
            opened?.Invoke();
        }
        else
        {
            closed?.Invoke();
        }
    }

    public void SetOpenImmediate(bool open, bool invokeEvents = true)
    {
        _isOpen = open;

        if (doorPivot != null)
        {
            doorPivot.localRotation = _isOpen ? _openRotation : _closedRotation;
        }

        ApplyBlockingColliderState();

        if (!invokeEvents)
        {
            return;
        }

        if (_isOpen)
        {
            opened?.Invoke();
        }
        else
        {
            closed?.Invoke();
        }
    }

    private void ApplyBlockingColliderState()
    {
        if (!disableBlockingCollidersWhenOpen)
        {
            return;
        }

        Collider[] colliders = blockingColliders;
        if (colliders == null || colliders.Length == 0)
        {
            colliders = GetComponents<Collider>();
        }

        foreach (Collider blockingCollider in colliders)
        {
            if (blockingCollider == null || blockingCollider == interactionCollider)
            {
                continue;
            }

            blockingCollider.enabled = !_isOpen;
        }
    }
}
