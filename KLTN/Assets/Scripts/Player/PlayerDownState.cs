using System;
using UnityEngine;
using UnityEngine.Events;

public class PlayerDownState : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private PlayerInteraction interaction;
    [SerializeField] private PlayerHidingController hidingController;
    [SerializeField] private PlayerCamera playerCamera;
    [SerializeField] private Transform visualRoot;

    [Header("Down")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float bleedoutSeconds = 90f;
    [SerializeField] private float crawlSpeedMultiplier = 0.32f;
    [SerializeField] private float downedCameraEyeHeight = 0.55f;
    [SerializeField] private Vector3 downedVisualEuler = new Vector3(70f, 0f, 0f);
    [SerializeField] private bool allowCrawlWhileDowned = true;

    [Header("Revive")]
    [SerializeField] private float revivedHealth = 35f;
    [SerializeField] private float reviveProtectionSeconds = 3f;

    [Header("Events")]
    [SerializeField] private UnityEvent downed;
    [SerializeField] private UnityEvent revived;
    [SerializeField] private UnityEvent eliminated;
    [SerializeField] private UnityEvent spectateStarted;

    private float _health;
    private float _bleedoutRemaining;
    private float _protectionUntil;
    private Quaternion _initialVisualRotation;
    private PlayerLifeState _state = PlayerLifeState.Active;
    private bool _networkAuthorityPresentationOnly;
    private bool _authoritativeReviveProtection;
    private float _authoritativeProtectionRemaining;

    public event Action<PlayerDownState, PlayerLifeState> StateChanged;
    public event Action<PlayerDownState, float> BleedoutChanged;

    public PlayerLifeState State => _state;
    public bool IsActive => _state == PlayerLifeState.Active;
    public bool IsDowned => _state == PlayerLifeState.Downed;
    public bool IsDown => IsDowned;
    public bool IsEliminated => _state == PlayerLifeState.Eliminated || _state == PlayerLifeState.Spectating;
    public bool IsSpectating => _state == PlayerLifeState.Spectating;
    public bool HasReviveProtection => _networkAuthorityPresentationOnly
        ? _authoritativeReviveProtection
        : Time.time < _protectionUntil;
    public float ReviveProtectionRemaining => _networkAuthorityPresentationOnly
        ? _authoritativeProtectionRemaining
        : Mathf.Max(0f, _protectionUntil - Time.time);
    public float Health => _health;
    public float BleedoutRemaining => _bleedoutRemaining;
    public float Bleedout01 => bleedoutSeconds <= 0f ? 0f : Mathf.Clamp01(_bleedoutRemaining / bleedoutSeconds);

    private void Awake()
    {
        if (movement == null)
        {
            movement = GetComponent<PlayerMovement>();
        }

        if (interaction == null)
        {
            interaction = GetComponent<PlayerInteraction>();
        }

        if (hidingController == null)
        {
            hidingController = GetComponent<PlayerHidingController>();
        }

        if (playerCamera == null)
        {
            playerCamera = FindAnyObjectByType<PlayerCamera>();
        }

        if (visualRoot != null)
        {
            _initialVisualRotation = visualRoot.localRotation;
        }

        _health = maxHealth;
        _bleedoutRemaining = bleedoutSeconds;
    }

    private void Update()
    {
        if (_networkAuthorityPresentationOnly) return;

        if (!IsDowned)
        {
            return;
        }

        _bleedoutRemaining = Mathf.Max(0f, _bleedoutRemaining - Time.deltaTime);
        BleedoutChanged?.Invoke(this, _bleedoutRemaining);

        if (_bleedoutRemaining <= 0f)
        {
            Eliminate();
        }
    }

    public bool ApplyDamage(float damage)
    {
        if (_networkAuthorityPresentationOnly) return false;

        if (damage <= 0f || !IsActive || HasReviveProtection)
        {
            return false;
        }

        _health = Mathf.Max(0f, _health - damage);
        if (_health <= 0f)
        {
            Down();
        }

        return true;
    }

    public void Down()
    {
        if (_networkAuthorityPresentationOnly) return;

        if (!IsActive)
        {
            return;
        }

        _health = 0f;
        _bleedoutRemaining = bleedoutSeconds;

        if (hidingController != null && hidingController.IsHidden)
        {
            hidingController.ExitHiding();
        }

        SetState(PlayerLifeState.Downed);
        ApplyDownedControls();
        downed?.Invoke();
    }

    public bool Revive()
    {
        if (_networkAuthorityPresentationOnly) return false;

        if (!IsDowned)
        {
            return false;
        }

        _health = Mathf.Clamp(revivedHealth, 1f, maxHealth);
        _bleedoutRemaining = bleedoutSeconds;
        _protectionUntil = Time.time + reviveProtectionSeconds;
        SetState(PlayerLifeState.Active);
        ApplyActiveControls();
        revived?.Invoke();
        return true;
    }

    public void Eliminate()
    {
        if (_networkAuthorityPresentationOnly) return;

        if (IsEliminated)
        {
            return;
        }

        _health = 0f;
        _bleedoutRemaining = 0f;
        SetState(PlayerLifeState.Eliminated);
        ApplyEliminatedControls();
        eliminated?.Invoke();
    }

    public void StartSpectating()
    {
        if (_networkAuthorityPresentationOnly)
        {
            SetState(PlayerLifeState.Spectating);
            ApplyEliminatedControls();
            return;
        }

        if (!IsEliminated)
        {
            Eliminate();
        }

        SetState(PlayerLifeState.Spectating);
        spectateStarted?.Invoke();
    }

    public void ResetToActive()
    {
        if (_networkAuthorityPresentationOnly) return;

        _health = maxHealth;
        _bleedoutRemaining = bleedoutSeconds;
        _protectionUntil = 0f;
        SetState(PlayerLifeState.Active);
        ApplyActiveControls();
    }

    public void SetNetworkAuthorityPresentationOnly(bool enabled)
    {
        _networkAuthorityPresentationOnly = enabled;
    }

    /// <summary>Applies replicated semantic state without running local damage or timers.</summary>
    public void ApplyAuthoritativeSnapshot(
        PlayerLifeState state,
        float health,
        float bleedoutRemaining,
        float protectionRemaining,
        bool applyLocalControls)
    {
        var previousState = _state;
        _health = Mathf.Clamp(health, 0f, maxHealth);
        _bleedoutRemaining = Mathf.Max(0f, bleedoutRemaining);
        _authoritativeProtectionRemaining = Mathf.Max(0f, protectionRemaining);
        _authoritativeReviveProtection = _authoritativeProtectionRemaining > 0f;
        SetState(state);
        BleedoutChanged?.Invoke(this, _bleedoutRemaining);

        if (applyLocalControls)
        {
            if (state == PlayerLifeState.Downed) ApplyDownedControls();
            else if (state == PlayerLifeState.Active) ApplyActiveControls();
            else ApplyEliminatedControls();
        }

        if (previousState == state) return;
        if (state == PlayerLifeState.Downed) downed?.Invoke();
        else if (state == PlayerLifeState.Active && previousState == PlayerLifeState.Downed) revived?.Invoke();
        else if (state == PlayerLifeState.Eliminated) eliminated?.Invoke();
        else if (state == PlayerLifeState.Spectating)
        {
            eliminated?.Invoke();
            spectateStarted?.Invoke();
        }
    }

    private void ApplyDownedControls()
    {
        if (movement != null)
        {
            movement.enabled = allowCrawlWhileDowned;
            movement.SetExternalSpeedMultiplier(crawlSpeedMultiplier);
            movement.SetSprintBlocked(true);
        }

        if (interaction != null)
        {
            interaction.enabled = false;
        }

        if (playerCamera != null)
        {
            playerCamera.SetForcedEyeHeight(downedCameraEyeHeight);
        }

        if (visualRoot != null)
        {
            visualRoot.localRotation = Quaternion.Euler(downedVisualEuler);
        }
    }

    private void ApplyActiveControls()
    {
        if (movement != null)
        {
            movement.enabled = true;
            movement.SetExternalSpeedMultiplier(1f);
            movement.SetSprintBlocked(false);
        }

        if (interaction != null)
        {
            interaction.enabled = true;
        }

        if (playerCamera != null)
        {
            playerCamera.ClearForcedEyeHeight();
        }

        if (visualRoot != null)
        {
            visualRoot.localRotation = _initialVisualRotation;
        }
    }

    private void ApplyEliminatedControls()
    {
        if (movement != null)
        {
            movement.enabled = false;
        }

        if (interaction != null)
        {
            interaction.enabled = false;
        }

        if (playerCamera != null)
        {
            playerCamera.ClearForcedEyeHeight();
        }

        if (visualRoot != null)
        {
            visualRoot.localRotation = _initialVisualRotation;
        }
    }

    private void SetState(PlayerLifeState nextState)
    {
        if (_state == nextState)
        {
            return;
        }

        _state = nextState;
        StateChanged?.Invoke(this, _state);
    }
}
