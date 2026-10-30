using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.Loading
{
    /// <summary>Pending, active, and completed phase marks; independent of percentage/progress.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HorrorLoadingStatusGraphic : MaskableGraphic
    {
        [SerializeField, Range(0, 2)] private int _state;
        private int _sample = -1;
        private float _time;
        [SerializeField] private Color _red = new Color32(205, 19, 29, 255);
        [SerializeField] private Color _pending = new Color32(105, 99, 99, 255);

        public void Configure(int state, float time, Color red, Color pending)
        {
            state = Mathf.Clamp(state, 0, 2);
            int sample = state == 1 ? Mathf.FloorToInt(time * 20f) : 0;
            if (_state == state && _sample == sample && _red == red && _pending == pending) return;
            _state = state;
            _sample = sample;
            _time = sample / 20f;
            _red = red;
            _pending = pending;
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
            float radius = Mathf.Min(rect.width, rect.height) * 0.43f;
            if (radius <= 0) return;
            Vector2 center = rect.center;
            float stroke = Mathf.Max(0.8f, radius * 0.15f);
            if (_state == 0)
            {
                Ring(mesh, center, radius, stroke, _pending, 0, Mathf.PI * 2);
                return;
            }

            Color red = _red;
            if (_state == 1)
            {
                float pulse = 0.72f + Mathf.Sin(_time * 3.4f) * 0.18f;
                Ring(mesh, center, radius * 1.1f, stroke * 1.7f, Alpha(red, 0.09f * pulse), 0, Mathf.PI * 2);
                Ring(mesh, center, radius, stroke, Alpha(red, pulse), 0, Mathf.PI * 2);
                float angle = -_time * 0.85f;
                Color highlight = Color.Lerp(red, new Color(1f, 0.7f, 0.62f, red.a), 0.7f);
                Ring(mesh, center, radius, stroke * 1.12f, highlight, angle, angle + 1.2f);
                return;
            }

            Disc(mesh, center, radius * 0.92f, new Color(red.r * 0.24f, red.g * 0.18f, red.b * 0.18f, red.a * 0.45f));
            Ring(mesh, center, radius, stroke * 0.9f, Alpha(red, 0.92f), 0, Mathf.PI * 2);
            Segment(mesh, center + new Vector2(-0.48f, 0.02f) * radius,
                center + new Vector2(-0.1f, -0.34f) * radius, stroke * 1.15f, red);
            Segment(mesh, center + new Vector2(-0.1f, -0.34f) * radius,
                center + new Vector2(0.55f, 0.46f) * radius, stroke * 1.15f, red);
        }

        private static Color Alpha(Color tint, float alpha) { tint.a *= alpha; return tint; }

        private void Ring(VertexHelper mesh, Vector2 center, float radius, float thickness, Color tint, float from, float to)
        {
            const int segments = 48;
            float inner = Mathf.Max(0, radius - thickness);
            for (int index = 0; index < segments; index++)
            {
                float a = Mathf.Lerp(from, to, index / (float)segments);
                float b = Mathf.Lerp(from, to, (index + 1) / (float)segments);
                Vector2 d0 = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 d1 = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                Quad(mesh, center + d0 * inner, center + d0 * radius, center + d1 * radius, center + d1 * inner, tint);
            }
        }

        private void Disc(VertexHelper mesh, Vector2 center, float radius, Color tint)
        {
            const int segments = 48;
            int start = mesh.currentVertCount;
            mesh.AddVert(center, tint * color, Vector2.zero);
            for (int index = 0; index <= segments; index++)
            {
                float a = index * Mathf.PI * 2 / segments;
                mesh.AddVert(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, tint * color, Vector2.zero);
                if (index > 0) mesh.AddTriangle(start, start + index, start + index + 1);
            }
        }

        private void Segment(VertexHelper mesh, Vector2 a, Vector2 b, float thickness, Color tint)
        {
            Vector2 normal = new Vector2(a.y - b.y, b.x - a.x).normalized * thickness * 0.5f;
            Quad(mesh, a - normal, a + normal, b + normal, b - normal, tint);
        }

        private void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int start = mesh.currentVertCount;
            Color vertex = tint * color;
            mesh.AddVert(a, vertex, Vector2.zero);
            mesh.AddVert(b, vertex, Vector2.zero);
            mesh.AddVert(c, vertex, Vector2.zero);
            mesh.AddVert(d, vertex, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
