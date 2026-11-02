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
            colors.highlightedColor=new Color(1.2f,1.08f,1.08f,1f);
            colors.selectedColor=colors.highlightedColor;
            colors.pressedColor=new Color(0.65f,0.58f,0.58f,1f);
            colors.disabledColor=new Color(0.35f,0.38f,0.4f,1f);
            colors.colorMultiplier=1f;colors.fadeDuration=0.08f;control.colors=colors;
        }
        public static void StyleRelayAction(Button button,bool primary=false,bool danger=false)
        {
            if(button==null)return;
            var frame=button.GetComponent<Image>();
            if(frame!=null)frame.color=primary || danger ? IndustrialUiStyle.Accent : IndustrialUiStyle.Border;
            var bodyTransform=button.transform.Find("RelayButtonBody") ?? button.transform.Find("Fill");
            Image body;
            if(bodyTransform==null) {
                var go=new GameObject("RelayButtonBody",typeof(RectTransform),typeof(Image));
                go.transform.SetParent(button.transform,false);go.transform.SetAsFirstSibling();
                var rect=go.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
                rect.offsetMin=new Vector2(2,2);rect.offsetMax=new Vector2(-2,-2);
                body=go.GetComponent<Image>();body.raycastTarget=false;
            } else body=bodyTransform.GetComponent<Image>();
            body.color=danger || primary ? IndustrialUiStyle.Blood : IndustrialUiStyle.Panel;
            button.targetGraphic=body;StyleRelayControl(button);
            var label=button.GetComponentInChildren<TMP_Text>(true);
            if(label!=null) {
                label.color=IndustrialUiStyle.Accent;label.raycastTarget=false;
                UiTypographyTheme.Apply(label, true);
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
                text.color = title || button ? IndustrialUiStyle.Accent : IndustrialUiStyle.Text;
                UiTypographyTheme.Apply(text, button);
                text.text=EchoProtocol.Settings.GameLanguage.Translate(text.text);
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
                IndustrialUiStyle.Button(button);
                var colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.18f, 1.08f, 1.08f);
                colors.selectedColor = colors.highlightedColor;
                colors.pressedColor = new Color(.65f, .58f, .58f);
                colors.disabledColor = new Color(.38f, .38f, .38f, .7f);
                button.colors = colors;
            }
        }
    }
}
