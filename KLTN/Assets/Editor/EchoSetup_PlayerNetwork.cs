using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using System.IO;

/// <summary>
/// EchoProtocol one-click setup:
///   1. Synchronize components & assign all references in PlayerNetwork.prefab
///   2. Ensure child Flashlight_Light and NetworkPlayerFlashlight
///   3. Ensure PlayerHidingController, PlayerSpectateController, InteractionPromptOnGUI
///   4. Verify FusionPlayerLifecycle on NetworkRunner.prefab points to PlayerNetwork.prefab
///   5. Uncheck Root Transform Position (XZ) Bake Into Pose (lockRootPositionXZ = false) for all player FBX animations
/// Menu: EchoProtocol > Setup > Fix PlayerNetwork + Bake Anim XZ
/// </summary>
[InitializeOnLoad]
public static class EchoSetup_PlayerNetwork
{
    private const string RunMarker = "Assets/Editor/.run_setup_playernetwork";

    static EchoSetup_PlayerNetwork()
    {
        EditorApplication.delayCall += () =>
        {
            string fullMarkerPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", RunMarker));
            if (File.Exists(fullMarkerPath))
            {
                try { File.Delete(fullMarkerPath); } catch { }
                RunAll();
            }
        };
    }

    [MenuItem("EchoProtocol/Setup/Fix PlayerNetwork + Bake Anim XZ")]
    [MenuItem("Tools/ECHO Protocol/Fix PlayerNetwork + Bake Anim XZ")]
    public static void RunAll()
    {
        Debug.Log("<color=#00E5FF>[EchoSetup]</color> Bắt đầu đồng bộ PlayerNetwork & tắt Root XZ bake...");
        int fixCount = FixPlayerNetworkPrefab();
        int runnerFixed = VerifyNetworkRunner();
        int sceneFixed = FixScenePlayerSetup();
        int bakeCount = BakeAllAnimXZ();
        Debug.Log($"<color=#00E5FF>[EchoSetup]</color> Hoàn thành! Đã sửa {fixCount} thành phần/references trên PlayerNetwork.prefab, {runnerFixed} trên NetworkRunner, và xử lý {bakeCount} animation clips.");
        Debug.Log($"<color=#00E5FF>[EchoSetup]</color> Hoàn thành! Đã sửa {fixCount} refs trên PlayerNetwork.prefab, {runnerFixed} trên NetworkRunner, {sceneFixed} trong scene, và xử lý {bakeCount} animation clips.");
        EditorUtility.DisplayDialog("EchoSetup Complete",
            $"Hoàn tất đồng bộ PlayerNetwork!\n- Đã sửa/gán {fixCount} references & components trên PlayerNetwork.prefab\n- Runner verified: {runnerFixed}\n- Đã tắt Root XZ Bake Into Pose trên {bakeCount} FBX files.",
            $"Hoàn tất đồng bộ PlayerNetwork!\n- Đã sửa/gán {fixCount} references & components trên PlayerNetwork.prefab\n- Runner verified: {runnerFixed}\n- Scene fixed: {sceneFixed}\n- Đã tắt Root XZ Bake Into Pose trên {bakeCount} FBX files.",
            "OK");
    }

    // =========================================================
    // PART 1 - PlayerNetwork.prefab component & reference sync
    // =========================================================
    public static int FixPlayerNetworkPrefab()
    {
        string prefabPath = "Assets/Prefabs/PlayerNetwork.prefab";
        var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefabAsset == null)
        {
            Debug.LogError($"[EchoSetup] Không tìm thấy PlayerNetwork.prefab tại {prefabPath}");
            return 0;
        }

        var beaconDeployedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Gameplay/Imported/DistressBeaconDeployed.prefab");
        var beaconClosedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Gameplay/Imported/DistressBeaconClosed.prefab");
        var firstAidKitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/GeeKay3D/First-Aid-Set/Assets/Prefabs/FirstAidKit_Red.prefab");
        var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
            "Assets/InputSystem_Actions.inputactions");

        int fixCount = 0;

        using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
        {
            var root = scope.prefabContentsRoot;

            var networkMovement     = root.GetComponent<EchoProtocol.Networking.NetworkPlayerMovement>();
            var characterController = root.GetComponent<CharacterController>();
            var networkInteractor   = root.GetComponent<EchoProtocol.Networking.NetworkPlayerInteractor>();
            var animDriver          = root.GetComponent<PlayerAnimatorDriver>();
            var teamToolView        = root.GetComponent<NetworkTeamToolHeldView>();
            var upperBodyAim        = root.GetComponent<PlayerUpperBodyAim>();
            var lobbyState          = root.GetComponent<EchoProtocol.Networking.LobbyPlayerState>();

            // 1. Flashlight_Light child object
            Transform flashlightChild = root.transform.Find("Flashlight_Light");
            Light flashlightLight = null;
            if (flashlightChild == null)
            {
                var lightObj = new GameObject("Flashlight_Light");
                lightObj.transform.SetParent(root.transform, false);
                lightObj.transform.localPosition = new Vector3(0.0645f, 0.8569f, 0.0815f);
                lightObj.transform.localRotation = Quaternion.identity;

                flashlightLight = lightObj.AddComponent<Light>();
                flashlightLight.type = LightType.Spot;
                flashlightLight.range = 28f;
                flashlightLight.spotAngle = 65f;
                flashlightLight.innerSpotAngle = 45f;
                flashlightLight.color = new Color(1f, 0.96f, 0.88f);
                flashlightLight.intensity = 2.8f;
                fixCount++;
                Debug.Log("[EchoSetup] Đã tạo child GameObject Flashlight_Light trên PlayerNetwork");
            }
            else
            {
                flashlightLight = flashlightChild.GetComponent<Light>();
                if (flashlightLight == null)
                {
                    flashlightLight = flashlightChild.gameObject.AddComponent<Light>();
                    flashlightLight.type = LightType.Spot;
                    flashlightLight.range = 28f;
                    flashlightLight.spotAngle = 65f;
                    flashlightLight.innerSpotAngle = 45f;
                    flashlightLight.color = new Color(1f, 0.96f, 0.88f);
                    flashlightLight.intensity = 2.8f;
                    fixCount++;
                }
            }

            // 2. NetworkPlayerFlashlight component
            var networkFlashlight = root.GetComponent<EchoProtocol.Networking.NetworkPlayerFlashlight>();
            if (networkFlashlight == null)
            {
                networkFlashlight = root.AddComponent<EchoProtocol.Networking.NetworkPlayerFlashlight>();
                fixCount++;
                Debug.Log("[EchoSetup] Đã thêm NetworkPlayerFlashlight vào PlayerNetwork");
            }
            if (networkFlashlight != null)
            {
                var so = new SerializedObject(networkFlashlight);
                var lightProp = so.FindProperty("_flashlight");
                if (lightProp != null && lightProp.objectReferenceValue == null && flashlightLight != null)
                {
                    lightProp.objectReferenceValue = flashlightLight;
                    fixCount++;
                }
                var actProp = so.FindProperty("_inputActions");
                if (actProp != null && actProp.objectReferenceValue == null && inputActions != null)
                {
                    actProp.objectReferenceValue = inputActions;
                    fixCount++;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 3. PlayerHidingController component
            var hidingController = root.GetComponent<PlayerHidingController>();
            if (hidingController == null)
            {
                hidingController = root.AddComponent<PlayerHidingController>();
                fixCount++;
                Debug.Log("[EchoSetup] Đã thêm PlayerHidingController vào PlayerNetwork");
            }
            if (hidingController != null)
            {
                var so = new SerializedObject(hidingController);
                var netMovProp = so.FindProperty("networkMovement");
                if (netMovProp != null && netMovProp.objectReferenceValue == null && networkMovement != null)
                {
                    netMovProp.objectReferenceValue = networkMovement;
                    fixCount++;
                }
                var netIntProp = so.FindProperty("networkInteractor");
                if (netIntProp != null && netIntProp.objectReferenceValue == null && networkInteractor != null)
                {
                    netIntProp.objectReferenceValue = networkInteractor;
                    fixCount++;
                }
                var actProp = so.FindProperty("inputActions");
                if (actProp != null && actProp.objectReferenceValue == null && inputActions != null)
                {
                    actProp.objectReferenceValue = inputActions;
                    fixCount++;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 4. PlayerSpectateController component
            var spectateController = root.GetComponent<PlayerSpectateController>();
            if (spectateController == null)
            {
                spectateController = root.AddComponent<PlayerSpectateController>();
                fixCount++;
                Debug.Log("[EchoSetup] Đã thêm PlayerSpectateController vào PlayerNetwork");
            }

            // 5. InteractionPromptOnGUI component
            var promptGUI = root.GetComponent<InteractionPromptOnGUI>();
            if (promptGUI == null)
            {
                promptGUI = root.AddComponent<InteractionPromptOnGUI>();
                fixCount++;
                Debug.Log("[EchoSetup] Đã thêm InteractionPromptOnGUI vào PlayerNetwork");
            }
            if (promptGUI != null)
            {
                var so = new SerializedObject(promptGUI);
                var netIntProp = so.FindProperty("networkInteractor");
                if (netIntProp != null && netIntProp.objectReferenceValue == null && networkInteractor != null)
                {
                    netIntProp.objectReferenceValue = networkInteractor;
                    fixCount++;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 6. PlayerAnimatorDriver
            if (animDriver != null)
            {
                var so = new SerializedObject(animDriver);
                var movProp = so.FindProperty("movement");
                if (movProp != null && movProp.objectReferenceValue == null && networkMovement != null)
                { movProp.objectReferenceValue = networkMovement; fixCount++; }
                var ccProp = so.FindProperty("characterController");
                if (ccProp != null && ccProp.objectReferenceValue == null && characterController != null)
                { ccProp.objectReferenceValue = characterController; fixCount++; }
                var lobbyProp = so.FindProperty("lobbyState");
                if (lobbyProp != null && lobbyProp.objectReferenceValue == null && lobbyState != null)
                { lobbyProp.objectReferenceValue = lobbyState; fixCount++; }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 7. NetworkPlayerInteractor
            if (networkInteractor != null)
            {
                var so = new SerializedObject(networkInteractor);
                var beaconProp = so.FindProperty("_noiseMakerBeaconPrefab");
                if (beaconProp != null && beaconProp.objectReferenceValue == null && beaconDeployedPrefab != null)
                { beaconProp.objectReferenceValue = beaconDeployedPrefab; fixCount++; }
                var actProp = so.FindProperty("_inputActions");
                if (actProp != null && actProp.objectReferenceValue == null && inputActions != null)
                { actProp.objectReferenceValue = inputActions; fixCount++; }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 8. NetworkTeamToolHeldView
            if (teamToolView != null)
            {
                var so = new SerializedObject(teamToolView);
                var v2 = so.FindProperty("toolVisual_2");
                if (v2 != null && v2.objectReferenceValue == null && beaconClosedPrefab != null)
                { v2.objectReferenceValue = beaconClosedPrefab; fixCount++; }
                var v3 = so.FindProperty("toolVisual_3");
                if (v3 != null && v3.objectReferenceValue == null && firstAidKitPrefab != null)
                { v3.objectReferenceValue = firstAidKitPrefab; fixCount++; }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 9. PlayerUpperBodyAim
            if (upperBodyAim != null)
            {
                var so = new SerializedObject(upperBodyAim);
                var rootProp = so.FindProperty("playerRoot");
                if (rootProp != null && rootProp.objectReferenceValue == null)
                { rootProp.objectReferenceValue = root.transform; fixCount++; }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 10. NetworkPlayerMovement inputActions
            if (networkMovement != null)
            {
                var so = new SerializedObject(networkMovement);
                var actProp = so.FindProperty("_inputActions");
                if (actProp != null && actProp.objectReferenceValue == null && inputActions != null)
                { actProp.objectReferenceValue = inputActions; fixCount++; }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        AssetDatabase.SaveAssets();
        return fixCount;
    }

    // =========================================================
    // PART 2 - Verify NetworkRunner playerPrefab
    // =========================================================
    public static int VerifyNetworkRunner()
    {
        string runnerPrefabPath = "Assets/Prefabs/NetworkRunner.prefab";
        var runnerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(runnerPrefabPath);
        if (runnerAsset == null) return 0;

        var playerNetworkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerNetwork.prefab");
        if (playerNetworkPrefab == null) return 0;

        int changed = 0;
        using (var scope = new PrefabUtility.EditPrefabContentsScope(runnerPrefabPath))
        {
            var root = scope.prefabContentsRoot;
            var lifecycle = root.GetComponent<EchoProtocol.Networking.FusionPlayerLifecycle>();
            if (lifecycle != null)
            {
                var so = new SerializedObject(lifecycle);
                var prefabProp = so.FindProperty("playerPrefab");
                var netObj = playerNetworkPrefab.GetComponent<Fusion.NetworkObject>();
                if (prefabProp != null && prefabProp.objectReferenceValue != netObj)
                {
                    prefabProp.objectReferenceValue = netObj;
                    changed++;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log("[EchoSetup] Đã gán FusionPlayerLifecycle.playerPrefab = PlayerNetwork");
                }
            }
        }

        if (changed > 0)
        {
            AssetDatabase.SaveAssets();
        }

        return changed;
    }

    // =========================================================
    // PART 3 - Bake Root Transform Position (XZ) into pose
    // =========================================================
    public static int BakeAllAnimXZ()
    {
        string[] fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Animations" });
        int count = 0;

        foreach (string guid in fbxGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;

            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            bool changed = false;
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.defaultClipAnimations;

            foreach (var clip in clips)
            {
                if (clip.lockRootPositionXZ)
                {
                    clip.lockRootPositionXZ = false;  // Tắt Bake Into Pose XZ
                    changed = true;
                }
            }

            if (changed)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                count++;
                Debug.Log($"[EchoSetup] Đã tắt Root XZ Bake: {Path.GetFileName(path)}");
            }
        }

        return count;
    }

    // =========================================================
    // PART 4 - Setup active scene to use PlayerNetwork as main
    // =========================================================
    public static int FixScenePlayerSetup()
    {
        var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        int fixes = 0;

        // Tim PlayerNetwork trong active scene
        var netMovements = Object.FindObjectsByType<EchoProtocol.Networking.NetworkPlayerMovement>(FindObjectsInactive.Include);
        Transform netPlayerTransform = null;
        foreach (var nm in netMovements)
        {
            if (nm != null)
            {
                netPlayerTransform = nm.transform;
                if (!nm.gameObject.activeSelf)
                {
                    nm.gameObject.SetActive(true);
                    EditorUtility.SetDirty(nm.gameObject);
                    fixes++;
                }
                break;
            }
        }

        // Tat legacy Player neu co trong scene de khong bi xung dot 2 nhan vat
        var players = GameObject.FindGameObjectsWithTag("Player");
        foreach (var p in players)
        {
            if (p != null && p.GetComponent<PlayerMovement>() != null)
            {
                if (netPlayerTransform != null && p.transform != netPlayerTransform && p.activeSelf)
                {
                    p.SetActive(false);
                    EditorUtility.SetDirty(p);
                    fixes++;
                    Debug.Log($"[EchoSetup] Đã tắt legacy Player '{p.name}' trong scene để dùng PlayerNetwork");
                }
            }
        }

        // Gan target cho PlayerCamera trong scene
        var playerCamera = Object.FindAnyObjectByType<PlayerCamera>();
        if (playerCamera != null && netPlayerTransform != null)
        {
            var so = new SerializedObject(playerCamera);
            var targetProp = so.FindProperty("target");
            if (targetProp != null && targetProp.objectReferenceValue != netPlayerTransform)
            {
                targetProp.objectReferenceValue = netPlayerTransform;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(playerCamera);
                fixes++;
                Debug.Log($"[EchoSetup] Đã gán PlayerCamera.target = PlayerNetwork trong scene '{activeScene.name}'");
            }
        }

        if (fixes > 0 && activeScene.isLoaded)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
        }

        return fixes;
    }
}
