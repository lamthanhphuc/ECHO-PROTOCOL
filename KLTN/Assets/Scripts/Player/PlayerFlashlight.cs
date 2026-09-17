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
    [SerializeField, Min(0f)] private float beamIntensity = 33f;
    [SerializeField, Min(1f)] private float beamRange = 44f;
    [SerializeField, Range(1f, 179f)] private float beamSpotAngle = 42f;
    [SerializeField, Range(1f, 179f)] private float beamInnerSpotAngle = 24f;

    private InputAction _flashlightAction;
    private float _cooldownUntil;
    private PlayerCamera _playerCamera;
    private PlayerDownState _downState;

    private void Awake()
    {
        ResolveLight();
        ResolveDownState();

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
        if (!CanUseFlashlight())
        {
            if (flashlight != null)
            {
                flashlight.enabled = false;
                RemoveBeamVisual();
            }

            return;
        }

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
                RemoveBeamVisual();
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
        }

        ApplyBeamTuning();
    }

    private void ResolveDownState()
    {
        if (_downState == null)
        {
            _downState = GetComponent<PlayerDownState>();
        }
    }

    private bool CanUseFlashlight()
    {
        ResolveDownState();
        return _downState == null || !_downState.IsEliminated;
    }

    private void ApplyBeamTuning()
    {
        if (flashlight == null)
        {
            return;
        }

        flashlight.type = LightType.Spot;
        flashlight.color = new Color(1f, 0.96f, 0.88f);
        flashlight.intensity = beamIntensity;
        flashlight.range = beamRange;
        flashlight.spotAngle = beamSpotAngle;
        flashlight.innerSpotAngle = Mathf.Min(beamInnerSpotAngle, beamSpotAngle);
        flashlight.renderMode = LightRenderMode.ForcePixel;
        flashlight.bounceIntensity = 0.4f;
        flashlight.shadows = LightShadows.Soft;

        RemoveBeamVisual();
    }

    private void RemoveBeamVisual()
    {
        if (flashlight == null)
        {
            return;
        }

        var beamVisual = flashlight.GetComponent<FlashlightBeamVisual>();
        if (beamVisual != null)
        {
            Destroy(beamVisual);
        }

        Transform beam = flashlight.transform.Find("Flashlight_Beam_Visual");
        if (beam != null)
        {
            Destroy(beam.gameObject);
        }
    }

    /// <summary>Tra ve true neu den pin dang bat.</summary>
    public bool IsOn => flashlight != null && flashlight.enabled;
}
