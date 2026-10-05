using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.RelayB
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RelayBFeedbackGraphic : MaskableGraphic
    {
        [SerializeField] private RelayBCodeFeedback feedback;
        public void SetFeedback(RelayBCodeFeedback value, Color tint)
        {
            feedback = value; color = tint; raycastTarget = false; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var rect = GetPixelAdjustedRect();
            if (feedback == RelayBCodeFeedback.Right)
            {
                Line(vh, rect, new Vector2(0.12f, 0.48f), new Vector2(0.4f, 0.2f));
                Line(vh, rect, new Vector2(0.4f, 0.2f), new Vector2(0.9f, 0.84f));
            }
            else if (feedback == RelayBCodeFeedback.WrongPlace)
            {
                Line(vh, rect, new Vector2(0.12f, 0.5f), new Vector2(0.88f, 0.5f));
                Line(vh, rect, new Vector2(0.12f, 0.5f), new Vector2(0.35f, 0.72f));
                Line(vh, rect, new Vector2(0.12f, 0.5f), new Vector2(0.35f, 0.28f));
                Line(vh, rect, new Vector2(0.88f, 0.5f), new Vector2(0.65f, 0.72f));
                Line(vh, rect, new Vector2(0.88f, 0.5f), new Vector2(0.65f, 0.28f));
            }
            else
            {
                Line(vh, rect, new Vector2(0.22f, 0.22f), new Vector2(0.78f, 0.78f));
                Line(vh, rect, new Vector2(0.22f, 0.78f), new Vector2(0.78f, 0.22f));
            }
        }
        private void Line(VertexHelper vh, Rect rect, Vector2 a, Vector2 b)
        {
            a = rect.min + Vector2.Scale(rect.size, a); b = rect.min + Vector2.Scale(rect.size, b);
            var offset = new Vector2(-(b - a).y, (b - a).x).normalized * Mathf.Max(0.8f, rect.height * 0.065f);
            int start = vh.currentVertCount;
            vh.AddVert(a - offset, color, Vector2.zero); vh.AddVert(a + offset, color, Vector2.zero);
            vh.AddVert(b + offset, color, Vector2.zero); vh.AddVert(b - offset, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
