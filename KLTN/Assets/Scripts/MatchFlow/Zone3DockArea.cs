using UnityEngine;

namespace EchoProtocol.Networking
{
    [ExecuteAlways]
    [RequireComponent(typeof(BoxCollider), typeof(LineRenderer))]
    public sealed class Zone3DockArea : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float containmentInset = 0.05f;
        [SerializeField, Min(0f)] private float clearance = 1f;
        [SerializeField] private Color waitingColor = new Color(0.05f, 0.8f, 1f);
        [SerializeField] private Color readyColor = new Color(0.1f, 1f, 0.35f);
        [SerializeField, Min(0.01f)] private float lineWidth = 0.12f;

        private BoxCollider _area;
        private LineRenderer _line;
        private MaterialPropertyBlock _lineProperties;

        private void OnEnable()
        {
            RefreshLine();
        }

        private void OnValidate()
        {
            RefreshLine();
        }

        public void MatchShipFootprint(Collider ship)
        {
            if (ship == null) return;
            if (_area == null) _area = GetComponent<BoxCollider>();
            if (_area == null) return;
            Vector3 shipSize = ship.bounds.size;
            Vector3 scale = transform.lossyScale;
            _area.size = new Vector3(
                shipSize.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)) + 2f * clearance,
                _area.size.y,
                shipSize.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)) + 2f * clearance);
            RefreshLine();
        }

        public bool FullyContains(Collider ship)
        {
            if (ship == null) return false;
            if (_area == null) _area = GetComponent<BoxCollider>();
            if (_area == null || !_area.enabled) return false;

            if (ship is BoxCollider box)
            {
                Vector3 half = box.size * 0.5f;
                for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    var local = box.center + new Vector3(x * half.x, 0f, z * half.z);
                    if (!ContainsPoint(ship.transform.TransformPoint(local))) return false;
                }
                return true;
            }

            // For other collider shapes, their world bounds give a conservative footprint.
            Bounds bounds = ship.bounds;
            for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                var point = bounds.center + new Vector3(x * bounds.extents.x, 0f, z * bounds.extents.z);
                if (!ContainsPoint(point)) return false;
            }
            return true;
        }

        public void SetVisualState(bool visible, bool ready)
        {
            if (_line == null) _line = GetComponent<LineRenderer>();
            if (_line == null) return;
            _line.enabled = visible;
            var color = ready ? readyColor : waitingColor;
            _line.startColor = color;
            _line.endColor = color;
            if (_lineProperties == null) _lineProperties = new MaterialPropertyBlock();
            _lineProperties.SetColor("_BaseColor", color);
            _line.SetPropertyBlock(_lineProperties);
        }

        private bool ContainsPoint(Vector3 worldPoint)
        {
            Vector3 point = transform.InverseTransformPoint(worldPoint) - _area.center;
            Vector3 half = _area.size * 0.5f;
            return Mathf.Abs(point.x) <= half.x - containmentInset
                && Mathf.Abs(point.z) <= half.z - containmentInset;
        }

        private void RefreshLine()
        {
            _area = GetComponent<BoxCollider>();
            _line = GetComponent<LineRenderer>();
            if (_area == null || _line == null) return;
            _area.isTrigger = true;
            Vector3 center = _area.center;
            Vector3 half = _area.size * 0.5f;
            float y = center.y - half.y + 0.08f;
            _line.useWorldSpace = false;
            _line.loop = false;
            _line.positionCount = 5;
            _line.widthMultiplier = lineWidth;
            _line.SetPosition(0, new Vector3(center.x - half.x, y, center.z - half.z));
            _line.SetPosition(1, new Vector3(center.x + half.x, y, center.z - half.z));
            _line.SetPosition(2, new Vector3(center.x + half.x, y, center.z + half.z));
            _line.SetPosition(3, new Vector3(center.x - half.x, y, center.z + half.z));
            _line.SetPosition(4, _line.GetPosition(0));
        }
    }
}
