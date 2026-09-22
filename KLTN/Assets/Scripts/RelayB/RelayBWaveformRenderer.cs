using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.RelayB
{
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RelayBWaveformRenderer : MaskableGraphic
    {
        [Header("Waveform Settings")]
        [SerializeField] private WaveformType waveformType = WaveformType.Sine;
        [SerializeField, Range(1f, 120f)] private float frequency = 40f;
        [SerializeField, Range(0f, 360f)] private float phase = 0f;
        [SerializeField, Range(0f, 2f)] private float amplitude = 1f;
        [SerializeField, Range(1f, 8f)] private float lineWidth = 3.5f;
        [SerializeField] private Color waveformColor = new Color(0.2f, 0.9f, 1f, 1f);

        [Header("Grid Display")]
        [SerializeField] private bool showGrid = true;
        [SerializeField] private Color gridColor = new Color(0.12f, 0.45f, 0.5f, 0.35f);
        [SerializeField] private Color axisColor = new Color(0.18f, 0.65f, 0.72f, 0.6f);
        [SerializeField, Range(2, 10)] private int verticalGridDivisions = 6;
        [SerializeField, Range(2, 8)] private int horizontalGridDivisions = 4;

        [Header("Effects")]
        [SerializeField, Range(0f, 1f)] private float noiseIntensity = 0f;
        [SerializeField] private bool animateTime = true;
        [SerializeField, Range(0f, 5f)] private float animationSpeed = 0.5f;

        private float _timeOffset;

        public WaveformType Waveform => waveformType;
        public float Frequency => frequency;
        public float Phase => phase;
        public float Amplitude => amplitude;
        public float LineWidth => lineWidth;
        public Color WaveformColor => waveformColor;

        public override Texture mainTexture => s_WhiteTexture;

        protected override void Awake()
        {
            base.Awake();
            useLegacyMeshGeneration = false;
            raycastTarget = false;
            color = Color.white;
        }

        private void Update()
        {
            if (animateTime && Application.isPlaying)
            {
                _timeOffset += Time.deltaTime * animationSpeed;
                SetVerticesDirty();
            }
        }

        public void SetWaveParameters(WaveformType type, float freq, float phaseDeg, float amp = 1f)
        {
            waveformType = type;
            frequency = Mathf.Max(1f, freq);
            phase = phaseDeg;
            amplitude = Mathf.Clamp(amp, 0f, 2f);
            SetVerticesDirty();
        }

        public void SetWaveformColor(Color color)
        {
            waveformColor = color;
            SetVerticesDirty();
        }

        public void SetNoise(float noise)
        {
            noiseIntensity = Mathf.Clamp01(noise);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 1f || rect.height <= 1f)
            {
                return;
            }

            if (showGrid)
            {
                DrawGrid(vh, rect);
            }

            DrawWaveform(vh, rect);
        }

        private void DrawGrid(VertexHelper vh, Rect rect)
        {
            float halfWidth = 0.75f;

            // Vertical division lines
            for (int i = 1; i < verticalGridDivisions; i++)
            {
                float x = rect.xMin + (rect.width * i / verticalGridDivisions);
                AddLineQuad(vh, new Vector2(x, rect.yMin), new Vector2(x, rect.yMax), halfWidth, gridColor);
            }

            // Horizontal division lines
            for (int j = 1; j < horizontalGridDivisions; j++)
            {
                float y = rect.yMin + (rect.height * j / horizontalGridDivisions);
                AddLineQuad(vh, new Vector2(rect.xMin, y), new Vector2(rect.xMax, y), halfWidth, gridColor);
            }

            // Center axis (time baseline)
            float centerY = rect.center.y;
            AddLineQuad(vh, new Vector2(rect.xMin, centerY), new Vector2(rect.xMax, centerY), 1.25f, axisColor);
        }

        private void DrawWaveform(VertexHelper vh, Rect rect)
        {
            const int sampleCount = 120;
            float step = rect.width / (sampleCount - 1);
            float maxWaveAmp = rect.height * 0.42f * amplitude;
            float cycles = Mathf.Clamp(frequency / 18f, 1f, 8f);
            float phaseRad = (phase + (_timeOffset * 45f)) * Mathf.Deg2Rad;

            Vector2 prevPoint = Vector2.zero;

            for (int i = 0; i < sampleCount; i++)
            {
                float x = rect.xMin + (i * step);
                float t = (float)i / (sampleCount - 1);
                float theta = (t * Mathf.PI * 2f * cycles) + phaseRad;

                float sample = EvaluateWave(theta, waveformType);

                if (noiseIntensity > 0.001f)
                {
                    float noise = (Mathf.PerlinNoise(t * 12f, _timeOffset * 6f) - 0.5f) * 2f;
                    sample += noise * noiseIntensity * 0.35f;
                }

                float y = rect.center.y + (sample * maxWaveAmp);
                Vector2 curPoint = new Vector2(x, y);

                if (i > 0)
                {
                    AddLineQuad(vh, prevPoint, curPoint, lineWidth * 0.5f, waveformColor);
                }

                prevPoint = curPoint;
            }
        }

        public static float EvaluateWave(float theta, WaveformType type)
        {
            switch (type)
            {
                case WaveformType.Sine:
                    return Mathf.Sin(theta);

                case WaveformType.Square:
                    float sin = Mathf.Sin(theta);
                    return sin >= 0f ? 0.85f : -0.85f;

                case WaveformType.Triangle:
                    // 2/pi * asin(sin(theta))
                    return (2f / Mathf.PI) * Mathf.Asin(Mathf.Clamp(Mathf.Sin(theta), -1f, 1f));

                case WaveformType.Sawtooth:
                    // 2 * (theta / 2pi - floor(theta / 2pi + 0.5))
                    float norm = (theta / (Mathf.PI * 2f));
                    return 2f * (norm - Mathf.Floor(norm + 0.5f));

                case WaveformType.CompositeHarmonic:
                    // Multi-harmonic composite wave
                    float composite = Mathf.Sin(theta) + (0.35f * Mathf.Sin(theta * 3f)) + (0.2f * Mathf.Cos(theta * 2f));
                    return composite / 1.35f;

                default:
                    return Mathf.Sin(theta);
            }
        }

        private static void AddLineQuad(VertexHelper vh, Vector2 start, Vector2 end, float halfThickness, Color color)
        {
            Vector2 direction = (end - start).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * halfThickness;

            int index = vh.currentVertCount;

            UIVertex v1 = UIVertex.simpleVert;
            v1.color = color;
            v1.position = start + normal;
            v1.uv0 = new Vector2(0.5f, 0.5f);

            UIVertex v2 = UIVertex.simpleVert;
            v2.color = color;
            v2.position = end + normal;
            v2.uv0 = new Vector2(0.5f, 0.5f);

            UIVertex v3 = UIVertex.simpleVert;
            v3.color = color;
            v3.position = end - normal;
            v3.uv0 = new Vector2(0.5f, 0.5f);

            UIVertex v4 = UIVertex.simpleVert;
            v4.color = color;
            v4.position = start - normal;
            v4.uv0 = new Vector2(0.5f, 0.5f);

            vh.AddVert(v1);
            vh.AddVert(v2);
            vh.AddVert(v3);
            vh.AddVert(v4);

            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index + 2, index + 3, index);
        }
    }
}

