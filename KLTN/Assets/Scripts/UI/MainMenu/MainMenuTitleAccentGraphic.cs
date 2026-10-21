using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.MainMenu
{
    /// <summary>
    /// Sparse red registration marks around the two-line main menu title.
    /// The marks scale with this RectTransform and use the default UI material.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MainMenuTitleAccentGraphic : MaskableGraphic
    {
        [SerializeField] private Color _signalColor = new Color(0.76f, 0.045f, 0.055f, 1f);
        [SerializeField, Range(0f, 2f)] private float _intensity = 1f;

        public void Configure(Color signalColor, float intensity = 1f)
        {
            _signalColor = signalColor;
            _intensity = Mathf.Clamp(intensity, 0f, 2f);
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f || _intensity <= 0f)
                return;

            // Authored over a roughly 590 x 210 title field. Normalized
            // positions preserve the accents when the canvas is scaled.
            float scale = Mathf.Max(0.45f,
                Mathf.Min(rect.width / 590f, rect.height / 210f));

            // The reference has a broken red scan near each word baseline.
            // Wide low-alpha strokes give a little bleed; narrow tapered
            // strokes provide the sharp registration errors.
            Streak(mesh, rect, 0.015f, 0.58f, 0.72f, 0.58f,
                7f * scale, 0.065f);
            Streak(mesh, rect, 0.005f, 0.581f, 0.62f, 0.581f,
                0.95f * scale, 0.43f);
            Streak(mesh, rect, 0.11f, 0.61f, 0.46f, 0.606f,
                0.65f * scale, 0.23f);
            Streak(mesh, rect, 0.03f, 0.556f, 0.37f, 0.556f,
                0.55f * scale, 0.19f);

            Streak(mesh, rect, 0.40f, 0.065f, 0.995f, 0.065f,
                7.5f * scale, 0.075f);
            Streak(mesh, rect, 0.47f, 0.066f, 0.99f, 0.066f,
                1.15f * scale, 0.42f);
            Streak(mesh, rect, 0.06f, 0.055f, 0.36f, 0.055f,
                0.6f * scale, 0.19f);
            Streak(mesh, rect, 0.58f, 0.036f, 0.91f, 0.036f,
                0.6f * scale, 0.18f);

            // Faint vertical cuts echo the red slivers through the ECHO and
            // PROTOCOL letters without drawing a continuous border or frame.
            Cut(mesh, rect, 0.248f, 0.51f, 1.015f, scale, 0.43f);
            Cut(mesh, rect, 0.318f, 0.58f, 0.955f, scale, 0.30f);
            Cut(mesh, rect, 0.405f, 0.49f, 0.90f, scale, 0.26f);
            Cut(mesh, rect, 0.742f, 0.07f, 0.44f, scale, 0.30f);
            Cut(mesh, rect, 0.91f, 0.10f, 0.36f, scale, 0.24f);
        }

        private void Cut(VertexHelper mesh, Rect rect, float x,
            float bottom, float top, float scale, float alpha)
        {
            Streak(mesh, rect, x, bottom, x, top,
                4.5f * scale, alpha * 0.17f);
            Streak(mesh, rect, x, bottom, x, top,
                0.72f * scale, alpha);
        }

        private void Streak(VertexHelper mesh, Rect rect,
            float x0, float y0, float x1, float y1,
            float width, float alpha)
        {
            Vector2 start = Point(rect, x0, y0);
            Vector2 end = Point(rect, x1, y1);
            Vector2 rise = Vector2.Lerp(start, end, 0.14f);
            Vector2 fall = Vector2.Lerp(start, end, 0.78f);
            Segment(mesh, start, rise, width, 0f, alpha);
            Segment(mesh, rise, fall, width, alpha, alpha * 0.72f);
            Segment(mesh, fall, end, width, alpha * 0.72f, 0f);
        }

        private static Vector2 Point(Rect rect, float x, float y)
        {
            return new Vector2(rect.xMin + rect.width * x,
                rect.yMin + rect.height * y);
        }

        private void Segment(VertexHelper mesh, Vector2 start, Vector2 end,
            float width, float startAlpha, float endAlpha)
        {
            Vector2 direction = end - start;
            if (direction.sqrMagnitude < 0.0001f) return;
            Vector2 normal = new Vector2(-direction.y, direction.x).normalized *
                (width * 0.5f);
            Color32 first = Tint(startAlpha);
            Color32 last = Tint(endAlpha);
            int vertex = mesh.currentVertCount;
            mesh.AddVert(start - normal, first, Vector2.zero);
            mesh.AddVert(start + normal, first, Vector2.zero);
            mesh.AddVert(end + normal, last, Vector2.zero);
            mesh.AddVert(end - normal, last, Vector2.zero);
            mesh.AddTriangle(vertex, vertex + 1, vertex + 2);
            mesh.AddTriangle(vertex, vertex + 2, vertex + 3);
        }

        private Color32 Tint(float alpha)
        {
            Color graphicColor = color;
            return new Color(
                _signalColor.r * graphicColor.r,
                _signalColor.g * graphicColor.g,
                _signalColor.b * graphicColor.b,
                Mathf.Clamp01(alpha * _intensity *
                    _signalColor.a * graphicColor.a));
        }
    }
}
