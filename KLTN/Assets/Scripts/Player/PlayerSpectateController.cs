using UnityEngine;
using UnityEngine.InputSystem;
using Fusion;

[DefaultExecutionOrder(500)]
public class PlayerSpectateController : MonoBehaviour
{
    [SerializeField] private PlayerDownState downState;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private bool autoSpectateWhenEliminated = true;
    [SerializeField] private InputActionReference nextSpectateTargetAction;

    private Transform _spectateTarget;
    private EchoProtocol.Networking.NetworkPlayerLifeState _networkLife;
    private int _spectateTargetIndex = -1;
    private int _spectateTargetCount;
    private bool _wasSpectating;

    public bool IsSpectating => downState != null && downState.IsSpectating;
    public Transform SpectateTarget => _spectateTarget;
    public int SpectateTargetIndex => _spectateTargetIndex;
    public int SpectateTargetCount => _spectateTargetCount;
    public string SpectateTargetLabel => BuildSpectateTargetLabel(_spectateTarget, _spectateTargetIndex);

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
        nextSpectateTargetAction?.action?.Enable();
    }

    private void OnDisable()
    {
        nextSpectateTargetAction?.action?.Disable();
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
            if (!IsSpectating)
            {
                _wasSpectating = false;
                _spectateTarget = null;
                _spectateTargetCount = 0;
                var ownCamera = Camera.main;
                var ownController = ownCamera != null ? ownCamera.GetComponent<PlayerCamera>() : null;
                if (ownController != null && ownController.Target != transform)
                {
                    ownController.SetTarget(transform);
                }
                ownController?.SetSpectateThirdPerson(false);
                return;
            }
            playerCamera = Camera.main;

            if (!_wasSpectating)
            {
                _wasSpectating = true;
                SelectNextSpectateTarget();
            }
            else if (WasNextSpectateTargetPressed())
            {
                SelectNextSpectateTarget();
            }
            else
            {
                EnsureValidSpectateTarget();
            }
        }
        if (!IsSpectating || _spectateTarget == null || playerCamera == null)
        {
            return;
        }

        var controller = playerCamera.GetComponent<PlayerCamera>();
        if (controller != null && controller.Target != _spectateTarget)
        {
            controller.SetTarget(_spectateTarget, driveTargetRotation: false);
        }
        if (controller != null)
        {
            controller.SetSpectateThirdPerson(true);
        }
        else if (controller == null)
        {
            Vector3 targetPosition = _spectateTarget.position + Vector3.up * 2.1f - _spectateTarget.forward * 4.5f;
            Quaternion targetRotation = Quaternion.LookRotation(
                (_spectateTarget.position + Vector3.up * 1.35f - targetPosition).normalized,
                Vector3.up);
            playerCamera.transform.SetPositionAndRotation(targetPosition, targetRotation);
        }
    }

    private void EnsureValidSpectateTarget()
    {
        var targetLife = _spectateTarget != null
            ? _spectateTarget.GetComponent<EchoProtocol.Networking.NetworkPlayerLifeState>()
            : null;
        if (targetLife == null || !IsValidSpectateLife(targetLife))
        {
            SelectNextSpectateTarget();
        }
    }

    private void SelectNextSpectateTarget()
    {
        if (_networkLife == null || _networkLife.Runner == null)
        {
            _spectateTarget = null;
            _spectateTargetCount = 0;
            return;
        }

        var candidates = new System.Collections.Generic.List<Transform>();
        foreach (var candidate in _networkLife.Runner.ActivePlayers)
        {
            if (!_networkLife.Runner.TryGetPlayerObject(candidate, out var player)
                || player == _networkLife.Object
                || !player.TryGetComponent<EchoProtocol.Networking.NetworkPlayerLifeState>(out var life)
                || !IsValidSpectateLife(life))
            {
                continue;
            }

            candidates.Add(player.transform);
        }

        if (candidates.Count == 0)
        {
            _spectateTarget = null;
            _spectateTargetIndex = -1;
            _spectateTargetCount = 0;
            return;
        }

        _spectateTargetCount = candidates.Count;
        int currentIndex = _spectateTarget != null ? candidates.IndexOf(_spectateTarget) : _spectateTargetIndex;
        _spectateTargetIndex = (currentIndex + 1) % candidates.Count;
        _spectateTarget = candidates[_spectateTargetIndex];
    }

    private bool WasNextSpectateTargetPressed()
    {
        if (nextSpectateTargetAction != null && nextSpectateTargetAction.action != null)
        {
            return nextSpectateTargetAction.action.WasPressedThisFrame();
        }

        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    private static string BuildSpectateTargetLabel(Transform target, int index)
    {
        if (target == null) return "NO SIGNAL";

        var lobbyState = target.GetComponent<EchoProtocol.Networking.LobbyPlayerState>();
        if (lobbyState != null)
        {
            if (lobbyState.TeamId > 0) return $"PLAYER {lobbyState.TeamId}";

            var operatorName = lobbyState.OperatorName.ToString();
            if (!string.IsNullOrWhiteSpace(operatorName)) return operatorName.ToUpperInvariant();
        }

        var networkObject = target.GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.InputAuthority.IsRealPlayer)
            return networkObject.InputAuthority.ToString().ToUpperInvariant();

        return index >= 0 ? $"PLAYER {index + 1}" : target.name.ToUpperInvariant();
    }

    private static bool IsValidSpectateLife(EchoProtocol.Networking.NetworkPlayerLifeState life)
    {
        return life != null && !life.IsEliminated && (life.CanInitiateAction || life.IsDowned);
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
