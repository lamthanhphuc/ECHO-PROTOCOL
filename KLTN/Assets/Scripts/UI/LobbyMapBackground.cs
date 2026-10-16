using EchoProtocol.Networking;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace EchoProtocol.UI
{
    /// <summary>Local lobby decoration driven by the host's existing map selection.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class LobbyMapBackground : MonoBehaviour
    {
        private const int BackgroundLayer = 30;
        private const uint BackgroundRenderingLayers = 4u;
        [SerializeField] private GameObject sciFiEnvironment;
        [SerializeField] private GameObject floodedEnvironment;
        [SerializeField] private Light[] sciFiLights;
        [SerializeField] private Camera foregroundCamera;
        [SerializeField] private Camera backgroundCamera;
        [SerializeField] private EnvironmentState floodedLighting;
        private EnvironmentState _originalLighting;
        private NetworkBootstrap _bootstrap;
        private Camera _camera;
        private float _originalFar;
        private int _originalMask;
        private CameraRenderType _originalRenderType;
        private bool[] _originalLights;
        private int _appliedMap = -1;
        private float _nextRefresh;

        [System.Serializable]
        private struct EnvironmentState
        {
            public Material Skybox;
            public Light Sun;
            public bool Fog;
            public FogMode FogMode;
            public Color FogColor, SkyColor, EquatorColor, GroundColor;
            public AmbientMode AmbientMode;
            public DefaultReflectionMode ReflectionMode;
            public Texture CustomReflection;
            public float[] AmbientProbe;
            public float FogStart, FogEnd, FogDensity, AmbientIntensity, ReflectionIntensity;

            public static EnvironmentState Capture() => new EnvironmentState
            {
                Skybox = RenderSettings.skybox, Sun = RenderSettings.sun,
                Fog = RenderSettings.fog, FogMode = RenderSettings.fogMode,
                FogColor = RenderSettings.fogColor, FogStart = RenderSettings.fogStartDistance,
                FogEnd = RenderSettings.fogEndDistance, FogDensity = RenderSettings.fogDensity,
                SkyColor = RenderSettings.ambientSkyColor, EquatorColor = RenderSettings.ambientEquatorColor,
                GroundColor = RenderSettings.ambientGroundColor, AmbientMode = RenderSettings.ambientMode,
                AmbientIntensity = RenderSettings.ambientIntensity, ReflectionIntensity = RenderSettings.reflectionIntensity,
                ReflectionMode = RenderSettings.defaultReflectionMode,
                CustomReflection = RenderSettings.customReflectionTexture,
                AmbientProbe = CaptureAmbientProbe()
            };

            private static float[] CaptureAmbientProbe()
            {
                var probe = RenderSettings.ambientProbe;
                var values = new float[27];
                // The getter returns intensity-scaled SH; the setter expects unscaled SH.
                float intensity = RenderSettings.ambientMode == AmbientMode.Skybox
                    ? (QualitySettings.activeColorSpace == ColorSpace.Linear
                        ? Mathf.GammaToLinearSpace(RenderSettings.ambientIntensity)
                        : RenderSettings.ambientIntensity)
                    : 1f;
                float inverseIntensity = intensity > 0.000001f ? 1f / intensity : 0f;
                for (int channel = 0; channel < 3; channel++)
                    for (int coefficient = 0; coefficient < 9; coefficient++)
                        values[channel * 9 + coefficient] = probe[channel, coefficient] * inverseIntensity;
                return values;
            }

            public void Apply()
            {
                RenderSettings.skybox = Skybox; RenderSettings.sun = Sun;
                RenderSettings.fog = Fog; RenderSettings.fogMode = FogMode;
                RenderSettings.fogColor = FogColor; RenderSettings.fogStartDistance = FogStart;
                RenderSettings.fogEndDistance = FogEnd; RenderSettings.fogDensity = FogDensity;
                RenderSettings.ambientMode = AmbientMode; RenderSettings.ambientSkyColor = SkyColor;
                RenderSettings.ambientEquatorColor = EquatorColor; RenderSettings.ambientGroundColor = GroundColor;
                RenderSettings.ambientIntensity = AmbientIntensity; RenderSettings.reflectionIntensity = ReflectionIntensity;
                RenderSettings.defaultReflectionMode = ReflectionMode;
                RenderSettings.customReflectionTexture = CustomReflection;
                if (AmbientProbe != null && AmbientProbe.Length == 27)
                {
                    var probe = new SphericalHarmonicsL2();
                    for (int channel = 0; channel < 3; channel++)
                        for (int coefficient = 0; coefficient < 9; coefficient++)
                            probe[channel, coefficient] = AmbientProbe[channel * 9 + coefficient];
                    RenderSettings.ambientProbe = probe;
                }
                else DynamicGI.UpdateEnvironment();
            }
        }

        private void OnEnable()
        {
            if (floodedEnvironment)
                foreach (var light in floodedEnvironment.GetComponentsInChildren<Light>(true))
                {
                    // URP reads these masks from its additional data, not just Light.
                    var data = light.GetUniversalAdditionalLightData();
                    data.renderingLayers = BackgroundRenderingLayers;
                    data.shadowRenderingLayers = BackgroundRenderingLayers;
                    light.cullingMask = 1 << BackgroundLayer;
                }
            _originalLighting = EnvironmentState.Capture();
            if (_originalLighting.Sun && _originalLighting.Sun.gameObject.scene != gameObject.scene)
                _originalLighting.Sun = null;
            _originalLights = new bool[sciFiLights == null ? 0 : sciFiLights.Length];
            for (int i = 0; i < _originalLights.Length; i++)
                _originalLights[i] = sciFiLights[i] && sciFiLights[i].enabled;
            _appliedMap = -1;
            _nextRefresh = 0;
            _camera = foregroundCamera ? foregroundCamera : Camera.main;
            if (_camera)
            {
                _originalFar = _camera.farClipPlane;
                _originalMask = _camera.cullingMask;
                _originalRenderType = _camera.GetUniversalAdditionalCameraData().renderType;
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + .15f;
            if (!_bootstrap) _bootstrap = FindAnyObjectByType<NetworkBootstrap>();
            ApplyMap(LobbyMapCatalog.Selected(_bootstrap ? _bootstrap.Runner : null));
        }

        public void ApplyMap(int map)
        {
            if (!floodedEnvironment || !sciFiEnvironment) return;
            map = map == 1 ? 1 : 0;
            if (_appliedMap == map) return;
            bool flooded = map == 1;
            sciFiEnvironment.SetActive(!flooded);
            floodedEnvironment.SetActive(flooded);
            for (int i = 0; i < _originalLights.Length; i++)
                if (sciFiLights[i]) sciFiLights[i].enabled = !flooded && _originalLights[i];
            SetCameraComposition(flooded);
            if (flooded) floodedLighting.Apply();
            else if (_appliedMap == 1) _originalLighting.Apply();
            _appliedMap = map;
        }

        private void OnDisable()
        {
            if (_appliedMap == 1 && SceneManager.GetActiveScene() == gameObject.scene)
                _originalLighting.Apply();
            SetCameraComposition(false);
            if (floodedEnvironment) floodedEnvironment.SetActive(false);
            if (sciFiEnvironment) sciFiEnvironment.SetActive(true);
            if (_originalLights != null)
                for (int i = 0; i < _originalLights.Length; i++)
                    if (sciFiLights[i]) sciFiLights[i].enabled = _originalLights[i];
            _appliedMap = -1;
        }

        private void SetCameraComposition(bool flooded)
        {
            if (!_camera || !backgroundCamera) return;
            var foreground = _camera.GetUniversalAdditionalCameraData();
            var background = backgroundCamera.GetUniversalAdditionalCameraData();
            if (flooded)
            {
                // Keep the user's exact map camera angle while preserving lobby character framing.
                _camera.cullingMask = _originalMask & ~(1 << BackgroundLayer);
                _camera.farClipPlane = 50f;
                foreground.renderType = CameraRenderType.Overlay;
                if (!background.cameraStack.Contains(_camera)) background.cameraStack.Add(_camera);
            }
            else
            {
                background.cameraStack.Remove(_camera);
                foreground.renderType = _originalRenderType;
                _camera.cullingMask = _originalMask;
                _camera.farClipPlane = _originalFar;
            }
            backgroundCamera.enabled = flooded;
        }
    }
}
