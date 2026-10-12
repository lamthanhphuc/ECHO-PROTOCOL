using EchoProtocol.Networking;
using EchoProtocol.Pets;
using EchoProtocol.UI.MainMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI
{
    [DisallowMultipleComponent]
    public sealed class LobbyPetSkinSelector : MonoBehaviour
    {
        private sealed class View
        {
            public int Id;
            public Button Button;
            public Image Background;
            public Outline Outline;
            public Image Preview;
            public TMP_Text Name;
            public TMP_Text State;
        }

        private static readonly Color32 Overlay =
            new Color32(0, 0, 0, 214);

        private static readonly Color32 Window =
            new Color32(10, 14, 16, 253);

        private static readonly Color32 Card =
            new Color32(17, 22, 24, 255);

        private static readonly Color32 Selected =
            new Color32(38, 24, 23, 255);

        private static readonly Color32 Border =
            new Color32(63, 70, 71, 235);

        private static readonly Color32 Accent =
            new Color32(151, 58, 51, 255);

        private static readonly Color32 Primary =
            new Color32(222, 225, 221, 255);

        private static readonly Color32 Secondary =
            new Color32(132, 141, 141, 255);

        private static readonly Color32 Locked =
            new Color32(171, 86, 78, 255);

        private readonly View[] _views =
            new View[
                PetSkinLibrary.SkinCount];

        private RectTransform _root;
        private RectTransform _window;
        private Image _largePreview;
        private TMP_Text _title;
        private TMP_Text _status;

        private LobbyPlayerState _state;
        private int _selectedSkin;
        private bool _open;
        private float _nextRefresh;

        public void Open(
            LobbyPlayerState state)
        {
            if (state == null)
                return;

            EnsureBuilt();

            _state = state;
            _selectedSkin =
                Mathf.Clamp(
                    state.PetSkinId,
                    0,
                    PetSkinLibrary.SkinCount - 1);

            _open = true;
            _root.gameObject.SetActive(
                true);

            _root.SetAsLastSibling();

            if (!TeamToolOwnershipSession.IsLoaded
                && !TeamToolOwnershipSession.IsLoading)
            {
                TeamToolOwnershipSession.Refresh();
            }

            Refresh();
        }

        public void Close()
        {
            _open = false;
            _state = null;

            if (_root != null)
                _root.gameObject.SetActive(
                    false);
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
                Time.unscaledTime + 0.12f;

            Refresh();
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
                return;

            var parent =
                canvas.rootCanvas != null
                    ? canvas.rootCanvas.transform
                    : canvas.transform;

            var overlay =
                new GameObject(
                    "LobbyPetSkinSelectorOverlay",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button));

            overlay.transform.SetParent(
                parent,
                false);

            _root =
                overlay.GetComponent<RectTransform>();

            Stretch(_root);

            var overlayImage =
                overlay.GetComponent<Image>();

            overlayImage.color =
                Overlay;

            var blocker =
                overlay.GetComponent<Button>();

            blocker.transition =
                Selectable.Transition.None;

            blocker.targetGraphic =
                overlayImage;

            blocker.onClick.AddListener(
                Close);

            _window =
                CreateRect(
                    "PetOutfitWindow",
                    _root,
                    0f,
                    0f,
                    920f,
                    560f,
                    Window);

            _window.anchorMin =
                new Vector2(0.5f, 0.5f);

            _window.anchorMax =
                new Vector2(0.5f, 0.5f);

            _window.pivot =
                new Vector2(0.5f, 0.5f);

            _window.anchoredPosition =
                Vector2.zero;

            _title =
                CreateText(
                    "Title",
                    _window,
                    "PET OUTFIT",
                    28f,
                    22f,
                    660f,
                    32f,
                    22,
                    FontStyles.Bold,
                    Primary);

            CreateText(
                "Hint",
                _window,
                "SELECT A PET MODEL COLOR / SKIN",
                28f,
                54f,
                660f,
                20f,
                9,
                FontStyles.Normal,
                Secondary);

            var close =
                CreateButton(
                    "Close",
                    _window,
                    "CLOSE",
                    804f,
                    22f,
                    88f,
                    34f);

            close.onClick.AddListener(
                Close);

            var previewFrame =
                CreateRect(
                    "PreviewFrame",
                    _window,
                    28f,
                    96f,
                    340f,
                    400f,
                    new Color32(
                        5,
                        8,
                        9,
                        255));

            _largePreview =
                CreatePreviewImage(
                    previewFrame,
                    8f,
                    8f,
                    324f,
                    384f);

            for (int i = 0;
                 i < _views.Length;
                 i++)
            {
                float x =
                    398f
                    + (i % 2)
                    * 238f;

                float y =
                    96f
                    + (i / 2)
                    * 190f;

                CreateSkinCard(
                    i,
                    x,
                    y);
            }

            _status =
                CreateText(
                    "Status",
                    _window,
                    string.Empty,
                    398f,
                    496f,
                    482f,
                    28f,
                    10,
                    FontStyles.Bold,
                    Secondary);

            _root.gameObject.SetActive(
                false);
        }

        private void CreateSkinCard(
            int skinId,
            float x,
            float y)
        {
            var card =
                CreateRect(
                    "Skin_"
                    + skinId,
                    _window,
                    x,
                    y,
                    220f,
                    168f,
                    Card);

            var background =
                card.GetComponent<Image>();

            var button =
                card.gameObject
                    .AddComponent<Button>();

            button.targetGraphic =
                background;

            var preview =
                CreatePreviewImage(
                    card,
                    8f,
                    8f,
                    204f,
                    112f);

            var name =
                CreateText(
                    "Name",
                    card,
                    "SKIN",
                    10f,
                    124f,
                    130f,
                    25f,
                    9,
                    FontStyles.Bold,
                    Primary);

            var state =
                CreateText(
                    "State",
                    card,
                    string.Empty,
                    142f,
                    124f,
                    68f,
                    25f,
                    8,
                    FontStyles.Bold,
                    Secondary);

            state.alignment =
                TextAlignmentOptions.Right;

            int captured =
                skinId;

            button.onClick.AddListener(
                () => SelectSkin(
                    captured));

            _views[skinId] =
                new View
                {
                    Id = skinId,
                    Button = button,
                    Background = background,
                    Outline =
                        card.GetComponent<Outline>(),
                    Preview = preview,
                    Name = name,
                    State = state,
                };
        }

        private void SelectSkin(
            int skinId)
        {
            if (_state == null
                || _state.IsReady
                || _state.PetId <= 0)
            {
                return;
            }

            _selectedSkin =
                Mathf.Clamp(
                    skinId,
                    0,
                    PetSkinLibrary.SkinCount - 1);

            bool owned =
                PetShopAssets.IsSkinOwned(
                    _state.PetId,
                    _selectedSkin);

            if (!owned)
            {
                Refresh();
                return;
            }

            if (_state.PetSkinId
                != _selectedSkin)
            {
                _state.RequestPetSkin(
                    _selectedSkin);
            }

            Refresh();
        }

        private void Refresh()
        {
            if (_state == null)
                return;

            int petId =
                _state.PetId;

            if (petId <= 0)
            {
                _title.text =
                    "PET OUTFIT // NONE";

                LobbyModelThumbnailUtility.SetResource(
                    _largePreview,
                    null,
                    Color.clear);

                foreach (var view
                         in _views)
                {
                    view.Button.interactable =
                        false;

                    LobbyModelThumbnailUtility.SetResource(
                        view.Preview,
                        null,
                        Color.clear);

                    view.Name.text =
                        "NO PET";

                    view.State.text =
                        "BLOCKED";

                    view.State.color =
                        Locked;
                }

                _status.text =
                    "SELECT AN OWNED PET WITH < > FIRST.";

                return;
            }

            _title.text =
                "PET OUTFIT // "
                + GetPetName(
                    petId);

            string resource =
                LobbyModelThumbnailUtility
                    .GetPetBaseResource(
                        petId);

            LobbyModelThumbnailUtility.SetResource(
                _largePreview,
                resource,
                LobbyModelThumbnailUtility
                    .GetPetSkinTint(
                        petId,
                        _selectedSkin));

            for (int i = 0;
                 i < _views.Length;
                 i++)
            {
                var view =
                    _views[i];

                bool owned =
                    PetShopAssets.IsSkinOwned(
                        petId,
                        view.Id);

                bool equipped =
                    _state.PetSkinId
                    == view.Id;

                bool selected =
                    _selectedSkin
                    == view.Id;

                LobbyModelThumbnailUtility.SetResource(
                    view.Preview,
                    resource,
                    LobbyModelThumbnailUtility
                        .GetPetSkinTint(
                            petId,
                            view.Id));

                string skinName =
                    PetSkinLibrary.GetColorName(
                        petId,
                        view.Id);

                view.Name.text =
                    string.IsNullOrWhiteSpace(
                        skinName)
                        ? "SKIN " + view.Id
                        : skinName;

                view.State.text =
                    equipped
                        ? "EQUIPPED"
                        : owned
                            ? "OWNED"
                            : "LOCKED";

                view.State.color =
                    owned
                        ? Secondary
                        : Locked;

                view.Background.color =
                    selected
                        ? Selected
                        : Card;

                view.Outline.effectColor =
                    selected
                        ? Accent
                        : Border;

                // Locked skins remain visible so the player
                // can inspect them, but SelectSkin blocks equip.
                view.Button.interactable =
                    !_state.IsReady;
            }

            bool selectedOwned =
                PetShopAssets.IsSkinOwned(
                    petId,
                    _selectedSkin);

            _status.text =
                _state.IsReady
                    ? "UNREADY TO CHANGE PET OUTFIT."
                    : selectedOwned
                        ? "MODEL PREVIEW // CLICK TO EQUIP"
                        : "LOCKED // PURCHASE THIS SKIN IN STORE.";
        }

        private static string GetPetName(
            int petId)
        {
            if (PetShopAssets.TryGetPet(
                    petId,
                    out var pet))
            {
                return pet.Name;
            }

            return PetCatalog.Name(
                petId);
        }

        private static Image CreatePreviewImage(
            Transform parent,
            float x,
            float y,
            float width,
            float height)
        {
            var owner =
                new GameObject(
                    "ModelPreview",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));

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

            image.preserveAspect =
                true;

            image.color =
                Color.clear;

            image.raycastTarget =
                false;

            return image;
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent,
            float x,
            float y,
            float width,
            float height,
            Color color)
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

            var outline =
                owner.GetComponent<Outline>();

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
                    Card);

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
                    9,
                    FontStyles.Bold,
                    Primary);

            label.alignment =
                TextAlignmentOptions.Center;

            return button;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            string value,
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
                value;

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

            text.textWrappingMode =
                TextWrappingModes.NoWrap;

            return text;
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
    }
}
