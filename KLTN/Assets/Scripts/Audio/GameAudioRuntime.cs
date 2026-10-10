using System.Collections.Generic;
using Unity.Profiling;
using EchoProtocol.Networking;
using EchoProtocol.AI.Stalker.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EchoProtocol.Audio
{
    /// <summary>Client presentation only. Never submits AI noise or changes network state.</summary>
    public sealed class GameAudioRuntime : MonoBehaviour
    {
        private static GameAudioRuntime _instance;
        private GameAudioCatalog _catalog;
        private AudioSource _ui;
        private AudioSource _ambience;
        private AudioSource _roomCurrent;
        private AudioSource _roomNext;
        private AudioSource _dangerMusic;
        private string _roomKey;
        private bool _roomTransitioning;
        private float _nextEnvironmentUpdate;
        private readonly HashSet<GameAudioEnvironmentSource> _environmentSources = new HashSet<GameAudioEnvironmentSource>();
        private static readonly ProfilerMarker EnvironmentMarker = new ProfilerMarker("Echo.Audio.Environment");
        private static readonly ProfilerMarker ButtonRegistrationMarker = new ProfilerMarker("Echo.Audio.SceneButtons");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (_instance != null) return;
            var catalog = Resources.Load<GameAudioCatalog>("GameAudioCatalog");
            if (catalog == null)
            {
                Debug.LogWarning("[GameAudio] Resources/GameAudioCatalog is missing.");
                return;
            }
            var root = new GameObject("GameAudio");
            DontDestroyOnLoad(root);
            _instance = root.AddComponent<GameAudioRuntime>();
            _instance._catalog = catalog;
            _instance._ui = CreateSource(root, false);
            _instance._ambience = CreateSource(root, false);
            _instance._roomCurrent = CreateSource(root, false);
            _instance._roomNext = CreateSource(root, false);
            _instance._dangerMusic = CreateSource(root, false);
            GameAudioSettings.RouteMusic(_instance._dangerMusic);
            SceneManager.activeSceneChanged += _instance.OnSceneChanged;
            SceneManager.sceneLoaded += _instance.OnSceneLoaded;
            NetworkPlayerInteractor.LocalRequestCompleted += _instance.OnInteractionCompleted;
            EchoProtocol.Tools.Scanner.NetworkToolPickup.ToolPickedUp += _instance.OnToolPickedUp;
        }

        public static void EnsureInitialized() => Initialize();

        public static AudioSource CreateSource(GameObject owner, bool spatial)
        {
            var source = owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = spatial ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 2f;
            source.maxDistance = 25f;
            source.dopplerLevel = 0f;
            GameAudioSettings.RouteEffects(source);
            return source;
        }

        public static void Play(AudioSource source, string key, float volume = 1f)
        {
            if (_instance == null || source == null || !Application.isPlaying) return;
            var clip = _instance._catalog.Find(key);
            if (clip != null) source.PlayOneShot(clip, volume * _instance._catalog.effectsVolume);
        }

        public static void Play(AudioSource source, string key, float volume, float pitchMin, float pitchMax)
        {
            if (_instance == null || source == null || !Application.isPlaying) return;
            var clip = _instance._catalog.Find(key);
            if (clip == null) return;

            var originalPitch = source.pitch;
            source.pitch = Random.Range(Mathf.Min(pitchMin, pitchMax), Mathf.Max(pitchMin, pitchMax));
            source.PlayOneShot(clip, volume * _instance._catalog.effectsVolume);
            source.pitch = originalPitch;
        }

        public static void UI(string key) { if (_instance != null) Play(_instance._ui, key); }
        public static AudioClip FindClip(string key) => _instance != null ? _instance._catalog.Find(key) : null;

        public static void AtPoint(string key, Vector3 position)
        {
            if (_instance == null) return;
            var clip = _instance._catalog.Find(key);
            if (clip == null) return;
            var owner = new GameObject("OneShotAudio");
            owner.transform.position = position;
            var source = CreateSource(owner, true);
            source.clip = clip;
            source.volume = _instance._catalog.effectsVolume;
            source.Play();
            Destroy(owner, clip.length + 0.1f);
        }

        private void OnInteractionCompleted(InteractionRequestResult result)
        {
            if (!result.Accepted) UI("ui/error");
        }

        private void OnToolPickedUp(EchoProtocol.Tools.Scanner.NetworkToolPickup pickup, Fusion.PlayerRef player)
        {
            try
            {
                if (pickup != null && pickup.Runner != null && pickup.Runner.LocalPlayer == player)
                {
                    UI("ui/inventory_pickup");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[GameAudioRuntime] OnToolPickedUp error: {ex.Message}");
            }
        }

        public static void Loop(AudioSource source, string key, float volume = 0.35f)
        {
            if (_instance == null || source == null) return;
            var clip = key == null ? null : _instance._catalog.Find(key);
            if (source.clip == clip && (clip == null || source.isPlaying)) return;
            source.Stop();
            source.clip = clip;
            source.loop = true;
            source.volume = volume * _instance._catalog.effectsVolume;
            if (clip != null) source.Play();
        }

        private void OnSceneChanged(Scene previous, Scene current)
        {
            _nextEnvironmentUpdate = 0f;
            _roomKey = null;
            _roomTransitioning = false;
            _roomCurrent.Stop();
            _roomNext.Stop();
            Loop(_dangerMusic, null);
            var inFacility = current.name == LobbyManager.GameSceneName || current.name == "Lobby";
            Loop(_ambience, inFacility ? "map_ambience/facility_room_tone_loop" : null,
                _catalog.ambienceVolume);
            if (current.name == LobbyManager.GameSceneName) UI("ui/match_start");
        }

        private void Update()
        {
            UpdateRoomFade();
            if (Time.unscaledTime < _nextEnvironmentUpdate) return;
            _nextEnvironmentUpdate = Time.unscaledTime + 1f;
            using (EnvironmentMarker.Auto()) UpdateEnvironment();
        }

        // Existing scene buttons are discovered once per load, including initially hidden panels.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            using (ButtonRegistrationMarker.Auto())
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var button in root.GetComponentsInChildren<Button>(true)) RegisterButton(button);
        }
        public static void RegisterButton(Button button)
        {
            if (button!=null && button.GetComponent<GameAudioButton>()==null) button.gameObject.AddComponent<GameAudioButton>();
        }
        public static void RegisterEmitter(Component owner)
        {
            if (owner!=null && owner.GetComponent<GameAudioEmitter>()==null) owner.gameObject.AddComponent<GameAudioEmitter>();
        }
        public static void RegisterEnvironmentOwner(Component owner)
        {
            if (owner!=null && owner.GetComponent<GameAudioEnvironmentSource>()==null) owner.gameObject.AddComponent<GameAudioEnvironmentSource>();
        }
        public static void RegisterEnvironment(GameAudioEnvironmentSource source)
        {
            EnsureInitialized();
            if (_instance!=null) _instance._environmentSources.Add(source);
        }
        public static void UnregisterEnvironment(GameAudioEnvironmentSource source)
        {
            if (_instance!=null) _instance._environmentSources.Remove(source);
        }

        private void UpdateEnvironment()
        {
            if (SceneManager.GetActiveScene().name != LobbyManager.GameSceneName) return;
            var camera = Camera.main;
            if (camera == null) return;
            float nearest = 16f;
            string room = null;
            foreach (var source in _environmentSources)
                if (source!=null && source.isActiveAndEnabled && source.RoomKey!=null)
                    ConsiderRoom(source.transform,source.RoomKey,camera.transform.position,ref nearest,ref room);

            if (_roomKey != room)
            {
                _roomKey = room;
                if (_roomTransitioning)
                {
                    _roomCurrent.Stop();
                    var previous = _roomCurrent;
                    _roomCurrent = _roomNext;
                    _roomNext = previous;
                }
                _roomNext.Stop();
                _roomNext.clip = room == null ? null : _catalog.Find(room);
                _roomNext.loop = true;
                _roomNext.volume = 0f;
                if (_roomNext.clip != null) _roomNext.Play();
                _roomTransitioning = true;
            }

            float nearestDangerDistance = 34f;
            bool danger = false;
            foreach (var source in _environmentSources)
            {
                var stalker=source!=null && source.isActiveAndEnabled ? source.Stalker : null;
                if (stalker==null || !stalker.isActiveAndEnabled || stalker.Object==null || !stalker.Object.IsValid) continue;
                // StalkerAudioController already owns the spatial chase loop for this prefab.
                // A second 2D drone makes the monster sound close regardless of its position.
                if (stalker.GetComponent<EchoProtocol.AI.Stalker.Presentation.StalkerAudioController>() != null)
                    continue;
                float distance = Vector3.Distance(stalker.transform.position, camera.transform.position);
                if (distance >= nearestDangerDistance) continue;
                var state = stalker.GetReplicatedPresentationState();
                if (state.PresentationVisible && (state.SemanticState == EchoProtocol.AI.Stalker.StalkerState.DETECT
                    || state.SemanticState == EchoProtocol.AI.Stalker.StalkerState.CHASE))
                {
                    danger = true;
                    nearestDangerDistance = distance;
                }
            }
            Loop(_dangerMusic, danger ? "horror_ambience/tension_drone_loop" : null, 0.18f);
            if (danger)
                _dangerMusic.volume = 0.18f * _catalog.effectsVolume
                    * Mathf.InverseLerp(34f, 6f, nearestDangerDistance);
        }

        private static void ConsiderRoom(Transform source, string key, Vector3 listener, ref float nearest, ref string room)
        {
            float distance = Vector3.Distance(source.position, listener);
            if (distance >= nearest) return;
            nearest = distance;
            room = key;
        }

        private void UpdateRoomFade()
        {
            float target = _roomTransitioning && _roomNext.clip != null ? _catalog.ambienceVolume * 0.6f : 0f;
            _roomNext.volume = Mathf.MoveTowards(_roomNext.volume, target, Time.unscaledDeltaTime * 0.12f);
            _roomCurrent.volume = Mathf.MoveTowards(_roomCurrent.volume,
                _roomTransitioning ? 0f : (_roomCurrent.clip != null ? _catalog.ambienceVolume * 0.6f : 0f),
                Time.unscaledDeltaTime * 0.12f);
            if (_roomTransitioning && _roomCurrent.volume <= 0.001f)
            {
                _roomCurrent.Stop();
                var old = _roomCurrent;
                _roomCurrent = _roomNext;
                _roomNext = old;
                _roomNext.clip = null;
                _roomTransitioning = false;
            }
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            NetworkPlayerInteractor.LocalRequestCompleted -= OnInteractionCompleted;
            EchoProtocol.Tools.Scanner.NetworkToolPickup.ToolPickedUp -= OnToolPickedUp;
            if (_instance == this) _instance = null;
        }
    }
}
