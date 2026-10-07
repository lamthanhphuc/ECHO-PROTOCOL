using EchoProtocol.MatchFlow;
using EchoProtocol.Networking;
using Fusion;
using QuickOutline;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class PlayerZoneExitGuide : MonoBehaviour
{
    private enum GuideStage { None = 0, Zone1Exit = 1, Zone2Exit = 2, FinalExit = 3 }
    private static readonly Color GuideBlue = new Color(0.1f, 0.95f, 1f, 1f);
    [Header("Arrow")]
    [SerializeField, Min(0.2f)] private float arrowForwardOffset = 1.15f;
    [SerializeField, Min(0f)] private float arrowGroundOffset = 0.04f;
    [SerializeField, Min(0.5f)] private float arrivalDistance = 3f;
    [SerializeField, Min(0.01f)] private float arrowWidth = 0.07f;
    private NetworkObject _networkObject;
    private LineRenderer _arrow;
    private Material _arrowMaterial;
    private GuideStage _stage;
    private bool _arrived;
    private Transform _outlinedTarget;
    private Outline _outline;
    private bool _createdOutline;
    private bool _originalOutlineEnabled;
    private Color _originalOutlineColor;
    private float _originalOutlineWidth;
    private Outline.Mode _originalOutlineMode;

    private void Awake() { _networkObject = GetComponent<NetworkObject>(); CreateArrow(); SetArrowVisible(false); }
    private void OnDisable() { SetArrowVisible(false); ClearDoorOutline(); }
    private void OnDestroy() { ClearDoorOutline(); if (_arrowMaterial != null) Destroy(_arrowMaterial); }
    private void LateUpdate()
    {
        if (_networkObject == null || !_networkObject.IsValid || !_networkObject.HasInputAuthority) { SetArrowVisible(false); ClearDoorOutline(); return; }
        var nextStage = ResolveGuideStage();
        if (nextStage != _stage) { _stage = nextStage; _arrived = false; ClearDoorOutline(); }
        if (_stage == GuideStage.None || _arrived || !TryResolveGuideTarget(_stage, out var target)) { SetArrowVisible(false); ClearDoorOutline(); return; }
        var toTarget = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up);
        if (toTarget.sqrMagnitude <= arrivalDistance * arrivalDistance) { _arrived = true; SetArrowVisible(false); ClearDoorOutline(); return; }
        if (_stage == GuideStage.Zone1Exit || _stage == GuideStage.Zone2Exit) ApplyDoorOutline(target); else ClearDoorOutline();
        DrawArrow(toTarget.normalized);
    }
    private GuideStage ResolveGuideStage()
    {
        var match = NetworkMatchState.Instance;
        if (match != null && match.Object != null && match.Object.IsValid && !match.IsEnded)
        {
            switch (match.CurrentPhase)
            {
                case NetworkMatchPhase.Zone2Objective: return GuideStage.Zone1Exit;
                case NetworkMatchPhase.Zone3FindFrigate: return GuideStage.Zone2Exit;
                case NetworkMatchPhase.FinalHunt:
                case NetworkMatchPhase.Escape: return GuideStage.FinalExit;
                default: return GuideStage.None;
            }
        }
        var flow = FindAnyObjectByType<MatchFlowController>();
        if (flow == null || flow.IsMatchEnded) return GuideStage.None;
        switch (flow.Phase)
        {
            case MatchPhase.SecurityHold:
            case MatchPhase.PowerPuzzle: return GuideStage.Zone1Exit;
            case MatchPhase.Zone3FindFrigate: return GuideStage.Zone2Exit;
            case MatchPhase.FinalHunt:
            case MatchPhase.ExitCountdown: return GuideStage.FinalExit;
            default: return GuideStage.None;
        }
    }
    private bool TryResolveGuideTarget(GuideStage stage, out Transform target)
    {
        target = null;
        switch (stage)
        {
            case GuideStage.Zone1Exit:
                var door = GameObject.Find("DoorToZone2"); target = door != null ? door.transform : null; return target != null;
            case GuideStage.Zone2Exit:
                var director = Zone2MissionDirector.Instance; if (director == null) return false;
                target = Closest(ResolveDoorRoot(director.DoorBlocker1), ResolveDoorRoot(director.DoorBlocker2)); return target != null;
            case GuideStage.FinalExit:
                var zone3 = Zone3MissionDirector.Instance;
                if (zone3 != null && zone3.ExitTransform != null) { target = zone3.ExitTransform; return true; }
                var exit = GameObject.Find("Doorexit"); target = exit != null ? exit.transform : null; return target != null;
            default: return false;
        }
    }
    private Transform ResolveDoorRoot(GameObject blocker) => blocker == null ? null : blocker.transform.parent != null ? blocker.transform.parent : blocker.transform;
    private Transform Closest(Transform first, Transform second)
    {
        if (first == null) return second;
        if (second == null) return first;
        return (first.position - transform.position).sqrMagnitude <= (second.position - transform.position).sqrMagnitude ? first : second;
    }
    private void ApplyDoorOutline(Transform target)
    {
        if (_outlinedTarget == target && _outline != null) { ConfigureBlueOutline(_outline); return; }
        ClearDoorOutline(); _outlinedTarget = target; _outline = target.GetComponent<Outline>(); _createdOutline = _outline == null;
        if (_outline == null) _outline = target.gameObject.AddComponent<Outline>();
        else { _originalOutlineEnabled = _outline.enabled; _originalOutlineColor = _outline.OutlineColor; _originalOutlineWidth = _outline.OutlineWidth; _originalOutlineMode = _outline.OutlineMode; }
        ConfigureBlueOutline(_outline);
    }
    private static void ConfigureBlueOutline(Outline outline)
    {
        if (outline == null) return;
        outline.OutlineMode = Outline.Mode.OutlineAll; outline.OutlineColor = GuideBlue; outline.OutlineWidth = 5f; outline.enabled = true; outline.UpdateMaterialProperties();
    }
    private void ClearDoorOutline()
    {
        if (_outline != null)
        {
            if (_createdOutline) Destroy(_outline);
            else { _outline.OutlineMode = _originalOutlineMode; _outline.OutlineColor = _originalOutlineColor; _outline.OutlineWidth = _originalOutlineWidth; _outline.enabled = _originalOutlineEnabled; _outline.UpdateMaterialProperties(); }
        }
        _outline = null; _outlinedTarget = null; _createdOutline = false;
    }
    private void CreateArrow()
    {
        var arrowObject = new GameObject("ZoneExitGuideArrow"); arrowObject.transform.SetParent(transform, false); _arrow = arrowObject.AddComponent<LineRenderer>();
        _arrow.useWorldSpace = true; _arrow.positionCount = 5; _arrow.startWidth = arrowWidth; _arrow.endWidth = arrowWidth; _arrow.numCapVertices = 4; _arrow.numCornerVertices = 2; _arrow.shadowCastingMode = ShadowCastingMode.Off; _arrow.receiveShadows = false; _arrow.startColor = GuideBlue; _arrow.endColor = GuideBlue;
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        if (shader != null) { _arrowMaterial = new Material(shader); if (_arrowMaterial.HasProperty("_BaseColor")) _arrowMaterial.SetColor("_BaseColor", GuideBlue); _arrowMaterial.color = GuideBlue; _arrow.material = _arrowMaterial; }
    }
    private void DrawArrow(Vector3 direction)
    {
        if (_arrow == null || direction.sqrMagnitude < 0.001f) { SetArrowVisible(false); return; }
        var forward = Camera.main != null ? Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up) : Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f) forward = transform.forward; forward.Normalize();
        var probe = transform.position + forward * arrowForwardOffset + Vector3.up;
        var center = transform.position + forward * arrowForwardOffset + Vector3.up * arrowGroundOffset;
        if (Physics.Raycast(probe, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore)) center = hit.point + Vector3.up * arrowGroundOffset;
        var right = Vector3.Cross(Vector3.up, direction).normalized;
        var tail = center - direction * 0.36f; var tip = center + direction * 0.48f; var headBase = tip - direction * 0.24f; var left = headBase - right * 0.19f; var rightPoint = headBase + right * 0.19f;
        _arrow.SetPosition(0, tail); _arrow.SetPosition(1, tip); _arrow.SetPosition(2, left); _arrow.SetPosition(3, tip); _arrow.SetPosition(4, rightPoint); SetArrowVisible(true);
    }
    private void SetArrowVisible(bool visible) { if (_arrow != null && _arrow.enabled != visible) _arrow.enabled = visible; }
}
