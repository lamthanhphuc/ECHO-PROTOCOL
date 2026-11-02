using System;
using System.Collections.Generic;
using EchoProtocol.Networking;
using EchoProtocol.Networking.Loading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace EchoProtocol.UI.Loading
{
    /// <summary>Presentation only: every phase and progress value comes from the match coordinator.</summary>
    [DisallowMultipleComponent]
    public sealed class CinematicMatchLoadingScreen : MonoBehaviour
    {
        private static CinematicMatchLoadingScreen _instance;
        private static readonly string[] StepLabels =
        {
            "Connecting to server...", "Syncing player data...", "Loading map data...",
            "Preparing game session...", "Finalizing..."
        };
        private readonly PlayerInteractionControlLock _inputLock = new PlayerInteractionControlLock();
        private readonly LoadingUiNavigationLock _navigationLock = new LoadingUiNavigationLock();
        [SerializeField] private CinematicMatchLoadingTheme _theme;
        [SerializeField, HideInInspector] private GameObject _content;
        [SerializeField, HideInInspector] private CanvasGroup _group;
        [SerializeField, HideInInspector] private Image _fill, _indeterminate, _logo, _background, _atmosphere, _handprint;
        [SerializeField, HideInInspector] private TMP_Text _phase, _detail, _percent, _footer, _recoveryText, _title;
        [SerializeField, HideInInspector] private TMP_Text[] _steps = new TMP_Text[5];
        [SerializeField, HideInInspector] private HorrorLoadingStatusGraphic[] _status = new HorrorLoadingStatusGraphic[5];
        [SerializeField, HideInInspector] private CinematicLoadingSignalGraphic _signal;
        [SerializeField, HideInInspector] private Button _recovery;
        private readonly List<Material> _ownedMaterials = new List<Material>();
        private Material _logoMaterial, _bloodMaterial, _atmosphereMaterial;
        private Vector2 _logoPosition, _backgroundPosition;
        private Vector3 _backgroundScale;
        private float _displayedProgress;
        private int _lastActiveStep;
        private bool _preview;
        private bool _positionsCached;
        private Sprite _fallbackProgressSprite;
        private bool _visible, _recovering;
        private float _openedAt, _completeAt = -1;

        private bool _sessionVisible;
        private string _sessionMessage;
        private Action _sessionCancel;
        private int _sessionFrame = -10;

        // Session transitions share this screen without creating a match readiness barrier.
        public void SetSessionTransition(bool visible, string message, Action cancel)
        {
            _sessionVisible = visible;
            _sessionMessage = message;
            _sessionCancel = cancel;
            _sessionFrame = Time.frameCount;
        }

        private bool SessionVisible => _sessionVisible && Time.frameCount - _sessionFrame <= 1;

        private void PresentSession()
        {
            SetVisible(true);
            MaintainInputBlock();
            float time = Time.unscaledTime;
            Present(MatchLoadingPhase.NetworkSynchronization, "", -1f, false, 0, 0, null, time);
            _group.alpha = 1f; // Cover the outgoing scene immediately; never fade through it.
            string message = _sessionMessage ?? "";
            int lineBreak = message.IndexOf('\n');
            _phase.text = lineBreak < 0 ? message : message.Substring(0, lineBreak);
            _detail.text = lineBreak < 0 ? "" : message.Substring(lineBreak + 1);
            _detail.gameObject.SetActive(lineBreak >= 0);
            _recovery.interactable = !_recovering;
            _percent.text = ""; // Connection/recovery has no measured scene percentage.
            foreach (var step in _steps) step.gameObject.SetActive(false);
            foreach (var status in _status) status.gameObject.SetActive(false);
            _recovery.gameObject.SetActive(_sessionCancel != null);
            _recoveryText.text = EchoProtocol.Settings.GameLanguage.Choose("Hủy và về menu", "Cancel and return to menu");
            if (_sessionCancel != null && Keyboard.current != null
                && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
                Recover();
        }

        public static CinematicMatchLoadingScreen EnsureExists()
        {
            if (_instance == null)
            {
                var prefab = Resources.Load<GameObject>("UI/Loading/CinematicMatchLoadingScreen");
                if (prefab != null) Instantiate(prefab);
                if (_instance == null)
                    new GameObject("CinematicMatchLoadingScreen", typeof(RectTransform)).AddComponent<CinematicMatchLoadingScreen>();
            }
            return _instance;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        private void Awake()
        {
            if (!Application.isPlaying) return;
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Build(Resources.Load<CinematicMatchLoadingTheme>(CinematicMatchLoadingTheme.ResourcePath) ?? _theme);
            ApplyMaterials(true);
            CacheArtPositions();
            _recovery.onClick.AddListener(Recover);
            SetVisible(false);
        }

        private void LateUpdate()
        {
            var coordinator = MatchLoadingCoordinator.Instance;
            if (coordinator == null || !coordinator.Snapshot.IsVisible)
            {
                if (SessionVisible) PresentSession();
                else SetVisible(false);
                return;
            }
            foreach (var step in _steps) step.gameObject.SetActive(true);
            foreach (var status in _status) status.gameObject.SetActive(true);
            var snapshot = coordinator.Snapshot;
            SetVisible(true);
            MaintainInputBlock();
            float time = Time.unscaledTime;
            Present(snapshot.Phase, snapshot.SceneName, snapshot.SceneProgress, snapshot.LocalReady,
                snapshot.ReadyPlayers, snapshot.RequiredPlayers, snapshot.Error, time);
            if (snapshot.Phase == MatchLoadingPhase.Complete)
            {
                if (SessionVisible)
                {
                    _completeAt = -1;
                    _group.alpha = 1f;
                    return;
                }
                if (_completeAt < 0) _completeAt = time;
                float afterHold = time - _completeAt - HoldSeconds;
                _group.alpha = afterHold <= 0 ? 1 : 1 - Mathf.Clamp01(afterHold / Mathf.Max(0.001f, FadeOutSeconds));
                if (afterHold >= FadeOutSeconds)
                {
                    coordinator.Dismiss();
                    SetVisible(false);
                }
            }
            else
            {
                _completeAt = -1;
                _group.alpha = 1f;
            }
            if (snapshot.Phase == MatchLoadingPhase.Failed && !_recovering && Keyboard.current != null &&
                (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
                Recover();
        }

        private void SetVisible(bool visible)
        {
            if (!visible)
            {
                _inputLock.Release();
                _navigationLock.Release();
            }
            if (_content == null) return;
            if (visible && !_visible)
            {
                _openedAt = Time.unscaledTime;
                _completeAt = -1;
                _group.alpha = 0;
                _recovering = false;
                _displayedProgress = 0;
                _lastActiveStep = 0;
            }
            _visible = visible;
            _content.SetActive(visible);
            if (!visible)
            {
                _group.alpha = 0;
            }
        }

        private void MaintainInputBlock()
        {
            if (EventSystem.current == null)
            {
                var events = new GameObject("LoadingEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            _navigationLock.Acquire(EventSystem.current);
            GameObject owner = gameObject;
            var runner = NetworkBootstrap.Instance != null ? NetworkBootstrap.Instance.Runner : null;
            if (runner != null && runner.IsRunning && runner.LocalPlayer.IsRealPlayer &&
                runner.TryGetPlayerObject(runner.LocalPlayer, out var player) && player != null && player.IsValid)
                owner = player.gameObject;
            // Scene unloading deliberately clears existing project modal locks, including persistent owners.
            if (!_inputLock.IsLocked || _inputLock.Player != owner)
                _inputLock.Acquire(owner, allowWhileDowned: true);
        }

        private async void Recover()
        {
            if (_recovering) return;
            if (!MatchLoadingCoordinator.OwnsPresentation && SessionVisible)
            {
                var cancel = _sessionCancel;
                _sessionCancel = null;
                _recovery.gameObject.SetActive(false);
                cancel?.Invoke();
                return;
            }
            _recovering = true;
            _recovery.interactable = false;
            _recoveryText.text = "RETURNING...";
            try
            {
                var coordinator = MatchLoadingCoordinator.Instance;
                if (coordinator != null) await coordinator.RecoverAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (this != null) _detail.text = "Could not return to the menu. Please try again.";
            }
            finally
            {
                if (this != null)
                {
                    _recovering = false;
                    _recovery.interactable = true;
                    _recoveryText.text = "RETURN TO MAIN MENU";
                }
            }
        }

        private void Present(MatchLoadingPhase phase, string sceneName, float progress, bool localReady,
            int ready, int required, string error, float time)
        {
            bool complete = phase == MatchLoadingPhase.Complete;
            bool failed = phase == MatchLoadingPhase.Failed;
            bool waiting = phase == MatchLoadingPhase.GameplayReadiness && localReady;
            bool measured = phase == MatchLoadingPhase.SceneLoading && progress >= 0 && progress < 1;
            _phase.text = failed ? "TRANSMISSION INTERRUPTED" : complete ? "PROTOCOL SYNCHRONIZED"
                : waiting ? "WAITING FOR OTHER PLAYERS" : "CONNECTING TO MATCH...";
            _phase.color = failed ? Accent : Foreground;
            if (_handprint != null)
                _handprint.gameObject.SetActive(!waiting && !failed && !complete && _handprint.sprite != null);
            _detail.text = failed ? (string.IsNullOrWhiteSpace(error) ? "The session could not be loaded. Return to the menu and try again." : error)
                : complete ? "All required operators are ready. Entering the facility."
                : waiting ? $"LOCAL LOAD COMPLETE  //  OPERATORS READY {ready} / {required}"
                : phase switch
                {
                    MatchLoadingPhase.BackendAuthorization => "Awaiting backend match authorization.",
                    MatchLoadingPhase.NetworkSynchronization => "Establishing the shared scene and synchronizing operators.",
                    MatchLoadingPhase.SceneLoading => progress >= 1 ? "Scene assets are loaded. Waiting for gameplay initialization."
                        : measured ? "Reading scene assets and activating the facility." : "Waiting for scene progress from Fusion.",
                    _ => $"Verifying local player, camera and session readiness  //  {ready} / {required}"
                };
            _percent.text = complete ? "100%" : failed ? "OFFLINE"
                : phase == MatchLoadingPhase.GameplayReadiness && required > 0 ? $"{ready} / {required}"
                : phase == MatchLoadingPhase.SceneLoading && progress >= 1 ? "READY"
                : measured ? $"{Mathf.FloorToInt(progress * 100)}%" : "--";
            _detail.gameObject.SetActive(failed || waiting || complete);
            float target = complete ? 1f : measured ? progress : 0;
            // Visual easing can lag an observed operation, but can never invent forward progress.
            _displayedProgress = complete || _preview || target < _displayedProgress ? target
                : Mathf.MoveTowards(_displayedProgress, target, Time.unscaledDeltaTime * 1.5f);
            _fill.fillAmount = _displayedProgress;
            _fill.gameObject.SetActive(complete || (measured && _displayedProgress > 0));
            _indeterminate.gameObject.SetActive(!complete && !failed && !measured);
            if (_indeterminate.gameObject.activeSelf)
            {
                var marker = _indeterminate.rectTransform;
                float x = Mathf.PingPong(time * 0.22f, 0.83f);
                marker.anchorMin = new Vector2(x, 0);
                marker.anchorMax = new Vector2(x + 0.17f, 1);
                marker.offsetMin = new Vector2(0, 8);
                marker.offsetMax = new Vector2(0, -8);
            }
            int activeStep = phase == MatchLoadingPhase.BackendAuthorization ? 0
                : phase == MatchLoadingPhase.NetworkSynchronization ? 1
                : phase == MatchLoadingPhase.SceneLoading ? 2 : localReady ? 4 : 3;
            if (!failed) _lastActiveStep = activeStep;
            for (int i = 0; i < _steps.Length; i++)
            {
                bool done = complete || i < (failed ? _lastActiveStep : activeStep);
                bool active = !complete && i == (failed ? _lastActiveStep : activeStep);
                _steps[i].color = active ? Accent : done ? Foreground : Muted;
                _steps[i].text = StepLabels[i];
                _status[i].Configure(done ? 2 : active ? 1 : 0, time, Accent, Muted);
            }
            _recovery.gameObject.SetActive(failed);
            _recovery.interactable = !_recovering;
            if (failed && !_recovering)
                _recoveryText.text = MatchLoadingCoordinator.Instance != null
                    && MatchLoadingCoordinator.Instance.RecoveryReturnsToLobby ? "RETURN TO LOBBY" : "RETURN TO MAIN MENU";
            _footer.text = "DO NOT TRUST THE SIGNAL";
            _signal.SetSignal(time, _theme != null ? _theme.StaticIntensity : 0.035f, _theme != null ? _theme.GlitchIntensity : 0.22f);
            UpdateAtmosphere(time);
        }

        private void Build(CinematicMatchLoadingTheme theme)
        {
            _theme = theme ?? Resources.Load<CinematicMatchLoadingTheme>(CinematicMatchLoadingTheme.ResourcePath);
            theme = _theme;
            if (_content != null) return;
            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            _content = new GameObject("FullscreenInputBlocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            _content.transform.SetParent(transform, false);
            Stretch(_content.GetComponent<RectTransform>());
            _content.GetComponent<Image>().color = new Color(0.003f, 0.005f, 0.007f, 1);
            _group = _content.GetComponent<CanvasGroup>();
            _group.blocksRaycasts = true;
            _group.interactable = true;
            if (theme != null && theme.Background != null)
            {
                _background = Graphic<Image>("Laboratory", _content.transform);
                Stretch(_background.rectTransform);
                _background.sprite = theme.Background;
                _background.color = theme.BackgroundTint;
                var aspect = _background.gameObject.AddComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                aspect.aspectRatio = theme.Background.rect.width / theme.Background.rect.height;
            }
            var dim = Graphic<Image>("Darkness", _content.transform);
            Stretch(dim.rectTransform);
            dim.color = theme != null ? theme.Darkness : new Color(0.014f, 0.002f, 0.004f, 0.1f);
            _atmosphere = Graphic<Image>("FogOverlay", _content.transform);
            Stretch(_atmosphere.rectTransform);
            _atmosphere.sprite = WhiteProgressSprite;
            _atmosphere.color = Color.white;
            _signal = Graphic<CinematicLoadingSignalGraphic>("SignalStaticAndVignette", _content.transform);
            Stretch(_signal.rectTransform);
            // The reference environment supplies its own lighting; retain only a faint grain vignette.
            _signal.gameObject.AddComponent<CanvasGroup>().alpha = 0.1f;
            float letterbox = theme != null ? theme.LetterboxHeight : 0;
            var upper = Graphic<Image>("UpperLetterbox", _content.transform);
            upper.color = Color.black;
            Anchor(upper.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -letterbox), new Vector2(0, letterbox));
            var lower = Graphic<Image>("LowerLetterbox", _content.transform);
            lower.color = Color.black;
            Anchor(lower.rectTransform, Vector2.zero, Vector2.right, Vector2.zero, new Vector2(0, letterbox));
            _logo = Graphic<Image>("Logo", _content.transform);
            ReferenceRect(_logo.rectTransform, 960, 220, 760, 330);
            _logo.sprite = theme != null ? theme.Logo : null;
            _logo.preserveAspect = true;
            _logo.gameObject.SetActive(_logo.sprite != null);
            _title = Label("Title", 960, 215, 900, 315, "ECHO\nPROTOCOL", 102, Foreground);
            _title.textWrappingMode = TextWrappingModes.Normal;
            _title.lineSpacing = -35;
            _title.characterSpacing = 10;
            _title.gameObject.SetActive(_logo.sprite == null);
            var subtitle = Label("Subtitle", 960, 385, 1160, 34,
                "SOME EXPERIMENTS SHOULD HAVE STAYED BURIED", 18, new Color(0.62f, 0.31f, 0.31f, 1));
            subtitle.characterSpacing = 5;
            _phase = Label("Phase", 960, 480, 1180, 52, string.Empty, 32, Foreground);
            _phase.characterSpacing = 4;
            _handprint = Graphic<Image>("Handprint", _content.transform);
            ReferenceRect(_handprint.rectTransform, 1285, 480, 110, 110);
            _handprint.sprite = Resources.Load<Sprite>("UI/Loading/ReferenceHandprint");
            _handprint.preserveAspect = true;
            _handprint.color = new Color(1, 1, 1, 0.6f);
            _handprint.gameObject.SetActive(_handprint.sprite != null);
            _detail = Label("Detail", 960, 873, 1180, 60, string.Empty, 18, Foreground, telemetry: true);
            _detail.textWrappingMode = TextWrappingModes.Normal;
            _percent = Label("Percentage", 1458, 562, 136, 54, "--", 40, Foreground, telemetry: true);
            var track = Graphic<HorrorLoadingProgressFrameGraphic>("ProgressTrack", _content.transform);
            ReferenceRect(track.rectTransform, 960, 562, 850, 52);
            track.Configure(Accent);
            _fill = Graphic<Image>("MeasuredSceneProgress", track.transform);
            Stretch(_fill.rectTransform);
            _fill.rectTransform.offsetMin = new Vector2(8, 8);
            _fill.rectTransform.offsetMax = new Vector2(-8, -8);
            _fill.sprite = WhiteProgressSprite;
            _fill.type = Image.Type.Filled;
            _fill.fillMethod = Image.FillMethod.Horizontal;
            _fill.fillOrigin = 0;
            _fill.fillAmount = 0;
            _fill.color = Accent;
            _indeterminate = Graphic<Image>("IndeterminateSignal", track.transform);
            _indeterminate.sprite = WhiteProgressSprite;
            _indeterminate.color = Accent;
            var row = new GameObject("PhaseChecklist", typeof(RectTransform));
            row.transform.SetParent(_content.transform, false);
            Stretch(row.GetComponent<RectTransform>());
            for (int i = 0; i < _steps.Length; i++)
            {
                _status[i] = Graphic<HorrorLoadingStatusGraphic>("Status" + i, row.transform);
                ReferenceRect(_status[i].rectTransform, 660, 635 + i * 47, 35, 35);
                _steps[i] = Graphic<TextMeshProUGUI>("Phase" + i, row.transform);
                ReferenceRect(_steps[i].rectTransform, 1033, 635 + i * 47, 660, 38);
                _steps[i].font = TelemetryFont;
                _steps[i].fontSize = 23;
                _steps[i].enableAutoSizing = true;
                _steps[i].fontSizeMin = 17;
                _steps[i].fontSizeMax = 23;
                _steps[i].alignment = TextAlignmentOptions.Left;
                _steps[i].text = StepLabels[i];
                _steps[i].richText = false;
            }
            _footer = Label("Footer", 960, 936, 980, 34, "DO NOT TRUST THE SIGNAL", 19,
                new Color(0.68f, 0.15f, 0.16f, 1));
            _footer.characterSpacing = 9;
            FooterRule();
            var buttonOwner = new GameObject("ReturnToMenu", typeof(RectTransform), typeof(CanvasRenderer), typeof(HorrorFrameGraphic), typeof(Button));
            buttonOwner.transform.SetParent(_content.transform, false);
            var buttonRect = buttonOwner.GetComponent<RectTransform>();
            ReferenceRect(buttonRect, 960, 1022, 440, 48);
            var frame = buttonOwner.GetComponent<HorrorFrameGraphic>();
            frame.Configure(new Color(0.12f, 0.025f, 0.032f, 0.98f), true);
            _recovery = buttonOwner.GetComponent<Button>();
            _recovery.targetGraphic = frame;
            _recovery.navigation = new Navigation { mode = Navigation.Mode.None };
            _recoveryText = Graphic<TextMeshProUGUI>("Label", buttonOwner.transform);
            Stretch(_recoveryText.rectTransform);
            _recoveryText.font = HeadingFont;
            _recoveryText.fontSize = 20;
            _recoveryText.color = Foreground;
            _recoveryText.alignment = TextAlignmentOptions.Center;
            _recoveryText.text = "RETURN TO MAIN MENU";
            _recovery.gameObject.SetActive(false);
        }

        /// <summary>Only used by the isolated Editor preview; does not start or alter a network session.</summary>
        public void Preview(CinematicMatchLoadingTheme theme, MatchLoadingPhase phase, float sceneProgress,
            bool localReady, int readyPlayers, int requiredPlayers, string error = "")
        {
            if (Application.isPlaying) return;
            Build(theme);
            ApplyMaterials(true);
            CacheArtPositions();
            _preview = true;
            _content.SetActive(true);
            _group.alpha = 1;
            Present(phase, "Scene_A", sceneProgress, localReady, readyPlayers, requiredPlayers, error, 4.5f);
        }

        /// <summary>Builds the editable resource prefab with asset materials and an inactive overlay.</summary>
        public void PrepareAuthoringPrefab(CinematicMatchLoadingTheme theme)
        {
            if (Application.isPlaying) return;
            Build(theme);
            ApplyMaterials(false);
            _phase.text = "CONNECTING TO MATCH...";
            _percent.text = "--";
            _detail.gameObject.SetActive(false);
            _recovery.gameObject.SetActive(false);
            for (int i = 0; i < _status.Length; i++) _status[i].Configure(i == 0 ? 1 : 0, 0, Accent, Muted);
            _group.alpha = 0;
            _content.SetActive(false);
        }

        public void BuildPrefab(CinematicMatchLoadingTheme theme) => PrepareAuthoringPrefab(theme);

        private TMP_Text Label(string name, float x, float y, float width, float height,
            string text, float size, Color color, bool telemetry = false)
        {
            var label = Graphic<TextMeshProUGUI>(name, _content.transform);
            ReferenceRect(label.rectTransform, x, y, width, height);
            label.font = telemetry ? TelemetryFont : HeadingFont;
            label.fontSize = size;
            label.enableAutoSizing = true;
            label.fontSizeMin = size * 0.65f;
            label.fontSizeMax = size;
            label.color = color;
            label.text = text;
            label.richText = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.alignment = TextAlignmentOptions.Center;
            return label;
        }

        private void FooterRule()
        {
            var left = Graphic<Image>("WarningRuleLeft", _content.transform);
            ReferenceRect(left.rectTransform, 855, 974, 155, 1);
            left.color = Accent;
            var right = Graphic<Image>("WarningRuleRight", _content.transform);
            ReferenceRect(right.rectTransform, 1065, 974, 155, 1);
            right.color = Accent;
            var sigil = Graphic<HorrorLoadingStatusGraphic>("WarningSigil", _content.transform);
            ReferenceRect(sigil.rectTransform, 960, 974, 23, 23);
            sigil.Configure(0, 0, Accent, Accent);
            var cross = Graphic<Image>("WarningCross", _content.transform);
            ReferenceRect(cross.rectTransform, 960, 974, 1, 34);
            cross.color = Accent;
            var crossbar = Graphic<Image>("WarningCrossbar", _content.transform);
            ReferenceRect(crossbar.rectTransform, 960, 974, 34, 1);
            crossbar.color = Accent;
        }

        private void ApplyMaterials(bool clone)
        {
            DisposeMaterials();
            _logoMaterial = MaterialFor(_theme != null ? _theme.LogoMaterial : null, clone);
            _bloodMaterial = MaterialFor(_theme != null ? _theme.ProgressFillMaterial : null, clone);
            _atmosphereMaterial = MaterialFor(_theme != null ? _theme.AtmosphereMaterial : null, clone);
            _logo.material = _logoMaterial;
            _fill.material = _bloodMaterial;
            _indeterminate.material = _bloodMaterial;
            _atmosphere.material = _atmosphereMaterial;
            // A sprite supplies stable 0..1 UVs for the full-screen procedural shader.
            _atmosphere.sprite = WhiteProgressSprite;
            _atmosphere.gameObject.SetActive(_atmosphereMaterial != null);
            _fill.sprite = _indeterminate.sprite = WhiteProgressSprite;
            _fill.type = Image.Type.Filled;
            _fill.fillMethod = Image.FillMethod.Horizontal;
            _fill.fillOrigin = 0;
            _fill.color = _indeterminate.color = _bloodMaterial != null ? Color.white : Accent;
            _fill.transform.parent.GetComponent<HorrorLoadingProgressFrameGraphic>().Configure(Accent);
            if (_theme != null && _theme.Logo != null) _logo.sprite = _theme.Logo;
            _logo.gameObject.SetActive(_logo.sprite != null);
            _title.gameObject.SetActive(_logo.sprite == null);
        }

        private Material MaterialFor(Material source, bool clone)
        {
            if (source == null || !clone) return source;
            var material = new Material(source) { name = source.name + " (Loading Instance)", hideFlags = HideFlags.HideAndDontSave };
            _ownedMaterials.Add(material);
            return material;
        }

        private void DisposeMaterials()
        {
            foreach (var material in _ownedMaterials)
            {
                if (material == null) continue;
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
            _ownedMaterials.Clear();
        }

        private void CacheArtPositions()
        {
            if (_positionsCached) return;
            _positionsCached = true;
            _logoPosition = _logo.rectTransform.anchoredPosition;
            if (_background != null)
            {
                _backgroundPosition = _background.rectTransform.anchoredPosition;
                _backgroundScale = _background.rectTransform.localScale;
            }
        }

        private void UpdateAtmosphere(float time)
        {
            // Only owned material instances receive per-frame changes; the theme assets stay untouched.
            if (_ownedMaterials.Count != 0)
            {
                SetFloat(_logoMaterial, "_EffectTime", time);
                SetFloat(_logoMaterial, "_GlitchIntensity", _theme != null ? _theme.GlitchIntensity : 0.22f);
                SetFloat(_bloodMaterial, "_EffectTime", time);
                SetFloat(_atmosphereMaterial, "_EffectTime", time);
                SetFloat(_atmosphereMaterial, "_Intensity", 1);
                SetFloat(_atmosphereMaterial, "_FogIntensity", _theme != null ? _theme.FogIntensity : 0.6f);
                SetFloat(_atmosphereMaterial, "_ParticleIntensity", _theme != null ? _theme.ParticleIntensity : 0.3f);
                SetFloat(_atmosphereMaterial, "_RedFlickerIntensity", _theme != null ? _theme.RedFlickerIntensity : 0.24f);
            }
            float glitch = _theme != null ? _theme.GlitchIntensity : 0.22f;
            float jitter = Mathf.FloorToInt(time * 8) % 43 == 0 ? glitch * 1.4f : 0;
            _logo.rectTransform.anchoredPosition = _logoPosition + Vector2.right * jitter;
            if (_background != null)
            {
                float motion = _theme != null ? _theme.BackgroundMotion : 0.3f;
                _background.rectTransform.localScale = _backgroundScale * (1 + motion * 0.015f);
                _background.rectTransform.anchoredPosition = _backgroundPosition +
                    new Vector2(Mathf.Sin(time * 0.09f) * 5, Mathf.Cos(time * 0.07f) * 3) * motion;
            }
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material != null && material.HasProperty(property)) material.SetFloat(property, value);
        }

        private Sprite WhiteProgressSprite
        {
            get
            {
                if (_theme != null && _theme.ProgressSprite != null) return _theme.ProgressSprite;
                var resource = Resources.Load<Sprite>("UI/Loading/ProgressMask");
                if (resource != null) return resource;
                if (_fallbackProgressSprite == null)
                {
                    _fallbackProgressSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
                    _fallbackProgressSprite.hideFlags = HideFlags.HideAndDontSave;
                }
                return _fallbackProgressSprite;
            }
        }

        private static T Graphic<T>(string name, Transform parent) where T : Graphic
        {
            var owner = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
            owner.transform.SetParent(parent, false);
            var graphic = owner.GetComponent<T>();
            graphic.raycastTarget = false;
            return graphic;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
        {
            rect.anchorMin = min; rect.anchorMax = max;
            rect.pivot = new Vector2(0.5f, 0);
            rect.sizeDelta = size; rect.anchoredPosition = position;
        }
        private static void ReferenceRect(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x - 960, 540 - y);
        }
        private static TMP_FontAsset Font(string name) => Resources.Load<TMP_FontAsset>("UI/MainMenuTypography/" + name) ?? TMP_Settings.defaultFontAsset;
        private TMP_FontAsset HeadingFont => _theme != null && _theme.HeadingFont != null ? _theme.HeadingFont
            : Resources.Load<TMP_FontAsset>("UI/LobbyReference/LobbySerif") ?? TMP_Settings.defaultFontAsset;
        private TMP_FontAsset TelemetryFont => _theme != null && _theme.TelemetryFont != null ? _theme.TelemetryFont : Font("JetBrainsMonoTMP");
        private Color Foreground => IndustrialUiStyle.Text;
        private Color Muted => IndustrialUiStyle.Muted;
        private Color Accent => IndustrialUiStyle.Accent;
        private float FadeInSeconds => _theme != null ? _theme.FadeInSeconds : 0.25f;
        private float HoldSeconds => _theme != null ? _theme.CompleteHoldSeconds : 0.45f;
        private float FadeOutSeconds => _theme != null ? _theme.FadeOutSeconds : 0.65f;
        private void OnDisable() => SetVisible(false);
        private void OnDestroy()
        {
            SetVisible(false);
            if (_recovery != null) _recovery.onClick.RemoveListener(Recover);
            DisposeMaterials();
            if (_fallbackProgressSprite != null)
            {
                if (Application.isPlaying) Destroy(_fallbackProgressSprite);
                else DestroyImmediate(_fallbackProgressSprite);
            }
            if (_instance == this) _instance = null;
        }
    }
}
