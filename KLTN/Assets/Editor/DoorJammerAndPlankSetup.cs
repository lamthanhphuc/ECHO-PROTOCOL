using System.IO;
using EchoProtocol.Networking;
using EchoProtocol.Tools.Scanner;
using Fusion;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace EchoProtocol.Editor
{
    [InitializeOnLoad]
    public static class DoorJammerAndPlankSetup
    {
        private const string RunMarker = "Assets/Editor/.run_door_jammer_setup";
        private const string DoorPrefabPath = "Assets/Prefabs/Environment/Door/PF_SciFiSlidingDoor.prefab";
        private const string JammerPrefabPath = "Assets/Resources/Network/PF_DoorJammer.prefab";
        private const string PlankItemPath = "Assets/ScriptableObjects/Inventory/TeamTools/SO_Plank_ItemDefinition.asset";
        private const string PlankPickupPath = "Assets/Prefabs/Tools/PF_Plank_Pickup.prefab";
        private const string PlankImportedPrefabPath = "Assets/Prefabs/Gameplay/Imported/PF_Plank_Imported.prefab";
        private const string PlankImportAltPrefabPath = "Assets/import/plank/PF_Plank.prefab";
        private const string PlankFbxPath = "Assets/import/plank/source/Plank4.fbx";
        private const string PlankMaterialPath = "Assets/Materials/ImportedGameplay/M_Plank_Imported.mat";

        static DoorJammerAndPlankSetup()
        {
            EditorApplication.update += CheckAndRun;
        }

        private static void CheckAndRun()
        {
            string markerPath = Path.Combine(Application.dataPath, "Editor/.run_door_jammer_setup");
            if (File.Exists(markerPath))
            {
                File.Delete(markerPath);
                RunSetup();
            }

            string scannerMarkerPath = Path.Combine(Application.dataPath, "Editor/.run_field_scanner_setup");
            if (File.Exists(scannerMarkerPath))
            {
                File.Delete(scannerMarkerPath);
                EchoProtocol.EditorTools.FieldScannerSetupBuilder.ExecuteSetup();
            }

            string testMarkerPath = Path.Combine(Application.dataPath, "Editor/.run_door_tests");
            if (File.Exists(testMarkerPath))
            {
                File.Delete(testMarkerPath);
                RunDoorAndStalkerTests();
            }
        }

        [MenuItem("Tools/ECHO Protocol/Setup Door Jammer and Planks")]
        public static void RunSetup()
        {
            Debug.Log("[DoorJammerAndPlankSetup] Starting setup...");
            SetupDoorJammerPrefab();
            SetupDoorPrefab();
            SetupPlankItemAndPickup();
            UpgradePlayerPrefabs();
            PlaceTestBrokenDoorInScene();
            MotionDecoyAndCoreStabilizerSetup.RunSetup();
            EchoProtocol.EditorTools.FieldScannerSetupBuilder.ExecuteSetup();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DoorJammerAndPlankSetup] Setup completed successfully!");
        }

        [MenuItem("Tools/ECHO Protocol/Place Test Broken Door In Scene")]
        public static void PlaceTestBrokenDoorInScene()
        {
            var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (activeScene.name != "SciFi")
            {
                activeScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SciFi.unity");
            }

            GameObject doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
            GameObject plankPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlankPickupPath);

            if (doorPrefab == null)
            {
                Debug.LogError($"[PlaceTestBrokenDoor] Cannot find door prefab at {DoorPrefabPath}");
                return;
            }

            Vector3 refPos = new Vector3(-85f, 3.11f, -22.4f);
            var playerObj = GameObject.Find("PlayerNetwork");
            if (playerObj != null)
            {
                refPos = playerObj.transform.position;
            }

            GameObject existingTestDoor = GameObject.Find("PF_SciFiSlidingDoor_Broken_Test");
            if (existingTestDoor == null)
            {
                existingTestDoor = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab);
                existingTestDoor.name = "PF_SciFiSlidingDoor_Broken_Test";
            }

            existingTestDoor.transform.position = refPos + new Vector3(1.36f, 0.3f, 3.5f);
            existingTestDoor.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            existingTestDoor.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);

            var doorComp = existingTestDoor.GetComponent<NetworkSlidingDoor>();
            if (doorComp != null)
            {
                SerializedObject doorSo = new SerializedObject(doorComp);
                var brokenProp = doorSo.FindProperty("_startsBroken");
                if (brokenProp != null)
                {
                    brokenProp.boolValue = true;
                }
                var jammerProp = doorSo.FindProperty("_doorJammerPrefab");
                if (jammerProp != null)
                {
                    jammerProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<NetworkObject>(JammerPrefabPath);
                }
                doorSo.ApplyModifiedPropertiesWithoutUndo();
            }

            if (plankPrefab != null)
            {
                var oldImported = GameObject.Find("PF_Plank_Imported_TestPickup");
                if (oldImported != null) Object.DestroyImmediate(oldImported);

                GameObject existingPlankPickup = GameObject.Find("PF_Plank_Pickup_Test");
                if (existingPlankPickup == null)
                {
                    existingPlankPickup = (GameObject)PrefabUtility.InstantiatePrefab(plankPrefab);
                    existingPlankPickup.name = "PF_Plank_Pickup_Test";
                }

                existingPlankPickup.transform.position = refPos + new Vector3(1.0f, 0.1f, 1.0f);
                existingPlankPickup.transform.rotation = Quaternion.identity;
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(activeScene);

            Debug.Log($"[PlaceTestBrokenDoor] Successfully placed PF_SciFiSlidingDoor_Broken_Test at {existingTestDoor.transform.position} (startsBroken=true) and plank pickup at {refPos + new Vector3(1.0f, 0.1f, 1.0f)}!");
        }

        [MenuItem("Tools/ECHO Protocol/Run Door And Stalker Tests")]
        public static void RunDoorAndStalkerTests()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var callbacks = new DoorTestCallbacks(api);
            api.RegisterCallbacks(callbacks);

            var settings = new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = new[]
                {
                    "EchoProtocol.Player.EditMode.Tests",
                    "EchoProtocol.AI.Stalker.EditMode.Tests",
                    "Assembly-CSharp-Editor"
                }
            })
            {
                runSynchronously = true
            };

            Debug.Log("[DOOR-TESTS] Starting test execution...");
            api.Execute(settings);
        }

        private sealed class DoorTestCallbacks : ICallbacks
        {
            private readonly TestRunnerApi _api;
            private readonly System.Text.StringBuilder _failures = new System.Text.StringBuilder();

            public DoorTestCallbacks(TestRunnerApi api)
            {
                _api = api;
            }

            public void RunStarted(ITestAdaptor testsToRun) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                var status = result.FailCount == 0 && result.PassCount > 0 ? "PASS" : "FAIL";
                string summary = $"status={status} passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount} duration={result.Duration:F3}s";
                Debug.Log($"[DOOR-TESTS] Finished: {summary}");
                try
                {
                    string outPath = Path.Combine(Application.dataPath, "../test_results.txt");
                    File.WriteAllText(outPath, summary + "\n" + _failures.ToString());
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[DOOR-TESTS] Could not write test_results.txt: " + ex.Message);
                }
                _api.UnregisterCallbacks(this);
                Object.DestroyImmediate(_api);
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.HasChildren || result.TestStatus.ToString() != "Failed")
                {
                    return;
                }

                string failMsg = $"FAILED: {result.FullName} -> {result.Message}";
                Debug.LogError($"[DOOR-TESTS] {failMsg}");
                _failures.AppendLine(failMsg);
            }
        }

        public static void SetupDoorJammerPrefab()
        {
            GameObject plankPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlankImportedPrefabPath);
            if (plankPrefab == null)
            {
                plankPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlankImportAltPrefabPath);
            }
            GameObject sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(PlankFbxPath);
            Material plankMat = AssetDatabase.LoadAssetAtPath<Material>(PlankMaterialPath);
            AudioClip breakClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/stalker/door_jammer_break.wav");

            // Extract shared mesh from imported plank prefab or source FBX
            Mesh plankMesh = null;
            if (plankPrefab != null)
            {
                var mf = plankPrefab.GetComponentInChildren<MeshFilter>(true);
                if (mf != null) plankMesh = mf.sharedMesh;
            }
            if (plankMesh == null && sourceModel != null)
            {
                var mf = sourceModel.GetComponentInChildren<MeshFilter>(true);
                if (mf != null) plankMesh = mf.sharedMesh;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(JammerPrefabPath);
            if (root == null)
            {
                Debug.LogError($"[DoorJammerAndPlankSetup] Failed to load {JammerPrefabPath}");
                return;
            }

            try
            {
                // Configure BoxCollider (covers full door opening from Y = -0.04 to 3.15)
                var boxCol = root.GetComponent<BoxCollider>();
                if (boxCol == null)
                {
                    boxCol = root.AddComponent<BoxCollider>();
                }
                boxCol.center = new Vector3(0f, 0.06f, 0f);
                boxCol.size = new Vector3(0.5f, 3.5f, 3.6f);
                boxCol.isTrigger = false;
                boxCol.enabled = false;

                // Find or create VisualRoot
                Transform visualRootTransform = root.transform.Find("VisualRoot");
                if (visualRootTransform == null)
                {
                    var go = new GameObject("VisualRoot");
                    visualRootTransform = go.transform;
                    visualRootTransform.SetParent(root.transform, false);
                }
                visualRootTransform.localPosition = new Vector3(0f, -1.5f, 0f);
                visualRootTransform.localRotation = Quaternion.identity;
                visualRootTransform.localScale = Vector3.one;

                // Remove any old MeshFilter/MeshRenderer directly on VisualRoot
                var oldFilter = visualRootTransform.GetComponent<MeshFilter>();
                if (oldFilter != null) Object.DestroyImmediate(oldFilter);
                var oldRenderer = visualRootTransform.GetComponent<MeshRenderer>();
                if (oldRenderer != null) Object.DestroyImmediate(oldRenderer);

                // Clear any existing children in VisualRoot
                for (int i = visualRootTransform.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(visualRootTransform.GetChild(i).gameObject);
                }

                // Exact transforms tuned by user in Inspector for Plank_2 to Plank_7
                (string name, Vector3 pos, Vector3 rot, Vector3 scale)[] plankConfigs = new[]
                {
                    ("Plank_2", new Vector3(-0.02f, -0.04f, 0f), new Vector3(90f, 0f, -87.8f), new Vector3(20f, 20f, 20f)),
                    ("Plank_3", new Vector3(0.03f, 0.56f, 0f), new Vector3(90f, 0f, -91.5f), new Vector3(20f, 20f, 20f)),
                    ("Plank_4", new Vector3(-0.01f, 1.24f, 0f), new Vector3(90f, 0f, -88.2f), new Vector3(20f, 20f, 20f)),
                    ("Plank_5", new Vector3(0.02f, 1.92f, 0f), new Vector3(90f, 0f, -92f), new Vector3(20f, 20f, 20f)),
                    ("Plank_6", new Vector3(-0.02f, 2.57f, 0f), new Vector3(90f, 0f, -88.5f), new Vector3(20f, 20f, 20f)),
                    ("Plank_7", new Vector3(0.01f, 3.15f, 0f), new Vector3(90f, 0f, -91.2f), new Vector3(20f, 20f, 20f)),
                };

                for (int i = 0; i < plankConfigs.Length; i++)
                {
                    var cfg = plankConfigs[i];
                    GameObject plankObj = new GameObject(cfg.name);
                    plankObj.transform.SetParent(visualRootTransform, false);
                    plankObj.transform.localPosition = cfg.pos;
                    plankObj.transform.localRotation = Quaternion.Euler(cfg.rot);
                    plankObj.transform.localEulerAngles = cfg.rot;
                    plankObj.transform.localScale = cfg.scale;

                    GameObject visualChild = new GameObject("Visual");
                    visualChild.transform.SetParent(plankObj.transform, false);
                    visualChild.transform.localPosition = Vector3.zero;
                    visualChild.transform.localRotation = Quaternion.identity;
                    visualChild.transform.localScale = new Vector3(22.239828f, 0.5456054f, 2.9859567f);

                    var mf = visualChild.AddComponent<MeshFilter>();
                    mf.sharedMesh = plankMesh;
                    var mr = visualChild.AddComponent<MeshRenderer>();
                    if (plankMat != null)
                    {
                        mr.sharedMaterial = plankMat;
                    }

                    plankObj.SetActive(true);
                }

                // Configure NetworkDoorJammer
                var jammer = root.GetComponent<NetworkDoorJammer>();
                if (jammer != null)
                {
                    SerializedObject so = new SerializedObject(jammer);
                    so.FindProperty("_blockingCollider").objectReferenceValue = boxCol;
                    so.FindProperty("_visualRoot").objectReferenceValue = visualRootTransform;
                    so.FindProperty("_breakClip").objectReferenceValue = breakClip;
                    so.FindProperty("_breakDurationSeconds").floatValue = 3f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, JammerPrefabPath);
                Debug.Log($"[DoorJammerAndPlankSetup] Saved {JammerPrefabPath} with {plankConfigs.Length} planks from {(plankPrefab != null ? plankPrefab.name : "sourceModel")}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void SetupDoorPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(DoorPrefabPath);
            if (root == null)
            {
                Debug.LogError($"[DoorJammerAndPlankSetup] Failed to load {DoorPrefabPath}");
                return;
            }

            try
            {
                // Ensure JammerMount exists at opening center
                Transform jammerMount = root.transform.Find("JammerMount");
                if (jammerMount == null)
                {
                    var mountObj = new GameObject("JammerMount");
                    jammerMount = mountObj.transform;
                    jammerMount.SetParent(root.transform, false);
                }
                jammerMount.localPosition = new Vector3(0.045f, -0.171f, -0.906f);
                jammerMount.localRotation = Quaternion.identity;
                jammerMount.localScale = Vector3.one;

                NetworkObject jammerPrefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(JammerPrefabPath);
                AudioClip doorBreakClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/stalker/door_break.wav");
                AudioClip jammerDeployClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/door/door_jammer_deploy.wav");

                var door = root.GetComponent<NetworkSlidingDoor>();
                if (door != null)
                {
                    SerializedObject so = new SerializedObject(door);
                    so.FindProperty("_jammerMount").objectReferenceValue = jammerMount;
                    so.FindProperty("_doorJammerPrefab").objectReferenceValue = jammerPrefab;
                    so.FindProperty("_doorBreakClip").objectReferenceValue = doorBreakClip;
                    so.FindProperty("_jammerDeployClip").objectReferenceValue = jammerDeployClip;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, DoorPrefabPath);
                Debug.Log($"[DoorJammerAndPlankSetup] Saved {DoorPrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void SetupPlankItemAndPickup()
        {
            // 1. ScriptableObject item definition
            InventoryItemDefinition itemDef = AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(PlankItemPath);
            if (itemDef == null)
            {
                string dir = Path.GetDirectoryName(PlankItemPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                itemDef = ScriptableObject.CreateInstance<InventoryItemDefinition>();
                AssetDatabase.CreateAsset(itemDef, PlankItemPath);
            }

            // 2. Setup standalone pickup prefab first
            SetupStandalonePickupPrefab(itemDef);
            GameObject pickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlankPickupPath);
            ConfigurePlankPickupPrefab(PlankPickupPath, itemDef);

            // Configure imported prefabs if present
            ConfigurePlankPickupPrefab(PlankImportedPrefabPath, itemDef);
            ConfigurePlankPickupPrefab(PlankImportAltPrefabPath, itemDef);

            SerializedObject itemSo = new SerializedObject(itemDef);
            itemSo.FindProperty("itemId").stringValue = "plank";
            itemSo.FindProperty("displayName").stringValue = "Wooden Planks";
            itemSo.FindProperty("itemType").intValue = (int)InventoryItemType.TeamTool;
            itemSo.FindProperty("worldPrefab").objectReferenceValue = pickupPrefab;
            var gameplayProp = itemSo.FindProperty("teamToolGameplayPrefab");
            if (gameplayProp != null) gameplayProp.objectReferenceValue = pickupPrefab;
            itemSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(itemDef);
        }

        public static void ConfigurePlankPickupPrefab(string prefabPath, InventoryItemDefinition itemDef)
        {
            if (!File.Exists(prefabPath)) return;

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                // Ensure BoxCollider
                BoxCollider col = root.GetComponent<BoxCollider>();
                if (col == null)
                {
                    col = root.AddComponent<BoxCollider>();
                    col.size = new Vector3(0.25f, 0.03f, 0.05f);
                    col.center = Vector3.zero;
                }
                else
                {
                    Vector3 currentSize = col.size;
                    if (currentSize.y < 0.015f)
                    {
                        col.size = new Vector3(currentSize.x, 0.025f, currentSize.z);
                    }
                }

                if (!col.enabled) { col.enabled = true; }
                if (col.isTrigger) { col.isTrigger = false; }

                // Ensure AudioSource
                AudioSource audio = root.GetComponent<AudioSource>();
                if (audio == null)
                {
                    audio = root.AddComponent<AudioSource>();
                }
                if (audio.playOnAwake) { audio.playOnAwake = false; }
                if (Mathf.Abs(audio.spatialBlend - 1f) > 0.01f) { audio.spatialBlend = 1f; }

                // Ensure NetworkObject
                NetworkObject netObj = root.GetComponent<NetworkObject>();
                if (netObj == null)
                {
                    netObj = root.AddComponent<NetworkObject>();
                }

                // Ensure NetworkToolPickup
                NetworkToolPickup pickup = root.GetComponent<NetworkToolPickup>();
                if (pickup == null)
                {
                    pickup = root.AddComponent<NetworkToolPickup>();
                }

                SerializedObject so = new SerializedObject(pickup);
                var defProp = so.FindProperty("_toolItemDefinition");
                var idProp = so.FindProperty("_toolId");
                var promptProp = so.FindProperty("_pickupPrompt");
                var colProp = so.FindProperty("_pickupCollider");
                var rendProp = so.FindProperty("_visualRenderer");
                var distProp = so.FindProperty("_interactionDistance");

                if (defProp != null && defProp.objectReferenceValue != itemDef) { defProp.objectReferenceValue = itemDef; }
                if (idProp != null && idProp.intValue != 4) { idProp.intValue = 4; }
                if (promptProp != null && promptProp.stringValue != "Nh\u1EB7t Planks [E]") { promptProp.stringValue = "Nh\u1EB7t Planks [E]"; }
                if (colProp != null && colProp.objectReferenceValue != col) { colProp.objectReferenceValue = col; }
                Renderer rend = root.GetComponentInChildren<Renderer>();
                if (rendProp != null && rendProp.objectReferenceValue != rend) { rendProp.objectReferenceValue = rend; }
                if (distProp != null && Mathf.Abs(distProp.floatValue - 3.0f) > 0.01f) { distProp.floatValue = 3.0f; }

                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[DoorJammerAndPlankSetup] Configured plank pickup on: {prefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void SetupStandalonePickupPrefab(InventoryItemDefinition itemDef)
        {
            string pickupDir = Path.GetDirectoryName(PlankPickupPath);
            if (!Directory.Exists(pickupDir))
            {
                Directory.CreateDirectory(pickupDir);
            }

            GameObject sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(PlankFbxPath);
            Material plankMat = AssetDatabase.LoadAssetAtPath<Material>(PlankMaterialPath);

            GameObject pickupRoot = new GameObject("PF_Plank_Pickup");
            try
            {
                pickupRoot.layer = 0;

                // BoxCollider for pickup interaction
                var boxCol = pickupRoot.AddComponent<BoxCollider>();
                boxCol.size = new Vector3(0.5f, 0.4f, 1.2f);
                boxCol.center = new Vector3(0f, 0.2f, 0f);
                boxCol.isTrigger = false;

                // AudioSource
                var audioSrc = pickupRoot.AddComponent<AudioSource>();
                audioSrc.playOnAwake = false;
                audioSrc.spatialBlend = 1f;

                // Visual child
                GameObject visual = sourceModel != null
                    ? Object.Instantiate(sourceModel, pickupRoot.transform)
                    : new GameObject("Visual");
                visual.name = "Visual";
                visual.transform.localPosition = new Vector3(0f, 0.15f, 0f);
                visual.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                visual.transform.localScale = new Vector3(6f, 6f, 6f);

                foreach (var c in visual.GetComponentsInChildren<Collider>(true))
                {
                    Object.DestroyImmediate(c);
                }

                Renderer visualRenderer = null;
                if (plankMat != null)
                {
                    foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
                    {
                        r.sharedMaterial = plankMat;
                        if (visualRenderer == null) visualRenderer = r;
                    }
                }

                // Fusion NetworkObject
                var networkObject = pickupRoot.AddComponent<NetworkObject>();

                // NetworkToolPickup
                var toolPickup = pickupRoot.AddComponent<NetworkToolPickup>();
                SerializedObject toolSo = new SerializedObject(toolPickup);
                toolSo.FindProperty("_toolItemDefinition").objectReferenceValue = itemDef;
                toolSo.FindProperty("_toolId").intValue = 4;
                toolSo.FindProperty("_pickupPrompt").stringValue = "Nh\u1EB7t Planks [E]";
                toolSo.FindProperty("_pickupCollider").objectReferenceValue = boxCol;
                toolSo.FindProperty("_visualRenderer").objectReferenceValue = visualRenderer;
                toolSo.FindProperty("_interactionDistance").floatValue = 3f;
                toolSo.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(pickupRoot, PlankPickupPath);
                Debug.Log($"[DoorJammerAndPlankSetup] Saved {PlankPickupPath}");
            }
            finally
            {
                Object.DestroyImmediate(pickupRoot);
            }
        }

        public static void UpgradePlayerPrefabs()
        {
            GameObject plankPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlankPickupPath)
                ?? AssetDatabase.LoadAssetAtPath<GameObject>(PlankImportedPrefabPath);

            string[] playerPrefabPaths = new[]
            {
                "Assets/Prefabs/PlayerNetwork.prefab",
                "Assets/Prefabs/TestNetworkPlayer.prefab",
                "Assets/Prefabs/Player.prefab",
                "Assets/Prefabs/Player/Variants/PF_PlayerCharacter_P1_Default.prefab",
                "Assets/Prefabs/Player/Variants/PF_PlayerCharacter_P2_Orange.prefab",
                "Assets/Prefabs/Player/Variants/PF_PlayerCharacter_P3_Green.prefab",
                "Assets/Prefabs/Player/Variants/PF_PlayerCharacter_P4_Purple.prefab",
            };

            foreach (var prefabPath in playerPrefabPaths)
            {
                if (!File.Exists(prefabPath)) continue;

                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    bool changed = false;

                    // 1. Assign toolVisual_4 and plank held pose on NetworkTeamToolHeldView
                    NetworkTeamToolHeldView toolView = root.GetComponentInChildren<NetworkTeamToolHeldView>(true);
                    if (toolView != null)
                    {
                        SerializedObject toolSo = new SerializedObject(toolView);
                        var visualProp = toolSo.FindProperty("toolVisual_4");
                        if (visualProp != null && visualProp.objectReferenceValue != plankPrefab)
                        {
                            visualProp.objectReferenceValue = plankPrefab;
                            changed = true;
                        }

                        var posProp = toolSo.FindProperty("plankLocalPosition");
                        var rotProp = toolSo.FindProperty("plankLocalEulerAngles");
                        var scaleProp = toolSo.FindProperty("plankLocalScale");
                        var childPosProp = toolSo.FindProperty("plankChildLocalPosition");
                        var childRotProp = toolSo.FindProperty("plankChildLocalEulerAngles");
                        var childScaleProp = toolSo.FindProperty("plankChildLocalScale");

                        if (posProp != null) posProp.vector3Value = Vector3.zero;
                        if (rotProp != null) rotProp.vector3Value = Vector3.zero;
                        if (scaleProp != null) scaleProp.vector3Value = Vector3.one;
                        if (childPosProp != null) childPosProp.vector3Value = new Vector3(0.0151f, 0.0386f, -0.0076f);
                        if (childRotProp != null) childRotProp.vector3Value = new Vector3(90f, 0f, 0f);
                        if (childScaleProp != null) childScaleProp.vector3Value = new Vector3(22.23983f, 0.5456054f, 2.985957f);

                        toolSo.ApplyModifiedPropertiesWithoutUndo();
                        changed = true;
                    }

                    // 2. Assign plank held pose on PlayerHeldItemView
                    PlayerHeldItemView heldView = root.GetComponentInChildren<PlayerHeldItemView>(true);
                    if (heldView != null)
                    {
                        SerializedObject heldSo = new SerializedObject(heldView);
                        var posProp = heldSo.FindProperty("plankLocalPosition");
                        var rotProp = heldSo.FindProperty("plankLocalEulerAngles");
                        var scaleProp = heldSo.FindProperty("plankLocalScale");
                        var childPosProp = heldSo.FindProperty("plankChildLocalPosition");
                        var childRotProp = heldSo.FindProperty("plankChildLocalEulerAngles");
                        var childScaleProp = heldSo.FindProperty("plankChildLocalScale");

                        if (posProp != null) posProp.vector3Value = Vector3.zero;
                        if (rotProp != null) rotProp.vector3Value = Vector3.zero;
                        if (scaleProp != null) scaleProp.vector3Value = Vector3.one;
                        if (childPosProp != null) childPosProp.vector3Value = new Vector3(0.0151f, 0.0386f, -0.0076f);
                        if (childRotProp != null) childRotProp.vector3Value = new Vector3(90f, 0f, 0f);
                        if (childScaleProp != null) childScaleProp.vector3Value = new Vector3(22.23983f, 0.5456054f, 2.985957f);

                        heldSo.ApplyModifiedPropertiesWithoutUndo();
                        changed = true;
                    }

                    // 3. Update tool definition 4 in LobbyPlayerState
                    LobbyPlayerState lobbyState = root.GetComponent<LobbyPlayerState>();
                    if (lobbyState != null)
                    {
                        SerializedObject lobbySo = new SerializedObject(lobbyState);
                        SerializedProperty toolsProp = lobbySo.FindProperty("_toolDefinitions");
                        if (toolsProp != null)
                        {
                            bool found = false;
                            for (int i = 0; i < toolsProp.arraySize; i++)
                            {
                                var elem = toolsProp.GetArrayElementAtIndex(i);
                                if (elem.FindPropertyRelative("_id").intValue == 4)
                                {
                                    elem.FindPropertyRelative("_displayName").stringValue = "Wooden Planks";
                                    elem.FindPropertyRelative("_isUnique").boolValue = false;
                                    found = true;
                                    break;
                                }
                            }

                            if (!found)
                            {
                                int newIdx = toolsProp.arraySize;
                                toolsProp.InsertArrayElementAtIndex(newIdx);
                                var elem = toolsProp.GetArrayElementAtIndex(newIdx);
                                elem.FindPropertyRelative("_id").intValue = 4;
                                elem.FindPropertyRelative("_displayName").stringValue = "Wooden Planks";
                                elem.FindPropertyRelative("_isUnique").boolValue = false;
                            }

                            lobbySo.ApplyModifiedPropertiesWithoutUndo();
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                        Debug.Log($"[DoorJammerAndPlankSetup] Updated player prefab: {prefabPath}");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }
    }
}