using EchoProtocol.Networking;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Local presentation only. Called after PlayerCamera has resolved its gameplay pose.</summary>
public sealed class PlayerCameraFeedback : MonoBehaviour
{
    private Transform _target;
    private NetworkPlayerMovement _movement;
    private PlayerMovement _offlineMovement;
    private PlayerDownState _life;
    private NetworkPlayerFlashlight _uv;
    private PlayerJumpscareController _jumpscare;
    private PlayerInteraction _interaction;
    private NetworkPlayerInteractor _networkInteraction;
    private CharacterController _controller;
    private Vector3 _previousPosition;
    private float _previousHealth = -1, _phase, _amplitude, _hit;
    private Canvas _canvas;
    private Image _edge;
    private Texture2D _texture;
    private Sprite _sprite;

    public static bool IsDamage(float previous, float current) => previous >= 0 && current < previous - .01f;
    public static float MotionWeight(bool blocked, bool grounded, bool alive, bool reduceMotion, float intensity)
        => blocked || !grounded || !alive || reduceMotion ? 0 : Mathf.Clamp01(intensity);

    private void Bind(Transform target)
    {
        _target = target;
        _movement = target.GetComponent<NetworkPlayerMovement>();
        _offlineMovement = target.GetComponent<PlayerMovement>();
        _life = target.GetComponent<PlayerDownState>();
        _uv = target.GetComponent<NetworkPlayerFlashlight>();
        _jumpscare = target.GetComponent<PlayerJumpscareController>();
        _interaction = target.GetComponent<PlayerInteraction>();
        _networkInteraction = target.GetComponent<NetworkPlayerInteractor>();
        _controller = target.GetComponent<CharacterController>();
        _previousPosition = target.position;
        _previousHealth = -1;
        _phase = _amplitude = _hit = 0;
    }

    public void Apply(Transform target, bool ownedView, bool reviving)
    {
        if (target == null) { if (_canvas != null) _canvas.enabled = false; return; }
        if (_target != target) Bind(target);
        float dt = Mathf.Min(Time.deltaTime, .05f);
        float speed = Vector3.ProjectOnPlane(target.position - _previousPosition, Vector3.up).magnitude / Mathf.Max(Time.deltaTime, .001f);
        _previousPosition = target.position;
        bool blocked = !ownedView || PlayerInteractionControlLock.IsGameplayInputBlocked(target.gameObject)
            || (_movement != null && _movement.Object != null && _movement.Object.IsValid && !_movement.Object.HasInputAuthority)
            || (_jumpscare != null && _jumpscare.IsPlaying)
            || (NetworkMatchState.Instance != null && NetworkMatchState.Instance.CurrentPhase == NetworkMatchPhase.MatchEnded);
        bool alive = _life == null || _life.IsActive;
        float health = _life != null ? _life.Health : 100;
        if (!blocked && alive && IsDamage(_previousHealth, health)) _hit = 1;
        _previousHealth = health;
        _hit = blocked ? 0 : Mathf.MoveTowards(_hit, 0, dt / .65f);
        bool grounded = _movement != null ? _movement.IsPresentationGrounded : _controller != null && _controller.isGrounded;
        bool holding = _interaction != null && _interaction.IsHoldingInteraction
            || _networkInteraction != null && _networkInteraction.CurrentReviveTarget != null;
        float weight = MotionWeight(blocked || reviving || holding, grounded, alive, PlayerCameraSettings.ReduceMotion, PlayerCameraSettings.Intensity);
        bool crouching = _movement != null ? _movement.IsAnimationCrouching : _offlineMovement != null && _offlineMovement.IsCrouching;
        bool sprinting = _movement != null ? _movement.IsAnimationSprinting : _offlineMovement != null && _offlineMovement.IsSprinting;
        float frequency = crouching ? 1.25f : sprinting ? 2.5f : 1.8f;
        float desired = speed > .2f && speed < 12 ? (crouching ? .006f : sprinting ? .021f : .011f) * weight : 0;
        if (_uv != null && _uv.Object != null && _uv.Object.IsValid && _uv.ResonanceActive) desired *= .5f;
        _amplitude = weight == 0 ? 0 : Mathf.Lerp(_amplitude, desired, 1 - Mathf.Exp(-12 * dt));
        _phase = Mathf.Repeat(_phase + dt * frequency * Mathf.PI * 2, Mathf.PI * 2);
        transform.position += transform.rotation * new Vector3(Mathf.Sin(_phase * .5f) * _amplitude * .35f, Mathf.Cos(_phase) * _amplitude, 0);
        float impact = Mathf.Clamp01((_hit - .65f) / .35f);
        float shake = PlayerCameraSettings.ReduceMotion || blocked || !alive ? 0 : impact * impact * PlayerCameraSettings.Intensity;
        // Roll keeps transform.forward unchanged: interaction and scanner rays use this camera too.
        transform.rotation *= Quaternion.Euler(0, 0,
            Mathf.Sin(_phase * .5f) * _amplitude * 12 + Mathf.Sin(_hit * 55) * shake * 1.65f);
        UpdateEdge(blocked, health / Mathf.Max(1, _life != null ? _life.MaxHealth : 100), _life != null && _life.IsDowned, dt);
    }

    private void UpdateEdge(bool blocked, float health, bool downed, float dt)
    {
        if (_canvas == null) CreateEdge();
        _canvas.enabled = !blocked && (_life == null || (!_life.IsEliminated && !_life.IsSpectating));
        float low = Mathf.Clamp01((.4f - health) / .4f);
        float pulse = PlayerCameraSettings.ReduceMotion ? 1 : .88f + Mathf.Sin(Time.time * 4) * .12f;
        float alpha = downed ? .78f : Mathf.Min(.8f, _hit * .75f + low * .48f * pulse);
        Color target = downed ? new Color(.025f, .015f, .02f, alpha) : new Color(.42f, .015f, .025f, alpha);
        _edge.color = Color.Lerp(_edge.color, target, 1 - Mathf.Exp(-dt * (alpha > _edge.color.a ? 24 : 5)));
    }

    private void CreateEdge()
    {
        var root = new GameObject("Local damage feedback", typeof(Canvas));
        root.transform.SetParent(transform, false);
        _canvas = root.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 90;
        var image = new GameObject("Peripheral vignette", typeof(RectTransform), typeof(Image));
        image.transform.SetParent(root.transform, false);
        _edge = image.GetComponent<Image>();
        _edge.raycastTarget = false;
        _edge.color = Color.clear;
        var rect = _edge.rectTransform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        const int size = 128;
        _texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float radius = new Vector2((x + .5f) / size * 2 - 1, (y + .5f) / size * 2 - 1).magnitude;
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.48f, 1.18f, radius)));
            }
        _texture.SetPixels(pixels); _texture.Apply(false, true);
        _sprite = Sprite.Create(_texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
        _edge.sprite = _sprite;
    }

    private void OnDisable() { if (_canvas != null) _canvas.enabled = false; }
    private void OnDestroy()
    {
        if (Application.isPlaying)
        {
            if (_sprite != null) Destroy(_sprite);
            if (_texture != null) Destroy(_texture);
        }
        else
        {
            if (_sprite != null) DestroyImmediate(_sprite);
            if (_texture != null) DestroyImmediate(_texture);
        }
    }
}
