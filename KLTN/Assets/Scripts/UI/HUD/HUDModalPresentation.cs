using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.HUD
{
    public static class HUDModalPresentation
    {
        // Relay controls share the contrast and tactile feedback of Relay A stabilization.
        public static void StyleRelayControl(Selectable control)
        {
            if(control==null)return;
            control.transition=Selectable.Transition.ColorTint;
            var colors=control.colors;
            colors.normalColor=Color.white;
            colors.highlightedColor=new Color(1.25f,1.35f,1.4f,1f);
            colors.selectedColor=colors.highlightedColor;
            colors.pressedColor=new Color(0.65f,0.8f,0.85f,1f);
            colors.disabledColor=new Color(0.35f,0.38f,0.4f,1f);
            colors.colorMultiplier=1f;colors.fadeDuration=0.08f;control.colors=colors;
        }
        public static void StyleRelayAction(Button button,bool primary=false,bool danger=false)
        {
            if(button==null)return;
            var frame=button.GetComponent<Image>();
            if(frame!=null)frame.color=primary ? new Color(0.2f,0.43f,0.45f) : danger ? new Color(0.44f,0.2f,0.18f) : new Color(0.24f,0.3f,0.32f);
            var bodyTransform=button.transform.Find("RelayButtonBody") ?? button.transform.Find("Fill");
            Image body;
            if(bodyTransform==null) {
                var go=new GameObject("RelayButtonBody",typeof(RectTransform),typeof(Image));
                go.transform.SetParent(button.transform,false);go.transform.SetAsFirstSibling();
                var rect=go.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
                rect.offsetMin=new Vector2(2,2);rect.offsetMax=new Vector2(-2,-2);
                body=go.GetComponent<Image>();body.raycastTarget=false;
            } else body=bodyTransform.GetComponent<Image>();
            body.color=danger ? new Color(0.24f,0.075f,0.07f) : primary ? new Color(0.07f,0.23f,0.25f) : new Color(0.085f,0.13f,0.15f);
            button.targetGraphic=body;StyleRelayControl(button);
            var label=button.GetComponentInChildren<TMP_Text>(true);
            if(label!=null) {
                label.color=new Color(0.92f,0.94f,0.94f);label.raycastTarget=false;
                label.fontSize=Mathf.Min(label.fontSize,16f);label.fontSizeMax=16f;label.fontSizeMin=12f;label.enableAutoSizing=true;
            }
        }

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
                    case "SECURITY TERMINAL": text.text = "Trạm an ninh"; break;
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
