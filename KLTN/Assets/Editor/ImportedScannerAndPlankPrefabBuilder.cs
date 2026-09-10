using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ImportedScannerAndPlankPrefabBuilder
{
    private const string RunMarker = "Assets/Editor/.run_scanner_plank_builder";
    private const string MaterialFolder = "Assets/Materials/ImportedGameplay";
    private const string PrefabFolder = "Assets/Prefabs/Gameplay/Imported";

    static ImportedScannerAndPlankPrefabBuilder()
    {
        EditorApplication.delayCall += CheckAndBuild;
    }

    private static void CheckAndBuild()
    {
        string markerPath = Path.Combine(Application.dataPath, "Editor/.run_scanner_plank_builder");
        string scannerPrefab = Path.Combine(Application.dataPath, "Prefabs/Gameplay/Imported/PF_Scanner_Imported.prefab");
        string plankPrefab = Path.Combine(Application.dataPath, "Prefabs/Gameplay/Imported/PF_Plank_Imported.prefab");

        if (File.Exists(markerPath) || !File.Exists(scannerPrefab) || !File.Exists(plankPrefab))
        {
            if (File.Exists(markerPath))
            {
                File.Delete(markerPath);
            }
            BuildPrefabs();
        }
    }

    [MenuItem("Tools/ECHO Protocol/Build Scanner and Plank Prefabs")]
    public static void BuildPrefabs()
    {
        EnsureFolder(MaterialFolder);
        EnsureFolder(PrefabFolder);

        // 1. Scanner Material & Prefab
        Material scannerMaterial = BuildScannerMaterial();
        GameObject scannerPrefab = CreateModelPrefab(
            "Assets/import/scanner/source/scannerf.fbx",
            scannerMaterial,
            "PF_Scanner_Imported",
            new[]
            {
                PrefabFolder + "/PF_Scanner_Imported.prefab",
                "Assets/import/scanner/PF_Scanner.prefab"
            },
            forcedVisualScale: Vector3.one);

        // 2. Plank Material & Prefab
        Material plankMaterial = BuildPlankMaterial();
        GameObject plankPrefab = CreateModelPrefab(
            "Assets/import/plank/source/Plank4.fbx",
            plankMaterial,
            "PF_Plank_Imported",
            new[]
            {
                PrefabFolder + "/PF_Plank_Imported.prefab",
                "Assets/import/plank/PF_Plank.prefab"
            },
            forcedVisualScale: null);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ImportedScannerAndPlankPrefabBuilder] Build completed successfully. Scanner={scannerPrefab != null}, Plank={plankPrefab != null}");
    }

    private static Material BuildScannerMaterial()
    {
        string matPath = MaterialFolder + "/M_Scanner_Imported.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, matPath);
        }

        Texture2D baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/import/scanner/textures/Master_Texture.jpg");
        if (baseTex != null)
        {
            SetTextureIfExists(material, "_BaseMap", baseTex);
            SetTextureIfExists(material, "_MainTex", baseTex);
        }

        SetFloatIfExists(material, "_Metallic", 0.3f);
        SetFloatIfExists(material, "_Smoothness", 0.5f);

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material BuildPlankMaterial()
    {
        string normalPath = "Assets/import/plank/textures/lambert1_normal.png";
        TextureImporter normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
        if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
        {
            normalImporter.textureType = TextureImporterType.NormalMap;
            normalImporter.SaveAndReimport();
        }

        string matPath = MaterialFolder + "/M_Plank_Imported.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, matPath);
        }

        Texture2D baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/import/plank/textures/lambert1_baseColor.jpg");
        if (baseTex != null)
        {
            SetTextureIfExists(material, "_BaseMap", baseTex);
            SetTextureIfExists(material, "_MainTex", baseTex);
        }

        Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        if (normalTex != null)
        {
            SetTextureIfExists(material, "_BumpMap", normalTex);
            material.EnableKeyword("_NORMALMAP");
        }

        Texture2D roughnessTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/import/plank/textures/lambert1_roughness.jpg");
        if (roughnessTex != null)
        {
            SetFloatIfExists(material, "_Smoothness", 0.25f);
        }

        SetFloatIfExists(material, "_Metallic", 0.0f);

        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject CreateModelPrefab(string fbxPath, Material material, string rootName, string[] savePaths, Vector3? forcedVisualScale = null)
    {
        GameObject sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (sourceModel == null)
        {
            Debug.LogError($"[ImportedScannerAndPlankPrefabBuilder] Source model not found at {fbxPath}");
            return null;
        }

        GameObject root = new GameObject(rootName);
        GameObject visual = PrefabUtility.InstantiatePrefab(sourceModel) as GameObject;
        if (visual != null)
        {
            Vector3 visualScale = forcedVisualScale.HasValue ? forcedVisualScale.Value : visual.transform.localScale;
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = visualScale;

            AssignMaterial(visual, material);
        }

        EnsureBoxCollider(root);

        GameObject primaryPrefab = null;
        foreach (string savePath in savePaths)
        {
            EnsureFolder(Path.GetDirectoryName(savePath).Replace('\\', '/'));
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, savePath);
            AssetDatabase.ImportAsset(savePath);
            if (primaryPrefab == null)
            {
                primaryPrefab = saved;
            }
            Debug.Log($"[ImportedScannerAndPlankPrefabBuilder] Saved prefab to {savePath}");
        }

        Object.DestroyImmediate(root);
        return primaryPrefab;
    }

    private static void AssignMaterial(GameObject root, Material material)
    {
        if (material == null) return;

        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = r.sharedMaterials;
            if (mats == null || mats.Length == 0)
            {
                r.sharedMaterial = material;
            }
            else
            {
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i] = material;
                }
                r.sharedMaterials = mats;
            }
        }
    }

    private static void EnsureBoxCollider(GameObject root)
    {
        BoxCollider collider = root.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = root.AddComponent<BoxCollider>();
        }

        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
        if (meshFilters.Length > 0 && meshFilters[0].sharedMesh != null)
        {
            Bounds totalBounds = new Bounds();
            bool hasBounds = false;

            foreach (MeshFilter mf in meshFilters)
            {
                if (mf.sharedMesh == null) continue;
                Bounds b = mf.sharedMesh.bounds;
                Vector3[] corners = new Vector3[]
                {
                    mf.transform.TransformPoint(b.center + new Vector3(-b.extents.x, -b.extents.y, -b.extents.z)),
                    mf.transform.TransformPoint(b.center + new Vector3(-b.extents.x, -b.extents.y,  b.extents.z)),
                    mf.transform.TransformPoint(b.center + new Vector3(-b.extents.x,  b.extents.y, -b.extents.z)),
                    mf.transform.TransformPoint(b.center + new Vector3(-b.extents.x,  b.extents.y,  b.extents.z)),
                    mf.transform.TransformPoint(b.center + new Vector3( b.extents.x, -b.extents.y, -b.extents.z)),
                    mf.transform.TransformPoint(b.center + new Vector3( b.extents.x, -b.extents.y,  b.extents.z)),
                    mf.transform.TransformPoint(b.center + new Vector3( b.extents.x,  b.extents.y, -b.extents.z)),
                    mf.transform.TransformPoint(b.center + new Vector3( b.extents.x,  b.extents.y,  b.extents.z)),
                };

                for (int i = 0; i < corners.Length; i++)
                {
                    Vector3 localCorner = root.transform.InverseTransformPoint(corners[i]);
                    if (!hasBounds)
                    {
                        totalBounds = new Bounds(localCorner, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        totalBounds.Encapsulate(localCorner);
                    }
                }
            }

            if (hasBounds)
            {
                collider.center = totalBounds.center;
                collider.size = totalBounds.size;
                return;
            }
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            collider.center = Vector3.zero;
            collider.size = Vector3.one;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        collider.center = root.transform.InverseTransformPoint(bounds.center);
        collider.size = bounds.size;
    }

    private static void SetTextureIfExists(Material material, string propertyName, Texture texture)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetTexture(propertyName, texture);
        }
    }

    private static void SetFloatIfExists(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
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
}
