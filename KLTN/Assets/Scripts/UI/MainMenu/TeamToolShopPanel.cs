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

            if (!_built)
                return;

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

            if (!_built)
                return;

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
            var livePreview =
                GetComponent<StoreLivePreviewRenderer>();

            if (livePreview != null)
            {
                livePreview.Clear();
            }

            gameObject.SetActive(false);
        }
        private void Build()
        {
            if (_built)
                return;

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

            // EDITABLE PREFAB PATH:
            // If the Store hierarchy already exists in the prefab/scene,
            // bind it instead of rebuilding layout from hard-coded values.
            if (TryBindExistingView())
            {
                _built = true;

                WireExistingView();

                EnsureCharacterStore();

                ShowCategory(
                    StoreCategory.TeamTool);

                return;
            }

            // STORE_V2_PREFAB_REQUIRED
            // Store V2 visual hierarchy is prefab-authored. Do not rebuild
            // header/navigation/content/footer from runtime C#.
            Debug.LogError(
                "[StoreV2] Prefab-authored Store hierarchy is incomplete. " +
                "Fix StoreV2Popup.prefab; runtime fallback UI has been removed.",
                this);
        }

        private bool TryBindExistingView()
        {
            _credits =
                transform.Find(
                    "Window/CreditsBox/Amount")
                    ?.GetComponent<Text>();

            _sectionTitle =
                transform.Find(
                    "Window/Content/SectionTitle")
                    ?.GetComponent<Text>();

            _sectionSubtitle =
                transform.Find(
                    "Window/Content/SectionSubtitle")
                    ?.GetComponent<Text>();

            _status =
                transform.Find(
                    "Window/Content/Status")
                    ?.GetComponent<Text>();

            _characterButton =
                transform.Find(
                    "Window/Categories/CharacterButton")
                    ?.GetComponent<Button>();

            _teamToolButton =
                transform.Find(
                    "Window/Categories/TeamToolButton")
                    ?.GetComponent<Button>();

            _petButton =
                transform.Find(
                    "Window/Categories/PetButton")
                    ?.GetComponent<Button>();

            _teamTools =
                transform.Find(
                    "Window/Content/TeamTools")
                as RectTransform;

            _selectedPreview =
                transform.Find(
                    "Window/Content/TeamTools/SelectedPreviewBox/SelectedPreview")
                    ?.GetComponent<RawImage>();

            _grid =
                transform.Find(
                    "Window/Content/TeamTools/ItemGrid")
                as RectTransform;

            _selectedName =
                transform.Find(
                    "Window/Content/TeamTools/SelectedName")
                    ?.GetComponent<Text>();

            _selectedDescription =
                transform.Find(
                    "Window/Content/TeamTools/SelectedDescription")
                    ?.GetComponent<Text>();

            _selectedPrice =
                transform.Find(
                    "Window/Content/TeamTools/SelectedPrice")
                    ?.GetComponent<Text>();

            _buyButton =
                transform.Find(
                    "Window/Content/TeamTools/BuyButton")
                    ?.GetComponent<Button>();

            _closeButton =
                transform.Find(
                    "Window/CloseButton")
                    ?.GetComponent<Button>();

            return
                _credits != null &&
                _sectionTitle != null &&
                _sectionSubtitle != null &&
                _status != null &&
                _characterButton != null &&
                _teamToolButton != null &&
                _petButton != null &&
                _teamTools != null &&
                _selectedPreview != null &&
                _grid != null &&
                _selectedName != null &&
                _selectedDescription != null &&
                _selectedPrice != null &&
                _buyButton != null &&
                _closeButton != null;
        }

        private void WireExistingView()
        {
            // StoreV2CategoryRouter is the only category-click authority.
            // This controller owns only its Buy/Close actions here.
            _buyButton.onClick.RemoveAllListeners();
            _closeButton.onClick.RemoveAllListeners();

            _buyButton.onClick.AddListener(
                PurchaseSelected);

            _closeButton.onClick.AddListener(
                Close);
        }

        private CharacterShopController EnsureCharacterStore()
        {
            var characterStore =
                GetComponent<CharacterShopController>();

            if (characterStore == null)
            {
                characterStore =
                    gameObject.AddComponent<CharacterShopController>();
            }

            characterStore.EnsureBuilt();

            return characterStore;
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
            if (tools)
            {
                RefreshSelectedTeamToolPreview();
            }

            switch (category)
            {
                case StoreCategory.Character:
                    _sectionTitle.text =
                        "CHARACTER";

                    _sectionSubtitle.text =
                        "OPERATOR UNLOCKS // PERMANENT";

                    _status.text =
                        "SELECT A CHARACTER.";
                    break;
                case StoreCategory.Pet:
                    _sectionTitle.text =
                        "PET";

                    _sectionSubtitle.text =
                        "COMPANIONS // PERMANENT UNLOCKS";

                    _status.text =
                        "SELECT A PET.";
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

            var characterStore =
                GetComponent<CharacterShopController>();

            if (characterStore != null)
            {
                characterStore.SetVisible(
                    category == StoreCategory.Character,
                    _previewMode);
            }
            var petStore =
                GetComponent<PetShopController>();

            if (petStore != null)
            {
                petStore.SetVisible(
                    category == StoreCategory.Pet,
                    _previewMode);
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
            GameObject card =
                StoreV2PrefabCardFactory
                    .CreateCatalogCard(
                        transform,
                        _grid,
                        "Tool_"
                        + expected.ToolId,
                        expected.ThumbnailResource,
                        this);

            if (card == null)
                return;

            Button button =
                card.GetComponent<Button>();
            if(button!=null)EchoProtocol.Audio.GameAudioRuntime.RegisterButton(button);

            if (item == null)
            {
                button.interactable =
                    false;

                return;
            }

            var selected =
                item;

            button.onClick.AddListener(
                () =>
                    SelectItem(
                        selected));
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

                var existingLivePreview =
                    GetComponent<StoreLivePreviewRenderer>();

                if (existingLivePreview != null)
                {
                    existingLivePreview.Clear(
                        _selectedPreview);
                }

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
                var fallbackTexture =
                    Resources.Load<Texture2D>(
                        expected.ThumbnailResource);

                // Static image is kept as fallback while
                // the live model is being resolved.
                _selectedPreview.texture =
                    fallbackTexture;

                _selectedPreview.color =
                    fallbackTexture != null
                        ? Color.white
                        : Color.clear;

                var livePreview =
                    ResolveLivePreviewRenderer();

                bool liveShown =
                    livePreview.Show(
                        expected.LivePreviewResource,
                        _selectedPreview);

                if (!liveShown)
                {
                    _selectedPreview.texture =
                        fallbackTexture;

                    _selectedPreview.color =
                        fallbackTexture != null
                            ? Color.white
                            : Color.clear;
                }
            }
            else
            {
                var existingLivePreview =
                    GetComponent<StoreLivePreviewRenderer>();

                if (existingLivePreview != null)
                {
                    existingLivePreview.Clear(
                        _selectedPreview);
                }

                _selectedPreview.texture =
                    null;

                _selectedPreview.color =
                    Color.clear;
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

        private void RefreshSelectedTeamToolPreview()
        {
            if (_selected == null
                || _selectedPreview == null)
            {
                return;
            }

            if (!TeamToolShopAssets.TryGetByItemId(
                    _selected.itemId,
                    out var expected))
            {
                return;
            }

            var fallbackTexture =
                Resources.Load<Texture2D>(
                    expected.ThumbnailResource);

            _selectedPreview.texture =
                fallbackTexture;

            _selectedPreview.color =
                fallbackTexture != null
                    ? Color.white
                    : Color.clear;

            var livePreview =
                ResolveLivePreviewRenderer();

            bool liveShown =
                livePreview.Show(
                    expected.LivePreviewResource,
                    _selectedPreview);

            if (!liveShown)
            {
                _selectedPreview.texture =
                    fallbackTexture;

                _selectedPreview.color =
                    fallbackTexture != null
                        ? Color.white
                        : Color.clear;
            }
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

            // STORE_V2_PREFAB_CATEGORY_STATE
            // Color/size/position are authored in the prefab. Runtime only
            // toggles the already-authored Accent state.
            Transform accent =
                button.transform.Find("Accent");

            if (accent != null)
            {
                accent.gameObject.SetActive(
                    selected);
            }
        }



        /// <summary>
        /// Removes only decorative chrome from Store preview regions.
        /// Keeps RawImage/model rendering and thumbnail cards untouched.
        /// </summary>



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


    }
}
