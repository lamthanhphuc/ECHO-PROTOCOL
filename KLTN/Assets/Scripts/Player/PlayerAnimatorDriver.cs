using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class PlayerAnimatorDriver : MonoBehaviour
{
    private const string EditorAnimatorControllerPath = "Assets/Animations/Player/AC_PlayerCharacter.controller";

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsSprintingHash = Animator.StringToHash("IsSprinting");
    private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
    private static readonly int IsCarryingHash = Animator.StringToHash("IsCarrying");
    private static readonly int IsDownedHash = Animator.StringToHash("IsDowned");
    private static readonly int IsRevivingHash = Animator.StringToHash("IsReviving");
    private static readonly int ReviveHash = Animator.StringToHash("Revive");

    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private PlayerEnergyCoreCarrier coreCarrier;
    [SerializeField] private PlayerDownState downState;
    [SerializeField] private float walkSpeedReference = 4f;
    [SerializeField] private float runSpeedReference = 7f;
    [SerializeField] private float speedDampTime = 0.16f;
    [SerializeField] private float directionDampTime = 0.12f;

    private float _smoothedSpeed;

    private void Awake()
    {
        animator = ResolvePlayableAnimator();
        movement = movement != null ? movement : GetComponentInParent<PlayerMovement>();
        characterController = characterController != null ? characterController : GetComponentInParent<CharacterController>();
        coreCarrier = coreCarrier != null ? coreCarrier : GetComponentInParent<PlayerEnergyCoreCarrier>();
        downState = downState != null ? downState : GetComponentInParent<PlayerDownState>();
    }

    private void Update()
    {
        if (animator == null)
        {
            animator = ResolvePlayableAnimator();
        }

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        if (movement == null) movement = GetComponent<PlayerMovement>() ?? GetComponentInParent<PlayerMovement>();
        if (coreCarrier == null) coreCarrier = GetComponent<PlayerEnergyCoreCarrier>() ?? GetComponentInParent<PlayerEnergyCoreCarrier>();
        if (downState == null) downState = GetComponent<PlayerDownState>() ?? GetComponentInParent<PlayerDownState>();

        bool isCrouching = movement != null && movement.IsCrouching;
        bool isSprinting = movement != null
            ? movement.IsSprinting
            : (Keyboard.current != null
                && Keyboard.current.leftShiftKey.isPressed
                && Keyboard.current.wKey.isPressed
                && !Keyboard.current.aKey.isPressed
                && !Keyboard.current.dKey.isPressed
                && !Keyboard.current.sKey.isPressed);
        bool isCarrying = coreCarrier != null && coreCarrier.IsCarrying;
        bool isDowned = downState != null && downState.IsDowned;
        Vector2 moveDirection = GetMoveDirection(isSprinting, isCarrying, isDowned);
        bool isMoving = moveDirection.sqrMagnitude > 0.01f;
        float normalizedSpeed = isMoving ? GetNormalizedSpeed(isSprinting, isCrouching, isCarrying, isDowned) : 0f;

        _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, normalizedSpeed, 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.001f, speedDampTime)));
        animator.SetFloat(SpeedHash, _smoothedSpeed);
        animator.SetFloat(MoveXHash, moveDirection.x, directionDampTime, Time.deltaTime);
        animator.SetFloat(MoveYHash, moveDirection.y, directionDampTime, Time.deltaTime);
        animator.SetBool(IsMovingHash, isMoving);
        animator.SetBool(IsSprintingHash, isSprinting);
        animator.SetBool(IsCrouchingHash, isCrouching);
        animator.SetBool(IsCarryingHash, isCarrying);
        animator.SetBool(IsDownedHash, isDowned);
    }

    public void TriggerRevive()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            animator = ResolvePlayableAnimator();
        }

        animator?.SetTrigger(ReviveHash);
    }

    public void SetReviving(bool isReviving)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            animator = ResolvePlayableAnimator();
        }

        animator?.SetBool(IsRevivingHash, isReviving);
    }

    private Animator ResolvePlayableAnimator()
    {
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            return animator;
        }

        Animator[] animators = GetComponentsInChildren<Animator>(true);
        foreach (Animator candidate in animators)
        {
            if (candidate.runtimeAnimatorController != null)
            {
                return candidate;
            }
        }

        Animator fallback = animator != null ? animator : GetComponentInChildren<Animator>(true);
#if UNITY_EDITOR
        if (fallback != null && fallback.runtimeAnimatorController == null)
        {
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(EditorAnimatorControllerPath);
            if (controller != null)
            {
                fallback.runtimeAnimatorController = controller;
            }
        }
#endif
        return fallback != null && fallback.runtimeAnimatorController != null ? fallback : null;
    }

    private float GetNormalizedSpeed(bool isSprinting, bool isCrouching, bool isCarrying, bool isDowned)
    {
        if (isDowned || isCrouching)
        {
            return 1f;
        }

        if (isCarrying)
        {
            return isSprinting ? 1.35f : 0.85f;
        }

        return isSprinting ? Mathf.Clamp(runSpeedReference / Mathf.Max(0.001f, walkSpeedReference), 1f, 2f) : 1f;
    }

    private Vector2 GetMoveDirection(bool isSprinting, bool isCarrying, bool isDowned)
    {
        Vector2 input = movement != null ? movement.MoveInput : Vector2.zero;

        if (input.sqrMagnitude <= 0.01f && Keyboard.current != null)
        {
            float x = 0f;
            float y = 0f;
            if (Keyboard.current.wKey.isPressed) y += 1f;
            if (Keyboard.current.sKey.isPressed) y -= 1f;
            if (Keyboard.current.aKey.isPressed) x -= 1f;
            if (Keyboard.current.dKey.isPressed) x += 1f;
            input = new Vector2(x, y);
        }

        if (input.sqrMagnitude <= 0.01f)
        {
            return Vector2.zero;
        }

        input = input.sqrMagnitude > 1f ? input.normalized : input;

        if (isSprinting && !isCarrying && !isDowned)
        {
            return Vector2.up;
        }

        return input;
    }

}
