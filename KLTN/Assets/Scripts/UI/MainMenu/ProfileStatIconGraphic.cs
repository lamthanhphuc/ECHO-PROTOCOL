using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.MainMenu
{
    /// <summary>Small, tintable dossier icons drawn directly by the Unity UI canvas.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/ECHO PROTOCOL/Profile Stat Icon Graphic")]
    public sealed class ProfileStatIconGraphic : MaskableGraphic
    {
        public enum IconKind
        {
            Document = 0,
            Crosshair = 1,
            Pulse = 2,
            Credits = 3,
            Calendar = 4,
            Trophy = 5,
            Skull = 6
        }

        [SerializeField] private IconKind icon = IconKind.Document;
        [SerializeField, Min(0.5f)] private float lineThickness = 1.8f;

        public IconKind Kind
        {
            get => icon;
            set
            {
                if (icon == value) return;
                icon = value;
                SetVerticesDirty();
            }
        }

        protected override void OnEnable()
        {
            // These icons are decoration inside statistic cards, never click targets.
            raycastTarget = false;
            base.OnEnable();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float scale = Mathf.Min(rect.width, rect.height) / 24f;
            if (scale <= 0f) return;

            var pen = new Pen(mesh, rect.center, scale, Mathf.Max(0.5f, lineThickness), color);
            switch (icon)
            {
                case IconKind.Document:
                    DrawDocument(pen);
                    break;
                case IconKind.Crosshair:
                    DrawCrosshair(pen);
                    break;
                case IconKind.Pulse:
                    DrawPulse(pen);
                    break;
                case IconKind.Credits:
                    DrawCredits(pen);
                    break;
                case IconKind.Calendar:
                    DrawCalendar(pen);
                    break;
                case IconKind.Trophy:
                    DrawTrophy(pen);
                    break;
                case IconKind.Skull:
                    DrawSkull(pen);
                    break;
            }
        }

        private static void DrawDocument(Pen pen)
        {
            // A clipped personnel sheet with a raised clipboard clasp.
            pen.Line(-7f, -10f, 7f, -10f);
            pen.Line(7f, -10f, 7f, 9f);
            pen.Line(7f, 9f, 3.5f, 9f);
            pen.Line(-3.5f, 9f, -7f, 9f);
            pen.Line(-7f, 9f, -7f, -10f);
            pen.Line(-3.5f, 8f, -3.5f, 11f);
            pen.Line(-3.5f, 11f, 3.5f, 11f);
            pen.Line(3.5f, 11f, 3.5f, 8f);
            pen.Line(-3.5f, 8f, 3.5f, 8f);
            pen.Line(-4f, 4f, 4f, 4f);
            pen.Line(-4f, 0f, 4f, 0f);
            pen.Line(-4f, -4f, 4f, -4f);
            pen.Line(-4f, -7f, 1f, -7f);
        }

        private static void DrawCrosshair(Pen pen)
        {
            pen.Ellipse(0f, 0f, 7f, 7f, 32);
            pen.Line(-11f, 0f, -5f, 0f);
            pen.Line(5f, 0f, 11f, 0f);
            pen.Line(0f, -11f, 0f, -5f);
            pen.Line(0f, 5f, 0f, 11f);
            pen.Disk(0f, 0f, 1.25f, 12);
        }

        private static void DrawPulse(Pen pen)
        {
            pen.Line(-11f, 0f, -6f, 0f);
            pen.Line(-6f, 0f, -3.5f, 2f);
            pen.Line(-3.5f, 2f, -1.5f, -5f);
            pen.Line(-1.5f, -5f, 1f, 8f);
            pen.Line(1f, 8f, 3.5f, -2f);
            pen.Line(3.5f, -2f, 5.5f, 0f);
            pen.Line(5.5f, 0f, 11f, 0f);
        }

        private static void DrawCredits(Pen pen)
        {
            // Three slightly overlapping pressed-metal coin edges.
            pen.Ellipse(0f, 6f, 8f, 2.5f, 24);
            pen.Line(-8f, 6f, -8f, 1f);
            pen.Line(8f, 6f, 8f, 1f);
            pen.Ellipse(0f, 1f, 8f, 2.5f, 24);
            pen.Line(-8f, 1f, -8f, -4f);
            pen.Line(8f, 1f, 8f, -4f);
            pen.Ellipse(0f, -4f, 8f, 2.5f, 24);
            pen.Line(-8f, -4f, -8f, -8f);
            pen.Line(8f, -4f, 8f, -8f);
            pen.Ellipse(0f, -8f, 8f, 2.5f, 24);
        }

        private static void DrawCalendar(Pen pen)
        {
            pen.Line(-9f, -10f, 9f, -10f);
            pen.Line(9f, -10f, 9f, 8f);
            pen.Line(9f, 8f, -9f, 8f);
            pen.Line(-9f, 8f, -9f, -10f);
            pen.Line(-9f, 3f, 9f, 3f);
            pen.Line(-5f, 6f, -5f, 11f);
            pen.Line(5f, 6f, 5f, 11f);
            pen.Line(-5f, -2f, -2f, -2f);
            pen.Line(2f, -2f, 5f, -2f);
            pen.Line(-5f, -6f, -2f, -6f);
            pen.Line(2f, -6f, 5f, -6f);
        }

        private static void DrawTrophy(Pen pen)
        {
            pen.Line(-7f, 8f, 7f, 8f);
            pen.Line(-7f, 8f, -5f, -1f);
            pen.Line(7f, 8f, 5f, -1f);
            pen.Line(-5f, -1f, 0f, -4f);
            pen.Line(5f, -1f, 0f, -4f);
            pen.Line(-7f, 6f, -10f, 6f);
            pen.Line(-10f, 6f, -9f, 0f);
            pen.Line(-9f, 0f, -5f, -2f);
            pen.Line(7f, 6f, 10f, 6f);
            pen.Line(10f, 6f, 9f, 0f);
            pen.Line(9f, 0f, 5f, -2f);
            pen.Line(0f, -4f, 0f, -8f);
            pen.Line(-5f, -8f, 5f, -8f);
        }

        private static void DrawSkull(Pen pen)
        {
            pen.Ellipse(0f, 2f, 8f, 8f, 24);
            pen.Ellipse(-3f, 2f, 2f, 2.4f, 12);
            pen.Ellipse(3f, 2f, 2f, 2.4f, 12);
            pen.Line(0f, 0f, -1f, -3f);
            pen.Line(-1f, -3f, 1f, -3f);
            pen.Line(1f, -3f, 0f, 0f);
            pen.Line(-5f, -4f, -5f, -9f);
            pen.Line(5f, -4f, 5f, -9f);
            pen.Line(-5f, -9f, 5f, -9f);
            pen.Line(-2f, -9f, -2f, -5f);
            pen.Line(2f, -9f, 2f, -5f);
        }

        private readonly struct Pen
        {
            private readonly VertexHelper mesh;
            private readonly Vector2 center;
            private readonly float scale;
            private readonly float thickness;
            private readonly Color32 tint;

            public Pen(VertexHelper mesh, Vector2 center, float scale, float thickness, Color tint)
            {
                this.mesh = mesh;
                this.center = center;
                this.scale = scale;
                this.thickness = thickness * scale;
                this.tint = tint;
            }

            public void Line(float x1, float y1, float x2, float y2)
            {
                Vector2 a = center + new Vector2(x1, y1) * scale;
                Vector2 b = center + new Vector2(x2, y2) * scale;
                Vector2 delta = b - a;
                if (delta.sqrMagnitude < 0.0001f) return;

                Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (thickness * 0.5f);
                int first = mesh.currentVertCount;
                mesh.AddVert(a - normal, tint, Vector2.zero);
                mesh.AddVert(a + normal, tint, Vector2.zero);
                mesh.AddVert(b + normal, tint, Vector2.zero);
                mesh.AddVert(b - normal, tint, Vector2.zero);
                mesh.AddTriangle(first, first + 1, first + 2);
                mesh.AddTriangle(first, first + 2, first + 3);
            }

            public void Ellipse(float cx, float cy, float rx, float ry, int segments)
            {
                float step = Mathf.PI * 2f / segments;
                float x = cx + rx;
                float y = cy;
                for (int i = 1; i <= segments; i++)
                {
                    float radians = i * step;
                    float nextX = cx + Mathf.Cos(radians) * rx;
                    float nextY = cy + Mathf.Sin(radians) * ry;
                    Line(x, y, nextX, nextY);
                    x = nextX;
                    y = nextY;
                }
            }

            public void Disk(float cx, float cy, float radius, int segments)
            {
                int first = mesh.currentVertCount;
                mesh.AddVert(center + new Vector2(cx, cy) * scale, tint, Vector2.zero);
                for (int i = 0; i <= segments; i++)
                {
                    float radians = i * Mathf.PI * 2f / segments;
                    mesh.AddVert(center + new Vector2(cx + Mathf.Cos(radians) * radius,
                        cy + Mathf.Sin(radians) * radius) * scale, tint, Vector2.zero);
                    if (i > 0) mesh.AddTriangle(first, first + i, first + i + 1);
                }
            }
        }
    }
}
