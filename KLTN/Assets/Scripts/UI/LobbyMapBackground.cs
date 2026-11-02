using EchoProtocol.Networking;
using Fusion;
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
        private bool _confirmedPurgeMode;
        private NetworkRunner _confirmedRunner;
        private string _confirmedRoom;
        private float _nextRefresh;
        private bool _lightingReady;

        private System.Collections.IEnumerator Start()
        {
            // Fusion may enable this scene before making its lighting settings active.
            while (SceneManager.GetActiveScene() != gameObject.scene) yield return null;
            yield return null;
            _originalLighting = EnvironmentState.Capture();
            var probe = RenderSettings.ambientProbe;
            _originalLighting.AmbientProbe = new float[27];
            for (int channel = 0; channel < 3; channel++)
                for (int coefficient = 0; coefficient < 9; coefficient++)
                    _originalLighting.AmbientProbe[channel * 9 + coefficient] = probe[channel, coefficient];
            _lightingReady = true;
        }

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
                // Do not cache the scene ambient probe here. OnEnable can run before
                // Unity has finished resolving the scene's lighting data, and restoring
                // that transient probe corrupts the lobby after repeated map switches.
                AmbientProbe = null
            };

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
                else
                {
                    // Rebuild the probe from the restored skybox/ambient settings instead
                    // of replaying a possibly stale scene-load snapshot.
                    DynamicGI.UpdateEnvironment();
                }
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
            _confirmedPurgeMode = false;
            _confirmedRunner = null;
            _confirmedRoom = null;
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
            if (!_lightingReady) return;
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + .15f;
            if (!_bootstrap) _bootstrap = FindAnyObjectByType<NetworkBootstrap>();
            var runner = _bootstrap ? _bootstrap.Runner : null;
            if (runner != _confirmedRunner || runner != null && runner.SessionInfo.IsValid
                && _confirmedRoom != runner.SessionInfo.Name)
            {
                RestorePresentation();
                _confirmedRunner = runner;
                _confirmedRoom = runner != null && runner.SessionInfo.IsValid ? runner.SessionInfo.Name : null;
                _confirmedPurgeMode = false;
            }
            if (runner != null && runner.SessionInfo.IsValid
                && runner.SessionInfo.Properties.TryGetValue(LobbyMapCatalog.GameModeKey, out var mode))
                _confirmedPurgeMode = (int)mode == 1;
            else if (_confirmedPurgeMode && _appliedMap == 1)
                return; // Keep the confirmed PURGE backdrop through partial room snapshots.
            ApplyMap(LobbyMapCatalog.Selected(runner));
        }

        public void ApplyMap(int map)
        {
            if (!floodedEnvironment || !sciFiEnvironment) return;
            map = map >= 1 ? 1 : 0;
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
            RestorePresentation();
        }

        private void RestorePresentation()
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
