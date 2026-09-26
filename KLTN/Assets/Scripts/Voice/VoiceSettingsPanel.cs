using System.Text;
using EchoProtocol.Networking;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace EchoProtocol.Voice
{
    /// <summary>Canvas presentation for voice controls in Lobby and gameplay.</summary>
    public sealed class VoiceSettingsPanel : MonoBehaviour
    {
        private const string PrefabPath = "Voice/VoiceSettingsCanvas";
        private readonly PlayerInteractionControlLock _controlLock = new PlayerInteractionControlLock();
        private VoiceManager _voice;
        private GameObject _canvas;
        private GameObject _overlay;
        private TMP_Text _hudLabel, _statusText, _micLabel, _inputHint, _deviceText;
        private TMP_Text _levelText, _volumeText, _testLabel, _keyLabel;
        private Image _levelFill, _micImage;
        private GameObject _retryButton;
        private Slider _volumeSlider;
        private Transform _deviceList, _teamList;
        private string _deviceSignature, _teamSignature;
        private bool _open, _rebinding;
        public static bool IsOpen { get; private set; }

        private void Awake()
        {
            _voice = GetComponent<VoiceManager>();
            var prefab = Resources.Load<GameObject>(PrefabPath);
            _canvas = prefab != null && prefab.transform.Find("Overlay/Window/StatusCard/RetryButton") != null
                ? Instantiate(prefab, transform) : VoiceSettingsCanvasFactory.Create();
            _canvas.name = "VoiceSettingsCanvas";
            _canvas.transform.SetParent(transform, false);
            Bind();
            RefreshVisuals();
        }

        private void Bind()
        {
            _overlay = _canvas.transform.Find("Overlay").gameObject;
            _hudLabel = Find<TMP_Text>("HudButton/Label");
            _statusText = Find<TMP_Text>("Overlay/Window/StatusCard/StatusText");
            _micLabel = Find<TMP_Text>("Overlay/Window/MicButton/Label");
            _micImage = Find<Image>("Overlay/Window/MicButton");
            _inputHint = Find<TMP_Text>("Overlay/Window/InputHint");
            _deviceText = Find<TMP_Text>("Overlay/Window/DeviceText");
            _levelText = Find<TMP_Text>("Overlay/Window/MicLevelText");
            _levelFill = Find<Image>("Overlay/Window/MicLevelTrack/MicLevelFill");
            _volumeText = Find<TMP_Text>("Overlay/Window/VolumeText");
            _testLabel = Find<TMP_Text>("Overlay/Window/TestButton/Label");
            _keyLabel = Find<TMP_Text>("Overlay/Window/KeyButton/Label");
            _volumeSlider = Find<Slider>("Overlay/Window/VolumeSlider");
            _retryButton = _canvas.transform.Find("Overlay/Window/StatusCard/RetryButton").gameObject;
            _deviceList = _canvas.transform.Find("Overlay/Window/Devices/Viewport/Content");
            _teamList = _canvas.transform.Find("Overlay/Window/TeamList/Viewport/Content");

            Find<Button>("HudButton").onClick.AddListener(Toggle);
            Find<Button>("Overlay/Window/CloseButton").onClick.AddListener(Close);
            Find<Button>("Overlay/Window/MicButton").onClick.AddListener(_voice.ToggleMicrophone);
            Find<Button>("Overlay/Window/RefreshButton").onClick.AddListener(() => { _voice.Devices.Refresh(); _deviceSignature = null; });
            Find<Button>("Overlay/Window/TestButton").onClick.AddListener(() =>
            {
                if (_voice.Devices.Testing) _voice.StopTest();
                else _voice.StartTest();
            });
            Find<Button>("Overlay/Window/StatusCard/RetryButton").onClick.AddListener(_voice.Retry);
            Find<Button>("Overlay/Window/KeyButton").onClick.AddListener(() => _rebinding = true);
            _volumeSlider.onValueChanged.AddListener(_voice.SetVolume);
        }

        private T Find<T>(string path) where T : Component => _canvas.transform.Find(path).GetComponent<T>();

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (_open && (_controlLock.ShouldAutoRelease() || (!_rebinding && _controlLock.ConsumeEscape())))
            {
                Close();
                return;
            }
            if (keyboard != null)
            {
                if (_rebinding)
                {
                    foreach (var key in keyboard.allKeys)
                    {
                        if (!key.wasPressedThisFrame) continue;
                        if (key.keyCode != Key.Escape && key.keyCode != Key.F8)
                            _voice.SetToggleMicrophoneKey(key.keyCode);
                        _rebinding = false;
                        break;
                    }
                }
                else if (keyboard.f8Key.wasPressedThisFrame && (_open || !PlayerInteractionControlLock.HasModal))
                    Toggle();
            }
            RefreshVisuals();
        }

        private void Toggle()
        {
            if (_open) Close();
            else Open();
        }

        private void Open()
        {
            if (PlayerInteractionControlLock.HasModal) return;
            EnsureEventSystem();
            var camera = FindAnyObjectByType<PlayerCamera>();
            _controlLock.Acquire(camera != null && camera.Target != null ? camera.Target.gameObject : gameObject, Close);
            _open = IsOpen = true;
            _overlay.SetActive(true);
            _deviceSignature = _teamSignature = null;
            _voice.Devices.Refresh();
            RefreshVisuals();
        }

        private void Close()
        {
            _open = IsOpen = false;
            _rebinding = false;
            if (_overlay != null) _overlay.SetActive(false);
            if (_voice != null) _voice.StopTest();
            _controlLock.Release();
        }

        private void RefreshVisuals()
        {
            if (_voice == null || _canvas == null) return;
            bool live = _voice.MicrophoneEnabled && !_voice.SelfMuted;
            _hudLabel.text = !live ? "MIC OFF  ·  F8 SETTINGS"
                : !_voice.Devices.Ready ? "MIC NEEDS SETUP  [F8]"
                : !_voice.Joined ? "VOICE WAITING  [F8]" : "MIC ON  ·  F8 SETTINGS";
            if (!_open) return;
            _statusText.text = _voice.Joined && !_voice.Devices.Ready ? "Connected. Select a microphone to talk."
                : _voice.Joined ? "Connected to room voice"
                : _voice.Status.StartsWith("Waiting") ? "Join a room to use voice chat" : _voice.Status;
            _retryButton.SetActive(!_voice.Joined && (_voice.Status.Contains("failed") || _voice.Status.Contains("disconnected")));
            _micLabel.text = live ? "TURN MICROPHONE OFF" : "TURN MICROPHONE ON";
            _micImage.color = live ? new Color(0.76f, 0.26f, 0.27f) : new Color(0.11f, 0.16f, 0.19f);
            _inputHint.text = live && !_voice.Devices.Ready ? "Choose a microphone below to start talking."
                : "Press " + _voice.ToggleMicrophoneKey + " to turn your mic on or off. No need to hold it.";
            _deviceText.text = !string.IsNullOrEmpty(_voice.Devices.Error) ? _voice.Devices.Error
                : string.IsNullOrEmpty(_voice.Devices.Selected) ? "No microphone selected"
                : "Selected microphone: " + _voice.Devices.Selected;
            float level = Mathf.Clamp01(_voice.InputLevel);
            _levelFill.fillAmount = level;
            _levelText.text = Mathf.RoundToInt(level * 100) + "%";
            _testLabel.text = _voice.Devices.Testing ? "STOP LOCAL TEST" : "TEST MIC";
            _keyLabel.text = _rebinding ? "PRESS A NEW KEY  ·  ESC TO CANCEL"
                : "CHANGE MIC SHORTCUT  ·  " + _voice.ToggleMicrophoneKey;
            _volumeSlider.SetValueWithoutNotify(_voice.OutputVolume);
            _volumeText.text = Mathf.RoundToInt(_voice.OutputVolume * 100) + "%";
            RefreshDevices();
            RefreshTeam();
        }

        private void RefreshDevices()
        {
            var devices = _voice.Devices.Devices;
            string signature = string.Join("|", devices) + ":" + _voice.Devices.Selected + ":" + _voice.Devices.Error;
            if (signature == _deviceSignature) return;
            _deviceSignature = signature;
            ClearRows(_deviceList);
            if (devices.Length == 0)
            {
                VoiceSettingsCanvasFactory.AddListButton(_deviceList, "NoDevice", "No microphone found. You can still listen.", false)
                    .interactable = false;
                return;
            }
            foreach (string device in devices)
            {
                string selected = device;
                bool current = selected == _voice.Devices.Selected;
                var button = VoiceSettingsCanvasFactory.AddListButton(_deviceList, "Device",
                    current ? "Selected: " + selected : "Select: " + selected, current);
                button.onClick.AddListener(() => { _voice.SelectDevice(selected); _deviceSignature = null; });
            }
        }

        private void RefreshTeam()
        {
            var bindings = _voice.GetComponentsInChildren<VoicePlayerBinding>();
            var signature = new StringBuilder();
            foreach (var binding in bindings)
            {
                var player = _voice.FindPlayer(binding.Key);
                if (player != null) signature.Append(binding.Key).Append(NameFor(player))
                    .Append(_voice.IsPlayerMuted(binding.Key) ? '1' : '0');
            }
            string value = signature.ToString();
            if (value == _teamSignature) return;
            _teamSignature = value;
            ClearRows(_teamList);
            int count = 0;
            foreach (var binding in bindings)
            {
                var player = _voice.FindPlayer(binding.Key);
                if (player == null) continue;
                count++;
                string key = binding.Key;
                bool muted = _voice.IsPlayerMuted(key);
                var button = VoiceSettingsCanvasFactory.AddListButton(_teamList, "Teammate",
                    (muted ? "Unmute " : "Mute ") + NameFor(player), muted);
                button.onClick.AddListener(() => { _voice.SetPlayerMuted(key, !_voice.IsPlayerMuted(key)); _teamSignature = null; });
            }
            if (count == 0)
                VoiceSettingsCanvasFactory.AddListButton(_teamList, "EmptyTeam", "No teammates in voice yet", false)
                    .interactable = false;
        }

        private static string NameFor(NetworkObject player)
        {
            var state = player.GetComponent<LobbyPlayerState>();
            string name = state != null ? state.OperatorName.ToString() : null;
            return string.IsNullOrWhiteSpace(name) ? player.InputAuthority.ToString() : name;
        }

        private static void ClearRows(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var owner = new GameObject("VoiceEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            owner.transform.SetParent(transform, false);
            owner.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private void OnDisable() => Close();
        private void OnDestroy()
        {
            Close();
            if (_canvas != null) Destroy(_canvas);
        }
    }
}
