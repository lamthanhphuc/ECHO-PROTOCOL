using System;
using EchoProtocol.Api;
using EchoProtocol.Auth;
using EchoProtocol.Profile;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.MainMenu
{
    [DisallowMultipleComponent]
    public sealed class TeamToolShopPanel : MonoBehaviour
    {
        private enum StoreCategory
        {
            Character,
            TeamTool,
            Pet
        }

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

        private static readonly Color WindowBackground =
            new Color32(10, 13, 14, 250);

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

        private static readonly Color Accent =
            new Color32(132, 37, 33, 255);

        private static readonly Color AccentSoft =
            new Color32(50, 24, 23, 235);

        private static readonly Color Credits =
            new Color32(193, 178, 130, 255);

        private RectTransform _window;
        private RectTransform _teamTools;
        private RectTransform _grid;

        private Text _credits;
        private Text _sectionTitle;
        private Text _sectionSubtitle;
        private Text _selectedName;
        private Text _selectedDescription;
        private Text _selectedPrice;
        private Text _status;

        private RawImage _selectedPreview;

        private Button _characterButton;
        private Button _teamToolButton;
        private Button _petButton;
        private Button _buyButton;
        private Button _closeButton;

        private MainMenuProfileController _profile;

        private ShopItem[] _catalog =
            Array.Empty<ShopItem>();

        private ShopItem _selected;

        private bool _built;
        private bool _purchasing;
        private bool _previewMode;

        public void Open()
        {
            Open(null);
        }

        public void Open(
            MainMenuProfileController profile)
        {
            _profile = profile;
            _previewMode = false;

            gameObject.SetActive(true);

            if (!_built)
                Build();

            ShowCategory(
                StoreCategory.TeamTool);

            RefreshCredits();
            LoadCatalog();
        }

#if UNITY_EDITOR
        public void PreviewLayout()
        {
            _previewMode = true;

            gameObject.SetActive(true);

            if (!_built)
                Build();

            ShowCategory(
                StoreCategory.TeamTool);

            RefreshCredits();

            _catalog =
                BuildPreviewCatalog();

            ShowItems(_catalog);

            _status.text =
                "SELECT A TEAM TOOL.";
        }
#endif

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void Build()
        {
            _window =
                transform.Find("Window")
                as RectTransform;

            if (_window == null)
            {
                Debug.LogError(
                    "[StoreV2] Window missing.",
                    this);

                return;
            }

            _built = true;

            BuildHeader();
            BuildCategoryNavigation();
            BuildContent();
            BuildFooter();

            ShowCategory(
                StoreCategory.TeamTool);
        }

        private void BuildHeader()
        {
            var title =
                Label(
                    "StoreTitle",
                    _window,
                    "STORE",
                    30);

            PlaceTopLeft(
                title.rectTransform,
                38f,
                24f,
                500f,
                42f);

            title.fontStyle =
                FontStyle.Bold;

            title.color =
                TextPrimary;

            var subtitle =
                Label(
                    "StoreSubtitle",
                    _window,
                    "ECHO PROTOCOL // SUPPLY TERMINAL",
                    10);

            PlaceTopLeft(
                subtitle.rectTransform,
                40f,
                60f,
                480f,
                22f);

            subtitle.color =
                TextSecondary;

            var creditsBox =
                Box(
                    "CreditsBox",
                    _window);

            PlaceTopLeft(
                creditsBox,
                900f,
                20f,
                188f,
                56f);

            var amount =
                Label(
                    "Amount",
                    creditsBox,
                    "0",
                    18);

            PlaceTopLeft(
                amount.rectTransform,
                14f,
                6f,
                158f,
                24f);

            amount.fontStyle =
                FontStyle.Bold;

            amount.color =
                Credits;

            _credits =
                amount;

            var caption =
                Label(
                    "Caption",
                    creditsBox,
                    "CREDITS",
                    10);

            PlaceTopLeft(
                caption.rectTransform,
                14f,
                30f,
                158f,
                18f);

            caption.color =
                TextSecondary;

            var line =
                Box(
                    "HeaderDivider",
                    _window);

            PlaceTopLeft(
                line,
                38f,
                91f,
                1048f,
                1f);

            line.GetComponent<Image>().color =
                Border;
        }

        private void BuildCategoryNavigation()
        {
            var nav =
                Box(
                    "Categories",
                    _window);

            PlaceTopLeft(
                nav,
                38f,
                116f,
                184f,
                474f);

            _characterButton =
                CategoryButton(
                    nav,
                    "CharacterButton",
                    "CHARACTER",
                    18f);

            _teamToolButton =
                CategoryButton(
                    nav,
                    "TeamToolButton",
                    "TEAM TOOL",
                    84f);

            _petButton =
                CategoryButton(
                    nav,
                    "PetButton",
                    "PET",
                    150f);

            _characterButton.onClick.AddListener(
                () => ShowCategory(
                    StoreCategory.Character));

            _teamToolButton.onClick.AddListener(
                () => ShowCategory(
                    StoreCategory.TeamTool));

            _petButton.onClick.AddListener(
                () => ShowCategory(
                    StoreCategory.Pet));

            var hint =
                Label(
                    "NavigationHint",
                    nav,
                    "PURCHASED TEAM TOOLS\nCAN BE SELECTED IN LOBBY.",
                    10);

            PlaceTopLeft(
                hint.rectTransform,
                14f,
                392f,
                156f,
                50f);

            hint.color =
                TextSecondary;

            hint.horizontalOverflow =
                HorizontalWrapMode.Wrap;

            hint.verticalOverflow =
                VerticalWrapMode.Overflow;
        }

        private void BuildContent()
        {
            var content =
                Box(
                    "Content",
                    _window);

            PlaceTopLeft(
                content,
                242f,
                116f,
                846f,
                474f);

            _sectionTitle =
                Label(
                    "SectionTitle",
                    content,
                    "TEAM TOOL",
                    20);

            PlaceTopLeft(
                _sectionTitle.rectTransform,
                20f,
                14f,
                500f,
                30f);

            _sectionTitle.fontStyle =
                FontStyle.Bold;

            _sectionSubtitle =
                Label(
                    "SectionSubtitle",
                    content,
                    "FIELD EQUIPMENT // PERMANENT UNLOCK",
                    9);

            PlaceTopLeft(
                _sectionSubtitle.rectTransform,
                20f,
                42f,
                500f,
                18f);

            _sectionSubtitle.color =
                TextSecondary;

            _teamTools =
                Box(
                    "TeamTools",
                    content);

            Stretch(
                _teamTools,
                new Vector2(18f, 18f),
                new Vector2(-18f, -70f));

            _teamTools.GetComponent<Image>().color =
                Color.clear;

            BuildTeamToolContent();

            _status =
                Label(
                    "Status",
                    content,
                    string.Empty,
                    10);

            PlaceTopLeft(
                _status.rectTransform,
                20f,
                434f,
                780f,
                22f);

            _status.color =
                TextSecondary;
        }

        private void BuildTeamToolContent()
        {
            var previewBox =
                Box(
                    "SelectedPreviewBox",
                    _teamTools);

            PlaceTopLeft(
                previewBox,
                0f,
                60f,
                392f,
                228f);

            var previewObject =
                new GameObject(
                    "SelectedPreview",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(RawImage));

            previewObject.transform.SetParent(
                previewBox,
                false);

            var previewRect =
                previewObject.transform
                as RectTransform;

            PlaceTopLeft(
                previewRect,
                12f,
                12f,
                368f,
                204f);

            _selectedPreview =
                previewObject.GetComponent<RawImage>();

            _selectedPreview.raycastTarget =
                false;

            _selectedPreview.color =
                Color.clear;

            _grid =
                Box(
                    "ItemGrid",
                    _teamTools);

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
                    "SelectedName",
                    _teamTools,
                    "SELECT A TEAM TOOL",
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
                    "SelectedDescription",
                    _teamTools,
                    string.Empty,
                    11);

            PlaceTopLeft(
                _selectedDescription.rectTransform,
                0f,
                334f,
                530f,
                42f);

            _selectedDescription.color =
                new Color32(150, 158, 155, 255);

            _selectedDescription.horizontalOverflow =
                HorizontalWrapMode.Wrap;

            _selectedDescription.verticalOverflow =
                VerticalWrapMode.Overflow;

            _selectedPrice =
                Label(
                    "SelectedPrice",
                    _teamTools,
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
                    "BuyButton",
                    _teamTools,
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

            SetBuyState();
        }

        private void BuildFooter()
        {
            _closeButton =
                CreateButton(
                    "CloseButton",
                    _window,
                    "BACK");

            PlaceTopLeft(
                _closeButton.transform
                    as RectTransform,
                900f,
                610f,
                188f,
                40f);

            _closeButton.onClick.AddListener(
                Close);
        }

        private void ShowCategory(
            StoreCategory category)
        {
            if (!_built)
                return;

            bool tools =
                category == StoreCategory.TeamTool;

            if (_teamTools != null)
                _teamTools.gameObject.SetActive(
                    tools);

            switch (category)
            {
                case StoreCategory.Character:
                    _sectionTitle.text =
                        "CHARACTER";

                    _sectionSubtitle.text =
                        "UNDER DEVELOPMENT";

                    _status.text =
                        "CHARACTER STORE IS NOT AVAILABLE IN THIS BUILD.";
                    break;

                case StoreCategory.Pet:
                    _sectionTitle.text =
                        "PET";

                    _sectionSubtitle.text =
                        "UNDER DEVELOPMENT";

                    _status.text =
                        "PET STORE IS NOT AVAILABLE IN THIS BUILD.";
                    break;

                default:
                    _sectionTitle.text =
                        "TEAM TOOL";

                    _sectionSubtitle.text =
                        "FIELD EQUIPMENT // PERMANENT UNLOCK";

                    _status.text =
                        _previewMode
                            ? "SELECT A TEAM TOOL."
                            : TeamToolOwnershipSession.IsLoaded
                                ? "SELECT A TEAM TOOL."
                                : "LOADING INVENTORY...";
                    break;
            }

            StyleCategoryButton(
                _characterButton,
                category == StoreCategory.Character);

            StyleCategoryButton(
                _teamToolButton,
                category == StoreCategory.TeamTool);

            StyleCategoryButton(
                _petButton,
                category == StoreCategory.Pet);
        }

        private void LoadCatalog()
        {
            if (_previewMode)
            {
                _catalog =
                    BuildPreviewCatalog();

                ShowItems(_catalog);
                _status.text =
                    "SELECT A TEAM TOOL.";
                return;
            }

            var runtime =
                AuthRuntime.EnsureExists();

            if (runtime == null
                || runtime.Client == null)
            {
                _status.text =
                    "STORE SERVICE UNAVAILABLE.";

                ShowItems(null);
                return;
            }

            _status.text =
                "LOADING TEAM TOOLS...";

            runtime.Client.GetJson<CatalogResponse>(
                ApiEndpoints.ShopTeamTools,
                true,
                result =>
                {
                    if (this == null)
                        return;

                    if (!result.IsSuccess
                        || result.Data == null
                        || !result.Data.success
                        || result.Data.data == null)
                    {
                        _catalog =
                            Array.Empty<ShopItem>();

                        ShowItems(null);

                        _status.text =
                            "TEAM TOOL CATALOG UNAVAILABLE.";

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

                            ShowItems(_catalog);

                            _status.text =
                                success
                                    ? "SELECT A TEAM TOOL."
                                    : "INVENTORY STATUS UNAVAILABLE.";
                        });
                });
        }

        private void ShowItems(
            ShopItem[] items)
        {
            if (_grid == null)
                return;

            ClearChildren(_grid);

            ShopItem first =
                null;

            for (int i = 0;
                 i < TeamToolShopAssets.Items.Length;
                 i++)
            {
                var expected =
                    TeamToolShopAssets.Items[i];

                ShopItem item =
                    FindItem(
                        items,
                        expected.ItemId);

                if (item != null
                    && first == null)
                {
                    first = item;
                }

                CreateItemCard(
                    expected,
                    item);
            }

            if (first != null)
                SelectItem(first);
            else
                SelectItem(null);
        }

        private void CreateItemCard(
            TeamToolShopAssets.Item expected,
            ShopItem item)
        {
            var card =
                Box(
                    "Tool_" + expected.ToolId,
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
                14f,
                78f,
                78f);

            var thumbnail =
                imageObject.GetComponent<RawImage>();

            thumbnail.texture =
                Resources.Load<Texture2D>(
                    expected.ThumbnailResource);

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
                    expected.Name,
                    11);

            PlaceTopLeft(
                name.rectTransform,
                96f,
                16f,
                84f,
                36f);

            name.fontStyle =
                FontStyle.Bold;

            name.resizeTextForBestFit =
                true;

            name.resizeTextMinSize = 9;
            name.resizeTextMaxSize = 11;

            string state =
                item == null
                    ? "UNAVAILABLE"
                    : TeamToolOwnershipSession
                        .OwnsItem(item.itemId)
                            ? "OWNED"
                            : $"{item.price:N0} CR";

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
                84f,
                18f);

            stateText.color =
                item != null
                && TeamToolOwnershipSession
                    .OwnsItem(item.itemId)
                    ? new Color32(122, 170, 127, 255)
                    : Credits;

            if (item == null)
            {
                button.interactable =
                    false;
            }
            else
            {
                var selected =
                    item;

                button.onClick.AddListener(
                    () => SelectItem(
                        selected));
            }
        }

        private void SelectItem(
            ShopItem item)
        {
            _selected =
                item;

            if (item == null)
            {
                _selectedName.text =
                    "NO ITEM AVAILABLE";

                _selectedDescription.text =
                    string.Empty;

                _selectedPrice.text =
                    "-- CR";

                _selectedPreview.texture =
                    null;

                _selectedPreview.color =
                    Color.clear;

                SetBuyState();
                return;
            }

            _selectedName.text =
                string.IsNullOrWhiteSpace(
                    item.name)
                    ? "TEAM TOOL"
                    : item.name.ToUpperInvariant();

            _selectedDescription.text =
                item.description
                ?? string.Empty;

            _selectedPrice.text =
                $"{item.price:N0} CR";

            if (TeamToolShopAssets.TryGetByItemId(
                    item.itemId,
                    out var expected))
            {
                _selectedPreview.texture =
                    Resources.Load<Texture2D>(
                        expected.ThumbnailResource);
            }
            else
            {
                _selectedPreview.texture =
                    null;
            }

            _selectedPreview.color =
                _selectedPreview.texture != null
                    ? Color.white
                    : Color.clear;

            SetBuyState();
        }

        private void SetBuyState()
        {
            if (_buyButton == null)
                return;

            var caption =
                _buyButton
                    .GetComponentInChildren<Text>();

            if (_selected == null)
            {
                _buyButton.interactable =
                    false;

                if (caption != null)
                    caption.text =
                        "UNAVAILABLE";

                return;
            }

            bool owned =
                TeamToolOwnershipSession
                    .OwnsItem(
                        _selected.itemId);

            if (_previewMode)
            {
                _buyButton.interactable =
                    true;

                if (caption != null)
                    caption.text = "BUY";

                return;
            }

            _buyButton.interactable =
                !owned
                && !_purchasing;

            if (caption != null)
            {
                caption.text =
                    owned
                        ? "OWNED"
                        : _purchasing
                            ? "PURCHASING..."
                            : "BUY";
            }
        }

        private void PurchaseSelected()
        {
            if (_previewMode)
            {
                _status.text =
                    "EDITOR PREVIEW MODE.";
                return;
            }

            if (_selected == null
                || _purchasing
                || TeamToolOwnershipSession
                    .OwnsItem(_selected.itemId))
            {
                return;
            }

            var runtime =
                AuthRuntime.EnsureExists();

            if (runtime == null
                || runtime.Client == null)
            {
                _status.text =
                    "STORE SERVICE UNAVAILABLE.";

                return;
            }

            ShopItem purchasingItem =
                _selected;

            _purchasing = true;

            _status.text =
                "PURCHASING " +
                purchasingItem.name
                    .ToUpperInvariant() +
                "...";

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
                        _status.text =
                            string.IsNullOrWhiteSpace(
                                result.Message)
                                ? "PURCHASE FAILED."
                                : result.Message
                                    .ToUpperInvariant();

                        SetBuyState();
                        return;
                    }

                    TeamToolOwnershipSession.MarkOwned(
                        purchasingItem.itemId);

                    _status.text =
                        purchasingItem.name
                            .ToUpperInvariant() +
                        " PURCHASED.";

                    ShowItems(
                        _catalog);

                    runtime.PlayerProfileService
                        .GetCurrentProfile(
                            _ =>
                            {
                                if (this == null)
                                    return;

                                RefreshCredits();

                                if (_profile != null)
                                    _profile.UpdateCreditsUI();
                            });
                });
        }

        private void RefreshCredits()
        {
            if (_credits == null)
                return;

            int balance =
                _previewMode
                    ? 1250
                    : PlayerProfileSession.HasProfile
                        ? PlayerProfileSession
                            .WalletBalance
                        : AuthSession.WalletBalance;

            _credits.text =
                $"{balance:N0}";
        }

        private static ShopItem[] BuildPreviewCatalog()
        {
            var result =
                new ShopItem[
                    TeamToolShopAssets.Items.Length];

            for (int i = 0;
                 i < TeamToolShopAssets.Items.Length;
                 i++)
            {
                var item =
                    TeamToolShopAssets.Items[i];

                result[i] =
                    new ShopItem
                    {
                        itemId =
                            item.ItemId,
                        name =
                            item.Name,
                        description =
                            PreviewDescriptionFor(
                                item.ItemId,
                                item.Name),
                        assetReference =
                            item.ThumbnailResource,
                        price =
                            PreviewPriceFor(
                                item.ItemId)
                    };
            }

            return result;
        }

        private static int PreviewPriceFor(
            string itemId)
        {
            if (string.Equals(
                    itemId,
                    "12000000-0000-0000-0000-000000000001",
                    StringComparison.OrdinalIgnoreCase))
            {
                return 300;
            }

            if (string.Equals(
                    itemId,
                    "12000000-0000-0000-0000-000000000002",
                    StringComparison.OrdinalIgnoreCase))
            {
                return 250;
            }

            if (string.Equals(
                    itemId,
                    "12000000-0000-0000-0000-000000000004",
                    StringComparison.OrdinalIgnoreCase))
            {
                return 275;
            }

            if (string.Equals(
                    itemId,
                    "12000000-0000-0000-0000-000000000006",
                    StringComparison.OrdinalIgnoreCase))
            {
                return 350;
            }

            return 0;
        }

        private static string PreviewDescriptionFor(
            string itemId,
            string fallbackName)
        {
            if (string.Equals(
                    itemId,
                    "12000000-0000-0000-0000-000000000001",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Scan Energy Cores and moving threats.";
            }

            if (string.Equals(
                    itemId,
                    "12000000-0000-0000-0000-000000000002",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Place a beacon that draws the Stalker.";
            }

            if (string.Equals(
                    itemId,
                    "12000000-0000-0000-0000-000000000004",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Temporarily jam a compatible door.";
            }

            if (string.Equals(
                    itemId,
                    "12000000-0000-0000-0000-000000000006",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Stabilize volatile energy-core handling.";
            }

            return fallbackName;
        }

        private static ShopItem FindItem(
            ShopItem[] items,
            string itemId)
        {
            if (items == null)
                return null;

            for (int i = 0;
                 i < items.Length;
                 i++)
            {
                ShopItem item =
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

        private static void StyleCategoryButton(
            Button button,
            bool selected)
        {
            if (button == null)
                return;

            var image =
                button.targetGraphic as Image;

            if (image != null)
            {
                image.color =
                    selected
                        ? AccentSoft
                        : CardBackground;
            }

            var text =
                button.GetComponentInChildren<Text>();

            if (text != null)
            {
                text.color =
                    selected
                        ? TextPrimary
                        : TextSecondary;
            }

            var accent =
                button.transform.Find("Accent")
                as RectTransform;

            if (accent == null)
            {
                var go =
                    new GameObject(
                        "Accent",
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(Image));

                go.transform.SetParent(
                    button.transform,
                    false);

                accent =
                    go.transform
                    as RectTransform;

                PlaceTopLeft(
                    accent,
                    0f,
                    0f,
                    4f,
                    52f);

                go.GetComponent<Image>().raycastTarget =
                    false;
            }

            accent.GetComponent<Image>().color =
                selected
                    ? Accent
                    : Color.clear;
        }

        private static Button CategoryButton(
            Transform parent,
            string name,
            string caption,
            float y)
        {
            var button =
                CreateButton(
                    name,
                    parent,
                    caption);

            PlaceTopLeft(
                button.transform
                    as RectTransform,
                10f,
                y,
                164f,
                52f);

            return button;
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

            colors.selectedColor =
                colors.highlightedColor;

            colors.disabledColor =
                new Color32(
                    12, 15, 15, 170);

            colors.fadeDuration =
                0.08f;

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
                new Vector2(1f, -1f);

            return rect;
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
                GameObject child =
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
