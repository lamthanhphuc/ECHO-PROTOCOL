using System.IO;
using EchoProtocol.Networking;
using EchoProtocol.Tools.Scanner;
using Fusion;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EchoProtocol.EditorTools
{
    [InitializeOnLoad]
    public static class FieldScannerSetupBuilder
    {
        private const string ScannerModelPrefabPath = "Assets/Prefabs/Gameplay/Imported/PF_Scanner_Imported.prefab";
        private const string ScannerModelAltPrefabPath = "Assets/import/scanner/PF_Scanner.prefab";
        private const string FieldScannerPrefabPath = "Assets/Prefabs/Tools/PF_FieldScanner.prefab";
        private const string FieldScannerPickupPrefabPath = "Assets/Prefabs/Tools/PF_FieldScanner_Pickup.prefab";
        private const string ItemDefinitionPath = "Assets/ScriptableObjects/Inventory/SO_FieldScanner_ItemDefinition.asset";
        private const string PlayerNetworkPrefabPath = "Assets/Prefabs/PlayerNetwork.prefab";
        private const string TestPlayerPrefabPath = "Assets/_Project/Prefabs/Network/TestNetworkPlayer.prefab";
        private const string StalkerPrefabPath = "Assets/Prefabs/StalkerNetwork.prefab";

        static FieldScannerSetupBuilder()
        {
            EditorApplication.update += CheckAndRun;
        }

        private static void CheckAndRun()
        {
            string markerPath = Path.Combine(Application.dataPath, "Editor/.run_field_scanner_setup");
            if (File.Exists(markerPath))
            {
                File.Delete(markerPath);
                ExecuteSetup();
            }
        }

        [MenuItem("Tools/ECHO Protocol/Setup Field Scanner Prefab and Player", priority = 20)]
        [MenuItem("ECHO Protocol/Setup Field Scanner Prefab and Player", priority = 20)]
        public static void ExecuteSetup()
        {
            Debug.Log("[FieldScannerSetupBuilder] Starting Field Scanner setup...");

            EnsureFolder("Assets/Prefabs/Tools");
            EnsureFolder("Assets/ScriptableObjects/Inventory");

            // 1. Build PF_FieldScanner handheld visual prefab with live 3D ScreenCanvas
            GameObject scannerPrefab = BuildScannerPrefab();

            // 2. Load or create InventoryItemDefinition
            InventoryItemDefinition itemDef = AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(ItemDefinitionPath);
            if (itemDef == null)
            {
                itemDef = ScriptableObject.CreateInstance<InventoryItemDefinition>();
                AssetDatabase.CreateAsset(itemDef, ItemDefinitionPath);
            }

            // 3. Configure pickup prefab with NetworkObject + NetworkToolPickup
            GameObject pickupPrefab = BuildScannerPickupPrefab();
            ConfigureScannerPickupPrefab(ScannerModelPrefabPath, itemDef);
            ConfigureScannerPickupPrefab(ScannerModelAltPrefabPath, itemDef);

            GameObject primaryPickup = pickupPrefab ?? AssetDatabase.LoadAssetAtPath<GameObject>(ScannerModelPrefabPath);
            BuildItemDefinition(primaryPickup, scannerPrefab);

            // 4. Upgrade Player Prefabs
            UpgradePlayerPrefab(PlayerNetworkPrefabPath, scannerPrefab);
            UpgradePlayerPrefab(TestPlayerPrefabPath, scannerPrefab);
            UpgradePlayerPrefab("Assets/Prefabs/Player.prefab", scannerPrefab);
            UpgradePlayerPrefab("Assets/Prefabs/Player/Variants/PF_PlayerCharacter_P1_Default.prefab", scannerPrefab);
            UpgradePlayerPrefab("Assets/Prefabs/Player/Variants/PF_PlayerCharacter_P2_Orange.prefab", scannerPrefab);
            UpgradePlayerPrefab("Assets/Prefabs/Player/Variants/PF_PlayerCharacter_P3_Green.prefab", scannerPrefab);
            UpgradePlayerPrefab("Assets/Prefabs/Player/Variants/PF_PlayerCharacter_P4_Purple.prefab", scannerPrefab);

            // 5. Upgrade Stalker Monster Prefab
            UpgradeMonsterPrefab(StalkerPrefabPath);

            // 6. Place Pickup in SciFi Scene and Upgrade Scene Players
            PlaceScannerPickupInSciFiScene(pickupPrefab, scannerPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[FieldScannerSetupBuilder] Field Scanner setup completed successfully!");
        }

        private static GameObject BuildScannerPrefab()
        {
            GameObject baseSource = AssetDatabase.LoadAssetAtPath<GameObject>(ScannerModelPrefabPath);
            if (baseSource == null)
            {
                Debug.LogError($"[FieldScannerSetupBuilder] Base scanner prefab not found at {ScannerModelPrefabPath}");
                return null;
            }

            GameObject root = Object.Instantiate(baseSource);
            root.name = "PF_FieldScanner";

            // Remove pickup components if present on base source (held visuals shouldn't have pickup logic)
            var pickupComp = root.GetComponent<NetworkToolPickup>();
            if (pickupComp != null) Object.DestroyImmediate(pickupComp);
            var netObj = root.GetComponent<NetworkObject>();
            if (netObj != null) Object.DestroyImmediate(netObj);

            // Disable box collider on root (held visuals shouldn't block physical rays)
            var col = root.GetComponent<BoxCollider>();
            if (col != null)
            {
                col.isTrigger = true;
                col.enabled = false;
            }

            Transform visualChild = root.transform.Find("Visual");
            if (visualChild != null)
            {
                visualChild.localPosition = new Vector3(0f, 0.04f, 0f);
                visualChild.localRotation = Quaternion.identity;
                visualChild.localScale = Vector3.one;
            }

            // Ensure AudioSource
            var audioSource = root.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = root.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0.5f;
            }
            var scannerAudio = root.GetComponent<FieldScannerAudio>();
            if (scannerAudio == null)
            {
                scannerAudio = root.AddComponent<FieldScannerAudio>();
            }

            // Build live 3D ScreenCanvas on the handheld device
            BuildScreenCanvas(root, scannerAudio);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, FieldScannerPrefabPath);
            EnsureFolder("Assets/Resources");
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/PF_FieldScanner.prefab");
            Object.DestroyImmediate(root);
            Debug.Log($"[FieldScannerSetupBuilder] Saved PF_FieldScanner with 3D ScreenCanvas to {FieldScannerPrefabPath}");
            return savedPrefab;
        }

        public static void BuildScreenCanvas(GameObject root, FieldScannerAudio scannerAudio)
        {
            Transform existing = root.transform.Find("ScreenCanvas");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            GameObject canvasGo = new GameObject("ScreenCanvas");
            canvasGo.transform.SetParent(root.transform, false);
            // Position on device screen face
            canvasGo.transform.localPosition = new Vector3(0.019f, 0.032f, -0.012f);
            canvasGo.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            canvasGo.transform.localScale = new Vector3(0.0006f, 0.0006f, 0.0006f);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(100f, 80f);

            var screenView = root.GetComponent<FieldScannerScreenView>();
            if (screenView == null) screenView = root.AddComponent<FieldScannerScreenView>();

            var header = EnsureTextElement(canvasGo.transform, "Header", new Vector2(0f, 26f), new Vector2(96f, 18f), 8f, "FIELD SCANNER\n[ CORE MODE ]");
            header.color = new Color(0.15f, 0.95f, 0.85f);

            var radar = EnsureTextElement(canvasGo.transform, "Radar", new Vector2(0f, 4f), new Vector2(96f, 24f), 10f, "\n   ▲\n");
            radar.color = new Color(0f, 1f, 0.7f);

            var signal = EnsureTextElement(canvasGo.transform, "Signal", new Vector2(0f, -16f), new Vector2(96f, 14f), 7f, "SIGNAL  □ □ □ □");
            signal.color = new Color(1f, 0.9f, 0.2f);

            var status = EnsureTextElement(canvasGo.transform, "Status", new Vector2(0f, -28f), new Vector2(96f, 12f), 6.5f, "SCAN READY");
            status.color = new Color(0.3f, 1f, 0.4f);

            SerializedObject so = new SerializedObject(screenView);
            var headerProp = so.FindProperty("tmpHeader");
            if (headerProp != null) headerProp.objectReferenceValue = header;
            var radarProp = so.FindProperty("tmpRadar");
            if (radarProp != null) radarProp.objectReferenceValue = radar;
            var signalProp = so.FindProperty("tmpSignal");
            if (signalProp != null) signalProp.objectReferenceValue = signal;
            var statusProp = so.FindProperty("tmpStatus");
            if (statusProp != null) statusProp.objectReferenceValue = status;
            var audioProp = so.FindProperty("scannerAudio");
            if (audioProp != null) audioProp.objectReferenceValue = scannerAudio;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TextMeshProUGUI EnsureTextElement(Transform parent, string name, Vector2 anchoredPos, Vector2 size, float fontSize, string defaultText)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name);
            go.transform.SetParent(parent, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.text = defaultText;
            tmp.color = Color.white;
            return tmp;
        }

        private static InventoryItemDefinition BuildItemDefinition(GameObject pickupPrefab, GameObject scannerHeldPrefab)
        {
            InventoryItemDefinition itemDef = AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(ItemDefinitionPath);
            if (itemDef == null)
            {
                itemDef = ScriptableObject.CreateInstance<InventoryItemDefinition>();
                AssetDatabase.CreateAsset(itemDef, ItemDefinitionPath);
            }

            SerializedObject so = new SerializedObject(itemDef);
            so.FindProperty("itemId").stringValue = "FieldScanner";
            so.FindProperty("displayName").stringValue = "Field Scanner";
            so.FindProperty("itemType").enumValueIndex = (int)InventoryItemType.TeamTool;
            so.FindProperty("worldPrefab").objectReferenceValue = pickupPrefab;
            var gameplayProp = so.FindProperty("teamToolGameplayPrefab");
            if (gameplayProp != null) gameplayProp.objectReferenceValue = scannerHeldPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(itemDef);
            Debug.Log($"[FieldScannerSetupBuilder] Saved SO_FieldScanner_ItemDefinition at {ItemDefinitionPath}");

            string resPath = "Assets/Resources/SO_FieldScanner_ItemDefinition.asset";
            if (File.Exists(resPath))
            {
                var resItem = AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(resPath);
                if (resItem != null)
                {
                    SerializedObject resSo = new SerializedObject(resItem);
                    resSo.FindProperty("itemId").stringValue = "FieldScanner";
                    resSo.FindProperty("displayName").stringValue = "Field Scanner";
                    resSo.FindProperty("itemType").enumValueIndex = (int)InventoryItemType.TeamTool;
                    resSo.FindProperty("worldPrefab").objectReferenceValue = pickupPrefab;
                    var resGp = resSo.FindProperty("teamToolGameplayPrefab");
                    if (resGp != null) resGp.objectReferenceValue = scannerHeldPrefab;
                    resSo.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(resItem);
                }
            }

            return itemDef;
        }

        private static void UpgradePlayerPrefab(string prefabPath, GameObject scannerPrefab)
        {
            if (!File.Exists(prefabPath))
            {
                Debug.LogWarning($"[FieldScannerSetupBuilder] Player prefab not found: {prefabPath}");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                bool changed = false;

                // 1. Ensure NetworkFieldScanner component
                NetworkFieldScanner scannerComp = root.GetComponent<NetworkFieldScanner>();
                if (scannerComp == null)
                {
                    root.AddComponent<NetworkFieldScanner>();
                    changed = true;
                }

                // 2. Assign toolVisual_1 on NetworkTeamToolHeldView
                NetworkTeamToolHeldView toolView = root.GetComponentInChildren<NetworkTeamToolHeldView>(true);
                if (toolView != null)
                {
                    SerializedObject toolSo = new SerializedObject(toolView);
                    var visualProp = toolSo.FindProperty("toolVisual_1");
                    if (visualProp != null && visualProp.objectReferenceValue != scannerPrefab)
                    {
                        visualProp.objectReferenceValue = scannerPrefab;
                        toolSo.ApplyModifiedPropertiesWithoutUndo();
                        changed = true;
                    }
                }

                // 3. Update tool definition 1 in LobbyPlayerState
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
                            if (elem.FindPropertyRelative("_id").intValue == 1)
                            {
                                elem.FindPropertyRelative("_displayName").stringValue = "Field Scanner";
                                elem.FindPropertyRelative("_isUnique").boolValue = true;
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                        {
                            int newIdx = toolsProp.arraySize;
                            toolsProp.InsertArrayElementAtIndex(newIdx);
                            var elem = toolsProp.GetArrayElementAtIndex(newIdx);
                            elem.FindPropertyRelative("_id").intValue = 1;
                            elem.FindPropertyRelative("_displayName").stringValue = "Field Scanner";
                            elem.FindPropertyRelative("_isUnique").boolValue = true;
                        }

                        lobbySo.ApplyModifiedPropertiesWithoutUndo();
                        changed = true;
                    }
                }

                // 4. Update PlayerHeldItemView transform parameters
                PlayerHeldItemView heldView = root.GetComponentInChildren<PlayerHeldItemView>(true);
                if (heldView != null)
                {
                    SerializedObject heldSo = new SerializedObject(heldView);
                    var posProp = heldSo.FindProperty("fieldScannerLocalPosition");
                    var rotProp = heldSo.FindProperty("fieldScannerLocalEulerAngles");
                    var scaleProp = heldSo.FindProperty("fieldScannerLocalScale");
                    var childPosProp = heldSo.FindProperty("fieldScannerChildLocalPosition");
                    var childRotProp = heldSo.FindProperty("fieldScannerChildLocalEulerAngles");
                    var childScaleProp = heldSo.FindProperty("fieldScannerChildLocalScale");
                    var prefabProp = heldSo.FindProperty("fieldScannerHeldPrefab");

                    if (posProp != null) posProp.vector3Value = new Vector3(-0.003f, 0.291f, 0.118f);
                    if (rotProp != null) rotProp.vector3Value = new Vector3(184.192f, 99.672f, -5.550995f);
                    if (scaleProp != null) scaleProp.vector3Value = new Vector3(3f, 3f, 3f);
                    if (childPosProp != null) childPosProp.vector3Value = new Vector3(0f, 0.04f, 0f);
                    if (childRotProp != null) childRotProp.vector3Value = Vector3.zero;
                    if (childScaleProp != null) childScaleProp.vector3Value = Vector3.one;
                    if (prefabProp != null && scannerPrefab != null) prefabProp.objectReferenceValue = scannerPrefab;

                    heldSo.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }

                // 5. Update NetworkTeamToolHeldView transform parameters
                if (toolView != null)
                {
                    SerializedObject toolSo = new SerializedObject(toolView);
                    var posProp = toolSo.FindProperty("fieldScannerLocalPosition");
                    var rotProp = toolSo.FindProperty("fieldScannerLocalEulerAngles");
                    var scaleProp = toolSo.FindProperty("fieldScannerLocalScale");
                    var childPosProp = toolSo.FindProperty("fieldScannerChildLocalPosition");
                    var childRotProp = toolSo.FindProperty("fieldScannerChildLocalEulerAngles");
                    var childScaleProp = toolSo.FindProperty("fieldScannerChildLocalScale");

                    if (posProp != null) posProp.vector3Value = new Vector3(-0.003f, 0.291f, 0.118f);
                    if (rotProp != null) rotProp.vector3Value = new Vector3(184.192f, 99.672f, -5.550995f);
                    if (scaleProp != null) scaleProp.vector3Value = new Vector3(3f, 3f, 3f);
                    if (childPosProp != null) childPosProp.vector3Value = new Vector3(0f, 0.04f, 0f);
                    if (childRotProp != null) childRotProp.vector3Value = Vector3.zero;
                    if (childScaleProp != null) childScaleProp.vector3Value = Vector3.one;

                    toolSo.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    Debug.Log($"[FieldScannerSetupBuilder] Upgraded player prefab: {prefabPath}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void UpgradeMonsterPrefab(string prefabPath)
        {
            if (!File.Exists(prefabPath))
            {
                Debug.LogWarning($"[FieldScannerSetupBuilder] Monster prefab not found: {prefabPath}");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                MotionScannableTarget targetComp = root.GetComponent<MotionScannableTarget>();
                if (targetComp == null)
                {
                    root.AddComponent<MotionScannableTarget>();
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    Debug.Log($"[FieldScannerSetupBuilder] Added MotionScannableTarget to {prefabPath}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject BuildScannerPickupPrefab()
        {
            GameObject baseSource = AssetDatabase.LoadAssetAtPath<GameObject>(ScannerModelPrefabPath);
            if (baseSource == null)
            {
                Debug.LogError($"[FieldScannerSetupBuilder] Base scanner prefab not found at {ScannerModelPrefabPath}");
                return null;
            }

            GameObject root = Object.Instantiate(baseSource);
            root.name = "PF_FieldScanner_Pickup";
            root.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // Ensure BoxCollider is active and enabled
            var col = root.GetComponent<BoxCollider>();
            if (col == null) col = root.AddComponent<BoxCollider>();
            col.enabled = true;
            col.isTrigger = false;

            // Ensure NetworkObject is attached
            var netObj = root.GetComponent<NetworkObject>();
            if (netObj == null) netObj = root.AddComponent<NetworkObject>();

            // Ensure NetworkToolPickup is attached
            var pickup = root.GetComponent<NetworkToolPickup>();
            if (pickup == null) pickup = root.AddComponent<NetworkToolPickup>();

            InventoryItemDefinition itemDef = AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(ItemDefinitionPath);

            SerializedObject so = new SerializedObject(pickup);
            so.FindProperty("_toolItemDefinition").objectReferenceValue = itemDef;
            so.FindProperty("_toolId").intValue = 1;
            so.FindProperty("_pickupPrompt").stringValue = "Nhặt Field Scanner [E]";
            so.FindProperty("_pickupCollider").objectReferenceValue = col;
            so.FindProperty("_visualRenderer").objectReferenceValue = root.GetComponentInChildren<Renderer>();
            so.FindProperty("_interactionDistance").floatValue = 3.0f;
            so.ApplyModifiedPropertiesWithoutUndo();

            StripScannerScreenComponents(root);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, FieldScannerPickupPrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[FieldScannerSetupBuilder] Saved {FieldScannerPickupPrefabPath}");
            return savedPrefab;
        }

        public static void ConfigureScannerPickupPrefab(string prefabPath, InventoryItemDefinition itemDef)
        {
            if (!File.Exists(prefabPath)) return;

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                bool changed = false;

                // Ensure BoxCollider
                BoxCollider col = root.GetComponent<BoxCollider>();
                if (col == null)
                {
                    col = root.AddComponent<BoxCollider>();
                    changed = true;
                }
                if (!col.enabled) { col.enabled = true; changed = true; }
                if (col.isTrigger) { col.isTrigger = false; changed = true; }

                // Ensure NetworkObject
                NetworkObject netObj = root.GetComponent<NetworkObject>();
                if (netObj == null)
                {
                    netObj = root.AddComponent<NetworkObject>();
                    changed = true;
                }

                // Ensure NetworkToolPickup
                NetworkToolPickup pickup = root.GetComponent<NetworkToolPickup>();
                if (pickup == null)
                {
                    pickup = root.AddComponent<NetworkToolPickup>();
                    changed = true;
                }

                SerializedObject so = new SerializedObject(pickup);
                var defProp = so.FindProperty("_toolItemDefinition");
                var idProp = so.FindProperty("_toolId");
                var promptProp = so.FindProperty("_pickupPrompt");
                var colProp = so.FindProperty("_pickupCollider");
                var rendProp = so.FindProperty("_visualRenderer");
                var distProp = so.FindProperty("_interactionDistance");

                if (defProp != null && defProp.objectReferenceValue != itemDef) { defProp.objectReferenceValue = itemDef; changed = true; }
                if (idProp != null && idProp.intValue != 1) { idProp.intValue = 1; changed = true; }
                if (promptProp != null && promptProp.stringValue != "Nhặt Field Scanner [E]") { promptProp.stringValue = "Nhặt Field Scanner [E]"; changed = true; }
                if (colProp != null && colProp.objectReferenceValue != col) { colProp.objectReferenceValue = col; changed = true; }
                Renderer rend = root.GetComponentInChildren<Renderer>();
                if (rendProp != null && rendProp.objectReferenceValue != rend) { rendProp.objectReferenceValue = rend; changed = true; }
                if (distProp != null && Mathf.Abs(distProp.floatValue - 3.0f) > 0.01f) { distProp.floatValue = 3.0f; changed = true; }

                so.ApplyModifiedPropertiesWithoutUndo();

                if (StripScannerScreenComponents(root))
                {
                    changed = true;
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    Debug.Log($"[FieldScannerSetupBuilder] Configured pickup scripts on: {prefabPath}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static bool StripScannerScreenComponents(GameObject root)
        {
            if (root == null) return false;
            bool changed = false;

            Transform existingCanvas = root.transform.Find("ScreenCanvas");
            if (existingCanvas != null)
            {
                Object.DestroyImmediate(existingCanvas.gameObject);
                changed = true;
            }

            var screenViews = root.GetComponentsInChildren<FieldScannerScreenView>(true);
            for (int i = 0; i < screenViews.Length; i++)
            {
                if (screenViews[i] != null)
                {
                    Object.DestroyImmediate(screenViews[i]);
                    changed = true;
                }
            }

            return changed;
        }

        [MenuItem("Tools/ECHO Protocol/Clean ScreenCanvas from All Scanner Prefabs", priority = 24)]
        public static void CleanAllScannerPrefabs()
        {
            string[] prefabs = new string[]
            {
                ScannerModelPrefabPath,
                ScannerModelAltPrefabPath,
                FieldScannerPrefabPath,
                FieldScannerPickupPrefabPath,
                "Assets/Resources/PF_FieldScanner.prefab"
            };

            foreach (var path in prefabs)
            {
                if (!File.Exists(path)) continue;
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (StripScannerScreenComponents(root))
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        Debug.Log($"[FieldScannerSetupBuilder] Cleaned ScreenCanvas from {path}");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void PlaceScannerPickupInSciFiScene(GameObject pickupPrefab, GameObject scannerHeldPrefab)
        {
            string scenePath = "Assets/Scenes/SciFi.unity";
            if (!File.Exists(scenePath)) return;

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) return;

            bool modified = false;

            // 1. Position on table where PF_Scanner_Imported was
            Vector3 tablePos = new Vector3(-83.23782f, 1.06f, -23.951048f);
            Quaternion tableRot = Quaternion.identity;

            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                if (go != null && (go.name == "PF_Scanner_Imported" || go.name == "PF_FieldScanner_Pickup"))
                {
                    tablePos = go.transform.position;
                    tableRot = go.transform.rotation;
                    Object.DestroyImmediate(go);
                    modified = true;
                }
            }

            // 2. Instantiate PF_FieldScanner_Pickup
            if (pickupPrefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(pickupPrefab);
                instance.name = "PF_FieldScanner_Pickup";
                instance.transform.position = tablePos;
                instance.transform.rotation = Quaternion.Euler(90f, tableRot.eulerAngles.y, 0f);
                modified = true;
                Debug.Log("[FieldScannerSetupBuilder] Placed PF_FieldScanner_Pickup in SciFi scene at " + tablePos);
            }

            // 3. Upgrade any Player instances in scene
            foreach (var player in Object.FindObjectsByType<PlayerHeldItemView>(FindObjectsInactive.Include))
            {
                var pRoot = player.gameObject;
                var netScanner = pRoot.GetComponent<NetworkFieldScanner>();
                if (netScanner == null)
                {
                    pRoot.AddComponent<NetworkFieldScanner>();
                    modified = true;
                }

                SerializedObject heldSo = new SerializedObject(player);
                var prefabProp = heldSo.FindProperty("fieldScannerHeldPrefab");
                if (prefabProp != null && prefabProp.objectReferenceValue != scannerHeldPrefab)
                {
                    prefabProp.objectReferenceValue = scannerHeldPrefab;
                    heldSo.ApplyModifiedPropertiesWithoutUndo();
                    modified = true;
                }
            }

            if (modified)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[FieldScannerSetupBuilder] Saved SciFi scene with Field Scanner pickup and player upgrades.");
            }
        }

        [MenuItem("Tools/ECHO Protocol/Clean Field Scanner Pickups From Scene", priority = 25)]
        public static void RemovePickupsFromScene()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            bool opened = false;
            if (!activeScene.path.EndsWith("SciFi.unity"))
            {
                activeScene = EditorSceneManager.OpenScene("Assets/Scenes/SciFi.unity");
                opened = true;
            }

            string[] namesToRemove = new[]
            {
                "FieldScanner_Pickup_StartArea",
                "FieldScanner_Pickup_Storage",
                "PF_FieldScanner_Pickup",
                "PF_Scanner_Imported"
            };

            bool removedAny = false;
            foreach (var rootGo in activeScene.GetRootGameObjects())
            {
                var children = rootGo.GetComponentsInChildren<Transform>(true);
                for (int i = children.Length - 1; i >= 0; i--)
                {
                    var child = children[i];
                    if (child == null) continue;
                    if (System.Array.IndexOf(namesToRemove, child.gameObject.name) >= 0 ||
                        child.gameObject.GetComponent<NetworkToolPickup>() != null)
                    {
                        Debug.Log($"[FieldScannerSetupBuilder] Removing pickup from scene: {child.gameObject.name} in {rootGo.name}");
                        Object.DestroyImmediate(child.gameObject);
                        removedAny = true;
                    }
                }
            }

            if (removedAny || opened)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                Debug.Log("[FieldScannerSetupBuilder] Cleaned Field Scanner pickups from scene.");
            }
        }

        private static void EnsureFolder(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
        }

        [MenuItem("Tools/ECHO Protocol/Run Field Scanner EditMode Tests", priority = 30)]
        public static void RunTests()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var callbacks = new ScannerTestCallbacks(api);
            api.RegisterCallbacks(callbacks);

            var settings = new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                groupNames = new[] { "FieldScannerTests" }
            })
            {
                runSynchronously = true
            };

            Debug.Log("[FieldScannerTests] Running tests...");
            api.Execute(settings);
        }

        private sealed class ScannerTestCallbacks : ICallbacks
        {
            private readonly TestRunnerApi _api;

            public ScannerTestCallbacks(TestRunnerApi api)
            {
                _api = api;
            }

            public void RunStarted(ITestAdaptor testsToRun) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                var status = result.FailCount == 0 && result.PassCount > 0 ? "PASS" : "FAIL";
                Debug.Log(
                    $"SCANNER-TESTS|result={status}|passed={result.PassCount}|failed={result.FailCount}|" +
                    $"skipped={result.SkipCount}|inconclusive={result.InconclusiveCount}|duration={result.Duration:F3}s");

                _api.UnregisterCallbacks(this);
                Object.DestroyImmediate(_api);
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.TestStatus == TestStatus.Failed)
                {
                    Debug.LogError($"[FieldScannerTests] FAILED: {result.FullName}\n{result.Message}\n{result.StackTrace}");
                }
            }
        }
    }
}
