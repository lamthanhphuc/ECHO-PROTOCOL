using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class SciFiWarehouseUrpMaterialConverter
{
  private const string Root = "Assets/SciFi Warehouse Kit";
  private const string LitShaderName = "Universal Render Pipeline/Lit";
  private const string UnlitShaderName = "Universal Render Pipeline/Unlit";

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

  [MenuItem("ECHO PROTOCOL/Tools/Convert SciFi Warehouse Materials To URP")]
  public static void ConvertAll()
  {
    var litShader = Shader.Find(LitShaderName);
    var unlitShader = Shader.Find(UnlitShaderName);

    if (litShader == null || unlitShader == null)
    {
      Debug.LogError("[ECHO] URP shaders were not found. Check that Universal RP is installed.");
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
      if (material == null || IsUrpMaterial(material))
      {
        continue;
      }

      ConvertMaterial(material, IsEmissiveOnly(material) ? unlitShader : litShader);
      EditorUtility.SetDirty(material);
      converted++;
    }

    if (converted > 0)
    {
      AssetDatabase.SaveAssets();
      AssetDatabase.Refresh();
    }

    Debug.Log($"[ECHO] Converted {converted} SciFi Warehouse material(s) to URP.");
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

  private static bool IsEmissiveOnly(Material material)
  {
    return material.name.Contains("Light") || material.name.Contains("Sign");
  }

  private static void ConvertMaterial(Material material, Shader shader)
  {
    var mainTexture = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
    var mainScale = material.HasProperty("_MainTex") ? material.GetTextureScale("_MainTex") : Vector2.one;
    var mainOffset = material.HasProperty("_MainTex") ? material.GetTextureOffset("_MainTex") : Vector2.zero;
    var normalTexture = material.HasProperty("_BumpMap") ? material.GetTexture("_BumpMap") : null;
    var emissionTexture = material.HasProperty("_EmissionMap") ? material.GetTexture("_EmissionMap") : null;
    var color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
    var emissionColor = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
    var metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f;
    var smoothness = material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : 0.5f;
    var cutoff = material.HasProperty("_Cutoff") ? material.GetFloat("_Cutoff") : 0.5f;
    var legacyMode = material.HasProperty("_Mode") ? material.GetFloat("_Mode") : 0f;
    var alphaClip = material.IsKeywordEnabled("_ALPHATEST_ON") || material.name.Contains("Alpha");
    var transparent = material.IsKeywordEnabled("_ALPHABLEND_ON") || material.name.Contains("Trans") || material.name.Contains("Windows") || legacyMode > 1f;

    material.shader = shader;
    material.SetTexture("_BaseMap", mainTexture);
    material.SetTextureScale("_BaseMap", mainScale);
    material.SetTextureOffset("_BaseMap", mainOffset);
    material.SetColor("_BaseColor", color);

    if (material.HasProperty("_BumpMap"))
    {
      material.SetTexture("_BumpMap", normalTexture);
      if (normalTexture != null)
      {
        material.EnableKeyword("_NORMALMAP");
      }
    }

    if (material.HasProperty("_Metallic"))
    {
      material.SetFloat("_Metallic", metallic);
    }

    if (material.HasProperty("_Smoothness"))
    {
      material.SetFloat("_Smoothness", smoothness);
    }

    if (material.HasProperty("_Cutoff"))
    {
      material.SetFloat("_Cutoff", cutoff);
    }

    if (material.HasProperty("_EmissionMap"))
    {
      material.SetTexture("_EmissionMap", emissionTexture);
    }

    if (material.HasProperty("_EmissionColor"))
    {
      material.SetColor("_EmissionColor", emissionColor);
    }

    if (emissionTexture != null || emissionColor.maxColorComponent > 0f || IsEmissiveOnly(material))
    {
      material.EnableKeyword("_EMISSION");
    }

    ConfigureSurface(material, transparent, alphaClip);
  }

  private static void ConfigureSurface(Material material, bool transparent, bool alphaClip)
  {
    material.SetFloat("_Surface", transparent ? 1f : 0f);
    material.SetFloat("_AlphaClip", alphaClip ? 1f : 0f);
    material.SetFloat("_Blend", 0f);
    material.SetFloat("_SrcBlend", transparent ? (float)UnityEngine.Rendering.BlendMode.SrcAlpha : (float)UnityEngine.Rendering.BlendMode.One);
    material.SetFloat("_DstBlend", transparent ? (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha : (float)UnityEngine.Rendering.BlendMode.Zero);
    material.SetFloat("_ZWrite", transparent ? 0f : 1f);
    material.renderQueue = transparent ? (int)UnityEngine.Rendering.RenderQueue.Transparent : -1;

    material.SetOverrideTag("RenderType", transparent ? "Transparent" : string.Empty);
    CoreUtils.SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", transparent);
    CoreUtils.SetKeyword(material, "_ALPHATEST_ON", alphaClip);
  }
}
