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

    private bool _playing;
    private bool _seenCatch;
    private bool _animated;
    private bool _screamed;
    private bool _hudWasActive;
    private bool _stalkerBite;

    private float _startedAt;
    private float _duration;
    private float _baseFov;

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
            StopJumpscare();
            return;
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
        if (!IsLocal
            || stalkerPresenter == null
            || !TryBindLocalCamera())
        {
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
            Mathf.Max(0.2f, _stalkerBiteSeconds),
            0f,
            true);
    }

    private bool TryBindLocalCamera()
    {
        _camera = Camera.main;

        var cameraDriver =
            _camera != null
                ? _camera.GetComponent<PlayerCamera>()
                : null;

        return _camera != null
            && cameraDriver != null
            && cameraDriver.Target == transform;
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

        if (_ghostJumpscarePrefab != null)
        {
            _ghost =
                Instantiate(
                    _ghostJumpscarePrefab,
                    _camera.transform);

            _ghost.transform.localPosition =
                _ghostOffset;

            _ghost.transform.localRotation =
                Quaternion.Euler(
                    _ghostEuler);

            // Root Animator owns the camera lunge.
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

        if (!_animated
            && elapsed >= 0.1f)
        {
            _animated = true;

            // Root visual lunge.
            if (_animator != null)
            {
                _animator.SetTrigger(
                    "Jumpscare");
            }

            // New Stalker model animation.
            if (_stalkerBite
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
            && elapsed >= 0.15f)
        {
            _screamed = true;

            if (_scream != null)
            {
                _audio.PlayOneShot(
                    _scream);
            }
        }

        var envelope =
            elapsed >= 0.2f
                ? Mathf.Clamp01(
                    1f
                    - (elapsed - 0.2f)
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

        var fadeStart =
            Mathf.Min(
                1.5f,
                _duration * 0.75f);

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
        StopJumpscare();
        _seenCatch = false;
    }
}
