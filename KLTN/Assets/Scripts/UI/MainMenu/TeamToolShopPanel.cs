using System;
using System.Collections.Generic;
using EchoProtocol.Api;
using EchoProtocol.Auth;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.MainMenu
{
    [DisallowMultipleComponent]
    public sealed class TeamToolShopPanel : MonoBehaviour
    {
        [Serializable] private sealed class CatalogResponse
        {
            public bool success;
            public string message;
            public CatalogData data;
        }

        [Serializable] private sealed class CatalogData
        {
            public ShopItem[] items;
        }

        [Serializable] private sealed class ShopItem
        {
            public string itemId;
            public string name;
            public string description;
            public string assetReference;
            public int price;
        }

        [Serializable] private sealed class PurchaseRequest
        {
            public string itemId;
            public string idempotencyKey;
        }

        [Serializable] private sealed class PurchaseResponse
        {
            public bool success;
            public string message;
        }

        [Serializable] private sealed class InventoryResponse
        {
            public bool success;
            public InventoryData data;
        }

        [Serializable] private sealed class InventoryData
        {
            public OwnedItem[] items;
        }

        [Serializable] private sealed class OwnedItem
        {
            public string itemId;
        }

        private RectTransform _panel;
        private RectTransform _list;
        private Text _status;
        private MainMenuProfileController _profile;
        private bool _purchasing;
        private readonly HashSet<string> _owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private GameObject[] _cosmeticCards;

        public void Open(MainMenuProfileController profile)
        {
            _profile = profile;
            if (_panel == null) Build();
            SetToolView(true);
            LoadCatalog();
        }

#if UNITY_EDITOR
        // Allows isolated Editor previews to use the same layout without sending API requests.
        public void PreviewLayout()
        {
            if (_panel == null) Build();
            if (_panel == null) return;
            SetToolView(true);
            ShowItems(null);
        }
#endif

        private void Build()
        {
            var window = transform.Find("Window");
            if (window == null) { Debug.LogError("[TeamToolShopPanel] Store Terminal Window not found.", this); return; }
            _cosmeticCards = new[]
            {
                window.Find("HazmatCard")?.gameObject,
                window.Find("FlashlightCard")?.gameObject,
                window.Find("BackpackCard")?.gameObject,
                window.Find("GasMaskCard")?.gameObject
            };
            foreach (var button in window.GetComponentsInChildren<Button>(true))
            {
                var caption = button.GetComponentInChildren<Text>(true);
                if (caption == null) continue;
                if (caption.text == "EQUIPMENT" || caption.text == "ALL") button.onClick.AddListener(() => SetToolView(true));
                if (caption.text == "COSMETICS" || caption.text == "CHARACTER") button.onClick.AddListener(() => SetToolView(false));
            }
            // The footer owns the bottom 56 pixels; content starts above it with a 16px gap.
            _panel = Box("TeamTools", window, Vector2.zero, Vector2.zero);
            Stretch(_panel, new Vector2(200f, 72f), new Vector2(-32f, -100f));
            _panel.GetComponent<Image>().color = Color.clear;
            _panel.GetComponent<Image>().raycastTarget = false;
            _status = Label("Status", _panel, "Loading...", 13, Vector2.zero, Vector2.zero);
            var statusRect = _status.rectTransform;
            statusRect.anchorMin = Vector2.zero;
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = new Vector2(0f, 20f);
            _status.horizontalOverflow = HorizontalWrapMode.Wrap;
            _status.resizeTextForBestFit = true;
            _status.resizeTextMinSize = 10;
            _status.resizeTextMaxSize = 13;
            var listBox = new GameObject("Items", typeof(RectTransform));
            listBox.transform.SetParent(_panel, false);
            _list = (RectTransform)listBox.transform;
            Stretch(_list, new Vector2(0f, 24f), Vector2.zero);
            var layout = listBox.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;

            var close = window.Find("CloseButton") as RectTransform;
            if (close != null)
            {
                close.anchorMin = Vector2.zero;
                close.anchorMax = new Vector2(1f, 0f);
                close.pivot = new Vector2(0.5f, 0f);
                close.offsetMin = new Vector2(32f, 20f);
                close.offsetMax = new Vector2(-32f, 56f);
                close.SetAsLastSibling();
            }

            ShowItems(null);
        }

        private void SetToolView(bool visible)
        {
            if (_panel != null) _panel.gameObject.SetActive(visible);
            if (_cosmeticCards == null) return;
            foreach (var card in _cosmeticCards) if (card != null) card.SetActive(!visible);
        }

        private void LoadCatalog()
        {
            var runtime = AuthRuntime.EnsureExists();
            if (_panel == null) return;
            if (runtime == null || runtime.Client == null) { ShowItems(null); return; }
            _status.text = "Loading team tools...";
            runtime.Client.GetJson<CatalogResponse>(ApiEndpoints.ShopTeamTools, true, result =>
            {
                if (this == null) return;
                if (!result.IsSuccess || result.Data == null || !result.Data.success || result.Data.data == null)
                {
                    ShowItems(null);
                    return;
                }
                var items = result.Data.data.items;
                runtime.Client.GetJson<InventoryResponse>(ApiEndpoints.InventoryMe, true, inventory =>
                {
                    if (this == null) return;
                    _owned.Clear();
                    if (inventory.IsSuccess && inventory.Data != null && inventory.Data.success
                        && inventory.Data.data != null && inventory.Data.data.items != null)
                        foreach (var owned in inventory.Data.data.items)
                            if (owned != null && !string.IsNullOrEmpty(owned.itemId)) _owned.Add(owned.itemId);
                    ShowItems(items);
                });
            });
        }

        private void ShowItems(ShopItem[] items)
        {
            for (int i = _list.childCount - 1; i >= 0; i--)
            {
                var child = _list.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
            int available = 0;
            foreach (var expected in TeamToolShopAssets.Items)
            {
                ShopItem item = null;
                if (items != null)
                    foreach (var candidate in items)
                        if (candidate != null && candidate.itemId == expected.ItemId) { item = candidate; break; }
                if (item != null) available++;
                var card = Box("Tool_" + expected.Name, _list, Vector2.zero, Vector2.zero);
                card.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                card.GetComponent<Image>().raycastTarget = false;

                var imageRegion = new GameObject("ImageRegion", typeof(RectTransform));
                imageRegion.transform.SetParent(card, false);
                Stretch((RectTransform)imageRegion.transform, new Vector2(8f, 68f), new Vector2(-8f, -8f));
                var preview = new GameObject("PrefabPreview", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                preview.transform.SetParent(imageRegion.transform, false);
                var thumbnail = preview.GetComponent<RawImage>();
                thumbnail.texture = Resources.Load<Texture2D>(expected.ThumbnailResource);
                thumbnail.raycastTarget = false;
                thumbnail.color = thumbnail.texture != null ? Color.white : Color.clear;
                var fit = preview.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = 1f;

                var name = Label("Name", card, expected.Name, 14, Vector2.zero, Vector2.zero);
                BottomBand(name.rectTransform, 43f, 22f, 4f);
                name.alignment = TextAnchor.MiddleCenter;
                name.resizeTextForBestFit = true;
                name.resizeTextMinSize = 11;
                name.resizeTextMaxSize = 14;
                var button = Button("Buy", card, item != null ? $"BUY · {item.price} CR" : "UNAVAILABLE",
                    Vector2.zero, Vector2.zero);
                BottomBand((RectTransform)button.transform, 8f, 30f, 8f);
                button.GetComponent<Image>().color = new Color(0.55f, 0.025f, 0.025f, 1f);
                var caption = button.GetComponentInChildren<Text>();
                Stretch(caption.rectTransform, new Vector2(3f, 0f), new Vector2(-3f, 0f));
                caption.fontSize = 13;
                caption.resizeTextForBestFit = true;
                caption.resizeTextMinSize = 10;
                caption.resizeTextMaxSize = 13;
                if (item == null) button.interactable = false;
                else if (_owned.Contains(item.itemId))
                {
                    button.interactable = false;
                    button.GetComponentInChildren<Text>().text = "OWNED";
                }
                var selected = item;
                button.onClick.AddListener(() => Purchase(selected, button));
            }
            _status.text = available == 0 ? "Items are currently unavailable for purchase." : "Select a team tool to buy.";
        }

        private void Purchase(ShopItem item, Button button)
        {
            if (_purchasing || item == null) return;
            var runtime = AuthRuntime.EnsureExists();
            if (runtime == null || runtime.Client == null) { _status.text = "Shop unavailable."; return; }
            _purchasing = true;
            button.interactable = false;
            _status.text = "Purchasing " + item.name + "...";
            runtime.Client.PostJson<PurchaseRequest, PurchaseResponse>(ApiEndpoints.ShopPurchase,
                new PurchaseRequest { itemId = item.itemId, idempotencyKey = Guid.NewGuid().ToString("N") }, true,
                result =>
                {
                    if (this == null) return;
                    _purchasing = false;
                    if (!result.IsSuccess || result.Data == null || !result.Data.success)
                    {
                        if (button != null) button.interactable = true;
                        _status.text = result.Message ?? "Purchase failed.";
                        return;
                    }
                    if (button != null) button.GetComponentInChildren<Text>().text = "OWNED";
                    _owned.Add(item.itemId);
                    _status.text = item.name + " purchased.";
                    runtime.AuthService.GetCurrentUser(_ => { if (_profile != null) _profile.UpdateCreditsUI(); });
                });
        }

        private static RectTransform Box(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var image = go.GetComponent<Image>();
            image.color = new Color(0.035f, 0.065f, 0.08f, 0.96f);
            return rect;
        }

        private static Text Label(string name, Transform parent, string value, int fontSize, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        private static Button Button(string name, Transform parent, string caption, Vector2 size, Vector2 position)
        {
            var rect = Box(name, parent, size, position);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            var label = Label("Caption", rect, caption, 17, size, Vector2.zero);
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            return button;
        }

        private static void Stretch(RectTransform rect, Vector2 minimum, Vector2 maximum)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = minimum;
            rect.offsetMax = maximum;
        }

        private static void BottomBand(RectTransform rect, float bottom, float height, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(inset, bottom);
            rect.offsetMax = new Vector2(-inset, bottom + height);
        }
    }
}
