using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI
{
    public enum SettingsIcon
    {
        Gear, Microphone, Mouse, Audio, Keyboard, Monitor, Team, Interact, Run, Crouch,
        Flashlight, Inventory, Up, Down, Left, Right, Play, Exit, Dot, Pill
    }

    /// <summary>Small vector icons, independent of installed fonts and texture resolution.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SettingsIconGraphic : MaskableGraphic
    {
        [SerializeField] private SettingsIcon _icon;
        private VertexHelper _mesh;
        private Rect _bounds;

        public void Configure(SettingsIcon icon, Color tint)
        {
            _icon = icon;
            color = tint;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            _mesh = mesh;
            _bounds = GetPixelAdjustedRect();
            switch (_icon)
            {
                case SettingsIcon.Gear:
                    Arc(50, 50, 28, 0, 360, 9);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4;
                        Stroke(50 + Mathf.Cos(a) * 30, 50 + Mathf.Sin(a) * 30, 50 + Mathf.Cos(a) * 43, 50 + Mathf.Sin(a) * 43, 11);
                    }
                    break;
                case SettingsIcon.Microphone:
                    Arc(50, 68, 16, 0, 180); Arc(50, 46, 16, 180, 360);
                    Stroke(34, 46, 34, 68); Stroke(66, 46, 66, 68);
                    Arc(50, 44, 28, 180, 360); Stroke(22, 44, 22, 56); Stroke(78, 44, 78, 56);
                    Stroke(50, 16, 50, 5); Stroke(32, 5, 68, 5); break;
                case SettingsIcon.Mouse:
                    Arc(50, 65, 29, 0, 180); Arc(50, 34, 29, 180, 360);
                    Stroke(21, 34, 21, 65); Stroke(79, 34, 79, 65);
                    Stroke(21, 59, 79, 59); Stroke(50, 59, 50, 94); Stroke(50, 68, 50, 77, 8); break;
                case SettingsIcon.Audio:
                    Polygon(new[] { new Vector2(12, 37), new Vector2(28, 37), new Vector2(52, 17), new Vector2(52, 83), new Vector2(28, 63), new Vector2(12, 63) });
                    Arc(51, 50, 25, -55, 55); Arc(51, 50, 40, -55, 55); break;
                case SettingsIcon.Keyboard:
                    Box(6, 19, 88, 61);
                    for (int row = 0; row < 3; row++)
                        for (int col = 0; col < 6; col++) Stroke(17 + col * 13, 69 - row * 14, 22 + col * 13, 69 - row * 14, 5);
                    Stroke(26, 28, 74, 28, 5); break;
                case SettingsIcon.Monitor:
                    Box(7, 29, 86, 57); Stroke(50, 29, 50, 12); Stroke(28, 12, 72, 12); break;
                case SettingsIcon.Team:
                    Disc(50, 70, 13); Disc(21, 62, 10); Disc(79, 62, 10);
                    Arc(50, 30, 21, 0, 180, 7); Arc(19, 23, 14, 0, 150, 6); Arc(81, 23, 14, 30, 180, 6);
                    Stroke(29, 30, 29, 15); Stroke(71, 30, 71, 15); Stroke(29, 15, 71, 15); break;
                case SettingsIcon.Interact:
                    Stroke(34, 44, 34, 86); Stroke(46, 44, 46, 94); Stroke(58, 44, 58, 91); Stroke(70, 44, 70, 78);
                    Stroke(34, 44, 18, 59); Stroke(18, 59, 13, 50); Stroke(13, 50, 37, 16);
                    Stroke(37, 16, 66, 16); Stroke(66, 16, 76, 43); Stroke(76, 43, 76, 62); break;
                case SettingsIcon.Run:
                    Disc(63, 85, 11); Stroke(56, 68, 43, 44, 8); Stroke(55, 65, 77, 54, 6);
                    Stroke(53, 67, 34, 69, 6); Stroke(34, 69, 21, 52, 6); Stroke(43, 44, 65, 29, 7);
                    Stroke(65, 29, 61, 8, 7); Stroke(43, 44, 29, 21, 7); Stroke(29, 21, 9, 20, 7); break;
                case SettingsIcon.Crouch:
                    Disc(60, 78, 11); Stroke(56, 61, 36, 43, 8); Stroke(53, 57, 76, 40, 6);
                    Stroke(36, 43, 64, 29, 8); Stroke(64, 29, 47, 12, 7); Stroke(47, 12, 76, 12, 6);
                    Stroke(36, 43, 23, 25, 7); Stroke(23, 25, 23, 10, 7); break;
                case SettingsIcon.Flashlight:
                    Polygon(new[] { new Vector2(15, 24), new Vector2(26, 13), new Vector2(60, 47), new Vector2(49, 58) });
                    Polygon(new[] { new Vector2(44, 63), new Vector2(65, 42), new Vector2(73, 57), new Vector2(59, 71) });
                    Stroke(70, 81, 78, 89, 3); Stroke(80, 71, 91, 76, 3); Stroke(58, 86, 61, 96, 3); break;
                case SettingsIcon.Inventory:
                    Box(19, 16, 62, 59); Arc(50, 75, 17, 0, 180); Box(31, 27, 38, 22); break;
                case SettingsIcon.Up: Arrow(0); break;
                case SettingsIcon.Down: Arrow(180); break;
                case SettingsIcon.Left: Arrow(90); break;
                case SettingsIcon.Right: Arrow(-90); break;
                case SettingsIcon.Play:
                    Polygon(new[] { new Vector2(24, 13), new Vector2(86, 50), new Vector2(24, 87) }); break;
                case SettingsIcon.Exit:
                    Stroke(45, 88, 17, 88); Stroke(17, 88, 17, 12); Stroke(17, 12, 45, 12);
                    Stroke(38, 50, 91, 50); Stroke(73, 68, 91, 50); Stroke(73, 32, 91, 50); break;
                case SettingsIcon.Dot: Disc(50, 50, 48); break;
                case SettingsIcon.Pill:
                    float radius = _bounds.height / _bounds.width * 50;
                    var points = new Vector2[34];
                    for (int i = 0; i <= 16; i++)
                    {
                        float angle = (-90 + i * 180f / 16) * Mathf.Deg2Rad;
                        points[i] = new Vector2(100 - radius + Mathf.Cos(angle) * radius, 50 + Mathf.Sin(angle) * 50);
                        points[17 + i] = new Vector2(radius - Mathf.Cos(angle) * radius, 50 - Mathf.Sin(angle) * 50);
                    }
                    Polygon(points); break;
            }
        }

        private void Arrow(float angle)
        {
            var p = new[] { new Vector2(0, -34), new Vector2(0, 32), new Vector2(-22, 8), new Vector2(0, 32), new Vector2(22, 8) };
            var rotation = Quaternion.Euler(0, 0, angle);
            for (int i = 0; i < p.Length; i++) p[i] = (Vector2)(rotation * p[i]) + new Vector2(50, 50);
            for (int i = 1; i < p.Length; i++) Stroke(p[i - 1].x, p[i - 1].y, p[i].x, p[i].y, 7);
        }
        private void Box(float x, float y, float width, float height)
        {
            Stroke(x, y, x + width, y); Stroke(x + width, y, x + width, y + height);
            Stroke(x + width, y + height, x, y + height); Stroke(x, y + height, x, y);
        }
        private void Arc(float x, float y, float radius, float start, float end, float thickness = 5)
        {
            for (int i = 0; i < 24; i++)
            {
                float a = Mathf.Lerp(start, end, i / 24f) * Mathf.Deg2Rad;
                float b = Mathf.Lerp(start, end, (i + 1) / 24f) * Mathf.Deg2Rad;
                Stroke(x + Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius, x + Mathf.Cos(b) * radius, y + Mathf.Sin(b) * radius, thickness);
            }
        }
        private void Disc(float x, float y, float radius)
        {
            var points = new Vector2[32];
            for (int i = 0; i < points.Length; i++)
            {
                float a = i * Mathf.PI * 2 / points.Length;
                points[i] = new Vector2(x + Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius);
            }
            Polygon(points);
        }
        private void Stroke(float x1, float y1, float x2, float y2, float thickness = 5)
        {
            var a = new Vector2(x1, y1); var b = new Vector2(x2, y2);
            var normal = new Vector2(-(b - a).y, (b - a).x).normalized * thickness * 0.5f;
            Polygon(new[] { a - normal, b - normal, b + normal, a + normal });
        }
        private void Polygon(Vector2[] points)
        {
            int start = _mesh.currentVertCount;
            foreach (var point in points)
                _mesh.AddVert(new Vector2(_bounds.xMin + point.x * _bounds.width / 100, _bounds.yMin + point.y * _bounds.height / 100), color, Vector2.zero);
            for (int i = 1; i < points.Length - 1; i++) _mesh.AddTriangle(start, start + i, start + i + 1);
        }
    }
}
