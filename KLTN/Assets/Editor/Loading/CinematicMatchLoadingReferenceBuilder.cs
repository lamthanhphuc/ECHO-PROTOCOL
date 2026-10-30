using System;
using System.IO;
using System.Text;
using EchoProtocol.UI.Loading;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace EchoProtocol.Editor.Loading
{
    /// <summary>Builds reference artwork, typography and an editable UI prefab without editing a game scene.</summary>
    public static class CinematicMatchLoadingReferenceBuilder
    {
        public const string Folder = "Assets/Resources/UI/Loading";
        public const string PrefabPath = Folder + "/CinematicMatchLoadingScreen.prefab";
        public const string BackgroundPath = Folder + "/ReferenceFacilityBackground.png";
        public const string LogoPath = Folder + "/ReferenceEchoLogo.png";
        private const string ProgressPath = Folder + "/ProgressMask.png";
        private const string Typography = Folder + "/Typography";
        private const string SpectralSource = "Assets/Fonts/Lobby/Spectral-Regular.ttf";
        private const string CourierSource = "Assets/Fonts/Loading/CourierPrime-Regular.ttf";

        [MenuItem("ECHO PROTOCOL/Loading Screen/Apply Reference Design")]
        public static void ApplyReferenceDesign()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
                || EditorApplication.isUpdating)
                throw new InvalidOperationException("Apply the loading design in Edit mode after compilation and import finish.");

            // Validate both generated assets before changing the shared theme or material assets.
            RequireFile(BackgroundPath);
            RequireFile(LogoPath);
            RequireFile(SpectralSource);
            RequireFile(CourierSource);
            var logoShader = RequireShader("ECHO/Loading/LogoSignal");
            var bloodShader = RequireShader("ECHO/Loading/BloodProgress");
            var atmosphereShader = RequireShader("ECHO/Loading/Atmosphere");

            EnsureFolder(Typography);
            var background = ImportSprite(BackgroundPath);
            var logo = ImportSprite(LogoPath);
            var handprintPath = Folder + "/ReferenceHandprint.png";
            if (File.Exists(handprintPath)) ImportSprite(handprintPath);
            var progress = EnsureProgressSprite();
            var serif = EnsureFont(SpectralSource, Typography + "/SpectralLoadingTMP.asset", "Spectral Loading");
            var typewriter = EnsureFont(CourierSource, Typography + "/CourierPrimeLoadingTMP.asset", "Courier Prime Loading");
            var logoMaterial = EnsureMaterial(Folder + "/LogoSignal.mat", logoShader);
            SetColor(logoMaterial, "_Color", Color.white);
            SetFloat(logoMaterial, "_GlitchIntensity", 0.055f);
            var bloodMaterial = EnsureMaterial(Folder + "/BloodProgress.mat", bloodShader);
            SetColor(bloodMaterial, "_Color", Color.white);
            var atmosphereMaterial = EnsureMaterial(Folder + "/Atmosphere.mat", atmosphereShader);
            SetColor(atmosphereMaterial, "_Color", Color.white);
            SetFloat(atmosphereMaterial, "_Intensity", 1f);
            SetFloat(atmosphereMaterial, "_FogIntensity", 0.03f);
            SetFloat(atmosphereMaterial, "_ParticleIntensity", 0.10f);
            SetFloat(atmosphereMaterial, "_RedFlickerIntensity", 0.025f);

            var theme = CinematicMatchLoadingTools.EnsureTheme();
            var serialized = new SerializedObject(theme);
            Reference(serialized, "background", background);
            Reference(serialized, "logo", logo);
            Reference(serialized, "logoMaterial", logoMaterial);
            Reference(serialized, "progressFillMaterial", bloodMaterial);
            Reference(serialized, "atmosphereMaterial", atmosphereMaterial);
            Reference(serialized, "progressSprite", progress);
            Reference(serialized, "headingFont", serif);
            Reference(serialized, "telemetryFont", typewriter);
            ColorValue(serialized, "backgroundTint", Color.white);
            ColorValue(serialized, "darkness", new Color(0.002f, 0.003f, 0.005f, 0.10f));
            ColorValue(serialized, "text", new Color(0.91f, 0.89f, 0.85f, 1f));
            ColorValue(serialized, "muted", new Color(0.62f, 0.62f, 0.60f, 1f));
            ColorValue(serialized, "bloodRed", new Color(0.80f, 0.015f, 0.025f, 1f));
            FloatValue(serialized, "staticIntensity", 0.04f);
            FloatValue(serialized, "glitchIntensity", 0.055f);
            FloatValue(serialized, "fogIntensity", 0.03f);
            FloatValue(serialized, "particleIntensity", 0.10f);
            FloatValue(serialized, "backgroundMotion", 0.22f);
            FloatValue(serialized, "redFlickerIntensity", 0.025f);
            FloatValue(serialized, "fadeInSeconds", 0.65f);
            FloatValue(serialized, "completeHoldSeconds", 0.50f);
            FloatValue(serialized, "fadeOutSeconds", 0.80f);
            FloatValue(serialized, "letterboxHeight", 0f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            BuildPrefab(theme);
            AssetDatabase.SaveAssets();
            Debug.Log("[MatchLoading] Reference design applied. Editable prefab: " + PrefabPath
                + "; shared visual settings: " + CinematicMatchLoadingTools.ThemePath);
        }

        [MenuItem("ECHO PROTOCOL/Loading Screen/Select Editable Loading Prefab")]
        public static void SelectPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
                throw new InvalidOperationException("Apply Reference Design before selecting its prefab.");
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }

        private static void BuildPrefab(CinematicMatchLoadingTheme theme)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var owner = new GameObject("CinematicMatchLoadingScreen", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(owner, scene);
                var screen = owner.AddComponent<CinematicMatchLoadingScreen>();
                screen.PrepareAuthoringPrefab(theme);
                var prefab = PrefabUtility.SaveAsPrefabAsset(owner, PrefabPath);
                if (prefab == null) throw new InvalidOperationException("Could not save the editable loading prefab.");
            }
            finally
            {
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static Sprite EnsureProgressSprite()
        {
            if (!File.Exists(ProgressPath))
            {
                var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                try
                {
                    var pixels = new Color32[64];
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
                    texture.SetPixels32(pixels);
                    texture.Apply();
                    File.WriteAllBytes(ProgressPath, texture.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(texture); }
            }
            return ImportSprite(ProgressPath);
        }

        private static Sprite ImportSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Could not import loading artwork: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Loading sprite is missing after import: " + path);
            return sprite;
        }

        private static TMP_FontAsset EnsureFont(string sourcePath, string path, string name)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;
            AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceSynchronousImport);
            var source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (source == null) throw new InvalidOperationException("Loading source font could not be imported: " + sourcePath);
            var font = TMP_FontAsset.CreateFontAsset(source);
            if (font == null) throw new InvalidOperationException("Could not create loading font: " + name);
            font.name = name;
            var characters = new StringBuilder();
            for (int i = 32; i < 127; i++) characters.Append((char)i);
            font.TryAddCharacters(characters.ToString(), out _);
            if (!font.HasCharacters(characters.ToString(), out uint[] missing))
            {
                Object.DestroyImmediate(font);
                throw new InvalidOperationException(name + " does not contain required glyphs: " + string.Join(", ", missing));
            }
            AssetDatabase.CreateAsset(font, path);
            if (font.material != null && !AssetDatabase.Contains(font.material))
                AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures)
                if (atlas != null && !AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, font);
            EditorUtility.SetDirty(font);
            foreach (var atlas in font.atlasTextures)
                if (atlas != null) EditorUtility.SetDirty(atlas);
            return font;
        }

        private static Material EnsureMaterial(string path, Shader shader)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader) material.shader = shader;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetColor(Material material, string property, Color color)
        {
            if (material.HasProperty(property)) material.SetColor(property, color);
        }
        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }
        private static SerializedProperty Property(SerializedObject target, string name) =>
            target.FindProperty(name) ?? throw new InvalidOperationException("Loading theme property is missing: " + name);
        private static void Reference(SerializedObject target, string name, Object value) =>
            Property(target, name).objectReferenceValue = value;
        private static void ColorValue(SerializedObject target, string name, Color value) => Property(target, name).colorValue = value;
        private static void FloatValue(SerializedObject target, string name, float value) => Property(target, name).floatValue = value;
        private static Shader RequireShader(string name)
        {
            var shader = Shader.Find(name);
            if (shader == null) throw new InvalidOperationException("Loading shader is unavailable: " + name);
            return shader;
        }
        private static void RequireFile(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Required loading design asset is missing.", path);
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) throw new InvalidOperationException("Invalid asset folder: " + path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
