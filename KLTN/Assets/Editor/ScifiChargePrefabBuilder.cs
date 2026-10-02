using System;
using UnityEditor;
using UnityEngine;

public static class ScifiChargePrefabBuilder
{
    private const string Root = "Assets/import/Scifi_charge";
    private const string ModelPath = Root + "/source/model/model.dae";
    private const string MaterialFolder = Root + "/materials";
    private const string PrefabPath = "Assets/Prefabs/Environment/PF_ScifiCharge.prefab";
    private static readonly string[] Parts = { "body", "pivotscreen", "wireport" };

    [MenuItem("ECHO PROTOCOL/Tools/Build Scifi Charge Prefab")]
    public static void Build()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("[ScifiCharge] URP Lit shader is unavailable.");
            return;
        }

        var materials = new Material[Parts.Length];
        for (int i = 0; i < Parts.Length; i++)
        {
            string part = Parts[i];
            string path = MaterialFolder + "/M_ScifiCharge_" + part + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "M_ScifiCharge_" + part };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;

            var albedo = LoadTexture(Root + "/textures/" + part + "_albedo.jpeg");
            var normal = LoadTexture(Root + "/textures/" + part + "_normal.png", true);
            var metallicSmoothness = LoadTexture(MaterialFolder + "/" + part + "_metallic_smoothness.png", false, true);
            var ao = LoadTexture(Root + "/textures/" + part + "_AO.jpeg", false, true);
            var emission = LoadTexture(Root + "/textures/" + part + "_emissive.jpeg");
            if (albedo == null || normal == null || metallicSmoothness == null || ao == null || emission == null)
            {
                Debug.LogError("[ScifiCharge] Missing texture for " + part + ".");
                return;
            }

            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", albedo);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.SetTexture("_MetallicGlossMap", metallicSmoothness);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetTexture("_OcclusionMap", ao);
            material.SetFloat("_OcclusionStrength", 1f);
            material.SetTexture("_EmissionMap", emission);
            material.SetColor("_EmissionColor", Color.white);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            EditorUtility.SetDirty(material);
            materials[i] = material;
        }

        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError("[ScifiCharge] Missing model.dae.");
            return;
        }
        for (int i = 0; i < Parts.Length; i++)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), Parts[i]), materials[i]);
        importer.SaveAndReimport();

        var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (source == null) return;
        var root = new GameObject("PF_ScifiCharge");
        try
        {
            var model = PrefabUtility.InstantiatePrefab(source, root.transform) as GameObject;
            if (model == null) throw new InvalidOperationException("Could not instantiate model.dae");
            model.name = "Scifi_charge_Model";
            // The DAE declares metres but its vertex coordinates are in centimetres.
            model.transform.localScale = Vector3.one * 0.01f;
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length != Parts.Length)
                throw new InvalidOperationException("Expected 3 model renderers; found " + renderers.Length);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                var shared = renderer.sharedMaterials;
                string name = renderer.gameObject.name.ToLowerInvariant();
                int index = Array.FindIndex(Parts, part => name.Contains(part));
                if (index < 0) throw new InvalidOperationException("Unknown model part: " + renderer.gameObject.name);
                for (int i = 0; i < shared.Length; i++) shared[i] = materials[index];
                renderer.sharedMaterials = shared;
                bounds.Encapsulate(renderer.bounds);
            }
            model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                throw new InvalidOperationException("Could not save " + PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[ScifiCharge] Built " + PrefabPath + " (size " + bounds.size + ").");
        }
        catch (Exception error)
        {
            Debug.LogError("[ScifiCharge] " + error);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static Texture2D LoadTexture(string path, bool normal = false, bool linear = false)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;
        bool changed = false;
        if (normal && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            changed = true;
        }
        if (linear && importer.sRGBTexture)
        {
            importer.sRGBTexture = false;
            changed = true;
        }
        if (changed) importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
