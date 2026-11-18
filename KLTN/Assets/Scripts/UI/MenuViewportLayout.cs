using UnityEngine;

namespace EchoProtocol.UI
{
    internal static class MenuViewportLayout
    {
        public static void NormalizeChildren(RectTransform panel)
        {
            var size = panel.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;
            foreach (Transform child in panel)
            {
                if (child is not RectTransform rect) continue;
                Vector2 position = rect.localPosition;
                var minimum = position + rect.rect.min - panel.rect.min;
                var maximum = position + rect.rect.max - panel.rect.min;
                rect.anchorMin = new Vector2(minimum.x / size.x, minimum.y / size.y);
                rect.anchorMax = new Vector2(maximum.x / size.x, maximum.y / size.y);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
        }

        public static float Fit(RectTransform panel, Vector2 design, float width, float height)
        {
            float scale = Mathf.Max(.05f, Mathf.Min(width / design.x, height / design.y));
            panel.sizeDelta = new Vector2(width / scale, height / scale);
            panel.localScale = Vector3.one * scale;
            panel.anchoredPosition = Vector2.zero;
            return scale;
        }
    }
}
