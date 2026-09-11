using System.IO;
using EchoProtocol.Networking;
using EchoProtocol.Tools.Scanner;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace EchoProtocol.Editor
{
    [InitializeOnLoad]
    public static class MotionDecoyAndCoreStabilizerSetup
    {
        public const string CoreStabilizerNetworkPickupPath = "Assets/Prefabs/Tools/PF_CoreStabilizer_NetworkPickup.prefab";
        public const string CoreStabilizerAnimatedDevicePath = "Assets/Prefabs/Environment/Teamtoools/Animated/PF_CoreStabilizer_Device_Animated.prefab";
        public const string CoreStabilizerItemPath = "Assets/ScriptableObjects/Inventory/TeamTools/SO_CoreStabilizer_TeamTool.asset";
        public const string CoreStabilizerPulseAudioPath = "Assets/Audio/energy_core/pickup.wav";

        static MotionDecoyAndCoreStabilizerSetup()
        {
            EditorApplication.update += CheckAndRun;
        }

        private static void CheckAndRun()
        {
            string markerPath = Path.Combine(Application.dataPath, "Editor/.run_team_tools_setup");
            if (File.Exists(markerPath))
            {
                File.Delete(markerPath);
                RunSetup();
            }

            string testMarkerPath = Path.Combine(Application.dataPath, "Editor/.run_team_tools_tests");
            if (File.Exists(testMarkerPath))
            {
                File.Delete(testMarkerPath);
                RunProductionTests();
            }
        }

        [MenuItem("Tools/ECHO Protocol/Setup Core Stabilizer")]
        [MenuItem("Tools/ECHO Protocol/Setup Motion Decoy and Core Stabilizer")]
        public static void RunSetup()
        {
            Debug.Log("[MotionDecoyAndCoreStabilizerSetup] Starting Core Stabilizer setup & Motion Decoy purge...");
            SetupCoreStabilizerNetworkPickup();
            UpgradePlayerPrefabs();
            PlacePickupsInSciFiScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MotionDecoyAndCoreStabilizerSetup] Setup completed successfully!");
        }

        public static void PlacePickupsInSciFiScene()
        {
            string scenePath = "Assets/Scenes/SciFi.unity";
            if (!File.Exists(scenePath)) return;

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) return;

            bool modified = false;

            // 1. Destroy all Motion Decoy objects (visuals, pickups) and old static stabilizer visuals
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go != null && (go.name.StartsWith("PF_MotionDecoy_") || go.name.StartsWith("PF_CoreStabilizer_Device_Visual")))
                {
                    Object.DestroyImmediate(go);
                    modified = true;
                }
            }

            // 2. Ensure Core Stabilizer Network Pickup is in scene
            var stabilizerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CoreStabilizerNetworkPickupPath);
            bool hasStabilizer = false;
            foreach (var pickup in Object.FindObjectsByType<NetworkToolPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (pickup != null && pickup.name.Contains("CoreStabilizer"))
                {
                    hasStabilizer = true;
                    break;
                }
            }

            if (!hasStabilizer && stabilizerPrefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(stabilizerPrefab);
                instance.transform.position = new Vector3(-87.89f, 0.05f, -18.76f);
                instance.transform.rotation = Quaternion.identity;
                modified = true;
                Debug.Log("[MotionDecoyAndCoreStabilizerSetup] Instantiated Core Stabilizer Pickup in SciFi scene");
            }

            // 4. Fix FirstPersonArms material in SciFi scene
            var suitMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PlayerCharacter/M_PF_PlayerCharacter_P1_Default_Suit.mat");
            if (suitMat != null)
            {
                foreach (var smr in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (smr != null && smr.name == "FirstPersonArms")
                    {
                        if (smr.sharedMaterial == null || smr.sharedMaterial.shader == null || smr.sharedMaterial.shader.name == "Hidden/InternalErrorShader")
                        {
                            smr.sharedMaterial = suitMat;
                            EditorUtility.SetDirty(smr);
                            modified = true;
                            Debug.Log("[MotionDecoyAndCoreStabilizerSetup] Assigned suit material to FirstPersonArms in SciFi scene");
                        }
                    }
                }
            }

            if (modified)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[MotionDecoyAndCoreStabilizerSetup] Saved modified SciFi scene.");
            }
        }

        public static void SetupCoreStabilizerNetworkPickup()
        {
            var itemDef = AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(CoreStabilizerItemPath);
            var visualModel = AssetDatabase.LoadAssetAtPath<GameObject>(CoreStabilizerAnimatedDevicePath);

            string dir = Path.GetDirectoryName(CoreStabilizerNetworkPickupPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            GameObject root = new GameObject("PF_CoreStabilizer_NetworkPickup");
            try
            {
                root.layer = 0;

                var boxCol = root.AddComponent<BoxCollider>();
                boxCol.size = new Vector3(0.35f, 0.4f, 0.35f);
                boxCol.center = new Vector3(0f, 0.2f, 0f);
                boxCol.isTrigger = false;

                var audioSrc = root.AddComponent<AudioSource>();
                audioSrc.playOnAwake = false;
                audioSrc.spatialBlend = 1f;

                GameObject visual = visualModel != null
                    ? Object.Instantiate(visualModel, root.transform)
                    : new GameObject("Visual");
                visual.name = "Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;

                foreach (var c in visual.GetComponentsInChildren<Collider>(true))
                {
                    Object.DestroyImmediate(c);
                }

                Renderer visualRenderer = visual.GetComponentInChildren<Renderer>(true);

                var netObj = root.AddComponent<NetworkObject>();
                var toolPickup = root.AddComponent<NetworkToolPickup>();

                SerializedObject so = new SerializedObject(toolPickup);
                so.FindProperty("_toolItemDefinition").objectReferenceValue = itemDef;
                so.FindProperty("_toolId").intValue = LobbyPlayerState.CoreStabilizerToolId;
                so.FindProperty("_pickupPrompt").stringValue = "Nhặt Core Stabilizer [E]";
                so.FindProperty("_pickupCollider").objectReferenceValue = boxCol;
                so.FindProperty("_visualRenderer").objectReferenceValue = visualRenderer;
                so.FindProperty("_interactionDistance").floatValue = 3f;
                so.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject netSo = new SerializedObject(netObj);
                var netBehaviours = netSo.FindProperty("NetworkedBehaviours");
                if (netBehaviours != null)
                {
                    netBehaviours.ClearArray();
                    netBehaviours.InsertArrayElementAtIndex(0);
                    netBehaviours.GetArrayElementAtIndex(0).objectReferenceValue = toolPickup;
                    netSo.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, CoreStabilizerNetworkPickupPath);
                Debug.Log($"[MotionDecoyAndCoreStabilizerSetup] Created {CoreStabilizerNetworkPickupPath}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        public static void UpgradePlayerPrefabs()
        {
            var coreStabilizerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CoreStabilizerAnimatedDevicePath);
            var coreStabilizerClip = AssetDatabase.LoadAssetAtPath<AudioClip>(CoreStabilizerPulseAudioPath);

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

                    // 1. Assign toolVisual_6 on NetworkTeamToolHeldView
                    NetworkTeamToolHeldView toolView = root.GetComponentInChildren<NetworkTeamToolHeldView>(true);
                    if (toolView != null)
                    {
                        SerializedObject toolSo = new SerializedObject(toolView);
                        var v6Prop = toolSo.FindProperty("toolVisual_6");
                        if (v6Prop != null && v6Prop.objectReferenceValue != coreStabilizerPrefab)
                        {
                            v6Prop.objectReferenceValue = coreStabilizerPrefab;
                            changed = true;
                        }

                        var pos6Prop = toolSo.FindProperty("coreStabilizerLocalPosition");
                        var rot6Prop = toolSo.FindProperty("coreStabilizerLocalEulerAngles");
                        var scale6Prop = toolSo.FindProperty("coreStabilizerLocalScale");
                        if (pos6Prop != null) pos6Prop.vector3Value = new Vector3(0.035f, 0.02f, 0.12f);
                        if (rot6Prop != null) rot6Prop.vector3Value = new Vector3(10f, 90f, -15f);
                        if (scale6Prop != null) scale6Prop.vector3Value = new Vector3(0.45f, 0.45f, 0.45f);

                        toolSo.ApplyModifiedPropertiesWithoutUndo();
                    }

                    // 2. Assign clips on NetworkPlayerInteractor
                    NetworkPlayerInteractor interactor = root.GetComponentInChildren<NetworkPlayerInteractor>(true);
                    if (interactor != null)
                    {
                        SerializedObject intSo = new SerializedObject(interactor);
                        var pulseClipProp = intSo.FindProperty("_coreStabilizerPulseClip");
                        if (pulseClipProp != null && pulseClipProp.objectReferenceValue != coreStabilizerClip)
                        {
                            pulseClipProp.objectReferenceValue = coreStabilizerClip;
                            changed = true;
                        }

                        intSo.ApplyModifiedPropertiesWithoutUndo();
                    }

                    // 3. Remove tool definition 5 and ensure tool definition 6 in LobbyPlayerState
                    LobbyPlayerState lobbyState = root.GetComponent<LobbyPlayerState>();
                    if (lobbyState != null)
                    {
                        SerializedObject lobbySo = new SerializedObject(lobbyState);
                        SerializedProperty toolsProp = lobbySo.FindProperty("_toolDefinitions");
                        if (toolsProp != null)
                        {
                            RemoveToolDefinition(toolsProp, 5, ref changed);
                            EnsureToolDefinition(toolsProp, LobbyPlayerState.CoreStabilizerToolId, "Core Stabilizer", true, ref changed);
                            lobbySo.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }

                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                        Debug.Log($"[MotionDecoyAndCoreStabilizerSetup] Updated player prefab: {prefabPath}");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void RemoveToolDefinition(SerializedProperty toolsProp, int id, ref bool changed)
        {
            for (int i = toolsProp.arraySize - 1; i >= 0; i--)
            {
                var elem = toolsProp.GetArrayElementAtIndex(i);
                if (elem.FindPropertyRelative("_id").intValue == id)
                {
                    toolsProp.DeleteArrayElementAtIndex(i);
                    changed = true;
                }
            }
        }

        private static void EnsureToolDefinition(SerializedProperty toolsProp, int id, string displayName, bool isUnique, ref bool changed)
        {
            bool found = false;
            for (int i = 0; i < toolsProp.arraySize; i++)
            {
                var elem = toolsProp.GetArrayElementAtIndex(i);
                if (elem.FindPropertyRelative("_id").intValue == id)
                {
                    elem.FindPropertyRelative("_displayName").stringValue = displayName;
                    elem.FindPropertyRelative("_isUnique").boolValue = isUnique;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                int newIdx = toolsProp.arraySize;
                toolsProp.InsertArrayElementAtIndex(newIdx);
                var elem = toolsProp.GetArrayElementAtIndex(newIdx);
                elem.FindPropertyRelative("_id").intValue = id;
                elem.FindPropertyRelative("_displayName").stringValue = displayName;
                elem.FindPropertyRelative("_isUnique").boolValue = isUnique;
                changed = true;
            }
        }

        public static void RunProductionTests()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var filter = new Filter
            {
                testMode = TestMode.EditMode,
                testNames = new[]
                {
                    "EchoProtocol.Player.Tests.TeamToolMultiplayerProductionTests"
                }
            };
            api.Execute(new ExecutionSettings(filter));
        }
    }
}

