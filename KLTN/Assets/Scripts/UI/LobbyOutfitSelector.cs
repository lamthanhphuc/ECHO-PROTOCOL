using EchoProtocol.Networking;
using EchoProtocol.UI.MainMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI
{
    [DisallowMultipleComponent]
    public sealed class LobbyOutfitSelector : MonoBehaviour
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

        private readonly View[] _views =
            new View[
                CharacterOutfitLibrary.OutfitCount];

        private RectTransform _root;
        private RectTransform _window;
        private Image _largePreview;
        private TMP_Text _title;
        private TMP_Text _status;

        private LobbyPlayerState _state;
        private int _selectedOutfit;
        private bool _open;
        private float _nextRefresh;

        public void Open(
            LobbyPlayerState state)
        {
            if (state == null)
                return;

            EnsureBuilt();

            _state = state;
            _selectedOutfit =
                Mathf.Clamp(
                    state.CharacterOutfitId,
                    0,
                    CharacterOutfitLibrary.OutfitCount - 1);

            _open = true;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();

            Refresh();
        }

        public void Close()
        {
            _open = false;
            _state = null;

            if (_root != null)
                _root.gameObject.SetActive(false);
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
                    "LobbyOutfitSelectorOverlay",
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
                    "OutfitWindow",
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
                    "OUTFIT",
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
                "SELECT AN OUTFIT MODEL",
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

                CreateOutfitCard(
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

        private void CreateOutfitCard(
            int outfitId,
            float x,
            float y)
        {
            var card =
                CreateRect(
                    "Outfit_"
                    + outfitId,
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
                    "OUTFIT",
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
                outfitId;

            button.onClick.AddListener(
                () => SelectOutfit(
                    captured));

            _views[outfitId] =
                new View
                {
                    Id = outfitId,
                    Button = button,
                    Background = background,
                    Outline =
                        card.GetComponent<Outline>(),
                    Preview = preview,
                    Name = name,
                    State = state,
                };
        }

        private void SelectOutfit(
            int outfitId)
        {
            if (_state == null
                || _state.IsReady)
            {
                return;
            }

            _selectedOutfit =
                Mathf.Clamp(
                    outfitId,
                    0,
                    CharacterOutfitLibrary.OutfitCount - 1);

            int characterId =
                _state.CharacterId;

            // Store preview is unrestricted, but Lobby equip requires
            // an inventory-owned Character outfit.
            if (!EchoProtocol.UI.MainMenu.CharacterOutfitShopAssets.IsOwned(
                    characterId,
                    _selectedOutfit))
            {
                Debug.LogWarning(
                    "[Lobby] Character outfit locked: "
                    + characterId
                    + ":"
                    + _selectedOutfit
                    + " // purchase this outfit in Store.",
                    this);

                return;
            }

            if (_state.CharacterOutfitId
                != _selectedOutfit)
            {
                _state.RequestCharacterOutfit(
                    _selectedOutfit);
            }

            Refresh();
        }

        private void Refresh()
        {
            if (_state == null)
                return;

            int characterId =
                _state.CharacterId;

            _title.text =
                "OUTFIT // "
                + GetCharacterName(
                    characterId);

            string resource =
                LobbyModelThumbnailUtility
                    .GetCharacterBaseResource(
                        characterId);

            LobbyModelThumbnailUtility.SetResource(
                _largePreview,
                resource,
                LobbyModelThumbnailUtility
                    .GetCharacterOutfitTint(
                        characterId,
                        _selectedOutfit));

            for (int i = 0;
                 i < _views.Length;
                 i++)
            {
                var view =
                    _views[i];

                bool equipped =
                    _state.CharacterOutfitId
                    == view.Id;

                bool selected =
                    _selectedOutfit
                    == view.Id;

                bool owned =
                    EchoProtocol.UI.MainMenu.CharacterOutfitShopAssets.IsOwned(
                        characterId,
                        view.Id);

                bool isDefault =
                    view.Id == 0;

                LobbyModelThumbnailUtility.SetResource(
                    view.Preview,
                    resource,
                    LobbyModelThumbnailUtility
                        .GetCharacterOutfitTint(
                            characterId,
                            view.Id));

                view.Name.text =
                    CharacterOutfitLibrary.GetName(
                        characterId,
                        view.Id);

                view.State.text =
                    equipped
                        ? "EQUIPPED"
                        : owned
                            ? isDefault
                                ? "DEFAULT"
                                : "OWNED"
                            : "LOCKED";

                view.Background.color =
                    selected
                        ? Selected
                        : Card;

                view.Outline.effectColor =
                    selected
                        ? Accent
                        : Border;

                view.Button.interactable =
                    !_state.IsReady
                    && owned
                    && !equipped;
            }

            bool selectedOwned =
                EchoProtocol.UI.MainMenu.CharacterOutfitShopAssets.IsOwned(
                    characterId,
                    _selectedOutfit);

            _status.text =
                _state.IsReady
                    ? "UNREADY TO CHANGE OUTFIT."
                    : !selectedOwned
                        ? "LOCKED // PURCHASE THIS OUTFIT IN STORE."
                        : _state.CharacterOutfitId == _selectedOutfit
                            ? "EQUIPPED"
                            : _selectedOutfit == 0
                                ? "DEFAULT // CLICK TO EQUIP"
                                : "OWNED // CLICK TO EQUIP";
        }

        private static string GetCharacterName(
            int characterId)
        {
            foreach (var item
                     in CharacterShopAssets.Items)
            {
                if (item.CharacterId
                    == characterId)
                {
                    return item.Name;
                }
            }

            return "CHARACTER";
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
                new Vector2(0f, 1f);

            rect.anchorMax =
                new Vector2(0f, 1f);

            rect.pivot =
                new Vector2(0f, 1f);

            rect.anchoredPosition =
                new Vector2(x, -y);

            rect.sizeDelta =
                new Vector2(width, height);
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
