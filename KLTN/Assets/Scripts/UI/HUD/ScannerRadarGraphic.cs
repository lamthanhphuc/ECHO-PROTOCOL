using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.HUD
{
    // A single reusable mesh; no marker GameObjects or per-frame Instantiate/Destroy.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ScannerRadarGraphic : MaskableGraphic
    {
        private readonly List<Vector2> _points = new List<Vector2>();
        private readonly List<HUDPurgeIcon> _icons = new List<HUDPurgeIcon>();
        public string TargetLabel { get; set; }
        private Color _accent = new Color(0.35f, 0.78f, 0.76f);
        private bool _motion, _scanning;
        private float _pulse;
        private static readonly Vector2[] Corners = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };

        public void Present(IReadOnlyList<Vector3> offsets, float range, float heading, bool motion, bool scanning)
        {
            _points.Clear();
            if (offsets != null && range > 0f)
            {
                Quaternion worldToRadar = Quaternion.Euler(0f, -heading, 0f);
                for (int i = 0; i < offsets.Count; i++)
                {
                    Vector3 localOffset = worldToRadar * offsets[i];
                    _points.Add(Vector2.ClampMagnitude(new Vector2(localOffset.x, localOffset.z) / range, 1f));
                }
            }
            _motion = motion;
            _scanning = scanning;
            while (_icons.Count < _points.Count)
            {
                var go = new GameObject("TargetIcon", typeof(RectTransform), typeof(HUDPurgeIcon));
                go.transform.SetParent(transform, false);
                var icon = go.GetComponent<HUDPurgeIcon>();
                icon.raycastTarget = false;
                _icons.Add(icon);
            }
            string label = (TargetLabel ?? "").ToUpperInvariant();
            PurgeIcon kind = motion ? PurgeIcon.Alien
                : label.Contains("MEAT") ? PurgeIcon.Meat
                : label.Contains("FUEL") || label.Contains("JERRYCAN") || label.Contains("NHIÊN") ? PurgeIcon.Fuel
                : label.Contains("SECURITY") ? PurgeIcon.Terminal
                : label.Contains("RELAY") ? PurgeIcon.Relay
                : label.Contains("TÀU") || label.Contains("CART") ? PurgeIcon.Cart : PurgeIcon.Core;
            float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .44f - 9;
            for (int i = 0; i < _icons.Count; i++)
            {
                _icons[i].gameObject.SetActive(i < _points.Count);
                if (i >= _points.Count) continue;
                _icons[i].Kind = kind;
                _icons[i].color = motion ? new Color(.94f,.43f,.29f) : new Color(.35f,.78f,.76f);
                var rect = _icons[i].rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
                rect.sizeDelta = new Vector2(22,22);
                rect.anchoredPosition = _points[i] * radius;
            }
            _accent = motion ? new Color(0.94f, 0.43f, 0.29f) : new Color(0.35f, 0.78f, 0.76f);
            SetVerticesDirty();
        }

        private void Update()
        {
            if (!_scanning || canvasRenderer.cull || canvasRenderer.GetInheritedAlpha() <= 0.005f) return;
            _pulse = Mathf.Repeat(Time.unscaledTime * 0.55f, 1f);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float r = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.44f;
            Vector2 center = rectTransform.rect.center;
            Color grid = new Color(_accent.r, _accent.g, _accent.b, 0.18f);
            for (int i = -4; i <= 4; i++)
            {
                float x = r * i / 5f;
                float extent = Mathf.Sqrt(r * r - x * x);
                Line(vh, center + new Vector2(x, -extent), center + new Vector2(x, extent), 0.8f, grid);
                Line(vh, center + new Vector2(-extent, x), center + new Vector2(extent, x), 0.8f, grid);
            }
            for (int i = 1; i <= 4; i++)
                Ring(vh, center, r * i / 4f, 1f, new Color(_accent.r, _accent.g, _accent.b, i == 4 ? 0.65f : 0.25f));
            if (_scanning)
                Ring(vh, center, r * _pulse, 1f, new Color(_accent.r, _accent.g, _accent.b, (1f - _pulse) * 0.4f));
            Vector2 forward = Vector2.up;
            Vector2 side = Vector2.right;
            Vector2 tip = center + forward * 10f;
            Vector2 left = center - forward * 6f - side * 6f;
            Vector2 right = center - forward * 6f + side * 6f;
            Line(vh, tip, left, 2f, Color.white);
            Line(vh, left, center - forward * 2f, 2f, Color.white);
            Line(vh, center - forward * 2f, right, 2f, Color.white);
            Line(vh, right, tip, 2f, Color.white);
        }

        private static void Ring(VertexHelper vh, Vector2 center, float radius, float width, Color ink)
        {
            const int segments = 64;
            Vector2 previous = center + Vector2.up * radius;
            for (int i = 1; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                Vector2 next = center + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * radius;
                Line(vh, previous, next, width, ink);
                previous = next;
            }
        }

        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color ink)
        {
            Vector2 d = b - a;
            Vector2 n = new Vector2(-d.y, d.x).normalized * width * 0.5f;
            int start = vh.currentVertCount;
            vh.AddVert(a - n, ink, Vector2.zero);
            vh.AddVert(a + n, ink, Vector2.zero);
            vh.AddVert(b + n, ink, Vector2.zero);
            vh.AddVert(b - n, ink, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
