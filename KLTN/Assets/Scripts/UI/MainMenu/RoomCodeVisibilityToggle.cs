using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.MainMenu
{
    [RequireComponent(typeof(InputField))]
    public sealed class RoomCodeVisibilityToggle : MonoBehaviour
    {
        InputField _field;
        Button _button;
        RoomCodeEyeGraphic _eye;
        public bool IsHidden { get; private set; } = true;

        public static RoomCodeVisibilityToggle Attach(InputField field)
        {
            var toggle = field.GetComponent<RoomCodeVisibilityToggle>() ?? field.gameObject.AddComponent<RoomCodeVisibilityToggle>();
            toggle.Build();
            return toggle;
        }

        void Build()
        {
            if (_button != null) return;
            _field = GetComponent<InputField>();
            var rect = new GameObject("RoomCodeVisibility", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1, .5f);
            rect.pivot = new Vector2(1, .5f);
            rect.sizeDelta = new Vector2(48, 44);
            rect.anchoredPosition = new Vector2(-6, 0);
            _button = rect.GetComponent<Button>();
            _button.navigation = new Navigation { mode = Navigation.Mode.None };
            _button.onClick.AddListener(Toggle);
            IndustrialUiStyle.ActionButton(_button);
            var icon = new GameObject("Eye", typeof(RectTransform), typeof(RoomCodeEyeGraphic)).GetComponent<RectTransform>();
            icon.SetParent(rect, false);
            icon.sizeDelta = new Vector2(28, 22);
            icon.anchoredPosition = Vector2.zero;
            _eye = icon.GetComponent<RoomCodeEyeGraphic>();
            _eye.raycastTarget = false;
            _eye.color = IndustrialUiStyle.Accent;
            ReserveTextSpace(_field.textComponent?.rectTransform);
            ReserveTextSpace(_field.placeholder != null ? _field.placeholder.rectTransform : null);
            Hide();
        }

        static void ReserveTextSpace(RectTransform rect)
        {
            if (rect == null) return;
            var min = rect.offsetMin; min.x = Mathf.Max(min.x, 60); rect.offsetMin = min;
            var max = rect.offsetMax; max.x = Mathf.Min(max.x, -60); rect.offsetMax = max;
        }
        public void Toggle() { if (_field.interactable) SetHidden(!IsHidden); }
        public void Hide() { if (_field != null) SetHidden(true); }
        void SetHidden(bool hidden)
        {
            IsHidden = hidden;
            _field.inputType = hidden ? InputField.InputType.Password : InputField.InputType.Standard;
            _field.asteriskChar = '•';
            _field.ForceLabelUpdate();
            _eye.Hidden = hidden;
            _eye.SetVerticesDirty();
        }
        void OnEnable() { if (_field != null) Hide(); }
        void LateUpdate() { if (_button != null) _button.interactable = _field.interactable; }
    }

    // Draw the eye as UI geometry so it never depends on a font containing an eye glyph.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoomCodeEyeGraphic : MaskableGraphic
    {
        public bool Hidden;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            for (int i = 0; i < 24; i++)
            {
                float a = i / 24f, b = (i + 1) / 24f;
                foreach (float sign in new[] { -1f, 1f })
                    Stroke(vh, r.center + new Vector2((a - .5f) * r.width, Mathf.Sin(a * Mathf.PI) * r.height * .36f * sign),
                        r.center + new Vector2((b - .5f) * r.width, Mathf.Sin(b * Mathf.PI) * r.height * .36f * sign));
                Stroke(vh, r.center + new Vector2(Mathf.Cos(a * Mathf.PI * 2), Mathf.Sin(a * Mathf.PI * 2)) * r.height * .16f,
                    r.center + new Vector2(Mathf.Cos(b * Mathf.PI * 2), Mathf.Sin(b * Mathf.PI * 2)) * r.height * .16f);
            }
            if (Hidden) Stroke(vh, r.center + new Vector2(-r.width * .45f, -r.height * .45f), r.center + new Vector2(r.width * .45f, r.height * .45f));
        }
        void Stroke(VertexHelper vh, Vector2 a, Vector2 b)
        {
            var d = (b - a).normalized;
            var n = new Vector2(-d.y, d.x) * .85f;
            int start = vh.currentVertCount;
            foreach (var p in new[] { a - n, a + n, b + n, b - n }) vh.AddVert(p, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
