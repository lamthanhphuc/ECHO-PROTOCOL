using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.HUD
{
    public static class HUDModalPresentation
    {
        public static void Apply(GameObject root)
        {
            if (root == null) return;
            var canvas = root.GetComponentInParent<Canvas>();
            var scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : root.GetComponentInChildren<CanvasScaler>(true);
            if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                scaler.referenceResolution = new Vector2(1600f, 900f);
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                switch (text.text.Trim())
                {
                    case "SECURITY TERMINAL": text.text = "Security Terminal"; break;
                    case "SUBMIT": case "CONFIRM": text.text = "Xác nhận"; break;
                    case "CLOSE": text.text = "Đóng"; break;
                    case "RESET": text.text = "Đặt lại"; break;
                    case "BACK": text.text = "Trở về"; break;
                    case "NEXT": case "CONTINUE": text.text = "Tiếp tục"; break;
                    case "TRANSMIT": text.text = "Kiểm tra mã"; break;
                    case "01 FIND": text.text = "01 Tìm"; break;
                    case "02 DECODE": text.text = "02 Giải mã"; break;
                    case "03 SYNC": text.text = "03 Đồng bộ"; break;
                }
                bool button = text.GetComponentInParent<Button>() != null;
                bool title = text.name.ToLowerInvariant().Contains("title");
                text.fontSize = Mathf.Clamp(text.fontSize, 12f, title ? 24f : button ? 16f : 20f);
                text.fontSizeMin = 12f;
                text.fontSizeMax = text.fontSize;
                text.enableAutoSizing = true;
                text.color = title ? HUDPresentationStyle.Ink : HUDPresentationStyle.Muted;
                text.fontStyle = title ? FontStyles.Bold : FontStyles.Normal;
            }
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                string name = image.name.ToLowerInvariant();
                if (name.Contains("background") || name.Contains("panel") || name.Contains("card"))
                    image.color = new Color(0.055f, 0.065f, 0.065f, image.color.a);
            }
            foreach (var outline in root.GetComponentsInChildren<Outline>(true))
                outline.effectColor = new Color(0.22f, 0.26f, 0.25f, 0.35f);
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                var colors = button.colors;
                colors.normalColor = new Color(0.16f, 0.19f, 0.19f);
                colors.highlightedColor = new Color(0.26f, 0.31f, 0.3f);
                colors.selectedColor = colors.highlightedColor;
                colors.pressedColor = new Color(0.1f, 0.13f, 0.13f);
                colors.disabledColor = new Color(0.1f, 0.11f, 0.11f, 0.6f);
                button.colors = colors;
            }
        }
    }
}
