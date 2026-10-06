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
    public bool IsJammo => _applied == 1;
    public Quaternion RightHandGripCorrection => IsJammo
        ? _jammoRightPalm * Quaternion.Inverse(_defaultRightPalm) : Quaternion.identity;

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
    private readonly Dictionary<GameObject, bool> _defaultChildren = new Dictionary<GameObject, bool>();
    private readonly List<GameObject> _jammoChildren = new List<GameObject>();
    private GameObject _jammoRoot;
    private Avatar _jammoAvatar;
    private float _jammoScale = 1f;
    private RuntimeAnimatorController _jammoController;
    private int _applied = -1;
    private int _appliedTeam = -1;
    private MaterialPropertyBlock _teamTint;

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
        foreach (Transform child in animator.transform) _defaultChildren.Add(child.gameObject, child.gameObject.activeSelf);
    }
    private void Start() => Refresh();
    private void LateUpdate()
    {
        Refresh();
        RefreshTeamTint();
    }
    private void RefreshTeamTint()
    {
        if (_applied != 1) return;
        int team = _state != null && _state.Object != null && _state.Object.IsValid ? _state.TeamId : 0;
        if (_appliedTeam == team) return;
        if (_teamTint == null) _teamTint = new MaterialPropertyBlock();
        Color tint = GetComponent<PlayerColorPresenter>()?.ResolveTeamTint(team) ?? Color.white;
        foreach (var child in _jammoChildren)
        {
            foreach (var renderer in child.GetComponentsInChildren<Renderer>(true))
            {
                // Preserve the eye screen and its texture; tint metal body surfaces only.
                if (renderer.name == "head_eyes_low" || renderer.name == "head_screen_low") continue;
                renderer.GetPropertyBlock(_teamTint);
                _teamTint.SetColor("_BaseColor", tint);
                renderer.SetPropertyBlock(_teamTint);
            }
        }
        _appliedTeam = team;
    }
    public void Refresh()
    {
        int id = _state != null && _state.Object != null && _state.Object.IsValid ? _state.CharacterId : offlineCharacterId;
        if (_applied == id || animator == null) return;
        if (id == 1 && !EnsureJammo()) return;
        foreach (var child in _defaultChildren) if (child.Key != null) child.Key.SetActive(id == 0 && child.Value);
        foreach (var child in _jammoChildren) if (child != null) child.SetActive(id == 1);
        animator.avatar = id == 1 ? _jammoAvatar : _defaultAvatar;
        animator.runtimeAnimatorController = id == 1 ? _jammoController : _defaultController;
        animator.transform.localScale = id == 1 ? _defaultScale * _jammoScale : _defaultScale;
        animator.applyRootMotion = false;
        animator.Rebind();
        animator.Update(0f);
        _applied = id;
        foreach (var anchor in GetComponentsInChildren<PlayerHeldItemAnchor>(true)) anchor.RefreshCharacterRig(animator);
        _applied = id;
        _appliedTeam = -1;
        RefreshTeamTint();
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





