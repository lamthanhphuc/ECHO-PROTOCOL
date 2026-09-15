using EchoProtocol.Networking;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class PlayerReviveInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private PlayerDownState downState;
    [SerializeField] private float reviveDurationSeconds = 3f;
    [SerializeField] private string revivePrompt = "Giữ để Cứu Đồng Đội";
    [SerializeField] private UnityEvent reviveStarted;
    [SerializeField] private UnityEvent reviveCompleted;
    [SerializeField] private UnityEvent reviveInterrupted;

    private GameObject _reviver;
    private float _reviveTimer;
    private bool _networkAuthorityPresentationOnly;
    private bool _authoritativeIsReviving;
    private float _authoritativeProgress01;

    public bool IsReviving => _networkAuthorityPresentationOnly ? _authoritativeIsReviving : _reviver != null;
    public float ReviveProgress01 => _networkAuthorityPresentationOnly
        ? _authoritativeProgress01
        : reviveDurationSeconds <= 0f ? 1f : Mathf.Clamp01(_reviveTimer / reviveDurationSeconds);
    public GameObject Reviver => _reviver;
    public string InteractionPrompt => string.IsNullOrWhiteSpace(revivePrompt)
        ? "Giữ để Cứu Đồng Đội"
        : revivePrompt;

    private void Awake()
    {
        if (downState == null)
        {
            downState = GetComponent<PlayerDownState>();
        }
    }

    private void Update()
    {
        if (_networkAuthorityPresentationOnly || _reviver == null)
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
        if (_networkAuthorityPresentationOnly) return false;

        if (downState == null || !downState.IsDowned || interactor == null || interactor == gameObject)
        {
            return false;
        }

        PlayerDownState reviverState = interactor.GetComponentInParent<PlayerDownState>();
        return reviverState == null || reviverState.IsActive;
    }

    public void Interact(GameObject interactor)
    {
        if (_networkAuthorityPresentationOnly || !CanInteract(interactor))
        {
            return;
        }

        if (_reviver == null)
        {
            _reviver = interactor;
            _reviveTimer = 0f;
            SetReviverAnimation(_reviver, true);
            reviveStarted?.Invoke();
        }
    }

    public void InterruptRevive()
    {
        if (_networkAuthorityPresentationOnly || _reviver == null)
        {
            return;
        }

        SetReviverAnimation(_reviver, false);
        _reviver = null;
        _reviveTimer = 0f;
        reviveInterrupted?.Invoke();
    }

    public void SetNetworkAuthorityPresentationOnly(bool enabled)
    {
        _networkAuthorityPresentationOnly = enabled;
    }

    public void ApplyAuthoritativeSnapshot(
        bool isReviving,
        GameObject reviver,
        float progress01,
        bool completed)
    {
        var wasReviving = _authoritativeIsReviving;
        var previousReviver = _reviver;
        _authoritativeIsReviving = isReviving;
        _authoritativeProgress01 = Mathf.Clamp01(progress01);
        _reviver = reviver;

        if (!wasReviving && isReviving)
        {
            SetReviverAnimation(_reviver, true);
            reviveStarted?.Invoke();
        }
        else if (wasReviving && !isReviving)
        {
            SetReviverAnimation(previousReviver, false);
            if (completed) reviveCompleted?.Invoke();
            else reviveInterrupted?.Invoke();
        }
    }

    private void CompleteRevive()
    {
        if (downState != null && downState.Revive())
        {
            reviveCompleted?.Invoke();
        }

        SetReviverAnimation(_reviver, false);
        _reviver = null;
        _reviveTimer = 0f;
    }

    private static void SetReviverAnimation(GameObject reviver, bool isReviving)
    {
        if (reviver == null) return;

        var driver = reviver.GetComponentInParent<PlayerAnimatorDriver>();
        if (driver != null)
        {
            driver.SetReviving(isReviving);
        }
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
