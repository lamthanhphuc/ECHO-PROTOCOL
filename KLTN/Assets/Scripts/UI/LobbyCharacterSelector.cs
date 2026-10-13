using System;
using EchoProtocol.Networking;
using EchoProtocol.UI.MainMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI
{
    public sealed class LobbyCharacterSelector : MonoBehaviour
    {
        private sealed class CharacterView
        {
            public int CharacterId;
            public Button Button;
            public Image Background;
            public Outline Outline;
            public RawImage Thumbnail;
            public TMP_Text Name;
            public TMP_Text State;
        }

        private sealed class OutfitView
        {
            public int OutfitId;
            public Button Button;
            public Image Background;
            public Outline Outline;
            public Image Swatch;
            public TMP_Text Name;
            public TMP_Text State;
        }

        private static readonly Color32 OverlayColor =
            new Color32(0, 0, 0, 210);

        private static readonly Color32 WindowColor =
            new Color32(10, 14, 16, 253);

        private static readonly Color32 CardColor =
            new Color32(17, 22, 24, 255);

        private static readonly Color32 SelectedColor =
            new Color32(36, 22, 22, 255);

        private static readonly Color32 Accent =
            new Color32(151, 58, 51, 255);

        private static readonly Color32 Border =
            new Color32(63, 70, 71, 235);

        private static readonly Color32 Primary =
            new Color32(222, 225, 221, 255);

        private static readonly Color32 Secondary =
            new Color32(132, 141, 141, 255);

        private static readonly Color32 Owned =
            new Color32(116, 169, 124, 255);

        private static readonly Color32 Locked =
            new Color32(139, 82, 78, 255);

        private static readonly string[] Names =
        {
            "ASTRONAUT",
            "JAMMO",
        };

        private static readonly string[] Descriptions =
        {
            "STANDARD EXPEDITION OPERATOR",
            "COMPACT HAZARD EXPEDITION UNIT",
        };

        private readonly CharacterView[] _views =
            new CharacterView[2];

        private RectTransform _root;
        private RectTransform _window;

        private RawImage _preview;
        private TMP_Text _previewName;
        private TMP_Text _previewDescription;
        private TMP_Text _status;

        private readonly OutfitView[] _outfits =
            new OutfitView[CharacterOutfitLibrary.OutfitCount];

        private LobbyPlayerState _state;

        private int _selectedCharacterId;
        private bool _open;
        private bool _subscribed;
        private bool _inventoryRequested;
        private float _nextRefresh;


        public static string GetThumbnailResource(
            int characterId)
        {
            return characterId == 1
                ? "Shop/Characters/jammo"
                : "Shop/Characters/astronaut";
        }


        public static void EnsureMainButtonThumbnail(
            Button button)
        {
            if (button == null)
                return;

            var existing =
                button.transform.Find(
                    "CharacterLobbyThumbnail");

            RawImage image;

            if (existing == null)
            {
                var owner =
                    new GameObject(
                        "CharacterLobbyThumbnail",
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(RawImage));

                owner.transform.SetParent(
                    button.transform,
                    false);

                var rect =
                    owner.GetComponent<RectTransform>();

                rect.anchorMin =
                    new Vector2(0f, 0.5f);

                rect.anchorMax =
                    new Vector2(0f, 0.5f);

                rect.pivot =
                    new Vector2(0f, 0.5f);

                rect.anchoredPosition =
                    new Vector2(8f, 0f);

                rect.sizeDelta =
                    new Vector2(38f, 38f);

                image =
                    owner.GetComponent<RawImage>();

                image.raycastTarget =
                    false;
            }
            else
            {
                image =
                    existing.GetComponent<RawImage>();
            }

            var label =
                button.GetComponentInChildren<TMP_Text>(
                    true);

            if (label != null)
            {
                label.margin =
                    new Vector4(
                        54f,
                        0f,
                        12f,
                        0f);

                label.alignment =
                    TextAlignmentOptions.Left;
            }

            if (image != null
                && image.texture == null)
            {
                image.texture =
                    Resources.Load<Texture2D>(
                        GetThumbnailResource(0));

                image.color =
                    image.texture != null
                        ? Color.white
                        : Color.clear;
            }
        }


        public static void RefreshMainButtonThumbnail(
            Button button,
            int characterId)
        {
            if (button == null)
                return;

            EnsureMainButtonThumbnail(
                button);

            var target =
                button.transform.Find(
                    "CharacterLobbyThumbnail");

            if (target == null)
                return;

            var image =
                target.GetComponent<RawImage>();

            if (image == null)
                return;

            image.texture =
                Resources.Load<Texture2D>(
                    GetThumbnailResource(
                        characterId));

            image.color =
                image.texture != null
                    ? Color.white
                    : Color.clear;
        }


        public void Open(
            LobbyPlayerState state)
        {
            if (state == null)
                return;

            EnsureBuilt();

            _state =
                state;

            _selectedCharacterId =
                Mathf.Clamp(
                    state.CharacterId,
                    0,
                    1);

            _open =
                true;

            _root.gameObject.SetActive(
                true);

            _root.SetAsLastSibling();

            SubscribeOwnership();

            if (!TeamToolOwnershipSession.IsLoaded
                && !TeamToolOwnershipSession.IsLoading
                && !_inventoryRequested)
            {
                _inventoryRequested =
                    true;

                TeamToolOwnershipSession.Refresh(
                    success =>
                    {
                        if (this == null)
                            return;

                        _inventoryRequested =
                            false;

                        if (_open)
                            RefreshAll();
                    });
            }

            RefreshAll();
        }


        public void Close()
        {
            _open =
                false;

            _state =
                null;

            if (_root != null)
                _root.gameObject.SetActive(
                    false);

            UnsubscribeOwnership();
        }


        private void Update()
        {
            if (!_open
                || _state == null
                || Time.unscaledTime < _nextRefresh)
            {
                return;
            }

            _nextRefresh =
                Time.unscaledTime + 0.15f;

            RefreshAll();
        }


        private void OnDisable()
        {
            Close();
        }


        private void OnDestroy()
        {
            UnsubscribeOwnership();
        }


        private void SubscribeOwnership()
        {
            if (_subscribed)
                return;

            TeamToolOwnershipSession.Changed +=
                HandleOwnershipChanged;

            _subscribed =
                true;
        }


        private void UnsubscribeOwnership()
        {
            if (!_subscribed)
                return;

            TeamToolOwnershipSession.Changed -=
                HandleOwnershipChanged;

            _subscribed =
                false;
        }


        private void HandleOwnershipChanged()
        {
            if (_open)
                RefreshAll();
        }


        private bool IsOwned(
            int characterId)
        {
            if (characterId == 0)
                return true;

            return TeamToolOwnershipSession.IsLoaded
                && TeamToolOwnershipSession.OwnsItem(
                    CharacterShopAssets.JammoItemId);
        }


        private void EnsureBuilt()
        {
            if (_root != null)
                return;

            var canvas =
                GetComponentInParent<Canvas>();

            if (canvas == null)
                canvas =
                    FindAnyObjectByType<Canvas>();

            if (canvas == null)
            {
                throw new InvalidOperationException(
                    "LobbyCharacterSelector requires a Canvas.");
            }

            Transform parent =
                canvas.rootCanvas != null
                    ? canvas.rootCanvas.transform
                    : canvas.transform;


            // =================================================
            // OVERLAY
            // =================================================

            var overlayObject =
                new GameObject(
                    "LobbyCharacterSelectorOverlay",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button));

            overlayObject.transform.SetParent(
                parent,
                false);

            _root =
                overlayObject.GetComponent<RectTransform>();

            Stretch(
                _root);

            var overlay =
                overlayObject.GetComponent<Image>();

            overlay.color =
                OverlayColor;

            var blocker =
                overlayObject.GetComponent<Button>();

            blocker.transition =
                Selectable.Transition.None;

            blocker.targetGraphic =
                overlay;

            blocker.onClick.AddListener(
                Close);


            // =================================================
            // WINDOW
            // =================================================

            _window =
                CreateRect(
                    "CharacterWindow",
                    _root,
                    0f,
                    0f,
                    850f,
                    520f,
                    WindowColor,
                    true);

            _window.anchorMin =
                new Vector2(0.5f, 0.5f);

            _window.anchorMax =
                new Vector2(0.5f, 0.5f);

            _window.pivot =
                new Vector2(0.5f, 0.5f);

            _window.anchoredPosition =
                Vector2.zero;


            CreateText(
                "Header",
                _window,
                "CHARACTER",
                28f,
                20f,
                520f,
                34f,
                22,
                FontStyles.Bold,
                Primary);


            CreateText(
                "SubHeader",
                _window,
                "SELECT YOUR OPERATOR",
                28f,
                53f,
                520f,
                20f,
                10,
                FontStyles.Normal,
                Secondary);


            var close =
                CreateButton(
                    "Close",
                    _window,
                    "CLOSE",
                    730f,
                    20f,
                    92f,
                    34f);

            close.onClick.AddListener(
                Close);


            // =================================================
            // LARGE DEVOUR-LIKE PREVIEW
            // =================================================

            var previewFrame =
                CreateRect(
                    "PreviewFrame",
                    _window,
                    28f,
                    88f,
                    350f,
                    350f,
                    new Color32(
                        7,
                        10,
                        11,
                        255),
                    true);

            var previewObject =
                new GameObject(
                    "CharacterPreview",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(RawImage));

            previewObject.transform.SetParent(
                previewFrame,
                false);

            var previewRect =
                previewObject.GetComponent<RectTransform>();

            previewRect.anchorMin =
                Vector2.zero;

            previewRect.anchorMax =
                Vector2.one;

            previewRect.offsetMin =
                new Vector2(8f, 8f);

            previewRect.offsetMax =
                new Vector2(-8f, -8f);

            _preview =
                previewObject.GetComponent<RawImage>();

            _preview.raycastTarget =
                false;


            _previewName =
                CreateText(
                    "PreviewName",
                    _window,
                    "ASTRONAUT",
                    28f,
                    449f,
                    350f,
                    28f,
                    17,
                    FontStyles.Bold,
                    Primary);

            _previewName.alignment =
                TextAlignmentOptions.Center;


            _previewDescription =
                CreateText(
                    "PreviewDescription",
                    _window,
                    Descriptions[0],
                    28f,
                    479f,
                    350f,
                    20f,
                    9,
                    FontStyles.Normal,
                    Secondary);

            _previewDescription.alignment =
                TextAlignmentOptions.Center;


            // =================================================
            // CHARACTER CARDS
            // =================================================

            CreateText(
                "CharactersLabel",
                _window,
                "OPERATORS",
                408f,
                88f,
                410f,
                22f,
                11,
                FontStyles.Bold,
                Secondary);

            CreateCharacterCard(
                0,
                408f,
                120f);

            CreateCharacterCard(
                1,
                408f,
                220f);


            // =================================================
            // OUTFIT COLOR SELECTOR
            // =================================================

            CreateText(
                "OutfitHeader",
                _window,
                "OUTFIT",
                408f,
                334f,
                410f,
                22f,
                11,
                FontStyles.Bold,
                Secondary);


            for (int i = 0;
                 i < CharacterOutfitLibrary.OutfitCount;
                 i++)
            {
                CreateOutfitCard(
                    i,
                    408f + i * 103f,
                    364f);
            }


            _status =
                CreateText(
                    "Status",
                    _window,
                    string.Empty,
                    408f,
                    440f,
                    410f,
                    48f,
                    10,
                    FontStyles.Bold,
                    Secondary);


            _root.gameObject.SetActive(
                false);
        }


        private void CreateCharacterCard(
            int characterId,
            float x,
            float y)
        {
            var card =
                CreateRect(
                    "Character_" + characterId,
                    _window,
                    x,
                    y,
                    410f,
                    84f,
                    CardColor,
                    true);

            var background =
                card.GetComponent<Image>();

            var button =
                card.gameObject.AddComponent<Button>();

            button.targetGraphic =
                background;


            var thumbnailObject =
                new GameObject(
                    "Thumbnail",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(RawImage));

            thumbnailObject.transform.SetParent(
                card,
                false);

            var thumbnailRect =
                thumbnailObject.GetComponent<RectTransform>();

            PlaceTopLeft(
                thumbnailRect,
                8f,
                8f,
                68f,
                68f);

            var thumbnail =
                thumbnailObject.GetComponent<RawImage>();

            thumbnail.texture =
                Resources.Load<Texture2D>(
                    GetThumbnailResource(
                        characterId));

            thumbnail.color =
                thumbnail.texture != null
                    ? Color.white
                    : Color.clear;

            thumbnail.raycastTarget =
                false;


            var name =
                CreateText(
                    "Name",
                    card,
                    Names[characterId],
                    92f,
                    15f,
                    190f,
                    25f,
                    13,
                    FontStyles.Bold,
                    Primary);


            var description =
                CreateText(
                    "Description",
                    card,
                    Descriptions[characterId],
                    92f,
                    42f,
                    220f,
                    20f,
                    8,
                    FontStyles.Normal,
                    Secondary);


            var state =
                CreateText(
                    "State",
                    card,
                    characterId == 0
                        ? "OWNED"
                        : "LOCKED",
                    310f,
                    29f,
                    82f,
                    24f,
                    9,
                    FontStyles.Bold,
                    characterId == 0
                        ? Owned
                        : Locked);

            state.alignment =
                TextAlignmentOptions.Right;


            int captured =
                characterId;

            button.onClick.AddListener(
                () => SelectCharacter(
                    captured));


            _views[characterId] =
                new CharacterView
                {
                    CharacterId =
                        characterId,

                    Button =
                        button,

                    Background =
                        background,

                    Outline =
                        card.GetComponent<Outline>(),

                    Thumbnail =
                        thumbnail,

                    Name =
                        name,

                    State =
                        state,
                };
        }


        private void CreateOutfitCard(
            int outfitId,
            float x,
            float y)
        {
            var card =
                CreateRect(
                    "Outfit_" + outfitId,
                    _window,
                    x,
                    y,
                    92f,
                    68f,
                    CardColor,
                    true);

            var background =
                card.GetComponent<Image>();

            var button =
                card.gameObject.AddComponent<Button>();

            button.targetGraphic =
                background;


            var swatchRect =
                CreateRect(
                    "Swatch",
                    card,
                    8f,
                    8f,
                    24f,
                    24f,
                    CharacterOutfitLibrary.GetSwatch(
                        0,
                        outfitId),
                    true);

            var swatch =
                swatchRect.GetComponent<Image>();


            var name =
                CreateText(
                    "Name",
                    card,
                    CharacterOutfitLibrary.GetName(
                        0,
                        outfitId),
                    38f,
                    7f,
                    47f,
                    27f,
                    8,
                    FontStyles.Bold,
                    Primary);

            name.enableAutoSizing =
                true;

            name.fontSizeMin =
                6;

            name.fontSizeMax =
                8;


            var state =
                CreateText(
                    "State",
                    card,
                    "INCLUDED",
                    8f,
                    42f,
                    76f,
                    18f,
                    7,
                    FontStyles.Bold,
                    Owned);

            state.alignment =
                TextAlignmentOptions.Center;


            int captured =
                outfitId;

            button.onClick.AddListener(
                () => SelectOutfit(
                    captured));


            _outfits[outfitId] =
                new OutfitView
                {
                    OutfitId = outfitId,
                    Button = button,
                    Background = background,
                    Outline =
                        card.GetComponent<Outline>(),
                    Swatch = swatch,
                    Name = name,
                    State = state,
                };
        }

        private void SelectCharacter(
            int characterId)
        {
            if (_state == null
                || _state.IsReady)
            {
                return;
            }

            if (!IsOwned(
                    characterId))
            {
                SetStatus(
                    "LOCKED // PURCHASE JAMMO IN STORE.",
                    Locked);

                return;
            }

            _selectedCharacterId =
                characterId;

            if (_state.CharacterId
                != characterId)
            {
                if (!_state.RequestCharacter(
                        characterId))
                {
                    SetStatus(
                        "CHARACTER CHANGE REJECTED.",
                        Locked);

                    return;
                }
            }

            RefreshAll();
        }


        private void SelectOutfit(
            int outfitId)
        {
            if (_state == null
                || _state.IsReady)
            {
                return;
            }

            if (_selectedCharacterId < 0
                || _selectedCharacterId > 1)
            {
                return;
            }

            if (_state.CharacterId
                != _selectedCharacterId)
            {
                SetStatus(
                    "SYNCING CHARACTER...",
                    Secondary);

                return;
            }

            int safeOutfit =
                Mathf.Clamp(
                    outfitId,
                    0,
                    CharacterOutfitLibrary.OutfitCount - 1);

            // Store preview is unrestricted, but Lobby equip requires
            // ownership of both the Character and selected outfit.
            if (!EchoProtocol.UI.MainMenu.CharacterOutfitShopAssets.IsOwned(
                    _selectedCharacterId,
                    safeOutfit))
            {
                Debug.LogWarning(
                    "[Lobby] Character outfit locked: "
                    + _selectedCharacterId
                    + ":"
                    + safeOutfit
                    + " // purchase this outfit in Store.",
                    this);

                return;
            }

            if (_state.CharacterOutfitId
                == safeOutfit)
            {
                RefreshAll();
                return;
            }

            if (!_state.RequestCharacterOutfit(
                    safeOutfit))
            {
                SetStatus(
                    "OUTFIT CHANGE REJECTED.",
                    Locked);

                return;
            }

            RefreshAll();
        }

        private void RefreshAll()
        {
            if (!_open
                || _state == null)
            {
                return;
            }

            bool canChange =
                !_state.IsReady;

            bool loading =
                TeamToolOwnershipSession.IsLoading;

            for (int i = 0;
                 i < _views.Length;
                 i++)
            {
                var view =
                    _views[i];

                if (view == null)
                    continue;

                bool owned =
                    IsOwned(
                        view.CharacterId);

                bool equipped =
                    _state.CharacterId
                    == view.CharacterId;

                bool selected =
                    _selectedCharacterId
                    == view.CharacterId;


                view.Button.interactable =
                    canChange
                    && owned;

                view.Background.color =
                    selected
                        ? SelectedColor
                        : CardColor;

                view.Outline.effectColor =
                    selected
                        ? Accent
                        : Border;


                if (loading
                    && view.CharacterId == 1)
                {
                    view.State.text =
                        "...";

                    view.State.color =
                        Secondary;
                }
                else if (!owned)
                {
                    view.State.text =
                        "LOCKED";

                    view.State.color =
                        Locked;
                }
                else if (equipped)
                {
                    view.State.text =
                        "EQUIPPED";

                    view.State.color =
                        Owned;
                }
                else
                {
                    view.State.text =
                        "OWNED";

                    view.State.color =
                        Owned;
                }
            }


            _preview.texture =
                Resources.Load<Texture2D>(
                    GetThumbnailResource(
                        _selectedCharacterId));

            _preview.color =
                _preview.texture != null
                    ? Color.white
                    : Color.clear;


            _previewName.text =
                Names[_selectedCharacterId];

            _previewDescription.text =
                Descriptions[_selectedCharacterId];


            bool characterSynced =
                _state.CharacterId
                == _selectedCharacterId;

            for (int i = 0;
                 i < _outfits.Length;
                 i++)
            {
                var outfit =
                    _outfits[i];

                if (outfit == null)
                    continue;

                string outfitName =
                    CharacterOutfitLibrary.GetName(
                        _selectedCharacterId,
                        outfit.OutfitId);

                outfit.Name.text =
                    outfitName;

                outfit.Swatch.color =
                    CharacterOutfitLibrary.GetSwatch(
                        _selectedCharacterId,
                        outfit.OutfitId);

            bool outfitOwned =
                EchoProtocol.UI.MainMenu.CharacterOutfitShopAssets.IsOwned(
                    _selectedCharacterId,
                    outfit.OutfitId);

            bool outfitDefault =
                outfit.OutfitId == 0;
                bool equipped =
                    characterSynced
                    && _state.CharacterOutfitId
                        == outfit.OutfitId;

            outfit.Button.interactable =
                outfitOwned
                && !equipped;

                outfit.Background.color =
                    equipped
                        ? SelectedColor
                        : CardColor;

                outfit.Outline.effectColor =
                    equipped
                        ? Accent
                        : Border;

            outfit.State.text =
                equipped
                    ? "EQUIPPED"
                    : outfitDefault
                        ? "DEFAULT"
                        : outfitOwned
                            ? "OWNED"
                            : "LOCKED";

            outfit.State.color =
                outfitOwned
                    ? Owned
                    : new Color32(
                        145,
                        150,
                        156,
                        255);
            }


            if (_state.IsReady)
            {
                SetStatus(
                    "UNREADY TO CHANGE LOADOUT.",
                    Locked);
            }
            else if (!IsOwned(
                         _selectedCharacterId))
            {
                SetStatus(
                    "LOCKED // PURCHASE THIS CHARACTER IN STORE.",
                    Locked);
            }
            else if (_state.CharacterId
                     != _selectedCharacterId)
            {
                SetStatus(
                    "SYNCING CHARACTER...",
                    Secondary);
            }
            else
            {
                SetStatus(
                    "EQUIPPED // "
                    + Names[
                        _state.CharacterId],
                    Owned);
            }
        }


        private void SetStatus(
            string value,
            Color color)
        {
            if (_status == null)
                return;

            _status.text =
                value;

            _status.color =
                color;
        }


        private static RectTransform CreateRect(
            string name,
            Transform parent,
            float x,
            float y,
            float width,
            float height,
            Color color,
            bool outline)
        {
            var owner =
                new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Outline));

            owner.transform.SetParent(
                parent,
                false);

            var rect =
                owner.GetComponent<RectTransform>();

            PlaceTopLeft(
                rect,
                x,
                y,
                width,
                height);

            owner.GetComponent<Image>().color =
                color;

            var border =
                owner.GetComponent<Outline>();

            border.enabled =
                outline;

            border.effectColor =
                Border;

            border.effectDistance =
                new Vector2(
                    1f,
                    -1f);

            return rect;
        }


        private static Button CreateButton(
            string name,
            Transform parent,
            string caption,
            float x,
            float y,
            float width,
            float height)
        {
            var rect =
                CreateRect(
                    name,
                    parent,
                    x,
                    y,
                    width,
                    height,
                    CardColor,
                    true);

            var button =
                rect.gameObject.AddComponent<Button>();

            button.targetGraphic =
                rect.GetComponent<Image>();


            var text =
                CreateText(
                    "Label",
                    rect,
                    caption,
                    0f,
                    0f,
                    width,
                    height,
                    10,
                    FontStyles.Bold,
                    Primary);

            text.alignment =
                TextAlignmentOptions.Center;

            return button;
        }


        private static TMP_Text CreateText(
            string name,
            Transform parent,
            string content,
            float x,
            float y,
            float width,
            float height,
            int size,
            FontStyles style,
            Color color)
        {
            var owner =
                new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));

            owner.transform.SetParent(
                parent,
                false);

            var rect =
                owner.GetComponent<RectTransform>();

            PlaceTopLeft(
                rect,
                x,
                y,
                width,
                height);

            var text =
                owner.GetComponent<TextMeshProUGUI>();

            text.text =
                content;

            text.fontSize =
                size;

            text.fontStyle =
                style;

            text.color =
                color;

            text.alignment =
                TextAlignmentOptions.Left;

            text.raycastTarget =
                false;

            text.overflowMode =
                TextOverflowModes.Ellipsis;

            return text;
        }


        private static void Stretch(
            RectTransform rect)
        {
            rect.anchorMin =
                Vector2.zero;

            rect.anchorMax =
                Vector2.one;

            rect.offsetMin =
                Vector2.zero;

            rect.offsetMax =
                Vector2.zero;
        }


        private static void PlaceTopLeft(
            RectTransform rect,
            float x,
            float y,
            float width,
            float height)
        {
            rect.anchorMin =
                new Vector2(0f, 1f);

            rect.anchorMax =
                new Vector2(0f, 1f);

            rect.pivot =
                new Vector2(0f, 1f);

            rect.anchoredPosition =
                new Vector2(
                    x,
                    -y);

            rect.sizeDelta =
                new Vector2(
                    width,
                    height);
        }
    }
}