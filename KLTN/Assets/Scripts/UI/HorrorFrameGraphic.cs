using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI
{
    /// <summary>Resolution independent, worn metal panels for the settings menu.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HorrorFrameGraphic : MaskableGraphic
    {
        [SerializeField] private Color _border = new Color(0.32f, 0.28f, 0.27f, 0.8f);
        [SerializeField] private bool _accent;

        public void Configure(Color fill, bool accent = false)
        {
            color = fill;
            _accent = accent;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var r = GetPixelAdjustedRect();
            float cut = Mathf.Min(7, r.height * 0.15f);
            var corners = new[]
            {
                new Vector2(r.xMin + cut, r.yMin), new Vector2(r.xMax - cut, r.yMin),
                new Vector2(r.xMax, r.yMin + cut), new Vector2(r.xMax, r.yMax - cut),
                new Vector2(r.xMax - cut, r.yMax), new Vector2(r.xMin + cut, r.yMax),
                new Vector2(r.xMin, r.yMax - cut), new Vector2(r.xMin, r.yMin + cut)
            };
            mesh.AddVert(r.center, color, Vector2.zero);
            foreach (var point in corners) mesh.AddVert(point, color, Vector2.zero);
            for (int i = 0; i < corners.Length; i++) mesh.AddTriangle(0, i + 1, (i + 1) % 8 + 1);
            var edge = _accent ? new Color(0.68f, 0.18f, 0.17f, 0.9f) : _border;
            for (int i = 0; i < corners.Length; i++) Line(mesh, corners[i], corners[(i + 1) % 8], 1, edge);

            // Sparse fixed scratches: no generated bitmap assets or per-frame noise.
            var random = new System.Random(47);
            for (int i = 0; i < 90; i++)
            {
                float x = Mathf.Lerp(r.xMin + 5, r.xMax - 5, (float)random.NextDouble());
                bool top = (i & 1) == 0;
                float depth = (float)random.NextDouble() * 16;
                float y = top ? r.yMax - depth : r.yMin + depth;
                float length = 1 + (float)random.NextDouble() * 9;
                var tint = i % 3 == 0 ? new Color(0.48f, 0.12f, 0.10f, 0.34f)
                    : new Color(0.57f, 0.52f, 0.46f, 0.12f);
                Line(mesh, new Vector2(x, y), new Vector2(Mathf.Min(x + length, r.xMax - 3), y + 1.5f), 0.7f, tint);
            }
            Line(mesh, new Vector2(r.xMin + 18, r.yMax - 1), new Vector2(r.xMin + r.width * 0.28f, r.yMax - 1),
                1.5f, new Color(0.67f, 0.17f, 0.15f, 0.65f));
        }

        private static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            var normal = new Vector2(-(b - a).y, (b - a).x).normalized * width * 0.5f;
            int start = mesh.currentVertCount;
            mesh.AddVert(a - normal, tint, Vector2.zero);
            mesh.AddVert(a + normal, tint, Vector2.zero);
            mesh.AddVert(b + normal, tint, Vector2.zero);
            mesh.AddVert(b - normal, tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
