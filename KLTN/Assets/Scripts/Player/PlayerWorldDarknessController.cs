using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerWorldDarknessController : MonoBehaviour
{
    [SerializeField] private bool applyDarkness = true;
    [SerializeField] private Color fogColor = new Color(0.01f, 0.012f, 0.016f, 1f);
    [SerializeField, Range(0.001f, 0.4f)] private float fogDensity = 0.05f;
    [SerializeField, Range(-10f, 100f)] private float ambientIntensity = 0.015f;
    [SerializeField] private Color ambientLight = new Color(0.29803923f, 0.29803923f, 0.29803923f, 1f);
    [SerializeField] private bool useLocalPlayerVisionLight = true;
    [SerializeField, Min(0f)] private float localVisionIntensity = 0.35f;
    [SerializeField, Min(0.1f)] private float localVisionRange = 1.25f;
    [SerializeField] private Color localVisionColor = new Color(1f, 0.92f, 0.78f, 1f);

    private Camera _camera;
    private Light _localVisionLight;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        ApplySettings();
    }

    private void OnEnable()
    {
        ApplySettings();
    }

    private void LateUpdate()
    {
        ApplySettings();
    }

    private void ApplySettings()
    {
        if (!applyDarkness)
        {
            return;
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ambientLight;
        RenderSettings.ambientIntensity = ambientIntensity;

        if (_camera != null)
        {
            _camera.backgroundColor = fogColor;
        }

        ApplyLocalVisionLight();
    }

    private void ApplyLocalVisionLight()
    {
        if (!useLocalPlayerVisionLight)
        {
            if (_localVisionLight != null)
            {
                _localVisionLight.enabled = false;
            }
            return;
        }

        if (_localVisionLight == null)
        {
            Transform existing = transform.Find("Local_Player_Vision_Light");
            if (existing != null)
            {
                _localVisionLight = existing.GetComponent<Light>();
            }
        }

        if (_localVisionLight == null)
        {
            var lightObj = new GameObject("Local_Player_Vision_Light");
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.localPosition = Vector3.zero;
            lightObj.transform.localRotation = Quaternion.identity;
            _localVisionLight = lightObj.AddComponent<Light>();
        }

        _localVisionLight.enabled = true;
        _localVisionLight.type = LightType.Point;
        _localVisionLight.color = localVisionColor;
        _localVisionLight.intensity = localVisionIntensity;
        _localVisionLight.range = localVisionRange;
        _localVisionLight.shadows = LightShadows.None;
    }
}
