using System.IO;
using UnityEditor;
using UnityEngine;

public static class PlayerCharacterVariantPrefabBuilder
{
    private const string RunMarker = "Assets/Editor/.run_player_character_variant_builder";
    private const string MaterialFolder = "Assets/Materials/PlayerCharacter";
    private const string PrefabFolder = "Assets/Prefabs/Player/Variants";
    private const string BodyModelPath = "Assets/import/Character/source/astro-engineer-07d.fbx";
    private const string HelmetBackpackModelPath = "Assets/import/Character/source/astro-engineer-helmet-backpack.obj";
    private static readonly Vector3 HelmetBackpackOffset = new Vector3(-0.015f, -0.315f, 0.003f);
    private static readonly Vector3 HelmetBackpackScale = new Vector3(1.18f, 1.18f, 1.18f);

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
            BuildPlayerCharacterVariants();
        };
    }

    [MenuItem("Tools/ECHO Protocol/Build Player Character Variants")]
    public static void BuildPlayerCharacterVariants()
    {
        EnsureFolder(MaterialFolder);
        EnsureFolder(PrefabFolder);

        BuildVariant("PF_PlayerCharacter_P1_Default", new Color(0.92f, 0.93f, 0.95f), new Color(0.85f, 0.86f, 0.88f));
        BuildVariant("PF_PlayerCharacter_P2_Orange", new Color(1.00f, 0.65f, 0.35f), new Color(0.95f, 0.80f, 0.65f));
        BuildVariant("PF_PlayerCharacter_P3_Green", new Color(0.40f, 0.95f, 0.55f), new Color(0.70f, 0.95f, 0.75f));
        BuildVariant("PF_PlayerCharacter_P4_Purple", new Color(0.85f, 0.48f, 1.00f), new Color(0.88f, 0.72f, 0.98f));

        DeleteLegacyPrefab(PrefabFolder + "/PF_PlayerCharacter_P1_Cyan.prefab");
        DeleteLegacyMaterial(MaterialFolder + "/M_PF_PlayerCharacter_P1_Cyan_Accent.mat");
        DeleteLegacyMaterial(MaterialFolder + "/M_PF_PlayerCharacter_P1_Cyan_HelmetBackpack.mat");
        DeleteLegacyMaterial(MaterialFolder + "/M_PF_PlayerCharacter_P1_Cyan_Visor.mat");
        DeleteLegacyMaterial(MaterialFolder + "/M_PF_PlayerCharacter_P2_Orange_Accent.mat");
        DeleteLegacyMaterial(MaterialFolder + "/M_PF_PlayerCharacter_P3_Green_Accent.mat");
        DeleteLegacyMaterial(MaterialFolder + "/M_PF_PlayerCharacter_P4_Purple_Accent.mat");
        DeleteLegacyMaterial(MaterialFolder + "/M_PlayerSuit_Neutral.mat");
        DeleteLegacyMaterial(MaterialFolder + "/M_PF_PlayerCharacter_P1_Default_BackpackAccent.mat");
        DeleteLegacyMaterial(MaterialFolder + "/M_PF_PlayerCharacter_P2_Orange_BackpackAccent.mat");
        DeleteLegacyMaterial(MaterialFolder + "/M_PF_PlayerCharacter_P3_Green_BackpackAccent.mat");
        DeleteLegacyMaterial(MaterialFolder + "/M_PF_PlayerCharacter_P4_Purple_BackpackAccent.mat");
        DeleteLegacyMaterial(MaterialFolder + "/M_PF_PlayerCharacter_P1_Default_HelmetAccent.mat");
        DeleteLegacyMaterial(MaterialFolder + "/M_PF_PlayerCharacter_P2_Orange_HelmetAccent.mat");
        DeleteLegacyMaterial(MaterialFolder + "/M_PF_PlayerCharacter_P3_Green_HelmetAccent.mat");
        DeleteLegacyMaterial(MaterialFolder + "/M_PF_PlayerCharacter_P4_Purple_HelmetAccent.mat");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        PlayerAnimatorSetupBuilder.BuildAnimatorSetup();
        Debug.Log("[PlayerCharacterVariantPrefabBuilder] Built 4 player character prefabs with helmet/backpack visuals.");
    }

    private static void BuildVariant(string prefabName, Color suitColor, Color equipmentBaseColor)
    {
        Material bodyMaterial = CreateUrpMaterial(
            MaterialFolder + "/M_" + prefabName + "_Suit.mat",
            "Assets/import/Character/textures/atlas-satrolady_tm_PBR_Diffuse.jpeg",
            "Assets/import/Character/textures/atlas-satrolady_tm_PBR_Normal.jpeg",
            "Assets/import/Character/textures/atlas-satrolady_tm_PBR_Metalness.jpeg",
            null,
            Color.black,
            suitColor);

        Material equipmentBaseMaterial = CreateUrpMaterial(
            MaterialFolder + "/M_" + prefabName + "_HelmetBackpackBase.mat",
            "Assets/import/Character/textures/atlas-helmet-backpack_tm_PBR_Diffuse_2.jpeg",
            "Assets/import/Character/textures/atlas-helmet-backpack_tm_PBR_Normal_1.jpeg",
            "Assets/import/Character/textures/atlas-helmet-backpack_tm_PBR_Metalness-atlas-helmet-backpac.jpeg",
            "Assets/import/Character/textures/atlas-helmet-backpack_tm_PBR_Emission_0.jpeg",
            Color.black,
            equipmentBaseColor,
            flipY: true);

        Material visorMaterial = CreateUrpMaterial(
            MaterialFolder + "/M_" + prefabName + "_Visor.mat",
            "Assets/import/Character/textures/helmet-visor_BaseColor_5.jpeg",
            "Assets/import/Character/textures/helmet-visor_Normal_4.jpeg",
            null,
            null,
            Color.black,
            Color.white,
            flipY: true);

        GameObject root = new GameObject(prefabName);
        GameObject body = InstantiateModel(BodyModelPath, "Body", root.transform);
        if (body != null)
        {
            AssignMaterial(body, bodyMaterial);
        }

        GameObject helmetBackpackVisual = InstantiateModel(HelmetBackpackModelPath, "Equipment_Source", root.transform);
        if (helmetBackpackVisual != null)
        {
            AssignEquipmentMaterials(helmetBackpackVisual, equipmentBaseMaterial, visorMaterial);
            helmetBackpackVisual.transform.localPosition = HelmetBackpackOffset;
            helmetBackpackVisual.transform.localRotation = Quaternion.identity;
            helmetBackpackVisual.transform.localScale = HelmetBackpackScale;
            AttachEquipmentToBodyBones(root.transform, body, helmetBackpackVisual);
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/" + prefabName + ".prefab");
        Object.DestroyImmediate(root);

        if (prefab == null)
        {
            Debug.LogError("[PlayerCharacterVariantPrefabBuilder] Failed to save " + prefabName);
        }
    }

    private static GameObject InstantiateModel(string assetPath, string childName, Transform parent)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (source == null)
        {
            Debug.LogWarning("[PlayerCharacterVariantPrefabBuilder] Missing source model: " + assetPath);
            return null;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (instance == null)
        {
            Debug.LogWarning("[PlayerCharacterVariantPrefabBuilder] Could not instantiate source model: " + assetPath);
            return null;
        }

        instance.name = childName;
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        if (PrefabUtility.IsPartOfPrefabInstance(instance))
        {
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }

        return instance;
    }

    private static void AttachEquipmentToBodyBones(Transform root, GameObject body, GameObject equipmentSource)
    {
        if (body == null || equipmentSource == null)
        {
            return;
        }

        Animator animator = body.GetComponent<Animator>();
        if (animator == null)
        {
            animator = body.AddComponent<Animator>();
        }

        Avatar avatar = FindBodyAvatar();
        if (avatar != null)
        {
            animator.avatar = avatar;
        }

        Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head) ?? FindBoneByName(body.transform, "Head");
        Transform backpackBone = animator.GetBoneTransform(HumanBodyBones.UpperChest)
            ?? animator.GetBoneTransform(HumanBodyBones.Chest)
            ?? animator.GetBoneTransform(HumanBodyBones.Spine)
            ?? FindBoneByName(body.transform, "UpperChest")
            ?? FindBoneByName(body.transform, "Chest")
            ?? FindBoneByName(body.transform, "Spine");

        if (headBone == null || backpackBone == null)
        {
            Debug.LogWarning("[PlayerCharacterVariantPrefabBuilder] Could not resolve body bones for equipment attachment.");
            return;
        }

        GameObject helmetAttachment = new GameObject("Helmet_Attachment");
        helmetAttachment.transform.SetParent(headBone, false);

        GameObject backpackAttachment = new GameObject("Backpack_Attachment");
        backpackAttachment.transform.SetParent(backpackBone, false);

        Renderer[] renderers = equipmentSource.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            Transform part = renderer.transform;
            Transform targetParent = IsBackpackPart(part.name) ? backpackAttachment.transform : helmetAttachment.transform;
            part.SetParent(targetParent, true);
        }

        Object.DestroyImmediate(equipmentSource);
    }

    private static bool IsBackpackPart(string partName)
    {
        string lowerName = partName.ToLowerInvariant();
        return lowerName.Contains("backpack") || lowerName.Contains("shoulder_strap") || lowerName == "tube_baked";
    }

    private static Avatar FindBodyAvatar()
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(BodyModelPath);
        foreach (Object asset in assets)
        {
            if (asset is Avatar avatar && avatar.isHuman)
            {
                return avatar;
            }
        }

        foreach (Object asset in assets)
        {
            if (asset is Avatar avatar)
            {
                return avatar;
            }
        }

        return null;
    }

    private static Transform FindBoneByName(Transform root, string token)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.ToLowerInvariant().Contains(token.ToLowerInvariant()))
            {
                return child;
            }
        }

        return null;
    }

    private static Material CreateUrpMaterial(
        string path,
        string baseMapPath,
        string normalMapPath,
        string maskMapPath,
        string emissionMapPath,
        Color emissionColor,
        Color baseColor,
        bool flipY = false)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader != null ? shader : Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = baseColor;
        SetTexture(material, "_BaseMap", baseMapPath, flipY);
        SetTexture(material, "_BumpMap", normalMapPath, flipY);
        SetTexture(material, "_MetallicGlossMap", maskMapPath, flipY);
        SetTexture(material, "_EmissionMap", emissionMapPath, flipY);

        if (normalMapPath != null)
        {
            material.EnableKeyword("_NORMALMAP");
        }

        if (maskMapPath != null)
        {
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        if (emissionColor.maxColorComponent > 0f)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emissionColor);
        }
        else
        {
            material.DisableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetTexture(Material material, string propertyName, string texturePath, bool flipY = false)
    {
        if (string.IsNullOrWhiteSpace(texturePath) || !material.HasProperty(propertyName))
        {
            return;
        }

        Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(texturePath);
        if (texture == null)
        {
            return;
        }

        material.SetTexture(propertyName, texture);
        if (flipY)
        {
            material.SetTextureScale(propertyName, new Vector2(1f, -1f));
            material.SetTextureOffset(propertyName, new Vector2(0f, 1f));
        }
        else
        {
            material.SetTextureScale(propertyName, Vector2.one);
            material.SetTextureOffset(propertyName, Vector2.zero);
        }
    }

    private static void AssignEquipmentMaterials(GameObject root, Material equipmentBaseMaterial, Material visorMaterial)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                renderer.sharedMaterial = equipmentBaseMaterial;
                continue;
            }

            for (int i = 0; i < materials.Length; i++)
            {
                string materialName = materials[i] != null ? materials[i].name.ToLowerInvariant() : string.Empty;
                string rendererName = renderer.name.ToLowerInvariant();
                bool isVisor = materialName.Contains("visor") || rendererName.Contains("visor") || (materials.Length >= 4 && i == 3);
                materials[i] = isVisor ? visorMaterial : equipmentBaseMaterial;
            }

            renderer.sharedMaterials = materials;
        }
    }

    private static void AssignMaterial(GameObject root, Material material)
    {
        if (material == null)
        {
            return;
        }

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                renderer.sharedMaterial = material;
                continue;
            }

            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }

            renderer.sharedMaterials = materials;
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

    private static void DeleteLegacyPrefab(string prefabPath)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            AssetDatabase.DeleteAsset(prefabPath);
        }
    }

    private static void DeleteLegacyMaterial(string materialPath)
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) != null)
        {
            AssetDatabase.DeleteAsset(materialPath);
        }
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, assetPath);
    }
}
