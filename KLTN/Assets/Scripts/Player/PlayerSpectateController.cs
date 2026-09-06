using UnityEngine;

public class PlayerSpectateController : MonoBehaviour
{
    [SerializeField] private PlayerDownState downState;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private bool autoSpectateWhenEliminated = true;

    private Transform _spectateTarget;

    public bool IsSpectating => downState != null && downState.IsSpectating;
    public Transform SpectateTarget => _spectateTarget;

    private void Awake()
    {
        EnsureDownState();

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
    }

    private void OnEnable()
    {
        EnsureDownState();
    }

    private void OnDisable()
    {
        if (downState != null)
        {
            downState.StateChanged -= OnLifeStateChanged;
        }
    }

    private void EnsureDownState()
    {
        if (downState == null)
        {
            downState = GetComponent<PlayerDownState>();
            if (downState != null)
            {
                downState.StateChanged -= OnLifeStateChanged;
                downState.StateChanged += OnLifeStateChanged;
            }
        }
    }

    public void SetSpectateTarget(Transform target)
    {
        _spectateTarget = target;
    }

    private void LateUpdate()
    {
        EnsureDownState();
        if (!IsSpectating || _spectateTarget == null || playerCamera == null)
        {
            return;
        }

        Vector3 targetPosition = _spectateTarget.position + Vector3.up * 1.65f;
        playerCamera.transform.SetPositionAndRotation(targetPosition, _spectateTarget.rotation);
    }

    private void OnLifeStateChanged(PlayerDownState player, PlayerLifeState state)
    {
        if (autoSpectateWhenEliminated && state == PlayerLifeState.Eliminated)
        {
            player.StartSpectating();
        }
    }
}
