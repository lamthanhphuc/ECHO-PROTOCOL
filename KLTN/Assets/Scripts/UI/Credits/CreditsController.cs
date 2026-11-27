using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EchoProtocol.Audio;
using EchoProtocol.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
        [SerializeField, Min(0f)] private float _sectionSpacing = 144f;
        [Header("Playback (1920 x 1080 design units)")]
        [SerializeField, Min(0.01f)] private float _fadeDuration = 1.2f;
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
        internal static readonly Color BackgroundColor = new Color32(5, 6, 8, 255);
        private static readonly Color Primary = new Color32(232, 232, 234, 255);
        private static readonly Color Secondary = new Color32(173, 173, 183, 255);
        private static readonly Color Accent = new Color32(202, 188, 141, 255);
        private readonly List<(CreditsData.Block Data, RectTransform Rect)> _blocks = new();
        private readonly List<Vector2> _rollStops = new();
        private RectTransform _thankYou;
        private readonly CreditsRollPlayback _playback = new();
        private readonly PlayerInteractionControlLock _inputLock = new();
        private readonly Dictionary<AudioSource, bool> _ducked = new();
        private readonly HashSet<EventSystem> _suspendedUi = new();
        private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private float _openingY, _rollDuration, _nextAudioScan;
        private float _playbackRate = 1f;
        private Vector2 _lastViewportSize;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;
        private bool _completionSent, _started, _waitForSpaceRelease;
        public static CreditsController Active { get; private set; }
        public static bool BlocksGameplayInput => Active != null && Active.isActiveAndEnabled && Active._started;
        public Task Completion => _completion.Task;
        public CreditsData Data => _data;
        public RectTransform Content => _content;
        public RectTransform Viewport => _viewport;
        public RectTransform ThankYou => _thankYou;
        public float PlannedDuration => _blocks.Count == 0 ? 0 :
            (_blocks[0].Data.holdSeconds + _rollDuration + _blocks[^1].Data.holdSeconds) / _playbackRate + _fadeDuration;
        public bool HasReferences => _data != null && _viewport != null && _content != null && _rollingGroup != null
            && _content.GetComponent<VerticalLayoutGroup>() != null && _music != null
            && _fallbackListener != null && _skipHint != null && _skipProgress != null;

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
            if (CreditsFlow.IsDiscardingLoad) { gameObject.SetActive(false); return; }
            if (Active != null && Active != this) { Destroy(gameObject); return; }
            Active = this;
            if (CreditsFlow.IsResultPlayback) DontDestroyOnLoad(gameObject);
            if (_fallbackListener != null) _fallbackListener.enabled = false;
        }

        private void EnablePreviewCamera()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                var camera = root.GetComponent<Camera>();
                if (camera == null) continue;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = BackgroundColor;
                camera.cullingMask = 0;
                camera.enabled = true;
                return;
            }
        }

        private void OnEnable() => SceneManager.sceneLoaded += SceneLoaded;
        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_started) SuspendBackground();
        }

        private void Start()
        {
            if (Active != this) return;
            try
            {
                if (!HasReferences) throw new InvalidOperationException("AfterCredit has missing UI or data references.");
                if (!CreditsFlow.IsManagedPlayback) EnablePreviewCamera();
                UpdateSafeArea();
                RebuildContent();
                _rollingGroup.alpha = 0;
                _skipProgress.fillAmount = 0;
                _music.clip = _backgroundMusic;
                _music.loop = true;
                _music.spatialBlend = 0;
                _music.playOnAwake = false;
                GameAudioSettings.RouteMusic(_music);
                _music.volume = 0;
                if (_backgroundMusic != null) _music.Play();
                _waitForSpaceRelease = Keyboard.current?.spaceKey.isPressed == true;
                _started = true;
                SuspendBackground();
                MaintainInput();
            }
            catch (Exception error)
            {
                if (CreditsFlow.IsManagedPlayback) _completion.TrySetException(error);
                else Debug.LogException(error, this);
                gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            if (!_started || _completionSent) return;
            MaintainInput();
            UpdateSafeArea();
            RefreshLayout();
            if (Time.unscaledTime >= _nextAudioScan)
            {
                _nextAudioScan = Time.unscaledTime + 0.3f;
                SuspendBackground();
            }
            bool pressed = Keyboard.current?.spaceKey.isPressed == true;
            if (Application.isFocused && !pressed) _waitForSpaceRelease = false;
            bool holding = Application.isFocused && !_waitForSpaceRelease && pressed;
            _playback.Tick(Time.unscaledDeltaTime, holding, _blocks[0].Data.holdSeconds / _playbackRate,
                _fadeDuration, _rollDuration / _playbackRate, _blocks[^1].Data.holdSeconds / _playbackRate, _holdToSkipSeconds);
            _content.anchoredPosition = new Vector2(0, _openingY + RollDistance(_playback.RollSeconds * _playbackRate));
            _rollingGroup.alpha = _playback.Opacity;
            _music.volume = _musicVolume * _playback.Opacity;
            _skipProgress.fillAmount = CreditsRollPlayback.HoldProgress(_playback.SkipHeldSeconds, _holdToSkipSeconds);
            _skipHint.text = GameLanguage.Choose("GIỮ SPACE ĐỂ BỎ QUA", "HOLD SPACE TO SKIP");
            if (_playback.Phase == CreditsRollPhase.Completed) Complete();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) _waitForSpaceRelease = true;
        }

        private void MaintainInput()
        {
            // The existing scene-unload driver releases modal locks during a host's Lobby transition.
            if (!_inputLock.IsLocked) _inputLock.Acquire(gameObject, unlockCursor: false, allowWhileDowned: true);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = false;
        }

        private void Complete()
        {
            if (_completionSent) return;
            _completionSent = true;
            _skipHint.text = string.Empty;
            _skipProgress.transform.parent.gameObject.SetActive(false);
            _completion.TrySetResult(true);
            if (!CreditsFlow.IsManagedPlayback) enabled = false;
        }

        private void SuspendBackground()
        {
            foreach (var system in FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude))
            {
                if (!system.enabled) continue;
                system.enabled = false;
                _suspendedUi.Add(system);
            }
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
            _blocks.Clear();
            _thankYou = null;
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var child = _content.GetChild(i);
                child.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
            }
            var layout = _content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = _sectionSpacing;
            foreach (var section in _data.sections)
            {
                for (int i = 0; i < section.blocks.Length; i++)
                {
                    var block = section.blocks[i];
                    var rect = Column(string.IsNullOrEmpty(block.heading) ? section.title : block.heading, _content, 14);
                    _blocks.Add((block, rect));
                    BuildBlock(section, block, rect, i == 0);
                }
            }
            if (_blocks.Count < 2 || _blocks[0].Data.layout != CreditsData.Layout.Opening
                || _blocks[^1].Data.layout != CreditsData.Layout.Closing || _thankYou == null)
                throw new InvalidOperationException("AfterCredit data must have an opening and closing title.");
            RefreshLayout(true);
        }

        private void BuildBlock(CreditsData.Section section, CreditsData.Block block, RectTransform rect, bool firstBlock)
        {
            // Give the logo and dedicated holds clear space from the neighbouring rolling sections.
            var group = rect.GetComponent<VerticalLayoutGroup>();
            group.childAlignment = TextAnchor.MiddleCenter;
            var size = rect.gameObject.AddComponent<LayoutElement>();
            bool isThanks = section.blocks[^1].layout == CreditsData.Layout.Closing && block.layout == CreditsData.Layout.People;
            size.minHeight = isThanks ? 560 : block.layout switch
            {
                CreditsData.Layout.Opening or CreditsData.Layout.Closing or CreditsData.Layout.Platforms => 680,
                CreditsData.Layout.Legal => 650,
                CreditsData.Layout.People when block.entries.Length <= 2 => 164,
                _ => 418
            };

            if (block.layout is CreditsData.Layout.Opening or CreditsData.Layout.Closing)
            {
                AddLogo(rect);
                if (block.layout == CreditsData.Layout.Closing)
                    _thankYou = Text("ThankYouForPlaying", rect, block.note, _fontSize * 0.92f, false, Primary).rectTransform;
                return;
            }

            string heading = string.IsNullOrEmpty(block.heading) ? section.title : block.heading;
            if (firstBlock && !isThanks && !string.IsNullOrEmpty(block.heading) && !string.Equals(section.title, block.heading, StringComparison.Ordinal))
                Text("Section", rect, section.title.ToUpperInvariant(), _fontSize * 0.43f, true, Secondary);
            Text("Heading", rect, heading.ToUpperInvariant(), _fontSize * 0.73f, true, Accent);
            var spacer = new GameObject("Heading spacing", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(rect, false);
            spacer.GetComponent<LayoutElement>().preferredHeight = 8;

            int columns = Mathf.Clamp(block.columns, 1, 2);
            int entriesPerColumn = (block.entries.Length + columns - 1) / columns;
            Transform row = rect;
            if (columns == 2)
            {
                var obj = new GameObject("Columns", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                obj.transform.SetParent(rect, false);
                var grid = obj.GetComponent<HorizontalLayoutGroup>();
                grid.spacing = 42;
                grid.childAlignment = TextAnchor.UpperCenter;
                grid.childControlWidth = grid.childControlHeight = true;
                grid.childForceExpandWidth = true;
                grid.childForceExpandHeight = false;
                row = obj.transform;
            }

            bool readAcross = block.layout == CreditsData.Layout.People && entriesPerColumn <= 2;
            for (int column = 0; column < columns; column++)
            {
                var entries = Column("Entries", row, entriesPerColumn >= 6 ? 8 : 20);
                if (columns == 2)
                {
                    var width = entries.gameObject.AddComponent<LayoutElement>();
                    width.minWidth = width.preferredWidth = 0;
                    width.flexibleWidth = 1;
                }
                for (int rowIndex = 0; rowIndex < entriesPerColumn; rowIndex++)
                {
                    int index = readAcross ? rowIndex * columns + column : column * entriesPerColumn + rowIndex;
                    if (index >= block.entries.Length) break;
                    var entry = block.entries[index];
                    var pair = Column("Entry", entries, 3);
                    float nameSize = block.layout == CreditsData.Layout.Attributions || entriesPerColumn >= 6 ? 0.63f : 0.85f;
                    if (isThanks) nameSize = 1f;
                    Text("MemberName", pair, entry.name, _fontSize * nameSize, false, Primary);
                    if (!string.IsNullOrEmpty(entry.role))
                    {
                        var role = Text("Role", pair, entry.role, _fontSize * 0.52f, false, Secondary);
                        role.lineSpacing = -3;
                        if (block.layout == CreditsData.Layout.Legal || isThanks)
                            role.margin = new Vector4(235, 0, 235, 0);
                    }
                }
            }
            if (!string.IsNullOrEmpty(block.note))
                Text("Note", rect, block.note, _fontSize * 0.55f, false, Secondary);
        }

        private void AddLogo(Transform parent)
        {
            if (_data.logo == null)
            {
                Text("ProjectTitle", parent, _data.projectTitle, _fontSize * 1.25f, true, Primary);
                return;
            }
            var obj = new GameObject("ECHO PROTOCOL Logo", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>();
            image.sprite = _data.logo;
            image.preserveAspect = true;
            image.raycastTarget = false;
            obj.GetComponent<LayoutElement>().preferredHeight = 240;
        }

        private static RectTransform Column(string name, Transform parent, float spacing)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            obj.transform.SetParent(parent, false);
            var group = obj.GetComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.childAlignment = TextAnchor.UpperCenter; group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = true; group.childForceExpandHeight = false;
            return (RectTransform)obj.transform;
        }

        private TMP_Text Text(string name, Transform parent, string value, float size, bool heading, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var label = obj.GetComponent<TMP_Text>(); label.text = value ?? string.Empty;
            label.font = (heading ? _roleFont : _nameFont) ?? UiTypographyTheme.BodyTmp;
            label.fontSize = size; label.color = color;
            label.alignment = TextAlignmentOptions.Center; label.richText = false; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            if (heading) label.characterSpacing = 5;
            return label;
        }

        private void UpdateSafeArea()
        {
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            var safeArea = Screen.safeArea;
            if (screenSize == _lastScreenSize && safeArea == _lastSafeArea) return;
            _lastScreenSize = screenSize;
            _lastSafeArea = safeArea;
            if (screenSize.x <= 0 || screenSize.y <= 0) return;
            ApplySafeArea(safeArea, screenSize);
        }

        public void ApplySafeArea(Rect safeArea, Vector2 size)
        {
            Vector2 min = safeArea.min / size;
            Vector2 span = safeArea.size / size;
            float horizontal = Mathf.Max(0.12f, (1f - 1.36f * safeArea.height / Mathf.Max(1, safeArea.width)) * 0.5f);
            SetAnchors(_viewport, min, span, new(horizontal, 0.12f), new(1 - horizontal, 0.93f));
            SetAnchors(_skipHint.rectTransform, min, span, new(0.55f, 0.045f), new(0.95f, 0.08f));
            SetAnchors((RectTransform)_skipProgress.transform.parent, min, span, new(0.82f, 0.028f), new(0.95f, 0.032f));
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 span, Vector2 lower, Vector2 upper)
        {
            rect.anchorMin = min + span * lower;
            rect.anchorMax = min + span * upper;
        }

        private void RefreshLayout(bool force = false)
        {
            if (!force && _lastViewportSize == _viewport.rect.size) return;
            Canvas.ForceUpdateCanvases();
            _lastViewportSize = _viewport.rect.size;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            _openingY = CenteredY(_blocks[0].Rect);
            _rollStops.Clear();
            _rollStops.Add(Vector2.zero);
            _rollDuration = 0;
            for (int i = 1; i < _blocks.Count; i++)
            {
                var (block, rect) = _blocks[i];
                float distance = CenteredY(rect) - _openingY;
                _rollDuration += Mathf.Max(0.01f, block.travelSeconds);
                _rollStops.Add(new Vector2(_rollDuration, distance));
                if (i == _blocks.Count - 1 || block.holdSeconds <= 0) continue;
                _rollDuration += block.holdSeconds;
                _rollStops.Add(new Vector2(_rollDuration, distance));
            }
            _content.anchoredPosition = new Vector2(0, _openingY + RollDistance(_playback.RollSeconds * _playbackRate));
        }

        private float CenteredY(RectTransform block) => _content.anchoredPosition.y + _viewport.rect.center.y
            - _viewport.InverseTransformPoint(block.TransformPoint(block.rect.center)).y;

        private float RollDistance(float seconds)
        {
            for (int i = 1; i < _rollStops.Count; i++)
            {
                var next = _rollStops[i];
                if (seconds > next.x) continue;
                var previous = _rollStops[i - 1];
                return Mathf.Lerp(previous.y, next.y, Mathf.InverseLerp(previous.x, next.x, seconds));
            }
            return _rollStops[^1].y;
        }

        public void PreviewSection(int index)
        {
            RebuildContent();
            var block = _blocks[Mathf.Clamp(index, 0, _blocks.Count - 1)];
            _content.anchoredPosition = new Vector2(0, CenteredY(block.Rect));
            _rollingGroup.alpha = 1;
            _skipHint.text = GameLanguage.Choose("GIỮ SPACE ĐỂ BỎ QUA", "HOLD SPACE TO SKIP");
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            _inputLock.Release();
            if (_music != null) _music.Stop();
            if (_fallbackListener != null) _fallbackListener.enabled = false;
            foreach (var pair in _ducked) if (pair.Key != null) pair.Key.mute = pair.Value;
            _ducked.Clear();
            foreach (var system in _suspendedUi) if (system != null) system.enabled = true;
            _suspendedUi.Clear();
            if (Application.isPlaying && !_completion.Task.IsCompleted) _completion.TrySetCanceled();
        }

        private void OnDestroy()
        {
            _completion.TrySetCanceled();
            if (Active == this) Active = null;
        }
    }
}
