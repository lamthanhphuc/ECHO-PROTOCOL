using EchoProtocol.Networking;
using UnityEngine;

[DefaultExecutionOrder(150)]
public sealed class PlayerCrawlPoseCorrector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerDownState downState;
    [SerializeField] private NetworkPlayerLifeState networkLifeState;
    [SerializeField] private CharacterController characterController;

    [Header("Crawl Grounding")]
    [SerializeField] private bool correctWhileDowned = true;
    [SerializeField, Min(0f)] private float groundOffset = 0.02f;
    [SerializeField, Min(0.05f)] private float targetHipsHeight = 0.24f;
    [SerializeField, Min(0f)] private float handContactBias = 0f;
    [SerializeField, Min(0f)] private float rearFootLiftOffset = 0.03f;
    [SerializeField, Min(0f)] private float rearLowerLegLiftOffset = 0.05f;
    [SerializeField, Min(0f)] private float maximumLift = 0.6f;
    [SerializeField, Min(0.01f)] private float blendSpeed = 12f;

    private Transform _hips;
    private Transform _spine;
    private Transform _head;
    private Transform _leftHand;
    private Transform _rightHand;
    private Transform _leftFoot;
    private Transform _rightFoot;
    private Transform _leftLowerLeg;
    private Transform _rightLowerLeg;
    private float _weight;

    private void Awake()
    {
        ResolveReferences();
    }

    private void LateUpdate()
    {
        ResolveReferences();

        bool shouldCorrect = correctWhileDowned && IsDowned() && animator != null && animator.isHuman;
        float targetWeight = shouldCorrect ? 1f : 0f;
        _weight = Mathf.MoveTowards(_weight, targetWeight, blendSpeed * Time.deltaTime);
        if (_weight <= 0.001f)
        {
            return;
        }

        ResolveBones();
        if (_hips == null)
        {
            return;
        }

        float floorY = ResolveFloorY();

        // 1. Find lowest contact point among available limbs
        float lowestContactY = GetLowestContactY();

        // 2. Determine how much the model is floating above the floor
        float targetContactFloorY = floorY + groundOffset + handContactBias;
        float floatDistance = lowestContactY - targetContactFloorY;

        // 3. Lower hips so the body lies close to the floor ("nằm sát sàn")
        float currentHipsY = _hips.position.y;
        float groundedHipsY = currentHipsY - Mathf.Max(0f, floatDistance);
        float maxAllowedHipsY = floorY + targetHipsHeight;
        float finalTargetHipsY = Mathf.Min(groundedHipsY, maxAllowedHipsY);

        Vector3 hipsPosition = _hips.position;
        hipsPosition.y = Mathf.Lerp(currentHipsY, finalTargetHipsY, _weight);
        _hips.position = hipsPosition;

        // 4. Ensure hands, knees, and feet rest cleanly on floor without clipping through
        float handMinY = floorY + groundOffset;
        float lowerLegMinY = floorY + rearLowerLegLiftOffset;
        float footMinY = floorY + rearFootLiftOffset;
        float torsoMinY = floorY + 0.10f;

        LiftBoneToMinimumY(_leftHand, handMinY);
        LiftBoneToMinimumY(_rightHand, handMinY);
        LiftBoneToMinimumY(_leftLowerLeg, lowerLegMinY);
        LiftBoneToMinimumY(_rightLowerLeg, lowerLegMinY);
        LiftBoneToMinimumY(_leftFoot, footMinY);
        LiftBoneToMinimumY(_rightFoot, footMinY);
        LiftBoneToMinimumY(_hips, torsoMinY);
        LiftBoneToMinimumY(_spine, torsoMinY);
        LiftBoneToMinimumY(_head, torsoMinY);
    }

    private void ResolveReferences()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (downState == null)
        {
            downState = GetComponent<PlayerDownState>() ?? GetComponentInParent<PlayerDownState>();
        }

        if (networkLifeState == null)
        {
            networkLifeState = GetComponent<NetworkPlayerLifeState>() ?? GetComponentInParent<NetworkPlayerLifeState>();
        }

        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>() ?? GetComponentInParent<CharacterController>();
        }
    }

    private void ResolveBones()
    {
        if (animator == null || !animator.isHuman)
        {
            return;
        }

        _hips ??= animator.GetBoneTransform(HumanBodyBones.Hips);
        _spine ??= animator.GetBoneTransform(HumanBodyBones.Spine);
        _head ??= animator.GetBoneTransform(HumanBodyBones.Head);
        _leftHand ??= animator.GetBoneTransform(HumanBodyBones.LeftHand);
        _rightHand ??= animator.GetBoneTransform(HumanBodyBones.RightHand);
        _leftFoot ??= animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        _rightFoot ??= animator.GetBoneTransform(HumanBodyBones.RightFoot);
        _leftLowerLeg ??= animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        _rightLowerLeg ??= animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
    }

    private bool IsDowned()
    {
        if (networkLifeState != null
            && networkLifeState.Object != null
            && networkLifeState.Object.IsValid)
        {
            return networkLifeState.IsDowned;
        }

        return downState != null && downState.IsDowned;
    }

    private float ResolveFloorY()
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        int layerMask = ~LayerMask.GetMask("Ignore Raycast");

        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 2.5f, layerMask, QueryTriggerInteraction.Ignore);
        float highestFloor = float.MinValue;
        bool found = false;

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit.collider != null && !hit.collider.transform.IsChildOf(transform))
            {
                if (hit.point.y > highestFloor)
                {
                    highestFloor = hit.point.y;
                    found = true;
                }
            }
        }

        if (found)
        {
            return highestFloor;
        }

        if (characterController != null)
        {
            return transform.position.y + characterController.center.y - (characterController.height * 0.5f);
        }

        return transform.position.y - 1.0f;
    }

    private float GetLowestContactY()
    {
        float lowest = float.MaxValue;
        bool hasSample = false;

        void Sample(Transform bone)
        {
            if (bone != null)
            {
                lowest = Mathf.Min(lowest, bone.position.y);
                hasSample = true;
            }
        }

        Sample(_leftHand);
        Sample(_rightHand);
        Sample(_leftLowerLeg);
        Sample(_rightLowerLeg);
        Sample(_leftFoot);
        Sample(_rightFoot);

        return hasSample ? lowest : (_hips != null ? _hips.position.y : transform.position.y);
    }

    private void LiftBoneToMinimumY(Transform bone, float targetY)
    {
        if (bone == null)
        {
            return;
        }

        Vector3 position = bone.position;
        if (position.y >= targetY)
        {
            return;
        }

        float liftedY = Mathf.Min(targetY, position.y + maximumLift);
        position.y = Mathf.Lerp(position.y, liftedY, _weight);
        bone.position = position;
    }
}

