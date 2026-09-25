using UnityEngine;
using UnityEngine.InputSystem;

namespace EchoProtocol.Voice
{
    /// <summary>Compact runtime settings available in Lobby and gameplay, without scene references.</summary>
    public sealed class VoiceSettingsPanel : MonoBehaviour
    {
        private VoiceManager _voice;
        private bool _open, _rebinding;
        public static bool IsOpen { get; private set; }
        private Rect _window = new Rect(20, 70, 390, 520);
        private Vector2 _scroll;
        private readonly PlayerInteractionControlLock _controlLock = new PlayerInteractionControlLock();

        private void Awake() => _voice = GetComponent<VoiceManager>();
        private void Update()
        {
            var keyboard = Keyboard.current;
            if (_open && (_controlLock.ShouldAutoRelease() || (!_rebinding && _controlLock.ConsumeEscape())))
            {
                Close();
                return;
            }
            if (keyboard == null) return;
            if (_rebinding)
            {
                foreach (var key in keyboard.allKeys)
                    if (key.wasPressedThisFrame) { if (key.keyCode != Key.Escape && key.keyCode != Key.F8) _voice.SetPushToTalk(key.keyCode); _rebinding = false; break; }
            }
            else if (keyboard.f8Key.wasPressedThisFrame && (_open || !PlayerInteractionControlLock.HasModal)) Toggle();
        }

        private void Toggle()
        {
            if (!_open && PlayerInteractionControlLock.HasModal) return;
            _open = !_open;
            IsOpen = _open;
            if (_open)
            {
                var camera = FindAnyObjectByType<PlayerCamera>();
                _controlLock.Acquire(camera != null && camera.Target != null ? camera.Target.gameObject : gameObject, Close);
            }
            else Close();
        }

        private void Close()
        {
            _open = false;
            IsOpen = false;
            _rebinding = false;
            if (_voice != null) _voice.StopTest();
            _controlLock.Release();
        }

        private void OnGUI()
        {
            if (_voice == null) return;
            if (GUI.Button(new Rect(20, 20, 170, 30), "Voice settings [F8]")) Toggle();
            if (_open) _window = GUILayout.Window(827314, _window, Draw, "Voice chat");
        }

        private void Draw(int id)
        {
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(460));
            GUILayout.Label(_voice.Status);
            GUILayout.Label("Microphone");
            if (GUILayout.Button("Refresh devices")) _voice.Devices.Refresh();
            foreach (string device in _voice.Devices.Devices)
                if (GUILayout.Button((_voice.Devices.Selected == device ? "Selected: " : "") + device)) _voice.SelectDevice(device);
            if (_voice.Devices.Devices.Length == 0) GUILayout.Label("No microphone found. You can still listen.");
            if (!string.IsNullOrEmpty(_voice.Devices.Error)) GUILayout.Label(_voice.Devices.Error);
            bool enabled = GUILayout.Toggle(_voice.MicrophoneEnabled, "Enable microphone");
            if (enabled != _voice.MicrophoneEnabled) _voice.EnableMicrophone(enabled);
            bool muted = GUILayout.Toggle(_voice.SelfMuted, "Mute my microphone");
            if (muted != _voice.SelfMuted) _voice.SetMuted(muted);
            bool openMic = GUILayout.Toggle(_voice.OpenMic, "Open mic (off = push-to-talk)");
            if (openMic != _voice.OpenMic) _voice.SetOpenMic(openMic);
            if (GUILayout.Button(_rebinding ? "Press a key (Esc cancels)" : "Push-to-talk: " + _voice.PushToTalkKey)) _rebinding = true;
            GUILayout.Label("Mic level: " + Mathf.RoundToInt(_voice.InputLevel * 100) + "%" + (_voice.Speaking ? " — Speaking" : ""));
            if (GUILayout.Button(_voice.Devices.Testing ? "Stop local mic test" : "Test microphone locally (10 seconds)"))
            { if (_voice.Devices.Testing) _voice.StopTest(); else _voice.StartTest(); }
            GUILayout.Label("Voice volume");
            float volume = GUILayout.HorizontalSlider(_voice.OutputVolume, 0, 1);
            if (!Mathf.Approximately(volume, _voice.OutputVolume)) _voice.SetVolume(volume);
            foreach (var binding in _voice.GetComponentsInChildren<VoicePlayerBinding>())
            {
                var player = _voice.FindPlayer(binding.Key);
                if (player == null) continue;
                bool wasMuted = _voice.IsPlayerMuted(binding.Key);
                bool nowMuted = GUILayout.Toggle(wasMuted, "Mute " + player.InputAuthority + (binding.IsSpeaking ? " (speaking)" : ""));
                if (wasMuted != nowMuted) _voice.SetPlayerMuted(binding.Key, nowMuted);
            }
            if (GUILayout.Button("Retry voice connection")) _voice.Retry();
            GUILayout.Label("If capture fails: Windows Settings > Privacy & security > Microphone > Allow desktop apps.");
            GUILayout.EndScrollView();
            if (GUILayout.Button("Close")) Toggle();
            GUI.DragWindow(new Rect(0, 0, 390, 20));
        }

        private void OnDisable() { if (_open) Toggle(); }
    }
}
