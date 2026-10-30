using System;
using System.Collections.Generic;
using EchoProtocol.Api;
using EchoProtocol.Auth;
using EchoProtocol.Pets;
using EchoProtocol.Profile;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.MainMenu
{
    [DisallowMultipleComponent]
    public sealed class PetShopController : MonoBehaviour
    {
        private void OnEnable() => EchoProtocol.Networking.LobbyPlayerState.LocalSelectionRequestCompleted += SelectionCompleted;
        private void OnDisable() => EchoProtocol.Networking.LobbyPlayerState.LocalSelectionRequestCompleted -= SelectionCompleted;
        private void SelectionCompleted(EchoProtocol.Networking.LobbySelectionResult result)
        {
            if (!result.Accepted && (result.Kind == EchoProtocol.Networking.LobbySelectionKind.Pet
                || result.Kind == EchoProtocol.Networking.LobbySelectionKind.PetSkin))
                EchoProtocol.UI.GameUIFeedback.Instance?.Toast("Pet: " + result.Error);
        }
        private enum PurchaseTarget
        {
            Pet,
            Skin
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







        private RectTransform _root;
        private RectTransform _petGrid;

        private RawImage _preview;

        private Text _selectedName;
        private Text _selectedDescription;
        private Text _selectedPrice;
        private Text _status;
        private Text _credits;

        private Button _buyButton;

        private ShopItem[] _petCatalog =
            Array.Empty<ShopItem>();

        private ShopItem[] _skinCatalog =
            Array.Empty<ShopItem>();

        private PetShopAssets.PetItem _selectedPet;
        private PetShopAssets.SkinItem _selectedSkin;

        private ShopItem _selectedPetItem;
        private ShopItem _selectedSkinItem;

        private int _selectedPetId = 1;
        private int _selectedSkinId;

        private PurchaseTarget _purchaseTarget =
            PurchaseTarget.Pet;

        private bool _built;
        private bool _loading;
        private bool _purchasing;
        private bool _previewMode;

        // STORE_V2_PET_SELECTION_API
        // Read-only state exposed for Store V2 presentation.
        // The controller remains authoritative for selection logic.
        public bool IsBuyingSkin => _purchaseTarget == PurchaseTarget.Skin;

        public int SelectedPetId =>
            _selectedPetId;

        public int SelectedSkinId =>
            _selectedSkinId;

        public bool TrySelectSkinFromVariant(
            int skinId)
        {
            if (_selectedPetId < 1
                || !PetShopAssets.TryGetSkin(
                    _selectedPetId,
                    skinId,
                    out var definition))
            {
                return false;
            }

            ShopItem item =
                definition.DefaultOwned
                    ? null
                    : FindItem(
                        _skinCatalog,
                        definition.ItemId);

            SelectSkin(
                definition,
                item);

            return _selectedSkinId
                == skinId;
        }

        public void EnsureBuilt()
        {
            if (_built)
                return;

            var content =
                transform.Find("Window/Content")
                as RectTransform;

            if (content == null)
            {
                Debug.LogError(
                    "[PetStore] Store content missing.",
                    this);

                return;
            }

            _status =
                transform.Find("Window/Content/Status")
                    ?.GetComponent<Text>();

            _credits =
                transform.Find("Window/CreditsBox/Amount")
                    ?.GetComponent<Text>();

            // STORE_V2_PREFAB_FIRST_PET
            // The prefab-authored PetContent is the source of truth.
            // Bind it before any legacy runtime UI is created.
            if (TryBindExistingPetView())
            {
                _root.gameObject.SetActive(false);

                _built = true;

                return;
            }

            // STORE_V2_PREFAB_REQUIRED
            // PetContent is authored in StoreV2Popup.prefab. Runtime code
            // binds data only and must never reconstruct visual hierarchy.
            Debug.LogError(
                "[PetStore] Prefab-authored PetContent is incomplete. " +
                "Fix StoreV2Popup.prefab; runtime fallback UI has been removed.",
                this);
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

            RefreshCredits();

            if (_previewMode)
            {
                _petCatalog =
                    BuildPreviewPetCatalog();

                _skinCatalog =
                    BuildPreviewSkinCatalog();

                ShowPets(
                    _selectedPetId);

                SetStatus(
                    "SELECT A PET OR SKIN.");

                return;
            }

            if (!_loading
                && _petCatalog.Length == 0)
            {
                LoadCatalog();
                return;
            }

            ShowPets(
                _selectedPetId);

            SetStatus(
                TeamToolOwnershipSession.IsLoaded
                    ? "SELECT A PET OR SKIN."
                    : "LOADING INVENTORY...");
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
                _petCatalog =
                    Array.Empty<ShopItem>();

                _skinCatalog =
                    Array.Empty<ShopItem>();

                ShowPets(
                    _selectedPetId);

                SetStatus(
                    "PET STORE SERVICE UNAVAILABLE.");

                return;
            }

            _loading = true;

            SetStatus(
                "LOADING PET CATALOG...");

            runtime.Client.GetJson<CatalogResponse>(
                ApiEndpoints.ShopPets,
                true,
                petResult =>
                {
                    if (this == null)
                        return;

                    bool petAvailable =
                        petResult.IsSuccess
                        && petResult.Data != null
                        && petResult.Data.success
                        && petResult.Data.data != null;

                    _petCatalog =
                        petAvailable
                            ? petResult.Data.data.items
                              ?? Array.Empty<ShopItem>()
                            : Array.Empty<ShopItem>();

                    runtime.Client.GetJson<CatalogResponse>(
                        ApiEndpoints.ShopPetSkins,
                        true,
                        skinResult =>
                        {
                            if (this == null)
                                return;

                            bool skinAvailable =
                                skinResult.IsSuccess
                                && skinResult.Data != null
                                && skinResult.Data.success
                                && skinResult.Data.data != null;

                            _skinCatalog =
                                skinAvailable
                                    ? skinResult.Data.data.items
                                      ?? Array.Empty<ShopItem>()
                                    : Array.Empty<ShopItem>();

                            TeamToolOwnershipSession.Refresh(
                                inventoryLoaded =>
                                {
                                    if (this == null)
                                        return;

                                    _loading =
                                        false;

                                    ShowPets(
                                        _selectedPetId);

                                    if (!petAvailable)
                                    {
                                        SetStatus(
                                            "PET CATALOG UNAVAILABLE.");
                                    }
                                    else if (!skinAvailable)
                                    {
                                        SetStatus(
                                            "PET SKIN CATALOG UNAVAILABLE.");
                                    }
                                    else if (!inventoryLoaded)
                                    {
                                        SetStatus(
                                            "INVENTORY STATUS UNAVAILABLE.");
                                    }
                                    else
                                    {
                                        SetStatus(
                                            "SELECT A PET OR SKIN.");
                                    }
                                });
                        });
                });
        }

        private void ShowPets(
            int selectedPetId)
        {
            ClearChildren(
                _petGrid);

            for (int i = 0;
                 i < PetShopAssets.Pets.Length;
                 i++)
            {
                var definition =
                    PetShopAssets.Pets[i];

                var item =
                    FindItem(
                        _petCatalog,
                        definition.ItemId);

                CreatePetCard(
                    definition,
                    item);
            }

            if (!PetShopAssets.TryGetPet(
                    selectedPetId,
                    out var selected))
            {
                selected =
                    PetShopAssets.Pets[0];
            }

            SelectPet(
                selected,
                FindItem(
                    _petCatalog,
                    selected.ItemId));
        }

        private void CreatePetCard(
            PetShopAssets.PetItem definition,
            ShopItem item)
        {
            string thumbnailResource =
                PetShopThumbnailAssets
                    .GetThumbnailResource(
                        definition.PetId);

            GameObject card =
                StoreV2PrefabCardFactory
                    .CreateCatalogCard(
                        transform,
                        _petGrid,
                        "Pet_"
                        + definition.PetId,
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
                    SelectPet(
                        capturedDefinition,
                        capturedItem));
        }

        private void SelectPet(
            PetShopAssets.PetItem definition,
            ShopItem item)
        {
            _selectedPet =
                definition;

            _selectedPetItem =
                item;

            _selectedPetId =
                definition.PetId;

            _selectedSkinId =
                0;

            _purchaseTarget =
                PurchaseTarget.Pet;

            PetShopAssets.TryGetSkin(
                definition.PetId,
                0,
                out _selectedSkin);

            _selectedSkinItem =
                null;

            ShowLivePet();

            UpdateDetails();

            RefreshSelectionStyles();

            SetBuyState();
        }



        private void SelectSkin(
            PetShopAssets.SkinItem definition,
            ShopItem item)
        {
            if (definition.PetId
                != _selectedPetId)
            {
                return;
            }

            _selectedSkin =
                definition;

            _selectedSkinItem =
                item;

            _selectedSkinId =
                definition.SkinId;

            _purchaseTarget =
                PurchaseTarget.Skin;

            ApplySelectedSkin();

            UpdateDetails();

            RefreshSelectionStyles();

            SetBuyState();
        }

        private void RefreshSelectionStyles()
        {
            // STORE_V2_PREFAB_VISUAL_AUTHORITY
            // Selection visuals are authored by prefab Button transition
            // states. Runtime selection code does not mutate Image/Outline.
        }

        private void ShowLivePet()
        {
            var renderer =
                ResolveLivePreviewRenderer();

            bool shown =
                renderer.Show(
                    _selectedPet.PrefabResource,
                    _preview);

            if (!shown)
            {
                _preview.texture =
                    null;

                _preview.color =
                    Color.clear;

                return;
            }

            _preview.color =
                Color.white;

            ApplySelectedSkin();
        }

        private void ApplySelectedSkin()
        {
            var material =
                PetSkinLibrary.Load(
                    _selectedPetId,
                    _selectedSkinId);

            if (material == null)
                return;

            ResolveLivePreviewRenderer()
                .ApplyMaterial(
                    material);
        }

        private void UpdateDetails()
        {
            if (_purchaseTarget
                == PurchaseTarget.Pet)
            {
                StoreReferencePresentation.SetName(_selectedName, _selectedPet.Name);

                _selectedDescription.text =
                    _selectedPet.Description;

                int price =
                    _selectedPetItem != null
                        ? _selectedPetItem.price
                        : _selectedPet.PreviewPrice;

                _selectedPrice.text =
                    $"{price:N0} CR";

                return;
            }

            StoreReferencePresentation.SetName(_selectedName, _selectedPet.Name +
                " // " +
                _selectedSkin.Name);

            _selectedDescription.text =
                _selectedSkin.DefaultOwned
                    ? "DEFAULT SKIN // INCLUDED WITH PET"
                    : "COSMETIC PET SKIN // PERMANENT UNLOCK";

            if (_selectedSkin.DefaultOwned)
            {
                _selectedPrice.text =
                    string.Empty;
            }
            else
            {
                int price =
                    _selectedSkinItem != null
                        ? _selectedSkinItem.price
                        : _selectedSkin.PreviewPrice;

                _selectedPrice.text =
                    $"{price:N0} CR";
            }
        }

        private void SetBuyState()
        {
            if (_buyButton == null
                || _selectedPrice == null)
            {
                return;
            }

            if(StoreLobbyEquipment.ShowAction(_buyButton,_selectedPrice,true,_selectedPetId,(_purchaseTarget == PurchaseTarget.Skin ? _selectedSkinId : 0),(_purchaseTarget == PurchaseTarget.Skin ? PetShopAssets.IsSkinOwned(_selectedPetId,_selectedSkinId) && PetShopAssets.IsPetOwned(_selectedPetId) : PetShopAssets.IsPetOwned(_selectedPetId))))return;

            Text caption =
                _buyButton
                    .GetComponentInChildren<Text>();

            // ==================================================
            // BASE PET
            // ==================================================
            if (_purchaseTarget == PurchaseTarget.Pet)
            {
                if (_selectedPet.PetId <= 0)
                {
                    _selectedPrice.text = "--";
                    _buyButton.interactable = false;

                    if (caption != null)
                        caption.text = EchoProtocol.Settings.GameLanguage.Choose("Chưa khả dụng", "Unavailable");

                    return;
                }

                if (PetShopAssets.IsPetOwned(
                        _selectedPet.PetId))
                {
                    // Do not continue showing 700 / 800 / etc.
                    // after the Pet is already in inventory.
                    _selectedPrice.text = string.Empty;
                    _buyButton.interactable = false;

                    if (caption != null)
                        caption.text = EchoProtocol.Settings.GameLanguage.Choose("Đã sở hữu", "Owned");

                    return;
                }

                int petPrice =
                    _selectedPetItem != null
                        ? _selectedPetItem.price
                        : _selectedPet.PreviewPrice;

                _selectedPrice.text =
                    $"{petPrice:N0} CR";

                if (_previewMode)
                {
                    _buyButton.interactable = true;

                    if (caption != null)
                        caption.text = EchoProtocol.Settings.GameLanguage.Choose("Mua", "Buy");

                    return;
                }

                if (_selectedPetItem == null)
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

            // ==================================================
            // PET SKIN / PET OUTFIT
            // Any non-Pet PurchaseTarget in this controller is
            // the selected Pet skin target.
            // ==================================================
            if (_selectedPet.PetId <= 0)
            {
                _selectedPrice.text = "--";
                _buyButton.interactable = false;

                if (caption != null)
                    caption.text = EchoProtocol.Settings.GameLanguage.Choose("Chưa khả dụng", "Unavailable");

                return;
            }

            // Parent entitlement locks purchase/equip, but the Store
            // should still show the cosmetic's actual price.
            if (!PetShopAssets.IsPetOwned(
                    _selectedPet.PetId))
            {
                if (_selectedSkin.DefaultOwned)
                {
                    _selectedPrice.text =
                        string.Empty;
                }
                else
                {
                    int lockedSkinPrice =
                        _selectedSkinItem != null
                            ? _selectedSkinItem.price
                            : _selectedSkin.PreviewPrice;

                    _selectedPrice.text =
                        $"{lockedSkinPrice:N0} CR";
                }

                _buyButton.interactable =
                    false;

                if (caption != null)
                    caption.text = EchoProtocol.Settings.GameLanguage.Choose("Cần sở hữu thú cưng", "Requires pet");

                return;
            }

            if (_selectedSkin.DefaultOwned)
            {
                _selectedPrice.text =
                    string.Empty;

                _buyButton.interactable =
                    false;

                if (caption != null)
                    caption.text = EchoProtocol.Settings.GameLanguage.Choose("Mặc định", "Default");

                return;
            }

            if (PetShopAssets.IsSkinOwned(
                    _selectedPet.PetId,
                    _selectedSkin.SkinId))
            {
                // Owned skin no longer displays its old price.
                _selectedPrice.text =
                    string.Empty;

                _buyButton.interactable =
                    false;

                if (caption != null)
                    caption.text = EchoProtocol.Settings.GameLanguage.Choose("Đã sở hữu", "Owned");

                return;
            }

            int skinPrice =
                _selectedSkinItem != null
                    ? _selectedSkinItem.price
                    : _selectedSkin.PreviewPrice;

            _selectedPrice.text =
                $"{skinPrice:N0} CR";

            if (_previewMode)
            {
                _buyButton.interactable = true;

                if (caption != null)
                    caption.text = EchoProtocol.Settings.GameLanguage.Choose("Mua", "Buy");

                return;
            }

            if (_selectedSkinItem == null)
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

        private void SetButtonState(
            bool interactable,
            string value,
            Text caption)
        {
            _buyButton.interactable =
                interactable;

            if (caption != null)
            {
                caption.text =
                    value;
            }
        }

        private void PurchaseSelected()
        {
            if(StoreLobbyEquipment.Equip(transform,true,_selectedPetId,(_purchaseTarget == PurchaseTarget.Skin ? _selectedSkinId : 0),(_purchaseTarget == PurchaseTarget.Skin ? PetShopAssets.IsSkinOwned(_selectedPetId,_selectedSkinId) && PetShopAssets.IsPetOwned(_selectedPetId) : PetShopAssets.IsPetOwned(_selectedPetId))))return;

            if (_previewMode)
            {
                SetStatus(
                    "EDITOR PREVIEW MODE.");

                return;
            }

            if (_purchasing)
                return;

            ShopItem item;
            string displayName;

            int purchasingPetId =
                _selectedPetId;

            int purchasingSkinId =
                _selectedSkinId;

            bool purchasingSkin =
                _purchaseTarget
                == PurchaseTarget.Skin;

            if (!purchasingSkin)
            {
                if (PetShopAssets.IsPetOwned(
                        purchasingPetId)
                    || _selectedPetItem == null)
                {
                    return;
                }

                item =
                    _selectedPetItem;

                displayName =
                    _selectedPet.Name;
            }
            else
            {
                if (_selectedSkin.DefaultOwned
                    || !PetShopAssets.IsPetOwned(
                        purchasingPetId)
                    || PetShopAssets.IsSkinOwned(
                        purchasingPetId,
                        purchasingSkinId)
                    || _selectedSkinItem == null)
                {
                    return;
                }

                item =
                    _selectedSkinItem;

                displayName =
                    _selectedPet.Name +
                    " " +
                    _selectedSkin.Name;
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

            _purchasing = true;

            SetStatus(
                "PURCHASING " +
                displayName +
                "...");

            SetBuyState();

            runtime.Client.PostJson<
                PurchaseRequest,
                PurchaseResponse>(
                ApiEndpoints.ShopPurchase,
                new PurchaseRequest
                {
                    itemId =
                        item.itemId,

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
                        item.itemId);
                    StoreLobbyEquipment.Equip(transform,true,purchasingPetId,purchasingSkin ? purchasingSkinId : 0,true);

                    SetStatus(
                        displayName +
                        " PURCHASED.");

                    ShowPets(
                        purchasingPetId);

                    if (purchasingSkin
                        && PetShopAssets.TryGetSkin(
                            purchasingPetId,
                            purchasingSkinId,
                            out var purchasedSkin))
                    {
                        SelectSkin(
                            purchasedSkin,
                            FindItem(
                                _skinCatalog,
                                purchasedSkin.ItemId));
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

        private StoreLivePreviewRenderer
            ResolveLivePreviewRenderer()
        {
            var renderer =
                GetComponent<StoreLivePreviewRenderer>();

            if (renderer == null)
            {
                renderer =
                    gameObject.AddComponent<
                        StoreLivePreviewRenderer>();
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
                    message
                    ?? string.Empty;
            }
        }

        private static ShopItem FindItem(
            ShopItem[] catalog,
            string itemId)
        {
            if (catalog == null
                || string.IsNullOrWhiteSpace(
                    itemId))
            {
                return null;
            }

            for (int i = 0;
                 i < catalog.Length;
                 i++)
            {
                var item =
                    catalog[i];

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

        private bool TryBindExistingPetView()
        {
            RectTransform root =
                transform.Find(
                    "Window/Content/PetContent")
                as RectTransform;

            if (root == null)
                return false;

            RawImage preview =
                root.Find(
                    "PetPreviewBox/PetPreview")
                    ?.GetComponent<RawImage>();

            RectTransform petGrid =
                root.Find(
                    "PetGrid")
                as RectTransform;

            Text selectedName =
                root.Find(
                    "SelectedName")
                    ?.GetComponent<Text>();

            Text selectedDescription =
                root.Find(
                    "SelectedDescription")
                    ?.GetComponent<Text>();

            Text selectedPrice =
                root.Find(
                    "SelectedPrice")
                    ?.GetComponent<Text>();

            Button buyButton =
                root.Find(
                    "PetBuyButton")
                    ?.GetComponent<Button>();

            if (preview == null ||
                petGrid == null ||
                selectedName == null ||
                selectedDescription == null ||
                selectedPrice == null ||
                buyButton == null)
            {
                return false;
            }

            _root =
                root;

            _preview =
                preview;

            _petGrid =
                petGrid;

            _selectedName =
                selectedName;

            _selectedDescription =
                selectedDescription;

            _selectedPrice =
                selectedPrice;

            _buyButton =
                buyButton;

            SetPassiveGraphic(
                _root);

            SetPassiveGraphic(
                _petGrid);

            SetPassiveGraphic(
                _root.Find("PetPreviewBox"));

            _preview.raycastTarget =
                false;

            _buyButton.onClick
                .RemoveAllListeners();

            _buyButton.onClick
                .AddListener(
                    PurchaseSelected);

            return true;
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

        private static ShopItem[]
            BuildPreviewPetCatalog()
        {
            var result =
                new ShopItem[
                    PetShopAssets.Pets.Length];

            for (int i = 0;
                 i < PetShopAssets.Pets.Length;
                 i++)
            {
                var definition =
                    PetShopAssets.Pets[i];

                result[i] =
                    new ShopItem
                    {
                        itemId =
                            definition.ItemId,

                        name =
                            definition.Name,

                        description =
                            definition.Description,

                        assetReference =
                            definition.PrefabResource,

                        price =
                            definition.PreviewPrice
                    };
            }

            return result;
        }

        private static ShopItem[]
            BuildPreviewSkinCatalog()
        {
            var list =
                new List<ShopItem>();

            for (int i = 0;
                 i < PetShopAssets.Skins.Length;
                 i++)
            {
                var definition =
                    PetShopAssets.Skins[i];

                if (definition.DefaultOwned)
                    continue;

                list.Add(
                    new ShopItem
                    {
                        itemId =
                            definition.ItemId,

                        name =
                            definition.Name,

                        description =
                            "Pet cosmetic skin",

                        assetReference =
                            definition.MaterialResource,

                        price =
                            definition.PreviewPrice
                    });
            }

            return list.ToArray();
        }




        private static void ClearChildren(
            RectTransform parent)
        {
            if (StoreReferencePresentation.ClearCatalog(parent))return;

            if (parent == null)
                return;

            for (int i =
                     parent.childCount - 1;
                 i >= 0;
                 i--)
            {
                var child =
                    parent.GetChild(i)
                        .gameObject;

                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(
                        child);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(
                        child);
                }
            }
        }



    }
}
