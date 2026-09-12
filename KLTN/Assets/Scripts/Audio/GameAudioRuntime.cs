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
        private float _nextDiscovery;

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
            SceneManager.activeSceneChanged += _instance.OnSceneChanged;
            NetworkPlayerInteractor.LocalRequestCompleted += _instance.OnInteractionCompleted;
            EchoProtocol.Tools.Scanner.NetworkToolPickup.ToolPickedUp += _instance.OnToolPickedUp;
        }

        public static AudioSource CreateSource(GameObject owner, bool spatial)
        {
            var source = owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = spatial ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 2f;
            source.maxDistance = 25f;
            source.dopplerLevel = 0f;
            return source;
        }

        public static void Play(AudioSource source, string key, float volume = 1f)
        {
            if (_instance == null || source == null || !Application.isPlaying) return;
            var clip = _instance._catalog.Find(key);
            if (clip != null) source.PlayOneShot(clip, volume * _instance._catalog.effectsVolume);
        }

        public static void UI(string key) { if (_instance != null) Play(_instance._ui, key); }

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
            _nextDiscovery = 0f;
            var inFacility = current.name == LobbyManager.GameSceneName || current.name == "Lobby";
            Loop(_ambience, inFacility ? "map_ambience/facility_room_tone_loop" : null,
                _catalog.ambienceVolume);
            if (current.name == LobbyManager.GameSceneName) UI("ui/match_start");
        }

        private void Update()
        {
            // Also discover objects spawned by Fusion after scene load.
            if (Time.unscaledTime < _nextDiscovery) return;
            _nextDiscovery = Time.unscaledTime + 1f;
            Attach<NetworkPlayerMovement>();
            Attach<StalkerFusionRuntime>();
            Attach<NetworkSlidingDoor>();
            Attach<NetworkDoor>();
            Attach<NetworkPickupItem>();
            Attach<NetworkSectorBox>();
            Attach<NetworkPowerPuzzle>();
            Attach<NetworkMatchState>();
            Attach<SecurityTerminalDownload>();
            Attach<NoiseMakerBeacon>();
            foreach (var button in FindObjectsByType<Button>())
                if (button.GetComponent<GameAudioButton>() == null) button.gameObject.AddComponent<GameAudioButton>();
        }

        private static void Attach<T>() where T : Component
        {
            foreach (var component in FindObjectsByType<T>())
                if (component.GetComponent<GameAudioEmitter>() == null)
                    component.gameObject.AddComponent<GameAudioEmitter>();
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            NetworkPlayerInteractor.LocalRequestCompleted -= OnInteractionCompleted;
            EchoProtocol.Tools.Scanner.NetworkToolPickup.ToolPickedUp -= OnToolPickedUp;
            if (_instance == this) _instance = null;
        }
    }
}
