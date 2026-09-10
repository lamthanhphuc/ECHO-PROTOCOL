using System.IO;
using UnityEditor;
using UnityEngine;

public static class TeamToolGameplayPrefabBuilder
{
    private const string RunMarker = "Assets/Editor/.run_team_tool_gameplay_prefab_builder";
    private const string RootFolder = "Assets/Prefabs/Environment/Teamtoools/Gameplay";
    private const string ItemFolder = "Assets/ScriptableObjects/Inventory/TeamTools";
    private const string CoreAnimated = "Assets/Prefabs/Environment/Teamtoools/Animated/PF_CoreStabilizer_Device_Animated.prefab";
    private const string DecoyAnimated = "Assets/Prefabs/Environment/Teamtoools/Animated/PF_MotionDecoy_Device_Animated.prefab";
    private const string HologramVisual = "Assets/Prefabs/Environment/Teamtoools/Animated/PF_MotionDecoy_Hologram_Visual.prefab";
    private const string PlayerPrefab = "Assets/Prefabs/Player.prefab";

    private const string CoreGameplay = RootFolder + "/PF_CoreStabilizer_TeamTool.prefab";
    private const string DecoyGameplay = RootFolder + "/PF_MotionDecoy_TeamTool.prefab";
    private const string DecoyProjectile = RootFolder + "/PF_MotionDecoy_Projectile.prefab";
    private const string DecoyHologram = RootFolder + "/PF_MotionDecoy_Hologram.prefab";
    private const string CorePickup = RootFolder + "/PF_CoreStabilizer_Pickup.prefab";
    private const string DecoyPickup = RootFolder + "/PF_MotionDecoy_Pickup.prefab";
    private const string CoreItem = ItemFolder + "/SO_CoreStabilizer_TeamTool.asset";
    private const string DecoyItem = ItemFolder + "/SO_MotionDecoy_TeamTool.asset";

    [InitializeOnLoadMethod]
    private static void RunRequestedBuild()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(ToAbsolutePath(RunMarker))) return;
            File.Delete(ToAbsolutePath(RunMarker));
            BuildAll();
        };
    }

    [MenuItem("Tools/ECHO Protocol/Build Complete Team Tool Gameplay")]
    public static void BuildAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[TeamToolGameplayBuilder] Exit Play Mode before building.");
            return;
        }

        EnsureFolder(RootFolder);
        EnsureFolder(ItemFolder);
        RequirePrefab(CoreAnimated);
        RequirePrefab(DecoyAnimated);
        RequirePrefab(HologramVisual);

        GameObject hologram = BuildHologram();
        GameObject projectile = BuildProjectile(hologram);
        GameObject coreGameplay = BuildCoreGameplay();
        GameObject decoyGameplay = BuildDecoyGameplay(projectile);

        InventoryItemDefinition coreItem = GetOrCreateItem(CoreItem);
        InventoryItemDefinition decoyItem = GetOrCreateItem(DecoyItem);
        GameObject corePickup = BuildPickup(CorePickup, "PF_CoreStabilizer_Pickup", CoreAnimated, coreItem,
            "Pick up Core Stabilizer", new Vector3(0.32f, 0.22f, 0.32f));
        GameObject decoyPickup = BuildPickup(DecoyPickup, "PF_MotionDecoy_Pickup", DecoyAnimated, decoyItem,
            "Pick up Motion Decoy", new Vector3(0.28f, 0.35f, 0.28f));
        ConfigureItem(coreItem, "core_stabilizer", "Core Stabilizer", corePickup, coreGameplay);
        ConfigureItem(decoyItem, "motion_decoy", "Motion Decoy Projector", decoyPickup, decoyGameplay);
        AddControllerToPlayerPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = decoyGameplay;
        EditorGUIUtility.PingObject(decoyGameplay);
        Debug.Log("[TeamToolGameplayBuilder] COMPLETE: final gameplay prefabs, pickups, item definitions and Player controller are ready.");
    }

    [MenuItem("Tools/ECHO Protocol/Place Team Tool Local Test Pickups")]
    public static void PlaceTestPickups()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[TeamToolGameplayBuilder] Exit Play Mode before placing test pickups.");
            return;
        }

        PlayerInventory player = Object.FindAnyObjectByType<PlayerInventory>();
        if (player == null)
        {
            Debug.LogError("[TeamToolGameplayBuilder] No PlayerInventory found in the open scene.");
            return;
        }

        var root = new GameObject("TeamTool_LocalTest_UNSAVED");
        Undo.RegisterCreatedObjectUndo(root, "Place Team Tool test pickups");
        Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        PlacePrefab(CorePickup, player.transform.position + forward * 2.2f - right * 0.8f, root.transform);
        PlacePrefab(DecoyPickup, player.transform.position + forward * 2.2f + right * 0.8f, root.transform);
        Selection.activeGameObject = root;
        Debug.Log("[TeamToolGameplayBuilder] Test pickups placed in the open scene (scene intentionally not saved). Enter Play Mode, pick one up, press Q to use, T to drop it.");
    }

    private static GameObject BuildHologram()
    {
        GameObject root = InstantiatePrefab(HologramVisual);
        root.name = "PF_MotionDecoy_Hologram";
        MotionDecoyHologram runtime = root.GetComponent<MotionDecoyHologram>() ?? root.AddComponent<MotionDecoyHologram>();
        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        Animator vfx = root.GetComponent<Animator>();
        Animator body = null;
        foreach (Animator candidate in animators) if (candidate != vfx) { body = candidate; break; }
        SetObject(runtime, "vfxAnimator", vfx);
        SetObject(runtime, "bodyAnimator", body);
        return SaveAndDestroy(root, DecoyHologram);
    }

    private static GameObject BuildProjectile(GameObject hologram)
    {
        var root = new GameObject("PF_MotionDecoy_Projectile");
        GameObject visual = InstantiatePrefab(DecoyAnimated);
        visual.name = "AnimatedVisual";
        visual.transform.SetParent(root.transform, false);
        var body = root.AddComponent<Rigidbody>();
        body.mass = 0.8f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        var collider = root.AddComponent<SphereCollider>();
        collider.radius = 0.18f;
        collider.center = new Vector3(0f, 0.18f, 0f);
        MotionDecoyProjectile runtime = root.AddComponent<MotionDecoyProjectile>();
        SetObject(runtime, "body", body);
        SetObject(runtime, "bodyCollider", collider);
        SetObject(runtime, "animator", visual.GetComponent<Animator>());
        SetObject(runtime, "projectionOrigin", FindDeepChild(visual.transform, "ProjectionOrigin"));
        SetObject(runtime, "hologramPrefab", hologram);
        return SaveAndDestroy(root, DecoyProjectile);
    }

    private static GameObject BuildCoreGameplay()
    {
        var root = new GameObject("PF_CoreStabilizer_TeamTool");
        root.transform.localPosition = new Vector3(0.33f, 1.18f, 0.48f);
        root.transform.localRotation = Quaternion.Euler(8f, -18f, 4f);
        GameObject visual = InstantiatePrefab(CoreAnimated);
        visual.name = "AnimatedVisual";
        visual.transform.SetParent(root.transform, false);
        CoreStabilizerTeamTool runtime = root.AddComponent<CoreStabilizerTeamTool>();
        SetObject(runtime, "animator", visual.GetComponent<Animator>());
        SetObject(runtime, "supportFieldVfx", FindDeepChild(visual.transform, "SupportFieldVFX"));
        return SaveAndDestroy(root, CoreGameplay);
    }

    private static GameObject BuildDecoyGameplay(GameObject projectile)
    {
        var root = new GameObject("PF_MotionDecoy_TeamTool");
        root.transform.localPosition = new Vector3(0.34f, 1.16f, 0.5f);
        root.transform.localRotation = Quaternion.Euler(8f, -18f, 4f);
        GameObject visual = InstantiatePrefab(DecoyAnimated);
        visual.name = "AnimatedVisual";
        visual.transform.SetParent(root.transform, false);
        MotionDecoyTeamTool runtime = root.AddComponent<MotionDecoyTeamTool>();
        SetObject(runtime, "animator", visual.GetComponent<Animator>());
        SetObject(runtime, "projectilePrefab", projectile);
        return SaveAndDestroy(root, DecoyGameplay);
    }

    private static GameObject BuildPickup(string path, string name, string visualPath,
        InventoryItemDefinition item, string prompt, Vector3 size)
    {
        var root = new GameObject(name);
        GameObject visual = InstantiatePrefab(visualPath);
        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        var collider = root.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = size;
        collider.center = Vector3.up * size.y * 0.5f;
        PickupItem pickup = root.AddComponent<PickupItem>();
        SetObject(pickup, "item", item);
        SetString(pickup, "promptOverride", prompt);
        return SaveAndDestroy(root, path);
    }

    private static void AddControllerToPlayerPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
        try
        {
            Transform socket = root.transform.Find("TeamToolSocket");
            if (socket == null)
            {
                var socketObject = new GameObject("TeamToolSocket");
                socket = socketObject.transform;
                socket.SetParent(root.transform, false);
            }
            socket.localPosition = Vector3.zero;
            socket.localRotation = Quaternion.identity;
            PlayerTeamToolController controller = root.GetComponent<PlayerTeamToolController>() ?? root.AddComponent<PlayerTeamToolController>();
            SetObject(controller, "inventory", root.GetComponent<PlayerInventory>());
            SetObject(controller, "handSocket", socket);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static InventoryItemDefinition GetOrCreateItem(string path)
    {
        InventoryItemDefinition item = AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(path);
        if (item != null) return item;
        item = ScriptableObject.CreateInstance<InventoryItemDefinition>();
        AssetDatabase.CreateAsset(item, path);
        return item;
    }

    private static void ConfigureItem(InventoryItemDefinition item, string id, string displayName,
        GameObject worldPrefab, GameObject gameplayPrefab)
    {
        SerializedObject serialized = new SerializedObject(item);
        serialized.FindProperty("itemId").stringValue = id;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("itemType").enumValueIndex = (int)InventoryItemType.TeamTool;
        serialized.FindProperty("worldPrefab").objectReferenceValue = worldPrefab;
        serialized.FindProperty("teamToolGameplayPrefab").objectReferenceValue = gameplayPrefab;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
    }

    private static void PlacePrefab(string path, Vector3 position, Transform parent)
    {
        GameObject prefab = RequirePrefab(path);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "Place " + prefab.name);
        instance.transform.SetParent(parent, true);
        instance.transform.position = position;
    }

    private static GameObject InstantiatePrefab(string path)
    {
        return (GameObject)PrefabUtility.InstantiatePrefab(RequirePrefab(path));
    }

    private static GameObject RequirePrefab(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new FileNotFoundException("Required prefab not found", path);
        return prefab;
    }

    private static GameObject SaveAndDestroy(GameObject root, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static Transform FindDeepChild(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name) return child;
        return null;
    }

    private static void SetObject(Object target, string property, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty field = serialized.FindProperty(property);
        if (field == null) throw new System.MissingFieldException(target.GetType().Name, property);
        field.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetString(Object target, string property, string value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(property).stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static string ToAbsolutePath(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
    }
}
