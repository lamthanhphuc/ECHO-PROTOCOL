using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI
{
    public static class SettingsMenuWidgets
    {
        public static readonly Color Ink = new Color(0.025f, 0.025f, 0.027f, 0.95f);
        public static readonly Color Accent = new Color(0.76f, 0.20f, 0.20f);
        public static readonly Color Foreground = new Color(0.91f, 0.89f, 0.85f);
        public static readonly Color Muted = new Color(0.59f, 0.58f, 0.56f);
        public static readonly Color Rule = new Color(0.32f, 0.29f, 0.27f, 0.5f);

        public static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public static RectTransform Panel(string name, Transform parent, float x, float y, float w, float h, Color color)
        {
            var rect = Rect(name, parent, x, y, w, h);
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        public static RectTransform Frame(string name, Transform parent, float x, float y, float w, float h, bool accent = false)
        {
            var rect = Rect(name, parent, x, y, w, h);
            rect.gameObject.AddComponent<HorrorFrameGraphic>().Configure(accent ? new Color(0.18f, 0.025f, 0.026f, 0.95f) : Ink, accent);
            return rect;
        }

        public static void Line(string name, Transform parent, float x, float y, float w, Color? tint = null)
        {
            Panel(name, parent, x, y, w, 1, tint ?? Rule).GetComponent<Image>().raycastTarget = false;
        }

        public static TMP_Text Label(string name, Transform parent, float x, float y, float w, float h,
            string text, float size = 21, Color? tint = null, TextAlignmentOptions align = TextAlignmentOptions.Left,
            bool bold = false)
        {
            var rect = Rect(name, parent, x, y, w, h);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.text = text;
            label.fontSize = size;
            label.color = tint ?? Foreground;
            label.alignment = align;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return label;
        }

        public static Button Button(string name, Transform parent, float x, float y, float w, float h,
            string title, bool accent = false, float fontSize = 19)
        {
            var rect = Frame(name, parent, x, y, w, h, accent);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<HorrorFrameGraphic>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.35f, 1.2f, 1.2f);
            colors.pressedColor = new Color(0.75f, 0.7f, 0.7f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f);
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Label("Label", rect, 8, 0, w - 16, h, title, fontSize, accent ? new Color(1, 0.54f, 0.51f) : Foreground,
                TextAlignmentOptions.Center, true);
            return button;
        }

        public static Toggle Toggle(string name, Transform parent, float x, float y)
        {
            var root = Rect(name, parent, x, y, 145, 34);
            root.gameObject.AddComponent<Image>().color = Color.clear;
            var track = Icon("Track", root, 0, 5, 54, 24, SettingsIcon.Pill, new Color(0.21f, 0.20f, 0.20f));
            var on = Icon("On", track.transform, 0, 0, 54, 24, SettingsIcon.Pill, Accent);
            var offDot = Icon("OffDot", track.transform, 4, 4, 16, 16, SettingsIcon.Dot, Muted);
            var dot = Icon("Dot", on.transform, 34, 4, 16, 16, SettingsIcon.Dot, Foreground);
            on.transform.SetAsLastSibling();
            var toggle = root.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = track;
            toggle.graphic = on;
            toggle.toggleTransition = UnityEngine.UI.Toggle.ToggleTransition.None;
            toggle.navigation = new Navigation { mode = Navigation.Mode.None };
            Label("Value", root, 67, 0, 78, 34, "Tắt", 19);
            toggle.onValueChanged.AddListener(value =>
            {
                var text = root.Find("Value").GetComponent<TMP_Text>();
                text.text = value ? "Bật" : "Tắt";
                dot.gameObject.SetActive(value);
                offDot.gameObject.SetActive(!value);
            });
            toggle.isOn = false;
            dot.gameObject.SetActive(false);
            return toggle;
        }

        public static Slider Slider(string name, Transform parent, float x, float y, float width, float min = 0, float max = 1)
        {
            var root = Rect(name, parent, x, y, width, 30);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            Panel("Track", root, 0, 12, width, 6, new Color(0.20f, 0.21f, 0.22f)).GetComponent<Image>().raycastTarget = false;
            var fillArea = Rect("FillArea", root, 0, 12, width, 6);
            var fill = Panel("Fill", fillArea, 0, 0, width, 6, Accent);
            Stretch(fill);
            var handleArea = Rect("HandleArea", root, 8, 8, width - 16, 14);
            var handle = Icon("Handle", handleArea, 0, 0, 14, 14, SettingsIcon.Dot, Foreground);
            handle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            handle.rectTransform.sizeDelta = new Vector2(14, 0);
            handle.rectTransform.anchoredPosition = Vector2.zero;
            var slider = root.gameObject.AddComponent<Slider>();
            slider.targetGraphic = handle;
            slider.fillRect = fill;
            slider.handleRect = handle.rectTransform;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = Mathf.Clamp(1, min, max);
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            return slider;
        }

        public static RectTransform Card(string name, Transform parent, float x, float y, float w, float h,
            string title, string symbol)
        {
            var card = Frame(name, parent, x, y, w, h);
            var kind = symbol == "◉" ? SettingsIcon.Microphone : symbol == "◐" ? SettingsIcon.Mouse
                : symbol == "♪" ? SettingsIcon.Audio : symbol == "▣" ? SettingsIcon.Monitor
                : symbol == "◌" ? SettingsIcon.Team : SettingsIcon.Keyboard;
            Icon("Icon", card, 24, 12, 34, 34, kind, Foreground);
            Label("Heading", card, 76, 10, w - 106, 38, title, 27);
            Line("Rule", card, 23, 53, w - 46);
            Line("Accent", card, 23, 53, 86, Accent);
            return card;
        }

        public static SettingsIconGraphic Icon(string name, Transform parent, float x, float y, float w, float h,
            SettingsIcon kind, Color? tint = null)
        {
            var icon = Rect(name, parent, x, y, w, h).gameObject.AddComponent<SettingsIconGraphic>();
            icon.Configure(kind, tint ?? Muted);
            return icon;
        }

        public static void ScrollArea(string name, Transform parent, float x, float y, float w, float h)
        {
            var area = Panel(name, parent, x, y, w, h, new Color(0.01f, 0.01f, 0.01f, 0.3f));
            var viewport = Rect("Viewport", area, 8, 8, w - 16, h - 16);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport, 0, 0, w - 16, 0);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6;
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25;
        }
    }
}
