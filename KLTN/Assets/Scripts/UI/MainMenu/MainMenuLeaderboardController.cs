using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EchoProtocol.Auth;
using EchoProtocol.Audio;
using EchoProtocol.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EchoProtocol.UI.MainMenu
{
    /// <summary>
    /// Adds the archive to the existing MainMenu canvas without replacing any
    /// scene objects or interrupting its audio and navigation controllers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuLeaderboardController : MonoBehaviour
    {
        private const int MaximumPageSize = 12;
        private const float SoloRowHeight = 42f;
        private const float TeamRowHeight = 55f;
        private const float PanelWidth = 1530f;
        private const float PanelHeight = 860f;
        private static readonly Color Charcoal = Hex("111415", 0.98f);
        private static readonly Color Metal = Hex("272A29", 1f);
        private static readonly Color Bronze = Hex("A58255", 1f);
        private static readonly Color Blood = Hex("873B39", 1f);
        private static readonly Color Pale = Hex("DEE0D9", 1f);
        private static readonly Color Muted = Hex("929792", 1f);

        private Canvas _canvas;
        private Font _font;
        private Font _titleFont;
        private LeaderboardArchiveVisuals _visuals;
        private Sprite _portraitSprite;
        private RectTransform _profileRect;
        private RectTransform _iconRect;
        private Vector2 _profileBasePosition;
        private Button _iconButton;
        private GameObject _tooltip;
        private RectTransform _overlay;
        private CanvasGroup _overlayGroup;
        private RectTransform _panel;
        private RectTransform _entriesRoot;
        private RectTransform _recordsViewport;
        private ScrollRect _recordsScroll;
        private GameObject _rowPrefab;
        private Image[] _staticLines;
        private Text _heading;
        private Text _status;
        private Text _bodyMessage;
        private Text _bodySubline;
        private SettingsIconGraphic _radioSymbol;
        private Text _pageLabel;
        private Text _mapLabel;
        private Text _difficultyLabel;
        private Text _columnLabel;
        private Button _soloTab;
        private Button _teamTab;
        private Image _soloTabAccent;
        private Image _teamTabAccent;
        private Button _previousButton;
        private Button _nextButton;
        private Button _retryButton;
        private Button _backdropButton;
        private Button _closeButton;
        private Coroutine _opening;
        private Coroutine _tabTransition;
        private LeaderboardApiService _service;
        private string[] _maps = { "SciFi", "Scene_A", "Scene_B" };
        private string[] _difficulties = { "EASY", "NORMAL", "HARD" };
        private string _mapId = "SciFi";
        private string _difficulty = "NORMAL";
        private LeaderboardMode _mode;
        private int _page = 1;
        private int _pageSize = MaximumPageSize;
        private int _totalCount;
        private ArchiveState _archiveState = ArchiveState.Loading;
        private int _requestVersion;
        private bool _loadRecords = true;
        private float _fitScale = 1f;
        private Vector2 _lastScreenSize;
        private Rect _lastSafeArea;
        private GameObject _previousSelection;
        private readonly List<LockedGroup> _lockedGroups = new List<LockedGroup>();

        private struct LockedGroup
        {
            public CanvasGroup group;
            public bool interactable;
            public bool blocksRaycasts;
            public bool added;
        }

        public bool IsOpen => _overlay != null && _overlay.gameObject.activeSelf;
        public Button TrophyButton => _iconButton;
        public Button BackdropButton => _backdropButton;
        public Button CloseButton => _closeButton;
        public CanvasGroup OverlayGroup => _overlayGroup;
        public RectTransform OverlayRoot => _overlay;
        public LeaderboardMode CurrentMode => _mode;
        public string SelectedMap => _mapId;
        public string SelectedDifficulty => _difficulty;

        public enum ArchiveState { Loading, Empty, Error, Records }

        public static MainMenuLeaderboardController Install(
            Canvas canvas,
            bool loadRecords = true)
        {
            if (canvas == null) return null;
            var existing = canvas.GetComponent<MainMenuLeaderboardController>();
            if (existing != null) return existing;
            var store = canvas.transform.Find("StorePanel");
            var profile = store != null ? store.Find("ProfileButton") as RectTransform : null;
            if (profile == null)
            {
                Debug.LogWarning("[Leaderboard] ProfileButton missing; archive icon was not installed.");
                return null;
            }

            var component = canvas.gameObject.AddComponent<MainMenuLeaderboardController>();
            component._canvas = canvas;
            component._loadRecords = loadRecords;
            component.Build(profile);
            return component;
        }

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
        }

        private void OnDisable()
        {
            Close();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (_panel != null) UpdatePanelSize();
        }

        private void Update()
        {
            ProcessEscapePressed(Keyboard.current != null
                && Keyboard.current.escapeKey.wasPressedThisFrame);
            if (_panel != null && (_lastScreenSize.x != Screen.width
                || _lastScreenSize.y != Screen.height
                || !_lastSafeArea.Equals(Screen.safeArea)))
                UpdatePanelSize();
        }

        public void ProcessEscapePressed(bool pressed)
        {
            if (pressed && IsOpen) Close();
        }

        private void Build(RectTransform profile)
        {
            _profileRect = profile;
            _profileBasePosition = profile.anchoredPosition;
            _visuals = Resources.Load<LeaderboardArchiveVisuals>("LeaderboardArchive/Visuals");
            _font = profile.GetComponentInParent<Canvas>()
                .GetComponentsInChildren<Text>(true)
                .FirstOrDefault(text => text.font != null)?.font
                ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _titleFont = _visuals != null && _visuals.spectralFont != null
                ? _visuals.spectralFont : _font;
            _portraitSprite = _visuals != null && _visuals.operatorPortrait != null
                ? _visuals.operatorPortrait : FindSceneSprite("OperatorPortrait");
            var rowAsset = Resources.Load<GameObject>(
                "LeaderboardArchive/LeaderboardRow");
            _rowPrefab = rowAsset != null
                && rowAsset.GetComponent<LeaderboardRowView>() != null
                ? rowAsset : null;
            BuildIcon(profile);
            BuildOverlay();
        }

        private Sprite FindSceneSprite(string spriteName)
        {
            return _canvas.GetComponentsInChildren<Image>(true)
                .FirstOrDefault(image => image.sprite != null
                    && image.sprite.name == spriteName)?.sprite;
        }

        private void BuildIcon(RectTransform profile)
        {
            var rect = NewRect("LeaderboardButton", profile.parent);
            rect.anchorMin = profile.anchorMin;
            rect.anchorMax = profile.anchorMax;
            rect.pivot = profile.pivot;
            rect.sizeDelta = new Vector2(44f, 44f);
            rect.anchoredPosition = profile.anchoredPosition
                + new Vector2(-55f, 0f);
            _iconRect = rect;
            rect.SetSiblingIndex(profile.GetSiblingIndex());

            var image = rect.gameObject.AddComponent<Image>();
            image.color = Hex("181817", 0.97f);
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = Hex("514134", 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);
            _iconButton = rect.gameObject.AddComponent<Button>();
            _iconButton.targetGraphic = image;
            _iconButton.transition = Selectable.Transition.ColorTint;
            var colors = _iconButton.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = Hex("3A2522", 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = Hex("5A2723", 1f);
            colors.fadeDuration = 0.12f;
            _iconButton.colors = colors;
            _iconButton.onClick.AddListener(Open);
            rect.gameObject.AddComponent<GameAudioButton>();

            var symbol = NewRect("TrophyIcon", rect);
            Stretch(symbol, 7f, 7f, 7f, 7f);
            var trophy = symbol.gameObject.AddComponent<SettingsIconGraphic>();
            trophy.Configure(SettingsIcon.Trophy, Bronze);

            var tooltipRect = NewRect("LeaderboardTooltip", rect);
            tooltipRect.anchorMin = tooltipRect.anchorMax = new Vector2(0.5f, 0f);
            tooltipRect.pivot = new Vector2(0.5f, 1f);
            tooltipRect.anchoredPosition = new Vector2(0f, -9f);
            tooltipRect.sizeDelta = new Vector2(132f, 31f);
            var tooltipImage = tooltipRect.gameObject.AddComponent<Image>();
            tooltipImage.color = Hex("0C0E0D", 0.97f);
            tooltipImage.raycastTarget = false;
            var tooltipOutline = tooltipRect.gameObject.AddComponent<Outline>();
            tooltipOutline.effectColor = Blood;
            tooltipOutline.effectDistance = new Vector2(1f, -1f);
            var tooltipText = NewText("Label", tooltipRect, "LEADERBOARD", 12, Bronze,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(tooltipText.rectTransform, 3f, 2f, 3f, 2f);
            _tooltip = tooltipRect.gameObject;
            _tooltip.SetActive(false);

            var hover = rect.gameObject.AddComponent<LeaderboardIconHover>();
            hover.Configure(trophy, outline, _tooltip);
        }

        private void BuildOverlay()
        {
            _overlay = NewRect("LeaderboardOverlay", _canvas.transform);
            Stretch(_overlay, 0f, 0f, 0f, 0f);
            _overlayGroup = _overlay.gameObject.AddComponent<CanvasGroup>();
            _overlayGroup.blocksRaycasts = true;
            _overlayGroup.interactable = true;

            var backdrop = NewRect("DimBackground", _overlay);
            Stretch(backdrop, 0f, 0f, 0f, 0f);
            var backdropImage = backdrop.gameObject.AddComponent<Image>();
            backdropImage.color = Hex("030405", 0.83f);
            _backdropButton = backdrop.gameObject.AddComponent<Button>();
            _backdropButton.targetGraphic = backdropImage;
            _backdropButton.transition = Selectable.Transition.None;
            _backdropButton.onClick.AddListener(Close);

            _panel = NewRect("ArchivePanel", _overlay);
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            var panelImage = _panel.gameObject.AddComponent<Image>();
            panelImage.sprite = _visuals != null && _visuals.dossierMetal != null
                ? _visuals.dossierMetal : FindSceneSprite("DossierMetal");
            panelImage.color = panelImage.sprite != null ? Color.white : Charcoal;
            panelImage.raycastTarget = true;
            var panelBorder = _panel.gameObject.AddComponent<Outline>();
            panelBorder.effectColor = Hex("584339", 0.93f);
            panelBorder.effectDistance = new Vector2(2f, -2f);

            var topRule = NewRect("BloodRule", _panel);
            TopStretch(topRule, 0f, 3f, 0f, 0f);
            topRule.gameObject.AddComponent<Image>().color = Blood;
            var fileCode = NewText("ArchiveIndex", _panel,
                "ECHO PROTOCOL  //  CLASSIFIED RECORDS  /  EXTRACTION ARCHIVE",
                12, Bronze, TextAnchor.MiddleLeft);
            TopLeft(fileCode.rectTransform, 28f, 16f, 770f, 24f);
            _heading = NewText("Heading", _panel, "LEADERBOARD", 45, Pale,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            _heading.font = _titleFont;
            var titleShadow = _heading.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = Hex("77211F", 0.65f);
            titleShadow.effectDistance = new Vector2(3f, -2f);
            TopLeft(_heading.rectTransform, 28f, 41f, 710f, 56f);
            var subtitle = NewText("Subtitle", _panel,
                "EXTRACTION RECORDS  //  FASTEST COMPLETION TIMES",
                13, Muted, TextAnchor.MiddleLeft);
            TopLeft(subtitle.rectTransform, 28f, 100f, 730f, 23f);
            BuildEvidenceDetails();
            _closeButton = NewButton("CloseButton", _panel, "X", 15, 42f, 42f);
            var closeRect = (RectTransform)_closeButton.transform;
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-24f, -21f);
            _closeButton.onClick.AddListener(Close);

            _soloTab = NewButton("SoloTab", _panel, "SOLO RANKING", 15, 205f, 42f);
            TopLeft((RectTransform)_soloTab.transform, 28f, 137f, 205f, 42f);
            _soloTab.onClick.AddListener(() => SelectMode(LeaderboardMode.Solo));
            _teamTab = NewButton("TeamTab", _panel, "TEAM RANKING", 15, 205f, 42f);
            TopLeft((RectTransform)_teamTab.transform, 242f, 137f, 205f, 42f);
            _teamTab.onClick.AddListener(() => SelectMode(LeaderboardMode.Team));
            _soloTabAccent = CreateTabAccent(_soloTab);
            _teamTabAccent = CreateTabAccent(_teamTab);

            var mapButton = NewButton("MapFilter", _panel, "", 13, 240f, 37f);
            TopLeft((RectTransform)mapButton.transform, 28f, 194f, 240f, 37f);
            _mapLabel = mapButton.GetComponentInChildren<Text>();
            mapButton.onClick.AddListener(CycleMap);
            var difficultyButton = NewButton("DifficultyFilter", _panel, "", 13, 205f, 37f);
            TopLeft((RectTransform)difficultyButton.transform, 278f, 194f, 205f, 37f);
            _difficultyLabel = difficultyButton.GetComponentInChildren<Text>();
            difficultyButton.onClick.AddListener(CycleDifficulty);

            var columnRule = NewRect("ColumnRule", _panel);
            TopStretch(columnRule, 243f, 1f, 28f, 255f);
            columnRule.gameObject.AddComponent<Image>().color = Hex("674039", 0.75f);
            _columnLabel = NewText("ColumnHeading", _panel, "RANK       OPERATIVE", 11,
                Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
            TopLeft(_columnLabel.rectTransform, 34f, 249f, 440f, 24f);
            var timeColumn = NewText("TimeHeading", _panel, "TIME  (MM.SS)", 11,
                Muted, TextAnchor.MiddleRight, FontStyle.Bold);
            var timeRect = timeColumn.rectTransform;
            timeRect.anchorMin = timeRect.anchorMax = new Vector2(1f, 1f);
            timeRect.pivot = new Vector2(1f, 1f);
            timeRect.anchoredPosition = new Vector2(-263f, -249f);
            timeRect.sizeDelta = new Vector2(210f, 24f);

            _recordsViewport = NewRect("RecordsViewport", _panel);
            Stretch(_recordsViewport, 28f, 278f, 255f, 130f);
            var recordsBacking = _recordsViewport.gameObject.AddComponent<Image>();
            recordsBacking.color = Hex("060809", 0.89f);
            recordsBacking.raycastTarget = true;
            _recordsViewport.gameObject.AddComponent<RectMask2D>();
            _entriesRoot = NewRect("Entries", _recordsViewport);
            _entriesRoot.anchorMin = new Vector2(0f, 1f);
            _entriesRoot.anchorMax = new Vector2(1f, 1f);
            _entriesRoot.pivot = new Vector2(0.5f, 1f);
            _entriesRoot.anchoredPosition = Vector2.zero;
            _entriesRoot.sizeDelta = Vector2.zero;
            _recordsScroll = _recordsViewport.gameObject.AddComponent<ScrollRect>();
            _recordsScroll.viewport = _recordsViewport;
            _recordsScroll.content = _entriesRoot;
            _recordsScroll.horizontal = false;
            _recordsScroll.vertical = true;
            _recordsScroll.movementType = ScrollRect.MovementType.Clamped;
            _recordsScroll.inertia = true;
            _recordsScroll.scrollSensitivity = 30f;
            var radioRect = NewRect("RadioSignal", _recordsViewport);
            Center(radioRect, 0f, 65f, 78f, 78f);
            _radioSymbol = radioRect.gameObject.AddComponent<SettingsIconGraphic>();
            _radioSymbol.Configure(SettingsIcon.Radio, Hex("88423B", 0.75f));
            _bodyMessage = NewText("BodyMessage", _recordsViewport,
                "", 20, Pale, TextAnchor.MiddleCenter, FontStyle.Bold);
            Center(_bodyMessage.rectTransform, 0f, 1f, 700f, 38f);
            _bodySubline = NewText("BodySubline", _recordsViewport,
                "", 13, Muted, TextAnchor.MiddleCenter);
            Center(_bodySubline.rectTransform, 0f, -31f, 700f, 26f);
            var bottomRule = NewRect("BottomRule", _panel);
            bottomRule.anchorMin = new Vector2(0f, 0f);
            bottomRule.anchorMax = new Vector2(1f, 0f);
            bottomRule.pivot = new Vector2(0.5f, 0f);
            bottomRule.offsetMin = new Vector2(28f, 62f);
            bottomRule.offsetMax = new Vector2(-28f, 63f);
            bottomRule.gameObject.AddComponent<Image>().color = Hex("674039", 0.75f);

            _status = NewText("ArchiveStatus", _panel, "", 12, Muted,
                TextAnchor.MiddleLeft);
            var statusRect = _status.rectTransform;
            statusRect.anchorMin = statusRect.anchorMax = new Vector2(0f, 0f);
            statusRect.pivot = new Vector2(0f, 0f);
            statusRect.anchoredPosition = new Vector2(28f, 20f);
            statusRect.sizeDelta = new Vector2(500f, 28f);
            _previousButton = NewButton("PreviousPage", _panel, "<", 18, 40f, 36f);
            BottomRight((RectTransform)_previousButton.transform, 158f, 17f, 40f, 36f);
            _previousButton.onClick.AddListener(() => ChangePage(-1));
            _pageLabel = NewText("PageLabel", _panel, "01 / 01", 12, Bronze,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            BottomRight(_pageLabel.rectTransform, 77f, 20f, 75f, 30f);
            _nextButton = NewButton("NextPage", _panel, ">", 18, 40f, 36f);
            BottomRight((RectTransform)_nextButton.transform, 29f, 17f, 40f, 36f);
            _nextButton.onClick.AddListener(() => ChangePage(1));
            _retryButton = NewButton("RetryButton", _recordsViewport, "RETRY CONNECTION", 14,
                230f, 42f);
            Center((RectTransform)_retryButton.transform, 0f, -91f, 230f, 42f);
            _retryButton.onClick.AddListener(Fetch);
            PresentState(ArchiveState.Loading);

            _staticLines = new Image[3];
            for (int i = 0; i < _staticLines.Length; i++)
            {
                var line = NewRect("StaticInterference" + i, _panel);
                TopStretch(line, 75f + i * 131f, 2f, 18f, 18f);
                _staticLines[i] = line.gameObject.AddComponent<Image>();
                _staticLines[i].raycastTarget = false;
                _staticLines[i].color = Hex(i == 1 ? "A58255" : "873B39", 0f);
            }
            _staticLines[0].transform.SetAsLastSibling();
            _staticLines[1].transform.SetAsLastSibling();
            _staticLines[2].transform.SetAsLastSibling();
            _closeButton.transform.SetAsLastSibling();

            RefreshFilters();
            RefreshTabs();
            UpdatePanelSize();
            _overlay.gameObject.SetActive(false);
        }

        private static Image CreateTabAccent(Button tab)
        {
            var rule = NewRect("ActiveRule", tab.transform);
            rule.anchorMin = new Vector2(0f, 0f);
            rule.anchorMax = new Vector2(1f, 0f);
            rule.pivot = new Vector2(0.5f, 0f);
            rule.offsetMin = new Vector2(0f, 0f);
            rule.offsetMax = new Vector2(0f, 3f);
            var image = rule.gameObject.AddComponent<Image>();
            image.color = Blood;
            image.raycastTarget = false;
            return image;
        }

        private void BuildEvidenceDetails()
        {
            var divider = NewRect("EvidenceDivider", _panel);
            divider.anchorMin = divider.anchorMax = new Vector2(1f, 1f);
            divider.pivot = new Vector2(1f, 1f);
            divider.anchoredPosition = new Vector2(-240f, -245f);
            divider.sizeDelta = new Vector2(1f, 485f);
            var dividerImage = divider.gameObject.AddComponent<Image>();
            dividerImage.color = Hex("6B3B34", 0.65f);
            dividerImage.raycastTarget = false;

            var annotation = NewText("EvidenceAnnotation", _panel,
                "ONLY THE FASTEST\nLEAVE A TRACE...", 13,
                Hex("9E4942", 0.85f), TextAnchor.MiddleCenter);
            var annotationRect = annotation.rectTransform;
            annotationRect.anchorMin = annotationRect.anchorMax = new Vector2(1f, 1f);
            annotationRect.pivot = new Vector2(1f, 1f);
            annotationRect.anchoredPosition = new Vector2(-285f, -72f);
            annotationRect.sizeDelta = new Vector2(215f, 52f);
            annotationRect.localRotation = Quaternion.Euler(0f, 0f, 7f);

            Sprite still = _visuals != null && _visuals.surveillanceStill != null
                ? _visuals.surveillanceStill : FindSceneSprite("SurveillanceStill");
            var photo = NewDecoration("EvidencePhoto", still, _panel,
                new Vector2(-51f, -38f), new Vector2(214f, 156f), -5f,
                Hex("D8D3C6", 0.94f));
            photo.gameObject.SetActive(still != null);

            Sprite tape = _visuals != null && _visuals.agedTape != null
                ? _visuals.agedTape : FindSceneSprite("AgedTape");
            var tapeA = NewDecoration("EvidenceTapeLeft", tape, _panel,
                new Vector2(-204f, -24f), new Vector2(84f, 28f), 17f,
                Hex("D9C8A9", 0.82f));
            var tapeB = NewDecoration("EvidenceTapeRight", tape, _panel,
                new Vector2(-31f, -167f), new Vector2(83f, 28f), -26f,
                Hex("D9C8A9", 0.79f));
            tapeA.gameObject.SetActive(tape != null);
            tapeB.gameObject.SetActive(tape != null);

            Sprite note = _visuals != null && _visuals.tornNote != null
                ? _visuals.tornNote : FindSceneSprite("TornNote");
            var noteRect = NewRect("EvidenceNote", _panel);
            noteRect.anchorMin = noteRect.anchorMax = new Vector2(1f, 0f);
            noteRect.pivot = new Vector2(1f, 0f);
            noteRect.anchoredPosition = new Vector2(-31f, 130f);
            noteRect.sizeDelta = new Vector2(203f, 82f);
            noteRect.localRotation = Quaternion.Euler(0f, 0f, -6f);
            var noteImage = noteRect.gameObject.AddComponent<Image>();
            noteImage.sprite = note;
            noteImage.color = Hex("C7B8A1", 0.86f);
            noteImage.raycastTarget = false;
            noteRect.gameObject.SetActive(note != null);
            var noteStamp = NewText("EvidenceStamp", noteRect,
                "SUBJECTS\nNEVER LEAVE", 12, Hex("7E2724", 0.95f),
                TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(noteStamp.rectTransform, 26f, 8f, 26f, 8f);

            Sprite fingerprint = _visuals != null && _visuals.fingerprint != null
                ? _visuals.fingerprint : FindSceneSprite("Fingerprint");
            var print = NewDecoration("EvidenceFingerprint", fingerprint, _panel,
                new Vector2(-88f, -570f), new Vector2(75f, 98f), 8f,
                Hex("8E2A26", 0.32f));
            print.gameObject.SetActive(fingerprint != null);
        }

        private static Image NewDecoration(string name, Sprite sprite,
            Transform parent, Vector2 topRightPosition, Vector2 size,
            float rotation, Color tint)
        {
            var rect = NewRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = topRightPosition;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = tint;
            image.raycastTarget = false;
            return image;
        }

        public void Open()
        {
            if (_overlay == null || IsOpen) return;
            if (_tooltip != null) _tooltip.SetActive(false);
            _overlay.SetAsLastSibling();
            _overlay.gameObject.SetActive(true);
            _overlayGroup.alpha = 1f;
            _overlayGroup.blocksRaycasts = true;
            _overlayGroup.interactable = true;
            LockUnderlyingMenu();
            _previousSelection = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject : null;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_closeButton.gameObject);
            UpdatePanelSize();
            if (_opening != null) StopCoroutine(_opening);
            if (Application.isPlaying) _opening = StartCoroutine(AnimateOpen());
            if (_loadRecords) Fetch();
            else SetStatus("ARCHIVE TEST MODE");
        }

        public void Close()
        {
            if (_overlay == null || !IsOpen) return;
            _requestVersion++;
            if (_opening != null)
            {
                StopCoroutine(_opening);
                _opening = null;
            }
            _overlay.gameObject.SetActive(false);
            UnlockUnderlyingMenu();
            _overlayGroup.alpha = 1f;
            _panel.localScale = Vector3.one * _fitScale;
            if (_heading != null) _heading.color = Pale;
            if (EventSystem.current != null
                && _previousSelection != null && _previousSelection.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(_previousSelection);
            _previousSelection = null;
        }

        private IEnumerator AnimateOpen()
        {
            _overlayGroup.alpha = 0f;
            _panel.localScale = Vector3.one * (_fitScale * 0.965f);
            float elapsed = 0f;
            while (elapsed < 0.29f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / 0.29f);
                _overlayGroup.alpha = t * t * (3f - 2f * t);
                _panel.localScale = Vector3.one * (_fitScale
                    * Mathf.Lerp(0.965f, 1f, 1f - (1f - t) * (1f - t)));
                float flicker = elapsed < 0.16f
                    ? (Mathf.Sin(elapsed * 157f) > 0.1f ? 0.15f : 0.03f) : 0f;
                if (_heading != null)
                {
                    var headingColor = Pale;
                    headingColor.a = elapsed < 0.16f
                        ? 0.86f + flicker * 0.75f : 1f;
                    _heading.color = headingColor;
                }
                for (int i = 0; i < _staticLines.Length; i++)
                {
                    var color = _staticLines[i].color;
                    color.a = flicker * (i == 1 ? 0.5f : 1f);
                    _staticLines[i].color = color;
                }
                yield return null;
            }
            _overlayGroup.alpha = 1f;
            _panel.localScale = Vector3.one * _fitScale;
            if (_heading != null) _heading.color = Pale;
            foreach (var line in _staticLines)
            {
                var color = line.color;
                color.a = 0f;
                line.color = color;
            }
            _opening = null;
        }

        private void SelectMode(LeaderboardMode mode)
        {
            if (_mode == mode) return;
            _mode = mode;
            _page = 1;
            _pageSize = mode == LeaderboardMode.Team ? 9 : MaximumPageSize;
            RefreshTabs(true);
            Fetch();
        }

        private void CycleMap()
        {
            if (_maps.Length == 0) return;
            int index = Array.IndexOf(_maps, _mapId);
            _mapId = _maps[(index + 1) % _maps.Length];
            _page = 1;
            RefreshFilters();
            Fetch();
        }

        private void CycleDifficulty()
        {
            if (_difficulties.Length == 0) return;
            int index = Array.IndexOf(_difficulties, _difficulty);
            _difficulty = _difficulties[(index + 1) % _difficulties.Length];
            _page = 1;
            RefreshFilters();
            Fetch();
        }

        private void ChangePage(int delta)
        {
            int last = Math.Max(1, (_totalCount + _pageSize - 1) / _pageSize);
            int target = Mathf.Clamp(_page + delta, 1, last);
            if (target == _page) return;
            _page = target;
            Fetch();
        }

        private void Fetch()
        {
            if (!IsOpen || !_loadRecords) return;
            int version = ++_requestVersion;
            ClearEntries();
            PresentState(ArchiveState.Loading);
            RefreshPagination();
            _service ??= new LeaderboardApiService(AuthRuntime.EnsureExists().Client);
            _service.GetPage(_mode, _mapId, _difficulty, _page, _pageSize, result =>
            {
                if (this == null || !IsOpen || version != _requestVersion) return;
                if (!result.IsSuccess || result.Data == null
                    || !result.Data.success || result.Data.data == null)
                {
                    PresentState(ArchiveState.Error);
                    RefreshPagination();
                    return;
                }

                var data = result.Data.data;
                if (data.maps != null && data.maps.Length > 0) _maps = data.maps;
                if (data.difficulties != null && data.difficulties.Length > 0)
                    _difficulties = data.difficulties;
                RefreshFilters();
                _totalCount = Mathf.Max(0, data.totalCount);
                RenderEntries(LeaderboardPresentation.OrderEntries(data.items));
                PresentState(_totalCount == 0
                    ? ArchiveState.Empty : ArchiveState.Records, _totalCount);
                RefreshPagination();
            });
        }

        public void PresentState(ArchiveState state, int recordCount = 0)
        {
            if (_bodyMessage == null || _retryButton == null) return;
            _archiveState = state;
            bool hasRows = state == ArchiveState.Records;
            _entriesRoot.gameObject.SetActive(hasRows);
            _recordsScroll.enabled = hasRows;
            _bodyMessage.gameObject.SetActive(!hasRows);
            _bodySubline.gameObject.SetActive(!hasRows);
            _radioSymbol.gameObject.SetActive(state == ArchiveState.Error);
            _retryButton.gameObject.SetActive(state == ArchiveState.Error);
            switch (state)
            {
                case ArchiveState.Loading:
                    _bodyMessage.text = "DECRYPTING ARCHIVE";
                    _bodySubline.text = "ESTABLISHING SECURE CHANNEL...";
                    SetStatus("ARCHIVE: CONNECTING");
                    break;
                case ArchiveState.Empty:
                    _bodyMessage.text = "NO EXTRACTIONS ON FILE";
                    _bodySubline.text = "NO RECORDED RUNS FOR THIS MAP AND THREAT";
                    SetStatus("ARCHIVE: NO RECORDS");
                    break;
                case ArchiveState.Error:
                    _bodyMessage.text = "UNABLE TO REACH THE ARCHIVE";
                    _bodySubline.text = "THE SIGNAL IS WEAKENED...";
                    SetStatus("ARCHIVE: CONNECTION LOST");
                    break;
                default:
                    SetStatus(recordCount + " RECORDS ON FILE");
                    break;
            }
            RefreshPagination();
        }

        private void RenderEntries(LeaderboardEntryDto[] entries)
        {
            ClearEntries();
            if (entries.Length == 0) return;
            int count = Mathf.Min(entries.Length, _pageSize);
            float rowHeight = _mode == LeaderboardMode.Team
                ? TeamRowHeight : SoloRowHeight;
            _entriesRoot.sizeDelta = new Vector2(0f, count * rowHeight);
            for (int i = 0; i < count; i++)
            {
                var entry = entries[i];
                if (entry == null) continue;
                var view = _rowPrefab != null
                    ? Instantiate(_rowPrefab, _entriesRoot, false)
                        .GetComponent<LeaderboardRowView>()
                    : LeaderboardRowView.CreateFallback(_entriesRoot, _font);
                view.name = "Rank" + entry.rank;
                var row = (RectTransform)view.transform;
                row.anchorMin = new Vector2(0f, 1f);
                row.anchorMax = new Vector2(1f, 1f);
                row.pivot = new Vector2(0.5f, 1f);
                row.anchoredPosition = new Vector2(0f, -i * rowHeight);
                row.sizeDelta = new Vector2(0f, rowHeight - 3f);
                view.Bind(entry, _mode, _portraitSprite, i);
            }
            _recordsScroll.StopMovement();
            _recordsScroll.verticalNormalizedPosition = 1f;
        }

        private void ClearEntries()
        {
            if (_entriesRoot == null) return;
            for (int i = _entriesRoot.childCount - 1; i >= 0; i--)
            {
                var child = _entriesRoot.GetChild(i);
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
            _entriesRoot.sizeDelta = Vector2.zero;
        }

        private void RefreshFilters()
        {
            if (_mapLabel != null) _mapLabel.text = "MAP  < " + _mapId + " >";
            if (_difficultyLabel != null)
                _difficultyLabel.text = "THREAT  < " + _difficulty + " >";
        }

        private void RefreshTabs(bool animate = false)
        {
            if (_soloTab == null) return;
            _columnLabel.text = _mode == LeaderboardMode.Solo
                ? "RANK       OPERATIVE" : "RANK       SQUAD / OPERATIVES";
            if (_tabTransition != null)
            {
                StopCoroutine(_tabTransition);
                _tabTransition = null;
            }
            if (animate && Application.isPlaying)
                _tabTransition = StartCoroutine(AnimateTabTransition());
            else
            {
                SetTabColor(_soloTab, _mode == LeaderboardMode.Solo);
                SetTabColor(_teamTab, _mode == LeaderboardMode.Team);
                SetTabAccent(_soloTabAccent, _mode == LeaderboardMode.Solo);
                SetTabAccent(_teamTabAccent, _mode == LeaderboardMode.Team);
            }
        }

        private IEnumerator AnimateTabTransition()
        {
            Color soloStart = _soloTab.targetGraphic.color;
            Color teamStart = _teamTab.targetGraphic.color;
            Color soloTextStart = _soloTab.GetComponentInChildren<Text>().color;
            Color teamTextStart = _teamTab.GetComponentInChildren<Text>().color;
            Color soloRuleStart = _soloTabAccent.color;
            Color teamRuleStart = _teamTabAccent.color;
            bool soloActive = _mode == LeaderboardMode.Solo;
            float elapsed = 0f;
            while (elapsed < 0.2f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.2f));
                _soloTab.targetGraphic.color = Color.Lerp(soloStart,
                    soloActive ? Hex("502825", 1f) : Metal, t);
                _teamTab.targetGraphic.color = Color.Lerp(teamStart,
                    soloActive ? Metal : Hex("502825", 1f), t);
                _soloTab.GetComponentInChildren<Text>().color = Color.Lerp(
                    soloTextStart, soloActive ? Pale : Muted, t);
                _teamTab.GetComponentInChildren<Text>().color = Color.Lerp(
                    teamTextStart, soloActive ? Muted : Pale, t);
                _soloTabAccent.color = Color.Lerp(soloRuleStart,
                    Hex("873B39", soloActive ? 1f : 0f), t);
                _teamTabAccent.color = Color.Lerp(teamRuleStart,
                    Hex("873B39", soloActive ? 0f : 1f), t);
                yield return null;
            }
            _tabTransition = null;
        }

        private static void SetTabAccent(Image image, bool selected)
        {
            if (image != null) image.color = Hex("873B39", selected ? 1f : 0f);
        }

        private static void SetTabColor(Button button, bool selected)
        {
            button.targetGraphic.color = selected ? Hex("502825", 1f) : Metal;
            button.GetComponentInChildren<Text>().color = selected ? Pale : Muted;
        }

        private void RefreshPagination()
        {
            if (_archiveState == ArchiveState.Loading
                || _archiveState == ArchiveState.Error)
            {
                _pageLabel.text = _page.ToString("00") + " / --";
                _previousButton.interactable = false;
                _nextButton.interactable = false;
                return;
            }
            int last = Math.Max(1, (_totalCount + _pageSize - 1) / _pageSize);
            _pageLabel.text = _page.ToString("00") + " / " + last.ToString("00");
            _previousButton.interactable = _archiveState == ArchiveState.Records
                && _page > 1;
            _nextButton.interactable = _archiveState == ArchiveState.Records
                && _page < last;
        }

        private void SetStatus(string value)
        {
            if (_status != null) _status.text = value;
        }

        private void UpdatePanelSize()
        {
            ApplySafeArea(Screen.safeArea, new Vector2(Screen.width, Screen.height));
        }

        public static float ComputePanelScale(float availableWidth, float availableHeight)
        {
            return Mathf.Clamp(Mathf.Min(
                (availableWidth - 40f) / PanelWidth,
                (availableHeight - 40f) / PanelHeight), 0.05f, 1f);
        }

        public void ApplySafeArea(Rect safePixels, Vector2 screenPixels)
        {
            if (_panel == null || _canvas == null) return;
            var canvasRect = _canvas.transform as RectTransform;
            if (canvasRect == null) return;
            float screenWidth = Mathf.Max(1f, screenPixels.x);
            float screenHeight = Mathf.Max(1f, screenPixels.y);
            if (safePixels.width <= 0f || safePixels.height <= 0f)
                safePixels = new Rect(0f, 0f, screenWidth, screenHeight);
            float left = Mathf.Clamp(safePixels.xMin, 0f, screenWidth);
            float right = Mathf.Clamp(safePixels.xMax, left, screenWidth);
            float bottom = Mathf.Clamp(safePixels.yMin, 0f, screenHeight);
            float top = Mathf.Clamp(safePixels.yMax, bottom, screenHeight);
            safePixels = Rect.MinMaxRect(left, bottom, right, top);
            var safeCenter = new Vector2(safePixels.center.x / screenWidth,
                safePixels.center.y / screenHeight);
            _panel.anchorMin = _panel.anchorMax = safeCenter;
            float availableWidth = canvasRect.rect.width
                * safePixels.width / screenWidth;
            float availableHeight = canvasRect.rect.height
                * safePixels.height / screenHeight;
            _fitScale = ComputePanelScale(availableWidth, availableHeight);
            _panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            _panel.localScale = Vector3.one * _fitScale;
            _pageSize = _mode == LeaderboardMode.Team ? 9 : MaximumPageSize;
            if (_profileRect != null && _iconRect != null)
            {
                var parentScale = _profileRect.parent.lossyScale;
                float scaleX = Mathf.Max(0.01f, Mathf.Abs(parentScale.x));
                float scaleY = Mathf.Max(0.01f, Mathf.Abs(parentScale.y));
                _profileRect.anchoredPosition = _profileBasePosition
                    + new Vector2(-(screenWidth - safePixels.xMax) / scaleX,
                        -(screenHeight - safePixels.yMax) / scaleY);
                _iconRect.anchoredPosition = _profileRect.anchoredPosition
                    + new Vector2(-55f, 0f);
            }
            _lastScreenSize = screenPixels;
            _lastSafeArea = safePixels;
        }

        private Text NewText(string name, Transform parent, string value, int size,
            Color color, TextAnchor alignment, FontStyle style = FontStyle.Normal)
        {
            var rect = NewRect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = _font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.supportRichText = false;
            return text;
        }

        private Button NewButton(string name, Transform parent, string label,
            int fontSize, float width, float height)
        {
            var rect = NewRect(name, parent);
            rect.sizeDelta = new Vector2(width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Metal;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Metal;
            colors.highlightedColor = Hex("512C2A", 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = Blood;
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            rect.gameObject.AddComponent<GameAudioButton>();
            var text = NewText("Label", rect, label, fontSize, Pale,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(text.rectTransform, 2f, 2f, 2f, 2f);
            return button;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void TopLeft(RectTransform rect, float x, float y,
            float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void MiddleLeft(RectTransform rect, float x,
            float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Center(RectTransform rect, float x, float y,
            float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void BottomRight(RectTransform rect, float right, float bottom,
            float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-right, bottom);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Stretch(RectTransform rect, float left, float top,
            float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void TopStretch(RectTransform rect, float top, float height,
            float left, float right)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static Color Hex(string rgb, float alpha)
        {
            ColorUtility.TryParseHtmlString("#" + rgb, out var color);
            color.a = alpha;
            return color;
        }

        private void LockUnderlyingMenu()
        {
            _lockedGroups.Clear();
            foreach (Transform child in _canvas.transform)
            {
                if (child == _overlay) continue;
                var group = child.GetComponent<CanvasGroup>();
                bool added = group == null;
                if (added) group = child.gameObject.AddComponent<CanvasGroup>();
                _lockedGroups.Add(new LockedGroup
                {
                    group = group,
                    interactable = group.interactable,
                    blocksRaycasts = group.blocksRaycasts,
                    added = added
                });
                group.interactable = false;
                group.blocksRaycasts = false;
            }
        }

        private void UnlockUnderlyingMenu()
        {
            foreach (var state in _lockedGroups)
            {
                if (state.group == null) continue;
                state.group.interactable = state.interactable;
                state.group.blocksRaycasts = state.blocksRaycasts;
                if (state.added)
                {
                    if (Application.isPlaying) Destroy(state.group);
                    else DestroyImmediate(state.group);
                }
            }
            _lockedGroups.Clear();
        }
    }

    public sealed class LeaderboardIconHover : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        private SettingsIconGraphic _graphic;
        private Outline _outline;
        private GameObject _tooltip;
        private bool _hovered;

        public void Configure(SettingsIconGraphic graphic, Outline outline,
            GameObject tooltip)
        {
            _graphic = graphic;
            _outline = outline;
            _tooltip = tooltip;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            if (_tooltip != null) _tooltip.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            if (_tooltip != null) _tooltip.SetActive(false);
        }

        private void Update()
        {
            if (_graphic == null || _outline == null) return;
            var bronze = new Color(0.65f, 0.51f, 0.33f, 1f);
            var glow = new Color(0.94f, 0.70f, 0.43f, 1f);
            _graphic.color = Color.Lerp(_graphic.color,
                _hovered ? glow : bronze, Time.unscaledDeltaTime * 12f);
            _outline.effectColor = _hovered
                ? new Color(0.66f, 0.12f, 0.12f,
                    0.6f + Mathf.Sin(Time.unscaledTime * 4f) * 0.16f)
                : new Color(0.32f, 0.25f, 0.2f, 0.95f);
        }
    }
}
