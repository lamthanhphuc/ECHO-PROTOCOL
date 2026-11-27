using System.Collections.Generic;
using EchoProtocol.Audio;
using EchoProtocol.Networking;
using EchoProtocol.Networking.Loading;
using EchoProtocol.Settings;
using EchoProtocol.UI.HUD;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Cinematics
{
    [DefaultExecutionOrder(10000), DisallowMultipleComponent]
    public sealed class OpeningCutsceneController : MonoBehaviour
    {
        [SerializeField] private OpeningCutsceneDefinition _definition;
        [SerializeField] private PlayableDirector _director;
        [SerializeField] private Camera _camera;
        [SerializeField] private AudioListener _listener;
        [SerializeField] private AudioSource _audio;
        [SerializeField] private Light _fillLight;
        [SerializeField] private OpeningCutscenePresentation _presentation;
        [SerializeField] private GameObject[] _shotVisuals;
        private readonly PlayerInteractionControlLock _inputLock = new PlayerInteractionControlLock();
        private readonly Dictionary<Camera, bool> _cameras = new Dictionary<Camera, bool>();
        private readonly Dictionary<AudioListener, bool> _listeners = new Dictionary<AudioListener, bool>();
        private readonly Dictionary<Canvas, bool> _hud = new Dictionary<Canvas, bool>();
        private readonly Dictionary<PlayerFirstPersonVisibility, bool> _bodyViews = new Dictionary<PlayerFirstPersonVisibility, bool>();
        private readonly Dictionary<Renderer, bool> _hiddenPlayerRenderers = new Dictionary<Renderer, bool>();
        private readonly Dictionary<Canvas, bool> _hiddenPlayerCanvases = new Dictionary<Canvas, bool>();
        private readonly Dictionary<Light, bool> _hiddenPlayerLights = new Dictionary<Light, bool>();
        private NetworkMatchState _match;
        private Camera _playerCamera;
        private bool _captured, _playing, _voted, _reportedFailure;
        private float _hold, _revealStarted = -1f;
        private int _lastShot = -1;
        private GameAudioCatalog _catalog;
        private float _fillIntensity;

        public OpeningCutsceneDefinition Definition => _definition;
        public PlayableDirector Director => _director;
        public Camera CinematicCamera => _camera;
        public Light FillLight => _fillLight;
        public void SetFillLight(Light light) => _fillLight = light;
        public bool IsPlayable => _definition != null && _definition.IsPlayable && _director != null
            && _director.playableAsset == _definition.timeline && _camera != null && _listener != null
            && _presentation != null && _presentation.IsReady && HasTimelineBindings();

        private bool HasTimelineBindings()
        {
            bool hasOutput = false;
            foreach (var output in _definition.timeline.outputs)
            {
                hasOutput = true;
                if (_director.GetGenericBinding(output.sourceObject) == null) return false;
            }
            return hasOutput;
        }

        public static OpeningCutsceneController FindForScene(Scene scene)
        {
            string name = LobbyMapCatalog.IsGameplayScene(scene.name) ? scene.name : SceneManager.GetActiveScene().name;
            foreach (var controller in FindObjectsByType<OpeningCutsceneController>(FindObjectsInactive.Exclude))
                if (controller._definition != null && controller._definition.sceneName == name) return controller;
            return null;
        }

        public void Configure(OpeningCutsceneDefinition definition, PlayableDirector director, Camera camera,
            AudioListener listener, AudioSource audio, OpeningCutscenePresentation presentation)
        {
            _definition = definition; _director = director; _camera = camera;
            _listener = listener; _audio = audio; _presentation = presentation;
        }

        private void Awake()
        {
            if (_camera != null) _camera.enabled = false;
            if (_listener != null) _listener.enabled = false;
            if (_fillLight != null) _fillLight.enabled = false;
            if (_fillLight != null) _fillIntensity = _fillLight.intensity;
            if (_director != null) _director.playOnAwake = false;
            if (_presentation != null) _presentation.SkipRequested += Vote;
            _catalog = Resources.Load<GameAudioCatalog>("GameAudioCatalog");
            GameAudioSettings.RouteEffects(_audio);
        }

        private void LateUpdate()
        {
            var match = NetworkMatchState.Instance;
            if (match != _match)
            {
                Cleanup(); _match = match; _voted = _reportedFailure = false;
            }
            if (match == null || match.Object == null || !match.Object.IsValid || match.IsEnded
                || NetworkBootstrap.IsHostMigrationReconciliationPending)
            {
                Cleanup(); return;
            }
            if (!match.HasOpeningCutscene) return;
            if (match.IsGameplayReleased)
            {
                if (_captured)
                {
                    RestoreGameplay();
                    _revealStarted = Time.unscaledTime;
                }
                if (_revealStarted >= 0)
                {
                    float progress = (Time.unscaledTime - _revealStarted) / OpeningCutsceneRules.FadeSeconds;
                    _presentation.FadeToGameplay(1f - progress);
                    if (progress >= 1) { _presentation.Hide(); _revealStarted = -1f; }
                }
                return;
            }
            if (!IsPlayable)
            {
                if (!_reportedFailure && match.OpeningState == OpeningCutsceneState.Playing)
                { _reportedFailure = true; match.ReportOpeningUnavailable(); }
                return;
            }
            CaptureGameplay(match);
            if (!_captured) return;
            if (_definition.hidePlayers)
            {
                foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Exclude))
                    if (camera != _camera)
                    {
                        _cameras.TryAdd(camera, camera.enabled);
                        camera.enabled = false;
                    }
                // Include proxies, held props and any visuals attached during a late spawn.
                foreach (var movement in FindObjectsByType<NetworkPlayerMovement>(FindObjectsInactive.Exclude))
                    HidePlayerVisuals(movement.transform);
                foreach (var movement in FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude))
                    HidePlayerVisuals(movement.transform);
            }
            float elapsed = (float)match.OpeningElapsed;
            bool started = match.OpeningState == OpeningCutsceneState.Playing && match.Runner.Tick.Raw >= match.OpeningStartTick;
            if (started)
            {
                if (!_playing)
                {
                    _director.timeUpdateMode = DirectorUpdateMode.Manual;
                    _director.Play(); _director.Pause(); _playing = true;
                }
                _director.time = Mathf.Min(elapsed, _definition.Duration - 0.001f);
                _director.Evaluate();
                _camera.enabled = true; _listener.enabled = true;
                if (_fillLight != null) _fillLight.enabled = true;
                // Finish at the local owned FPS pose, including that player's real spawn offset.
                if (!_definition.hidePlayers && _playerCamera != null && elapsed > _definition.Duration - 1.8f)
                {
                    float blend = Mathf.SmoothStep(0, 1, (elapsed - (_definition.Duration - 1.8f)) / 1.2f);
                    _camera.transform.SetPositionAndRotation(Vector3.Lerp(_camera.transform.position, _playerCamera.transform.position, blend),
                        Quaternion.Slerp(_camera.transform.rotation, _playerCamera.transform.rotation, blend));
                }
            }
            int shot = _definition.ShotIndex(elapsed);
            if (_fillLight != null) _fillLight.intensity = _fillIntensity * _definition.shots[shot].fillMultiplier;
            if (started && shot != _lastShot)
            {
                // Seeking into a cinematic does not replay earlier audio cues.
                _lastShot = shot;
                SetShotVisuals(shot);
                var cue = _definition.shots[shot];
                var clip = cue.audioOverride != null ? cue.audioOverride : _catalog?.Find(cue.audioKey ?? string.Empty);
                if (clip != null && _audio != null) _audio.PlayOneShot(clip, (_catalog != null ? _catalog.effectsVolume : 1f) * 0.3f);
            }
            _voted |= match.HasOpeningVote(match.Runner.LocalPlayer);
            bool space = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
            _hold = space && started && !_voted ? _hold + Time.unscaledDeltaTime : 0f;
            if (_hold >= _definition.holdToSkipSeconds) Vote();
            float fade = !started ? 1f : Mathf.Max(1f - elapsed / OpeningCutsceneRules.FadeSeconds,
                1f - match.OpeningRemaining / OpeningCutsceneRules.FadeSeconds);
            var current = _definition.shots[shot];
            _presentation.Show(started ? GameLanguage.Choose(current.subtitle, current.subtitleEnglish) : string.Empty,
                fade, match.OpeningSkipVotes, match.OpeningConnectedPlayers, _voted,
                _hold / Mathf.Max(0.2f, _definition.holdToSkipSeconds));
        }

        private void CaptureGameplay(NetworkMatchState match)
        {
            if (_captured) return;
            if (!match.Runner.TryGetPlayerObject(match.Runner.LocalPlayer, out var player)
                || player == null || !player.HasInputAuthority) return;
            var driver = FindAnyObjectByType<PlayerCamera>();
            if (driver == null || driver.Target != player.transform) return;
            _playerCamera = driver.GetComponent<Camera>();
            if (_playerCamera == null) return;
            _inputLock.Acquire(player.gameObject, unlockCursor: true);
            if (!_definition.hidePlayers)
            {
                foreach (var view in player.GetComponentsInChildren<PlayerFirstPersonVisibility>(true))
                {
                    _bodyViews[view] = view.IsCinematicView;
                    view.SetCinematicView(true);
                }
            }
            _cameras[_playerCamera] = _playerCamera.enabled; _playerCamera.enabled = false;
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude))
            {
                if (listener == _listener) continue;
                _listeners[listener] = listener.enabled; listener.enabled = false;
            }
            foreach (var hud in FindObjectsByType<GameplayHUDManager>(FindObjectsInactive.Exclude))
            {
                var canvas = hud.GetComponent<Canvas>();
                if (canvas == null) continue;
                _hud[canvas] = canvas.enabled; canvas.enabled = false;
            }
            _captured = true;
        }

        private void HidePlayerVisuals(Transform player)
        {
            foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
            {
                // Independent of Renderer.enabled, which first-person and tool scripts manage.
                _hiddenPlayerRenderers.TryAdd(renderer, renderer.forceRenderingOff);
                renderer.forceRenderingOff = true;
            }
            foreach (var canvas in player.GetComponentsInChildren<Canvas>(true))
            {
                _hiddenPlayerCanvases.TryAdd(canvas, canvas.enabled);
                canvas.enabled = false;
            }
            foreach (var light in player.GetComponentsInChildren<Light>(true))
            {
                _hiddenPlayerLights.TryAdd(light, light.enabled);
                light.enabled = false;
            }
        }

        public void SetShotVisuals(int shot)
        {
            if (_shotVisuals == null) return;
            for (int i = 0; i < _shotVisuals.Length; i++)
                if (_shotVisuals[i] != null) _shotVisuals[i].SetActive(i == shot);
        }

        private void Vote()
        {
            if (_voted || _match == null || _match.OpeningState != OpeningCutsceneState.Playing) return;
            _match.RequestOpeningSkip(); _voted = true;
        }

        private void RestoreGameplay()
        {
            SetShotVisuals(-1);
            if (_director != null) _director.Stop();
            if (_camera != null) _camera.enabled = false;
            if (_listener != null) _listener.enabled = false;
            if (_fillLight != null) _fillLight.enabled = false;
            if (_fillLight != null && _captured) _fillLight.intensity = _fillIntensity;
            if (_audio != null) _audio.Stop();
            foreach (var entry in _cameras) if (entry.Key != null) entry.Key.enabled = entry.Value;
            foreach (var entry in _listeners) if (entry.Key != null) entry.Key.enabled = entry.Value;
            foreach (var entry in _hud) if (entry.Key != null) entry.Key.enabled = entry.Value;
            foreach (var entry in _bodyViews) if (entry.Key != null) entry.Key.SetCinematicView(entry.Value);
            foreach (var entry in _hiddenPlayerRenderers) if (entry.Key != null) entry.Key.forceRenderingOff = entry.Value;
            foreach (var entry in _hiddenPlayerCanvases) if (entry.Key != null) entry.Key.enabled = entry.Value;
            foreach (var entry in _hiddenPlayerLights) if (entry.Key != null) entry.Key.enabled = entry.Value;
            _cameras.Clear(); _listeners.Clear(); _hud.Clear();
            _bodyViews.Clear();
            _hiddenPlayerRenderers.Clear();
            _hiddenPlayerCanvases.Clear();
            _hiddenPlayerLights.Clear();
            _inputLock.Release();
            _captured = _playing = false; _hold = 0; _lastShot = -1;
        }

        public void Cleanup()
        {
            RestoreGameplay();
            if (_presentation != null) _presentation.Hide();
            _revealStarted = -1f;
        }

        private void OnDisable() => Cleanup();
        private void OnDestroy()
        {
            Cleanup();
            if (_presentation != null) _presentation.SkipRequested -= Vote;
        }
    }
}
