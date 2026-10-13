using EchoProtocol.MatchFlow;
using EchoProtocol.Networking;
using Fusion;
using QuickOutline;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerZoneExitGuide : MonoBehaviour
{
    private enum GuideStage
    {
        None = 0,
        Zone1Exit = 1,
        Zone2Exit = 2,
        FinalExit = 3,
    }

    private static readonly Color GuideBlue =
        new Color(0.1f, 0.95f, 1f, 1f);

    [SerializeField, Min(0.5f)]
    private float arrivalDistance = 3f;

    private NetworkObject _networkObject;
    private GuideStage _stage;
    private bool _arrived;

    private Transform _outlinedTarget;
    private Outline _outline;
    private bool _createdOutline;

    private bool _originalOutlineEnabled;
    private Color _originalOutlineColor;
    private float _originalOutlineWidth;
    private Outline.Mode _originalOutlineMode;

    private void Awake()
    {
        _networkObject =
            GetComponent<NetworkObject>();
    }

    private void OnDisable()
    {
        ClearDoorOutline();
    }

    private void OnDestroy()
    {
        ClearDoorOutline();
    }

    private void LateUpdate()
    {
        if (_networkObject == null
            || !_networkObject.IsValid
            || !_networkObject.HasInputAuthority)
        {
            ClearDoorOutline();
            return;
        }

        GuideStage nextStage =
            ResolveGuideStage();

        if (nextStage != _stage)
        {
            _stage = nextStage;
            _arrived = false;
            ClearDoorOutline();
        }

        if (_stage == GuideStage.None
            || _arrived
            || !TryResolveGuideTarget(
                _stage,
                out Transform target))
        {
            ClearDoorOutline();
            return;
        }

        Vector3 toTarget =
            Vector3.ProjectOnPlane(
                target.position
                - transform.position,
                Vector3.up);

        if (toTarget.sqrMagnitude
            <= arrivalDistance * arrivalDistance)
        {
            _arrived = true;
            ClearDoorOutline();
            return;
        }

        if (_stage == GuideStage.Zone1Exit
            || _stage == GuideStage.Zone2Exit)
        {
            ApplyDoorOutline(target);
        }
        else
        {
            ClearDoorOutline();
        }
    }

    private GuideStage ResolveGuideStage()
    {
        var match =
            NetworkMatchState.Instance;
        if (match != null && match.Object != null && match.Object.IsValid && match.IsEnded)
            return GuideStage.None;

        if (match != null
            && match.Object != null
            && match.Object.IsValid
            && !match.IsEnded)
        {
            switch (match.CurrentPhase)
            {
                case NetworkMatchPhase.Zone2Objective:
                    return GuideStage.Zone1Exit;

                case NetworkMatchPhase.Zone3FindFrigate:
                    return GuideStage.Zone2Exit;

                case NetworkMatchPhase.FinalHunt:
                case NetworkMatchPhase.Escape:
                    return GuideStage.FinalExit;

                default:
                    return GuideStage.None;
            }
        }

        var flow =
            FindAnyObjectByType<
                MatchFlowController>();

        if (flow == null
            || flow.IsMatchEnded)
        {
            return GuideStage.None;
        }

        switch (flow.Phase)
        {
            case MatchPhase.SecurityHold:
            case MatchPhase.PowerPuzzle:
                return GuideStage.Zone1Exit;

            case MatchPhase.Zone3FindFrigate:
                return GuideStage.Zone2Exit;

            case MatchPhase.FinalHunt:
            case MatchPhase.ExitCountdown:
                return GuideStage.FinalExit;

            default:
                return GuideStage.None;
        }
    }

    private bool TryResolveGuideTarget(
        GuideStage stage,
        out Transform target)
    {
        target = null;

        switch (stage)
        {
            case GuideStage.Zone1Exit:
            {
                var door =
                    GameObject.Find(
                        "DoorToZone2");

                target =
                    door != null
                        ? door.transform
                        : null;

                return target != null;
            }

            case GuideStage.Zone2Exit:
            {
                var director =
                    Zone2MissionDirector.Instance;

                if (director == null)
                {
                    return false;
                }

                target =
                    Closest(
                        ResolveDoorRoot(
                            director.DoorBlocker1),
                        ResolveDoorRoot(
                            director.DoorBlocker2));

                return target != null;
            }

            case GuideStage.FinalExit:
            {
                var zone3 =
                    Zone3MissionDirector.Instance;

                if (zone3 != null
                    && zone3.ExitTransform != null)
                {
                    target =
                        zone3.ExitTransform;

                    return true;
                }

                var exit =
                    GameObject.Find(
                        "Doorexit");

                target =
                    exit != null
                        ? exit.transform
                        : null;

                return target != null;
            }

            default:
                return false;
        }
    }

    private static Transform ResolveDoorRoot(
        GameObject blocker)
    {
        if (blocker == null)
        {
            return null;
        }

        return blocker.transform.parent != null
            ? blocker.transform.parent
            : blocker.transform;
    }

    private Transform Closest(
        Transform first,
        Transform second)
    {
        if (first == null)
        {
            return second;
        }

        if (second == null)
        {
            return first;
        }

        return (first.position
                - transform.position)
               .sqrMagnitude
               <= (second.position
                   - transform.position)
               .sqrMagnitude
            ? first
            : second;
    }

    private void ApplyDoorOutline(
        Transform target)
    {
        if (_outlinedTarget == target
            && _outline != null)
        {
            ConfigureBlueOutline(
                _outline);

            return;
        }

        ClearDoorOutline();

        _outlinedTarget = target;
        _outline =
            target.GetComponent<Outline>();

        _createdOutline =
            _outline == null;

        if (_outline == null)
        {
            _outline =
                target.gameObject
                    .AddComponent<Outline>();
        }
        else
        {
            _originalOutlineEnabled =
                _outline.enabled;

            _originalOutlineColor =
                _outline.OutlineColor;

            _originalOutlineWidth =
                _outline.OutlineWidth;

            _originalOutlineMode =
                _outline.OutlineMode;
        }

        ConfigureBlueOutline(
            _outline);
    }

    private static void ConfigureBlueOutline(
        Outline outline)
    {
        if (outline == null)
        {
            return;
        }

        outline.OutlineMode =
            Outline.Mode.OutlineAll;

        outline.OutlineColor =
            GuideBlue;

        outline.OutlineWidth =
            5f;

        outline.enabled = true;
        outline.UpdateMaterialProperties();
    }

    private void ClearDoorOutline()
    {
        if (_outline != null)
        {
            if (_createdOutline)
            {
                _outline.enabled = false;
                Destroy(_outline);
            }
            else
            {
                _outline.OutlineMode =
                    _originalOutlineMode;

                _outline.OutlineColor =
                    _originalOutlineColor;

                _outline.OutlineWidth =
                    _originalOutlineWidth;

                // A completed guide must not restore an old prefab highlight.
                _outline.enabled = false;

                _outline.UpdateMaterialProperties();
            }
        }

        _outline = null;
        _outlinedTarget = null;
        _createdOutline = false;
    }
}
