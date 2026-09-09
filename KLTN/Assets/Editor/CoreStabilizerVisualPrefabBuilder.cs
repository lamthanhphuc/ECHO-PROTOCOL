using System.IO;
using UnityEditor;
using UnityEngine;

public static class CoreStabilizerVisualPrefabBuilder
{
    private const string RunMarker = "Assets/Editor/.run_core_stabilizer_visual_prefab_builder";
    private const string MaterialFolder = "Assets/Materials/TeamTools";
    private const string PrefabFolder = "Assets/Prefabs/Environment/Teamtoools";
    private const string ModelPath = "Assets/random-scifi-device/source/propmaker_unwrap (7)/propmaker.obj";
    private const string BaseMapPath = "Assets/random-scifi-device/textures/voxel_diffuse.png";
    private const string NormalMapPath = "Assets/random-scifi-device/textures/voxel_normal.png";
    private const string OcclusionMapPath = "Assets/random-scifi-device/textures/voxel_shadow.png";
    private const string DeviceMaterialPath = MaterialFolder + "/M_CoreStabilizer_Device_URP.mat";
    private const string ParticleMaterialPath = MaterialFolder + "/M_CoreStabilizer_Particles.mat";
    private const string PrefabPath = PrefabFolder + "/PF_CoreStabilizer_Device_Visual.prefab";

    [InitializeOnLoadMethod]
    private static void RunRequestedBuild()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(ToAbsolutePath(RunMarker)))
            {
                return;
            }

            File.Delete(ToAbsolutePath(RunMarker));
            Build();
        };
    }

    [MenuItem("Tools/ECHO Protocol/Build Core Stabilizer Visual Prefab")]
    public static void Build()
    {
        EnsureFolder(MaterialFolder);
        EnsureFolder(PrefabFolder);
        ConfigureNormalMap();

        var deviceMaterial = CreateDeviceMaterial();
        var particleMaterial = CreateParticleMaterial();
        var sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (sourceModel == null)
        {
            Debug.LogError("[CoreStabilizerBuilder] Missing source model: " + ModelPath);
            return;
        }

        var root = new GameObject("PF_CoreStabilizer_Device_Visual");
        var gripPivot = CreateChild(root.transform, "GripPivot");
        var deviceModel = CreateChild(gripPivot.transform, "DeviceModel");

        var model = PrefabUtility.InstantiatePrefab(sourceModel, deviceModel.transform) as GameObject;
        if (model == null)
        {
            Object.DestroyImmediate(root);
            Debug.LogError("[CoreStabilizerBuilder] Could not instantiate source model.");
            return;
        }

        model.name = "RandomSciFiDevice";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            for (var i = 0; i < materials.Length; i++)
            {
                materials[i] = deviceMaterial;
            }
            renderer.sharedMaterials = materials;
        }

        foreach (var animator in model.GetComponentsInChildren<Animator>(true))
        {
            animator.enabled = false;
        }

        var bounds = CalculateBounds(model);
        var longestDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (longestDimension > 0.0001f)
        {
            model.transform.localScale = Vector3.one * (0.28f / longestDimension);
            bounds = CalculateBounds(model);
            model.transform.position -= bounds.center;
            bounds = CalculateBounds(model);
        }

        var supportFieldOrigin = CreateChild(root.transform, "SupportFieldOrigin");
        supportFieldOrigin.transform.localPosition = Vector3.zero;
        CreateCoveragePreview(supportFieldOrigin.transform, particleMaterial);

        var deviceVfxOrigin = CreateChild(root.transform, "DeviceVFXOrigin");
        deviceVfxOrigin.transform.localPosition = root.transform.InverseTransformPoint(
            bounds.center + Vector3.up * bounds.extents.y);

        var lightObject = CreateChild(deviceVfxOrigin.transform, "StatusLight");
        var energyLight = lightObject.AddComponent<Light>();
        energyLight.type = LightType.Point;
        energyLight.color = new Color(0.1f, 0.8f, 1f);
        energyLight.intensity = 0.8f;
        energyLight.range = 1.5f;
        energyLight.shadows = LightShadows.None;

        var particlesObject = CreateChild(deviceVfxOrigin.transform, "FieldStatusParticles");
        ConfigureStatusParticles(particlesObject.AddComponent<ParticleSystem>(), particleMaterial);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (prefab == null)
        {
            Debug.LogError("[CoreStabilizerBuilder] Failed to save prefab: " + PrefabPath);
            return;
        }

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log("[CoreStabilizerBuilder] Built visual prefab: " + PrefabPath);
    }

    private static Material CreateDeviceMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(DeviceMaterialPath);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = "M_CoreStabilizer_Device_URP" };
            AssetDatabase.CreateAsset(material, DeviceMaterialPath);
        }

        SetTexture(material, "_BaseMap", BaseMapPath);
        SetTexture(material, "_MainTex", BaseMapPath);
        SetTexture(material, "_BumpMap", NormalMapPath);
        SetTexture(material, "_OcclusionMap", OcclusionMapPath);
        material.SetFloat("_Metallic", 0.45f);
        material.SetFloat("_Smoothness", 0.3f);
        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_OCCLUSIONMAP");
        material.DisableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateParticleMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Particles/Standard Unlit")
                         ?? Shader.Find("Sprites/Default");
            material = new Material(shader) { name = "M_CoreStabilizer_Particles" };
            AssetDatabase.CreateAsset(material, ParticleMaterialPath);
        }

        var color = new Color(0.1f, 0.85f, 1f, 0.75f);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 2f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureStatusParticles(ParticleSystem particles, Material material)
    {
        var main = particles.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = 0.4f;
        main.startSpeed = 0.05f;
        main.startSize = 0.025f;
        main.startColor = new Color(0.1f, 0.85f, 1f, 0.8f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 40;

        var emission = particles.emission;
        emission.rateOverTime = 10f;

        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f;

        var colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.1f, 0.8f, 1f), 0f),
                new GradientColorKey(new Color(0.4f, 1f, 1f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.85f, 0.35f),
                new GradientAlphaKey(0f, 1f),
            });
        colorOverLifetime.color = gradient;

        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static void CreateCoveragePreview(Transform parent, Material material)
    {
        const float radius = 2.5f;
        const int segments = 72;

        var preview = CreateChild(parent, "CoveragePreview_2_5m");
        var line = preview.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = segments;
        line.startWidth = 0.025f;
        line.endWidth = 0.025f;
        line.startColor = new Color(0.1f, 0.85f, 1f, 0.65f);
        line.endColor = line.startColor;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        for (var i = 0; i < segments; i++)
        {
            var angle = i * Mathf.PI * 2f / segments;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }

        preview.SetActive(false);
    }

    private static void ConfigureNormalMap()
    {
        if (AssetImporter.GetAtPath(NormalMapPath) is not TextureImporter importer
            || importer.textureType == TextureImporterType.NormalMap)
        {
            return;
        }

        importer.textureType = TextureImporterType.NormalMap;
        importer.SaveAndReimport();
    }

    private static Bounds CalculateBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(root.transform.position, Vector3.one);
        }

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }
        return bounds;
    }

    private static GameObject CreateChild(Transform parent, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static void SetTexture(Material material, string propertyName, string assetPath)
    {
        if (!material.HasProperty(propertyName)) return;
        material.SetTexture(propertyName, AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath));
    }

    private static void EnsureFolder(string folderPath)
    {
        var parts = folderPath.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    private static string ToAbsolutePath(string assetPath)
    {
        return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
    }
}
