using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class PlayerReviveInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private PlayerDownState downState;
    [SerializeField] private float reviveDurationSeconds = 2.5f;
    [SerializeField] private string revivePrompt = "Revive teammate";
    [SerializeField] private UnityEvent reviveStarted;
    [SerializeField] private UnityEvent reviveCompleted;
    [SerializeField] private UnityEvent reviveInterrupted;

    private GameObject _reviver;
    private float _reviveTimer;

    public bool IsReviving => _reviver != null;
    public float ReviveProgress01 => reviveDurationSeconds <= 0f ? 1f : Mathf.Clamp01(_reviveTimer / reviveDurationSeconds);
    public GameObject Reviver => _reviver;
    public string InteractionPrompt => revivePrompt + " (" + Mathf.CeilToInt(reviveDurationSeconds) + "s)";

    private void Awake()
    {
        if (downState == null)
        {
            downState = GetComponent<PlayerDownState>();
        }
    }

    private void Update()
    {
        if (_reviver == null)
        {
            return;
        }

        if (!CanReviverContinue(_reviver))
        {
            InterruptRevive();
            return;
        }

        _reviveTimer += Time.deltaTime;
        if (_reviveTimer >= reviveDurationSeconds)
        {
            CompleteRevive();
        }
    }

    public bool CanInteract(GameObject interactor)
    {
        if (downState == null || !downState.IsDowned || interactor == null || interactor == gameObject)
        {
            return false;
        }

        PlayerDownState reviverState = interactor.GetComponentInParent<PlayerDownState>();
        return reviverState == null || reviverState.IsActive;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        if (_reviver == null)
        {
            _reviver = interactor;
            _reviveTimer = 0f;
            reviveStarted?.Invoke();
        }
    }

    public void InterruptRevive()
    {
        if (_reviver == null)
        {
            return;
        }

        _reviver = null;
        _reviveTimer = 0f;
        reviveInterrupted?.Invoke();
    }

    private void CompleteRevive()
    {
        if (downState != null && downState.Revive())
        {
            reviveCompleted?.Invoke();
        }

        _reviver = null;
        _reviveTimer = 0f;
    }

    private static bool CanReviverContinue(GameObject reviver)
    {
        if (reviver == null || !reviver.activeInHierarchy)
        {
            return false;
        }

        PlayerDownState reviverState = reviver.GetComponentInParent<PlayerDownState>();
        return reviverState == null || reviverState.IsActive;
    }
}
