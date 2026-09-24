using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class RelayPrefabBuilder
{
    private const string RelayAFolder = "Assets/import/RelayA";
    private const string RelayBFolder = "Assets/import/RelayB";

    [MenuItem("Tools/ECHO Protocol/Build RelayA and RelayB Prefabs")]
    public static void BuildRelayPrefabs()
    {
        BuildRelay(
            "RelayA",
            RelayAFolder,
            "Assets/import/RelayA/source/5I57A_FAB.fbx",
            new Vector3(2.2f, 2.4f, 1.4f));

        BuildRelay(
            "RelayB",
            RelayBFolder,
            "Assets/import/RelayB/source/Desk_02.fbx",
            new Vector3(2.6f, 1.6f, 1.6f));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[RelayPrefabBuilder] RelayA and RelayB prefabs are ready.");
    }

    private static void BuildRelay(string relayName, string relayFolder, string sourcePath, Vector3 fallbackColliderSize)
    {
        EnsureFolder(relayFolder + "/materials");

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (source == null)
        {
            Debug.LogError($"[RelayPrefabBuilder] Missing source model for {relayName}: {sourcePath}");
            return;
        }

        ConfigureTextureImporters(relayFolder + "/textures");
        Dictionary<string, Material> materialsByKey = BuildMaterials(relayName, relayFolder);

        GameObject root = new GameObject(relayName);
        GameObject visual = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (visual == null)
        {
            UnityEngine.Object.DestroyImmediate(root);
            Debug.LogError($"[RelayPrefabBuilder] Could not instantiate source model for {relayName}: {sourcePath}");
            return;
        }

        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        AssignMaterials(visual, materialsByKey);
        EnsureCollider(root, fallbackColliderSize);

        string prefabPath = $"{relayFolder}/{relayName}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        AssetDatabase.ImportAsset(prefabPath);
        UnityEngine.Object.DestroyImmediate(root);

        Debug.Log($"[RelayPrefabBuilder] Saved {relayName} prefab: {prefabPath}");
    }

    private static Dictionary<string, Material> BuildMaterials(string relayName, string relayFolder)
    {
        Dictionary<string, TextureSet> textureSets = CollectTextureSets(relayFolder + "/textures");
        Dictionary<string, Material> materials = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<string, TextureSet> pair in textureSets)
        {
            TextureSet set = pair.Value;
            if (set.BaseColor == null && set.Normal == null && set.Metallic == null && set.Roughness == null && set.Emission == null)
            {
                continue;
            }

            string materialPath = $"{relayFolder}/materials/M_{relayName}_{SanitizeFileName(pair.Key)}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }

            material.name = Path.GetFileNameWithoutExtension(materialPath);
            ApplyTextureSet(material, set);
            EditorUtility.SetDirty(material);
            materials[pair.Key] = material;
        }

        return materials;
    }

    private static Dictionary<string, TextureSet> CollectTextureSets(string textureFolder)
    {
        Dictionary<string, TextureSet> sets = new Dictionary<string, TextureSet>(StringComparer.OrdinalIgnoreCase);
        string absoluteFolder = ToAbsolutePath(textureFolder);
        if (!Directory.Exists(absoluteFolder))
        {
            return sets;
        }

        string[] files = Directory.GetFiles(absoluteFolder, "*.png", SearchOption.TopDirectoryOnly);
        for (int i = 0; i < files.Length; i++)
        {
            string assetPath = ToAssetPath(files[i]);
            string name = Path.GetFileNameWithoutExtension(assetPath);
            MapKind(name, out string key, out TextureKind kind);
            if (kind == TextureKind.Unknown)
            {
                continue;
            }

            if (!sets.TryGetValue(key, out TextureSet set))
            {
                set = new TextureSet();
                sets.Add(key, set);
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            switch (kind)
            {
                case TextureKind.BaseColor:
                    set.BaseColor = texture;
                    break;
                case TextureKind.Normal:
                    set.Normal = texture;
                    break;
                case TextureKind.Metallic:
                    set.Metallic = texture;
                    break;
                case TextureKind.Roughness:
                    set.Roughness = texture;
                    break;
                case TextureKind.Emission:
                    set.Emission = texture;
                    break;
                case TextureKind.Alpha:
                    set.Alpha = texture;
                    break;
            }
        }

        return sets;
    }

    private static void ApplyTextureSet(Material material, TextureSet set)
    {
        if (set.BaseColor != null)
        {
            SetTextureIfExists(material, "_BaseMap", set.BaseColor);
            SetTextureIfExists(material, "_MainTex", set.BaseColor);
        }

        if (set.Normal != null)
        {
            SetTextureIfExists(material, "_BumpMap", set.Normal);
            material.EnableKeyword("_NORMALMAP");
        }

        if (set.Metallic != null)
        {
            SetTextureIfExists(material, "_MetallicGlossMap", set.Metallic);
            SetFloatIfExists(material, "_Metallic", 1f);
        }
        else
        {
            SetFloatIfExists(material, "_Metallic", 0.25f);
        }

        if (set.Roughness != null)
        {
            SetFloatIfExists(material, "_Smoothness", 0.35f);
        }

        if (set.Emission != null)
        {
            SetTextureIfExists(material, "_EmissionMap", set.Emission);
            SetColorIfExists(material, "_EmissionColor", Color.white);
            material.EnableKeyword("_EMISSION");
        }
    }

    private static void AssignMaterials(GameObject visual, Dictionary<string, Material> materialsByKey)
    {
        Material fallback = FindFallbackMaterial(materialsByKey);
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];
            Material[] slots = renderer.sharedMaterials;
            if (slots == null || slots.Length == 0)
            {
                if (fallback != null)
                {
                    renderer.sharedMaterial = fallback;
                }

                continue;
            }

            for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                Material slot = slots[slotIndex];
                Material mapped = slot != null ? FindMaterialForSlot(slot.name, materialsByKey) : null;
                slots[slotIndex] = mapped != null ? mapped : fallback;
            }

            renderer.sharedMaterials = slots;
        }
    }

    private static Material FindMaterialForSlot(string slotName, Dictionary<string, Material> materialsByKey)
    {
        string normalizedSlot = NormalizeKey(slotName);
        foreach (KeyValuePair<string, Material> pair in materialsByKey)
        {
            string normalizedKey = NormalizeKey(pair.Key);
            if (normalizedSlot.Contains(normalizedKey) || normalizedKey.Contains(normalizedSlot))
            {
                return pair.Value;
            }
        }

        return null;
    }

    private static Material FindFallbackMaterial(Dictionary<string, Material> materialsByKey)
    {
        if (materialsByKey.Count == 0)
        {
            return null;
        }

        foreach (KeyValuePair<string, Material> pair in materialsByKey)
        {
            if (pair.Key.IndexOf("black", StringComparison.OrdinalIgnoreCase) >= 0
                || pair.Key.IndexOf("desk", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return pair.Value;
            }
        }

        foreach (KeyValuePair<string, Material> pair in materialsByKey)
        {
            return pair.Value;
        }

        return null;
    }

    private static void ConfigureTextureImporters(string textureFolder)
    {
        string absoluteFolder = ToAbsolutePath(textureFolder);
        if (!Directory.Exists(absoluteFolder))
        {
            return;
        }

        string[] files = Directory.GetFiles(absoluteFolder, "*.png", SearchOption.TopDirectoryOnly);
        for (int i = 0; i < files.Length; i++)
        {
            string assetPath = ToAssetPath(files[i]);
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            bool changed = false;
            TextureImporterType desiredType = fileName.IndexOf("Normal", StringComparison.OrdinalIgnoreCase) >= 0
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;
            if (importer.textureType != desiredType)
            {
                importer.textureType = desiredType;
                changed = true;
            }

            bool desiredSrgb = fileName.IndexOf("BaseColor", StringComparison.OrdinalIgnoreCase) >= 0
                || fileName.IndexOf("Emission", StringComparison.OrdinalIgnoreCase) >= 0
                || fileName.IndexOf("Emissive", StringComparison.OrdinalIgnoreCase) >= 0;
            if (importer.sRGBTexture != desiredSrgb)
            {
                importer.sRGBTexture = desiredSrgb;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }
    }

    private static void EnsureCollider(GameObject root, Vector3 fallbackSize)
    {
        BoxCollider collider = root.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = root.AddComponent<BoxCollider>();
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds total = new Bounds(Vector3.zero, Vector3.zero);
        for (int i = 0; i < renderers.Length; i++)
        {
            Bounds bounds = renderers[i].bounds;
            if (!hasBounds)
            {
                total = bounds;
                hasBounds = true;
            }
            else
            {
                total.Encapsulate(bounds);
            }
        }

        if (hasBounds)
        {
            collider.center = root.transform.InverseTransformPoint(total.center);
            collider.size = total.size;
        }
        else
        {
            collider.center = Vector3.zero;
            collider.size = fallbackSize;
        }
    }

    private static void MapKind(string fileName, out string key, out TextureKind kind)
    {
        string[] suffixes =
        {
            "_BaseColor",
            "_Normal",
            "_Metallic",
            "_Roughness",
            "_Emission",
            "_Emissive",
            "_Alpha"
        };

        for (int i = 0; i < suffixes.Length; i++)
        {
            if (!fileName.EndsWith(suffixes[i], StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            key = fileName.Substring(0, fileName.Length - suffixes[i].Length);
            kind = suffixes[i].Equals("_BaseColor", StringComparison.OrdinalIgnoreCase)
                ? TextureKind.BaseColor
                : suffixes[i].Equals("_Normal", StringComparison.OrdinalIgnoreCase)
                    ? TextureKind.Normal
                    : suffixes[i].Equals("_Metallic", StringComparison.OrdinalIgnoreCase)
                        ? TextureKind.Metallic
                        : suffixes[i].Equals("_Roughness", StringComparison.OrdinalIgnoreCase)
                            ? TextureKind.Roughness
                            : suffixes[i].Equals("_Alpha", StringComparison.OrdinalIgnoreCase)
                                ? TextureKind.Alpha
                                : TextureKind.Emission;
            return;
        }

        key = fileName;
        kind = TextureKind.Unknown;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static void SetTextureIfExists(Material material, string propertyName, Texture texture)
    {
        if (material != null && texture != null && material.HasProperty(propertyName))
        {
            material.SetTexture(propertyName, texture);
        }
    }

    private static void SetFloatIfExists(Material material, string propertyName, float value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static void SetColorIfExists(Material material, string propertyName, Color value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, value);
        }
    }

    private static string NormalizeKey(string value)
    {
        return value
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    private static string SanitizeFileName(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string sanitized = value;
        for (int i = 0; i < invalid.Length; i++)
        {
            sanitized = sanitized.Replace(invalid[i], '_');
        }

        return sanitized.Replace(' ', '_');
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string relative = assetPath.StartsWith("Assets/", StringComparison.Ordinal)
            ? assetPath.Substring("Assets/".Length)
            : assetPath;
        return Path.Combine(Application.dataPath, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string ToAssetPath(string absolutePath)
    {
        string normalized = absolutePath.Replace('\\', '/');
        string dataPath = Application.dataPath.Replace('\\', '/');
        if (!normalized.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        return "Assets" + normalized.Substring(dataPath.Length);
    }

    private enum TextureKind
    {
        Unknown,
        BaseColor,
        Normal,
        Metallic,
        Roughness,
        Emission,
        Alpha
    }

    private sealed class TextureSet
    {
        public Texture2D BaseColor;
        public Texture2D Normal;
        public Texture2D Metallic;
        public Texture2D Roughness;
        public Texture2D Emission;
        public Texture2D Alpha;
    }
}
