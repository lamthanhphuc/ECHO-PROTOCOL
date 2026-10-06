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

    [Header("Sprint Leg Anti-Cross")]
    [SerializeField, Min(0f)] private float sprintFootHalfSpacing = 0.09f;
    [SerializeField, Range(0f, 1f)] private float sprintLegCorrectionWeight = 0.85f;
    [SerializeField, Min(0f)] private float sprintKneePoleForwardOffset = 0.45f;
    [SerializeField, Min(0f)] private float sprintKneePoleSideOffset = 0.12f;

    [Header("Carry (Two-Hand) Pose")]
    [SerializeField] private float carryForwardOffset = 0.30f;
    [SerializeField] private float carryLateralOffset = 0.30f;
    [SerializeField] private float carryVerticalOffset = 0.08f;
    [SerializeField] private float carryChestHeight = 1.38f;
#pragma warning disable CS0414
    [SerializeField] private float carryMinHandY = 1.18f;
    [SerializeField] private float carryMaxHandY = 1.62f;
#pragma warning restore CS0414
    [SerializeField, Range(0f, 1f)] private float carryIKWeight = 0.88f;
    [SerializeField] private Vector3 rightElbowPoleOffset = new Vector3(0.40f, -0.25f, -0.10f);
    [SerializeField] private Vector3 leftElbowPoleOffset = new Vector3(-0.40f, -0.25f, -0.10f);
    [SerializeField] private Vector3 coreHandOffset = new Vector3(0f, 0f, 0.2f);

    [Header("Held Tool Right Hand Pose")]
    [SerializeField] private Vector3 handForwardOffset = new Vector3(0.20f, 0.12f, 0.36f);
#pragma warning disable CS0414
    [SerializeField] private Vector3 handEulerOffset = new Vector3(0f, 0f, -75f);
    [SerializeField, Range(0f, 1f)] private float rightHandRotWeight = 0.80f;
#pragma warning restore CS0414
    [SerializeField, Range(0f, 1f)] private float rightHandPosWeight = 0.82f;

    private static readonly int IsRevivingHash = Animator.StringToHash("IsReviving");
    private static readonly int IsDownedHash = Animator.StringToHash("IsDowned");
    private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
    private static readonly int IsPushingHash = Animator.StringToHash("IsPushing");
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsSprintingHash = Animator.StringToHash("IsSprinting");

    [Header("Jammo Grip Clearance")]
    [SerializeField, Min(0f)] private float jammoGripHeightOffset = 0.32f;
    [SerializeField, Min(0f)] private float jammoGripForwardClearance = 0.04f;
    [SerializeField, Min(0f)] private float jammoCarryLateralClearance = 0.04f;
    [SerializeField, Min(0f)] private float jammoToolLateralClearance = 0.035f;

    private PlayerInventory _inventory;
    private PlayerEnergyCoreCarrier _coreCarrier;
    private LobbyPlayerState _lobbyState;
    private NetworkPlayerMovement _networkMovement;
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
        _networkMovement = GetComponentInParent<NetworkPlayerMovement>();
        _heldItemAnchor = GetComponentInParent<PlayerHeldItemAnchor>();
    }

    private void LateUpdate()
    {
        if (playerRoot == null) playerRoot = ResolvePlayerRoot();
        if (!_hasExternalAim && (playerCamera == null || !IsCameraTargetForThisPlayer(playerCamera.Target)))
        {
            playerCamera = FindBoundCamera();
        }

        ApplyArmPosingInLateUpdate();
        ApplySprintLegSeparation();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (IsReviving() || IsDowned() || IsPushing())
        {
            if (animator != null && animator.isHuman) animator.SetLookAtWeight(0f);
            return;
        }
        if (animator == null || (!TryGetAim(out Vector3 aimOrigin, out Vector3 aimForward, out Vector3 aimUp)))
        {
            return;
        }

        // Native crash prevention:
        // Unity's native Humanoid IK solver crashes with 0xC0000005 (access violation at 0xFFFFFFFF00000000)
        // if SetLookAt or SetIKPosition are called when the avatar is not humanoid or has no valid bone mapping.
        if (!animator.isHuman || animator.avatar == null || !animator.avatar.isValid)
        {
            return;
        }

        Vector3 lookAt = aimOrigin + aimForward * lookAtDistance;

        animator.SetLookAtWeight(lookAtWeight, bodyWeight, headWeight, eyesWeight, clampWeight);
        animator.SetLookAtPosition(lookAt);

        // Note: We deliberately do NOT call animator.SetIKPosition or animator.SetIKRotation for AvatarIKGoal
        // because Unity's native Humanoid limb IK solver crashes with Access Violation (0xC0000005)
        // on this model's rig. Hand and arm posing is handled safely via Transform rotation in LateUpdate().
    }

    private void ApplyArmPosingInLateUpdate()
    {
        if (!driveRightHandWhenHolding || animator == null || !animator.isHuman)
        {
            return;
        }

        if (IsReviving() || IsDowned() || IsPushing())
        {
            return;
        }

        if (!TryGetAim(out Vector3 aimOrigin, out Vector3 aimForward, out Vector3 aimUp))
        {
            return;
        }

        Transform rightHandBone = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform leftHandBone = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform rightForeArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        Transform leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Transform leftForeArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);

        Transform chestBone = animator.GetBoneTransform(HumanBodyBones.Chest);
        Vector3 chestOrigin = chestBone != null ? chestBone.position : (transform.position + Vector3.up * carryChestHeight);
        Vector3 aimRight = Vector3.Cross(aimUp, aimForward).normalized;
        if (aimRight.sqrMagnitude <= 0.001f)
        {
            aimRight = transform.right;
        }

        var character = GetComponentInParent<PlayerCharacterPresenter>();
        bool jammo = character != null && character.IsJammo;
        bool isCarryingCore = IsCarryingCore();
        bool isHoldingCoreStabilizer = IsHoldingCoreStabilizer();
        if (isCarryingCore || isHoldingCoreStabilizer)
        {
            float twoHandSpread = jammo && (isHoldingCoreStabilizer || isCarryingCore) ? 0.13f : 0f;
            Vector3 rightHandPos = chestOrigin + aimForward * (carryForwardOffset + (jammo ? jammoGripForwardClearance : 0f)) + aimRight * (carryLateralOffset + (jammo ? jammoCarryLateralClearance : 0f) + twoHandSpread) + aimUp * (carryVerticalOffset + (jammo ? jammoGripHeightOffset : 0f));
            Vector3 leftHandPos  = chestOrigin + aimForward * (carryForwardOffset + (jammo ? jammoGripForwardClearance : 0f)) - aimRight * (carryLateralOffset + (jammo ? jammoCarryLateralClearance : 0f) + twoHandSpread) + aimUp * (carryVerticalOffset + (jammo ? jammoGripHeightOffset : 0f));

            float minCarryY = chestOrigin.y - 0.25f;
            float maxCarryY = chestOrigin.y + (jammo ? 0.50f : 0.35f);
            rightHandPos.y = Mathf.Clamp(rightHandPos.y, minCarryY, maxCarryY);
            leftHandPos.y  = Mathf.Clamp(leftHandPos.y,  minCarryY, maxCarryY);

            Vector3 rightArmRoot = rightUpperArm != null ? rightUpperArm.position : chestOrigin;
            Vector3 leftArmRoot  = leftUpperArm  != null ? leftUpperArm.position  : chestOrigin;
            Vector3 rightElbowPole = rightArmRoot + aimRight * rightElbowPoleOffset.x + aimUp * rightElbowPoleOffset.y - aimForward * Mathf.Abs(rightElbowPoleOffset.z);
            Vector3 leftElbowPole  = leftArmRoot  + aimRight * leftElbowPoleOffset.x  + aimUp * leftElbowPoleOffset.y  - aimForward * Mathf.Abs(leftElbowPoleOffset.z);

            float twoHandIKWeight = isHoldingCoreStabilizer ? 1f : carryIKWeight;
            SolveTwoBoneIK(rightUpperArm, rightForeArm, rightHandBone, rightHandPos, rightElbowPole, twoHandIKWeight);
            SolveTwoBoneIK(leftUpperArm,  leftForeArm,  leftHandBone,  leftHandPos,  leftElbowPole,  twoHandIKWeight);

            character?.ApplyJammoGrip(rightHandBone, false, aimForward, aimUp);
            character?.ApplyJammoGrip(leftHandBone, true, aimForward, aimUp);
            UpdateCoreAnchorPose(aimForward, aimUp, aimRight, rightHandBone, leftHandBone, rightHandPos, leftHandPos);
            return;
        }

        if (animator.GetBool(IsCrouchingHash) && !jammo)
        {
            return;
        }

        if (IsHoldingTeamTool())
        {
            if (rightHandBone == null || rightUpperArm == null || rightForeArm == null)
            {
                return;
            }

            Vector3 handTarget = chestOrigin
                + aimForward * (handForwardOffset.z + (jammo ? jammoGripForwardClearance : 0f))
                + aimRight * (handForwardOffset.x + (jammo ? jammoToolLateralClearance : 0f))
                + aimUp * (handForwardOffset.y + (jammo ? jammoGripHeightOffset : 0f));

            float maxHandY = jammo ? chestOrigin.y + 0.55f : transform.position.y + 1.60f;
            float minHandY = jammo ? chestOrigin.y - 0.10f : transform.position.y + 1.05f;
            handTarget.y = Mathf.Clamp(handTarget.y, minHandY, maxHandY);

            Vector3 rightElbowPole = rightUpperArm.position + aimRight * 0.35f - aimUp * 0.25f - aimForward * 0.10f;

            SolveTwoBoneIK(rightUpperArm, rightForeArm, rightHandBone, handTarget, rightElbowPole, rightHandPosWeight);
            character?.ApplyJammoGrip(rightHandBone, false, aimForward, aimUp);
        }
    }

    private void ApplySprintLegSeparation()
    {
        if (animator == null
            || !animator.isHuman
            || animator.avatar == null
            || !animator.avatar.isValid)
        {
            return;
        }

        if (IsReviving()
            || IsDowned()
            || IsPushing()
            || animator.GetBool(IsCrouchingHash)
            || !animator.GetBool(IsMovingHash)
            || !animator.GetBool(IsSprintingHash))
        {
            return;
        }

        Transform root = playerRoot != null ? playerRoot : transform;

        Transform leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        Transform leftLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);

        Transform rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        Transform rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);

        if (leftUpper == null || leftLower == null || leftFoot == null
            || rightUpper == null || rightLower == null || rightFoot == null)
        {
            return;
        }

        Vector3 leftLocal = root.InverseTransformPoint(leftFoot.position);
        Vector3 rightLocal = root.InverseTransformPoint(rightFoot.position);

        bool fixLeft = leftLocal.x > -sprintFootHalfSpacing;
        bool fixRight = rightLocal.x < sprintFootHalfSpacing;

        if (!fixLeft && !fixRight)
        {
            return;
        }

        if (fixLeft)
        {
            leftLocal.x = -sprintFootHalfSpacing;

            Vector3 leftTarget = root.TransformPoint(leftLocal);
            Vector3 leftPole =
                leftUpper.position
                + root.forward * sprintKneePoleForwardOffset
                - root.right * sprintKneePoleSideOffset;

            SolveTwoBoneIK(
                leftUpper,
                leftLower,
                leftFoot,
                leftTarget,
                leftPole,
                sprintLegCorrectionWeight);
        }

        if (fixRight)
        {
            rightLocal.x = sprintFootHalfSpacing;

            Vector3 rightTarget = root.TransformPoint(rightLocal);
            Vector3 rightPole =
                rightUpper.position
                + root.forward * sprintKneePoleForwardOffset
                + root.right * sprintKneePoleSideOffset;

            SolveTwoBoneIK(
                rightUpper,
                rightLower,
                rightFoot,
                rightTarget,
                rightPole,
                sprintLegCorrectionWeight);
        }
    }

    private static void SolveTwoBoneIK(
        Transform upperArm,
        Transform foreArm,
        Transform hand,
        Vector3 targetPos,
        Vector3 poleTarget,
        float weight)
    {
        if (upperArm == null || foreArm == null || hand == null || weight <= 0.001f)
        {
            return;
        }

        Vector3 aPos = upperArm.position;
        Vector3 bPos = foreArm.position;
        Vector3 cPos = hand.position;

        float lab = Vector3.Distance(aPos, bPos);
        float lbc = Vector3.Distance(bPos, cPos);
        if (lab < 0.01f || lbc < 0.01f)
        {
            return;
        }

        Vector3 toTarget = targetPos - aPos;
        float lat = toTarget.magnitude;
        if (lat < 0.001f)
        {
            return;
        }

        // Clamp reach to avoid impossible triangles or math domain errors
        float maxReach = (lab + lbc) * 0.999f;
        float minReach = Mathf.Max(0.01f, Mathf.Abs(lab - lbc) + 0.001f);
        float latClamped = Mathf.Clamp(lat, minReach, maxReach);

        // Law of cosines for angle at shoulder (upperArm)
        float cosA = (lab * lab + latClamped * latClamped - lbc * lbc) / (2f * lab * latClamped);
        cosA = Mathf.Clamp(cosA, -1f, 1f);
        float angleA = Mathf.Acos(cosA);

        // Direction to target and elbow pole vector
        Vector3 vAt = toTarget / lat;
        Vector3 vAp = poleTarget - aPos;

        // Normal vector defining the bend plane
        Vector3 planeNormal = Vector3.Cross(vAt, vAp);
        if (planeNormal.sqrMagnitude < 0.0001f)
        {
            planeNormal = Vector3.up;
        }
        else
        {
            planeNormal.Normalize();
        }

        // Direction from shoulder to solved elbow position (Rodrigues' rotation formula)
        float cosVal = Mathf.Cos(angleA);
        float sinVal = Mathf.Sin(angleA);
        Vector3 dirToElbow = vAt * cosVal + Vector3.Cross(planeNormal, vAt) * sinVal + planeNormal * Vector3.Dot(planeNormal, vAt) * (1f - cosVal);
        dirToElbow.Normalize();

        Vector3 solvedElbowPos = aPos + dirToElbow * lab;

        // 1. Rotate upperArm so foreArm moves towards solvedElbowPos
        Vector3 curUpperArmDir = foreArm.position - upperArm.position;
        Vector3 targetUpperArmDir = solvedElbowPos - upperArm.position;
        if (curUpperArmDir.sqrMagnitude > 0.0001f && targetUpperArmDir.sqrMagnitude > 0.0001f)
        {
            Quaternion deltaUpper = Quaternion.FromToRotation(curUpperArmDir.normalized, targetUpperArmDir.normalized);
            upperArm.rotation = Quaternion.Slerp(upperArm.rotation, deltaUpper * upperArm.rotation, Mathf.Clamp01(weight));
        }

        // 2. Rotate foreArm so hand moves towards targetPos
        Vector3 curForeArmDir = hand.position - foreArm.position;
        Vector3 targetForeArmDir = targetPos - foreArm.position;
        if (curForeArmDir.sqrMagnitude > 0.0001f && targetForeArmDir.sqrMagnitude > 0.0001f)
        {
            Quaternion deltaFore = Quaternion.FromToRotation(curForeArmDir.normalized, targetForeArmDir.normalized);
            foreArm.rotation = Quaternion.Slerp(foreArm.rotation, deltaFore * foreArm.rotation, Mathf.Clamp01(weight));
        }

        // Note: We deliberately do NOT override hand.rotation.
        // Hand is a child of foreArm, so it smoothly follows the forearm naturally.
        // Overriding hand.rotation with world Euler offsets causes wrist twisting / dislocation.
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

    private bool IsReviving()
    {
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
        {
            return false;
        }

        return animator.GetBool(IsRevivingHash) || animator.GetCurrentAnimatorStateInfo(0).IsName("Reviving");
    }

    private bool IsDowned()
    {
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
        {
            return false;
        }

        return animator.GetBool(IsDownedHash) || animator.GetCurrentAnimatorStateInfo(0).IsName("Downed Crawl");
    }

    private bool IsHoldingTeamTool()
    {
        if (IsCarryingCore() || IsPushing())
        {
            return false;
        }

        return (_inventory != null && _inventory.TeamToolSlot != null)
            || (_lobbyState != null && _lobbyState.Object != null && _lobbyState.Object.IsValid && _lobbyState.ToolId != 0);
    }

    private bool IsHoldingCoreStabilizer()
    {
        if (_lobbyState != null
            && _lobbyState.Object != null
            && _lobbyState.Object.IsValid
            && _lobbyState.ToolId == LobbyPlayerState.CoreStabilizerToolId)
        {
            return true;
        }

        InventoryItemDefinition tool = _inventory != null ? _inventory.TeamToolSlot : null;
        return tool != null && tool.ItemId == "core_stabilizer";
    }

    private bool IsCarryingCore()
    {
        return (_coreCarrier != null && _coreCarrier.IsCarrying)
            || (_lobbyState != null && _lobbyState.Object != null && _lobbyState.Object.IsValid && _lobbyState.CarriedCoreId.IsValid);
    }

    private bool IsPushing()
    {
        if (_networkMovement == null) _networkMovement = GetComponentInParent<NetworkPlayerMovement>();
        if (_networkMovement != null && _networkMovement.IsAnimationPushing)
        {
            return true;
        }

        return animator != null
            && animator.isActiveAndEnabled
            && animator.runtimeAnimatorController != null
            && animator.GetBool(IsPushingHash);
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

    private void UpdateCoreAnchorPose(
        Vector3 aimForward,
        Vector3 aimUp,
        Vector3 aimRight,
        Transform rightHandBone,
        Transform leftHandBone,
        Vector3 rightHandTarget,
        Vector3 leftHandTarget)
    {
        if (_heldItemAnchor == null)
        {
            _heldItemAnchor = GetComponentInParent<PlayerHeldItemAnchor>()
                ?? (playerRoot != null ? playerRoot.GetComponentInChildren<PlayerHeldItemAnchor>(true) : null);
        }

        if (_heldItemAnchor == null) return;

        Transform coreAnchor = _heldItemAnchor.CoreCarryAnchor;
        if (coreAnchor == null) return;

        Vector3 rHand = rightHandBone != null ? rightHandBone.position : rightHandTarget;
        Vector3 lHand = leftHandBone != null ? leftHandBone.position : leftHandTarget;
        Vector3 handsMid = (rHand + lHand) * 0.5f;

        Vector3 corePos = handsMid
            + aimForward * coreHandOffset.z
            + aimUp * coreHandOffset.y
            + aimRight * coreHandOffset.x;

        Quaternion coreRot = Quaternion.LookRotation(aimForward, aimUp);
        coreAnchor.SetPositionAndRotation(corePos, coreRot);
    }
}

