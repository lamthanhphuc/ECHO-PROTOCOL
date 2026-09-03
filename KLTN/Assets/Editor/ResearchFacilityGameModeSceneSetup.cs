using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ResearchFacilityGameModeSceneSetup
{
    private const string GameModeObjectName = "GameMode_ResearchFacility";
    private const string ExitDoorObjectName = "EscapeDoor_Zone3Exit";
    private const string ExitAreaMarkerName = "06_Exit_Area_E_EMPTY";
    private const string ExitDoorPrefabPath = "Assets/Prefabs/Gameplay/System/PF_EscapeDoor_Countdown.prefab";

    [MenuItem("Tools/ECHO Protocol/Setup Research Facility Game Mode")]
    public static void SetupGameMode()
    {
        GameObject gameMode = GameObject.Find(GameModeObjectName);
        if (gameMode == null)
        {
            gameMode = new GameObject(GameModeObjectName);
        }

        MatchFlowController matchFlow = GetOrAdd<MatchFlowController>(gameMode);
        GameplayDebugHUD debugHud = GetOrAdd<GameplayDebugHUD>(gameMode);

        EnergyCoreObjectiveProgress coreProgress = Object.FindAnyObjectByType<EnergyCoreObjectiveProgress>();
        PowerPuzzleController powerPuzzle = Object.FindAnyObjectByType<PowerPuzzleController>();
        SecurityTerminalDownload securityTerminal = Object.FindAnyObjectByType<SecurityTerminalDownload>();
        EscapeDoorCountdown escapeDoor = Object.FindAnyObjectByType<EscapeDoorCountdown>();
        if (escapeDoor == null)
        {
            escapeDoor = PlaceEscapeDoorIfPossible();
        }

        SetReference(matchFlow, "coreProgress", coreProgress);
        SetReference(matchFlow, "powerPuzzle", powerPuzzle);
        SetReference(matchFlow, "securityTerminal", securityTerminal);
        SetReference(matchFlow, "escapeDoor", escapeDoor);

        SetReference(debugHud, "matchFlow", matchFlow);
        SetReference(debugHud, "coreProgress", coreProgress);
        SetReference(debugHud, "powerPuzzle", powerPuzzle);
        SetReference(debugHud, "securityTerminal", securityTerminal);
        SetReference(debugHud, "escapeDoor", escapeDoor);

        foreach (PowerPuzzleStation station in Object.FindObjectsByType<PowerPuzzleStation>(FindObjectsInactive.Include))
        {
            SetReference(station, "controller", powerPuzzle);
        }

        if (escapeDoor != null)
        {
            SetReference(escapeDoor, "matchFlow", matchFlow);
        }

        EditorUtility.SetDirty(gameMode);
        EditorSceneManager.MarkSceneDirty(gameMode.scene);
        Debug.Log("[ResearchFacilityGameModeSceneSetup] GameMode_ResearchFacility is ready and references were bound.");
    }

    private static EscapeDoorCountdown PlaceEscapeDoorIfPossible()
    {
        GameObject existingDoor = GameObject.Find(ExitDoorObjectName);
        if (existingDoor != null)
        {
            return existingDoor.GetComponent<EscapeDoorCountdown>();
        }

        GameObject marker = GameObject.Find(ExitAreaMarkerName);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExitDoorPrefabPath);
        if (marker == null || prefab == null)
        {
            return null;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
        {
            return null;
        }

        instance.name = ExitDoorObjectName;
        instance.transform.SetParent(marker.transform, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        return instance.GetComponent<EscapeDoorCountdown>();
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void SetReference(Object target, string fieldName, Object value)
    {
        if (target == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
        {
            return;
        }

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
}
