using System;
using System.Collections.Generic;
using System.Text;
using EchoProtocol.Networking;
using Fusion;
using Photon.Realtime;
using Photon.Voice;
using Photon.Voice.Unity;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

namespace EchoProtocol.Voice
{
    /// <summary>Owns only the Voice connection. A Voice failure must never shut down Fusion.</summary>
    public sealed class VoiceManager : MonoBehaviour
    {
        public static VoiceManager Instance { get; private set; }
        [SerializeField] private AudioMixerGroup _outputMixer;
        public AudioMixerGroup OutputMixer => _outputMixer;
        public VoiceDeviceController Devices { get; private set; }
        public string Status { get; private set; } = "Waiting for a Fusion session";
        public bool MicrophoneEnabled { get; private set; }
        public bool SelfMuted { get; private set; }
        public Key ToggleMicrophoneKey { get; private set; } = Key.V;
        public float OutputVolume { get; private set; } = 1;
        public bool Speaking => _recorder != null && _recorder.IsCurrentlyTransmitting;
        public float InputLevel => Devices.Testing ? Devices.TestLevel : (_recorder.LevelMeter?.CurrentPeakAmp ?? 0);
        public bool Joined => _client != null && _client.Client.InRoom && _client.Client.CurrentRoom.Name == _room && _runner != null && _runner.IsRunning;
        private EchoVoiceClient _client;
        private Recorder _recorder;
        private NetworkRunner _runner;
        private string _room, _region, _localKey;
        private readonly HashSet<string> _muted = new HashSet<string>();
        private float _deadline, _nextAttempt, _captureStarted;
        private int _attempts;
        private bool _connecting, _joinRequested, _focused = true, _paused;

        public static void EnsureExists()
        {
            if (Instance != null) return;
            new GameObject("VoiceManager").AddComponent<VoiceManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _focused = Application.isFocused;
            DontDestroyOnLoad(gameObject);
            Devices = gameObject.AddComponent<VoiceDeviceController>();
            _recorder = gameObject.AddComponent<Recorder>();
            _recorder.RecordingEnabled = false;
            _recorder.TransmitEnabled = false;
            _recorder.RecordWhenJoined = false;
            _recorder.DebugEchoMode = false;
            _recorder.MicrophoneType = Recorder.MicType.Unity;
            _recorder.UseMicrophoneTypeFallback = false;
            _recorder.StopRecordingWhenPaused = true;
            _client = gameObject.AddComponent<EchoVoiceClient>();
            _client.Owner = this;
            _client.PrimaryRecorder = _recorder;
            var mixer = Resources.Load<AudioMixer>("Voice/VoiceMixer");
            if (mixer != null)
            {
                var groups = mixer.FindMatchingGroups("Master");
                if (groups.Length > 0) _outputMixer = groups[0];
            }
            SelfMuted = PlayerPrefs.GetInt("Echo.Voice.Muted", 0) != 0;
            OutputVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Echo.Voice.Volume", 1));
            if (Enum.TryParse(PlayerPrefs.GetString("Echo.Voice.ToggleKey", "V"), out Key key) && key != Key.None) ToggleMicrophoneKey = key;
            gameObject.AddComponent<VoiceSettingsPanel>();
        }

        private void Update()
        {
            if (_focused && !_paused && !VoiceSettingsPanel.IsOpen && !PlayerInteractionControlLock.HasModal
                && Keyboard.current != null && Keyboard.current[ToggleMicrophoneKey].wasPressedThisFrame)
                ToggleMicrophone();

            var bootstrap = NetworkBootstrap.Instance;
            var runner = bootstrap != null ? bootstrap.Runner : null;
            NetworkObject local = null;
            bool valid = runner != null && runner.IsRunning && runner.SessionInfo.IsValid
                && runner.TryGetPlayerObject(runner.LocalPlayer, out local) && local != null && local.HasInputAuthority;
            string room = valid ? runner.SessionInfo.Name : null;
            string region = valid ? runner.SessionInfo.Region : null;
            string key = valid ? MakeKey(room, local.Id.Raw) : null;
            if (_runner != runner || _room != room || _region != region || _localKey != key)
            {
                ResetConnection();
                _muted.Clear();
                _runner = runner; _room = room; _region = region; _localKey = key;
                _attempts = 0; _nextAttempt = 0;
            }
            if (!valid) { Status = "Waiting for a Fusion session and local player"; UpdateCapture(false); return; }
            if (Joined)
            {
                if (_connecting) { _connecting = false; _attempts = 0; }
                Status = "Voice connected";
            }
            else if (_connecting)
            {
                if (_client.ClientState == ClientState.ConnectedToMasterServer && !_joinRequested)
                {
                    _joinRequested = true;
                    if (!_client.Client.OpJoinOrCreateRoom(new EnterRoomArgs
                    {
                        RoomName = _room,
                        RoomOptions = new RoomOptions { MaxPlayers = 4, IsVisible = false, PublishUserId = false }
                    })) FailAttempt();
                }
                if (Time.unscaledTime > _deadline || _client.ClientState == ClientState.Disconnected) FailAttempt();
            }
            else if (_client.ClientState == ClientState.Disconnected || _client.ClientState == ClientState.PeerCreated)
            {
                if (_attempts < 3 && Time.unscaledTime >= _nextAttempt) Connect();
            }
            else if (!Joined && _client.ClientState != ClientState.Disconnecting)
            {
                // Unexpected room departure: finish disconnecting before starting another attempt.
                FailAttempt();
            }
            UpdateCapture(Joined);
        }

        private void Connect()
        {
            var settings = new AppSettings();
            PhotonAppSettings.Instance.AppSettings.CopyTo(settings);
            if (!Guid.TryParse(settings.AppIdVoice, out _))
            {
                Status = "Voice App ID is missing. Configure Photon App Settings.";
                _attempts = 3;
                return;
            }
            if (string.IsNullOrEmpty(_region))
            {
                Status = "Fusion region is unavailable; voice connection postponed.";
                _nextAttempt = Time.unscaledTime + 2;
                return;
            }
            settings.FixedRegion = _region;
            settings.AppVersion = "EchoProtocol.Voice.1";
            _recorder.UserData = _localKey;
            _attempts++;
            _connecting = true; _joinRequested = false;
            _deadline = Time.unscaledTime + 20;
            Status = "Connecting voice (attempt " + _attempts + "/3)";
            if (!_client.ConnectUsingSettings(settings)) FailAttempt();
        }

        private void FailAttempt()
        {
            StopCapture();
            _connecting = false; _joinRequested = false;
            _client.Client.Disconnect();
            ClearSpeakers();
            _nextAttempt = Time.unscaledTime + Mathf.Pow(2, _attempts);
            Status = _attempts >= 3 ? "Voice connection failed. Retry when ready." : "Voice disconnected; retrying...";
        }

        private void UpdateCapture(bool connected)
        {
            bool canCapture = VoiceTransmissionRules.CanCapture(connected, MicrophoneEnabled, SelfMuted,
                Devices.Ready, Devices.Testing, _focused, _paused, VoiceSettingsPanel.IsOpen);
            if (!canCapture) { StopCapture(); return; }
            if (!_recorder.RecordingEnabled)
            {
                _recorder.MicrophoneDevice = new DeviceInfo(Devices.Selected);
                _recorder.RecordingEnabled = true;
                _captureStarted = Time.unscaledTime;
            }
            _recorder.VoiceDetection = false;
            _recorder.TransmitEnabled = canCapture;
            if (Time.unscaledTime - _captureStarted > 3 && !Microphone.IsRecording(Devices.Selected))
            {
                StopCapture(); Devices.ReportCaptureFailure();
            }
        }

        private void StopCapture()
        {
            if (_recorder == null) return;
            _recorder.TransmitEnabled = false;
            _recorder.RecordingEnabled = false;
        }

        private void ResetConnection()
        {
            StopCapture();
            Devices.StopTest();
            _connecting = false; _joinRequested = false;
            _client.Client.Disconnect();
            ClearSpeakers();
        }

        private void ClearSpeakers()
        {
            foreach (var speaker in GetComponentsInChildren<VoicePlayerBinding>())
            {
                speaker.GetComponent<AudioSource>().mute = true;
                speaker.enabled = false;
                Destroy(speaker.gameObject);
            }
        }

        public void RemovePreviousStream(string key)
        {
            foreach (var binding in GetComponentsInChildren<VoicePlayerBinding>())
                if (binding.Key == key)
                {
                    binding.GetComponent<AudioSource>().mute = true;
                    binding.enabled = false;
                    Destroy(binding.gameObject);
                }
        }

        public void Retry() { ResetConnection(); Devices.Refresh(); _attempts = 0; _nextAttempt = Time.unscaledTime + 1; }
        public void ToggleMicrophone()
        {
            if (MicrophoneEnabled && !SelfMuted) EnableMicrophone(false);
            else { SetMuted(false); EnableMicrophone(true); }
        }
        public void EnableMicrophone(bool enabled) { MicrophoneEnabled = enabled; if (!enabled) { StopCapture(); StopTest(); } }
        public void SetMuted(bool muted) { SelfMuted = muted; PlayerPrefs.SetInt("Echo.Voice.Muted", muted ? 1 : 0); if (muted) StopCapture(); }
        public void SetVolume(float volume) { OutputVolume = Mathf.Clamp01(volume); PlayerPrefs.SetFloat("Echo.Voice.Volume", OutputVolume); }
        public void SetToggleMicrophoneKey(Key key) { if (key == Key.None) return; ToggleMicrophoneKey = key; PlayerPrefs.SetString("Echo.Voice.ToggleKey", key.ToString()); }
        public void SelectDevice(string device) { StopCapture(); Devices.Select(device); }
        public void StartTest() { StopCapture(); Devices.StartTest(); }
        public void StopTest() => Devices.StopTest();
        public void SetPlayerMuted(string key, bool muted) { if (muted) _muted.Add(key); else _muted.Remove(key); }
        public bool IsPlayerMuted(string key) => _muted.Contains(key);
        public bool AcceptStream(string key) => Joined && key != _localKey && key != null && key.StartsWith(SessionPrefix(_room), StringComparison.Ordinal);
        public static string MakeKey(string room, uint id) => SessionPrefix(room) + id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        private static string SessionPrefix(string room) => Convert.ToBase64String(Encoding.UTF8.GetBytes(room ?? "")) + ":";

        public NetworkObject FindPlayer(string key)
        {
            if (_runner == null || !_runner.IsRunning) return null;
            foreach (var player in _runner.ActivePlayers)
                if (_runner.TryGetPlayerObject(player, out var obj) && obj != null && MakeKey(_room, obj.Id.Raw) == key) return obj;
            return null;
        }

        private void OnApplicationFocus(bool focused) { _focused = focused; if (!focused) { StopCapture(); StopTest(); } }
        private void OnApplicationPause(bool paused) { _paused = paused; if (paused) { StopCapture(); StopTest(); } }
        private void OnDisable() { if (_client != null) ResetConnection(); }
        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
