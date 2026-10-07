using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerHeldItemAnchor : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Transform rightHandAnchor;
    [SerializeField] private Transform coreCarryAnchor;
    [SerializeField] private Vector3 fallbackLocalPosition = new Vector3(0.33f, 1.18f, 0.42f);
    [SerializeField] private Vector3 fallbackLocalEulerAngles = new Vector3(4f, 8f, 0f);
    [SerializeField] private Vector3 coreCarryLocalPosition = new Vector3(0f, 0.04f, 0.28f);
    [SerializeField] private Vector3 coreCarryLocalEulerAngles = new Vector3(0f, 0f, 0f);

    private Transform _runtimeFallbackAnchor;
    private Transform _runtimeCoreCarryAnchor;
    private PlayerHidingController _hidingController;
    private EchoProtocol.Networking.NetworkPlayerMovement _networkMovement;
    private bool _hiddenStateInitialized;
    private bool _lastHiddenState;

    public Transform RightHandAnchor
    {
        get
        {
            if (_runtimeFallbackAnchor != null)
            {
                return _runtimeFallbackAnchor;
            }

            Transform baseTransform = rightHandAnchor;
            if (baseTransform == null)
            {
                ResolveAnimator();
                if (animator != null && animator.isHuman)
                {
                    baseTransform = animator.GetBoneTransform(HumanBodyBones.RightHand);
                }
            }

            if (baseTransform != null)
            {
                Transform existing = baseTransform.Find("Runtime_RightHandAnchor");
                if (existing == null)
                {
                    GameObject anchorObj = new GameObject("Runtime_RightHandAnchor");
                    anchorObj.transform.SetParent(baseTransform, false);
                    anchorObj.transform.localPosition = Vector3.zero;
                    anchorObj.transform.localRotation = GetComponent<PlayerCharacterPresenter>()?.RightHandGripCorrection ?? Quaternion.identity;
                    Vector3 lossy = baseTransform.lossyScale;
                    anchorObj.transform.localScale = new Vector3(
                        Mathf.Approximately(lossy.x, 0f) ? 1f : 1f / lossy.x,
                        Mathf.Approximately(lossy.y, 0f) ? 1f : 1f / lossy.y,
                        Mathf.Approximately(lossy.z, 0f) ? 1f : 1f / lossy.z
                    );
                    existing = anchorObj.transform;
                }
                _runtimeFallbackAnchor = existing;
                return _runtimeFallbackAnchor;
            }

            return EnsureFallbackAnchor();
        }
    }

    public static Transform ResolveRightHandAnchor(GameObject playerRoot)
    {
        if (playerRoot == null)
        {
            return null;
        }

        PlayerHeldItemAnchor anchor = playerRoot.GetComponentInChildren<PlayerHeldItemAnchor>(true);
        if (anchor == null)
        {
            anchor = playerRoot.AddComponent<PlayerHeldItemAnchor>();
        }

        return anchor.RightHandAnchor;
    }

    public static Transform ResolveCoreCarryAnchor(GameObject playerRoot)
    {
        if (playerRoot == null)
        {
            return null;
        }

        PlayerHeldItemAnchor anchor = playerRoot.GetComponentInChildren<PlayerHeldItemAnchor>(true);
        if (anchor == null)
        {
            anchor = playerRoot.AddComponent<PlayerHeldItemAnchor>();
        }

        return anchor.CoreCarryAnchor;
    }

    public Transform CoreCarryAnchor
    {
        get
        {
            if (coreCarryAnchor != null)
            {
                return coreCarryAnchor;
            }

            return EnsureCoreCarryAnchor();
        }
    }

    public void RefreshCharacterRig(Animator currentAnimator)
    {
        animator = currentAnimator;
        rightHandAnchor = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
        coreCarryAnchor = null;
        if (_runtimeFallbackAnchor != null && rightHandAnchor != null)
        {
            ReparentForCharacter(_runtimeFallbackAnchor, rightHandAnchor);
            _runtimeFallbackAnchor.localRotation = GetComponent<PlayerCharacterPresenter>()?.RightHandGripCorrection ?? Quaternion.identity;
        }
        if (_runtimeCoreCarryAnchor != null)
            ReparentForCharacter(_runtimeCoreCarryAnchor, ResolveCoreCarryParent());
    }

    private static void ReparentForCharacter(Transform anchor, Transform parent)
    {
        anchor.SetParent(parent, false);
        Vector3 scale = parent.lossyScale;
        anchor.localScale = new Vector3(
            Mathf.Approximately(scale.x, 0f) ? 1f : 1f / scale.x,
            Mathf.Approximately(scale.y, 0f) ? 1f : 1f / scale.y,
            Mathf.Approximately(scale.z, 0f) ? 1f : 1f / scale.z);
    }

    private void Awake()
    {
        ResolveAnimator();
        _hidingController = GetComponentInParent<PlayerHidingController>();
        _networkMovement = GetComponentInParent<EchoProtocol.Networking.NetworkPlayerMovement>();
    }

    private void LateUpdate()
    {
        bool hidden = ResolveHiddenState();
        if (_hiddenStateInitialized && hidden == _lastHiddenState)
        {
            return;
        }

        _hiddenStateInitialized = true;
        _lastHiddenState = hidden;
        SetHeldVisualsVisible(!hidden);
    }

    private bool ResolveHiddenState()
    {
        if (_networkMovement != null
            && _networkMovement.Object != null
            && _networkMovement.Object.IsValid)
        {
            return _networkMovement.IsHidden;
        }

        return _hidingController != null && _hidingController.IsHidden;
    }

    private void SetHeldVisualsVisible(bool visible)
    {
        Transform rightHand = RightHandAnchor;
        if (rightHand != null && rightHand.gameObject.activeSelf != visible)
        {
            rightHand.gameObject.SetActive(visible);
        }

        Transform core = CoreCarryAnchor;
        if (core != null && core != rightHand && core.gameObject.activeSelf != visible)
        {
            core.gameObject.SetActive(visible);
        }
    }

    private void ResolveAnimator()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }
    }

    private Transform EnsureFallbackAnchor()
    {
        if (_runtimeFallbackAnchor != null)
        {
            return _runtimeFallbackAnchor;
        }

        Transform existing = transform.Find("Runtime_RightHandAnchor");
        if (existing != null)
        {
            _runtimeFallbackAnchor = existing;
            return _runtimeFallbackAnchor;
        }

        GameObject anchor = new GameObject("Runtime_RightHandAnchor");
        anchor.transform.SetParent(transform, false);
        anchor.transform.localPosition = fallbackLocalPosition;
        anchor.transform.localRotation = Quaternion.Euler(fallbackLocalEulerAngles);
        _runtimeFallbackAnchor = anchor.transform;
        return _runtimeFallbackAnchor;
    }

    private Transform EnsureCoreCarryAnchor()
    {
        if (_runtimeCoreCarryAnchor != null)
        {
            return _runtimeCoreCarryAnchor;
        }

        Transform existing = transform.Find("Runtime_CoreCarryAnchor");
        if (existing != null)
        {
            _runtimeCoreCarryAnchor = existing;
            return _runtimeCoreCarryAnchor;
        }

        GameObject anchor = new GameObject("Runtime_CoreCarryAnchor");
        anchor.transform.SetParent(ResolveCoreCarryParent(), false);
        anchor.transform.localPosition = coreCarryLocalPosition;
        anchor.transform.localRotation = Quaternion.Euler(coreCarryLocalEulerAngles);
        Transform parent = anchor.transform.parent;
        if (parent != null)
        {
            Vector3 lossy = parent.lossyScale;
            anchor.transform.localScale = new Vector3(
                Mathf.Approximately(lossy.x, 0f) ? 1f : 1f / lossy.x,
                Mathf.Approximately(lossy.y, 0f) ? 1f : 1f / lossy.y,
                Mathf.Approximately(lossy.z, 0f) ? 1f : 1f / lossy.z
            );
        }
        _runtimeCoreCarryAnchor = anchor.transform;
        return _runtimeCoreCarryAnchor;
    }

    private Transform ResolveCoreCarryParent()
    {
        ResolveAnimator();
        if (animator != null && animator.isHuman)
        {
            // Anchor to Chest so the core sits centered between both hands
            return animator.GetBoneTransform(HumanBodyBones.Chest)
                ?? animator.GetBoneTransform(HumanBodyBones.Spine)
                ?? animator.GetBoneTransform(HumanBodyBones.Hips)
                ?? transform;
        }

        return transform;
    }
}

