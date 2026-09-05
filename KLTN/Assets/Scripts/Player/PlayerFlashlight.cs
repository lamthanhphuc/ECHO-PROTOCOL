using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Toggle ca nhan den pin bang phim F.
/// Attach vao Player prefab. Tu tim Light con neu khong assign.
/// </summary>
public class PlayerFlashlight : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Light flashlight;
    [SerializeField] private InputActionAsset inputActions;

    [Header("Settings")]
    [SerializeField] private bool startOn = true;
    [SerializeField] private float toggleCooldown = 0.2f;

    private InputAction _flashlightAction;
    private float _cooldownUntil;

    private void Awake()
    {
        // Tu tim Light con neu chua assign trong Inspector
        if (flashlight == null)
            flashlight = GetComponentInChildren<Light>();

        // Lay action tu InputActionAsset
        if (inputActions != null)
        {
            var map = inputActions.FindActionMap("Player", false);
            _flashlightAction = map?.FindAction("Flashlight", false);
        }

        // Trang thai ban dau
        if (flashlight != null)
            flashlight.enabled = startOn;
    }

    private void OnEnable()
    {
        _flashlightAction?.Enable();
    }

    private void OnDisable()
    {
        _flashlightAction?.Disable();
    }

    private void Update()
    {
        if (_flashlightAction == null) return;
        if (Time.time < _cooldownUntil) return;

        if (_flashlightAction.WasPressedThisFrame())
        {
            if (flashlight != null)
                flashlight.enabled = !flashlight.enabled;

            _cooldownUntil = Time.time + toggleCooldown;
        }
    }

    /// <summary>Tra ve true neu den pin dang bat.</summary>
    public bool IsOn => flashlight != null && flashlight.enabled;
}
