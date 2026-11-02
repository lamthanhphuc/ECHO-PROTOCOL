using EchoProtocol.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EchoProtocol.UI
{
    public enum SettingsIcon
    {
        Gear, Microphone, Mouse, Audio, Keyboard, Monitor, Team, Interact, Run, Crouch,
        Flashlight, Inventory, Up, Down, Left, Right, Play, Exit, Dot, Pill, Shop, Web, Profile, Trophy, Radio, Warning, Age18
    }

    /// <summary>Small vector icons, independent of installed fonts and texture resolution.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SettingsIconGraphic : MaskableGraphic
    {
        [SerializeField] private SettingsIcon _icon;
        private VertexHelper _mesh;
        private Rect _bounds;

        public void Configure(SettingsIcon icon, Color tint)
        {
            _icon = icon;
            color = tint;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            _mesh = mesh;
            _bounds = GetPixelAdjustedRect();
            switch (_icon)
            {
                case SettingsIcon.Age18:
                    Arc(50, 50, 44, 0, 360, 6);
                    Stroke(19, 65, 29, 75, 6); Stroke(29, 75, 29, 29, 6); Stroke(19, 29, 39, 29, 5);
                    Arc(55, 63, 11, 0, 360, 5); Arc(55, 39, 12, 0, 360, 5);
                    Stroke(72, 54, 90, 54, 5); Stroke(81, 45, 81, 63, 5);
                    break;
                case SettingsIcon.Warning:
                    Stroke(50, 92, 6, 12, 6); Stroke(6, 12, 94, 12, 6); Stroke(94, 12, 50, 92, 6);
                    Stroke(50, 63, 50, 38, 7); Stroke(50, 26, 50, 22, 7); break;
                case SettingsIcon.Gear:
                    Arc(50, 50, 28, 0, 360, 9);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4;
                        Stroke(50 + Mathf.Cos(a) * 30, 50 + Mathf.Sin(a) * 30, 50 + Mathf.Cos(a) * 43, 50 + Mathf.Sin(a) * 43, 11);
                    }
                    break;
                case SettingsIcon.Microphone:
                    Arc(50, 68, 16, 0, 180); Arc(50, 46, 16, 180, 360);
                    Stroke(34, 46, 34, 68); Stroke(66, 46, 66, 68);
                    Arc(50, 44, 28, 180, 360); Stroke(22, 44, 22, 56); Stroke(78, 44, 78, 56);
                    Stroke(50, 16, 50, 5); Stroke(32, 5, 68, 5); break;
                case SettingsIcon.Mouse:
                    Arc(50, 65, 29, 0, 180); Arc(50, 34, 29, 180, 360);
                    Stroke(21, 34, 21, 65); Stroke(79, 34, 79, 65);
                    Stroke(21, 59, 79, 59); Stroke(50, 59, 50, 94); Stroke(50, 68, 50, 77, 8); break;
                case SettingsIcon.Audio:
                    Polygon(new[] { new Vector2(12, 37), new Vector2(28, 37), new Vector2(52, 17), new Vector2(52, 83), new Vector2(28, 63), new Vector2(12, 63) });
                    Arc(51, 50, 25, -55, 55); Arc(51, 50, 40, -55, 55); break;
                case SettingsIcon.Keyboard:
                    Box(6, 19, 88, 61);
                    for (int row = 0; row < 3; row++)
                        for (int col = 0; col < 6; col++) Stroke(17 + col * 13, 69 - row * 14, 22 + col * 13, 69 - row * 14, 5);
                    Stroke(26, 28, 74, 28, 5); break;
                case SettingsIcon.Monitor:
                    Box(7, 29, 86, 57); Stroke(50, 29, 50, 12); Stroke(28, 12, 72, 12); break;
                case SettingsIcon.Team:
                    Disc(50, 70, 13); Disc(21, 62, 10); Disc(79, 62, 10);
                    Arc(50, 30, 21, 0, 180, 7); Arc(19, 23, 14, 0, 150, 6); Arc(81, 23, 14, 30, 180, 6);
                    Stroke(29, 30, 29, 15); Stroke(71, 30, 71, 15); Stroke(29, 15, 71, 15); break;
                case SettingsIcon.Interact:
                    Stroke(34, 44, 34, 86); Stroke(46, 44, 46, 94); Stroke(58, 44, 58, 91); Stroke(70, 44, 70, 78);
                    Stroke(34, 44, 18, 59); Stroke(18, 59, 13, 50); Stroke(13, 50, 37, 16);
                    Stroke(37, 16, 66, 16); Stroke(66, 16, 76, 43); Stroke(76, 43, 76, 62); break;
                case SettingsIcon.Run:
                    Disc(63, 85, 11); Stroke(56, 68, 43, 44, 8); Stroke(55, 65, 77, 54, 6);
                    Stroke(53, 67, 34, 69, 6); Stroke(34, 69, 21, 52, 6); Stroke(43, 44, 65, 29, 7);
                    Stroke(65, 29, 61, 8, 7); Stroke(43, 44, 29, 21, 7); Stroke(29, 21, 9, 20, 7); break;
                case SettingsIcon.Crouch:
                    Disc(60, 78, 11); Stroke(56, 61, 36, 43, 8); Stroke(53, 57, 76, 40, 6);
                    Stroke(36, 43, 64, 29, 8); Stroke(64, 29, 47, 12, 7); Stroke(47, 12, 76, 12, 6);
                    Stroke(36, 43, 23, 25, 7); Stroke(23, 25, 23, 10, 7); break;
                case SettingsIcon.Flashlight:
                    Polygon(new[] { new Vector2(15, 24), new Vector2(26, 13), new Vector2(60, 47), new Vector2(49, 58) });
                    Polygon(new[] { new Vector2(44, 63), new Vector2(65, 42), new Vector2(73, 57), new Vector2(59, 71) });
                    Stroke(70, 81, 78, 89, 3); Stroke(80, 71, 91, 76, 3); Stroke(58, 86, 61, 96, 3); break;
                case SettingsIcon.Inventory:
                    Box(19, 16, 62, 59); Arc(50, 75, 17, 0, 180); Box(31, 27, 38, 22); break;
                case SettingsIcon.Up: Arrow(0); break;
                case SettingsIcon.Down: Arrow(180); break;
                case SettingsIcon.Left: Arrow(90); break;
                case SettingsIcon.Right: Arrow(-90); break;
                case SettingsIcon.Play:
                    Polygon(new[] { new Vector2(24, 13), new Vector2(86, 50), new Vector2(24, 87) }); break;
                case SettingsIcon.Exit:
                    Stroke(45, 88, 17, 88); Stroke(17, 88, 17, 12); Stroke(17, 12, 45, 12);
                    Stroke(38, 50, 91, 50); Stroke(73, 68, 91, 50); Stroke(73, 32, 91, 50); break;
                case SettingsIcon.Shop:
                    // Compact sci-fi storefront / equipment bag.
                    Box(18, 20, 64, 52);
                    Arc(50, 72, 18, 0, 180, 6);
                    Stroke(18, 58, 82, 58, 4);
                    Stroke(37, 20, 37, 58, 3);
                    Stroke(63, 20, 63, 58, 3);
                    break;

                case SettingsIcon.Web:
                    // Globe. Pure vector so it stays sharp
                    // at every UI resolution.
                    Arc(50, 50, 40, 0, 360, 6);
                    Stroke(11, 50, 89, 50, 4);
                    Stroke(50, 10, 50, 90, 4);
                    Stroke(23, 35, 77, 35, 3);
                    Stroke(23, 65, 77, 65, 3);
                    break;
                case SettingsIcon.Profile:
                    Arc(50, 50, 46, 0, 360, 3);
                    Disc(50, 63, 14);
                    Arc(50, 17, 28, 0, 180, 8);
                    Stroke(22, 17, 78, 17, 8);
                    break;
                case SettingsIcon.Trophy:
                    Polygon(new[] { new Vector2(25, 82), new Vector2(75, 82),
                        new Vector2(69, 55), new Vector2(59, 43), new Vector2(41, 43),
                        new Vector2(31, 55) });
                    Arc(23, 68, 17, 90, 270, 5);
                    Arc(77, 68, 17, -90, 90, 5);
                    Stroke(50, 43, 50, 22, 8);
                    Stroke(35, 20, 65, 20, 7);
                    Stroke(28, 12, 72, 12, 6);
                    break;
                case SettingsIcon.Radio:
                    Disc(50, 70, 5);
                    Polygon(new[] { new Vector2(49, 63), new Vector2(35, 13),
                        new Vector2(65, 13), new Vector2(51, 63) });
                    Stroke(25, 13, 75, 13, 4);
                    Arc(50, 70, 22, 35, 145, 4);
                    Arc(50, 70, 38, 35, 145, 3);
                    Stroke(18, 33, 30, 33, 3);
                    Stroke(70, 45, 82, 45, 3);
                    break;
                case SettingsIcon.Dot: Disc(50, 50, 48); break;
                case SettingsIcon.Pill:
                    float radius = _bounds.height / _bounds.width * 50;
                    var points = new Vector2[34];
                    for (int i = 0; i <= 16; i++)
                    {
                        float angle = (-90 + i * 180f / 16) * Mathf.Deg2Rad;
                        points[i] = new Vector2(100 - radius + Mathf.Cos(angle) * radius, 50 + Mathf.Sin(angle) * 50);
                        points[17 + i] = new Vector2(radius - Mathf.Cos(angle) * radius, 50 - Mathf.Sin(angle) * 50);
                    }
                    Polygon(points); break;
            }
        }

        private void Arrow(float angle)
        {
            var p = new[] { new Vector2(0, -34), new Vector2(0, 32), new Vector2(-22, 8), new Vector2(0, 32), new Vector2(22, 8) };
            var rotation = Quaternion.Euler(0, 0, angle);
            for (int i = 0; i < p.Length; i++) p[i] = (Vector2)(rotation * p[i]) + new Vector2(50, 50);
            for (int i = 1; i < p.Length; i++) Stroke(p[i - 1].x, p[i - 1].y, p[i].x, p[i].y, 7);
        }
        private void Box(float x, float y, float width, float height)
        {
            Stroke(x, y, x + width, y); Stroke(x + width, y, x + width, y + height);
            Stroke(x + width, y + height, x, y + height); Stroke(x, y + height, x, y);
        }
        private void Arc(float x, float y, float radius, float start, float end, float thickness = 5)
        {
            for (int i = 0; i < 24; i++)
            {
                float a = Mathf.Lerp(start, end, i / 24f) * Mathf.Deg2Rad;
                float b = Mathf.Lerp(start, end, (i + 1) / 24f) * Mathf.Deg2Rad;
                Stroke(x + Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius, x + Mathf.Cos(b) * radius, y + Mathf.Sin(b) * radius, thickness);
            }
        }
        private void Disc(float x, float y, float radius)
        {
            var points = new Vector2[32];
            for (int i = 0; i < points.Length; i++)
            {
                float a = i * Mathf.PI * 2 / points.Length;
                points[i] = new Vector2(x + Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius);
            }
            Polygon(points);
        }
        private void Stroke(float x1, float y1, float x2, float y2, float thickness = 5)
        {
            var a = new Vector2(x1, y1); var b = new Vector2(x2, y2);
            var normal = new Vector2(-(b - a).y, (b - a).x).normalized * thickness * 0.5f;
            Polygon(new[] { a - normal, b - normal, b + normal, a + normal });
        }
        private void Polygon(Vector2[] points)
        {
            int start = _mesh.currentVertCount;
            foreach (var point in points)
                _mesh.AddVert(new Vector2(_bounds.xMin + point.x * _bounds.width / 100, _bounds.yMin + point.y * _bounds.height / 100), color, Vector2.zero);
            for (int i = 1; i < points.Length - 1; i++) _mesh.AddTriangle(start, start + i, start + i + 1);
        }
    }
}

namespace EchoProtocol.UI
{
    // One visual and spacing rule for the Main Menu, Lobby and Store headers.
    public static class UtilityBarStyle
    {
        public const float ButtonSize = 48f;
        public const float Gap = 8f;
        public const float CreditsWidth = 178f;
        public const float ToolbarWidth = CreditsWidth + 4f * (ButtonSize + Gap);
        public const float RightMargin = 24f;
        public const float TopMargin = 20f;
        public const string WebsiteUrl = "https://web.3.1.238.6.sslip.io/";

        private static readonly Color Background = new(0.035f, 0.047f, 0.051f, 0.96f);
        private static readonly Color Border = new(0.22f, 0.26f, 0.27f, 0.92f);
        private static readonly Color IconColor = new(0.835f, 0.839f, 0.82f, 1f);
        private static readonly Color CreditsColor = IndustrialUiStyle.Text;
        private static Font _creditsFont;

        public static void MainMenu(RectTransform root)
        {
            if (root == null) return;
            Place(root.Find("CoinContainer") as RectTransform, 0f, CreditsWidth);
            Place(root.Find("ProfileButton") as RectTransform, 186f, ButtonSize);
            Place(root.Find("WebButton") as RectTransform, 298f, ButtonSize);
            Place(root.Find("ShopButton") as RectTransform, 354f, ButtonSize);
            Button(root.Find("ProfileButton")?.GetComponent<Button>(), SettingsIcon.Profile);
            Button(root.Find("ShopButton")?.GetComponent<Button>(), SettingsIcon.Shop);
            Button(root.Find("WebButton")?.GetComponent<Button>(), SettingsIcon.Web);
            Credits(root.Find("CoinContainer") as RectTransform);
            PositionAtSafeTopRight(root, ToolbarWidth);
        }

        public static void Lobby(RectTransform root)
        {
            if (root == null) return;
            Place(root.Find("CreditsButton") as RectTransform, 0f, CreditsWidth);
            EnsureButton(root, "ProfileButton", SettingsIcon.Profile, 186f);
            EnsureButton(root, "LeaderboardButton", SettingsIcon.Trophy, 242f);
            Place(root.Find("WebButton") as RectTransform, 298f, ButtonSize);
            EnsureButton(root, "SettingsButton", SettingsIcon.Gear, 354f);
            Button(root.Find("WebButton")?.GetComponent<Button>(), SettingsIcon.Web);
            Credits(root.Find("CreditsButton") as RectTransform);
            PositionAtSafeTopRight(root, ToolbarWidth);
        }

        public static Button EnsureButton(Transform parent, string name, SettingsIcon icon,
            float x)
        {
            var child = parent.Find(name) as RectTransform;
            if (child == null)
            {
                child = new GameObject(name, typeof(RectTransform), typeof(Image),
                    typeof(Button)).GetComponent<RectTransform>();
                child.SetParent(parent, false);
            }
            Place(child, x, ButtonSize);
            var button = child.GetComponent<Button>() ?? child.gameObject.AddComponent<Button>();
            Button(button, icon);
            return button;
        }

        public static void Place(RectTransform rect, float x, float width)
        {
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(width, ButtonSize);
            rect.localScale = Vector3.one;
        }

        public static void PositionAtSafeTopRight(RectTransform rect, float width,
            float extraRight = 0f)
        {
            PositionAtSafeTopRight(rect, width, Screen.safeArea,
                new Vector2(Screen.width, Screen.height), extraRight);
        }

        public static void PositionAtSafeTopRight(RectTransform rect, float width,
            Rect safe, Vector2 screen, float extraRight = 0f)
        {
            if (rect == null || screen.x <= 0 || screen.y <= 0) return;
            var canvas = rect.GetComponentInParent<Canvas>();
            var canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            if (canvasRect == null) return;
            float rightInset = (screen.x - safe.xMax) * canvasRect.rect.width / screen.x;
            float topInset = (screen.y - safe.yMax) * canvasRect.rect.height / screen.y;
            rect.anchorMin = rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-RightMargin - rightInset - extraRight,
                -TopMargin - topInset);
            rect.sizeDelta = new Vector2(width, ButtonSize);
            rect.localScale = Vector3.one;
        }

        public static void Button(Button button, SettingsIcon icon)
        {
            if (button == null) return;
            // Scene-authored buttons sometimes target a child background. Always use
            // the button's own image so every header has the same 48 px silhouette.
            var image = button.GetComponent<Image>() ?? button.gameObject.AddComponent<Image>();
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = Background;
            image.raycastTarget = true;
            button.targetGraphic = image;
            foreach (var childImage in button.GetComponentsInChildren<Image>(true))
                if (childImage != image && childImage.transform.parent == button.transform)
                    childImage.enabled = false;
            var legacyHover = button.GetComponent<EchoProtocol.UI.MainMenu.UIHoverEffect>();
            if (legacyHover != null) legacyHover.enabled = false;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.4f, 1.4f, 1.4f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(1f, 0.65f, 0.62f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            var outline = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
            outline.enabled = true;
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(1f, -1f);

            var iconRect = button.transform.Find("UtilityIcon") as RectTransform
                ?? button.transform.Find("ProfileIcon") as RectTransform
                ?? button.transform.Find("Icon") as RectTransform
                ?? button.transform.Find("TrophyIcon") as RectTransform;
            if (iconRect == null)
            {
                iconRect = new GameObject("UtilityIcon", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(SettingsIconGraphic)).GetComponent<RectTransform>();
                iconRect.SetParent(button.transform, false);
            }
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(28f, 28f);
            iconRect.localScale = Vector3.one;
            var graphic = iconRect.GetComponent<SettingsIconGraphic>()
                ?? iconRect.gameObject.AddComponent<SettingsIconGraphic>();
            graphic.Configure(icon, IconColor);
            EnsureTooltip(button, icon);
        }

        private static void EnsureTooltip(Button button, SettingsIcon icon)
        {
            var tooltip = button.transform.Find("UtilityTooltip") as RectTransform;
            if (tooltip == null)
            {
                tooltip = new GameObject("UtilityTooltip", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image), typeof(Outline)).GetComponent<RectTransform>();
                tooltip.SetParent(button.transform, false);
                tooltip.anchorMin = tooltip.anchorMax = new Vector2(0.5f, 0f);
                tooltip.pivot = new Vector2(0.5f, 1f);
                tooltip.anchoredPosition = new Vector2(0f, -9f);
                tooltip.sizeDelta = new Vector2(132f, 31f);
                var image = tooltip.GetComponent<Image>();
                image.color = new Color(0.035f, 0.047f, 0.051f, 0.97f);
                image.raycastTarget = false;
                var outline = tooltip.GetComponent<Outline>();
                outline.effectColor = new Color(0.45f, 0.08f, 0.07f, 1f);
                outline.effectDistance = new Vector2(1f, -1f);

                var labelRect = new GameObject("Label", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<RectTransform>();
                labelRect.SetParent(tooltip, false);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(3f, 2f);
                labelRect.offsetMax = new Vector2(-3f, -2f);
                var label = labelRect.GetComponent<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = 12f;
                label.fontStyle = FontStyles.Bold;
                label.color = CreditsColor;
                label.raycastTarget = false;
                UiTypographyTheme.Apply(label, true);
            }

            tooltip.GetComponent<Image>().enabled = true;
            tooltip.GetComponent<Outline>().enabled = true;

            var hover = button.GetComponent<UtilityBarTooltip>()
                ?? button.gameObject.AddComponent<UtilityBarTooltip>();
            hover.Configure(tooltip.gameObject, tooltip.Find("Label").GetComponent<TMP_Text>(), icon);
        }

        public static void Credits(RectTransform rect)
        {
            if (rect == null) return;
            var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = Background;
            foreach (var childImage in rect.GetComponentsInChildren<Image>(true))
                if (childImage != image && childImage.transform.parent == rect
                    && childImage.name != "CoinIcon" && childImage.name != "CurrencyMark")
                    childImage.enabled = false;
            var button = rect.GetComponent<Button>();
            if (button != null)
            {
                button.targetGraphic = image;
                var colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.4f, 1.4f, 1.4f, 1f);
                colors.selectedColor = colors.highlightedColor;
                colors.pressedColor = new Color(1f, 0.65f, 0.62f, 1f);
                button.colors = colors;
            }
            var outline = rect.GetComponent<Outline>() ?? rect.gameObject.AddComponent<Outline>();
            outline.enabled = true;
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(1f, -1f);
            var mark = rect.Find("CurrencyMark") as RectTransform
                ?? rect.Find("CoinIcon") as RectTransform;
            if (mark == null)
            {
                mark = new GameObject("CurrencyMark", typeof(RectTransform),
                    typeof(Image)).GetComponent<RectTransform>();
                mark.SetParent(rect, false);
            }
            mark.anchorMin = mark.anchorMax = new Vector2(0f, 0.5f);
            mark.pivot = new Vector2(0f, 0.5f);
            mark.anchoredPosition = new Vector2(12f, 0f);
            mark.sizeDelta = new Vector2(28f, 28f);
            var markImage = mark.GetComponent<Image>();
            markImage.sprite = null;
            markImage.color = CreditsColor;
            markImage.raycastTarget = false;
            mark.SetAsFirstSibling();
            var sharedFont = Resources.Load<TMP_FontAsset>("UI/LobbyReference/LobbySerif");
            // TMP's sourceFontFile is an import source, not a reliable UGUI runtime
            // font in builds. Use one dynamic serif for both UGUI credit labels.
            _creditsFont ??= Font.CreateDynamicFontFromOSFont(
                new[] { "Georgia", "Times New Roman", "Liberation Serif", "DejaVu Serif" }, 18);
            foreach (var text in rect.GetComponentsInChildren<Text>(true))
            {
                bool label = IsCreditLabel(text.name);
                PositionCreditText(text.rectTransform, label);
                if (_creditsFont != null) text.font = _creditsFont;
                text.fontSize = label ? 11 : 18;
                text.fontStyle = label ? FontStyle.Normal : FontStyle.Bold;
                text.resizeTextForBestFit = false;
                text.alignment = TextAnchor.MiddleLeft;
                text.color = label ? IconColor : CreditsColor;
            }
            foreach (var text in rect.GetComponentsInChildren<TMP_Text>(true))
            {
                bool label = IsCreditLabel(text.name);
                PositionCreditText(text.rectTransform, label);
                if (sharedFont != null) text.font = sharedFont;
                text.fontSize = label ? 11 : 18;
                text.fontStyle = label ? FontStyles.Normal : FontStyles.Bold;
                text.enableAutoSizing = false;
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.color = label ? IconColor : CreditsColor;
            }
            RefreshCreditsLabel(rect);
        }

        public static void RefreshCreditsLabel(RectTransform rect)
        {
            if (rect == null) return;
            string value = EchoProtocol.Settings.GameLanguage.Choose("TÍN DỤNG", "CREDITS");
            foreach (var text in rect.GetComponentsInChildren<Text>(true))
                if (text.name.Contains("Label") || text.name == "Caption") text.text = value;
            foreach (var text in rect.GetComponentsInChildren<TMP_Text>(true))
                if (text.name.Contains("Label") || text.name == "Caption") text.text = value;
        }

        private static bool IsCreditLabel(string name) =>
            name.Contains("Label") || name == "Caption";

        private static void PositionCreditText(RectTransform rect, bool label)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(50f, label ? -28f : -5f);
            rect.sizeDelta = new Vector2(120f, label ? 16f : 24f);
            rect.localScale = Vector3.one;
        }
    }

    public sealed class UtilityBarTooltip : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        private GameObject _tooltip;
        private TMP_Text _label;
        private SettingsIcon _icon;

        public void Configure(GameObject tooltip, TMP_Text label, SettingsIcon icon)
        {
            GameLanguage.Changed -= RefreshLanguage;
            _tooltip = tooltip;
            _label = label;
            _icon = icon;
            _tooltip.SetActive(false);
            GameLanguage.Changed += RefreshLanguage;
            RefreshLanguage();
        }

        private void OnDestroy() => GameLanguage.Changed -= RefreshLanguage;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_tooltip != null) _tooltip.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_tooltip != null) _tooltip.SetActive(false);
        }

        private void RefreshLanguage()
        {
            if (_label == null) return;
            _label.text = _icon switch
            {
                SettingsIcon.Profile => GameLanguage.Choose("HỒ SƠ", "PROFILE"),
                SettingsIcon.Trophy => GameLanguage.Choose("BẢNG XẾP HẠNG", "LEADERBOARD"),
                SettingsIcon.Web => GameLanguage.Choose("TRANG WEB", "WEBSITE"),
                SettingsIcon.Shop => GameLanguage.Choose("CỬA HÀNG", "SHOP"),
                SettingsIcon.Gear => GameLanguage.Choose("CÀI ĐẶT", "SETTINGS"),
                _ => string.Empty
            };
        }
    }
}
