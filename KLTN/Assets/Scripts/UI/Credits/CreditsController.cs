using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EchoProtocol.Audio;
using EchoProtocol.Settings;
using EchoProtocol.UI.Result;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EchoProtocol.UI.Credits
{
    [DisallowMultipleComponent, DefaultExecutionOrder(15000)]
    public sealed class CreditsController : MonoBehaviour
    {
        [Header("Credits")]
        [SerializeField] private CreditsData _data;
        [SerializeField] private TMP_FontAsset _nameFont, _roleFont;
        [SerializeField, Min(1f)] private float _fontSize = 52f;
        [SerializeField, Min(0f)] private float _sectionSpacing = 96f;
        [Header("Playback (1920 x 1080 design units)")]
        [SerializeField, Min(1f)] private float _scrollSpeed = 58f;
        [SerializeField, Min(0f)] private float _startDelay = 1.5f;
        [SerializeField, Min(0.01f)] private float _fadeDuration = 1.2f;
        [SerializeField, Min(0f)] private float _closingHold = 3f;
        [SerializeField, Min(0.1f)] private float _holdToSkipSeconds = 2f;
        [Header("Local Music")]
        [SerializeField] private AudioClip _backgroundMusic;
        [SerializeField, Range(0f, 1f)] private float _musicVolume = 0.14f;
        [Header("Scene References")]
        [SerializeField] private RectTransform _viewport, _content;
        [SerializeField] private CanvasGroup _rollingGroup;
        [SerializeField] private AudioSource _music;
        [SerializeField] private AudioListener _fallbackListener;
        [SerializeField] private TMP_Text _skipHint;
        [SerializeField] private Image _skipProgress;
        private RectTransform _thankYou;
        private readonly CreditsRollPlayback _playback = new();
        private readonly PlayerInteractionControlLock _inputLock = new();
        private readonly Dictionary<AudioSource, bool> _ducked = new();
        private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private float _startY, _endDistance, _nextAudioScan;
        private Vector2 _lastViewportSize;
        private bool _completionSent;
        public static CreditsController Active { get; private set; }
        public Task Completion => _completion.Task;
        public CreditsData Data => _data;
        public RectTransform Content => _content;
        public RectTransform Viewport => _viewport;
        public RectTransform ThankYou => _thankYou;
        public bool HasReferences => _data != null && _viewport != null && _content != null && _rollingGroup != null
            && _music != null && _fallbackListener != null && _skipHint != null && _skipProgress != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active = null;

        public void Configure(CreditsData data, TMP_FontAsset names, TMP_FontAsset roles, RectTransform viewport,
            RectTransform content, CanvasGroup rollingGroup, AudioSource music, AudioListener listener,
            TMP_Text skipHint, Image skipProgress, AudioClip backgroundMusic)
        {
            _data = data; _nameFont = names; _roleFont = roles; _viewport = viewport; _content = content;
            _rollingGroup = rollingGroup; _music = music; _fallbackListener = listener;
            _skipHint = skipHint; _skipProgress = skipProgress; _backgroundMusic = backgroundMusic;
        }

        private void Awake()
        {
            if (Active != null && Active != this) { Destroy(gameObject); return; }
            Active = this;
            DontDestroyOnLoad(gameObject);
            if (_fallbackListener != null) _fallbackListener.enabled = false;
        }

        private void OnEnable() => SceneManager.sceneLoaded += SceneLoaded;
        private void SceneLoaded(Scene scene, LoadSceneMode mode) => RefreshAudio();

        private void Start()
        {
            if (!HasReferences)
            {
                Debug.LogError("[Credits] AfterCredit has missing UI/data references.");
                _completion.TrySetException(new InvalidOperationException("Credits references are missing."));
                return;
            }
            RebuildContent();
            _rollingGroup.alpha = 0;
            _skipProgress.fillAmount = 0;
            _music.clip = _backgroundMusic; _music.loop = true; _music.spatialBlend = 0; _music.playOnAwake = false;
            GameAudioSettings.RouteMusic(_music);
            _music.volume = 0;
            if (_backgroundMusic != null) _music.Play();
            RefreshAudio();
            MaintainInput();
        }

        private void LateUpdate()
        {
            if (!HasReferences || _completionSent) return;
            MaintainInput();
            RefreshLayout();
            if (Time.unscaledTime >= _nextAudioScan) { _nextAudioScan = Time.unscaledTime + 0.3f; RefreshAudio(); }
            bool holding = Application.isFocused && Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
            _playback.Tick(Time.unscaledDeltaTime, holding, _scrollSpeed, _startDelay, _fadeDuration, _endDistance, _closingHold, _holdToSkipSeconds);
            _content.anchoredPosition = new Vector2(0, _startY + _playback.Distance);
            _rollingGroup.alpha = _playback.Opacity;
            _music.volume = _musicVolume * _playback.Opacity;
            _skipProgress.fillAmount = CreditsRollPlayback.HoldProgress(_playback.SkipHeldSeconds, _holdToSkipSeconds);
            _skipHint.text = GameLanguage.Choose("GIỮ SPACE ĐỂ BỎ QUA", "HOLD SPACE TO SKIP");
            if (_playback.Phase == CreditsRollPhase.Completed) Complete();
        }

        private void MaintainInput()
        {
            // The existing scene-unload driver releases modal locks during a host's Lobby transition.
            if (!_inputLock.IsLocked) _inputLock.Acquire(gameObject, unlockCursor: false, allowWhileDowned: true);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = false;
        }

        private async void Complete()
        {
            if (_completionSent) return;
            _completionSent = true;
            _skipHint.text = string.Empty;
            _completion.TrySetResult(true);
            if (CreditsFlow.IsResultPlayback) return;
            try { await CinematicResultSceneController.NavigateSessionToLobbyAsync(false); }
            catch (Exception error) { Debug.LogException(error); }
            finally { if (this != null) Destroy(gameObject); }
        }

        private void RefreshAudio()
        {
            if (!Application.isPlaying || _music == null) return;
            bool listenerAvailable = false;
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude))
                if (listener != _fallbackListener && listener.enabled) { listenerAvailable = true; break; }
            if (_fallbackListener != null) _fallbackListener.enabled = !listenerAvailable;
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude))
            {
                if (source == _music || source.GetComponentInParent<Photon.Voice.Unity.Speaker>(true) != null
                    || source.GetComponentInParent<EchoProtocol.Voice.VoiceManager>(true) != null
                    || source.GetComponentInParent<EchoProtocol.Voice.VoicePlayerBinding>(true) != null) continue;
                if (!_ducked.ContainsKey(source)) _ducked[source] = source.mute;
                source.mute = true;
            }
        }

        public void RebuildContent()
        {
            if (_data == null || _content == null) return;
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var child = _content.GetChild(i);
                child.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
            }
            var layout = _content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = _sectionSpacing;
            var branding = Block("Branding");
            if (_data.logo != null)
            {
                var image = new GameObject("ECHO PROTOCOL Logo", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                image.transform.SetParent(branding, false);
                image.GetComponent<Image>().sprite = _data.logo; image.GetComponent<Image>().preserveAspect = true;
                image.GetComponent<Image>().raycastTarget = false;
                image.GetComponent<LayoutElement>().preferredHeight = 270;
            }
            else Text("ProjectTitle", branding, _data.projectTitle, _fontSize * 1.25f, false);
            Text("ProjectCaption", branding, _data.projectTitle, _fontSize * 0.4f, true);
            if (_data.sections != null)
                foreach (var section in _data.sections)
                {
                    if (section == null || section.names == null || !Array.Exists(section.names, name => !string.IsNullOrWhiteSpace(name))) continue;
                    var block = Block("CreditsSection");
                    Text("Role", block, (section.role ?? string.Empty).ToUpperInvariant(), _fontSize * 0.48f, true);
                    foreach (var name in section.names)
                        if (!string.IsNullOrWhiteSpace(name)) Text("MemberName", block, name, _fontSize, false);
                    if (!string.IsNullOrWhiteSpace(section.contribution))
                        Text("Contribution", block, section.contribution, _fontSize * 0.38f, false, new Color(0.58f, 0.6f, 0.62f));
                }
            _thankYou = Text("ThankYouForPlaying", _content, _data.closingMessage, _fontSize * 0.8f, true).rectTransform;
            RefreshLayout(true);
        }

        private Transform Block(string name)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            obj.transform.SetParent(_content, false);
            var group = obj.GetComponent<VerticalLayoutGroup>(); group.spacing = 20;
            group.childAlignment = TextAnchor.UpperCenter; group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = true; group.childForceExpandHeight = false;
            obj.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return obj.transform;
        }

        private TMP_Text Text(string name, Transform parent, string value, float size, bool role, Color? color = null)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var label = obj.GetComponent<TMP_Text>(); label.text = value ?? string.Empty;
            label.font = (role ? _roleFont : _nameFont) ?? UiTypographyTheme.BodyTmp;
            label.fontSize = size; label.color = color ?? (role ? new Color(0.67f, 0.69f, 0.7f) : new Color(0.93f, 0.93f, 0.91f));
            label.alignment = TextAlignmentOptions.Center; label.richText = false; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            if (role) label.characterSpacing = 5;
            return label;
        }

        private void RefreshLayout(bool force = false)
        {
            if (_viewport == null || _content == null || _thankYou == null) return;
            if (force) Canvas.ForceUpdateCanvases();
            if (!force && _lastViewportSize == _viewport.rect.size) return;
            _lastViewportSize = _viewport.rect.size;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            _startY = -_viewport.rect.height - 80;
            _content.anchoredPosition = new Vector2(0, _startY);
            _endDistance = Mathf.Max(0, _viewport.rect.center.y - _viewport.InverseTransformPoint(_thankYou.position).y);
            _content.anchoredPosition = new Vector2(0, _startY + _playback.Distance);
        }

        public void PreviewSection(int index)
        {
            RebuildContent();
            var sections = _content.GetComponentsInChildren<TMP_Text>();
            TMP_Text target = null;
            int found = 0;
            foreach (var label in sections)
                if (label.name == "MemberName" && found++ == index) { target = label; break; }
            if (target == null) target = _thankYou.GetComponent<TMP_Text>();
            float offset = _viewport.rect.center.y - _viewport.InverseTransformPoint(target.transform.position).y;
            _content.anchoredPosition += new Vector2(0, offset);
            _rollingGroup.alpha = 1;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            _inputLock.Release();
            if (_music != null) _music.Stop();
            if (_fallbackListener != null) _fallbackListener.enabled = false;
            foreach (var pair in _ducked) if (pair.Key != null) pair.Key.mute = pair.Value;
            _ducked.Clear();
            if (Application.isPlaying && !_completion.Task.IsCompleted) _completion.TrySetCanceled();
        }

        private void OnDestroy()
        {
            _completion.TrySetCanceled();
            if (Active == this) Active = null;
        }
    }
}
