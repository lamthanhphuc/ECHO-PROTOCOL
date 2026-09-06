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
    private PlayerCamera _playerCamera;

    private void Awake()
    {
        ResolveLight();

        if (inputActions != null)
        {
            var map = inputActions.FindActionMap("Player", false);
            _flashlightAction = map?.FindAction("Flashlight", false);
        }

        if (_flashlightAction == null)
        {
            _flashlightAction = new InputAction("Flashlight", InputActionType.Button, "<Keyboard>/f");
        }

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

    private void OnDestroy()
    {
        _flashlightAction?.Dispose();
    }

    private void Update()
    {
        if (Time.time < _cooldownUntil) return;

        bool pressed = false;
        if (_flashlightAction != null && _flashlightAction.enabled && _flashlightAction.WasPressedThisFrame())
        {
            pressed = true;
        }
        else if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            pressed = true;
        }

        if (pressed)
        {
            ResolveLight();
            if (flashlight != null)
            {
                flashlight.enabled = !flashlight.enabled;
            }

            _cooldownUntil = Time.time + toggleCooldown;
        }
    }

    private void LateUpdate()
    {
        UpdateFlashlightRotation();
    }

    private void UpdateFlashlightRotation()
    {
        ResolveLight();
        if (flashlight == null) return;

        if (_playerCamera == null)
        {
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                _playerCamera = mainCam.GetComponent<PlayerCamera>();
            }
        }

        if (_playerCamera != null)
        {
            flashlight.transform.rotation = _playerCamera.transform.rotation;
        }
        else if (Camera.main != null)
        {
            flashlight.transform.rotation = Camera.main.transform.rotation;
        }
    }

    private void ResolveLight()
    {
        if (flashlight == null)
        {
            Transform child = transform.Find("Flashlight_Light");
            if (child != null)
            {
                flashlight = child.GetComponent<Light>();
            }
        }

        if (flashlight == null)
        {
            flashlight = GetComponentInChildren<Light>(true);
        }

        if (flashlight == null)
        {
            var lightObj = new GameObject("Flashlight_Light");
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.localPosition = new Vector3(0.0645f, 0.862f, 0.06f);
            lightObj.transform.localRotation = Quaternion.Euler(12.245f, 0f, 0f);

            flashlight = lightObj.AddComponent<Light>();
            flashlight.type = LightType.Spot;
            flashlight.color = new Color(1f, 0.96f, 0.88f);
            flashlight.intensity = 2.8f;
            flashlight.range = 28f;
            flashlight.spotAngle = 65f;
            flashlight.innerSpotAngle = 45f;
        }
    }

    /// <summary>Tra ve true neu den pin dang bat.</summary>
    public bool IsOn => flashlight != null && flashlight.enabled;
}
