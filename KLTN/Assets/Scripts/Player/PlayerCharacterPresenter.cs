using System.Collections.Generic;
using EchoProtocol.Networking;
using UnityEngine;

/// <summary>Swaps the visual rig on the existing Animator, preserving all gameplay component references.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(-50)]
public sealed class PlayerCharacterPresenter : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField, Range(0, 1)] private int offlineCharacterId;
    private LobbyPlayerState _state;
    private Avatar _defaultAvatar;
    private RuntimeAnimatorController _defaultController;
    private Vector3 _defaultScale;
    private float _defaultVisualHeight;
    private Quaternion _defaultRightPalm = Quaternion.identity;
    private Quaternion _defaultLeftPalm = Quaternion.identity;
    private Quaternion _lobbyBaseRotation;
    private bool _facingLobbyCamera;
    private Quaternion _jammoRightPalm = Quaternion.identity;
    private Quaternion _jammoLeftPalm = Quaternion.identity;
    [SerializeField, Min(0.1f)] private float jammoHeldItemScale = 1.4f;
    [SerializeField, Min(0f)] private float jammoHeldItemLift = 0.03f;
    public Vector3 HeldItemWorldOffset => IsJammo ? transform.up * jammoHeldItemLift : Vector3.zero;
    public float HeldItemScale => (_state != null && _state.Object != null && _state.Object.IsValid
        ? _state.CharacterId == 1 : IsJammo) ? jammoHeldItemScale : 1f;
    public float ToolHeldScale(int toolId) => HeldItemScale * (IsJammo && toolId == 2 ? 1.15f : 1f);
    private Vector3 _gripAimForward;
    public Vector3 ToolHeldOffset(int toolId)
    {
        float extra = toolId == 2 ? -0.03f : toolId == 1 ? 0.09f : toolId == 3 ? 0.03f : 0f;
        return HeldItemWorldOffset + (IsJammo ? transform.up * extra : Vector3.zero)
            + (IsJammo && (toolId == 1 || toolId == 6) ? (_gripAimForward.sqrMagnitude > 0.001f ? _gripAimForward : transform.forward) * (toolId == 6 ? 0.10f : 0.05f) : Vector3.zero);
    }
    private Quaternion _priestRightPalm, _priestLeftPalm;
    public bool IsPriest => _applied == 2;
    public bool IsJammo => _applied == 1;
    public Quaternion RightHandGripCorrection => IsJammo
        ? _jammoRightPalm * Quaternion.Inverse(_defaultRightPalm) : IsPriest ? _priestRightPalm * Quaternion.Inverse(_defaultRightPalm) : Quaternion.identity;

    private static Quaternion PalmBasis(Animator rig, bool left)
    {
        var hand = rig.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        var middle = rig.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
        var index = rig.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
        var little = rig.GetBoneTransform(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
        if (hand == null || middle == null || index == null || little == null) return Quaternion.identity;
        Vector3 forward = hand.InverseTransformDirection(middle.position - hand.position).normalized;
        Vector3 across = hand.InverseTransformDirection(index.position - little.position).normalized;
        Vector3 normal = Vector3.Cross(forward, across).normalized * (left ? -1f : 1f);
        return forward.sqrMagnitude < 0.01f || normal.sqrMagnitude < 0.01f
            ? Quaternion.identity : Quaternion.LookRotation(forward, normal);
    }

    public void ApplyJammoGrip(Transform hand, bool left, Vector3 forward, Vector3 up)
    {
        if (!IsJammo || hand == null) return;
        _gripAimForward = forward.normalized;
        Vector3 right = Vector3.Cross(up, forward).normalized;
        if (right.sqrMagnitude < 0.01f) return;
        // A neutral wrist faces the palm inward; finger direction follows the aim.
        hand.rotation = Quaternion.LookRotation(forward, left ? right : -right)
            * Quaternion.Inverse(left ? _jammoLeftPalm : _jammoRightPalm);
    }
    public void ApplyCarryGrip(Transform hand, bool left, Vector3 forward, Vector3 up)
    {
        if(IsJammo){ApplyJammoGrip(hand,left,forward,up);return;}
        if(hand==null)return;
        Vector3 right=Vector3.Cross(up,forward).normalized;
        if(right.sqrMagnitude<.01f)return;
        hand.rotation=Quaternion.LookRotation(forward,left ? right:-right)
            * Quaternion.Inverse(left ? (IsPriest ? _priestLeftPalm : _defaultLeftPalm) : (IsPriest ? _priestRightPalm : _defaultRightPalm));
    }
    public Vector3 FuelCellWristTarget(Transform hand, bool left, Vector3 handle, Vector3 forward, Vector3 up)
    {
        if (hand == null || animator == null) return handle;
        ApplyCarryGrip(hand, left, forward, up);
        var middle = animator.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
        // IK solves the wrist, while the handle belongs in the palm. Measure the
        // active rig in world space so the same correction works for both avatars.
        Vector3 palmOffset = middle != null ? (middle.position - hand.position) * .7f : Vector3.zero;
        Vector3 outward = Vector3.Cross(up, forward).normalized * (left ? -1f : 1f);
        // Keep the bulky glove outside the bar while the curled fingers wrap inward.
        return handle - palmOffset + outward * .075f;
    }
    public Vector3 FuelCellHandlePoint(Transform hand, bool left, Vector3 forward, Vector3 up)
    {
        var middle = animator != null ? animator.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal) : null;
        Vector3 palmOffset = middle != null ? (middle.position - hand.position) * .7f : Vector3.zero;
        Vector3 outward = Vector3.Cross(up, forward).normalized * (left ? -1f : 1f);
        return hand.position + palmOffset - outward * .075f;
    }
    private struct FingerPoseOverride
    {
        public Transform Bone;
        public Quaternion Before;
        public Quaternion Applied;
    }
    private readonly List<FingerPoseOverride> _fuelCellFingers = new List<FingerPoseOverride>(30);

    public void RestoreFuelCellFingerPose()
    {
        foreach (var pose in _fuelCellFingers)
        {
            // Do not undo a fresh pose that the Animator has already written.
            if (pose.Bone != null && Quaternion.Angle(pose.Bone.localRotation, pose.Applied) < .01f)
                pose.Bone.localRotation = pose.Before;
        }
        _fuelCellFingers.Clear();
    }

    public void ApplyFuelCellFingerPose(Vector3 forward, Vector3 up, bool rightHandOnly = false)
    {
        if (animator == null || !animator.isHuman) return;
        RestoreFuelCellFingerPose();
        Vector3 right = Vector3.Cross(up, forward).normalized;
        for (int side = rightHandOnly ? 1 : 0; side < 2; side++)
        {
            bool left = side == 0;
            Vector3 inward = left ? right : -right;
            Vector3 curlAxis = Vector3.Cross(forward, inward).normalized;
            // The avatar skeleton supplies each rig's neutral finger orientation;
            // world-space bend axes avoid assuming identical local axes on Jammo.
            int first = (int)(left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);
            for (int finger = 0; finger < 5; finger++)
            {
                for (int joint = 0; joint < 3; joint++)
                {
                    var bone = animator.GetBoneTransform((HumanBodyBones)(first + finger * 3 + joint));
                    if (bone == null) continue;
                    Quaternion before = bone.localRotation;
                    foreach (var neutral in animator.avatar.humanDescription.skeleton)
                        if (neutral.name == bone.name) { bone.localRotation = neutral.rotation; break; }
                    float angle = finger == 0 ? (joint == 0 ? 20f : 35f)
                        : joint == 0 ? 48f : joint == 1 ? 72f : 42f;
                    bone.rotation = Quaternion.AngleAxis(angle, curlAxis) * bone.rotation;
                    _fuelCellFingers.Add(new FingerPoseOverride { Bone = bone, Before = before, Applied = bone.localRotation });
                }
            }
        }
    }
    private readonly Dictionary<GameObject, bool> _defaultChildren = new Dictionary<GameObject, bool>();
    private readonly List<GameObject> _jammoChildren = new List<GameObject>();
    private GameObject _jammoRoot;
    private Avatar _jammoAvatar;
    private float _jammoScale = 1f;
    private RuntimeAnimatorController _jammoController;
    private readonly List<GameObject> _priestChildren = new List<GameObject>();
    private Avatar _priestAvatar;
    private RuntimeAnimatorController _priestController;
    private float _priestScale = 1f;
    private int _applied = -1;
    private int _appliedOutfit = -1;
    private MaterialPropertyBlock _teamTint;
    private readonly Material[] _jammoSkins = new Material[4];

    private void Awake() => InitializeVisualRig();
    public void InitializeVisualRig()
    {
        if (_defaultAvatar != null) return;
        _state = GetComponent<LobbyPlayerState>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (animator == null) return;
        _defaultAvatar = animator.avatar;
        _defaultController = animator.runtimeAnimatorController;
        _defaultScale = animator.transform.localScale;
        _defaultVisualHeight = MeasureVisualHeight(animator.gameObject);
        _defaultRightPalm = PalmBasis(animator, false);
        _defaultLeftPalm = PalmBasis(animator, true);
        foreach (Transform child in animator.transform) _defaultChildren.Add(child.gameObject, child.gameObject.activeSelf);
    }
    private void Start() => Refresh();
    private void LateUpdate()
    {
        Refresh();
        RefreshOutfit();
        FaceLobbyCamera();
    }
    private void FaceLobbyCamera()
    {
        if (animator == null || animator.transform == transform) return;
        bool inLobby = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Lobby";
        if (!inLobby) {
            if (_facingLobbyCamera) animator.transform.localRotation = _lobbyBaseRotation;
            _facingLobbyCamera = false;
            return;
        }
        var camera = Camera.main;
        if (camera == null) return;
        if (!_facingLobbyCamera) {
            _lobbyBaseRotation = animator.transform.localRotation;
            _facingLobbyCamera = true;
        }
        var direction = camera.transform.position - animator.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > .001f)
            animator.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }
    private void RefreshOutfit()
    {
        if (_applied != 1) return;
        int outfit = _state != null && _state.Object != null && _state.Object.IsValid ? _state.CharacterOutfitId : 0;
        if (_appliedOutfit == outfit) return;
        if (_teamTint == null) _teamTint = new MaterialPropertyBlock();
        int skinIndex = Mathf.Clamp(outfit, 0, 3);
        if (_jammoSkins[skinIndex] == null)
            _jammoSkins[skinIndex] = Resources.Load<Material>("Characters/JammoSkins/M_JammoSkin_" + skinIndex);
        var skin = _jammoSkins[skinIndex];
        if (skin == null) return;
        foreach (var child in _jammoChildren)
        {
            foreach (var renderer in child.GetComponentsInChildren<Renderer>(true))
            {
                // Preserve the eye screen and its texture; tint metal body surfaces only.
                if (renderer.name == "head_eyes_low" || renderer.name == "head_screen_low") continue;
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = skin;
                renderer.sharedMaterials = materials;
                renderer.GetPropertyBlock(_teamTint);
                _teamTint.SetColor("_BaseColor", Color.white);
                renderer.SetPropertyBlock(_teamTint);
            }
        }
        _appliedOutfit = outfit;
    }
    public void Refresh()
    {
        int id = _state != null && _state.Object != null && _state.Object.IsValid ? _state.CharacterId : offlineCharacterId;
        if (_applied == id || animator == null) return;
        if (id == 1 && !EnsureJammo()) return;
        if (id == 2 && !EnsurePriest()) return;
        foreach (var child in _priestChildren) if (child != null) child.SetActive(id == 2);
        foreach (var child in _defaultChildren) if (child.Key != null) child.Key.SetActive(id == 0 && child.Value);
        foreach (var child in _jammoChildren) if (child != null) child.SetActive(id == 1);
        animator.avatar = id == 2 ? _priestAvatar : id == 1 ? _jammoAvatar : _defaultAvatar;
        animator.runtimeAnimatorController = id == 2 ? _priestController : id == 1 ? _jammoController : _defaultController;
        animator.transform.localScale = _defaultScale * (id == 2 ? _priestScale : id == 1 ? _jammoScale : 1f);
        animator.applyRootMotion = false;
        animator.Rebind();
        animator.Update(0f);
        _applied = id;
        foreach (var anchor in GetComponentsInChildren<PlayerHeldItemAnchor>(true)) anchor.RefreshCharacterRig(animator);
        _applied = id;
        _appliedOutfit = -1;
        RefreshOutfit();
        GetComponent<QuickOutline.Outline>()?.RefreshRenderers();
        if (id == 0) GetComponent<PlayerColorPresenter>()?.Refresh();
    }
    private static float MeasureVisualHeight(GameObject root)
    {
        float minimum = float.PositiveInfinity;
        float maximum = float.NegativeInfinity;
        foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer.name.IndexOf("FirstPerson", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            minimum = Mathf.Min(minimum, renderer.bounds.min.y);
            maximum = Mathf.Max(maximum, renderer.bounds.max.y);
        }
        return float.IsInfinity(minimum) ? 0f : maximum - minimum;
    }

    private bool EnsurePriest()
    {
        if (_priestAvatar != null) return true;
        var prefab = Resources.Load<GameObject>("Characters/PF_CursedPriestVisual");
        _priestController = Resources.Load<RuntimeAnimatorController>("Characters/AOC_CursedPriestPlayer");
        var avatar = prefab != null ? prefab.GetComponent<Animator>()?.avatar : null;
        if (avatar == null || !avatar.isValid || !avatar.isHuman || _priestController == null)
        {
            Debug.LogError("[Character] Cursed Priest requires a valid Humanoid avatar and Jammo controller.");
            return false;
        }

        var root = Instantiate(prefab, animator.transform);
        var priestRig = root.GetComponent<Animator>();
        _priestRightPalm = PalmBasis(priestRig, false);
        _priestLeftPalm = PalmBasis(priestRig, true);
        priestRig.enabled = false;
        var height = MeasureVisualHeight(root);
        _priestScale = _defaultVisualHeight > 0f && height > 0f ? _defaultVisualHeight / height : 1f;
        while (root.transform.childCount > 0)
        {
            var child = root.transform.GetChild(0);
            child.SetParent(animator.transform, false);
            _priestChildren.Add(child.gameObject);
        }
        _priestAvatar = avatar;
        Destroy(root);
        return true;
    }

    private bool EnsureJammo()
    {
        if (_jammoAvatar != null) return true;
        var prefab = Resources.Load<GameObject>("Characters/PF_JammoVisual");
        _jammoController = Resources.Load<RuntimeAnimatorController>("Characters/AOC_JammoPlayer");
        if (prefab == null || _jammoController == null)
        {
            Debug.LogError("[Character] Jammo visual or animation override missing.");
            return false;
        }
        _jammoRoot = Instantiate(prefab, animator.transform);
        _jammoRoot.name = "JammoVisualSource";
        var sourceAnimator = _jammoRoot.GetComponent<Animator>();
        _jammoAvatar = sourceAnimator.avatar;
        _jammoRightPalm = PalmBasis(sourceAnimator, false);
        _jammoLeftPalm = PalmBasis(sourceAnimator, true);
        float jammoHeight = MeasureVisualHeight(_jammoRoot);
        _jammoScale = _defaultVisualHeight > 0f && jammoHeight > 0f ? _defaultVisualHeight / jammoHeight : 1f;
        sourceAnimator.enabled = false;
        // Avatar and original Generic clip paths start directly below the Animator root.
        var children = new List<Transform>();
        foreach (Transform child in _jammoRoot.transform) children.Add(child);
        foreach (var child in children)
        {
            child.SetParent(animator.transform, false);
            _jammoChildren.Add(child.gameObject);
        }
        if (Application.isPlaying) Destroy(_jammoRoot);
        else DestroyImmediate(_jammoRoot);
        return true;
    }
}





