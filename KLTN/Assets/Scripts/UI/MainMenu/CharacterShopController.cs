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








        private enum PurchaseTarget
        {
            Character,
            Outfit
        }

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

        private ShopItem[] _outfitCatalog =
            Array.Empty<ShopItem>();

        private CharacterShopAssets.Item _selectedDefinition;
        private ShopItem _selectedItem;
        private ShopItem _selectedOutfitItem;

        private int _selectedCharacterId = -1;
        private int _selectedOutfitId;

        private PurchaseTarget _purchaseTarget =
            PurchaseTarget.Character;

        // STORE_V2_CHARACTER_OUTFIT_SELECTION_API
        // The Character controller owns Store outfit selection state.
        // The visible outfit strip only sends a requested outfit id.
        public bool IsBuyingSkin => _purchaseTarget == PurchaseTarget.Outfit;

        public int SelectedCharacterId =>
            _selectedCharacterId;

        public int SelectedOutfitId =>
            _selectedOutfitId;

        public int SelectedOutfitCount
        {
            get
            {
                if (!CharacterShopAssets.TryGetByCharacterId(
                        _selectedCharacterId,
                        out var definition))
                {
                    return 0;
                }

                return ResolveOutfitCount(
                    definition);
            }
        }

        public bool TrySelectOutfitFromVariant(
            int outfitId)
        {
            if (!CharacterShopAssets.TryGetByCharacterId(
                    _selectedCharacterId,
                    out var definition))
            {
                return false;
            }

            int outfitCount =
                ResolveOutfitCount(
                    definition);

            if (outfitId < 0
                || outfitId >= outfitCount)
            {
                return false;
            }

            StoreLivePreviewRenderer livePreview =
                ResolveLivePreviewRenderer();

            if (livePreview == null
                || !livePreview.ApplyCharacterOutfit(
                    definition.CharacterId,
                    outfitId))
            {
                return false;
            }

            _selectedOutfitId =
                outfitId;

            ApplyOutfitPurchaseContext(
                definition);

            SetBuyState();

            return true;
        }

        private static int ResolveOutfitCount(
            CharacterShopAssets.Item definition)
        {
            // Both current Store characters have four authored visual
            // variants/thumbnails. Keep the definition value extensible,
            // but never hide a known Store V2 outfit because an older
            // CharacterShopAssets entry still reports 1.
            if (definition.CharacterId
                    == CharacterShopAssets.AstronautCharacterId
                || definition.CharacterId
                    == CharacterShopAssets.JammoCharacterId)
            {
                return Mathf.Max(
                    4,
                    definition.OutfitCount);
            }

            return Mathf.Max(
                1,
                definition.OutfitCount);
        }

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

            // Prefer the prefab-authored hierarchy.
            if (TryBindExistingCharacterView())
            {
                WireExistingCharacterView();

                _root.gameObject.SetActive(false);

                _built = true;

                return;
            }

            // STORE_V2_PREFAB_REQUIRED
            // Store V2 no longer creates visual hierarchy at runtime.
            // Missing nodes are an authoring error and must be fixed in
            // StoreV2Popup.prefab / Inspector.
            Debug.LogError(
                "[CharacterStore] Prefab-authored CharacterContent is incomplete. " +
                "Fix StoreV2Popup.prefab; runtime fallback UI has been removed.",
                this);
        }

        private bool TryBindExistingCharacterView()
        {
            _root =
                transform.Find(
                    "Window/Content/CharacterContent")
                as RectTransform;

            _preview =
                transform.Find(
                    "Window/Content/CharacterContent/CharacterPreviewBox/CharacterPreview")
                    ?.GetComponent<RawImage>();

            _grid =
                transform.Find(
                    "Window/Content/CharacterContent/CharacterGrid")
                as RectTransform;

            _selectedName =
                transform.Find(
                    "Window/Content/CharacterContent/SelectedCharacterName")
                    ?.GetComponent<Text>();

            _selectedDescription =
                transform.Find(
                    "Window/Content/CharacterContent/SelectedCharacterDescription")
                    ?.GetComponent<Text>();

            _selectedPrice =
                transform.Find(
                    "Window/Content/CharacterContent/SelectedCharacterPrice")
                    ?.GetComponent<Text>();

            _buyButton =
                transform.Find(
                    "Window/Content/CharacterContent/CharacterBuyButton")
                    ?.GetComponent<Button>();

            return
                _root != null &&
                _preview != null &&
                _grid != null &&
                _selectedName != null &&
                _selectedDescription != null &&
                _selectedPrice != null &&
                _buyButton != null;
        }

        private void WireExistingCharacterView()
        {
            SetPassiveGraphic(
                _root);

            SetPassiveGraphic(
                _grid);

            SetPassiveGraphic(
                _root != null
                    ? _root.Find("CharacterPreviewBox")
                    : null);

            if (_preview != null)
            {
                _preview.raycastTarget =
                    false;
            }

            _buyButton.onClick.RemoveAllListeners();

            _buyButton.onClick.AddListener(
                PurchaseSelected);
        }

        private static void SetPassiveGraphic(
            Transform target)
        {
            if (target == null)
                return;

            Graphic graphic =
                target.GetComponent<Graphic>();

            if (graphic != null)
            {
                graphic.raycastTarget =
                    false;
            }
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

                    runtime.Client.GetJson<CatalogResponse>(
                        ApiEndpoints.ShopCharacterOutfits,
                        true,
                        outfitResult =>
                        {
                            if (this == null)
                                return;

                            bool outfitCatalogAvailable =
                                outfitResult.IsSuccess
                                && outfitResult.Data != null
                                && outfitResult.Data.success
                                && outfitResult.Data.data != null;

                            _outfitCatalog =
                                outfitCatalogAvailable
                                    ? outfitResult.Data.data.items
                                        ?? Array.Empty<ShopItem>()
                                    : Array.Empty<ShopItem>();

                            TeamToolOwnershipSession.Refresh(
                                success =>
                                {
                                    if (this == null)
                                        return;

                                    ShowCharacters(
                                        _selectedCharacterId);

                                    if (!outfitCatalogAvailable)
                                    {
                                        SetStatus(
                                            "CHARACTER OUTFIT CATALOG UNAVAILABLE.");
                                    }
                                    else
                                    {
                                        SetStatus(
                                            success
                                                ? "SELECT A CHARACTER."
                                                : "INVENTORY STATUS UNAVAILABLE.");
                                    }
                                });
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
            string thumbnailResource =
                definition.CharacterId
                    == CharacterShopAssets.JammoCharacterId
                    ? "Shop/LobbyVariants/Characters/character-1-outfit-0"
                    : "Shop/LobbyVariants/Characters/character-0-outfit-0";

            GameObject card =
                StoreV2PrefabCardFactory
                    .CreateCatalogCard(
                        transform,
                        _grid,
                        "Character_"
                        + definition.CharacterId,
                        thumbnailResource,
                        this);

            if (card == null)
                return;

            Button button =
                card.GetComponent<Button>();

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

            _selectedOutfitId =
                0;

            _selectedItem =
                item;

            _selectedOutfitItem =
                null;

            _purchaseTarget =
                PurchaseTarget.Character;

            StoreReferencePresentation.SetName(_selectedName, definition.Name);

            _selectedDescription.text =
                !string.IsNullOrWhiteSpace(
                    item?.description)
                    ? item.description
                    : definition.Description;

            if (definition.DefaultOwned)
            {
                _selectedPrice.text =
                    string.Empty;
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

            if(StoreReferencePresentation.IsAwaitingSelection(transform)) {
                _preview.texture=null;_preview.color=Color.clear;SetBuyState();return;
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

                if (liveShown)
                {
                    // STORE_V2_CHARACTER_OUTFIT_PREVIEW
                    // Always reapply outfit 0 when a Character card is
                    // selected so a previously selected color cannot leak
                    // across repeated Character selections.
                    livePreview.ApplyCharacterOutfit(
                        definition.CharacterId,
                        _selectedOutfitId);
                }

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

        private void ApplyOutfitPurchaseContext(
            CharacterShopAssets.Item character)
        {
            _purchaseTarget =
                PurchaseTarget.Outfit;

            if (!CharacterOutfitShopAssets.TryGet(
                    character.CharacterId,
                    _selectedOutfitId,
                    out var outfit))
            {
                _selectedOutfitItem =
                    null;

                StoreReferencePresentation.SetName(_selectedName, character.Name + " // OUTFIT");

                _selectedDescription.text =
                    "Character outfit unavailable.";

                _selectedPrice.text =
                    "--";

                return;
            }

            _selectedOutfitItem =
                outfit.DefaultOwned
                    ? null
                    : FindItem(
                        _outfitCatalog,
                        outfit.ItemId);

            StoreReferencePresentation.SetName(_selectedName, character.Name
                + " // "
                + outfit.Name);

            _selectedDescription.text =
                outfit.DefaultOwned
                    ? "Default outfit included with this character."
                    : outfit.Name
                        + " cosmetic outfit for "
                        + character.Name
                        + ".";
        }

        private void SetBuyState()
        {
            if (_buyButton == null)
                return;

            if(StoreLobbyEquipment.ShowAction(_buyButton,_selectedPrice,false,_selectedCharacterId,(_purchaseTarget == PurchaseTarget.Outfit ? _selectedOutfitId : 0),(_purchaseTarget == PurchaseTarget.Outfit ? CharacterOutfitShopAssets.IsOwned(_selectedCharacterId,_selectedOutfitId) && CharacterShopAssets.IsOwned(_selectedCharacterId) : CharacterShopAssets.IsOwned(_selectedCharacterId))))return;

            var caption =
                _buyButton
                    .GetComponentInChildren<Text>();

            if (_selectedCharacterId < 0)
            {
                _selectedPrice.text = "--";
                _buyButton.interactable = false;

                if (caption != null)
                    caption.text = EchoProtocol.Settings.GameLanguage.Choose("Chưa khả dụng", "Unavailable");

                return;
            }

            if (_purchaseTarget == PurchaseTarget.Outfit)
            {
                if (!CharacterOutfitShopAssets.TryGet(
                        _selectedCharacterId,
                        _selectedOutfitId,
                        out var outfit))
                {
                    _selectedPrice.text = "--";
                    _buyButton.interactable = false;

                    if (caption != null)
                        caption.text = EchoProtocol.Settings.GameLanguage.Choose("Chưa khả dụng", "Unavailable");

                    return;
                }

                if (!CharacterShopAssets.IsOwned(
                        _selectedCharacterId))
                {
                    if (outfit.DefaultOwned)
                    {
                        _selectedPrice.text = string.Empty;
                    }
                    else
                    {
                        int lockedOutfitPrice =
                            _selectedOutfitItem != null
                                ? _selectedOutfitItem.price
                                : outfit.PreviewPrice;

                        _selectedPrice.text =
                            $"{lockedOutfitPrice:N0} CR";
                    }

                    _buyButton.interactable = false;

                    if (caption != null)
                        caption.text = EchoProtocol.Settings.GameLanguage.Choose("Cần sở hữu nhân vật", "Requires character");

                    return;
                }

                if (outfit.DefaultOwned)
                {
                    _selectedPrice.text = string.Empty;
                    _buyButton.interactable = false;

                    if (caption != null)
                        caption.text = EchoProtocol.Settings.GameLanguage.Choose("Mặc định", "Default");

                    return;
                }

                if (CharacterOutfitShopAssets.IsOwned(
                        _selectedCharacterId,
                        _selectedOutfitId))
                {
                    _selectedPrice.text = string.Empty;
                    _buyButton.interactable = false;

                    if (caption != null)
                        caption.text = EchoProtocol.Settings.GameLanguage.Choose("Đã sở hữu", "Owned");

                    return;
                }

                int outfitPrice =
                    _selectedOutfitItem != null
                        ? _selectedOutfitItem.price
                        : outfit.PreviewPrice;

                _selectedPrice.text =
                    $"{outfitPrice:N0} CR";

                if (_previewMode)
                {
                    _buyButton.interactable = true;

                    if (caption != null)
                        caption.text = EchoProtocol.Settings.GameLanguage.Choose("Mua", "Buy");

                    return;
                }

                if (_selectedOutfitItem == null)
                {
                    _buyButton.interactable = false;

                    if (caption != null)
                        caption.text = EchoProtocol.Settings.GameLanguage.Choose("Chưa khả dụng", "Unavailable");

                    return;
                }

                _buyButton.interactable =
                    !_purchasing;

                if (caption != null)
                {
                    caption.text =
                        _purchasing
                            ? EchoProtocol.Settings.GameLanguage.Choose("Đang mua…", "Purchasing…")
                            : EchoProtocol.Settings.GameLanguage.Choose("Mua", "Buy");
                }

                return;
            }

            bool owned =
                IsOwned(
                    _selectedDefinition);

            if (_selectedDefinition.DefaultOwned)
            {
                _selectedPrice.text = string.Empty;
                _buyButton.interactable = false;

                if (caption != null)
                    caption.text = EchoProtocol.Settings.GameLanguage.Choose("Mặc định", "Default");

                return;
            }

            if (owned)
            {
                _selectedPrice.text = string.Empty;
                _buyButton.interactable = false;

                if (caption != null)
                    caption.text = EchoProtocol.Settings.GameLanguage.Choose("Đã sở hữu", "Owned");

                return;
            }

            if (_selectedItem != null)
            {
                _selectedPrice.text =
                    $"{_selectedItem.price:N0} CR";
            }
            else
            {
                _selectedPrice.text =
                    $"{_selectedDefinition.PreviewPrice:N0} CR";
            }

            if (_previewMode)
            {
                _buyButton.interactable = true;

                if (caption != null)
                    caption.text = EchoProtocol.Settings.GameLanguage.Choose("Mua", "Buy");

                return;
            }

            if (_selectedItem == null)
            {
                _buyButton.interactable = false;

                if (caption != null)
                    caption.text = EchoProtocol.Settings.GameLanguage.Choose("Chưa khả dụng", "Unavailable");

                return;
            }

            _buyButton.interactable =
                !_purchasing;

            if (caption != null)
            {
                caption.text =
                    _purchasing
                        ? EchoProtocol.Settings.GameLanguage.Choose("Đang mua…", "Purchasing…")
                        : EchoProtocol.Settings.GameLanguage.Choose("Mua", "Buy");
            }
        }

        private void PurchaseSelected()
        {
            if(StoreLobbyEquipment.Equip(transform,false,_selectedCharacterId,(_purchaseTarget == PurchaseTarget.Outfit ? _selectedOutfitId : 0),(_purchaseTarget == PurchaseTarget.Outfit ? CharacterOutfitShopAssets.IsOwned(_selectedCharacterId,_selectedOutfitId) && CharacterShopAssets.IsOwned(_selectedCharacterId) : CharacterShopAssets.IsOwned(_selectedCharacterId))))return;

            if (_previewMode)
            {
                SetStatus(
                    "EDITOR PREVIEW MODE.");

                return;
            }

            if (_purchasing)
                return;

            ShopItem purchasingItem;
            string purchasingName;
            bool purchasingOutfit =
                _purchaseTarget == PurchaseTarget.Outfit;

            int purchasingCharacterId =
                _selectedCharacterId;

            int purchasingOutfitId =
                _selectedOutfitId;

            if (purchasingOutfit)
            {
                if (!CharacterShopAssets.IsOwned(
                        _selectedCharacterId))
                {
                    SetStatus(
                        "PURCHASE THE CHARACTER FIRST.");

                    SetBuyState();
                    return;
                }

                if (!CharacterOutfitShopAssets.TryGet(
                        _selectedCharacterId,
                        _selectedOutfitId,
                        out var outfit)
                    || outfit.DefaultOwned
                    || CharacterOutfitShopAssets.IsOwned(
                        _selectedCharacterId,
                        _selectedOutfitId))
                {
                    return;
                }

                if (_selectedOutfitItem == null)
                {
                    SetStatus(
                        "CHARACTER OUTFIT UNAVAILABLE.");

                    SetBuyState();
                    return;
                }

                purchasingItem =
                    _selectedOutfitItem;

                purchasingName =
                    _selectedDefinition.Name
                    + " "
                    + outfit.Name;
            }
            else
            {
                if (_selectedItem == null
                    || _selectedDefinition.DefaultOwned
                    || IsOwned(_selectedDefinition))
                {
                    return;
                }

                purchasingItem =
                    _selectedItem;

                purchasingName =
                    _selectedDefinition.Name;
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

            _purchasing =
                true;

            SetStatus(
                "PURCHASING "
                + purchasingName
                + "...");

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
                    StoreLobbyEquipment.Equip(transform,false,purchasingCharacterId,purchasingOutfit ? purchasingOutfitId : 0,true);

                    SetStatus(
                        purchasingName
                        + " PURCHASED.");

                    if (purchasingOutfit)
                    {
                        if (_selectedCharacterId
                                == purchasingCharacterId
                            && _selectedOutfitId
                                == purchasingOutfitId)
                        {
                            ApplyOutfitPurchaseContext(
                                _selectedDefinition);
                        }

                        SetBuyState();
                    }
                    else
                    {
                        ShowCharacters(
                            purchasingCharacterId);
                    }

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




        private static void ClearChildren(
            Transform parent)
        {
            if (StoreReferencePresentation.ClearCatalog(parent))return;

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


    }
}
