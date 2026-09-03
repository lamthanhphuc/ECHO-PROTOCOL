using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ResearchFacilityMissionPlacement
{
    private const string RunMarker = "Assets/Editor/.run_place_mission_objects";
    private const string ScenePath = "Assets/Scenes/SciFi.unity";

    private const string EnergyCorePrefabPath = "Assets/Prefabs/Gameplay/Imported/PF_EnergyCore_Imported.prefab";
    private const string SectorBoxPrefabPath = "Assets/Prefabs/Gameplay/Imported/PF_SectorBox_Imported.prefab";
    private const string PowerControlPrefabPath = "Assets/Prefabs/Gameplay/Imported/PF_PowerControl_Imported.prefab";
    private const string DistributionPanelPrefabPath = "Assets/Prefabs/Gameplay/Imported/PF_DistributionPanel_Imported.prefab";
    private const string SecurityTerminalPrefabPath = "Assets/Prefabs/Gameplay/Imported/PF_SecurityTerminal_Imported.prefab";
    private const string EscapeDoorPrefabPath = "Assets/Prefabs/Gameplay/System/PF_EscapeDoor_Countdown.prefab";

    [InitializeOnLoadMethod]
    private static void CheckRunMarker()
    {
        EditorApplication.delayCall += () =>
        {
            string fullMarkerPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", RunMarker));
            if (File.Exists(fullMarkerPath))
            {
                try
                {
                    File.Delete(fullMarkerPath);
                }
                catch
                {
                    // Ignore delete errors
                }

                PlaceMissionObjectsInRooms();
            }
        };
    }

    [MenuItem("Tools/ECHO Protocol/Place Mission Objects In Rooms")]
    public static void PlaceMissionObjectsInRooms()
    {
        // 1. Ensure SciFi scene is open
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.path.EndsWith("SciFi.unity"))
        {
            scene = EditorSceneManager.OpenScene(ScenePath);
        }

        // 2. Load Prefabs
        GameObject corePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnergyCorePrefabPath);
        GameObject sectorBoxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SectorBoxPrefabPath);
        GameObject powerControlPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PowerControlPrefabPath);
        GameObject distPanelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DistributionPanelPrefabPath);
        GameObject secTerminalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SecurityTerminalPrefabPath);
        GameObject escapeDoorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EscapeDoorPrefabPath);

        if (corePrefab == null || sectorBoxPrefab == null || powerControlPrefab == null ||
            distPanelPrefab == null || secTerminalPrefab == null || escapeDoorPrefab == null)
        {
            Debug.LogError("[ResearchFacilityMissionPlacement] One or more prefabs could not be loaded. Please ensure ImportedGameplayPrefabBuilder has run.");
            return;
        }

        // 3. Clean up loose root or misplaced instances
        CleanupMisplacedInstances();

        // 4. Zone 1: 3 Energy Core pickups & 1 Sector Box
        GameObject roomC1 = FindRoom("02_Initial_Storage_C1_EMPTY");
        GameObject roomC2 = FindRoom("04_Server_Room_C2_EMPTY");
        GameObject roomC3 = FindRoom("05_Research_Lab_C3_EMPTY");
        GameObject roomJunction = FindRoom("03_Central_Junction");

        PlaceOrUpdateChild(roomC1, corePrefab, "EnergyCore_C1_Storage", new Vector3(0f, 0.5f, 0f));
        PlaceOrUpdateChild(roomC2, corePrefab, "EnergyCore_C2_Server", new Vector3(0f, 0.5f, 0f));
        PlaceOrUpdateChild(roomC3, corePrefab, "EnergyCore_C3_ResearchLab", new Vector3(0f, 0.5f, 0f));
        GameObject sectorBoxGo = PlaceOrUpdateChild(roomJunction, sectorBoxPrefab, "SectorBox_CentralJunction", Vector3.zero);

        // 5. Zone 2: Power Control (Room A) & Distribution Panel (Room B)
        GameObject roomPC = FindRoom("04_Power_Control_PC_EMPTY");
        GameObject roomDP = FindRoom("05_Distribution_Panel_DP_EMPTY");

        PlaceOrUpdateChild(roomPC, powerControlPrefab, "PowerControl_Station", Vector3.zero);
        PlaceOrUpdateChild(roomDP, distPanelPrefab, "DistributionPanel_Station", Vector3.zero);

        // 6. Zone 3: Security Terminal & Escape Door
        GameObject roomSec = FindRoom("02_Security_Junction_EMPTY");
        GameObject roomExit = FindRoom("06_Exit_Area_E_EMPTY");

        GameObject secTerminalGo = PlaceOrUpdateChild(roomSec, secTerminalPrefab, "SecurityTerminal_Hold", Vector3.zero);
        if (secTerminalGo != null)
        {
            SecurityTerminalDownload secComp = secTerminalGo.GetComponent<SecurityTerminalDownload>();
            if (secComp != null)
            {
                SetSerializedFloat(secComp, "downloadDurationSeconds", 15f);
                SetSerializedBool(secComp, "requireHoldToDownload", true);
            }
        }

        GameObject escapeDoorGo = PlaceOrUpdateChild(roomExit, escapeDoorPrefab, "EscapeDoor_Zone3Exit", Vector3.zero);
        if (escapeDoorGo != null)
        {
            EscapeDoorCountdown doorComp = escapeDoorGo.GetComponent<EscapeDoorCountdown>();
            if (doorComp != null)
            {
                SetSerializedFloat(doorComp, "countdownSeconds", 45f);
            }
        }

        // 7. Verify internal SectorBox component connections
        if (sectorBoxGo != null)
        {
            EnergyCoreObjectiveProgress coreProgressComp = sectorBoxGo.GetComponent<EnergyCoreObjectiveProgress>();
            PowerPuzzleController powerPuzzleComp = sectorBoxGo.GetComponent<PowerPuzzleController>();
            SectorBox sectorBoxComp = sectorBoxGo.GetComponent<SectorBox>();

            if (sectorBoxComp != null && coreProgressComp != null)
            {
                SetReference(sectorBoxComp, "objectiveProgress", coreProgressComp);
            }

            if (powerPuzzleComp != null && coreProgressComp != null)
            {
                SetReference(powerPuzzleComp, "coreProgress", coreProgressComp);
            }
        }

        // 8. Bind all GameMode references (MatchFlowController & GameplayDebugHUD)
        ResearchFacilityGameModeSceneSetup.SetupGameMode();

        // 9. Save scene
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[ResearchFacilityMissionPlacement] Successfully placed and configured all mission objects inside room children!");
    }

    private static GameObject PlaceOrUpdateChild(GameObject parentRoom, GameObject prefab, string targetName, Vector3 localOffset)
    {
        if (parentRoom == null)
        {
            Debug.LogError($"[ResearchFacilityMissionPlacement] Parent room not found for target {targetName}!");
            return null;
        }

        Transform existing = parentRoom.transform.Find(targetName);
        if (existing != null)
        {
            existing.localPosition = localOffset;
            Debug.Log($"[ResearchFacilityMissionPlacement] Existing child '{targetName}' found under '{parentRoom.name}'. Updated position.");
            return existing.gameObject;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance != null)
        {
            instance.name = targetName;
            instance.transform.SetParent(parentRoom.transform, false);
            instance.transform.localPosition = localOffset;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            Debug.Log($"[ResearchFacilityMissionPlacement] Instantiated '{targetName}' under parent '{parentRoom.name}' at local offset {localOffset}.");
            return instance;
        }

        return null;
    }

    private static GameObject FindRoom(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go != null)
        {
            return go;
        }

        foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (obj.name == name && obj.scene.isLoaded)
            {
                return obj;
            }
        }

        return null;
    }

    private static void CleanupMisplacedInstances()
    {
        // Remove root loose PF_SectorBox_Imported if not under 03_Central_Junction
        GameObject rootSectorBox = GameObject.Find("PF_SectorBox_Imported");
        if (rootSectorBox != null && (rootSectorBox.transform.parent == null || rootSectorBox.transform.parent.name != "03_Central_Junction"))
        {
            Object.DestroyImmediate(rootSectorBox);
        }

        // Remove SecurityTerminal if misplaced under 02_Walls
        foreach (SecurityTerminalDownload sec in Object.FindObjectsByType<SecurityTerminalDownload>(FindObjectsInactive.Include))
        {
            if (sec.transform.parent != null && sec.transform.parent.name != "02_Security_Junction_EMPTY")
            {
                Object.DestroyImmediate(sec.gameObject);
            }
        }
    }

    private static void SetSerializedFloat(Component target, string fieldName, float value)
    {
        if (target == null) return;
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop != null)
        {
            prop.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }

    private static void SetSerializedBool(Component target, string fieldName, bool value)
    {
        if (target == null) return;
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop != null)
        {
            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }

    private static void SetReference(Object target, string fieldName, Object value)
    {
        if (target == null) return;
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop != null && prop.propertyType == SerializedPropertyType.ObjectReference)
        {
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}