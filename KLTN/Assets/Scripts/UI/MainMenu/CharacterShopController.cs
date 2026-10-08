using System;
using EchoProtocol.Api;
using EchoProtocol.Auth;
using EchoProtocol.Profile;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.MainMenu
{
    [DisallowMultipleComponent]
    public sealed class CharacterShopController : MonoBehaviour
    {
        [Serializable]
        private sealed class CatalogResponse
        {
            public bool success;
            public string message;
            public CatalogData data;
        }

        [Serializable]
        private sealed class CatalogData
        {
            public ShopItem[] items;
        }

        [Serializable]
        private sealed class ShopItem
        {
            public string itemId;
            public string name;
            public string description;
            public string assetReference;
            public int price;
        }

        [Serializable]
        private sealed class PurchaseRequest
        {
            public string itemId;
            public string idempotencyKey;
        }

        [Serializable]
        private sealed class PurchaseResponse
        {
            public bool success;
            public string message;
        }

        private static readonly Color PanelBackground =
            new Color32(14, 18, 19, 238);

        private static readonly Color CardBackground =
            new Color32(19, 23, 24, 245);

        private static readonly Color Border =
            new Color32(58, 66, 66, 230);

        private static readonly Color TextPrimary =
            new Color32(222, 224, 219, 255);

        private static readonly Color TextSecondary =
            new Color32(128, 136, 133, 255);

        private static readonly Color Credits =
            new Color32(193, 178, 130, 255);

        private static readonly Color Owned =
            new Color32(122, 170, 127, 255);

        private RectTransform _root;
        private RectTransform _grid;
        private RawImage _preview;

        private Text _selectedName;
        private Text _selectedDescription;
        private Text _selectedPrice;
        private Text _status;
        private Text _credits;

        private Button _buyButton;

        private ShopItem[] _catalog =
            Array.Empty<ShopItem>();

        private CharacterShopAssets.Item _selectedDefinition;
        private ShopItem _selectedItem;

        private int _selectedCharacterId = -1;

        private bool _built;
        private bool _loading;
        private bool _purchasing;
        private bool _previewMode;

        public void EnsureBuilt()
        {
            if (_built)
                return;

            var content =
                transform.Find(
                    "Window/Content")
                as RectTransform;

            if (content == null)
            {
                Debug.LogError(
                    "[CharacterStore] Store content missing.",
                    this);

                return;
            }

            _status =
                transform.Find(
                    "Window/Content/Status")
                    ?.GetComponent<Text>();

            _credits =
                transform.Find(
                    "Window/CreditsBox/Amount")
                    ?.GetComponent<Text>();

            _root =
                Box(
                    "CharacterContent",
                    content);

            Stretch(
                _root,
                new Vector2(18f, 18f),
                new Vector2(-18f, -70f));

            _root.GetComponent<Image>().color =
                Color.clear;

            BuildCharacterContent();

            _root.gameObject.SetActive(false);

            _built = true;
        }

        public void SetVisible(
            bool visible,
            bool previewMode)
        {
            EnsureBuilt();

            if (!_built)
                return;

            _previewMode =
                previewMode;

            _root.gameObject.SetActive(
                visible);

            if (!visible)
                return;

            if (_previewMode)
            {
                _catalog =
                    BuildPreviewCatalog();

                ShowCharacters(
                    _selectedCharacterId);

                SetStatus(
                    "SELECT A CHARACTER.");

                return;
            }

            LoadCatalog();
        }

        private void BuildCharacterContent()
        {
            var previewBox =
                Box(
                    "CharacterPreviewBox",
                    _root);

            PlaceTopLeft(
                previewBox,
                0f,
                60f,
                392f,
                228f);

            var previewObject =
                new GameObject(
                    "CharacterPreview",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(RawImage));

            previewObject.transform.SetParent(
                previewBox,
                false);

            var previewRect =
                previewObject.transform
                as RectTransform;

            PlaceCentered(
                previewRect,
                184f,
                184f);

            _preview =
                previewObject
                    .GetComponent<RawImage>();

            _preview.raycastTarget =
                false;

            _preview.color =
                Color.clear;

            _grid =
                Box(
                    "CharacterGrid",
                    _root);

            PlaceTopLeft(
                _grid,
                408f,
                60f,
                392f,
                228f);

            _grid.GetComponent<Image>().color =
                Color.clear;

            var layout =
                _grid.gameObject
                    .AddComponent<GridLayoutGroup>();

            layout.cellSize =
                new Vector2(190f, 108f);

            layout.spacing =
                new Vector2(12f, 12f);

            layout.constraint =
                GridLayoutGroup.Constraint.FixedColumnCount;

            layout.constraintCount = 2;

            _selectedName =
                Label(
                    "SelectedCharacterName",
                    _root,
                    "SELECT A CHARACTER",
                    17);

            PlaceTopLeft(
                _selectedName.rectTransform,
                0f,
                304f,
                470f,
                28f);

            _selectedName.fontStyle =
                FontStyle.Bold;

            _selectedDescription =
                Label(
                    "SelectedCharacterDescription",
                    _root,
                    string.Empty,
                    11);

            PlaceTopLeft(
                _selectedDescription.rectTransform,
                0f,
                334f,
                530f,
                42f);

            _selectedDescription.color =
                new Color32(
                    150, 158, 155, 255);

            _selectedDescription.horizontalOverflow =
                HorizontalWrapMode.Wrap;

            _selectedDescription.verticalOverflow =
                VerticalWrapMode.Overflow;

            _selectedPrice =
                Label(
                    "SelectedCharacterPrice",
                    _root,
                    "-- CR",
                    15);

            PlaceTopLeft(
                _selectedPrice.rectTransform,
                544f,
                332f,
                110f,
                34f);

            _selectedPrice.alignment =
                TextAnchor.MiddleRight;

            _selectedPrice.color =
                Credits;

            _buyButton =
                CreateButton(
                    "CharacterBuyButton",
                    _root,
                    "BUY");

            PlaceTopLeft(
                _buyButton.transform
                    as RectTransform,
                668f,
                326f,
                132f,
                44f);

            _buyButton.onClick.AddListener(
                PurchaseSelected);
        }

        private void LoadCatalog()
        {
            if (_loading)
                return;

            var runtime =
                AuthRuntime.EnsureExists();

            if (runtime == null
                || runtime.Client == null)
            {
                _catalog =
                    Array.Empty<ShopItem>();

                ShowCharacters(
                    _selectedCharacterId);

                SetStatus(
                    "CHARACTER STORE SERVICE UNAVAILABLE.");

                return;
            }

            _loading = true;

            SetStatus(
                "LOADING CHARACTERS...");

            runtime.Client.GetJson<CatalogResponse>(
                ApiEndpoints.ShopCharacters,
                true,
                result =>
                {
                    if (this == null)
                        return;

                    _loading = false;

                    if (!result.IsSuccess
                        || result.Data == null
                        || !result.Data.success
                        || result.Data.data == null)
                    {
                        _catalog =
                            Array.Empty<ShopItem>();

                        ShowCharacters(
                            _selectedCharacterId);

                        SetStatus(
                            "CHARACTER CATALOG UNAVAILABLE.");

                        return;
                    }

                    _catalog =
                        result.Data.data.items
                        ?? Array.Empty<ShopItem>();

                    TeamToolOwnershipSession.Refresh(
                        success =>
                        {
                            if (this == null)
                                return;

                            ShowCharacters(
                                _selectedCharacterId);

                            SetStatus(
                                success
                                    ? "SELECT A CHARACTER."
                                    : "INVENTORY STATUS UNAVAILABLE.");
                        });
                });
        }

        private void ShowCharacters(
            int preferredCharacterId = -1)
        {
            if (_grid == null)
                return;

            ClearChildren(
                _grid);

            bool selectedPreferred =
                false;

            CharacterShopAssets.Item first =
                default;

            ShopItem firstItem =
                null;

            bool hasFirst =
                false;

            for (int i = 0;
                 i < CharacterShopAssets.Items.Length;
                 i++)
            {
                var definition =
                    CharacterShopAssets.Items[i];

                ShopItem item =
                    definition.DefaultOwned
                        ? BuildBuiltinItem(
                            definition)
                        : FindItem(
                            _catalog,
                            definition.ItemId);

                if (!hasFirst)
                {
                    first =
                        definition;

                    firstItem =
                        item;

                    hasFirst =
                        true;
                }

                CreateCharacterCard(
                    definition,
                    item);

                if (definition.CharacterId
                    == preferredCharacterId)
                {
                    SelectCharacter(
                        definition,
                        item);

                    selectedPreferred =
                        true;
                }
            }

            if (!selectedPreferred
                && hasFirst)
            {
                SelectCharacter(
                    first,
                    firstItem);
            }
        }

        private void CreateCharacterCard(
            CharacterShopAssets.Item definition,
            ShopItem item)
        {
            var card =
                Box(
                    "Character_" +
                    definition.CharacterId,
                    _grid);

            card.GetComponent<Image>().color =
                CardBackground;

            var button =
                card.gameObject
                    .AddComponent<Button>();

            button.targetGraphic =
                card.GetComponent<Image>();

            var imageObject =
                new GameObject(
                    "Thumbnail",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(RawImage));

            imageObject.transform.SetParent(
                card,
                false);

            var imageRect =
                imageObject.transform
                as RectTransform;

            PlaceTopLeft(
                imageRect,
                10f,
                18f,
                72f,
                72f);

            var thumbnail =
                imageObject
                    .GetComponent<RawImage>();

            thumbnail.texture =
                Resources.Load<Texture2D>(
                    definition.ThumbnailResource);

            thumbnail.color =
                thumbnail.texture != null
                    ? Color.white
                    : Color.clear;

            thumbnail.raycastTarget =
                false;

            var name =
                Label(
                    "Name",
                    card,
                    definition.Name,
                    11);

            PlaceTopLeft(
                name.rectTransform,
                96f,
                14f,
                86f,
                34f);

            name.fontStyle =
                FontStyle.Bold;

            name.resizeTextForBestFit =
                true;

            name.resizeTextMinSize = 9;
            name.resizeTextMaxSize = 11;

            bool owned =
                IsOwned(
                    definition);

            string state;

            if (owned)
            {
                state =
                    definition.DefaultOwned
                        ? "DEFAULT"
                        : "OWNED";
            }
            else if (item == null)
            {
                state =
                    "UNAVAILABLE";
            }
            else
            {
                state =
                    $"{item.price:N0} CR";
            }

            var stateText =
                Label(
                    "State",
                    card,
                    state,
                    10);

            PlaceTopLeft(
                stateText.rectTransform,
                96f,
                70f,
                86f,
                18f);

            stateText.color =
                owned
                    ? Owned
                    : item == null
                        ? TextSecondary
                        : Credits;

            var capturedDefinition =
                definition;

            var capturedItem =
                item;

            button.onClick.AddListener(
                () =>
                    SelectCharacter(
                        capturedDefinition,
                        capturedItem));
        }

        private void SelectCharacter(
            CharacterShopAssets.Item definition,
            ShopItem item)
        {
            _selectedDefinition =
                definition;

            _selectedCharacterId =
                definition.CharacterId;

            _selectedItem =
                item;

            _selectedName.text =
                definition.Name;

            _selectedDescription.text =
                !string.IsNullOrWhiteSpace(
                    item?.description)
                    ? item.description
                    : definition.Description;

            if (definition.DefaultOwned)
            {
                _selectedPrice.text =
                    "DEFAULT";
            }
            else if (item != null)
            {
                _selectedPrice.text =
                    $"{item.price:N0} CR";
            }
            else
            {
                _selectedPrice.text =
                    "-- CR";
            }

            var fallbackTexture =
                Resources.Load<Texture2D>(
                    definition.ThumbnailResource);

            _preview.texture =
                fallbackTexture;

            _preview.color =
                fallbackTexture != null
                    ? Color.white
                    : Color.clear;

            var livePreview =
                ResolveLivePreviewRenderer();

            if (livePreview != null)
            {
                bool liveShown =
                    livePreview.Show(
                        definition.LivePreviewResource,
                        _preview);

                if (!liveShown)
                {
                    _preview.texture =
                        fallbackTexture;

                    _preview.color =
                        fallbackTexture != null
                            ? Color.white
                            : Color.clear;
                }
            }

            SetBuyState();
        }

        private void SetBuyState()
        {
            if (_buyButton == null)
                return;

            var caption =
                _buyButton
                    .GetComponentInChildren<Text>();

            if (_selectedCharacterId < 0)
            {
                _buyButton.interactable =
                    false;

                if (caption != null)
                    caption.text =
                        "UNAVAILABLE";

                return;
            }

            bool owned =
                IsOwned(
                    _selectedDefinition);

            if (owned)
            {
                _buyButton.interactable =
                    false;

                if (caption != null)
                {
                    caption.text =
                        _selectedDefinition.DefaultOwned
                            ? "DEFAULT"
                            : "OWNED";
                }

                return;
            }

            if (_previewMode)
            {
                _buyButton.interactable =
                    true;

                if (caption != null)
                    caption.text = "BUY";

                return;
            }

            if (_selectedItem == null)
            {
                _buyButton.interactable =
                    false;

                if (caption != null)
                    caption.text =
                        "UNAVAILABLE";

                return;
            }

            _buyButton.interactable =
                !_purchasing;

            if (caption != null)
            {
                caption.text =
                    _purchasing
                        ? "PURCHASING..."
                        : "BUY";
            }
        }

        private void PurchaseSelected()
        {
            if (_previewMode)
            {
                SetStatus(
                    "EDITOR PREVIEW MODE.");

                return;
            }

            if (_purchasing
                || _selectedItem == null
                || _selectedDefinition.DefaultOwned
                || IsOwned(_selectedDefinition))
            {
                return;
            }

            var runtime =
                AuthRuntime.EnsureExists();

            if (runtime == null
                || runtime.Client == null)
            {
                SetStatus(
                    "STORE SERVICE UNAVAILABLE.");

                return;
            }

            var purchasingItem =
                _selectedItem;

            int purchasingCharacterId =
                _selectedCharacterId;

            _purchasing =
                true;

            SetStatus(
                "PURCHASING " +
                _selectedDefinition.Name +
                "...");

            SetBuyState();

            runtime.Client.PostJson<
                PurchaseRequest,
                PurchaseResponse>(
                ApiEndpoints.ShopPurchase,
                new PurchaseRequest
                {
                    itemId =
                        purchasingItem.itemId,

                    idempotencyKey =
                        Guid.NewGuid()
                            .ToString("N")
                },
                true,
                result =>
                {
                    if (this == null)
                        return;

                    _purchasing =
                        false;

                    if (!result.IsSuccess
                        || result.Data == null
                        || !result.Data.success)
                    {
                        SetStatus(
                            string.IsNullOrWhiteSpace(
                                result.Message)
                                ? "PURCHASE FAILED."
                                : result.Message
                                    .ToUpperInvariant());

                        SetBuyState();

                        return;
                    }

                    TeamToolOwnershipSession.MarkOwned(
                        purchasingItem.itemId);

                    SetStatus(
                        _selectedDefinition.Name +
                        " PURCHASED.");

                    ShowCharacters(
                        purchasingCharacterId);

                    runtime.PlayerProfileService
                        .GetCurrentProfile(
                            _ =>
                            {
                                if (this == null)
                                    return;

                                RefreshCredits();
                            });
                });
        }

        private bool IsOwned(
            CharacterShopAssets.Item definition)
        {
            return definition.DefaultOwned
                || TeamToolOwnershipSession
                    .OwnsItem(
                        definition.ItemId);
        }

        private StoreLivePreviewRenderer ResolveLivePreviewRenderer()
        {
            var renderer =
                GetComponent<StoreLivePreviewRenderer>();

            if (renderer == null)
            {
                renderer =
                    gameObject.AddComponent<StoreLivePreviewRenderer>();
            }

            renderer.EnsureInitialized();

            return renderer;
        }
        private void RefreshCredits()
        {
            if (_credits == null)
                return;

            int balance =
                PlayerProfileSession.HasProfile
                    ? PlayerProfileSession
                        .WalletBalance
                    : AuthSession.WalletBalance;

            _credits.text =
                $"{balance:N0}";
        }

        private void SetStatus(
            string message)
        {
            if (_status != null)
            {
                _status.text =
                    message ?? string.Empty;
            }
        }

        private static ShopItem BuildBuiltinItem(
            CharacterShopAssets.Item definition)
        {
            return new ShopItem
            {
                itemId =
                    definition.ItemId,

                name =
                    definition.Name,

                description =
                    definition.Description,

                assetReference =
                    definition.PrefabPath,

                price = 0
            };
        }

        private static ShopItem[] BuildPreviewCatalog()
        {
            return new[]
            {
                new ShopItem
                {
                    itemId =
                        CharacterShopAssets.JammoItemId,

                    name =
                        "Jammo",

                    description =
                        "Compact expedition unit configured for hazardous operations.",

                    assetReference =
                        "Assets/Resources/Characters/PF_JammoVisual.prefab",

                    price = 600
                }
            };
        }

        private static ShopItem FindItem(
            ShopItem[] items,
            string itemId)
        {
            if (items == null
                || string.IsNullOrWhiteSpace(itemId))
            {
                return null;
            }

            for (int i = 0;
                 i < items.Length;
                 i++)
            {
                var item =
                    items[i];

                if (item != null
                    && string.Equals(
                        item.itemId,
                        itemId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }

            return null;
        }

        private static RectTransform Box(
            string name,
            Transform parent)
        {
            var go =
                new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));

            go.transform.SetParent(
                parent,
                false);

            var rect =
                go.transform
                    as RectTransform;

            var image =
                go.GetComponent<Image>();

            image.color =
                PanelBackground;

            var outline =
                go.AddComponent<Outline>();

            outline.effectColor =
                Border;

            outline.effectDistance =
                new Vector2(
                    1f,
                    -1f);

            return rect;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            string caption)
        {
            var rect =
                Box(
                    name,
                    parent);

            var button =
                rect.gameObject
                    .AddComponent<Button>();

            button.targetGraphic =
                rect.GetComponent<Image>();

            var colors =
                button.colors;

            colors.normalColor =
                CardBackground;

            colors.highlightedColor =
                new Color32(
                    38, 43, 43, 255);

            colors.pressedColor =
                new Color32(
                    64, 34, 31, 255);

            colors.disabledColor =
                new Color32(
                    12, 15, 15, 170);

            button.colors =
                colors;

            var label =
                Label(
                    "Caption",
                    rect,
                    caption,
                    12);

            Stretch(
                label.rectTransform,
                new Vector2(10f, 0f),
                new Vector2(-10f, 0f));

            label.alignment =
                TextAnchor.MiddleCenter;

            label.fontStyle =
                FontStyle.Bold;

            return button;
        }

        private static Text Label(
            string name,
            Transform parent,
            string value,
            int fontSize)
        {
            var go =
                new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Text));

            go.transform.SetParent(
                parent,
                false);

            var text =
                go.GetComponent<Text>();

            text.font =
                Resources.GetBuiltinResource<Font>(
                    "LegacyRuntime.ttf");

            text.fontSize =
                fontSize;

            text.text =
                value;

            text.color =
                TextPrimary;

            text.alignment =
                TextAnchor.MiddleLeft;

            text.raycastTarget =
                false;

            return text;
        }

        private static void ClearChildren(
            Transform parent)
        {
            for (int i =
                     parent.childCount - 1;
                 i >= 0;
                 i--)
            {
                var child =
                    parent.GetChild(i)
                        .gameObject;

                child.SetActive(false);

                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
        }

        private static void Stretch(
            RectTransform rect,
            Vector2 minimum,
            Vector2 maximum)
        {
            rect.anchorMin =
                Vector2.zero;

            rect.anchorMax =
                Vector2.one;

            rect.offsetMin =
                minimum;

            rect.offsetMax =
                maximum;
        }

        private static void PlaceCentered(
            RectTransform rect,
            float width,
            float height)
        {
            rect.anchorMin =
                new Vector2(0.5f, 0.5f);

            rect.anchorMax =
                new Vector2(0.5f, 0.5f);

            rect.pivot =
                new Vector2(0.5f, 0.5f);

            rect.anchoredPosition =
                Vector2.zero;

            rect.sizeDelta =
                new Vector2(
                    width,
                    height);
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
                new Vector2(x, -y);

            rect.sizeDelta =
                new Vector2(
                    width,
                    height);
        }
    }
}
