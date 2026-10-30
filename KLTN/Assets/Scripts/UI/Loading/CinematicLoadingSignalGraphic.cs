using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.Loading
{
    /// <summary>Sparse, bounded signal noise; independent from Unity's gameplay random state.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CinematicLoadingSignalGraphic : MaskableGraphic
    {
        private float _staticIntensity, _glitchIntensity;
        private int _sample;

        public void SetSignal(float time, float staticIntensity, float glitchIntensity)
        {
            int sample = Mathf.FloorToInt(time * 8f);
            if (sample == _sample && _staticIntensity == staticIntensity && _glitchIntensity == glitchIntensity) return;
            _sample = sample;
            _staticIntensity = staticIntensity;
            _glitchIntensity = glitchIntensity;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = GetPixelAdjustedRect();
            // Vertical falloff keeps the laboratory readable and the lower text legible.
            Gradient(mesh, new Rect(r.xMin, r.yMin, r.width, r.height * 0.72f),
                new Color(0, 0, 0, 0.92f), new Color(0, 0, 0, 0));
            Gradient(mesh, new Rect(r.xMin, r.yMax - r.height * 0.28f, r.width, r.height * 0.28f),
                new Color(0, 0, 0, 0), new Color(0, 0, 0, 0.78f));
            uint state = unchecked((uint)(_sample * 1664525 + 1013904223));
            for (float y = r.yMin; y < r.yMax; y += 7f)
                Quad(mesh, new Rect(r.xMin, y, r.width, 0.7f), new Color(0, 0, 0, _staticIntensity * 0.26f));
            for (int i = 0; i < 150; i++)
            {
                float x = Mathf.Lerp(r.xMin, r.xMax, Next(ref state));
                float y = Mathf.Lerp(r.yMin, r.yMax, Next(ref state));
                float width = 1f + Next(ref state) * 3.5f;
                Quad(mesh, new Rect(x, y, width, 0.8f), new Color(0.72f, 0.82f, 0.80f, _staticIntensity * 0.65f));
            }
            // Brief low contrast horizontal dropouts, never a full-screen flash.
            if ((_sample % 37) < 2 && _glitchIntensity > 0)
                for (int i = 0; i < 3; i++)
                {
                    float y = Mathf.Lerp(r.yMin + r.height * 0.3f, r.yMax, Next(ref state));
                    Quad(mesh, new Rect(r.xMin, y, r.width, 1f + Next(ref state) * 3f),
                        new Color(0.3f, 0.58f, 0.57f, _glitchIntensity * 0.28f));
                }
        }

        private static float Next(ref uint state)
        {
            state = unchecked(state * 1664525u + 1013904223u);
            return (state & 0x00FFFFFF) / 16777216f;
        }

        private static void Quad(VertexHelper mesh, Rect rect, Color tint) => Gradient(mesh, rect, tint, tint);
        private static void Gradient(VertexHelper mesh, Rect r, Color bottom, Color top)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(new Vector2(r.xMin, r.yMin), bottom, Vector2.zero);
            mesh.AddVert(new Vector2(r.xMin, r.yMax), top, Vector2.zero);
            mesh.AddVert(new Vector2(r.xMax, r.yMax), top, Vector2.zero);
            mesh.AddVert(new Vector2(r.xMax, r.yMin), bottom, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
