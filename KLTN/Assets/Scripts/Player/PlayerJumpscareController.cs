using EchoProtocol.AI.Stalker.Presentation;
using EchoProtocol.Networking;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Owner-only presentation. Never changes gameplay state or sends an RPC.</summary>
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class PlayerJumpscareController : MonoBehaviour
{
    private static readonly int BiteStateHash =
        Animator.StringToHash("Base Layer.Bite");

    [SerializeField] private GameObject _ghostJumpscarePrefab;
    [SerializeField] private AudioClip _scream;
    [SerializeField] private GameObject _gameplayHud;
    [SerializeField] private Vector3 _ghostOffset = new Vector3(0f, -0.5f, 0.8f);
    [SerializeField] private Vector3 _ghostEuler = new Vector3(0f, 180f, 0f);
    [SerializeField, Min(0f)] private float _shakeDegrees = 3f;
    [SerializeField, Min(0f)] private float _fovPunch = 10f;
    [SerializeField, Min(0.2f)] private float _stalkerBiteSeconds = 1.5f;

    private NetworkPlayerLifeState _life;
    private Camera _camera;
    private GameObject _ghost;
    private Animator _animator;
    private AudioSource _audio;
    private CanvasGroup _fade;
    private StalkerAnimatorPresenter _suppressedStalkerPresenter;
    private StalkerAnimatorPresenter _pendingStalkerPresenter;

    private bool _playing;
    private bool _seenCatch;
    private bool _animated;
    private bool _screamed;
    private bool _hudWasActive;
    private bool _stalkerBite;
    private bool _beetleJumpscare;
    private Vector3 _beetleNearPosition;
    private Vector3 _beetleStartPosition;

    private float _startedAt;
    private float _duration;
    private float _baseFov;
    private float _pendingStalkerDeadline;

    private Quaternion _shake = Quaternion.identity;
    private uint _catchOrdinal;

    public bool IsPlaying => _playing;

    private bool IsLocal =>
        _life != null
        && _life.Object != null
        && _life.Object.IsValid
        && _life.Object.HasInputAuthority;

    private void Awake()
    {
        _life = GetComponent<NetworkPlayerLifeState>();
    }

    private void Update()
    {
        RemoveShake();

        if (!IsLocal)
        {
            _pendingStalkerPresenter = null;
            StopJumpscare();
            return;
        }

        if (_pendingStalkerPresenter != null)
        {
            if (Time.unscaledTime > _pendingStalkerDeadline)
            {
                Debug.LogWarning("[Jumpscare] Stalker bite skipped: local player camera was unavailable.");
                _pendingStalkerPresenter = null;
            }
            else if (TryBindLocalCamera())
            {
                var presenter = _pendingStalkerPresenter;
                _pendingStalkerPresenter = null;
                PlayStalkerBite(presenter);
            }
        }

        // Stalker Bite is a transient local presentation.
        // It does not depend on NetworkPlayerLifeStatus.Caught.
        if (_stalkerBite)
        {
            return;
        }

        if (!_life.IsCaught)
        {
            _seenCatch = false;
            StopJumpscare();
            return;
        }

        if (!_seenCatch
            || _catchOrdinal != _life.TransitionOrdinal)
        {
            PlayJumpscare();
        }
    }

    public void PlayJumpscare()
    {
        if (!IsLocal
            || !_life.IsCaught
            || _playing
            || !TryBindLocalCamera())
        {
            return;
        }

        _seenCatch = true;
        _catchOrdinal = _life.TransitionOrdinal;

        var duration =
            Mathf.Max(
                0.2f,
                _life.CatchDuration);

        var elapsed =
            Mathf.Clamp(
                duration - _life.CatchRemaining,
                0f,
                duration);

        StartJumpscare(
            duration,
            elapsed,
            false);
    }

    public void PlayStalkerBite(
        StalkerAnimatorPresenter stalkerPresenter)
    {
        if (!IsLocal)
        {
            Debug.LogWarning("[Jumpscare] Stalker bite skipped: player has no local input authority.");
            return;
        }
        if (stalkerPresenter == null)
        {
            return;
        }

        if (!TryBindLocalCamera())
        {
            _pendingStalkerPresenter = stalkerPresenter;
            _pendingStalkerDeadline = Time.unscaledTime + 1f;
            return;
        }

        // A new authoritative hit replaces any local presentation
        // currently running.
        StopJumpscare();

        if (!TryBindLocalCamera())
        {
            return;
        }

        _suppressedStalkerPresenter =
            stalkerPresenter;

        _suppressedStalkerPresenter
            .SetLocalVisibilitySuppressed(true);

        StartJumpscare(
            Mathf.Max(0.2f, stalkerPresenter.FitBiteJumpscareToCamera
                ? 2.6f : _stalkerBiteSeconds),
            0f,
            true);
    }

    private void FitJumpscareToCamera(GameObject visual)
    {
        visual.transform.localPosition = Vector3.zero;
        var bounds = new Bounds();
        var hasBounds = false;
        foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
        {
            var world = renderer.bounds;
            for (var corner = 0; corner < 8; corner++)
            {
                var point = world.center + Vector3.Scale(world.extents, new Vector3(
                    (corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f,
                    (corner & 4) == 0 ? -1f : 1f));
                point = _camera.transform.InverseTransformPoint(point);
                if (!hasBounds)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    hasBounds = true;
                }
                else bounds.Encapsulate(point);
            }
        }

        if (!hasBounds)
        {
            visual.transform.localPosition = _ghostOffset;
            return;
        }

        var halfVerticalFov = Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        var halfHorizontalFov = halfVerticalFov * Mathf.Max(0.5f, _camera.aspect);
        var distance = Mathf.Max(bounds.extents.y / halfVerticalFov,
            bounds.extents.x / halfHorizontalFov) * 0.65f + bounds.extents.z + 0.25f;
        visual.transform.localPosition = new Vector3(-bounds.center.x, -bounds.center.y,
            Mathf.Max(Mathf.Max(distance, _ghostOffset.z + bounds.center.z) - bounds.center.z,
                0.75f - bounds.min.z));
    }

    private bool TryBindLocalCamera()
    {
        _camera = Camera.main;
        if (_camera == null || _camera.GetComponent<PlayerCamera>() == null)
        {
            var localCamera = FindAnyObjectByType<PlayerCamera>();
            _camera = localCamera != null ? localCamera.GetComponent<Camera>() : null;
        }

        var cameraDriver =
            _camera != null
                ? _camera.GetComponent<PlayerCamera>()
                : null;

        // The local camera may already follow a spectator after a direct elimination.
        return _camera != null && cameraDriver != null;
    }

    private void StartJumpscare(
        float duration,
        float elapsed,
        bool stalkerBite)
    {
        _playing = true;
        _stalkerBite = stalkerBite;
        _duration = duration;
        _startedAt =
            Time.unscaledTime
            - Mathf.Clamp(
                elapsed,
                0f,
                duration);

        _baseFov = _camera.fieldOfView;
        _animated = false;
        _screamed = false;
        _pendingStalkerPresenter = null;
        _beetleJumpscare = stalkerBite && _suppressedStalkerPresenter != null
            && _suppressedStalkerPresenter.FitBiteJumpscareToCamera;

        if (_gameplayHud == null)
        {
            _gameplayHud =
                GameObject.Find(
                    "GameplayHUD_Canvas");
        }

        if (_gameplayHud != null)
        {
            _hudWasActive =
                _gameplayHud.activeSelf;

            _gameplayHud.SetActive(false);
        }

        var jumpscarePrefab = _stalkerBite
            && _suppressedStalkerPresenter != null
            && _suppressedStalkerPresenter.BiteJumpscarePrefab != null
                ? _suppressedStalkerPresenter.BiteJumpscarePrefab
                : _ghostJumpscarePrefab;

        if (jumpscarePrefab != null)
        {
            _ghost =
                Instantiate(
                    jumpscarePrefab,
                    _camera.transform);

            _ghost.transform.localPosition =
                _ghostOffset;

            _ghost.transform.localRotation =
                Quaternion.Euler(
                    _ghostEuler);
            if (_beetleJumpscare)
            {
                // The Beetle's head points down its local -Z axis.
                _ghost.transform.localRotation = Quaternion.identity;
                FitJumpscareToCamera(_ghost);
                _beetleNearPosition = _ghost.transform.localPosition;
                foreach (var bone in _ghost.GetComponentsInChildren<Transform>(true))
                {
                    if (bone.name != "head 1")
                        continue;

                    var head = _ghost.transform.InverseTransformPoint(bone.position);
                    _beetleNearPosition = new Vector3(-head.x, 0.1f - head.y,
                        1.45f - head.z);
                    break;
                }
                _beetleStartPosition = _beetleNearPosition + Vector3.forward * 1.1f;
                _ghost.transform.localPosition = _beetleStartPosition;
            }

            // The prefab root plays the bite clip; camera-relative movement stays local.
            _animator =
                _ghost.GetComponent<Animator>();

            var animators =
                _ghost.GetComponentsInChildren<
                    Animator>(true);

            foreach (var animator in animators)
            {
                animator.updateMode =
                    AnimatorUpdateMode.UnscaledTime;
            }

            if (_beetleJumpscare && _animator != null)
            {
                _ghost.SetActive(true);
                _animator.speed = 2.2f;
                _animator.Play(BiteStateHash, 0, 0f);
                _animated = true;
            }
            else
                _ghost.SetActive(false);
        }

        var audioObject =
            new GameObject(
                "LocalJumpscareAudio");

        audioObject.transform.SetParent(
            _camera.transform,
            false);

        _audio =
            audioObject.AddComponent<
                AudioSource>();

        _audio.playOnAwake = false;
        EchoProtocol.Audio.GameAudioSettings.RouteEffects(_audio);
        _audio.spatialBlend = 0f;

        var overlay =
            new GameObject(
                "LocalJumpscareFade",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasGroup));

        overlay.transform.SetParent(
            transform,
            false);

        var canvas =
            overlay.GetComponent<Canvas>();

        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        canvas.sortingOrder = 32760;

        _fade =
            overlay.GetComponent<CanvasGroup>();

        _fade.alpha = 0f;
        _fade.blocksRaycasts = false;

        var black =
            new GameObject(
                "Black",
                typeof(RectTransform),
                typeof(Image));

        black.transform.SetParent(
            overlay.transform,
            false);

        var rect =
            black.GetComponent<RectTransform>();

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        black.GetComponent<Image>().color =
            Color.black;

        black.GetComponent<Image>()
            .raycastTarget = false;
    }

    private void LateUpdate()
    {
        if (!_playing
            || !IsLocal
            || _camera == null)
        {
            return;
        }

        var elapsed =
            Time.unscaledTime
            - _startedAt;

        if (_ghost != null
            && elapsed >= 0.05f)
        {
            _ghost.SetActive(true);
        }

        if (_beetleJumpscare && _ghost != null)
        {
            var approach = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(elapsed / 0.35f));
            _ghost.transform.localPosition = Vector3.Lerp(
                _beetleStartPosition, _beetleNearPosition, approach);
        }

        if (!_animated
            && elapsed >= (_beetleJumpscare ? 0f : 0.1f))
        {
            _animated = true;

            // Root visual lunge.
            if (_animator != null)
            {
                if (_beetleJumpscare)
                    _animator.speed = 2.2f;
                if (_stalkerBite && _animator.HasState(0, BiteStateHash))
                    _animator.Play(BiteStateHash, 0, 0f);
                else
                    _animator.SetTrigger("Jumpscare");
            }

            // New Stalker model animation.
            if (_stalkerBite && !_beetleJumpscare
                && _ghost != null)
            {
                var animators =
                    _ghost.GetComponentsInChildren<
                        Animator>(true);

                foreach (var animator in animators)
                {
                    if (animator == _animator
                        || !animator.HasState(
                            0,
                            BiteStateHash))
                    {
                        continue;
                    }

                    animator.Play(
                        BiteStateHash,
                        0,
                        0f);

                    break;
                }
            }
        }

        if (!_screamed
            && elapsed >= (_beetleJumpscare ? 0.35f : 0.15f))
        {
            _screamed = true;

            if (_scream != null)
            {
                _audio.PlayOneShot(
                    _scream);
            }
        }

        var shakeStart = _beetleJumpscare ? 0.35f : 0.2f;
        var envelope =
            elapsed >= shakeStart
                ? Mathf.Clamp01(
                    1f
                    - (elapsed - shakeStart)
                    / 0.8f)
                : 0f;

        _shake =
            Quaternion.Euler(
                Mathf.Sin(elapsed * 83f)
                * _shakeDegrees
                * envelope,

                Mathf.Sin(elapsed * 107f)
                * _shakeDegrees
                * envelope,

                0f);

        _camera.transform.rotation *=
            _shake;

        _camera.fieldOfView =
            _baseFov
            - _fovPunch
            * envelope;

        var fadeStart = _beetleJumpscare
            ? 2f
            : Mathf.Min(1.5f, _duration * 0.75f);

        _fade.alpha =
            Mathf.Clamp01(
                (elapsed - fadeStart)
                / Mathf.Max(
                    0.05f,
                    _duration - fadeStart));

        if (_stalkerBite
            && elapsed >= _duration)
        {
            StopJumpscare();
            return;
        }

        // Ghost Catch keeps black until authority ends Caught.
        if (!_stalkerBite
            && elapsed >= _duration
            && _ghost != null)
        {
            _ghost.SetActive(false);
        }
    }

    private void RemoveShake()
    {
        if (_camera != null
            && _playing)
        {
            _camera.transform.rotation *=
                Quaternion.Inverse(_shake);
        }

        _shake =
            Quaternion.identity;
    }

    public void StopJumpscare()
    {
        if (_suppressedStalkerPresenter != null)
        {
            _suppressedStalkerPresenter
                .SetLocalVisibilitySuppressed(false);

            _suppressedStalkerPresenter = null;
        }

        if (!_playing)
        {
            _stalkerBite = false;
            return;
        }

        RemoveShake();

        _playing = false;
        _stalkerBite = false;
        _beetleJumpscare = false;

        if (_camera != null)
        {
            _camera.fieldOfView =
                _baseFov;
        }

        if (_gameplayHud != null)
        {
            _gameplayHud.SetActive(
                _hudWasActive);
        }

        if (_ghost != null)
        {
            Destroy(_ghost);
        }

        if (_audio != null)
        {
            _audio.Stop();
            Destroy(_audio.gameObject);
        }

        if (_fade != null)
        {
            Destroy(_fade.gameObject);
        }

        _ghost = null;
        _animator = null;
        _audio = null;
        _fade = null;
    }

    private void OnDisable()
    {
        _pendingStalkerPresenter = null;
        StopJumpscare();
        _seenCatch = false;
    }
}
