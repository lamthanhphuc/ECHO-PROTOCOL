using System.Collections.Generic;
using System;
using System.Text;
using EchoProtocol.Audio;
using EchoProtocol.Networking;
using EchoProtocol.Settings;
using EchoProtocol.UI;
using EchoProtocol.UI.Credits;
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
        private Text _settingsTitle;
        private TMP_Text _voiceHeading, _mouseHeading, _controlsGuide;
        private const string Footer = "Overlay/Window/Footer/";
        private readonly PlayerInteractionControlLock _controlLock = new PlayerInteractionControlLock();
        private readonly System.Collections.Generic.Dictionary<string, Component> _components =
            new System.Collections.Generic.Dictionary<string, Component>();
        private readonly Dictionary<Graphic, float> _mainMenuGraphics = new Dictionary<Graphic, float>();
        private readonly List<Graphic> _menuGraphicsBuffer = new List<Graphic>();
        private Transform _mainMenuRoot, _mainMenuBackground;
        private CanvasGroup _mainMenuInputGroup;
        private bool _mainMenuInputGroupAdded, _mainMenuWasInteractable, _mainMenuBlockedRaycasts;

        private VoiceManager _voice;
        private GameObject _canvas, _overlay, _confirm;
        private Transform _deviceList, _teamList;
        private Image[] _meter;
        private string _deviceSignature, _teamSignature, _bindingMessage;
        private bool _open, _micRebinding, _confirmQuit;
        private bool _previewingCredits;
        private Button _creditsButton;
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
            VoiceSettingsCanvasFactory.EnsureWindowBackground(_canvas.transform.Find("Overlay/Window"));
            var window = _canvas.transform.Find("Overlay/Window");
            var subtitle = window.Find("Subtitle");
            if (subtitle != null) subtitle.gameObject.SetActive(false);
            var originalTitle = window.Find("Title");
            if (originalTitle != null) originalTitle.gameObject.SetActive(false);
            var settingsIcon = window.Find("SettingsIcon");
            if (settingsIcon != null) settingsIcon.gameObject.SetActive(false);
            var heading = SettingsMenuWidgets.Rect("ScreenHeading", window, 0, 15, 0, 42);
            heading.anchorMin = heading.anchorMax = new Vector2(0.5f, 1f);
            heading.pivot = new Vector2(0.5f, 1f);
            heading.anchoredPosition = new Vector2(0f, -15f);
            heading.sizeDelta = new Vector2(800f, 42f);
            _settingsTitle = heading.gameObject.AddComponent<Text>();
            UiTypographyTheme.ApplyScreenHeading(_settingsTitle);
            _settingsTitle.fontSize = 48;
            _settingsTitle.verticalOverflow = VerticalWrapMode.Overflow;
            _voiceHeading = window.Find("Body/Voice/Heading")?.GetComponent<TMP_Text>();
            _mouseHeading = window.Find("Body/Mouse/Heading")?.GetComponent<TMP_Text>();
            if (_voiceHeading != null) _voiceHeading.overflowMode = TextOverflowModes.Overflow;
            if (_mouseHeading != null) _mouseHeading.overflowMode = TextOverflowModes.Overflow;
            foreach (string path in new[]
            {
                "Voice/InputTitle", "Voice/VolumeTitle", "Voice/PushToTalkTitle", "Voice/KeyTitle",
                "Mouse/SensitivityTitle", "Mouse/InvertTitle", "Mouse/AccelerationTitle",
                "Controls/Heading", "ControlsNote/Heading", "Audio/Heading",
                "Graphics/Heading", "Display/Heading", "Devices/Heading", "Team/Heading"
            })
            {
                var label = _canvas.transform.Find(Body + path)?.GetComponent<TMP_Text>();
                if (label == null) continue;
                var size = label.rectTransform.sizeDelta;
                size.y = Mathf.Max(size.y, label.fontSize >= 27f ? 46f : 36f);
                label.rectTransform.sizeDelta = size;
                label.overflowMode = TextOverflowModes.Overflow;
            }
            _controlsGuide = _canvas.transform.Find(Body + "ControlsNote/Body")?.GetComponent<TMP_Text>();
            if (_controlsGuide != null)
            {
                var size = _controlsGuide.rectTransform.sizeDelta;
                size.y = Mathf.Max(size.y, 190f);
                _controlsGuide.rectTransform.sizeDelta = size;
            }
            SettingsMenuWidgets.Button("LanguageShortcut", window, 20, 15, 170, 42,
                GameLanguage.Current == GameLocale.Vietnamese ? "TIẾNG VIỆT" : "ENGLISH", false, 16);
            _creditsButton = SettingsMenuWidgets.Button("ShowAfterCredit", window, 204, 15, 206, 42,
                GameLanguage.Choose("Đội Ngũ Sản Xuất", "SHOW AFTER CREDIT"), false, 16);
            _creditsButton.onClick.AddListener(ShowAfterCredit);
            var closeButton = window.Find("CloseButton");
            closeButton.Find("Label").gameObject.SetActive(false);
            foreach (float angle in new[] { -45f, 45f })
            {
                var stroke = SettingsMenuWidgets.Panel("CloseStroke", closeButton, 21, 21, 18, 3, Color.white);
                stroke.pivot = new Vector2(.5f, .5f);
                stroke.localRotation = Quaternion.Euler(0, 0, angle);
                stroke.GetComponent<Image>().raycastTarget = false;
            }
            var cameraMotion = SettingsMenuWidgets.Card("CameraMotion", window.Find("Body"), 0, 192, 780, 158,
                "Chuyển động camera", "◐");
            SettingsMenuWidgets.Label("MotionTitle", cameraMotion, 34, 65, 350, 34, GameLanguage.Choose("Cường độ chuyển động", "Motion intensity"), 19);
            cameraMotion.Find("Heading").GetComponent<TMP_Text>().overflowMode = TextOverflowModes.Overflow;
            var motionSlider = SettingsMenuWidgets.Slider("Intensity", cameraMotion, 405, 65, 278);
            motionSlider.SetValueWithoutNotify(PlayerCameraSettings.Intensity);
            motionSlider.onValueChanged.AddListener(value => PlayerCameraSettings.Intensity = value);
            SettingsMenuWidgets.Label("ReduceTitle", cameraMotion, 34, 106, 350, 34, GameLanguage.Choose("Giảm chuyển động", "Reduce motion"), 19);
            SettingsMenuWidgets.Label("IntensityValue", cameraMotion, 695, 65, 57, 34, "", 18,
                align: TextAlignmentOptions.Right);
            var reduceToggle = SettingsMenuWidgets.Toggle("Reduce", cameraMotion, 520, 104);
            reduceToggle.SetIsOnWithoutNotify(PlayerCameraSettings.ReduceMotion);
            reduceToggle.onValueChanged.AddListener(value => PlayerCameraSettings.ReduceMotion = value);
            LayoutSettings(window);
            BuildQuickSettings();
            Bind();
            IndustrialUiStyle.Controls(_canvas.transform);
            SettingsMenuWidgets.ApplyTypography(_canvas.transform);
            foreach (var label in _canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                string key = label.name;
                bool hint = key == "Hint" || key == "Description" || key == "SaveHint"
                    || key == "DeviceText" || key == "LevelText" || key == "Status"
                    || key == "Saved" || key == "Message" || key == "ControlsGuide";
                label.color = hint ? IndustrialUiStyle.Muted : IndustrialUiStyle.Text;
            }
            foreach (var slider in _canvas.GetComponentsInChildren<Slider>(true))
                if (slider.fillRect != null && slider.fillRect.TryGetComponent<Graphic>(out var fill))
                    fill.color = IndustrialUiStyle.Blood;
            foreach (var toggle in _canvas.GetComponentsInChildren<Toggle>(true))
                if (toggle.graphic is SettingsIconGraphic toggleGraphic)
                    toggleGraphic.Configure(SettingsIcon.Pill, IndustrialUiStyle.Blood);
            var quit = Find<HorrorFrameGraphic>(Footer + "Quit");
            quit.Configure(SettingsMenuWidgets.Ink, false);
            Find<Button>(Footer + "Resume").gameObject.SetActive(false);
            var quitRect = Find<RectTransform>(Footer + "Quit");
            quitRect.anchorMin = quitRect.anchorMax = new Vector2(.5f, 1f);
            quitRect.pivot = new Vector2(.5f, 1f);
            quitRect.anchoredPosition = Vector2.zero;
            quitRect.sizeDelta = new Vector2(420f, 53f);
            var quitLabel = TextAt(Footer + "Quit/Label").rectTransform;
            quitLabel.anchorMin = Vector2.zero;
            quitLabel.anchorMax = Vector2.one;
            quitLabel.offsetMin = new Vector2(60f, 0f);
            quitLabel.offsetMax = new Vector2(-60f, 0f);
            _overlay.SetActive(false);
        }

        private void LayoutSettings(Transform window)
        {
            var tabs = (RectTransform)window.Find("SettingsTabs");
            tabs.anchoredPosition = new Vector2(0, -111);
            tabs.sizeDelta = new Vector2(1480, 50);
            tabs.Find("Graphics").gameObject.SetActive(false);
            int visibleIndex = 0;
            for (int i = 0; i < tabs.childCount; i++)
            {
                var tab = (RectTransform)tabs.GetChild(i);
                if (!tab.gameObject.activeSelf) continue;
                tab.anchoredPosition = new Vector2(visibleIndex++ * 496, 0);
                tab.sizeDelta = new Vector2(488, 50);
                ((RectTransform)tab.Find("Icon")).anchoredPosition = new Vector2(138, -13);
                var label = (RectTransform)tab.Find("Label");
                label.anchoredPosition = new Vector2(167, 0);
                label.sizeDelta = new Vector2(180, 50);
                ((RectTransform)tab.Find("Active")).sizeDelta = new Vector2(488, 2);
            }

            LayoutRect("Voice", height: 278);
            Find<Button>(Body + "Voice/DeviceButton").gameObject.SetActive(false);
            Find<Button>(Body + "Voice/AdvancedButton").gameObject.SetActive(false);
            var audioRect = Find<RectTransform>(Body + "Audio");
            // Voice output already has a slider in the microphone card.
            foreach (string name in new[] { "Voice", "VoiceTitle", "VoiceValue" })
                audioRect.Find(name)?.gameObject.SetActive(false);
            audioRect.sizeDelta = new Vector2(audioRect.sizeDelta.x, 214);
            string[] audioRows = { "Master", "Music", "Effects" };
            for (int i = 0; i < audioRows.Length; i++)
                foreach (string suffix in new[] { "", "Title", "Value" })
                {
                    var row = audioRect.Find(audioRows[i] + suffix) as RectTransform;
                    if (row != null) row.anchoredPosition = new Vector2(row.anchoredPosition.x, -(65 + i * 54));
                }
            LayoutRect("Devices", height: 278);
            LayoutRect("Devices/List", height: 106);
            var viewport = Find<RectTransform>(Body + "Devices/List/Viewport");
            SettingsMenuWidgets.Stretch(viewport);
            viewport.offsetMin = new Vector2(8, 4);
            viewport.offsetMax = new Vector2(-8, -4);
            if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();
            Find<RectTransform>(Body + "Devices/List").gameObject.SetActive(false);
            Find<RectTransform>(Body + "Devices/DeviceText").gameObject.SetActive(false);
            SettingsMenuWidgets.Button("SelectDevice", Find<RectTransform>(Body + "Devices"), 27, 59, 626, 38, "Chọn micrô");
            LayoutRect("Devices/Test", top: 216, height: 30);
            LayoutRect("Devices/LevelText", top: 216, height: 30);
            foreach (string buttonPath in new[] { "Devices/Test", "Devices/Refresh", "Team/Retry" })
            {
                var button = Find<Button>(Body + buttonPath);
                IndustrialUiStyle.Button(button);
                var label = button.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (label == null) continue;
                SettingsMenuWidgets.Stretch(label.rectTransform);
                label.rectTransform.offsetMin = new Vector2(8f, 0f);
                label.rectTransform.offsetMax = new Vector2(-8f, 0f);
                label.alignment = TMPro.TextAlignmentOptions.Center;
            }

            LayoutRect("Team", height: 280);
            LayoutRect("Team/List", height: 94);
            LayoutRect("Team/Retry", top: 241, height: 31);

            foreach (string card in new[] { "Voice", "Audio", "Devices", "Team" })
            {
                var frame = Find<RectTransform>(Body + card);
                frame.sizeDelta = new Vector2(730, frame.sizeDelta.y);
                var rule = Find<RectTransform>(Body + card + "/Rule");
                rule.sizeDelta = new Vector2(684, rule.sizeDelta.y);
            }
            foreach (string path in new[] { "Voice/VolumeText", "Voice/Meter", "Voice/VolumeSlider",
                "Voice/MicToggle", "Voice/PushToTalkToggle", "Voice/KeyButton", "Audio/MasterValue",
                "Audio/MusicValue", "Audio/EffectsValue", "Audio/Master", "Audio/Music", "Audio/Effects" })
                Find<RectTransform>(Body + path).anchoredPosition -= new Vector2(50, 0);

            foreach (string name in audioRows)
            {
                var title = TextAt(Body + "Audio/" + name + "Title");
                title.rectTransform.sizeDelta = new Vector2(300, 42);
                title.rectTransform.anchoredPosition += new Vector2(0, 6);
                title.overflowMode = TextOverflowModes.Overflow;
            }

            foreach (string card in new[] { "Graphics", "Display" })
            {
                var frame = Find<RectTransform>(Body + card);
                frame.sizeDelta = new Vector2(730, 390);
                var line = Find<RectTransform>(Body + card + "/Rule");
                line.sizeDelta = new Vector2(684, line.sizeDelta.y);
            }
            foreach (string name in new[] { "Quality", "Resolution", "Fullscreen", "VSync", "Hint" })
            {
                var row = Find<RectTransform>(Body + "Graphics/" + name);
                if (name == "Hint") row.sizeDelta = new Vector2(662, row.sizeDelta.y);
                else row.anchoredPosition -= new Vector2(25, 0);
            }
            foreach (string name in new[] { "Monitor", "Stand", "Base", "SaveHint" })
                Find<RectTransform>(Body + "Display/" + name).anchoredPosition += new Vector2(25, 0);

            LayoutRect("Display/Monitor", top: 76, height: 191);
            LayoutRect("Display/Monitor/Resolution", top: 26, height: 65);
            LayoutRect("Display/Monitor/Quality", top: 103, height: 55);
            LayoutRect("Display/Stand", top: 267, height: 32);
            LayoutRect("Display/Base", top: 299);
            Find<RectTransform>(Body + "Display/Description").gameObject.SetActive(false);
            LayoutRect("Display/SaveHint", top: 328);

            LayoutRect("ControlsNote", height: 278);
            LayoutRect("ControlsNote/Body", top: 69, height: 190);
            var controlsNote = TextAt(Body + "ControlsNote/Body");
            controlsNote.rectTransform.sizeDelta = new Vector2(662, 190);
            controlsNote.textWrappingMode = TextWrappingModes.Normal;
            controlsNote.lineSpacing = 5;
            LayoutRect("Controls", height: 640);
            LayoutRect("Controls/Hint", top: 592);
        }

        private void LayoutRect(string path, float? top = null, float? height = null)
        {
            var rect = Find<RectTransform>(Body + path);
            if (top.HasValue)
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -top.Value);
            if (height.HasValue)
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, height.Value);
        }

        private void BuildQuickSettings()
        {
            var body = _canvas.transform.Find(Body.TrimEnd('/'));
            var audio = SettingsMenuWidgets.Frame("QuickAudio", body, 0, 0, 730, 322);
            var controls = SettingsMenuWidgets.Frame("QuickControls", body, 750, 0, 730, 322);

            foreach (var panel in new[] { audio, controls })
            {
                SettingsMenuWidgets.Label("Heading", panel, 32, 19, 666, 45, "", 26, bold: true);
                for (int i = 0; i < 3; i++)
                {
                    float y = 76 + i * 54;
                    SettingsMenuWidgets.Line("Rule" + i, panel, 32, y, 666);
                    SettingsMenuWidgets.Label("Row" + i, panel, 32, y + 6, 340, 40, "");
                }
            }

            SettingsMenuWidgets.Slider("Master", audio, 380, 84, 240).onValueChanged.AddListener(GameAudioSettings.SetMasterVolume);
            SettingsMenuWidgets.Label("MasterValue", audio, 625, 82, 73, 42, "", 18, align: TextAlignmentOptions.Right);
            SettingsMenuWidgets.Toggle("Microphone", audio, 504, 139).onValueChanged.AddListener(SetMicrophoneEnabled);
            SettingsMenuWidgets.Toggle("PushToTalk", audio, 504, 193).onValueChanged.AddListener(_voice.SetPushToTalk);
            SettingsMenuWidgets.Slider("Sensitivity", controls, 380, 84, 240, .1f, 3f)
                .onValueChanged.AddListener(value => GameplayInputSettings.MouseSensitivity = value);
            SettingsMenuWidgets.Label("SensitivityValue", controls, 625, 82, 73, 42, "", 18, align: TextAlignmentOptions.Right);
            SettingsMenuWidgets.Slider("Motion", controls, 380, 138, 240)
                .onValueChanged.AddListener(value => PlayerCameraSettings.Intensity = value);
            SettingsMenuWidgets.Label("MotionValue", controls, 625, 136, 73, 42, "", 18, align: TextAlignmentOptions.Right);
            SettingsMenuWidgets.Toggle("Reduce", controls, 504, 193)
                .onValueChanged.AddListener(value => PlayerCameraSettings.ReduceMotion = value);

            SettingsMenuWidgets.Button("Details", audio, 32, 255, 285, 40, "", false, 16)
                .onClick.AddListener(() => SelectTab(2));
            SettingsMenuWidgets.Button("Details", controls, 32, 255, 285, 40, "", false, 16)
                .onClick.AddListener(() => SelectTab(3));
        }

        private void SetMicrophoneEnabled(bool enabled)
        {
            if (enabled) _voice.SetMuted(false);
            _voice.EnableMicrophone(enabled);
        }

        private void RefreshQuickSettings()
        {
            TextAt(Body + "QuickAudio/Heading").text = GameLanguage.Choose("Âm thanh và thoại", "Audio and voice");
            TextAt(Body + "QuickAudio/Row0").text = GameLanguage.Choose("Tổng âm lượng", "Master volume");
            TextAt(Body + "QuickAudio/Row1").text = GameLanguage.Choose("Micrô", "Microphone");
            TextAt(Body + "QuickAudio/Row2").text = GameLanguage.Choose("Nhấn giữ để nói", "Push to talk");
            TextAt(Body + "QuickAudio/Details/Label").text = GameLanguage.Choose("Cài đặt âm thanh →", "Audio settings →");
            SetVolume("QuickAudio/Master", "QuickAudio/MasterValue", GameAudioSettings.MasterVolume);
            SetToggle("QuickAudio/Microphone", _voice.MicrophoneEnabled && !_voice.SelfMuted);
            SetToggle("QuickAudio/PushToTalk", _voice.PushToTalk);

            TextAt(Body + "QuickControls/Heading").text = GameLanguage.Choose("Chuột và camera", "Mouse and camera");
            TextAt(Body + "QuickControls/Row0").text = GameLanguage.Choose("Độ nhạy chuột", "Mouse sensitivity");
            TextAt(Body + "QuickControls/Row1").text = GameLanguage.Choose("Cường độ chuyển động", "Motion intensity");
            TextAt(Body + "QuickControls/Row2").text = GameLanguage.Choose("Giảm chuyển động", "Reduce motion");
            TextAt(Body + "QuickControls/Details/Label").text = GameLanguage.Choose("Cài đặt điều khiển →", "Control settings →");
            Find<Slider>(Body + "QuickControls/Sensitivity").SetValueWithoutNotify(GameplayInputSettings.MouseSensitivity);
            TextAt(Body + "QuickControls/SensitivityValue").text = GameplayInputSettings.MouseSensitivity.ToString("0.00");
            SetVolume("QuickControls/Motion", "QuickControls/MotionValue", PlayerCameraSettings.Intensity);
            SetToggle("QuickControls/Reduce", PlayerCameraSettings.ReduceMotion);
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
        private GameObject _graphicsChoices;
        private void OpenGraphicsChoices(string setting, string[] choices, Action<int> select)
        {
            if (_graphicsChoices != null) { Destroy(_graphicsChoices); _graphicsChoices = null; }
            var source = Find<Button>(Body + "Graphics/" + setting);
            var template = Instantiate(source);
            template.gameObject.SetActive(false);
            var root = new GameObject("GraphicsChoices", typeof(RectTransform), typeof(Image));
            _graphicsChoices = root;
            var rect = (RectTransform)root.transform;
            rect.SetParent(source.transform, false);
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(0, -4);
            rect.sizeDelta = new Vector2(0, Mathf.Min(choices.Length, 6) * 44);
            source.transform.parent.SetAsLastSibling();
            root.GetComponent<Image>().color = new Color(.025f,.035f,.04f,1);
            IndustrialUiStyle.Frame(root);
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(rect, false);
            var viewRect = (RectTransform)viewport.transform;
            viewRect.anchorMin = Vector2.zero; viewRect.anchorMax = Vector2.one;
            viewRect.offsetMin = viewRect.offsetMax = Vector2.zero;
            var content = new GameObject("Content", typeof(RectTransform));
            var contentRect = (RectTransform)content.transform;
            contentRect.SetParent(viewRect, false);
            contentRect.anchorMin = new Vector2(0,1); contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(.5f,1);
            contentRect.sizeDelta = new Vector2(0, choices.Length * 44);
            var scroll = root.AddComponent<ScrollRect>();
            scroll.viewport = viewRect; scroll.content = contentRect; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            for (int i = 0; i < choices.Length; i++)
            {
                int selected = i;
                var row = Instantiate(template, contentRect, false);
                row.gameObject.SetActive(true);
                var nested = row.transform.Find("GraphicsChoices");
                if (nested != null) DestroyImmediate(nested.gameObject);
                var rowRect = (RectTransform)row.transform;
                rowRect.anchorMin = new Vector2(0,1); rowRect.anchorMax = Vector2.one;
                rowRect.pivot = new Vector2(.5f,1);
                rowRect.anchoredPosition = new Vector2(0,-i*44); rowRect.sizeDelta = new Vector2(0,44);
                var rowLabel = row.GetComponentInChildren<TMP_Text>(true);
                bool current = choices[i] == (setting == "Quality" ? GameGraphicsSettings.QualityLabel : GameGraphicsSettings.ResolutionLabel);
                rowLabel.text = (current ? "• " : "") + choices[i];
                rowLabel.color = IndustrialUiStyle.Text;
                row.onClick = new Button.ButtonClickedEvent();
                row.onClick.AddListener(() => { select(selected); Destroy(root); _graphicsChoices = null; RefreshVisuals(); });
            }
            source.transform.SetAsLastSibling();
            Destroy(template.gameObject);
        }

        private void Bind()
        {
            Click("Overlay/Window/LanguageShortcut", () =>
            {
                CancelRebind();
                if (_graphicsChoices != null) Destroy(_graphicsChoices);
                _graphicsChoices = null;
                GameLanguage.Set(GameLanguage.Current == GameLocale.Vietnamese ? GameLocale.English : GameLocale.Vietnamese);
                _deviceSignature = _teamSignature = null;
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

            Find<Toggle>(Body + "Voice/MicToggle").onValueChanged.AddListener(SetMicrophoneEnabled);
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
            Click(Body + "Devices/SelectDevice", () =>
            {
                var list = Find<RectTransform>(Body + "Devices/List").gameObject;
                list.SetActive(!list.activeSelf);
                RefreshVisuals();
            });
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
            Click(Body + "Graphics/Quality", () => OpenGraphicsChoices("Quality", GameGraphicsSettings.QualityChoices, GameGraphicsSettings.SelectQuality));
            Click(Body + "Graphics/Resolution", () => OpenGraphicsChoices("Resolution", GameGraphicsSettings.ResolutionChoices, GameGraphicsSettings.SelectResolution));
            Find<Toggle>(Body + "Graphics/Fullscreen").onValueChanged.AddListener(GameGraphicsSettings.SetFullscreen);
            Find<Toggle>(Body + "Graphics/VSync").onValueChanged.AddListener(GameGraphicsSettings.SetVSync);
        }

        private void Update()
        {
            if (_previewingCredits) return;
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
            if (_graphicsChoices != null)
            {
                if (_controlLock.ConsumeEscape())
                { Destroy(_graphicsChoices); _graphicsChoices = null; return; }
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
                    && !RectTransformUtility.RectangleContainsScreenPoint((RectTransform)_graphicsChoices.transform,
                        Mouse.current.position.ReadValue(), null))
                { Destroy(_graphicsChoices); _graphicsChoices = null; }
            }
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
            _controlLock.Acquire(player, Close, allowWhileDowned: true, allowWhileSpectating: _parentMenuLock != null);
            if (!_controlLock.IsLocked) return false;
            try
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                HideMainMenuUi();
                _open = IsOpen = true;
                _overlay.SetActive(true);
                _confirm.SetActive(false);
                _deviceSignature = _teamSignature = null;
                _voice.Devices.Refresh();
                Find<Button>(Footer + "Inventory").gameObject.SetActive(_openInventory != null);
                var runner = NetworkBootstrap.Instance != null ? NetworkBootstrap.Instance.Runner : null;
                Find<Button>(Footer + "Leave").gameObject.SetActive(_parentMenuLock == null && runner != null && runner.IsRunning);
                Find<Button>(Footer + "Quit").gameObject.SetActive(_parentMenuLock == null);
                SelectTab(0);
                return true;
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                Close();
                return false;
            }
        }

        public void CloseFromGameplayMenu(PlayerInteractionControlLock menuLock)
        {
            if (_parentMenuLock != menuLock) return;
            _onClosed = null;
            Close();
        }

        private void Close()
        {
            if (_graphicsChoices != null) { Destroy(_graphicsChoices); _graphicsChoices = null; }
            GameGraphicsSettings.RevertResolution();
            bool wasOpen = _open;
            var callback = _onClosed;
            _open = IsOpen = false;
            _parentMenuLock = null;
            _onClosed = _openInventory = null;
            CancelRebind();
            if (_overlay != null) _overlay.SetActive(false);
            RestoreMainMenuUi();
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

        private void HideMainMenuUi()
        {
            if (SceneManager.GetActiveScene().name != EchoProtocol.Core.GameConstants.SceneMainMenu) return;

            var menu = GameObject.Find("MainMenuCanvas");
            if (menu == null || menu.scene != SceneManager.GetActiveScene()) return;

            var background = menu.transform.Find("background");
            if (background == null)
            {
                Debug.LogWarning("[Settings] Main Menu background missing; menu UI was not masked.");
                return;
            }

            _mainMenuRoot = menu.transform;
            _mainMenuBackground = background;
            _mainMenuInputGroup = menu.GetComponent<CanvasGroup>();
            _mainMenuInputGroupAdded = _mainMenuInputGroup == null;
            if (_mainMenuInputGroupAdded) _mainMenuInputGroup = menu.AddComponent<CanvasGroup>();
            if (_mainMenuInputGroup == null) throw new InvalidOperationException("Main Menu input group could not be created.");
            _mainMenuWasInteractable = _mainMenuInputGroup.interactable;
            _mainMenuBlockedRaycasts = _mainMenuInputGroup.blocksRaycasts;
            _mainMenuInputGroup.interactable = false;
            _mainMenuInputGroup.blocksRaycasts = false;
            KeepMainMenuUiHidden();
            Canvas.willRenderCanvases += KeepMainMenuUiHidden;
        }

        private void KeepMainMenuUiHidden()
        {
            if (_mainMenuRoot == null) return;

            // CanvasRenderer alpha leaves layout and existing CanvasGroups alone.
            // Recheck before rendering so newly created menu content stays hidden.
            if (_mainMenuInputGroup != null)
            {
                _mainMenuInputGroup.interactable = false;
                _mainMenuInputGroup.blocksRaycasts = false;
            }
            foreach (Transform child in _mainMenuRoot)
            {
                if (child == _mainMenuBackground) continue;

                _menuGraphicsBuffer.Clear();
                child.GetComponentsInChildren(true, _menuGraphicsBuffer);
                foreach (var graphic in _menuGraphicsBuffer)
                {
                    if (graphic == null) continue;
                    if (!_mainMenuGraphics.ContainsKey(graphic))
                        _mainMenuGraphics.Add(graphic, graphic.canvasRenderer.GetAlpha());
                    graphic.canvasRenderer.SetAlpha(0f);
                }
            }
        }

        private void RestoreMainMenuUi()
        {
            Canvas.willRenderCanvases -= KeepMainMenuUiHidden;
            _mainMenuRoot = _mainMenuBackground = null;

            foreach (var state in _mainMenuGraphics)
            {
                if (state.Key == null) continue;
                state.Key.canvasRenderer.SetAlpha(state.Value);
            }
            if (_mainMenuInputGroup != null)
            {
                _mainMenuInputGroup.interactable = _mainMenuWasInteractable;
                _mainMenuInputGroup.blocksRaycasts = _mainMenuBlockedRaycasts;
                if (_mainMenuInputGroupAdded) Destroy(_mainMenuInputGroup);
            }

            _mainMenuGraphics.Clear();
            _menuGraphicsBuffer.Clear();
            _mainMenuInputGroup = null;
            _mainMenuInputGroupAdded = false;
        }

        private void SelectTab(int index)
        {
            _tab = index;
            Find<RectTransform>(Body + "Devices/List").gameObject.SetActive(false);
            if (_graphicsChoices != null) Destroy(_graphicsChoices);
            _graphicsChoices = null;
            CancelRebind();
            Place("QuickAudio", index == 0, 0, 402);
            Place("QuickControls", index == 0, 750, 402);
            Place("Voice", index == 2, 0, 0);
            Place("Mouse", index == 3, 0, 0);
            Place("Audio", index == 2, 0, 290);
            Place("Controls", index == 3, 800, 0);
            Place("Devices", index == 2, 750, 0);
            Place("Team", index == 2, 750, 290);
            Place("Graphics", index == 0, 0, 0);
            Place("Display", index == 0, 750, 0);
            Place("ControlsNote", index == 3, 0, 362);
            Place("CameraMotion", index == 3, 0, 192);
            string[] tabs = { "General", "Graphics", "Audio", "Controls" };
            for (int i = 0; i < tabs.Length; i++)
            {
                string path = "Overlay/Window/SettingsTabs/" + tabs[i];
                Find<HorrorFrameGraphic>(path).Configure(i == index
                    ? new Color(0.18f, 0.025f, 0.026f, 0.95f) : SettingsMenuWidgets.Ink, i == index);
                _canvas.transform.Find(path + "/Active").gameObject.SetActive(i == index);
                TextAt(path + "/Label").color = i == index ? IndustrialUiStyle.Text : IndustrialUiStyle.Muted;
            }
            RefreshVisuals();
        }

        private void Place(string name, bool visible, float x, float y)
        {
            var rect = Find<RectTransform>(Body + name);
            rect.gameObject.SetActive(visible);
            rect.anchoredPosition = new Vector2(x, -y);
        }

        private async void ShowAfterCredit()
        {
            if (_previewingCredits || !_open || !_creditsButton.gameObject.activeSelf) return;
            _previewingCredits = true;
            _creditsButton.interactable = false;
            try { await CreditsFlow.PlayPreviewAsync(); }
            catch (Exception error) { Debug.LogException(error); }
            finally
            {
                _previewingCredits = false;
                if (_creditsButton != null) _creditsButton.interactable = true;
            }
        }

        private void RefreshVisuals()
        {
            TextAt("Overlay/Window/LanguageShortcut/Label").text = GameLanguage.Current == GameLocale.Vietnamese ? "TIẾNG VIỆT" : "ENGLISH";
            var sceneName = SceneManager.GetActiveScene().name;
            _creditsButton.gameObject.SetActive(sceneName == EchoProtocol.Core.GameConstants.SceneMainMenu
                || sceneName == NetworkBootstrap.LobbySceneName);
            TextAt("Overlay/Window/ShowAfterCredit/Label").text = GameLanguage.Choose("Đội Ngũ Sản Xuất", "SHOW AFTER CREDIT");
            if (_settingsTitle != null) _settingsTitle.text = GameLanguage.Choose("CÀI ĐẶT", "SETTINGS");
            if (_voiceHeading != null) _voiceHeading.text = GameLanguage.Choose("MICRÔ", "MICROPHONE");
            if (_mouseHeading != null) _mouseHeading.text = GameLanguage.Choose("CHUỘT", "MOUSE");
            TextAt(Body + "ControlsNote/Heading").text = GameLanguage.Choose("Phím cố định", "Fixed controls");
            TextAt(Body + "CameraMotion/Heading").text = GameLanguage.Choose("Chuyển động camera", "Camera motion");
            if (_controlsGuide != null) _controlsGuide.text = VoiceSettingsCanvasFactory.ControlsGuide;
            if (!_open) return;
            bool mainMenu = SceneManager.GetActiveScene().name == EchoProtocol.Core.GameConstants.SceneMainMenu;
            TextAt(Footer + "Resume/Label").text = _parentMenuLock != null ? GameLanguage.Choose("QUAY LẠI", "BACK") : mainMenu ? GameLanguage.Choose("ĐÓNG", "CLOSE") : GameLanguage.Choose("TIẾP TỤC", "RESUME");
            Find<Button>(Footer + "Inventory").interactable = CanOpenInventory;
            if (_tab == 0) RefreshQuickSettings();
            SetToggle("Voice/MicToggle", _voice.MicrophoneEnabled && !_voice.SelfMuted);
            SetToggle("Voice/PushToTalkToggle", _voice.PushToTalk);
            TextAt(Body + "Voice/KeyTitle").text = _voice.PushToTalk
                ? GameLanguage.Choose("Phím nhấn giữ để nói", "Push-to-talk key")
                : GameLanguage.Choose("Phím bật / tắt mic", "Microphone toggle key");
            TextAt(Body + "Voice/KeyButton/Label").text = _micRebinding ? GameLanguage.Choose("NHẤN PHÍM MỚI...", "PRESS A NEW KEY...") : KeyLabel(_voice.ToggleMicrophoneKey);
            TextAt(Body + "Voice/DeviceButton/Label").text = string.IsNullOrEmpty(_voice.Devices.Selected)
                ? GameLanguage.Choose("CHỌN MICROPHONE...", "SELECT MICROPHONE...") : _voice.Devices.Selected;
            SetVolume("Voice/VolumeSlider", "Voice/VolumeText", _voice.OutputVolume);
            TextAt(Body + "Audio/MasterTitle").text = GameLanguage.Choose("Tổng âm lượng", "Master volume");
            TextAt(Body + "Audio/MusicTitle").text = GameLanguage.Choose("Nhạc nền", "Music");
            TextAt(Body + "Audio/EffectsTitle").text = GameLanguage.Choose("Hiệu ứng", "Sound effects");
            SetVolume("Audio/Master", "Audio/MasterValue", GameAudioSettings.MasterVolume);
            SetVolume("Audio/Music", "Audio/MusicValue", GameAudioSettings.MusicVolume);
            SetVolume("Audio/Effects", "Audio/EffectsValue", GameAudioSettings.EffectsVolume);
            SetVolume("Audio/Voice", "Audio/VoiceValue", _voice.OutputVolume);
            Find<Slider>(Body + "Mouse/Sensitivity").SetValueWithoutNotify(GameplayInputSettings.MouseSensitivity);
            TextAt(Body + "Mouse/SensitivityText").text = GameplayInputSettings.MouseSensitivity.ToString("0.00");
            SetToggle("Mouse/Invert", GameplayInputSettings.InvertY);
            SetToggle("Mouse/Acceleration", GameplayInputSettings.MouseAcceleration);
            SetVolume("CameraMotion/Intensity", "CameraMotion/IntensityValue", PlayerCameraSettings.Intensity);
            SetToggle("CameraMotion/Reduce", PlayerCameraSettings.ReduceMotion);
            foreach (GameplayAction action in Enum.GetValues(typeof(GameplayAction)))
            {
                if (action == GameplayAction.Inventory) continue;
                TextAt(Body + "Controls/" + action + "/Label").text = _rebindAction == action
                    ? GameLanguage.Choose("NHẤN PHÍM...", "PRESS A KEY...") : GameplayInputSettings.GetKeyLabel(action);
            }
            TextAt(Body + "Controls/Hint").text = !string.IsNullOrEmpty(_bindingMessage) ? _bindingMessage
                : IsRebinding ? GameLanguage.Choose("Nhấn phím mới  ·  ESC để hủy", "Press a new key  ·  ESC to cancel") : GameLanguage.Choose("Chọn một phím để thay đổi  ·  ESC để hủy", "Select a binding to change  ·  ESC to cancel");
            TextAt(Footer + "Saved").text = !string.IsNullOrEmpty(_bindingMessage) && _micRebinding ? _bindingMessage
                : IsRebinding ? GameLanguage.Choose("ESC  ·  HỦY ĐỔI PHÍM", "ESC  ·  CANCEL REBIND") : mainMenu ? GameLanguage.Choose("ESC  ·  QUAY LẠI MENU", "ESC  ·  RETURN TO MENU") : GameLanguage.Choose("ESC  ·  QUAY LẠI GAME", "ESC  ·  RETURN TO GAME");
            if (_tab == 0)
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
                    ? GameLanguage.Choose("Đổi độ phân giải và chế độ toàn màn hình trong bản game.",
                        "Change the resolution and fullscreen mode in the game build.")
                    : GameLanguage.Choose("Chọn một giá trị để mở danh sách tùy chọn.",
                        "Select a value to open the list of options.");
            }
            if (_tab != 2) return;
            TextAt(Body + "Devices/SelectDevice/Label").text = !string.IsNullOrEmpty(_voice.Devices.Error)
                ? _voice.Devices.Selected + " · " + GameLanguage.Choose("Không khả dụng. Kiểm tra thiết bị hoặc chọn lại.", "Unavailable. Check the device or select another.")
                : string.IsNullOrEmpty(_voice.Devices.Selected) ? GameLanguage.Choose("Chọn microphone để bắt đầu nói", "Select a microphone to start talking") : GameLanguage.Translate(_voice.Devices.Selected) + " ▾";
            TextAt(Body + "Devices/Test/Label").text = _voice.Devices.Testing ? GameLanguage.Choose("DỪNG THỬ MIC", "STOP MIC TEST") : GameLanguage.Choose("THỬ MICROPHONE", "TEST MICROPHONE");
            Find<Button>(Body + "Devices/Test").interactable = _voice.Devices.Ready;
            TextAt(Body + "Devices/LevelText").text = GameLanguage.Choose("Mức đầu vào: ", "Input level: ") + Mathf.RoundToInt(_voice.InputLevel * 100) + "%";
            TextAt(Body + "Team/Status").text = _voice.Joined ? GameLanguage.Choose("Đã kết nối thoại", "Voice connected")
                 : _voice.Status.StartsWith("Waiting") ? GameLanguage.Choose("Vào phòng để kết nối thoại", "Join a room to connect voice")
                : _voice.RetryInSeconds>0 ? string.Format(GameLanguage.Choose("Tự kết nối lại sau {0} giây", "Reconnecting automatically in {0}s"),Mathf.CeilToInt(_voice.RetryInSeconds))
                : GameLanguage.Choose("Đang kết nối thoại…", "Connecting voice…");
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
                VoiceSettingsCanvasFactory.AddListButton(_deviceList, "NoDevice", GameLanguage.Choose("Không tìm thấy microphone.", "No microphone found."), false).interactable = false;
                return;
            }
            foreach (string device in devices)
            {
                string selected = device;
                bool current = selected == _voice.Devices.Selected;
                VoiceSettingsCanvasFactory.AddListButton(_deviceList, "Device", (current ? "✓  " : "") + selected, current)
                    .onClick.AddListener(() => { _voice.SelectDevice(selected); _deviceSignature = null; Find<RectTransform>(Body + "Devices/List").gameObject.SetActive(false); RefreshVisuals(); });
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
                VoiceSettingsCanvasFactory.AddListButton(_teamList, "Teammate", (muted ? GameLanguage.Choose("Bật tiếng  ·  ", "Unmute  ·  ") : GameLanguage.Choose("Tắt tiếng  ·  ", "Mute  ·  ")) + NameFor(player), muted)
                    .onClick.AddListener(() => { _voice.SetPlayerMuted(key, !_voice.IsPlayerMuted(key)); _teamSignature = null; });
            }
            if (count == 0) VoiceSettingsCanvasFactory.AddListButton(_teamList, "EmptyTeam", GameLanguage.Choose("Chưa có đồng đội trong thoại.", "No teammates in voice chat."), false).interactable = false;
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
