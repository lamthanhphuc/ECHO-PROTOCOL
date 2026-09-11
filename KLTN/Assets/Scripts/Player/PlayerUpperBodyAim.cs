using EchoProtocol.Networking;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerUpperBodyAim : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Transform playerRoot;
    [SerializeField] private PlayerCamera playerCamera;
    [SerializeField, Range(0f, 1f)] private float lookAtWeight = 0.82f;
    [SerializeField, Range(0f, 1f)] private float bodyWeight = 0.24f;
    [SerializeField, Range(0f, 1f)] private float headWeight = 0.74f;
    [SerializeField, Range(0f, 1f)] private float eyesWeight = 0.45f;
    [SerializeField, Range(0f, 1f)] private float clampWeight = 0.42f;
    [SerializeField] private float lookAtDistance = 12f;
    [SerializeField] private bool driveRightHandWhenHolding = true;

    [Header("Held Tool Right Hand Pose")]
    [SerializeField] private Vector3 handForwardOffset = new Vector3(0.18f, -0.08f, 0.32f);
    [SerializeField] private Vector3 handEulerOffset = new Vector3(0f, 0f, -75f);
    [SerializeField, Range(0f, 1f)] private float rightHandPosWeight = 0.72f;
    [SerializeField, Range(0f, 1f)] private float rightHandRotWeight = 0.75f;

    private PlayerInventory _inventory;
    private PlayerEnergyCoreCarrier _coreCarrier;
    private LobbyPlayerState _lobbyState;
    private PlayerHeldItemAnchor _heldItemAnchor;
    private bool _hasExternalAim;
    private Vector3 _externalAimOrigin;
    private Vector3 _externalAimForward;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
        if (playerRoot == null) playerRoot = ResolvePlayerRoot();
        _inventory = GetComponentInParent<PlayerInventory>();
        _coreCarrier = GetComponentInParent<PlayerEnergyCoreCarrier>();
        _lobbyState = GetComponentInParent<LobbyPlayerState>();
        _heldItemAnchor = GetComponentInParent<PlayerHeldItemAnchor>();
    }

    private void LateUpdate()
    {
        if (playerRoot == null) playerRoot = ResolvePlayerRoot();
        if (!_hasExternalAim && (playerCamera == null || !IsCameraTargetForThisPlayer(playerCamera.Target)))
        {
            playerCamera = FindBoundCamera();
        }
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || (!TryGetAim(out Vector3 aimOrigin, out Vector3 aimForward, out Vector3 aimUp)))
        {
            return;
        }

        Vector3 lookAt = aimOrigin + aimForward * lookAtDistance;

        animator.SetLookAtWeight(lookAtWeight, bodyWeight, headWeight, eyesWeight, clampWeight);
        animator.SetLookAtPosition(lookAt);

        if (!driveRightHandWhenHolding || !IsHoldingTeamTool())
        {
            return;
        }

        Transform handAnchor = _heldItemAnchor != null
            ? _heldItemAnchor.RightHandAnchor
            : PlayerHeldItemAnchor.ResolveRightHandAnchor(playerRoot != null ? playerRoot.gameObject : gameObject);
        if (handAnchor == null)
        {
            return;
        }

        Vector3 aimRight = Vector3.Cross(aimUp, aimForward).normalized;
        if (aimRight.sqrMagnitude <= 0.001f)
        {
            aimRight = transform.right;
        }

        Vector3 chestOrigin = transform.position + Vector3.up * 1.05f;
        Vector3 handTarget = chestOrigin
            + aimForward * handForwardOffset.z
            + aimRight * handForwardOffset.x
            + aimUp * handForwardOffset.y;

        float maxHandY = transform.position.y + 1.25f;
        float minHandY = transform.position.y + 0.75f;
        handTarget.y = Mathf.Clamp(handTarget.y, minHandY, maxHandY);

        Quaternion baseRotation = Quaternion.LookRotation(aimForward, aimUp);
        Quaternion handRotation = baseRotation * Quaternion.Euler(handEulerOffset);

        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, rightHandPosWeight);
        animator.SetIKRotationWeight(AvatarIKGoal.RightHand, rightHandRotWeight);
        animator.SetIKPosition(AvatarIKGoal.RightHand, handTarget);
        animator.SetIKRotation(AvatarIKGoal.RightHand, handRotation);
    }

    public void SetExternalAim(Vector3 origin, Vector3 forward)
    {
        if (forward.sqrMagnitude <= 0.001f)
        {
            return;
        }

        _hasExternalAim = true;
        _externalAimOrigin = origin;
        _externalAimForward = forward.normalized;
    }

    public void ClearExternalAim()
    {
        _hasExternalAim = false;
    }

    private bool TryGetAim(out Vector3 origin, out Vector3 forward, out Vector3 up)
    {
        if (_hasExternalAim)
        {
            origin = _externalAimOrigin;
            forward = _externalAimForward;
            up = Vector3.up;
            return true;
        }

        if (playerCamera != null)
        {
            origin = playerCamera.transform.position;
            forward = playerCamera.transform.forward;
            up = playerCamera.transform.up;
            return true;
        }

        Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        origin = head != null ? head.position : transform.position + Vector3.up * 1.5f;
        forward = transform.forward;
        up = Vector3.up;
        return true;
    }

    private bool IsHoldingTeamTool()
    {
        bool carryingCore = (_coreCarrier != null && _coreCarrier.IsCarrying)
            || (_lobbyState != null && _lobbyState.Object != null && _lobbyState.Object.IsValid && _lobbyState.CarriedCoreId.IsValid);
        if (carryingCore)
        {
            return false;
        }

        return (_inventory != null && _inventory.TeamToolSlot != null)
            || (_lobbyState != null && _lobbyState.Object != null && _lobbyState.Object.IsValid && _lobbyState.ToolId != 0);
    }

    private PlayerCamera FindBoundCamera()
    {
        foreach (PlayerCamera cameraController in FindObjectsByType<PlayerCamera>(FindObjectsInactive.Exclude))
        {
            if (IsCameraTargetForThisPlayer(cameraController.Target))
            {
                return cameraController;
            }
        }

        return null;
    }

    private bool IsCameraTargetForThisPlayer(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        Transform current = target;
        while (current != null)
        {
            if (current == transform || (playerRoot != null && current == playerRoot))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private Transform ResolvePlayerRoot()
    {
        Transform current = transform;
        while (current.parent != null)
        {
            if (current.GetComponent<PlayerMovement>() != null
                || current.GetComponent<NetworkPlayerMovement>() != null
                || current.GetComponent<LobbyPlayerState>() != null)
            {
                return current;
            }

            current = current.parent;
        }

        return transform.root != null ? transform.root : transform;
    }
}
