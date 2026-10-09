using System.Collections.Generic;
using System;
using System.Text;
using EchoProtocol.Audio;
using EchoProtocol.Networking;
using EchoProtocol.Settings;
using EchoProtocol.UI;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EchoProtocol.Voice
{
    /// <summary>Shared ESC settings for the lobby and gameplay; owns its modal input lock.</summary>
    public sealed class VoiceSettingsPanel : MonoBehaviour
    {
        private const string PrefabPath = "Voice/VoiceSettingsCanvas";
        private const string Body = "Overlay/Window/Body/";
        private const string Footer = "Overlay/Window/Footer/";
        private readonly PlayerInteractionControlLock _controlLock = new PlayerInteractionControlLock();
        private readonly System.Collections.Generic.Dictionary<string, Component> _components =
            new System.Collections.Generic.Dictionary<string, Component>();
        private VoiceManager _voice;
        private GameObject _canvas, _overlay, _confirm;
        private Transform _deviceList, _teamList;
        private Image[] _meter;
        private string _deviceSignature, _teamSignature, _bindingMessage;
        private bool _open, _micRebinding, _confirmQuit;
        private GameplayAction? _rebindAction;
        private EventSystem _rebindEventSystem;
        private bool _navigationBeforeRebind;
        private PlayerInteractionControlLock _parentMenuLock;
        private Action _onClosed, _openInventory;
        private float _nextRefresh;
        private int _tab;
        public static bool IsOpen { get; private set; }
        private bool IsRebinding => _micRebinding || _rebindAction.HasValue;
        private bool CanOpenInventory => _openInventory != null && _parentMenuLock != null
            && PlayerInteractionControlLock.IsPlayerStateValid(_parentMenuLock.Player);

        private void Awake()
        {
            _voice = GetComponent<VoiceManager>();
            var prefab = Resources.Load<GameObject>(PrefabPath);
            // Old saved prefabs are upgraded by the factory until the Editor rebuilds them.
            _canvas = prefab != null && prefab.transform.Find("Overlay/Window/SettingsTabs") != null
                ? Instantiate(prefab, transform) : VoiceSettingsCanvasFactory.Create();
            _canvas.name = "VoiceSettingsCanvas";
            _canvas.transform.SetParent(transform, false);
            _overlay = _canvas.transform.Find("Overlay").gameObject;
            _confirm = _canvas.transform.Find("Overlay/Confirm").gameObject;
            var general = _canvas.transform.Find(Body + "Mouse");
            SettingsMenuWidgets.Label("LanguageTitle", general, 34, 174, 350, 30, "Ngôn ngữ", 19);
            SettingsMenuWidgets.Button("Language", general, 405, 172, 347, 32, "Tiếng Việt / English", false, 17);
            general.GetComponent<RectTransform>().sizeDelta = new Vector2(780, 216);
            var audioRect = _canvas.transform.Find(Body + "Audio").GetComponent<RectTransform>();
            audioRect.anchoredPosition = new Vector2(audioRect.anchoredPosition.x, -570);
            audioRect.sizeDelta = new Vector2(audioRect.sizeDelta.x, 168);
            string[] audioRows = { "Master", "Music", "Effects", "Voice" };
            for (int i = 0; i < audioRows.Length; i++)
                foreach (string suffix in new[] { "", "Title", "Value" })
                {
                    var row = audioRect.Find(audioRows[i] + suffix) as RectTransform;
                    if (row != null) row.anchoredPosition = new Vector2(row.anchoredPosition.x, -(54 + i * 25));
                }
            foreach (var label in _canvas.GetComponentsInChildren<TMP_Text>(true))
                if (label.transform.parent == audioRect && label.name != "Title" && label.name != "Icon")
                    label.fontSize = Mathf.Min(label.fontSize, 18);
            Bind();
            _overlay.SetActive(false);
        }

        private T Find<T>(string path) where T : Component
        {
            string key = typeof(T).Name + path;
            if (!_components.TryGetValue(key, out var component))
            {
                component = _canvas.transform.Find(path).GetComponent<T>();
                _components.Add(key, component);
            }
            return (T)component;
        }

        private TMP_Text TextAt(string path) => Find<TMP_Text>(path);
        private void Click(string path, UnityEngine.Events.UnityAction action) => Find<Button>(path).onClick.AddListener(action);

        private void Bind()
        {
            Click(Body + "Mouse/Language", () =>
            {
                CancelRebind();
                GameLanguage.Set(GameLanguage.Current == GameLocale.Vietnamese ? GameLocale.English : GameLocale.Vietnamese);
                RefreshVisuals();
            });
            Click("Overlay/Window/CloseButton", Close);
            Click(Footer + "Resume", Close);
            Click(Footer + "Inventory", () =>
            {
                if (!CanOpenInventory) return;
                var action = _openInventory;
                Close();
                action?.Invoke();
            });
            Click(Footer + "Leave", () => ShowConfirmation(false));
            Click(Footer + "Quit", () => ShowConfirmation(true));
            Click("Overlay/Confirm/Prompt/Cancel", () => _confirm.SetActive(false));
            Click("Overlay/Confirm/Prompt/Accept", ConfirmExit);
            string[] tabs = { "General", "Graphics", "Audio", "Controls" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                Click("Overlay/Window/SettingsTabs/" + tabs[i], () => SelectTab(index));
            }

            Find<Toggle>(Body + "Voice/MicToggle").onValueChanged.AddListener(enabled =>
            {
                if (enabled) _voice.SetMuted(false);
                _voice.EnableMicrophone(enabled);
            });
            Find<Toggle>(Body + "Voice/PushToTalkToggle").onValueChanged.AddListener(_voice.SetPushToTalk);
            Find<Slider>(Body + "Voice/VolumeSlider").onValueChanged.AddListener(GameAudioSettings.SetVoiceVolume);
            Click(Body + "Voice/KeyButton", () => { BeginRebind(); _micRebinding = true; RefreshVisuals(); });
            Click(Body + "Voice/DeviceButton", () => SelectTab(2));
            Click(Body + "Voice/AdvancedButton", () => SelectTab(2));
            _meter = _canvas.transform.Find(Body + "Voice/Meter").GetComponentsInChildren<Image>();

            Find<Slider>(Body + "Mouse/Sensitivity").onValueChanged.AddListener(value => GameplayInputSettings.MouseSensitivity = value);
            Find<Toggle>(Body + "Mouse/Invert").onValueChanged.AddListener(value => GameplayInputSettings.InvertY = value);
            Find<Toggle>(Body + "Mouse/Acceleration").onValueChanged.AddListener(value => GameplayInputSettings.MouseAcceleration = value);
            Find<Slider>(Body + "Audio/Master").onValueChanged.AddListener(GameAudioSettings.SetMasterVolume);
            Find<Slider>(Body + "Audio/Music").onValueChanged.AddListener(GameAudioSettings.SetMusicVolume);
            Find<Slider>(Body + "Audio/Effects").onValueChanged.AddListener(GameAudioSettings.SetEffectsVolume);
            Find<Slider>(Body + "Audio/Voice").onValueChanged.AddListener(GameAudioSettings.SetVoiceVolume);

            foreach (GameplayAction action in Enum.GetValues(typeof(GameplayAction)))
            {
                if (action == GameplayAction.Inventory)
                {
                    var controls = _canvas.transform.Find(Body + "Controls");
                    foreach (var suffix in new[] { "", "Icon", "Title", "Rule" })
                        controls.Find(action + suffix)?.gameObject.SetActive(false);
                    continue;
                }
                GameplayAction selected = action;
                Click(Body + "Controls/" + action, () => { BeginRebind(); _rebindAction = selected; RefreshVisuals(); });
            }
            Click(Body + "Devices/Refresh", () => { _voice.Devices.Refresh(); _deviceSignature = null; });
            Click(Body + "Devices/Test", () =>
            {
                if (_voice.Devices.Testing) _voice.StopTest();
                else _voice.StartTest();
                RefreshVisuals();
            });
            Click(Body + "Team/Retry", _voice.Retry);
            _deviceList = _canvas.transform.Find(Body + "Devices/List/Viewport/Content");
            _teamList = _canvas.transform.Find(Body + "Team/List/Viewport/Content");
            Click(Body + "Graphics/Quality", () => { GameGraphicsSettings.CycleQuality(); RefreshVisuals(); });
            Click(Body + "Graphics/Resolution", () => { GameGraphicsSettings.CycleResolution(); RefreshVisuals(); });
            Find<Toggle>(Body + "Graphics/Fullscreen").onValueChanged.AddListener(GameGraphicsSettings.SetFullscreen);
            Find<Toggle>(Body + "Graphics/VSync").onValueChanged.AddListener(GameGraphicsSettings.SetVSync);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (!_open)
            {
                if (keyboard != null && (SceneManager.GetActiveScene().name == NetworkBootstrap.LobbySceneName
                    || SceneManager.GetActiveScene().name == EchoProtocol.Core.GameConstants.SceneMainMenu)
                    && keyboard.escapeKey.wasPressedThisFrame && !PlayerInteractionControlLock.HasModal
                    && !PlayerInteractionControlLock.EscapeConsumedThisFrame && !IsTextInputSelected())
                    Open(gameObject);
                return;
            }
            if (_controlLock.ShouldAutoRelease()) { Close(); return; }
            if (keyboard != null && _controlLock.IsTopmost)
            {
                if (_confirm.activeSelf)
                {
                    if (_controlLock.ConsumeEscape()) _confirm.SetActive(false);
                }
                else if (IsRebinding) ReadBinding(keyboard);
                else if (_controlLock.ConsumeEscape()) { Close(); return; }
            }
            float level = Mathf.Clamp01(_voice.InputLevel);
            for (int i = 0; i < _meter.Length; i++)
                _meter[i].color = level > i / (float)_meter.Length
                    ? new Color(0.32f, 0.78f, 0.43f) : new Color(0.19f, 0.21f, 0.20f);
            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + 0.1f;
                RefreshVisuals();
            }
        }

        private void ReadBinding(Keyboard keyboard)
        {
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                _controlLock.ConsumeEscape();
                CancelRebind();
                RefreshVisuals();
                return;
            }
            foreach (var key in keyboard.allKeys)
            {
                if (!key.wasPressedThisFrame) continue;
                if (_micRebinding)
                {
                    if (key.keyCode == Key.None || GameplayInputSettings.IsKeyBound(key.keyCode))
                    {
                        _bindingMessage = "Phím này đang dùng cho điều khiển khác.";
                        RefreshVisuals();
                        return;
                    }
                    _voice.SetToggleMicrophoneKey(key.keyCode);
                    CancelRebind();
                }
                else if (_rebindAction.HasValue)
                {
                    if (!GameplayInputSettings.TrySetKey(_rebindAction.Value, key.keyCode, out var error))
                    {
                        _bindingMessage = error;
                        RefreshVisuals();
                        return;
                    }
                    CancelRebind();
                }
                RefreshVisuals();
                break;
            }
        }

        private void BeginRebind()
        {
            CancelRebind();
            _rebindEventSystem = EventSystem.current;
            if (_rebindEventSystem == null) return;
            _navigationBeforeRebind = _rebindEventSystem.sendNavigationEvents;
            _rebindEventSystem.SetSelectedGameObject(null);
            _rebindEventSystem.sendNavigationEvents = false;
        }

        private void CancelRebind()
        {
            if (_rebindEventSystem != null)
            {
                _rebindEventSystem.SetSelectedGameObject(null);
                _rebindEventSystem.sendNavigationEvents = _navigationBeforeRebind;
                _rebindEventSystem = null;
            }
            _micRebinding = false;
            _rebindAction = null;
            _bindingMessage = null;
        }

        public bool OpenFromGameplayMenu(PlayerInteractionControlLock menuLock, Action onClosed = null, Action openInventory = null)
        {
            if (_open || menuLock == null || !menuLock.IsTopmost || menuLock.Player == null) return false;
            _parentMenuLock = menuLock;
            _onClosed = onClosed;
            _openInventory = openInventory;
            if (Open(menuLock.Player)) return true;
            _parentMenuLock = null;
            _onClosed = _openInventory = null;
            return false;
        }

        public bool OpenFromMainMenu()
        {
            if (_open || PlayerInteractionControlLock.HasModal) return false;
            return Open(gameObject);
        }

        private bool Open(GameObject player)
        {
            EnsureEventSystem();
            _controlLock.Acquire(player, Close, allowWhileDowned: true);
            if (!_controlLock.IsLocked) return false;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            _open = IsOpen = true;
            _overlay.SetActive(true);
            _confirm.SetActive(false);
            _deviceSignature = _teamSignature = null;
            _voice.Devices.Refresh();
            Find<Button>(Footer + "Inventory").gameObject.SetActive(_openInventory != null);
            var runner = NetworkBootstrap.Instance != null ? NetworkBootstrap.Instance.Runner : null;
            Find<Button>(Footer + "Leave").gameObject.SetActive(runner != null && runner.IsRunning);
            SelectTab(0);
            return true;
        }

        public void CloseFromGameplayMenu(PlayerInteractionControlLock menuLock)
        {
            if (_parentMenuLock != menuLock) return;
            _onClosed = null;
            Close();
        }

        private void Close()
        {
            GameGraphicsSettings.RevertResolution();
            bool wasOpen = _open;
            var callback = _onClosed;
            _open = IsOpen = false;
            _parentMenuLock = null;
            _onClosed = _openInventory = null;
            CancelRebind();
            if (_overlay != null) _overlay.SetActive(false);
            if (_voice != null) _voice.StopTest();
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && _canvas != null && selected.transform.IsChildOf(_canvas.transform))
                EventSystem.current.SetSelectedGameObject(null);
            _controlLock.Release();
            if (!wasOpen) return;
            GameplayInputSettings.Save();
            GameAudioSettings.Save();
            GameGraphicsSettings.Save();
            callback?.Invoke();
        }

        private void SelectTab(int index)
        {
            _tab = index;
            CancelRebind();
            Place("Voice", index == 0 || index == 2, 0, 0);
            Place("Mouse", index == 0 || index == 3, 0, index == 0 ? 342 : 0);
            Place("Audio", index == 0 || index == 2, 0, index == 0 ? 570 : 342);
            Place("Controls", index == 0 || index == 3, 800, 0);
            Place("Devices", index == 2, 800, 0);
            Place("Team", index == 2, 800, 362);
            Place("Graphics", index == 1, 0, 0);
            Place("Display", index == 1, 800, 0);
            Place("ControlsNote", index == 3, 0, 228);
            string[] tabs = { "General", "Graphics", "Audio", "Controls" };
            for (int i = 0; i < tabs.Length; i++)
            {
                string path = "Overlay/Window/SettingsTabs/" + tabs[i];
                Find<HorrorFrameGraphic>(path).Configure(i == index
                    ? new Color(0.18f, 0.025f, 0.026f, 0.95f) : SettingsMenuWidgets.Ink, i == index);
                _canvas.transform.Find(path + "/Active").gameObject.SetActive(i == index);
                TextAt(path + "/Label").color = i == index ? new Color(1, 0.60f, 0.56f) : SettingsMenuWidgets.Muted;
            }
            RefreshVisuals();
        }

        private void Place(string name, bool visible, float x, float y)
        {
            var rect = Find<RectTransform>(Body + name);
            rect.gameObject.SetActive(visible);
            rect.anchoredPosition = new Vector2(x, -y);
        }

        private void RefreshVisuals()
        {
            TextAt(Body + "Mouse/Language/Label").text = GameLanguage.Current == GameLocale.Vietnamese ? "Tiếng Việt" : "English";
            if (!_open) return;
            bool mainMenu = SceneManager.GetActiveScene().name == EchoProtocol.Core.GameConstants.SceneMainMenu;
            TextAt(Footer + "Resume/Label").text = mainMenu ? GameLanguage.Choose("Đóng", "Close") : GameLanguage.Choose("TIẾP TỤC", "RESUME");
            Find<Button>(Footer + "Inventory").interactable = CanOpenInventory;
            SetToggle("Voice/MicToggle", _voice.MicrophoneEnabled && !_voice.SelfMuted);
            SetToggle("Voice/PushToTalkToggle", _voice.PushToTalk);
            TextAt(Body + "Voice/KeyTitle").text = _voice.PushToTalk ? "Phím nhấn giữ để nói" : "Phím bật / tắt mic";
            TextAt(Body + "Voice/KeyButton/Label").text = _micRebinding ? "NHẤN PHÍM MỚI..." : KeyLabel(_voice.ToggleMicrophoneKey);
            TextAt(Body + "Voice/DeviceButton/Label").text = string.IsNullOrEmpty(_voice.Devices.Selected)
                ? "CHỌN MICROPHONE..." : _voice.Devices.Selected;
            SetVolume("Voice/VolumeSlider", "Voice/VolumeText", _voice.OutputVolume);
            SetVolume("Audio/Master", "Audio/MasterValue", GameAudioSettings.MasterVolume);
            SetVolume("Audio/Music", "Audio/MusicValue", GameAudioSettings.MusicVolume);
            SetVolume("Audio/Effects", "Audio/EffectsValue", GameAudioSettings.EffectsVolume);
            SetVolume("Audio/Voice", "Audio/VoiceValue", _voice.OutputVolume);
            Find<Slider>(Body + "Mouse/Sensitivity").SetValueWithoutNotify(GameplayInputSettings.MouseSensitivity);
            TextAt(Body + "Mouse/SensitivityText").text = GameplayInputSettings.MouseSensitivity.ToString("0.00");
            SetToggle("Mouse/Invert", GameplayInputSettings.InvertY);
            SetToggle("Mouse/Acceleration", GameplayInputSettings.MouseAcceleration);
            foreach (GameplayAction action in Enum.GetValues(typeof(GameplayAction)))
            {
                if (action == GameplayAction.Inventory) continue;
                TextAt(Body + "Controls/" + action + "/Label").text = _rebindAction == action
                    ? "NHẤN PHÍM..." : GameplayInputSettings.GetKeyLabel(action);
            }
            TextAt(Body + "Controls/Hint").text = !string.IsNullOrEmpty(_bindingMessage) ? _bindingMessage
                : IsRebinding ? "Nhấn phím mới  ·  ESC để hủy" : "Chọn một phím để thay đổi  ·  ESC để hủy";
            TextAt(Footer + "Saved").text = !string.IsNullOrEmpty(_bindingMessage) && _micRebinding ? _bindingMessage
                : IsRebinding ? "ESC  ·  HỦY ĐỔI PHÍM" : mainMenu ? GameLanguage.Choose("ESC  ·  QUAY LẠI MENU", "ESC  ·  RETURN TO MENU") : "ESC  ·  QUAY LẠI GAME";
            if (_tab == 1)
            {
                TextAt(Body + "Graphics/Quality/Label").text = GameGraphicsSettings.QualityLabel;
                TextAt(Body + "Graphics/Resolution/Label").text = GameGraphicsSettings.ResolutionLabel;
                TextAt(Body + "Display/Monitor/Quality").text = GameGraphicsSettings.QualityLabel;
                TextAt(Body + "Display/Monitor/Resolution").text = GameGraphicsSettings.ResolutionLabel;
                SetToggle("Graphics/Fullscreen", GameGraphicsSettings.Fullscreen);
                SetToggle("Graphics/VSync", GameGraphicsSettings.VSync);
                Find<Button>(Body + "Graphics/Resolution").interactable = GameGraphicsSettings.CanChangeResolution && !GameGraphicsSettings.HasResolutionPreview;
                Find<Toggle>(Body + "Graphics/Fullscreen").interactable = GameGraphicsSettings.CanChangeFullscreen && !GameGraphicsSettings.HasResolutionPreview;
                TextAt(Body + "Graphics/Hint").text = Application.isEditor
                    ? "Độ phân giải / toàn màn hình thay đổi trong bản game."
                    : "Chọn giá trị để chuyển sang tùy chọn tiếp theo.";
            }
            if (_tab != 2) return;
            TextAt(Body + "Devices/DeviceText").text = !string.IsNullOrEmpty(_voice.Devices.Error) ? "Không thể thu mic. Kiểm tra thiết bị và chọn lại."
                : string.IsNullOrEmpty(_voice.Devices.Selected) ? "Chọn microphone để bắt đầu nói" : _voice.Devices.Selected;
            TextAt(Body + "Devices/Test/Label").text = _voice.Devices.Testing ? "DỪNG THỬ MIC" : "THỬ MICROPHONE";
            Find<Button>(Body + "Devices/Test").interactable = _voice.Devices.Ready;
            TextAt(Body + "Devices/LevelText").text = "Mức đầu vào: " + Mathf.RoundToInt(_voice.InputLevel * 100) + "%";
            TextAt(Body + "Team/Status").text = _voice.Joined ? "Đã kết nối voice trong phòng"
                : _voice.Status.StartsWith("Waiting") ? "Vào phòng để kết nối voice" : "Voice chưa kết nối. Có thể thử kết nối lại.";
            Find<Button>(Body + "Team/Retry").gameObject.SetActive(!_voice.Joined);
            RefreshDevices();
            RefreshTeam();
        }

        private void SetVolume(string slider, string label, float value)
        {
            Find<Slider>(Body + slider).SetValueWithoutNotify(value);
            TextAt(Body + label).text = Mathf.RoundToInt(value * 100) + "%";
        }

        private void SetToggle(string path, bool value)
        {
            var toggle = Find<Toggle>(Body + path);
            toggle.SetIsOnWithoutNotify(value);
            toggle.transform.Find("Track/On/Dot").gameObject.SetActive(value);
            toggle.transform.Find("Track/OffDot").gameObject.SetActive(!value);
            TextAt(Body + path + "/Value").text = value ? "Bật" : "Tắt";
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
                VoiceSettingsCanvasFactory.AddListButton(_deviceList, "NoDevice", "Không tìm thấy microphone.", false).interactable = false;
                return;
            }
            foreach (string device in devices)
            {
                string selected = device;
                bool current = selected == _voice.Devices.Selected;
                VoiceSettingsCanvasFactory.AddListButton(_deviceList, "Device", (current ? "✓  " : "") + selected, current)
                    .onClick.AddListener(() => { _voice.SelectDevice(selected); _deviceSignature = null; });
            }
        }

        private void RefreshTeam()
        {
            var players = new List<Fusion.NetworkObject>(_voice.GetRemotePlayers());
            var signature = new StringBuilder();
            foreach (var player in players)
            {
                if (player != null) signature.Append(_voice.GetPlayerKey(player)).Append(NameFor(player)).Append(_voice.IsPlayerMuted(_voice.GetPlayerKey(player)));
            }
            string value = signature.ToString();
            if (value == _teamSignature) return;
            _teamSignature = value;
            ClearRows(_teamList);
            int count = 0;
            foreach (var player in players)
            {
                if (player == null) continue;
                count++;
                string key = _voice.GetPlayerKey(player);
                bool muted = _voice.IsPlayerMuted(key);
                VoiceSettingsCanvasFactory.AddListButton(_teamList, "Teammate", (muted ? "Bật tiếng  ·  " : "Tắt tiếng  ·  ") + NameFor(player), muted)
                    .onClick.AddListener(() => { _voice.SetPlayerMuted(key, !_voice.IsPlayerMuted(key)); _teamSignature = null; });
            }
            if (count == 0) VoiceSettingsCanvasFactory.AddListButton(_teamList, "EmptyTeam", "Chưa có đồng đội trong voice.", false).interactable = false;
        }

        private void ShowConfirmation(bool quit)
        {
            CancelRebind();
            GameUIFeedback.Instance.Confirm(GameLanguage.Choose(quit ? "Thoát game?" : "Rời phòng?", quit ? "Quit game?" : "Leave room?"),
                () => NetworkBootstrap.Instance != null && NetworkBootstrap.Instance.HasRunningRunner
                    ? GameUIFeedback.LeaveWarning() : GameLanguage.Choose("Bạn muốn đóng game?", "Do you want to close the game?"),
                () => { _confirmQuit = quit; ConfirmExit(); },
                GameLanguage.Choose(quit ? "Thoát game" : "Rời phòng", quit ? "Quit game" : "Leave room"));
        }

        private void ConfirmExit()
        {
            bool quit = _confirmQuit;
            Close();
            if (!quit)
            {
                var bootstrap = NetworkBootstrap.Instance;
                if (bootstrap != null) _ = bootstrap.Shutdown();
                return;
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static string KeyLabel(Key key) => key.ToString().Replace("Left", "").Replace("Right", "");
        private static bool IsTextInputSelected()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selected != null && selected.GetComponentInParent<TMP_InputField>() != null;
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
