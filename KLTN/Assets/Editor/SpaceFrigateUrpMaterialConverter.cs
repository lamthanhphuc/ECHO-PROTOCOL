using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SpaceFrigateUrpMaterialConverter
{
    private const string Root = "Assets/SpaceFrigate";
    private const string LitShaderName = "Universal Render Pipeline/Lit";

    [InitializeOnLoadMethod]
    private static void ConvertAfterImport()
    {
        EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.IsValidFolder(Root) && HasBuiltinMaterials())
            {
                ConvertAll();
            }
        };
    }

    [MenuItem("ECHO PROTOCOL/Tools/Convert SpaceFrigate Materials To URP")]
    public static void ConvertAll()
    {
        var litShader = Shader.Find(LitShaderName);
        if (litShader == null)
        {
            Debug.LogError("[ECHO] Universal Render Pipeline/Lit shader was not found.");
            return;
        }

        var materialPaths = AssetDatabase.FindAssets("t:Material", new[] { Root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".mat"))
            .ToArray();

        var converted = 0;
        foreach (var path in materialPaths)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) continue;

            if (material.shader != litShader)
            {
                material.shader = litShader;
            }

            var mainTex = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
            if (mainTex != null && material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", mainTex);
                material.SetColor("_BaseColor", Color.white);
            }

            var bumpMap = material.HasProperty("_BumpMap") ? material.GetTexture("_BumpMap") : null;
            if (bumpMap != null)
            {
                material.EnableKeyword("_NORMALMAP");
            }

            var metallicMap = material.HasProperty("_MetallicGlossMap") ? material.GetTexture("_MetallicGlossMap") : null;
            if (metallicMap != null)
            {
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            var occMap = material.HasProperty("_OcclusionMap") ? material.GetTexture("_OcclusionMap") : null;
            if (occMap != null)
            {
                material.EnableKeyword("_OCCLUSIONMAP");
            }

            EditorUtility.SetDirty(material);
            converted++;
        }

        if (converted > 0)
        {
            AssetDatabase.SaveAssets();
            var prefabPath = "Assets/SpaceFrigate/Prefabs/Spacefrigate.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
            }
            AssetDatabase.Refresh();
        }

        Debug.Log($"[ECHO] Converted {converted} SpaceFrigate material(s) to URP.");
    }

    private static bool HasBuiltinMaterials()
    {
        return AssetDatabase.FindAssets("t:Material", new[] { Root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<Material>)
            .Any(material => material != null && !IsUrpMaterial(material));
    }

    private static bool IsUrpMaterial(Material material)
    {
        var shaderName = material.shader != null ? material.shader.name : string.Empty;
        return shaderName.StartsWith("Universal Render Pipeline/");
    }
}
