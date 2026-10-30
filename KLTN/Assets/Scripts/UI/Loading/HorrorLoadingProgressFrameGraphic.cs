using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.Loading
{
    /// <summary>Worn metal and blood around the measured fill; never draws progress itself.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HorrorLoadingProgressFrameGraphic : MaskableGraphic
    {
        [SerializeField] private Color _accent = new Color32(190, 15, 24, 255);

        public void Configure(Color red)
        {
            if (_accent == red) return;
            _accent = red;
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
            if (rect.width <= 0 || rect.height <= 0) return;
            float scale = Mathf.Min(rect.height / 52f, rect.width / 850f);
            Rect rail = Inset(rect, 4f * scale);
            Rect recess = Inset(rect, 8f * scale);
            Color red = _accent;

            // A broad, faint halo and a narrow hard rail retain the reference's industrial silhouette.
            Quad(mesh, rect, Tint(red, 0.13f));
            Quad(mesh, Inset(rect, 1.5f * scale), new Color(0.02f, 0.002f, 0.003f, 0.84f));
            Quad(mesh, rail, new Color32(61, 37, 39, 255));
            Gradient(mesh, recess, new Color32(9, 2, 4, 255), new Color32(22, 8, 10, 255));
            Quad(mesh, new Rect(recess.xMin, recess.yMax - 2 * scale, recess.width, 2 * scale),
                new Color(0, 0, 0, 0.78f));
            // Soft rails surround the empty recess too; they do not imply measured progress.
            Gradient(mesh, new Rect(rect.xMin, rect.yMax - 4 * scale, rect.width, 4 * scale),
                Tint(red, 0.04f), Tint(red, 0.27f));
            Gradient(mesh, new Rect(rect.xMin, rect.yMin, rect.width, 4 * scale),
                Tint(red, 0.24f), Tint(red, 0.035f));

            uint random = 0xECA0743u;
            const int sections = 68;
            float segment = rail.width / sections;
            for (int index = 0; index < sections; index++)
            {
                float x = rail.xMin + segment * index;
                float width = segment * Mathf.Lerp(0.72f, 1.03f, Next(ref random));
                width = Mathf.Min(width, rail.xMax - x);
                float wear = Next(ref random);
                float notch = (Next(ref random) - 0.5f) * 1.7f * scale;
                Color metal = Color.Lerp(new Color32(61, 36, 37, 255), new Color32(162, 119, 110, 255), wear);
                Quad(mesh, new Rect(x, rail.yMax - 2.6f * scale + notch, width, 1.7f * scale), metal);
                Quad(mesh, new Rect(x, rail.yMin + 0.5f * scale + notch, width, 1.3f * scale),
                    Color.Lerp(new Color32(54, 15, 18, 255), metal, 0.5f));
                if (wear > 0.13f)
                {
                    Color chippedRed = Color.Lerp(red, new Color(1f, 0.13f, 0.10f, red.a), wear * 0.35f);
                    Quad(mesh, new Rect(x, rail.yMax + notch, width * 0.9f, (0.9f + wear * 0.7f) * scale),
                        Tint(chippedRed, 0.68f + wear * 0.3f));
                    Quad(mesh, new Rect(x, rail.yMin - 0.9f * scale + notch, width * 0.86f, (0.8f + wear * 0.45f) * scale),
                        Tint(chippedRed, 0.54f + wear * 0.35f));
                }
            }

            // Chipped end caps, bolt heads, and short diagonal scores stay out of the fill region.
            foreach (float x in new[] { rail.xMin, rail.xMax - 3f * scale })
            {
                Quad(mesh, new Rect(x, rail.yMin, 3f * scale, rail.height), new Color32(105, 67, 66, 255));
                Quad(mesh, new Rect(x + scale, rail.yMin + scale, scale, rail.height - 2 * scale), Tint(red, 0.8f));
            }
            for (int index = 0; index < 52; index++)
            {
                float x = Mathf.Lerp(rail.xMin + 3 * scale, rail.xMax - 10 * scale, Next(ref random));
                bool top = Next(ref random) > 0.5f;
                float y = top ? rail.yMax - 2 * scale : rail.yMin + scale;
                float length = (1.5f + Next(ref random) * 8f) * scale;
                Segment(mesh, new Vector2(x, y), new Vector2(x + length, y + 1.4f * scale), 0.65f * scale,
                    Next(ref random) > 0.55f ? new Color(0.78f, 0.63f, 0.59f, 0.52f) : new Color(0.01f, 0, 0, 0.9f));
            }

            // Sparse decorative drips collect near the left cap, outside the measured geometry.
            for (int index = 0; index < 7; index++)
            {
                float t = index < 4 ? 0.035f + index * 0.022f : 0.61f + (index - 4) * 0.13f;
                float x = Mathf.Lerp(rail.xMin, rail.xMax, t);
                float length = (index < 4 ? 10f + Next(ref random) * 21f : 6f + Next(ref random) * 10f) * scale;
                float width = (0.65f + Next(ref random) * 1.4f) * scale;
                float bottom = Mathf.Max(rect.yMin - 27 * scale, rail.yMin - length);
                Gradient(mesh, new Rect(x, bottom, width, rail.yMin - bottom), Tint(red, 0.09f), Tint(red, 0.70f));
            }
        }

        private static Rect Inset(Rect rect, float inset) => new Rect(rect.x + inset, rect.y + inset,
            Mathf.Max(0, rect.width - inset * 2), Mathf.Max(0, rect.height - inset * 2));

        private static Color Tint(Color color, float alpha) { color.a *= alpha; return color; }

        private static float Next(ref uint state)
        {
            state = unchecked(state * 1664525u + 1013904223u);
            return (state & 0x00ffffff) / 16777216f;
        }

        private void Quad(VertexHelper mesh, Rect rect, Color tint) => Gradient(mesh, rect, tint, tint);

        private void Gradient(VertexHelper mesh, Rect rect, Color bottom, Color top)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(new Vector2(rect.xMin, rect.yMin), bottom * color, Vector2.zero);
            mesh.AddVert(new Vector2(rect.xMin, rect.yMax), top * color, Vector2.zero);
            mesh.AddVert(new Vector2(rect.xMax, rect.yMax), top * color, Vector2.zero);
            mesh.AddVert(new Vector2(rect.xMax, rect.yMin), bottom * color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }

        private void Segment(VertexHelper mesh, Vector2 a, Vector2 b, float thickness, Color tint)
        {
            Vector2 normal = new Vector2(a.y - b.y, b.x - a.x).normalized * thickness * 0.5f;
            int start = mesh.currentVertCount;
            Color vertex = tint * color;
            mesh.AddVert(a - normal, vertex, Vector2.zero);
            mesh.AddVert(a + normal, vertex, Vector2.zero);
            mesh.AddVert(b + normal, vertex, Vector2.zero);
            mesh.AddVert(b - normal, vertex, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
