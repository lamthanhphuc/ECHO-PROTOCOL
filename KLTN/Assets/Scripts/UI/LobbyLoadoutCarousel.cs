using System.Collections.Generic;
using EchoProtocol.Networking;
using EchoProtocol.Pets;
using EchoProtocol.UI.MainMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI
{
    [DisallowMultipleComponent]
    public sealed class LobbyLoadoutCarousel : MonoBehaviour
    {
        private static readonly Color32 Primary =
            new Color32(222, 225, 221, 255);

        private static readonly Color32 Secondary =
            new Color32(132, 141, 141, 255);

        private static readonly Color32 Locked =
            new Color32(171, 86, 78, 255);

        private static readonly Color32 RowNormal =
            new Color32(14, 18, 19, 252);

        private static readonly Color32 Frame =
            new Color32(5, 8, 9, 255);

        private static readonly Color32 Border =
            new Color32(58, 66, 67, 255);

        private static readonly Color32 ArrowNormal =
            new Color32(18, 24, 25, 245);

        private static readonly Color32 ArrowHover =
            new Color32(42, 49, 50, 255);

        private LobbyManager _manager;

        private Button _characterButton;
        private Button _outfitButton;
        private Button _petButton;
        private Button _petOutfitButton;

        private TMP_Text _characterLabel;
        private TMP_Text _outfitLabel;
        private TMP_Text _petLabel;
        private TMP_Text _petOutfitLabel;

        private Image _characterPreview;
        private Image _outfitPreview;
        private Image _petPreview;
        private Image _petOutfitPreview;

        private Button _characterPrevious;
        private Button _characterNext;
        private Button _petPrevious;
        private Button _petNext;

        private GameObject _characterLockOverlay;
        private GameObject _petLockOverlay;

        private int _characterBrowseId =
            int.MinValue;

        private int _petBrowseId =
            int.MinValue;

        private bool _subscribed;
        private float _nextRefresh;

        public void Bind(
            LobbyManager manager,
            Button characterButton,
            Button outfitButton,
            Button petButton,
            Button petOutfitButton)
        {
            if (manager != null)
            {
                _manager = manager;
            }
            else if (_manager == null)
            {
                _manager =
                    FindAnyObjectByType<LobbyManager>();
            }

            _characterButton = characterButton;
            _outfitButton = outfitButton;
            _petButton = petButton;
            _petOutfitButton = petOutfitButton;

            BuildVisuals();
            Subscribe();

            if (!TeamToolOwnershipSession.IsLoaded
                && !TeamToolOwnershipSession.IsLoading)
            {
                TeamToolOwnershipSession.Refresh();
            }

            Refresh(true);
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (Time.unscaledTime
                < _nextRefresh)
            {
                return;
            }

            _nextRefresh =
                Time.unscaledTime
                + 0.10f;

            Refresh(false);
        }

        private void Subscribe()
        {
            if (_subscribed)
                return;

            TeamToolOwnershipSession.Changed +=
                HandleOwnershipChanged;

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
                return;

            TeamToolOwnershipSession.Changed -=
                HandleOwnershipChanged;

            _subscribed = false;
        }

        private void HandleOwnershipChanged()
        {
            Refresh(false);
        }

        private void BuildVisuals()
        {
            BuildCharacterRow();
            BuildOutfitRow();
            BuildPetRow();
            BuildPetOutfitRow();
        }

        private void BuildCharacterRow()
        {
            if (_characterButton == null)
                return;

            var preview =
                _characterButton.transform.Find(
                    "PreviewFrame/Preview");

            _characterPreview =
                preview != null
                    ? preview.GetComponent<Image>()
                    : null;

            var lockOverlay =
                _characterButton.transform.Find(
                    "PreviewFrame/LockOverlay");

            _characterLockOverlay =
                lockOverlay != null
                    ? lockOverlay.gameObject
                    : null;

            var value =
                _characterButton.transform.Find(
                    "ValueText");

            _characterLabel =
                value != null
                    ? value.GetComponent<TMP_Text>()
                    : null;

            var previous =
                _characterButton.transform.Find(
                    "PreviousButton");

            _characterPrevious =
                previous != null
                    ? previous.GetComponent<Button>()
                    : null;

            var next =
                _characterButton.transform.Find(
                    "NextButton");

            _characterNext =
                next != null
                    ? next.GetComponent<Button>()
                    : null;

            if (_characterPrevious != null)
            {
                _characterPrevious.onClick.RemoveAllListeners();

                _characterPrevious.onClick.AddListener(
                    () => CycleCharacter(-1));
            }

            if (_characterNext != null)
            {
                _characterNext.onClick.RemoveAllListeners();

                _characterNext.onClick.AddListener(
                    () => CycleCharacter(1));
            }
        }

        private void BuildOutfitRow()
        {
            if (_outfitButton == null)
                return;

            var preview =
                _outfitButton.transform.Find(
                    "PreviewFrame/Preview");

            _outfitPreview =
                preview != null
                    ? preview.GetComponent<Image>()
                    : null;

            var value =
                _outfitButton.transform.Find(
                    "ValueText");

            _outfitLabel =
                value != null
                    ? value.GetComponent<TMP_Text>()
                    : null;
        }

        private void BuildPetRow()
        {
            if (_petButton == null)
                return;

            var preview =
                _petButton.transform.Find(
                    "PreviewFrame/Preview");

            _petPreview =
                preview != null
                    ? preview.GetComponent<Image>()
                    : null;

            var lockOverlay =
                _petButton.transform.Find(
                    "PreviewFrame/LockOverlay");

            _petLockOverlay =
                lockOverlay != null
                    ? lockOverlay.gameObject
                    : null;

            var value =
                _petButton.transform.Find(
                    "ValueText");

            _petLabel =
                value != null
                    ? value.GetComponent<TMP_Text>()
                    : null;

            var previous =
                _petButton.transform.Find(
                    "PreviousButton");

            _petPrevious =
                previous != null
                    ? previous.GetComponent<Button>()
                    : null;

            var next =
                _petButton.transform.Find(
                    "NextButton");

            _petNext =
                next != null
                    ? next.GetComponent<Button>()
                    : null;

            if (_petPrevious != null)
            {
                _petPrevious.onClick.RemoveAllListeners();

                _petPrevious.onClick.AddListener(
                    () => CyclePet(-1));
            }

            if (_petNext != null)
            {
                _petNext.onClick.RemoveAllListeners();

                _petNext.onClick.AddListener(
                    () => CyclePet(1));
            }
        }

        private void BuildPetOutfitRow()
        {
            if (_petOutfitButton == null)
                return;

            var preview =
                _petOutfitButton.transform.Find(
                    "PreviewFrame/Preview");

            _petOutfitPreview =
                preview != null
                    ? preview.GetComponent<Image>()
                    : null;

            var value =
                _petOutfitButton.transform.Find(
                    "ValueText");

            _petOutfitLabel =
                value != null
                    ? value.GetComponent<TMP_Text>()
                    : null;
        }

        private static RectTransform ResetRow(
            Button row,
            string rootName)
        {
            var image =
                row.targetGraphic as Image
                ?? row.GetComponent<Image>();

            if (image != null)
            {
                image.color =
                    RowNormal;
            }

            for (int i = row.transform.childCount - 1;
                 i >= 0;
                 i--)
            {
                var child =
                    row.transform.GetChild(i);

                child.gameObject.SetActive(
                    false);
            }

            var owner =
                new GameObject(
                    rootName,
                    typeof(RectTransform));

            owner.transform.SetParent(
                row.transform,
                false);

            var rect =
                owner.GetComponent<RectTransform>();

            rect.anchorMin =
                Vector2.zero;

            rect.anchorMax =
                Vector2.one;

            rect.offsetMin =
                Vector2.zero;

            rect.offsetMax =
                Vector2.zero;

            return rect;
        }

        private static Image CreatePreview(
            Transform parent,
            float x,
            float y,
            float width,
            float height)
        {
            var frameObject =
                new GameObject(
                    "PreviewFrame",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Outline));

            frameObject.transform.SetParent(
                parent,
                false);

            var frameRect =
                frameObject.GetComponent<RectTransform>();

            PlaceTopLeft(
                frameRect,
                x,
                y,
                width,
                height);

            var frameImage =
                frameObject.GetComponent<Image>();

            frameImage.color =
                Frame;

            frameImage.raycastTarget =
                false;

            var outline =
                frameObject.GetComponent<Outline>();

            outline.effectColor =
                Border;

            outline.effectDistance =
                new Vector2(
                    1f,
                    -1f);

            var imageObject =
                new GameObject(
                    "Preview",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));

            imageObject.transform.SetParent(
                frameRect,
                false);

            var imageRect =
                imageObject.GetComponent<RectTransform>();

            imageRect.anchorMin =
                Vector2.zero;

            imageRect.anchorMax =
                Vector2.one;

            imageRect.offsetMin =
                new Vector2(
                    3f,
                    3f);

            imageRect.offsetMax =
                new Vector2(
                    -3f,
                    -3f);

            var image =
                imageObject.GetComponent<Image>();

            image.raycastTarget =
                false;

            image.preserveAspect =
                true;

            image.color =
                Color.clear;

            return image;
        }

        private static TMP_Text CreateLabel(
            Transform parent,
            float x,
            float y,
            float width,
            float height)
        {
            var owner =
                new GameObject(
                    "Label",
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

            var label =
                owner.GetComponent<TextMeshProUGUI>();

            label.fontSize =
                13f;

            label.fontSizeMin =
                8f;

            label.fontSizeMax =
                13f;

            label.enableAutoSizing =
                true;

            label.fontStyle =
                FontStyles.Bold;

            label.color =
                Primary;

            label.alignment =
                TextAlignmentOptions.Left;

            label.textWrappingMode =
                TextWrappingModes.NoWrap;

            label.overflowMode =
                TextOverflowModes.Ellipsis;

            label.raycastTarget =
                false;

            return label;
        }

        private static Button CreateArrow(
            Transform parent,
            string name,
            string caption,
            float x,
            float y,
            float width,
            float height)
        {
            var owner =
                new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button));

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

            var image =
                owner.GetComponent<Image>();

            image.color =
                ArrowNormal;

            var button =
                owner.GetComponent<Button>();

            button.targetGraphic =
                image;

            var colors =
                button.colors;

            colors.normalColor =
                ArrowNormal;

            colors.highlightedColor =
                ArrowHover;

            colors.selectedColor =
                ArrowHover;

            colors.pressedColor =
                new Color32(
                    81,
                    43,
                    40,
                    255);

            colors.disabledColor =
                new Color32(
                    9,
                    11,
                    11,
                    120);

            button.colors =
                colors;

            var textObject =
                new GameObject(
                    "Text",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));

            textObject.transform.SetParent(
                owner.transform,
                false);

            var textRect =
                textObject.GetComponent<RectTransform>();

            textRect.anchorMin =
                Vector2.zero;

            textRect.anchorMax =
                Vector2.one;

            textRect.offsetMin =
                Vector2.zero;

            textRect.offsetMax =
                Vector2.zero;

            var text =
                textObject.GetComponent<TextMeshProUGUI>();

            text.text =
                caption;

            text.fontSize =
                17f;

            text.fontStyle =
                FontStyles.Bold;

            text.color =
                Primary;

            text.alignment =
                TextAlignmentOptions.Center;

            text.raycastTarget =
                false;

            return button;
        }

        private static void PlaceTopLeft(
            RectTransform rect,
            float x,
            float y,
            float width,
            float height)
        {
            rect.anchorMin =
                new Vector2(
                    0f,
                    1f);

            rect.anchorMax =
                new Vector2(
                    0f,
                    1f);

            rect.pivot =
                new Vector2(
                    0f,
                    1f);

            rect.anchoredPosition =
                new Vector2(
                    x,
                    -y);

            rect.sizeDelta =
                new Vector2(
                    width,
                    height);
        }

        private bool TryGetState(
            out LobbyPlayerState state)
        {
            state = null;

            if (_manager == null)
            {
                _manager =
                    FindAnyObjectByType<LobbyManager>();
            }

            if (_manager == null)
            {
                return false;
            }

            return _manager.TryGetLocalPlayerState(
                out state,
                false);
        }

        private void CycleCharacter(
            int direction)
        {
            if (!TryGetState(
                    out var state)
                || state.IsReady)
            {
                return;
            }

            var items =
                CharacterShopAssets.Items;

            if (items == null
                || items.Length == 0)
            {
                return;
            }

            int current =
                _characterBrowseId
                    == int.MinValue
                    ? state.CharacterId
                    : _characterBrowseId;

            int index =
                0;

            for (int i = 0;
                 i < items.Length;
                 i++)
            {
                if (items[i].CharacterId
                    == current)
                {
                    index = i;
                    break;
                }
            }

            index =
                (index + direction)
                % items.Length;

            if (index < 0)
                index += items.Length;

            var candidate =
                items[index];

            _characterBrowseId =
                candidate.CharacterId;

            if (IsCharacterOwned(candidate)
                && state.CharacterId
                    != candidate.CharacterId)
            {
                state.RequestCharacter(
                    candidate.CharacterId);
            }

            Refresh(false);
        }

        private void CyclePet(
            int direction)
        {
            if (!TryGetState(
                    out var state)
                || state.IsReady)
            {
                return;
            }

            var ids =
                BuildPetIds();

            if (ids.Count == 0)
                return;

            int current =
                _petBrowseId
                    == int.MinValue
                    ? state.PetId
                    : _petBrowseId;

            int index =
                ids.IndexOf(
                    current);

            if (index < 0)
                index = 0;

            index =
                (index + direction)
                % ids.Count;

            if (index < 0)
                index += ids.Count;

            int candidate =
                ids[index];

            _petBrowseId =
                candidate;

            if (IsPetOwned(candidate)
                && state.PetId
                    != candidate)
            {
                state.RequestPet(
                    candidate);
            }

            Refresh(false);
        }

        private static List<int> BuildPetIds()
        {
            var result =
                new List<int>
                {
                    0
                };

            for (int id = 1;
                 id <= 64;
                 id++)
            {
                if (PetShopAssets.TryGetPet(
                        id,
                        out _))
                {
                    result.Add(
                        id);
                }
            }

            return result;
        }

        private static bool IsCharacterOwned(
            CharacterShopAssets.Item item)
        {
            if (item.CharacterId == 0)
                return true;

            return TeamToolOwnershipSession.IsLoaded
                && TeamToolOwnershipSession.OwnsItem(
                    item.ItemId);
        }

        private static bool IsPetOwned(
            int petId)
        {
            if (petId == 0)
                return true;

            return PetShopAssets.IsPetOwned(
                petId);
        }

        private static CharacterShopAssets.Item
            FindCharacter(
                int characterId)
        {
            var items =
                CharacterShopAssets.Items;

            for (int i = 0;
                 i < items.Length;
                 i++)
            {
                if (items[i].CharacterId
                    == characterId)
                {
                    return items[i];
                }
            }

            return items.Length > 0
                ? items[0]
                : default;
        }

        private static string GetPetName(
            int petId)
        {
            if (petId == 0)
                return "NONE";

            if (PetShopAssets.TryGetPet(
                    petId,
                    out var item))
            {
                return item.Name;
            }

            return PetCatalog.Name(
                petId);
        }

        private void Refresh(
            bool initialize)
        {
            if (!TryGetState(
                    out var state))
            {
                return;
            }

            if (initialize
                || _characterBrowseId
                    == int.MinValue)
            {
                _characterBrowseId =
                    state.CharacterId;
            }

            if (initialize
                || _petBrowseId
                    == int.MinValue)
            {
                _petBrowseId =
                    state.PetId;
            }

            RefreshCharacter(state);
            RefreshOutfit(state);
            RefreshPet(state);
            RefreshPetOutfit(state);
        }

        private void RefreshCharacter(
            LobbyPlayerState state)
        {
            var candidate =
                FindCharacter(
                    _characterBrowseId);

            bool owned =
                IsCharacterOwned(
                    candidate);

            bool equipped =
                state.CharacterId
                == candidate.CharacterId;

            if (_characterLabel != null)
            {
                _characterLabel.text =
                    candidate.Name
                    + (!owned
                        ? "\nLOCKED"
                        : equipped
                            ? string.Empty
                            : "\nSELECTING");

                _characterLabel.color =
                    owned
                        ? Primary
                        : Locked;
            }

            LobbyModelThumbnailUtility.SetResource(
                _characterPreview,
                LobbyModelThumbnailUtility
                    .GetCharacterPortraitResource(
                        candidate.CharacterId),
                Color.white);

            if (_characterLockOverlay != null)
            {
                _characterLockOverlay.SetActive(
                    !owned);
            }

            bool canBrowse =
                !state.IsReady;

            if (_characterPrevious != null)
            {
                _characterPrevious.interactable =
                    canBrowse;
            }

            if (_characterNext != null)
            {
                _characterNext.interactable =
                    canBrowse;
            }
        }

        private void RefreshOutfit(
            LobbyPlayerState state)
        {
            if (_outfitLabel != null)
            {
                _outfitLabel.text =
                    "OUTFIT   //   "
                    + CharacterOutfitLibrary.GetName(
                        state.CharacterId,
                        state.CharacterOutfitId);

                _outfitLabel.color =
                    Primary;
            }

            LobbyModelThumbnailUtility.SetResource(
                _outfitPreview,
                LobbyModelThumbnailUtility
                    .GetCharacterBaseResource(
                        state.CharacterId),
                LobbyModelThumbnailUtility
                    .GetCharacterOutfitTint(
                        state.CharacterId,
                        state.CharacterOutfitId));
        }

        private void RefreshPet(
            LobbyPlayerState state)
        {
            int candidate =
                _petBrowseId;

            bool owned =
                IsPetOwned(
                    candidate);

            bool equipped =
                state.PetId
                == candidate;

            if (_petLabel != null)
            {
                _petLabel.text =
                    GetPetName(
                        candidate)
                    + (!owned
                        ? "\nLOCKED"
                        : equipped
                            ? string.Empty
                            : "\nSELECTING");

                _petLabel.color =
                    owned
                        ? Primary
                        : Locked;
            }

            LobbyModelThumbnailUtility.SetResource(
                _petPreview,
                LobbyModelThumbnailUtility
                    .GetPetBaseResource(
                        candidate),
                Color.white);

            if (_petLockOverlay != null)
            {
                _petLockOverlay.SetActive(
                    !owned);
            }

            bool canBrowse =
                !state.IsReady;

            if (_petPrevious != null)
            {
                _petPrevious.interactable =
                    canBrowse;
            }

            if (_petNext != null)
            {
                _petNext.interactable =
                    canBrowse;
            }
        }

        private void RefreshPetOutfit(
            LobbyPlayerState state)
        {
            bool hasPet =
                state.PetId > 0;

            if (_petOutfitLabel != null)
            {
                if (!hasPet)
                {
                    _petOutfitLabel.text =
                        "PET OUTFIT   //   NONE";

                    _petOutfitLabel.color =
                        Secondary;
                }
                else
                {
                    string colorName =
                        PetSkinLibrary.GetColorName(
                            state.PetId,
                            state.PetSkinId);

                    if (string.IsNullOrWhiteSpace(
                            colorName))
                    {
                        colorName =
                            "SKIN "
                            + state.PetSkinId;
                    }

                    _petOutfitLabel.text =
                        "PET OUTFIT   //   "
                        + colorName;

                    _petOutfitLabel.color =
                        Primary;
                }
            }

            LobbyModelThumbnailUtility.SetResource(
                _petOutfitPreview,
                hasPet
                    ? LobbyModelThumbnailUtility
                        .GetPetBaseResource(
                            state.PetId)
                    : null,
                hasPet
                    ? LobbyModelThumbnailUtility
                        .GetPetSkinTint(
                            state.PetId,
                            state.PetSkinId)
                    : Color.clear);

            if (_petOutfitButton != null)
            {
                _petOutfitButton.interactable =
                    hasPet
                    && !state.IsReady;
            }
        }
    }
}
