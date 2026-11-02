using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI
{
    /// <summary>Main Menu/Lobby materials and colours for functional interface surfaces.</summary>
    public static class IndustrialUiStyle
    {
        public static readonly Color Text = new Color32(229, 227, 218, 255);
        public static readonly Color Muted = new Color32(173, 177, 176, 255);
        public static readonly Color Accent = new Color32(201, 191, 140, 255);
        public static readonly Color Blood = new Color32(91, 20, 26, 255);
        public static readonly Color Panel = new Color32(9, 13, 14, 245);
        public static readonly Color Border = new Color32(94, 91, 87, 185);

        public static void Frame(GameObject owner, bool primary = false)
        {
            var image = owner.GetComponent<Image>();
            if (image != null)
            {
                if (image.sprite == null)
                {
                    image.sprite = Resources.Load<Sprite>("UI/LobbyReference/Frame");
                    image.type = Image.Type.Sliced;
                }
                image.color = primary ? Blood : Panel;
            }
            var outline = owner.GetComponent<Outline>();
            if (outline != null) { outline.effectColor = Border; outline.effectDistance = new Vector2(1,-1); }
        }

        public static void Button(Button button, bool primary = false)
        {
            if (button == null) return;
            Frame(button.gameObject, primary);
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = colors.selectedColor = new Color(1.18f,1.08f,1.08f);
            colors.pressedColor = new Color(.65f,.58f,.58f);
            colors.disabledColor = new Color(.38f,.38f,.38f,.7f);
            colors.fadeDuration = .1f;
            button.colors = colors;
            // Texture/card selectors retain their own icon layout.
            if (button.GetComponentInChildren<TMP_Text>(true) == null
                && button.GetComponentInChildren<UnityEngine.UI.Text>(true) == null) return;
            Stripe(button.transform, "IndustrialAccentLeft", false);
            Stripe(button.transform, "IndustrialAccentRight", true);
        }

        public static void Controls(Transform root)
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                var rect = button.transform as RectTransform;
                if (rect == null || rect.rect.width < 120 || rect.rect.height < 32) continue;
                if (button.GetComponentInChildren<RawImage>(true) != null) continue;
                Button(button);
                foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
                { UiTypographyTheme.Apply(label, true); label.color = Accent; }
                foreach (var label in button.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                { UiTypographyTheme.Apply(label, true); label.color = Accent; }
            }
        }

        private static void Stripe(Transform parent, string name, bool right)
        {
            if (parent.Find(name) != null) return;
            var existing = parent.Find(right ? "MenuAccentRight" : "MenuAccent");
            if (existing != null) return;
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(right ? 1 : 0, 0);
            rect.anchorMax = new Vector2(right ? 1 : 0, 1);
            rect.offsetMin = new Vector2(right ? -3 : 0, 9);
            rect.offsetMax = new Vector2(right ? 0 : 3, -9);
            go.GetComponent<Image>().color = Accent;
            go.GetComponent<Image>().raycastTarget = false;
        }

        public static Color CopyColor(string name, Color current, bool button)
        {
            string key = name.ToLowerInvariant();
            if (button) return WithAlpha(Accent, current.a);
            // Status indicators and player/team colours carry gameplay meaning.
            if (key.Contains("status") || key.Contains("warning") || key.Contains("health")
                || key.Contains("danger") || key.Contains("ready") || key.Contains("playerstate")
                || key.Contains("signal") || key.Contains("timer")) return current;
            if (key.Contains("title") || key.Contains("heading") || key.Contains("caption")
                || key.Contains("referenc") && key.Contains("label"))
                return WithAlpha(Accent, current.a);
            if (key.Contains("value") || key.Contains("price") || key.Contains("selectedname")
                || key.Contains("mapname")) return WithAlpha(Accent, current.a);
            // Preserve bronze authored in the reference design.
            if (current.r > .55f && current.g > .45f && current.b < current.g * .85f)
                return WithAlpha(Accent, current.a);
            if (current.r > .7f && current.g > .7f && current.b > .65f)
                return WithAlpha(Text, current.a);
            return current;
        }

        private static Color WithAlpha(Color color, float alpha) { color.a = alpha; return color; }
    }
}
