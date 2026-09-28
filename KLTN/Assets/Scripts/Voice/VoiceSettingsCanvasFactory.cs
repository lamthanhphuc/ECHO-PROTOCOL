using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.Voice
{
    /// <summary>Builds the editable voice settings canvas used by the runtime and prefab builder.</summary>
    public static class VoiceSettingsCanvasFactory
    {
        private static readonly Color Background = Hex("080C10");
        private static readonly Color Surface = Hex("111A20");
        private static readonly Color Raised = Hex("1B2A31");
        private static readonly Color Border = Hex("43545A");
        private static readonly Color Accent = Hex("C34243");
        private static readonly Color Text = Hex("ECF2F1");
        private static readonly Color Muted = Hex("9BAAAC");

        public static GameObject Create()
        {
            var root = new GameObject("VoiceSettingsCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 260;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var hud = Panel("HudButton", root.transform, 32, 30, 230, 44, Surface);
            Stroke("HudAccent", hud, 0, 0, 4, 44, Accent);
            Button(hud, "MIC OFF  ·  SETTINGS F8", 16);

            var overlay = Panel("Overlay", root.transform, 0, 0, 0, 0, new Color(0, 0, 0, 0.78f));
            Stretch(overlay);
            var card = Panel("Window", overlay, 0, 0, 800, 930, Background);
            Center(card);
            card.GetComponent<Image>().raycastTarget = true;
            Outline(card.gameObject);
            Stroke("TopRule", card, 0, 0, 800, 5, Accent);
            Label("Eyebrow", card, 32, 18, 500, 22, "ECHO PROTOCOL  /  AUDIO", 14, Accent, FontStyles.Bold);
            Label("Title", card, 32, 43, 600, 48, "VOICE CHAT", 34, Text, FontStyles.Bold);
            Label("Subtitle", card, 32, 91, 680, 24, "Talk to players in your room", 16, Muted);
            var close = Panel("CloseButton", card, 730, 38, 38, 38, Raised);
            Button(close, "X", 20);
            Stroke("HeaderRule", card, 32, 124, 736, 1, Border);

            var statusCard = Panel("StatusCard", card, 32, 140, 736, 62, Surface);
            Stroke("StatusAccent", statusCard, 0, 0, 4, 62, Accent);
            Label("StatusTitle", statusCard, 16, 8, 180, 20, "VOICE CONNECTION", 12, Muted, FontStyles.Bold);
            Label("StatusText", statusCard, 16, 29, 542, 25, "Waiting to join a room", 16, Text);
            var retry = Panel("RetryButton", statusCard, 580, 13, 140, 36, Raised);
            Button(retry, "RECONNECT", 13);

            Label("InputTitle", card, 32, 222, 736, 29, "1.  TURN YOUR MICROPHONE ON OR OFF", 20, Text, FontStyles.Bold);
            Label("InputHint", card, 32, 252, 736, 22, "Press V once to talk. Press it again to mute.", 16, Muted);
            var mic = Panel("MicButton", card, 32, 284, 736, 65, Raised);
            Stroke("Accent", mic, 0, 0, 6, 65, Accent);
            Button(mic, "TURN MICROPHONE ON", 22);
            var key = Panel("KeyButton", card, 32, 360, 736, 38, Surface);
            Button(key, "CHANGE MIC SHORTCUT  ·  V", 14);

            Stroke("InputRule", card, 32, 418, 736, 1, Border);
            Label("DeviceTitle", card, 32, 435, 570, 29, "2.  CHOOSE YOUR MICROPHONE", 20, Text, FontStyles.Bold);
            var refresh = Panel("RefreshButton", card, 640, 434, 128, 32, Surface);
            Button(refresh, "REFRESH LIST", 13);
            Label("DeviceText", card, 32, 472, 736, 28, "No microphone selected", 16, Muted);
            ScrollArea("Devices", card, 32, 508, 736, 107);
            Label("MicLevelTitle", card, 32, 625, 132, 25, "INPUT LEVEL", 13, Muted, FontStyles.Bold);
            var level = Panel("MicLevelTrack", card, 166, 632, 410, 12, Surface);
            StretchFill("MicLevelFill", level, Accent);
            Label("MicLevelText", card, 586, 622, 62, 28, "0%", 15, Text, FontStyles.Bold, TextAlignmentOptions.Right);
            var test = Panel("TestButton", card, 658, 620, 110, 35, Surface);
            Button(test, "TEST MIC", 13);

            Stroke("OutputRule", card, 32, 677, 736, 1, Border);
            Label("OutputTitle", card, 32, 695, 736, 29, "3.  HEAR OTHER PLAYERS", 20, Text, FontStyles.Bold);
            Label("VolumeTitle", card, 32, 728, 570, 25, "Voice volume", 16, Muted);
            Label("VolumeText", card, 688, 728, 80, 25, "100%", 16, Text, FontStyles.Bold, TextAlignmentOptions.Right);
            VolumeSlider("VolumeSlider", card, 32, 760, 736, 22);
            Label("TeamTitle", card, 32, 793, 736, 22, "Click a player below to mute or unmute them", 14, Muted);
            ScrollArea("TeamList", card, 32, 821, 736, 74);

            Stroke("FooterRule", card, 32, 908, 736, 1, Border);
            Label("Footer", card, 32, 910, 736, 18, "F8 / ESC  CLOSE     ·     YOUR MIC DOES NOT TURN ON AUTOMATICALLY", 12, Muted);
            overlay.gameObject.SetActive(false);
            return root;
        }

        public static Button AddListButton(Transform parent, string name, string title, bool selected)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<Image>().color = selected ? Accent : Raised;
            row.GetComponent<LayoutElement>().preferredHeight = 34;
            var button = row.GetComponent<Button>();
            button.targetGraphic = row.GetComponent<Image>();
            var label = Label("Label", row.transform, 12, 0, 350, 34, title, 14, Text);
            label.anchorMin = new Vector2(0, 0.5f);
            label.anchorMax = new Vector2(1, 0.5f);
            label.sizeDelta = new Vector2(-24, 34);
            return button;
        }

        private static RectTransform Panel(string name, Transform parent, float x, float y, float w, float h, Color color)
        {
            var rect = Rect(name, parent, x, y, w, h);
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        private static RectTransform Stroke(string name, Transform parent, float x, float y, float w, float h, Color color)
        {
            var rect = Panel(name, parent, x, y, w, h, color);
            rect.GetComponent<Image>().raycastTarget = false;
            return rect;
        }

        private static RectTransform Label(string name, Transform parent, float x, float y, float w, float h,
            string content, int size, Color color, FontStyles style = FontStyles.Normal,
            TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            var rect = Rect(name, parent, x, y, w, h);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.text = content;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = align;
            label.verticalAlignment = VerticalAlignmentOptions.Middle;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return rect;
        }

        private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var owner = new GameObject(name, typeof(RectTransform));
            var rect = owner.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        private static void Center(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Button(RectTransform rect, string title, int size)
        {
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.85f, 0.9f, 0.9f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            button.colors = colors;
            var label = Label("Label", rect, 0, 0, 0, 0, title, size, Text, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(label);
        }

        private static void Outline(GameObject owner)
        {
            var outline = owner.AddComponent<Outline>();
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(1, -1);
        }

        private static void ScrollArea(string name, Transform parent, float x, float y, float w, float h)
        {
            var area = Panel(name, parent, x, y, w, h, Surface);
            var viewport = Rect("Viewport", area, 8, 8, w - 16, h - 16);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport, 0, 0, w - 16, 0);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.sizeDelta = new Vector2(0, 0);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
        }

        private static void StretchFill(string name, Transform parent, Color color)
        {
            var rect = Stroke(name, parent, 0, 0, 0, 0, color);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.GetComponent<Image>().type = Image.Type.Filled;
            rect.GetComponent<Image>().fillMethod = Image.FillMethod.Horizontal;
            rect.GetComponent<Image>().fillAmount = 0;
        }

        private static void VolumeSlider(string name, Transform parent, float x, float y, float w, float h)
        {
            var track = Panel(name, parent, x, y, w, h, Surface);
            var fill = Stroke("Fill", track, 0, 0, 0, 0, Accent);
            Stretch(fill);
            var handle = Panel("Handle", track, 0, 0, 14, h, Text);
            var slider = track.gameObject.AddComponent<Slider>();
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = 1;
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var result);
            return result;
        }
    }
}
