using EchoProtocol.Networking;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class PlayerUpperBodyAim : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Transform playerRoot;
    [SerializeField] private PlayerCamera playerCamera;
    [SerializeField] private bool driveRightHandWhenHolding = true;

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

    private Vector2 _lobbyLookAngles, _lobbyLookVelocity, _lastLobbyLookSent;
    private float _nextLobbyLookSend;
    private Transform _lobbyHead;
    private Quaternion _lobbyAppliedRotation, _lobbyHeadResult;
    private bool _lobbyRotationApplied;
    private bool IsLobbyPresentation => _lobbyState != null && _lobbyState.Object!=null && _lobbyState.Object.IsValid && !_lobbyState.IsGameplayPlayer
        && SceneManager.GetActiveScene().name == "Lobby";

    private void UpdateLobbyLook()
    {
        if (animator == null || !animator.isHuman || animator.avatar == null || !animator.avatar.isValid) return;
        var head=animator.GetBoneTransform(HumanBodyBones.Head);
        if(head==null)return;
        // Strip the previous offset when the Animator was culled and did not write a fresh pose.
        if(_lobbyRotationApplied && _lobbyHead==head && Quaternion.Angle(head.rotation,_lobbyHeadResult)<0.01f)
            head.rotation=Quaternion.Inverse(_lobbyAppliedRotation)*head.rotation;
        var camera=Camera.main;
        bool local=_lobbyState.Object.HasInputAuthority;
        Vector2 target=local ? Vector2.zero : _lobbyState.LobbyLookAngles;
        if(local && camera!=null && Mouse.current!=null && Application.isFocused && Screen.width>0 && Screen.height>0) {
            var mouse=Mouse.current.position.ReadValue();
            if(mouse.x>=0 && mouse.x<=Screen.width && mouse.y>=0 && mouse.y<=Screen.height) {
                var headScreen=camera.WorldToScreenPoint(head.position);
                target=new Vector2(Mathf.Clamp((mouse.x-headScreen.x)/(Screen.width*0.35f),-1f,1f)*28f,
                    Mathf.Clamp((mouse.y-headScreen.y)/(Screen.height*0.35f),-1f,1f)*16f);
            }
        }
        if(local && Time.unscaledTime>=_nextLobbyLookSend && (target-_lastLobbyLookSent).sqrMagnitude>0.04f) {
            _nextLobbyLookSend=Time.unscaledTime+0.05f;
            _lastLobbyLookSent=target;_lobbyState.RequestLobbyLook(target);
        }
        _lobbyLookAngles=Vector2.SmoothDamp(_lobbyLookAngles,target,ref _lobbyLookVelocity,0.16f,Mathf.Infinity,Time.unscaledDeltaTime);
        var up=playerRoot!=null ? playerRoot.up : Vector3.up;
        var right=camera!=null ? camera.transform.right : Vector3.right;
        // Camera screen-right is opposite the character's facing-right in the lobby.
        _lobbyAppliedRotation=Quaternion.AngleAxis(-_lobbyLookAngles.x,up)*Quaternion.AngleAxis(_lobbyLookAngles.y,right);
        head.rotation=_lobbyAppliedRotation*head.rotation;
        _lobbyHead=head;_lobbyHeadResult=head.rotation;_lobbyRotationApplied=true;
    }

    private void LateUpdate()
    {
        if (playerRoot == null) playerRoot = ResolvePlayerRoot();
        if (IsLobbyPresentation) { UpdateLobbyLook(); return; }
        if (_lobbyRotationApplied && _lobbyHead!=null && Quaternion.Angle(_lobbyHead.rotation,_lobbyHeadResult)<0.01f)
            _lobbyHead.rotation=Quaternion.Inverse(_lobbyAppliedRotation)*_lobbyHead.rotation;
        _lobbyRotationApplied=false;_lobbyLookAngles=Vector2.zero;_lobbyLookVelocity=Vector2.zero;

        if (!_hasExternalAim && (playerCamera == null || !IsCameraTargetForThisPlayer(playerCamera.Target)))
        {
            playerCamera = FindBoundCamera();
        }

        ApplyArmPosingInLateUpdate();
        CorrectFootRoll();
    }

    private Avatar _soleAvatar;
    private Quaternion _leftSole, _rightSole;
    private float _footCorrectionWeight;
    private void CorrectFootRoll()
    {
        if (animator == null || !animator.isHuman || animator.avatar == null || !animator.avatar.isValid) return;
        // Crouch keeps its clip-authored feet, including transitions into crouch.
        bool crouching = animator.GetBool(IsCrouchingHash)
            || animator.GetCurrentAnimatorStateInfo(0).IsName("Crouch Locomotion")
            || animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("Crouch Locomotion");
        if (crouching) { _footCorrectionWeight = 0f; return; }
        // Keep correction through normal locomotion, carry and their idle transitions.
        bool active = !IsDowned() && !IsReviving() && !IsPushing();
        _footCorrectionWeight = Mathf.MoveTowards(_footCorrectionWeight, active ? 1f : 0f, Time.deltaTime / 0.12f);
        if (_footCorrectionWeight <= 0f) return;
        var left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        var right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        if (left == null || right == null) return;
        if (_soleAvatar != animator.avatar)
        {
            _soleAvatar = animator.avatar;
            _leftSole = NeutralFootBasis(left, animator.GetBoneTransform(HumanBodyBones.LeftToes));
            _rightSole = NeutralFootBasis(right, animator.GetBoneTransform(HumanBodyBones.RightToes));
        }
        BoundFootRoll(left, _leftSole);
        BoundFootRoll(right, _rightSole);
    }
    private Quaternion NeutralFootBasis(Transform foot, Transform toes)
    {
        var skeleton = animator.avatar.humanDescription.skeleton;
        Quaternion rotation = Quaternion.identity;
        for (var bone = foot; bone != null && bone != animator.transform; bone = bone.parent)
        {
            int index = System.Array.FindIndex(skeleton, entry => entry.name == bone.name);
            if (index >= 0) rotation = skeleton[index].rotation * rotation;
        }
        // Calibrate each foot independently from its own toe direction; bone axes differ by rig/side.
        Vector3 forward = Vector3.forward;
        if (toes != null)
        {
            Vector3 localToe = foot.InverseTransformPoint(toes.position);
            Vector3 neutralToe = Vector3.ProjectOnPlane(rotation * localToe, Vector3.up);
            if (neutralToe.sqrMagnitude > 0.000001f) forward = neutralToe.normalized;
        }
        return Quaternion.Inverse(rotation) * Quaternion.LookRotation(forward, Vector3.up);
    }
    private void BoundFootRoll(Transform foot, Quaternion basis)
    {
        Vector3 up = playerRoot != null ? playerRoot.up : Vector3.up;
        Quaternion sole = foot.rotation * basis;
        Vector3 forward = sole * Vector3.forward;
        Vector3 heading = Vector3.ProjectOnPlane(forward, up);
        if (heading.sqrMagnitude > 0.001f)
        {
            Vector3 facing = playerRoot != null ? playerRoot.forward : transform.forward;
            float yaw = Vector3.SignedAngle(facing, heading, up);
            float excessYaw = yaw - Mathf.Clamp(yaw, -35f, 35f);
            sole = Quaternion.AngleAxis(-excessYaw * _footCorrectionWeight, up) * sole;
            forward = sole * Vector3.forward;
        }
        Vector3 levelUp = Vector3.ProjectOnPlane(up, forward);
        if (levelUp.sqrMagnitude < 0.001f) return;
        float correction = Vector3.SignedAngle(sole * Vector3.up, levelUp, forward);
        // Remove sideways roll completely around the toe axis. Pitch and heading remain clip-authored.
        foot.rotation = Quaternion.AngleAxis(correction * _footCorrectionWeight, forward) * sole * Quaternion.Inverse(basis);
    }

    private float _gameplayHeadWeight;
    private PlayerCharacterPresenter _headCharacter;
    private void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || animator == null || !animator.isHuman
            || animator.avatar == null || !animator.avatar.isValid) return;
        if (_headCharacter == null) _headCharacter = GetComponentInParent<PlayerCharacterPresenter>();
        bool follow = !IsLobbyPresentation && _headCharacter != null && _headCharacter.IsJammo
            && !IsDowned() && !IsReviving() && !IsPushing();
        _gameplayHeadWeight = Mathf.MoveTowards(_gameplayHeadWeight, follow ? 0.85f : 0f, Time.deltaTime / 0.12f);
        if (IsLobbyPresentation) _gameplayHeadWeight = 0f;
        // Head only: body weight stays zero so locomotion and held-item arms are untouched.
        animator.SetLookAtWeight(_gameplayHeadWeight, 0f, 1f, 0f, 0.6f);
        if (_gameplayHeadWeight > 0f && TryGetAim(out _, out var forward, out _))
        {
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null) animator.SetLookAtPosition(head.position + forward * 8f);
        }
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
        return EchoProtocol.MatchFlow.Zone3FuelCell.FindCarried(gameObject) != null
            || (_coreCarrier != null && _coreCarrier.IsCarrying)
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

