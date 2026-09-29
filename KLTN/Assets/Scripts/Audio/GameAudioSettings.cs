using System.Collections.Generic;
using EchoProtocol.Voice;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Audio
{
    /// <summary>Local, persistent listening levels. Does not change authored source volumes.</summary>
    public sealed class GameAudioSettings : MonoBehaviour
    {
        private const string MasterKey = "Echo.Audio.Master";
        private const string MusicKey = "Echo.Audio.Music";
        private const string EffectsKey = "Echo.Audio.Effects";
        private static bool _loaded;
        private static float _master, _music, _effects;
        private static AudioMixer _mixer;
        private static AudioMixerGroup _musicGroup, _effectsGroup;
        private readonly HashSet<Scene> _routedScenes = new HashSet<Scene>();

        public static float MasterVolume { get { Load(); return _master; } }
        public static float MusicVolume { get { Load(); return _music; } }
        public static float EffectsVolume { get { Load(); return _effects; } }
        public static float VoiceVolume => VoiceManager.Instance != null
            ? VoiceManager.Instance.OutputVolume : ReadVolume("Echo.Voice.Volume");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            _loaded = false;
            _mixer = null;
            _musicGroup = null;
            _effectsGroup = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            Load();
            var owner = new GameObject("GameAudioSettings");
            DontDestroyOnLoad(owner);
            owner.AddComponent<GameAudioSettings>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        private void Start()
        {
            Apply();
            for (int i = 0; i < SceneManager.sceneCount; i++) RouteScene(SceneManager.GetSceneAt(i));
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RouteScene(scene);
        private void OnSceneUnloaded(Scene scene) => _routedScenes.Remove(scene);

        private void RouteScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || !_routedScenes.Add(scene)) return;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var source in root.GetComponentsInChildren<AudioSource>(true))
            {
                if (source.outputAudioMixerGroup != null
                    || source.GetComponentInParent<VoiceManager>(true) != null
                    || source.GetComponentInParent<VoicePlayerBinding>(true) != null
                    || source.GetComponentInParent<Photon.Voice.Unity.Speaker>(true) != null) continue;

                // Imported warehouse ambience has no audio scripts; include inactive fans too.
                if (source.gameObject.name == "BackgroundMusic") RouteMusic(source);
                else RouteEffects(source);
            }
        }

        private void OnApplicationPause(bool paused) { if (paused) Save(); }
        private void OnApplicationQuit() => Save();

        public static void SetMasterVolume(float value)
        {
            Load();
            _master = Sanitize(value);
            PlayerPrefs.SetFloat(MasterKey, _master);
            AudioListener.volume = _master;
        }

        public static void SetMusicVolume(float value)
        {
            Load();
            _music = Sanitize(value);
            PlayerPrefs.SetFloat(MusicKey, _music);
            SetMixerVolume("MusicVolume", _music);
        }

        public static void SetEffectsVolume(float value)
        {
            Load();
            _effects = Sanitize(value);
            PlayerPrefs.SetFloat(EffectsKey, _effects);
            SetMixerVolume("EffectsVolume", _effects);
        }

        public static void SetVoiceVolume(float value)
        {
            value = Sanitize(value);
            if (VoiceManager.Instance != null) VoiceManager.Instance.SetVolume(value);
            else PlayerPrefs.SetFloat("Echo.Voice.Volume", value);
        }

        public static void Save() => PlayerPrefs.Save();

        public static void RouteEffects(AudioSource source)
        {
            Load();
            if (source != null && _effectsGroup != null) source.outputAudioMixerGroup = _effectsGroup;
        }

        public static void RouteMusic(AudioSource source)
        {
            Load();
            if (source != null && _musicGroup != null) source.outputAudioMixerGroup = _musicGroup;
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _master = ReadVolume(MasterKey);
            _music = ReadVolume(MusicKey);
            _effects = ReadVolume(EffectsKey);
            _mixer = Resources.Load<AudioMixer>("GameAudioMixer");
            if (_mixer == null)
            {
                Debug.LogWarning("[GameAudioSettings] Resources/GameAudioMixer is missing.");
                return;
            }
            var groups = _mixer.FindMatchingGroups("Music");
            if (groups.Length > 0) _musicGroup = groups[0];
            groups = _mixer.FindMatchingGroups("Effects");
            if (groups.Length > 0) _effectsGroup = groups[0];
        }

        private static void Apply()
        {
            Load();
            AudioListener.volume = _master;
            SetMixerVolume("MusicVolume", _music);
            SetMixerVolume("EffectsVolume", _effects);
        }

        private static void SetMixerVolume(string parameter, float volume)
        {
            // Mixer attenuation keeps fades and overlapping one-shots responsive to sliders.
            if (_mixer != null) _mixer.SetFloat(parameter, volume <= 0f ? -80f : 20f * Mathf.Log10(volume));
        }

        private static float ReadVolume(string key) => Sanitize(PlayerPrefs.GetFloat(key, 1f));
        private static float Sanitize(float value) => float.IsNaN(value) ? 1f : Mathf.Clamp01(value);
    }
}
