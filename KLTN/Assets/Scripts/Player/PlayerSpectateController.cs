using UnityEngine;

[DefaultExecutionOrder(500)]
public class PlayerSpectateController : MonoBehaviour
{
    [SerializeField] private PlayerDownState downState;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private bool autoSpectateWhenEliminated = true;

    private Transform _spectateTarget;
    private EchoProtocol.Networking.NetworkPlayerLifeState _networkLife;

    public bool IsSpectating => downState != null && downState.IsSpectating;
    public Transform SpectateTarget => _spectateTarget;

    private void Awake()
    {
        _networkLife = GetComponent<EchoProtocol.Networking.NetworkPlayerLifeState>();
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
        }
        if (downState != null)
        {
            downState.StateChanged -= OnLifeStateChanged;
            downState.StateChanged += OnLifeStateChanged;
        }
    }

    public void SetSpectateTarget(Transform target)
    {
        _spectateTarget = target;
    }

    private void LateUpdate()
    {
        EnsureDownState();
        if (_networkLife != null)
        {
            if (_networkLife.Object == null || !_networkLife.Object.IsValid || !_networkLife.Object.HasInputAuthority)
                return;
            if (!IsSpectating) { _spectateTarget = null; return; }
            playerCamera = Camera.main;
            var targetLife = _spectateTarget != null
                ? _spectateTarget.GetComponent<EchoProtocol.Networking.NetworkPlayerLifeState>() : null;
            if (targetLife == null || !targetLife.CanInitiateAction)
            {
                _spectateTarget = null;
                foreach (var candidate in _networkLife.Runner.ActivePlayers)
                {
                    if (!_networkLife.Runner.TryGetPlayerObject(candidate, out var player)
                        || player == _networkLife.Object
                        || !player.TryGetComponent<EchoProtocol.Networking.NetworkPlayerLifeState>(out var life)
                        || !life.CanInitiateAction) continue;
                    _spectateTarget = player.transform;
                    break;
                }
            }
        }
        if (!IsSpectating || _spectateTarget == null || playerCamera == null)
        {
            return;
        }

        Vector3 targetPosition = _spectateTarget.position + Vector3.up * 1.65f;
        playerCamera.transform.SetPositionAndRotation(targetPosition, _spectateTarget.rotation);
    }

    private void OnLifeStateChanged(PlayerDownState player, PlayerLifeState state)
    {
        if (_networkLife != null) return; // NetworkPlayerLifeState maps only the owner's death to Spectating.
        if (autoSpectateWhenEliminated && state == PlayerLifeState.Eliminated)
        {
            player.StartSpectating();
        }
    }
}
