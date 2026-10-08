using Fusion;
using Photon.Voice.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Voice
{
    public sealed class VoicePlayerBinding : MonoBehaviour
    {
        private VoiceManager _manager;
        private string _key;
        private AudioSource _source;
        private Speaker _speaker;
        private NetworkObject _player;
        private float _deadline;
        public string Key => _key;
        public bool IsSpeaking => _speaker != null && _speaker.IsPlaying && !_source.mute;

        public void Initialize(VoiceManager manager, string key, AudioSource source, Speaker speaker)
        {
            _manager = manager; _key = key; _source = source; _speaker = speaker;
            _deadline = Time.unscaledTime + 10;
        }

        private void LateUpdate()
        {
            if (_manager == null || !_manager.AcceptStream(_key)) { Destroy(gameObject); return; }
            var player = _manager.FindPlayer(_key);
            if (player == null)
            {
                _source.mute = true;
                if (Time.unscaledTime > _deadline) Destroy(gameObject);
                return;
            }
            _player = player;
            _deadline = Time.unscaledTime + 10;
            transform.position = _player.transform.position + Vector3.up * 1.6f;
            bool isLobby = SceneManager.GetActiveScene().name == "Lobby";
            _source.spatialBlend = isLobby ? 0f : 1f;
            _source.volume = _manager.OutputVolume;
            _source.outputAudioMixerGroup = _manager.OutputMixer;
            _source.mute = _player.HasInputAuthority || _manager.IsPlayerMuted(_key);
        }
    }
}
