using System;
using EchoProtocol.Networking;
using EchoProtocol.Pets;
using EchoProtocol.UI.MainMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI
{
    /// <summary>
    /// Runtime lobby cosmetic selector.
    /// Ownership is loaded from the authenticated inventory cache.
    /// Unowned pets and skins remain visible but cannot be selected.
    /// </summary>
    public sealed class LobbyPetSelector : MonoBehaviour
    {
        private sealed class PetView
        {
            public int PetId;
            public Button Button;
            public Image Background;
            public Outline Outline;
            public TMP_Text Name;
            public TMP_Text State;
        }

        private sealed class SkinView
        {
            public int SkinId;
            public Button Button;
            public Image Background;
            public Outline Outline;
            public Image Swatch;
            public TMP_Text Name;
            public TMP_Text State;
        }

        private static readonly Color32 OverlayColor =
            new Color32(0, 0, 0, 205);

        private static readonly Color32 WindowColor =
            new Color32(11, 16, 18, 252);

        private static readonly Color32 CardColor =
            new Color32(17, 23, 25, 255);

        private static readonly Color32 SelectedColor =
            new Color32(35, 22, 22, 255);

        private static readonly Color32 BorderColor =
            new Color32(54, 64, 66, 230);

        private static readonly Color32 Accent =
            new Color32(156, 57, 50, 255);

        private static readonly Color32 Primary =
            new Color32(220, 224, 221, 255);

        private static readonly Color32 Secondary =
            new Color32(135, 145, 145, 255);

        private static readonly Color32 Owned =
            new Color32(116, 170, 124, 255);

        private static readonly Color32 Locked =
            new Color32(120, 92, 88, 255);

        private RectTransform _root;
        private RectTransform _window;

        private TMP_Text _selectedPetText;
        private TMP_Text _skinHeader;
        private TMP_Text _status;

        private Button _noneButton;
        private Image _noneBackground;
        private Outline _noneOutline;
        private TMP_Text _noneState;

        private readonly PetView[] _pets =
            new PetView[4];

        private readonly SkinView[] _skins =
            new SkinView[PetSkinLibrary.SkinCount];

        private LobbyPlayerState _state;

        private int _selectedPetId;
        private bool _open;
        private bool _subscribed;
        private bool _inventoryRequested;

        private float _nextRefresh;


        public void Open(
            LobbyPlayerState state)
        {
            if (state == null)
                return;

            EnsureBuilt();

            _state =
                state;

            _selectedPetId =
                state.PetId;

            _open =
                true;

            _root.gameObject.SetActive(true);
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
                _root.gameObject.SetActive(false);

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


        private void EnsureBuilt()
        {
            if (_root != null)
                return;

            var canvas =
                GetComponentInParent<Canvas>();

            if (canvas == null)
                canvas = FindAnyObjectByType<Canvas>();

            if (canvas == null)
                throw new InvalidOperationException(
                    "LobbyPetSelector requires a Canvas.");

            Transform parent =
                canvas.rootCanvas != null
                    ? canvas.rootCanvas.transform
                    : canvas.transform;


            // -------------------------------------------------
            // Full-screen blocker
            // -------------------------------------------------

            var rootObject =
                new GameObject(
                    "LobbyPetSelectorOverlay",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button));

            rootObject.transform.SetParent(
                parent,
                false);

            _root =
                rootObject.GetComponent<RectTransform>();

            Stretch(_root);

            var overlay =
                rootObject.GetComponent<Image>();

            overlay.color =
                OverlayColor;

            var backgroundButton =
                rootObject.GetComponent<Button>();

            backgroundButton.targetGraphic =
                overlay;

            backgroundButton.transition =
                Selectable.Transition.None;

            backgroundButton.onClick.AddListener(
                Close);


            // -------------------------------------------------
            // Window
            // -------------------------------------------------

            _window =
                CreateRect(
                    "Window",
                    _root,
                    0f,
                    0f,
                    840f,
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

            var windowOutline =
                _window.GetComponent<Outline>();

            windowOutline.effectColor =
                new Color32(73, 56, 54, 235);

            windowOutline.effectDistance =
                new Vector2(1f, -1f);


            CreateText(
                "Title",
                _window,
                "COMPANION LOADOUT",
                28f,
                22f,
                520f,
                34f,
                20,
                FontStyles.Bold,
                Primary);

            CreateText(
                "Subtitle",
                _window,
                "SELECT AN OWNED PET AND COLOR VARIANT",
                28f,
                54f,
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
                    722f,
                    22f,
                    90f,
                    34f);

            close.onClick.AddListener(
                Close);


            // -------------------------------------------------
            // Companion side
            // -------------------------------------------------

            CreateText(
                "CompanionHeader",
                _window,
                "COMPANIONS",
                28f,
                92f,
                365f,
                24f,
                12,
                FontStyles.Bold,
                Secondary);

            CreatePetCard(
                1,
                28f,
                124f);

            CreatePetCard(
                2,
                216f,
                124f);

            CreatePetCard(
                3,
                28f,
                216f);

            CreatePetCard(
                4,
                216f,
                216f);

            CreateNoneCard(
                28f,
                316f);


            // -------------------------------------------------
            // Skin side
            // -------------------------------------------------

            CreateText(
                "VariantHeader",
                _window,
                "COLOR VARIANTS",
                430f,
                92f,
                365f,
                24f,
                12,
                FontStyles.Bold,
                Secondary);

            _selectedPetText =
                CreateText(
                    "SelectedPet",
                    _window,
                    "NO COMPANION",
                    430f,
                    120f,
                    365f,
                    30f,
                    17,
                    FontStyles.Bold,
                    Primary);

            _skinHeader =
                CreateText(
                    "SkinHint",
                    _window,
                    "SELECT AN OWNED COMPANION",
                    430f,
                    151f,
                    365f,
                    22f,
                    9,
                    FontStyles.Normal,
                    Secondary);

            for (int i = 0;
                 i < _skins.Length;
                 i++)
            {
                CreateSkinCard(
                    i,
                    430f,
                    184f + i * 58f);
            }


            _status =
                CreateText(
                    "Status",
                    _window,
                    string.Empty,
                    430f,
                    424f,
                    365f,
                    42f,
                    10,
                    FontStyles.Bold,
                    Secondary);

            CreateText(
                "Footer",
                _window,
                "COSMETIC ONLY // DOES NOT AFFECT GAMEPLAY",
                28f,
                470f,
                560f,
                20f,
                9,
                FontStyles.Normal,
                new Color32(
                    91,
                    101,
                    102,
                    255));

            _root.gameObject.SetActive(false);
        }


        private void CreatePetCard(
            int petId,
            float x,
            float y)
        {
            var card =
                CreateRect(
                    "Pet_" + petId,
                    _window,
                    x,
                    y,
                    176f,
                    82f,
                    CardColor,
                    true);

            var button =
                card.gameObject.AddComponent<Button>();

            button.targetGraphic =
                card.GetComponent<Image>();


            var thumbObject =
                new GameObject(
                    "Thumbnail",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(RawImage));

            thumbObject.transform.SetParent(
                card,
                false);

            var thumbRect =
                thumbObject.GetComponent<RectTransform>();

            PlaceTopLeft(
                thumbRect,
                8f,
                10f,
                58f,
                58f);

            var thumb =
                thumbObject.GetComponent<RawImage>();

            thumb.texture =
                Resources.Load<Texture2D>(
                    PetShopThumbnailAssets
                        .GetThumbnailResource(
                            petId));

            thumb.color =
                thumb.texture != null
                    ? Color.white
                    : Color.clear;

            thumb.raycastTarget =
                false;


            string name =
                PetShopAssets.TryGetPet(
                    petId,
                    out var pet)
                    ? pet.Name
                    : PetCatalog.Name(petId);


            var nameText =
                CreateText(
                    "Name",
                    card,
                    name.ToUpperInvariant(),
                    73f,
                    13f,
                    94f,
                    30f,
                    10,
                    FontStyles.Bold,
                    Primary);

            nameText.enableAutoSizing =
                true;

            nameText.fontSizeMin =
                8;

            nameText.fontSizeMax =
                10;


            var stateText =
                CreateText(
                    "State",
                    card,
                    "LOCKED",
                    73f,
                    52f,
                    94f,
                    18f,
                    8,
                    FontStyles.Bold,
                    Locked);


            int captured =
                petId;

            button.onClick.AddListener(
                () => SelectPet(captured));


            _pets[petId - 1] =
                new PetView
                {
                    PetId = petId,
                    Button = button,
                    Background =
                        card.GetComponent<Image>(),
                    Outline =
                        card.GetComponent<Outline>(),
                    Name = nameText,
                    State = stateText,
                };
        }


        private void CreateNoneCard(
            float x,
            float y)
        {
            var card =
                CreateRect(
                    "NoPet",
                    _window,
                    x,
                    y,
                    364f,
                    50f,
                    CardColor,
                    true);

            _noneBackground =
                card.GetComponent<Image>();

            _noneOutline =
                card.GetComponent<Outline>();

            _noneButton =
                card.gameObject.AddComponent<Button>();

            _noneButton.targetGraphic =
                _noneBackground;

            CreateText(
                "Name",
                card,
                "NO COMPANION",
                14f,
                10f,
                220f,
                28f,
                11,
                FontStyles.Bold,
                Primary);

            _noneState =
                CreateText(
                    "State",
                    card,
                    string.Empty,
                    250f,
                    11f,
                    98f,
                    24f,
                    9,
                    FontStyles.Bold,
                    Secondary);

            _noneState.alignment =
                TextAlignmentOptions.Right;

            _noneButton.onClick.AddListener(
                SelectNone);
        }


        private void CreateSkinCard(
            int skinId,
            float x,
            float y)
        {
            var card =
                CreateRect(
                    "Skin_" + skinId,
                    _window,
                    x,
                    y,
                    365f,
                    48f,
                    CardColor,
                    true);

            var button =
                card.gameObject.AddComponent<Button>();

            button.targetGraphic =
                card.GetComponent<Image>();


            var swatchRect =
                CreateRect(
                    "Swatch",
                    card,
                    11f,
                    10f,
                    28f,
                    28f,
                    Color.black,
                    true);

            var swatch =
                swatchRect.GetComponent<Image>();


            var name =
                CreateText(
                    "Name",
                    card,
                    "---",
                    53f,
                    8f,
                    180f,
                    30f,
                    11,
                    FontStyles.Bold,
                    Primary);


            var state =
                CreateText(
                    "State",
                    card,
                    string.Empty,
                    239f,
                    10f,
                    108f,
                    26f,
                    8,
                    FontStyles.Bold,
                    Secondary);

            state.alignment =
                TextAlignmentOptions.Right;


            int captured =
                skinId;

            button.onClick.AddListener(
                () => SelectSkin(captured));


            _skins[skinId] =
                new SkinView
                {
                    SkinId = skinId,
                    Button = button,
                    Background =
                        card.GetComponent<Image>(),
                    Outline =
                        card.GetComponent<Outline>(),
                    Swatch = swatch,
                    Name = name,
                    State = state,
                };
        }


        private void SelectNone()
        {
            if (_state == null
                || _state.IsReady)
            {
                return;
            }

            if (_state.PetId != 0
                && !_state.RequestPet(0))
            {
                SetStatus(
                    "COMPANION CHANGE REJECTED.",
                    Locked);

                return;
            }

            _selectedPetId =
                0;

            RefreshAll();
        }


        private void SelectPet(
            int petId)
        {
            if (_state == null
                || _state.IsReady)
            {
                return;
            }

            if (!TeamToolOwnershipSession.IsLoaded)
            {
                SetStatus(
                    "INVENTORY NOT READY.",
                    Locked);

                return;
            }

            if (!PetShopAssets.IsPetOwned(
                    petId))
            {
                SetStatus(
                    "LOCKED // PURCHASE THIS PET IN STORE.",
                    Locked);

                return;
            }

            _selectedPetId =
                petId;

            if (_state.PetId != petId)
            {
                if (!_state.RequestPet(petId))
                {
                    SetStatus(
                        "COMPANION CHANGE REJECTED.",
                        Locked);

                    return;
                }
            }

            RefreshAll();
        }


        private void SelectSkin(
            int skinId)
        {
            if (_state == null
                || _state.IsReady
                || _selectedPetId <= 0)
            {
                return;
            }

            if (!TeamToolOwnershipSession.IsLoaded)
            {
                SetStatus(
                    "INVENTORY NOT READY.",
                    Locked);

                return;
            }

            if (_state.PetId != _selectedPetId)
            {
                SetStatus(
                    "SYNCING COMPANION SELECTION...",
                    Secondary);

                return;
            }

            if (!PetShopAssets.IsSkinOwned(
                    _selectedPetId,
                    skinId))
            {
                SetStatus(
                    "LOCKED // PURCHASE THIS COLOR IN STORE.",
                    Locked);

                return;
            }

            if (_state.PetSkinId == skinId)
            {
                RefreshAll();
                return;
            }

            if (!_state.RequestPetSkin(
                    skinId))
            {
                SetStatus(
                    "COLOR CHANGE REJECTED.",
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

            bool loaded =
                TeamToolOwnershipSession.IsLoaded;

            bool loading =
                TeamToolOwnershipSession.IsLoading;

            bool canChange =
                !_state.IsReady;


            // -------------------------------------------------
            // NONE
            // -------------------------------------------------

            bool noPetEquipped =
                _state.PetId == 0;

            bool noPetSelected =
                _selectedPetId == 0;

            _noneButton.interactable =
                canChange;

            _noneBackground.color =
                noPetSelected
                    ? SelectedColor
                    : CardColor;

            _noneOutline.effectColor =
                noPetSelected
                    ? Accent
                    : BorderColor;

            _noneState.text =
                noPetEquipped
                    ? "EQUIPPED"
                    : "AVAILABLE";

            _noneState.color =
                noPetEquipped
                    ? Owned
                    : Secondary;


            // -------------------------------------------------
            // PETS
            // -------------------------------------------------

            for (int i = 0;
                 i < _pets.Length;
                 i++)
            {
                var view =
                    _pets[i];

                if (view == null)
                    continue;

                bool owned =
                    loaded
                    && PetShopAssets.IsPetOwned(
                        view.PetId);

                bool equipped =
                    _state.PetId == view.PetId;

                bool selected =
                    _selectedPetId == view.PetId;


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
                        : BorderColor;


                if (loading)
                {
                    view.State.text =
                        "...";

                    view.State.color =
                        Secondary;
                }
                else if (!loaded)
                {
                    view.State.text =
                        "LOCKED";

                    view.State.color =
                        Locked;
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


            // -------------------------------------------------
            // SKINS
            // -------------------------------------------------

            if (_selectedPetId <= 0)
            {
                _selectedPetText.text =
                    "NO COMPANION";

                _skinHeader.text =
                    "SELECT AN OWNED COMPANION";

                for (int i = 0;
                     i < _skins.Length;
                     i++)
                {
                    _skins[i]
                        .Button
                        .gameObject
                        .SetActive(false);
                }
            }
            else
            {
                string petName =
                    PetShopAssets.TryGetPet(
                        _selectedPetId,
                        out var pet)
                        ? pet.Name
                        : PetCatalog.Name(
                            _selectedPetId);

                _selectedPetText.text =
                    petName.ToUpperInvariant();

                bool petOwned =
                    loaded
                    && PetShopAssets.IsPetOwned(
                        _selectedPetId);

                bool petSynced =
                    _state.PetId ==
                    _selectedPetId;

                _skinHeader.text =
                    petSynced
                        ? "SELECT COLOR"
                        : "SYNCING COMPANION...";


                for (int i = 0;
                     i < _skins.Length;
                     i++)
                {
                    var view =
                        _skins[i];

                    view.Button.gameObject
                        .SetActive(true);

                    string colorName =
                        PetSkinLibrary.GetColorName(
                            _selectedPetId,
                            i)
                        ?? "UNKNOWN";

                    view.Name.text =
                        colorName.ToUpperInvariant();

                    view.Swatch.color =
                        GetSwatchColor(
                            colorName);


                    bool owned =
                        petOwned
                        && PetShopAssets.IsSkinOwned(
                            _selectedPetId,
                            i);

                    bool equipped =
                        petSynced
                        && _state.PetSkinId == i;


                    view.Button.interactable =
                        canChange
                        && petSynced
                        && owned;

                    view.Background.color =
                        equipped
                            ? SelectedColor
                            : CardColor;

                    view.Outline.effectColor =
                        equipped
                            ? Accent
                            : BorderColor;


                    if (loading)
                    {
                        view.State.text =
                            "...";

                        view.State.color =
                            Secondary;
                    }
                    else if (!petOwned)
                    {
                        view.State.text =
                            "PET LOCKED";

                        view.State.color =
                            Locked;
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
                    else if (i == 0)
                    {
                        view.State.text =
                            "INCLUDED";

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
            }


            // -------------------------------------------------
            // Status
            // -------------------------------------------------

            if (loading)
            {
                SetStatus(
                    "SYNCING INVENTORY...",
                    Secondary);
            }
            else if (!loaded)
            {
                SetStatus(
                    "INVENTORY UNAVAILABLE // PET LOADOUT LOCKED.",
                    Locked);
            }
            else if (_state.IsReady)
            {
                SetStatus(
                    "UNREADY TO CHANGE YOUR LOADOUT.",
                    Locked);
            }
            else if (_selectedPetId <= 0)
            {
                SetStatus(
                    "SELECT AN OWNED COMPANION.",
                    Secondary);
            }
            else if (_state.PetId != _selectedPetId)
            {
                SetStatus(
                    "SYNCING COMPANION SELECTION...",
                    Secondary);
            }
            else
            {
                string color =
                    PetSkinLibrary.GetColorName(
                        _state.PetId,
                        _state.PetSkinId)
                    ?? "DEFAULT";

                SetStatus(
                    "EQUIPPED // " +
                    PetCatalog.Name(
                        _state.PetId)
                        .ToUpperInvariant() +
                    " // " +
                    color.ToUpperInvariant(),
                    Owned);
            }
        }


        private void SetStatus(
            string text,
            Color color)
        {
            if (_status == null)
                return;

            _status.text =
                text;

            _status.color =
                color;
        }


        private static Color GetSwatchColor(
            string name)
        {
            switch ((name ?? string.Empty)
                .ToUpperInvariant())
            {
                case "RED":
                    return new Color32(
                        145, 48, 43, 255);

                case "BLUE":
                    return new Color32(
                        55, 91, 134, 255);

                case "GREEN":
                    return new Color32(
                        70, 112, 78, 255);

                case "PURPLE":
                    return new Color32(
                        101, 69, 123, 255);

                case "ALBINO":
                    return new Color32(
                        208, 205, 190, 255);

                case "GREY":
                    return new Color32(
                        105, 111, 111, 255);

                case "BLACK":
                    return new Color32(
                        28, 31, 32, 255);

                default:
                    return new Color32(
                        90, 96, 96, 255);
            }
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
            var types =
                outline
                    ? new[]
                    {
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(Image),
                        typeof(Outline),
                    }
                    : new[]
                    {
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(Image),
                    };

            var owner =
                new GameObject(
                    name,
                    types);

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

            if (outline)
            {
                var border =
                    owner.GetComponent<Outline>();

                border.effectColor =
                    BorderColor;

                border.effectDistance =
                    new Vector2(1f, -1f);
            }

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
                    new Color32(
                        25,
                        31,
                        32,
                        255),
                    true);

            var button =
                rect.gameObject
                    .AddComponent<Button>();

            button.targetGraphic =
                rect.GetComponent<Image>();

            var label =
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

            label.alignment =
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

            rect.pivot =
                new Vector2(0.5f, 0.5f);

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