using TMPro;
using UnityEngine;

namespace EchoProtocol.UI.Loading
{
    /// <summary>Visual settings shared by the persistent match loading screen and its Editor preview.</summary>
    [CreateAssetMenu(menuName = "ECHO PROTOCOL/Cinematic Match Loading Theme")]
    public sealed class CinematicMatchLoadingTheme : ScriptableObject
    {
        public const string ResourcePath = "UI/CinematicMatchLoadingTheme";

        [Header("Existing ECHO PROTOCOL artwork")]
        [SerializeField] private Sprite background;
        [SerializeField] private Sprite logo;
        [SerializeField] private Material logoMaterial;
        [SerializeField] private Material progressFillMaterial;
        [SerializeField] private Material atmosphereMaterial;
        [SerializeField] private Sprite progressSprite;
        [SerializeField] private TMP_FontAsset titleFont;
        [SerializeField] private Material titleMaterial;
        [SerializeField] private TMP_FontAsset headingFont;
        [SerializeField] private TMP_FontAsset telemetryFont;
        [Header("Palette")]
        [SerializeField] private Color backgroundTint = Color.white;
        [SerializeField] private Color darkness = new Color(0.014f, 0.002f, 0.004f, 0.1f);
        [SerializeField] private Color text = new Color(0.84f, 0.73f, 0.72f, 1f);
        [SerializeField] private Color muted = new Color(0.46f, 0.43f, 0.44f, 1f);
        [SerializeField] private Color bloodRed = new Color(0.94f, 0.018f, 0.04f, 1f);
        [Header("Cinematic motion (unscaled time)")]
        [SerializeField, Range(0, 1)] private float staticIntensity = 0.035f;
        [SerializeField, Range(0, 1)] private float glitchIntensity = 0.22f;
        [SerializeField, Range(0, 1)] private float fogIntensity = 0.6f;
        [SerializeField, Range(0, 1)] private float particleIntensity = 0.3f;
        [SerializeField, Range(0, 1)] private float backgroundMotion = 0.3f;
        [SerializeField, Range(0, 1)] private float redFlickerIntensity = 0.24f;
        [SerializeField, Min(0)] private float fadeInSeconds = 0.25f;
        [SerializeField, Min(0)] private float completeHoldSeconds = 0.45f;
        [SerializeField, Min(0)] private float fadeOutSeconds = 0.65f;
        [SerializeField, Range(0, 100)] private float letterboxHeight;

        public Sprite Background => background;
        public Sprite Logo => logo;
        public Material LogoMaterial => logoMaterial;
        public Material ProgressFillMaterial => progressFillMaterial;
        public Material AtmosphereMaterial => atmosphereMaterial;
        public Sprite ProgressSprite => progressSprite;
        public TMP_FontAsset TitleFont => titleFont;
        public Material TitleMaterial => titleMaterial;
        public TMP_FontAsset HeadingFont => headingFont;
        public TMP_FontAsset TelemetryFont => telemetryFont;
        public Color BackgroundTint => backgroundTint;
        public Color Darkness => darkness;
        public Color Text => text;
        public Color Muted => muted;
        public Color BloodRed => bloodRed;
        public float StaticIntensity => staticIntensity;
        public float GlitchIntensity => glitchIntensity;
        public float FogIntensity => fogIntensity;
        public float ParticleIntensity => particleIntensity;
        public float BackgroundMotion => backgroundMotion;
        public float RedFlickerIntensity => redFlickerIntensity;
        public float FadeInSeconds => fadeInSeconds;
        public float CompleteHoldSeconds => completeHoldSeconds;
        public float FadeOutSeconds => fadeOutSeconds;
        public float LetterboxHeight => letterboxHeight;
    }
}
