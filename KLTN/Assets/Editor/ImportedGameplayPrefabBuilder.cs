using System.IO;
using UnityEditor;
using UnityEngine;

public static class ImportedGameplayPrefabBuilder
{
    private const string RunMarker = "Assets/Editor/.run_imported_gameplay_prefab_builder";
    private const string MaterialFolder = "Assets/Materials/ImportedGameplay";
    private const string PrefabFolder = "Assets/Prefabs/Gameplay/Imported";
    private const string SystemPrefabFolder = "Assets/Prefabs/Gameplay/System";
    private const string ItemFolder = "Assets/ScriptableObjects/Inventory";

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
            BuildImportedGameplayPrefabs();
        };
    }

    [MenuItem("Tools/ECHO Protocol/Build Imported Gameplay Prefabs")]
    public static void BuildImportedGameplayPrefabs()
    {
        EnsureFolder(MaterialFolder);
        EnsureFolder(PrefabFolder);
        EnsureFolder(SystemPrefabFolder);
        EnsureFolder(ItemFolder);

        Material energyCoreMaterial = CreateUrpMaterial(
            MaterialFolder + "/M_EnergyCore_Imported.mat",
            "Assets/import/energy core/textures/eneergy_core_1_low_Material_Diffuse.png",
            "Assets/import/energy core/textures/eneergy_core_1_low_Material_Normal.png",
            "Assets/import/energy core/textures/eneergy_core_1_low_Material_Metallic.png",
            "Assets/import/energy core/textures/eneergy_core_1_low_Material_Emissive.png",
            new Color(0.6f, 1.4f, 2.0f));

        Material panelMaterial = CreateUrpMaterial(
            MaterialFolder + "/M_DistributionPanel_Imported.mat",
            "Assets/import/panel/textures/panel_control_low_4_uv_1001_BaseColor.png",
            "Assets/import/panel/textures/panel_control_low_4_uv_1001_Normal.png",
            "Assets/import/panel/textures/panel_control_low_4_uv_1001_Metallic.png",
            "Assets/import/panel/textures/panel_control_low_4_uv_1001_Emissive.png",
            new Color(0.7f, 1.2f, 0.6f));

        Material hubMaterial = CreateUrpMaterial(
            MaterialFolder + "/M_CoreReceiverHub_Imported.mat",
            "Assets/import/hub/ScifiGenerator/Mat_Base_Color.png",
            "Assets/import/hub/ScifiGenerator/Mat_Normal_DirectX.png",
            "Assets/import/hub/ScifiGenerator/Mat_Metallic.png",
            "Assets/import/hub/ScifiGenerator/Mat_Emissive.png",
            new Color(1.2f, 0.7f, 1.8f));

        Material powerControlMaterial = CreateUrpMaterial(
            MaterialFolder + "/M_PowerControl_Imported.mat",
            "Assets/import/Power Control/source/tex/blender_chad_console_fromPaint_diffuse.png",
            null,
            "Assets/import/Power Control/source/tex/blender_chad_console_fromPaint_metalness-blender_chad_console_fromPaint_roughness.png",
            "Assets/import/Power Control/source/tex/gltf_chad_console_fromPaint_emission.jpg",
            new Color(2.0f, 0.95f, 0.18f));

        Material powerControlScreenMaterial = CreateUrpMaterial(
            MaterialFolder + "/M_PowerControl_Screen_Imported.mat",
            "Assets/Materials/ImportedGameplay/Generated/display_power_orange.png",
            null,
            null,
            "Assets/Materials/ImportedGameplay/Generated/display_power_orange.png",
            new Color(3.0f, 1.25f, 0.25f));

        Material powerControlKeyboardMaterial = CreateUrpMaterial(
            MaterialFolder + "/M_PowerControl_Keyboard_Imported.mat",
            "Assets/import/Power Control/source/tex/keyboard_D.png",
            "Assets/import/Power Control/source/tex/keyboard_N.png",
            null,
            null,
            Color.black);

        Material securityTerminalMaterial = CreateUrpMaterial(
            MaterialFolder + "/M_SecurityTerminal_Imported.mat",
            "Assets/import/Power Control/source/tex/blender_chad_console_fromPaint_diffuse.png",
            null,
            "Assets/import/Power Control/source/tex/blender_chad_console_fromPaint_metalness-blender_chad_console_fromPaint_roughness.png",
            "Assets/import/Power Control/source/tex/gltf_chad_console_fromPaint_emission.jpg",
            new Color(0.2f, 2.0f, 0.25f));

        Material securityTerminalScreenMaterial = CreateUrpMaterial(
            MaterialFolder + "/M_SecurityTerminal_Screen_Imported.mat",
            "Assets/import/Power Control/source/tex/display.png",
            null,
            null,
            "Assets/import/Power Control/source/tex/display.png",
            new Color(0.35f, 3.0f, 0.6f));

        Material securityTerminalKeyboardMaterial = CreateUrpMaterial(
            MaterialFolder + "/M_SecurityTerminal_Keyboard_Imported.mat",
            "Assets/import/Power Control/source/tex/keyboard_D.png",
            "Assets/import/Power Control/source/tex/keyboard_N.png",
            null,
            null,
            Color.black);

        Material firstAidRedMaterial = CreateUrpMaterial(
            MaterialFolder + "/M_FirstAid_Red_URP.mat",
            "Assets/GeeKay3D/First-Aid-Set/Assets/Textures/red/FirstAidKit_red_AlbedoTransparency.png",
            "Assets/GeeKay3D/First-Aid-Set/Assets/Textures/red/FirstAidKit_red_Normal.png",
            "Assets/GeeKay3D/First-Aid-Set/Assets/Textures/red/FirstAidKit_red_MetallicSmoothness.png",
            null,
            Color.black);

        Material lockerCleanMaterial = CreateUrpMaterial(
            MaterialFolder + "/M_Locker_Clean_URP.mat",
            "Assets/Locker_HQ/Textures/locker_0001_clean_albedo_a.png",
            "Assets/Locker_HQ/Textures/locker_0001_clean_normal.png",
            "Assets/Locker_HQ/Textures/locker_0001_clean_maskmap.png",
            null,
            Color.black);

        Material lockerRustyMaterial = CreateUrpMaterial(
            MaterialFolder + "/M_Locker_Rusty_URP.mat",
            "Assets/Locker_HQ/Textures/locker_0001_rusty_albedo_a.png",
            "Assets/Locker_HQ/Textures/locker_0001_rusty_normal.png",
            "Assets/Locker_HQ/Textures/locker_0001_rusty_maskmap.png",
            null,
            Color.black);

        InventoryItemDefinition energyCoreItem = CreateItemDefinition(
            ItemFolder + "/SO_EnergyCore_ItemDefinition.asset",
            "energy_core",
            "Energy Core",
            InventoryItemType.EnergyCore,
            null);

        InventoryItemDefinition firstAidItem = CreateItemDefinition(
            ItemFolder + "/SO_FirstAid_ItemDefinition.asset",
            "first_aid",
            "First Aid Kit",
            InventoryItemType.Normal,
            null);

        GameObject energyCorePrefab = CreateModelPrefab(
            PrefabFolder + "/PF_EnergyCore_Imported.prefab",
            "Assets/import/energy core/source/eneergy_core_1_low.fbx",
            energyCoreMaterial,
            Vector3.one,
            new Vector3(0.8f, 1.0f, 0.8f),
            root =>
            {
                EnergyCorePickup pickup = root.AddComponent<EnergyCorePickup>();
                SetSerializedField(pickup, "coreItem", energyCoreItem);
                SetSerializedField(pickup, "coreId", "EnergyCore");
                SetSerializedField(pickup, "pickupPrompt", "Pick up Energy Core");
            });

        GameObject sectorBoxPrefab = CreateModelPrefab(
            PrefabFolder + "/PF_SectorBox_Imported.prefab",
            "Assets/import/hub/ScifiGenerator/ScifiGenerator.obj",
            hubMaterial,
            Vector3.one * 0.0025f,
            new Vector3(2.5f, 1.8f, 2.5f),
            root =>
            {
                EnergyCoreObjectiveProgress objectiveProgress = root.AddComponent<EnergyCoreObjectiveProgress>();
                PowerPuzzleController powerPuzzle = root.AddComponent<PowerPuzzleController>();
                SectorBox sectorBox = root.AddComponent<SectorBox>();
                SetSerializedField(sectorBox, "objectiveProgress", objectiveProgress);
                SetSerializedField(sectorBox, "placePrompt", "Place Energy Core");
                SetSerializedField(sectorBox, "completePrompt", "Sector Box complete");
                SetSerializedField(powerPuzzle, "coreProgress", objectiveProgress);
            });

        GameObject panelPrefab = CreateModelPrefab(
            PrefabFolder + "/PF_DistributionPanel_Imported.prefab",
            "Assets/import/panel/source/panel_control_low_4_uv.fbx",
            panelMaterial,
            Vector3.one,
            new Vector3(1.4f, 1.4f, 0.5f),
            root => AddPowerPuzzleStation(root, PowerPuzzleStationType.DistributionPanel, "Input distribution code"));

        GameObject powerControlPrefab = CreateMultiModelPrefab(
            PrefabFolder + "/PF_PowerControl_Imported.prefab",
            new ModelPart[]
            {
                new ModelPart("Body", "Assets/import/Power Control/source/parts/chad_console_body.obj", powerControlMaterial, Vector3.one),
                new ModelPart("Screen", "Assets/import/Power Control/source/parts/chad_console_screen.obj", powerControlScreenMaterial, Vector3.one),
                new ModelPart("Keyboard", "Assets/import/Power Control/source/parts/chad_console_keyboard.obj", powerControlKeyboardMaterial, Vector3.one)
            },
            new Vector3(1.8f, 1.2f, 1.0f),
            root => AddPowerPuzzleStation(root, PowerPuzzleStationType.PowerControl, "Read power routing code"));

        GameObject terminalPrefab = CreateMultiModelPrefab(
            PrefabFolder + "/PF_SecurityTerminal_Imported.prefab",
            new ModelPart[]
            {
                new ModelPart("Body", "Assets/import/Power Control/source/parts/chad_console_body.obj", securityTerminalMaterial, Vector3.one),
                new ModelPart("Screen", "Assets/import/Power Control/source/parts/chad_console_screen.obj", securityTerminalScreenMaterial, Vector3.one),
                new ModelPart("Keyboard", "Assets/import/Power Control/source/parts/chad_console_keyboard.obj", securityTerminalKeyboardMaterial, Vector3.one)
            },
            new Vector3(1.8f, 1.2f, 1.0f),
            AddSecurityTerminalDownload);

        GameObject firstAidPrefab = CreatePickupWrapperPrefab(
            PrefabFolder + "/PF_FirstAidPickup_Imported.prefab",
            "Assets/GeeKay3D/First-Aid-Set/Assets/Prefabs/FirstAidKit_Red.prefab",
            firstAidRedMaterial,
            firstAidItem,
            "Pick up First Aid Kit",
            new Vector3(0.5f, 0.35f, 0.35f));

        CreateHidingSpotPrefab(
            PrefabFolder + "/PF_LockerHidingSpot_Clean.prefab",
            "Assets/Locker_HQ/Prefabs/locker_clean_a.prefab",
            lockerCleanMaterial,
            new Vector3(1.0f, 2.0f, 0.8f));

        CreateHidingSpotPrefab(
            PrefabFolder + "/PF_LockerHidingSpot_Rusty.prefab",
            "Assets/Locker_HQ/Prefabs/locker_rusty_a.prefab",
            lockerRustyMaterial,
            new Vector3(1.0f, 2.0f, 0.8f));

        GameObject escapeDoorPrefab = CreateSourceWrapperPrefab(
            SystemPrefabFolder + "/PF_EscapeDoor_Countdown.prefab",
            "Assets/SciFi Warehouse Kit/Prefabs/Structures/Walls/Wall BayDoor.prefab",
            new Vector3(4.0f, 3.0f, 0.8f),
            root =>
            {
                EscapeDoorCountdown escapeDoor = root.AddComponent<EscapeDoorCountdown>();
                SetSerializedField(escapeDoor, "lockedPrompt", "Escape locked");
                SetSerializedField(escapeDoor, "startPrompt", "Start escape countdown");
                SetSerializedField(escapeDoor, "countingPrompt", "Escape opening");
                SetSerializedField(escapeDoor, "completePrompt", "Escape ready");
            });

        GameObject gameModePrefab = CreateGameModePrefab(SystemPrefabFolder + "/PF_GameMode_ResearchFacility.prefab");

        CreateItemDefinition(ItemFolder + "/SO_EnergyCore_ItemDefinition.asset", "energy_core", "Energy Core", InventoryItemType.EnergyCore, energyCorePrefab);
        CreateItemDefinition(ItemFolder + "/SO_FirstAid_ItemDefinition.asset", "first_aid", "First Aid Kit", InventoryItemType.Normal, firstAidPrefab);

        Debug.Log("[ImportedGameplayPrefabBuilder] Built imported gameplay materials and prefabs. PowerControl="
            + (powerControlPrefab != null) + ", Terminal=" + (terminalPrefab != null) + ", Panel=" + (panelPrefab != null)
            + ", SectorBox=" + (sectorBoxPrefab != null) + ", EscapeDoor=" + (escapeDoorPrefab != null)
            + ", GameMode=" + (gameModePrefab != null));

        PlayerCharacterVariantPrefabBuilder.BuildPlayerCharacterVariants();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static Material CreateUrpMaterial(string assetPath, string baseMapPath, string normalPath, string metallicPath, string emissionPath, Color emissionColor)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, assetPath);
        }

        Texture2D baseMap = LoadTexture(baseMapPath);
        if (baseMap != null)
        {
            SetTextureIfExists(material, "_BaseMap", baseMap);
            SetTextureIfExists(material, "_MainTex", baseMap);
        }

        Texture2D normal = LoadTexture(normalPath);
        if (normal != null)
        {
            SetTextureIfExists(material, "_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
        }

        Texture2D metallic = LoadTexture(metallicPath);
        if (metallic != null)
        {
            SetTextureIfExists(material, "_MetallicGlossMap", metallic);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            SetFloatIfExists(material, "_Metallic", 0.8f);
            SetFloatIfExists(material, "_Smoothness", 0.35f);
        }

        Texture2D emission = LoadTexture(emissionPath);
        if (emission != null)
        {
            SetTextureIfExists(material, "_EmissionMap", emission);
            SetColorIfExists(material, "_EmissionColor", emissionColor);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private struct ModelPart
    {
        public readonly string Name;
        public readonly string ModelPath;
        public readonly Material Material;
        public readonly Vector3 Scale;

        public ModelPart(string name, string modelPath, Material material, Vector3 scale)
        {
            Name = name;
            ModelPath = modelPath;
            Material = material;
            Scale = scale;
        }
    }

    private static GameObject CreateModelPrefab(string prefabPath, string modelPath, Material material, Vector3 visualScale, Vector3 fallbackSize, System.Action<GameObject> configure)
    {
        return CreateMultiModelPrefab(prefabPath, new[] { new ModelPart("Visual", modelPath, material, visualScale) }, fallbackSize, configure);
    }

    private static GameObject CreateMultiModelPrefab(string prefabPath, ModelPart[] parts, Vector3 fallbackSize, System.Action<GameObject> configure)
    {
        GameObject root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));

        foreach (ModelPart part in parts)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(part.ModelPath);
            if (model == null)
            {
                continue;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (instance != null)
            {
                instance.name = part.Name;
                instance.transform.SetParent(root.transform, false);
                instance.transform.localScale = part.Scale;
                AssignMaterial(instance, part.Material);
            }
        }

        if (root.transform.childCount == 0)
        {
            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.name = "PlaceholderVisual";
            fallback.transform.SetParent(root.transform, false);
            fallback.transform.localScale = fallbackSize;
        }

        EnsureBoxCollider(root, fallbackSize);
        configure?.Invoke(root);

        GameObject prefab = SavePrefab(root, prefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateSourceWrapperPrefab(string prefabPath, string sourcePrefabPath, Vector3 fallbackSize, System.Action<GameObject> configure)
    {
        GameObject root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPath);
        if (source != null)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance != null)
            {
                instance.name = "Visual";
                instance.transform.SetParent(root.transform, false);
            }
        }

        if (root.transform.childCount == 0)
        {
            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.name = "PlaceholderVisual";
            fallback.transform.SetParent(root.transform, false);
            fallback.transform.localScale = fallbackSize;
        }

        EnsureBoxCollider(root, fallbackSize);
        configure?.Invoke(root);

        GameObject prefab = SavePrefab(root, prefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateGameModePrefab(string prefabPath)
    {
        GameObject root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
        root.AddComponent<MatchFlowController>();
        root.AddComponent<GameplayDebugHUD>();

        GameObject prefab = SavePrefab(root, prefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void AddMapInteractionPoint(GameObject root, string interactionId, string prompt)
    {
        MapInteractionPoint interactionPoint = root.AddComponent<MapInteractionPoint>();
        SetSerializedField(interactionPoint, "interactionId", interactionId);
        SetSerializedField(interactionPoint, "prompt", prompt);
        SetSerializedField(interactionPoint, "requireEnabled", true);
    }

    private static void AddPowerPuzzleStation(GameObject root, PowerPuzzleStationType stationType, string fallbackPrompt)
    {
        PowerPuzzleStation station = root.AddComponent<PowerPuzzleStation>();
        SetSerializedField(station, "stationType", stationType);
        SetSerializedField(station, "fallbackPrompt", fallbackPrompt);
    }

    private static void AddSecurityTerminalDownload(GameObject root)
    {
        SecurityTerminalDownload download = root.AddComponent<SecurityTerminalDownload>();
        SetSerializedField(download, "startPrompt", "Download Access Code");
        SetSerializedField(download, "resumePrompt", "Resume Access Code Download");
        SetSerializedField(download, "downloadingPrompt", "Access Code downloading");
        SetSerializedField(download, "completePrompt", "Access Code downloaded");
    }

    private static GameObject CreatePickupWrapperPrefab(string prefabPath, string sourcePrefabPath, Material overrideMaterial, InventoryItemDefinition item, string prompt, Vector3 fallbackSize)
    {
        GameObject root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPath);
        if (source != null)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance != null)
            {
                instance.name = "Visual";
                instance.transform.SetParent(root.transform, false);
                AssignMaterial(instance, overrideMaterial);
            }
        }

        if (root.transform.childCount == 0)
        {
            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.name = "PlaceholderVisual";
            fallback.transform.SetParent(root.transform, false);
            fallback.transform.localScale = fallbackSize;
        }

        EnsureBoxCollider(root, fallbackSize);
        PickupItem pickup = root.AddComponent<PickupItem>();
        SetSerializedField(pickup, "item", item);
        SetSerializedField(pickup, "promptOverride", prompt);
        SetSerializedField(pickup, "destroyOnPickup", true);

        GameObject prefab = SavePrefab(root, prefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateFallbackPickupPrefab(string prefabPath, Material material, InventoryItemDefinition item, string prompt, Vector3 size)
    {
        GameObject root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "PlaceholderVisual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = size;
        AssignMaterial(root, material);
        EnsureBoxCollider(root, size);

        PickupItem pickup = root.AddComponent<PickupItem>();
        SetSerializedField(pickup, "item", item);
        SetSerializedField(pickup, "promptOverride", prompt);
        SetSerializedField(pickup, "destroyOnPickup", true);

        GameObject prefab = SavePrefab(root, prefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateHidingSpotPrefab(string prefabPath, string sourcePrefabPath, Material overrideMaterial, Vector3 fallbackSize)
    {
        GameObject root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPath);
        if (source != null)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance != null)
            {
                instance.name = "Visual";
                instance.transform.SetParent(root.transform, false);
                AssignMaterial(instance, overrideMaterial);
            }
        }

        if (root.transform.childCount == 0)
        {
            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.name = "PlaceholderVisual";
            fallback.transform.SetParent(root.transform, false);
            fallback.transform.localScale = fallbackSize;
        }

        Transform hidePoint = new GameObject("HidePoint").transform;
        hidePoint.SetParent(root.transform, false);
        hidePoint.localPosition = new Vector3(0.0f, 0.0f, 0.0f);

        Transform exitPoint = new GameObject("ExitPoint").transform;
        exitPoint.SetParent(root.transform, false);
        exitPoint.localPosition = new Vector3(0.0f, 0.0f, -1.5f);

        EnsureBoxCollider(root, fallbackSize);
        HidingSpot hidingSpot = root.AddComponent<HidingSpot>();
        SetSerializedField(hidingSpot, "hidePoint", hidePoint);
        SetSerializedField(hidingSpot, "exitPoint", exitPoint);
        SetSerializedField(hidingSpot, "enterPrompt", "Hide");
        SetSerializedField(hidingSpot, "exitPrompt", "Exit hiding");

        GameObject prefab = SavePrefab(root, prefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static InventoryItemDefinition CreateItemDefinition(string assetPath, string itemId, string displayName, InventoryItemType itemType, GameObject worldPrefab)
    {
        InventoryItemDefinition item = AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(assetPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<InventoryItemDefinition>();
            AssetDatabase.CreateAsset(item, assetPath);
        }

        SetSerializedField(item, "itemId", itemId);
        SetSerializedField(item, "displayName", displayName);
        SetSerializedField(item, "itemType", itemType);
        SetSerializedField(item, "worldPrefab", worldPrefab);
        EditorUtility.SetDirty(item);
        return item;
    }

    private static GameObject SavePrefab(GameObject root, string prefabPath)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        AssetDatabase.ImportAsset(prefabPath);
        return prefab != null ? AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) : null;
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

    private static void EnsureBoxCollider(GameObject root, Vector3 fallbackSize)
    {
        BoxCollider collider = root.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = root.AddComponent<BoxCollider>();
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            collider.center = Vector3.zero;
            collider.size = fallbackSize;
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

    private static Texture2D LoadTexture(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static void SetSerializedField(Object target, string fieldName, object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        if (property == null)
        {
            Debug.LogWarning("[ImportedGameplayPrefabBuilder] Missing serialized field " + fieldName + " on " + target.name);
            return;
        }

        switch (property.propertyType)
        {
            case SerializedPropertyType.ObjectReference:
                property.objectReferenceValue = value as Object;
                break;
            case SerializedPropertyType.String:
                property.stringValue = value as string;
                break;
            case SerializedPropertyType.Boolean:
                property.boolValue = value is bool boolValue && boolValue;
                break;
            case SerializedPropertyType.Integer:
                property.intValue = value is int intValue ? intValue : 0;
                break;
            case SerializedPropertyType.Float:
                property.floatValue = value is float floatValue ? floatValue : 0f;
                break;
            case SerializedPropertyType.Enum:
                property.enumValueIndex = (int)value;
                break;
            default:
                Debug.LogWarning("[ImportedGameplayPrefabBuilder] Unsupported field type " + property.propertyType + " for " + fieldName);
                break;
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
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

    private static void SetColorIfExists(Material material, string propertyName, Color value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, value);
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

    private static string ToAbsolutePath(string assetPath)
    {
        return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
    }
}
