using System;
using System.Collections.Generic;
using System.Text;
using EchoProtocol.Networking;
using EchoProtocol.Settings;
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
        public bool PushToTalk { get; private set; }
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
        private bool _pushToTalkHeld;
        private bool _outageNotified;
        public float RetryInSeconds => Joined || _connecting ? 0f : Mathf.Max(0f,_nextAttempt-Time.unscaledTime);

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
            Devices.DeviceChanged += StopCapture;
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
            MicrophoneEnabled = PlayerPrefs.GetInt("Echo.Voice.Enabled", 1) != 0;
            SelfMuted = PlayerPrefs.GetInt("Echo.Voice.Muted", 0) != 0;
            PushToTalk = PlayerPrefs.GetInt("Echo.Voice.PushToTalk", 0) != 0;
            OutputVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Echo.Voice.Volume", 1));
            RestoreMicrophoneKey();
            gameObject.AddComponent<VoiceSettingsPanel>();
        }

        private void RestoreMicrophoneKey()
        {
            if (Enum.TryParse(PlayerPrefs.GetString("Echo.Voice.ToggleKey", "V"), out Key saved)
                && IsMicrophoneKeyAvailable(saved)) ToggleMicrophoneKey = saved;
            else if (IsMicrophoneKeyAvailable(Key.V)) ToggleMicrophoneKey = Key.V;
            else
                foreach (Key candidate in Enum.GetValues(typeof(Key)))
                    if (IsMicrophoneKeyAvailable(candidate)) { ToggleMicrophoneKey = candidate; break; }
            PlayerPrefs.SetString("Echo.Voice.ToggleKey", ToggleMicrophoneKey.ToString());
        }

        private static bool IsMicrophoneKeyAvailable(Key key) => key != Key.None && key != Key.Escape
            && Enum.IsDefined(typeof(Key), key) && !GameplayInputSettings.IsKeyBound(key);

        private void Update()
        {
            bool canReadShortcut = _focused && !_paused && !VoiceSettingsPanel.IsOpen
                && !PlayerInteractionControlLock.HasModal && Keyboard.current != null;
            _pushToTalkHeld = canReadShortcut && PushToTalk && Keyboard.current[ToggleMicrophoneKey].isPressed;
            if (canReadShortcut && !PushToTalk && Keyboard.current[ToggleMicrophoneKey].wasPressedThisFrame)
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
                _attempts = 0; _nextAttempt = 0; _outageNotified=false;
            }
            if (!valid) { Status = "Waiting for a Fusion session and local player"; UpdateCapture(false); return; }
            if (Joined)
            {
                if (_connecting) { _connecting = false; _attempts = 0; }
                Status = "Voice connected";
                if (_outageNotified) {
                    _outageNotified=false;
                    EchoProtocol.UI.GameUIFeedback.Instance.Toast(GameLanguage.Choose(
                        "Đã kết nối lại thoại", "Voice reconnected"));
                }
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
                if (Time.unscaledTime >= _nextAttempt) Connect();
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
            if (!Fusion.Photon.Realtime.PhotonAppSettings.TryGetGlobal(out var fusionSettings)
                || fusionSettings == null)
            {
                Status = "Photon settings are unavailable.";
                ScheduleRetry();
                return;
            }
            fusionSettings.AppSettings.CopyTo(settings);
            if (!Guid.TryParse(settings.AppIdVoice, out _))
            {
                Status = "Voice App ID is missing. Configure AppIdVoice in Fusion PhotonAppSettings.";
                ScheduleRetry();
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
            _attempts=Mathf.Min(_attempts+1,5);
            _connecting = true; _joinRequested = false;
            _deadline = Time.unscaledTime + 20;
            Status = _attempts <= 3 ? "Connecting voice (attempt " + _attempts + "/3)" : "Reconnecting voice...";
            if (!_client.ConnectUsingSettings(settings)) FailAttempt();
        }

        private void FailAttempt()
        {
            StopCapture();
            _connecting = false; _joinRequested = false;
            _client.Client.Disconnect();
            ClearSpeakers();
            ScheduleRetry();
            Status = "Voice disconnected; retrying...";
        }

        private void ScheduleRetry()
        {
            // Missing service configuration also backs off instead of retrying every frame.
            if (_attempts==0) _attempts=3;
            _nextAttempt=Time.unscaledTime+VoiceRetryPolicy.DelaySeconds(_attempts);
            if (!_outageNotified) {
                _outageNotified=true;
                EchoProtocol.UI.GameUIFeedback.Instance.Toast(GameLanguage.Choose(
                    "Thoại mất kết nối · Đang tự kết nối lại", "Voice disconnected · Reconnecting automatically"));
            }
        }

        private void UpdateCapture(bool connected)
        {
            bool canCapture = VoiceTransmissionRules.CanCapture(connected, MicrophoneEnabled, SelfMuted,
                Devices.Ready, Devices.Testing, _focused, _paused, false);
            if (!canCapture) { StopCapture(); return; }
            if (!_recorder.RecordingEnabled)
            {
                _recorder.MicrophoneDevice = Devices.Selected == VoiceDeviceController.SystemDefault ? default(DeviceInfo) : new DeviceInfo(Devices.CaptureDevice);
                _recorder.RecordingEnabled = true;
                _captureStarted = Time.unscaledTime;
            }
            _recorder.VoiceDetection = true;
            _recorder.VoiceDetectionThreshold = 0.003f;
            _recorder.VoiceDetectionDelayMs = 800;
            _recorder.TransmitEnabled = canCapture && (!PushToTalk || _pushToTalkHeld);
            if (Time.unscaledTime - _captureStarted > 3 && !Microphone.IsRecording(Devices.CaptureDevice))
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
        public void EnableMicrophone(bool enabled) { MicrophoneEnabled = enabled; PlayerPrefs.SetInt("Echo.Voice.Enabled", enabled ? 1 : 0); if (!enabled) { StopCapture(); StopTest(); } }
        public void SetMuted(bool muted) { SelfMuted = muted; PlayerPrefs.SetInt("Echo.Voice.Muted", muted ? 1 : 0); if (muted) StopCapture(); }
        public void SetVolume(float volume) { OutputVolume = Mathf.Clamp01(volume); PlayerPrefs.SetFloat("Echo.Voice.Volume", OutputVolume); }
        public void SetPushToTalk(bool enabled)
        {
            PushToTalk = enabled;
            _pushToTalkHeld = false;
            PlayerPrefs.SetInt("Echo.Voice.PushToTalk", enabled ? 1 : 0);
            StopCapture();
        }
        public void SetToggleMicrophoneKey(Key key)
        {
            if (!IsMicrophoneKeyAvailable(key)) return;
            ToggleMicrophoneKey = key;
            PlayerPrefs.SetString("Echo.Voice.ToggleKey", key.ToString());
        }
        public void SelectDevice(string device) { StopCapture(); Devices.Select(device); }
        public void StartTest() { StopCapture(); Devices.StartTest(); }
        public void StopTest() => Devices.StopTest();
        public void SetPlayerMuted(string key, bool muted) { if (muted) _muted.Add(key); else _muted.Remove(key); }
        public bool IsPlayerMuted(string key) => _muted.Contains(key);
        public bool AcceptStream(string key) => Joined && key != _localKey && key != null && key.StartsWith(SessionPrefix(_room), StringComparison.Ordinal);
        public static string MakeKey(string room, uint id) => SessionPrefix(room) + id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        private static string SessionPrefix(string room) => Convert.ToBase64String(Encoding.UTF8.GetBytes(room ?? "")) + ":";

        public IEnumerable<NetworkObject> GetRemotePlayers()
        {
            if (_runner == null || !_runner.IsRunning) yield break;
            foreach (var player in _runner.ActivePlayers)
                if (_runner.TryGetPlayerObject(player, out var obj) && obj != null && !obj.HasInputAuthority)
                    yield return obj;
        }
        public string GetPlayerKey(NetworkObject player) => MakeKey(_room, player.Id.Raw);

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
        private void OnDestroy() { if (Devices != null) Devices.DeviceChanged -= StopCapture; if (Instance == this) Instance = null; }
    }
}
