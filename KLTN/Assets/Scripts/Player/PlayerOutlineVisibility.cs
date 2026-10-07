using EchoProtocol.Networking;
using QuickOutline;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Outline))]
public sealed class PlayerOutlineVisibility : MonoBehaviour
{
    private const float DefaultMaxVisibleDistance = 20f;

    [SerializeField, Min(1f)]
    private float maxVisibleDistance = DefaultMaxVisibleDistance;

    [SerializeField]
    private Color outlineColor =
        new Color(0f, 0.9f, 1f, 1f);

    [SerializeField, Range(0f, 10f)]
    private float outlineWidth = 4f;

    private Outline _outline;
    private LobbyPlayerState _playerState;
    private LobbyPlayerState _localPlayerState;
    private NetworkPlayerLifeState _lifeState;

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

        if (_outline == null
            || _playerState == null
            || _playerState.Object == null
            || !_playerState.Object.IsValid)
        {
            SetOutline(false);
            return;
        }

        // Local player không tự thấy outline của chính mình.
        if (_playerState.Object.HasInputAuthority)
        {
            SetOutline(false);
            return;
        }

        if (_lifeState == null
            || !_lifeState.IsDowned
            || !_lifeState.CanBeRevived)
        {
            SetOutline(false);
            return;
        }

        ResolveLocalPlayer();

        if (_localPlayerState == null
            || _localPlayerState.Object == null
            || !_localPlayerState.Object.IsValid)
        {
            SetOutline(false);
            return;
        }

        float maxDistanceSqr =
            maxVisibleDistance * maxVisibleDistance;

        float distanceSqr =
            (_localPlayerState.transform.position
             - _playerState.transform.position)
            .sqrMagnitude;

        SetOutline(distanceSqr <= maxDistanceSqr);
    }

    private void ResolveReferences()
    {
        if (_outline == null)
        {
            _outline = GetComponent<Outline>();
        }

        if (_playerState == null)
        {
            _playerState =
                GetComponent<LobbyPlayerState>()
                ?? GetComponentInParent<LobbyPlayerState>();
        }

        if (_lifeState == null)
        {
            _lifeState =
                GetComponent<NetworkPlayerLifeState>()
                ?? GetComponentInParent<NetworkPlayerLifeState>();
        }
    }

    private void ResolveLocalPlayer()
    {
        if (_localPlayerState != null
            && _localPlayerState.gameObject.activeInHierarchy
            && _localPlayerState.Object != null
            && _localPlayerState.Object.IsValid
            && _localPlayerState.Object.HasInputAuthority)
        {
            return;
        }

        _localPlayerState = null;

        LobbyPlayerState[] players =
            FindObjectsByType<LobbyPlayerState>(
                FindObjectsInactive.Exclude);

        for (int i = 0; i < players.Length; i++)
        {
            LobbyPlayerState player = players[i];

            if (player == null
                || player.Object == null
                || !player.Object.IsValid
                || !player.Object.HasInputAuthority)
            {
                continue;
            }

            _localPlayerState = player;
            return;
        }
    }

    private void ConfigureOutline()
    {
        if (_outline == null)
        {
            return;
        }

        _outline.OutlineMode = Outline.Mode.OutlineAll;
        _outline.OutlineColor = outlineColor;
        _outline.OutlineWidth = outlineWidth;
        _outline.UpdateMaterialProperties();
        _outline.enabled = false;
    }

    private void SetOutline(bool visible)
    {
        if (_outline != null
            && _outline.enabled != visible)
        {
            _outline.enabled = visible;
        }
    }
}
