using EchoProtocol.Networking;
using QuickOutline;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Outline))]
public sealed class PlayerOutlineVisibility : MonoBehaviour
{
    [SerializeField, Min(1f)]
    private float maxVisibleDistance = 20f;

    [SerializeField]
    private Color outlineColor =
        new Color(0f, 0.9f, 1f, 1f);

    [SerializeField, Range(0f, 10f)]
    private float outlineWidth = 4f;

    private Outline _outline;
    private LobbyPlayerState _lobbyState;

    private void Awake()
    {
        ResolveReferences();
        ConfigureOutline();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ConfigureOutline();
    }

    private void OnValidate()
    {
        ResolveReferences();
        ConfigureOutline();
    }

    private void Update()
    {
        ResolveReferences();

        if (_outline == null)
        {
            return;
        }

        if (_lobbyState == null
            || _lobbyState.Object == null
            || !_lobbyState.Object.IsValid)
        {
            _outline.enabled = false;
            return;
        }

        // Không outline chính player local.
        if (_lobbyState.Object.HasInputAuthority)
        {
            _outline.enabled = false;
            return;
        }

        Camera viewer = Camera.main;
        if (viewer == null)
        {
            _outline.enabled = false;
            return;
        }

        float maxSqr =
            maxVisibleDistance
            * maxVisibleDistance;

        float sqrDistance =
            (viewer.transform.position
             - transform.position)
            .sqrMagnitude;

        _outline.enabled =
            sqrDistance <= maxSqr;
    }

    private void ResolveReferences()
    {
        if (_outline == null)
        {
            _outline = GetComponent<Outline>();
        }

        if (_lobbyState == null)
        {
            _lobbyState =
                GetComponent<LobbyPlayerState>()
                ?? GetComponentInParent<LobbyPlayerState>();
        }
    }

    private void ConfigureOutline()
    {
        if (_outline == null)
        {
            return;
        }

        _outline.OutlineMode =
            Outline.Mode.OutlineAll;

        _outline.OutlineColor =
            outlineColor;

        _outline.OutlineWidth =
            outlineWidth;

        _outline.UpdateMaterialProperties();
    }
}
