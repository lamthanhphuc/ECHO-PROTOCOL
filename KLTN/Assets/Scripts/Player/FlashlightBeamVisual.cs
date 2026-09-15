using UnityEngine;

[DisallowMultipleComponent]
public sealed class FlashlightBeamVisual : MonoBehaviour
{
    private const string BeamChildName = "Flashlight_Beam_Visual";
    private const int SegmentCount = 28;

    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Mesh _mesh;
    private Material _material;
    private float _lastLength = -1f;
    private float _lastSpotAngle = -1f;

    public void Configure(float length, float spotAngle, Color color, float alpha)
    {
        EnsureRenderer();

        length = Mathf.Max(0.5f, length);
        spotAngle = Mathf.Clamp(spotAngle, 1f, 175f);
        if (!Mathf.Approximately(_lastLength, length)
            || !Mathf.Approximately(_lastSpotAngle, spotAngle))
        {
            RebuildMesh(length, spotAngle);
        }

        if (_material != null)
        {
            color.a = Mathf.Clamp01(alpha);
            _material.SetColor("_Color", color);
        }
    }

    public void SetVisible(bool visible)
    {
        EnsureRenderer();
        if (_meshRenderer != null)
        {
            _meshRenderer.enabled = visible;
        }
    }

    private void EnsureRenderer()
    {
        if (_meshRenderer != null && _meshFilter != null)
        {
            return;
        }

        Transform beam = transform.Find(BeamChildName);
        if (beam == null)
        {
            var beamObject = new GameObject(BeamChildName);
            beamObject.transform.SetParent(transform, false);
            beamObject.transform.localPosition = Vector3.zero;
            beamObject.transform.localRotation = Quaternion.identity;
            beamObject.transform.localScale = Vector3.one;
            beam = beamObject.transform;
        }

        _meshFilter = beam.GetComponent<MeshFilter>();
        if (_meshFilter == null)
        {
            _meshFilter = beam.gameObject.AddComponent<MeshFilter>();
        }

        _meshRenderer = beam.GetComponent<MeshRenderer>();
        if (_meshRenderer == null)
        {
            _meshRenderer = beam.gameObject.AddComponent<MeshRenderer>();
        }

        _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _meshRenderer.receiveShadows = false;
        _meshRenderer.allowOcclusionWhenDynamic = false;

        if (_material == null)
        {
            Shader shader = Shader.Find("EchoProtocol/FlashlightBeam")
                ?? Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Sprites/Default");
            _material = new Material(shader)
            {
                name = "M_Runtime_FlashlightBeam"
            };
        }

        _meshRenderer.sharedMaterial = _material;
    }

    private void RebuildMesh(float length, float spotAngle)
    {
        _lastLength = length;
        _lastSpotAngle = spotAngle;

        if (_mesh == null)
        {
            _mesh = new Mesh
            {
                name = "Runtime_FlashlightBeam_Cone"
            };
            _mesh.MarkDynamic();
            _meshFilter.sharedMesh = _mesh;
        }

        float radius = Mathf.Tan(spotAngle * 0.5f * Mathf.Deg2Rad) * length;
        var vertices = new Vector3[SegmentCount + 2];
        var uvs = new Vector2[vertices.Length];
        var triangles = new int[SegmentCount * 6];

        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(0f, 0.5f);

        for (int i = 0; i < SegmentCount; i++)
        {
            float angle = (i / (float)SegmentCount) * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                length);
            uvs[i + 1] = new Vector2(1f, i / (float)SegmentCount);
        }

        vertices[vertices.Length - 1] = new Vector3(0f, 0f, length);
        uvs[vertices.Length - 1] = new Vector2(1f, 0.5f);

        int tri = 0;
        int centerIndex = vertices.Length - 1;
        for (int i = 0; i < SegmentCount; i++)
        {
            int next = i == SegmentCount - 1 ? 1 : i + 2;
            int current = i + 1;

            triangles[tri++] = 0;
            triangles[tri++] = current;
            triangles[tri++] = next;

            triangles[tri++] = centerIndex;
            triangles[tri++] = next;
            triangles[tri++] = current;
        }

        _mesh.Clear();
        _mesh.vertices = vertices;
        _mesh.uv = uvs;
        _mesh.triangles = triangles;
        _mesh.RecalculateBounds();
    }
}
