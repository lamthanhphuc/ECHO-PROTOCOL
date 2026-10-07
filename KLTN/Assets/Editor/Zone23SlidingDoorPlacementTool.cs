#if UNITY_EDITOR
using System;
using EchoProtocol.Networking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class Zone23SlidingDoorPlacementTool : EditorWindow
{
    private const string ScenePath = "Assets/Scenes/SciFi.unity";
    private const string DoorPrefabPath = "Assets/Prefabs/Environment/Door/PF_SciFiSlidingDoor.prefab";
    private const string Zone2RootName = "Zone02_PowerEngineering";
    private const string Zone3RootName = "Zone03_SecurityContainment";
    private const string DoorContainerName = "Door";
    private const string DoorNamePrefix = "PF_SciFiSlidingDoor";

    private enum PlacementZone
    {
        Auto = 0,
        Zone2 = 2,
        Zone3 = 3
    }

    private PlacementZone placementZone = PlacementZone.Auto;
    private bool placementEnabled = true;
    private bool startsOpen;
    private bool startsLocked;
    private float animationDuration = 0.85f;
    private float yaw;
    private bool rotationStep15 = true;
    private float floorOffset;

    private GameObject doorPrefab;
    private GameObject previewInstance;

    private struct ZoneInfo
    {
        public int Number;
        public Transform Root;
    }

    [MenuItem("Tools/ECHO Protocol/Zone 2-3/Sliding Door Placer")]
    public static void ShowWindow()
    {
        var window = GetWindow<Zone23SlidingDoorPlacementTool>("Z2-Z3 Door Placer");
        window.minSize = new Vector2(360f, 390f);
        window.LoadPrefab();
    }

    private void OnEnable()
    {
        LoadPrefab();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        DestroyPreview();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("SciFi Sliding Door Placer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Open SciFi scene, enable placement, then click floor surfaces inside Zone 2 or Zone 3. " +
            "Mouse wheel rotates 90 degrees by default; hold Shift for 15 degrees.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Open SciFi Scene"))
                OpenScene();

            if (GUILayout.Button("Save Scene"))
                SaveScene();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Apply Door NavMesh Modifiers"))
                ApplySciFiSlidingDoorNavMeshModifiers.Apply();

            if (GUILayout.Button("Reload Prefab"))
                LoadPrefab();
        }

        EditorGUILayout.Space(8f);
        placementEnabled = EditorGUILayout.Toggle("Placement Enabled", placementEnabled);
        placementZone = (PlacementZone)EditorGUILayout.EnumPopup("Zone Filter", placementZone);
        startsOpen = EditorGUILayout.Toggle("Starts Open", startsOpen);
        startsLocked = EditorGUILayout.Toggle("Starts Locked", startsLocked);
        animationDuration = EditorGUILayout.FloatField("Animation Duration", animationDuration);
        yaw = EditorGUILayout.FloatField("Yaw", yaw);
        rotationStep15 = EditorGUILayout.Toggle("Shift Wheel = 15°", rotationStep15);
        floorOffset = EditorGUILayout.FloatField("Floor Offset", floorOffset);

        EditorGUILayout.Space(8f);
        EditorGUILayout.ObjectField("Door Prefab", doorPrefab, typeof(GameObject), false);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Clear Z2 Tool Doors"))
                ClearToolDoors(2);

            if (GUILayout.Button("Clear Z3 Tool Doors"))
                ClearToolDoors(3);
        }

        if (GUILayout.Button("Clear Z2 + Z3 Tool Doors"))
        {
            ClearToolDoors(2);
            ClearToolDoors(3);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            "Placed doors are named PF_SciFiSlidingDoor (Z2-001/Z3-001...) under each zone's Door container.",
            MessageType.None);
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!placementEnabled || doorPrefab == null)
            return;

        Event e = Event.current;
        if (e.type == EventType.ScrollWheel)
        {
            yaw += e.shift && rotationStep15 ? Math.Sign(e.delta.y) * 15f : Math.Sign(e.delta.y) * 90f;
            e.Use();
            Repaint();
        }

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f))
        {
            DestroyPreview();
            return;
        }

        if (!IsFloorSurface(hit.collider.transform) || !TryResolveZone(hit.collider.transform, out ZoneInfo zone))
        {
            DestroyPreview();
            return;
        }

        if (placementZone != PlacementZone.Auto && zone.Number != (int)placementZone)
        {
            DestroyPreview();
            return;
        }

        Vector3 position = hit.point + Vector3.up * floorOffset;
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        UpdatePreview(position, rotation, zone);

        Handles.color = zone.Number == 2 ? Color.cyan : Color.magenta;
        Handles.DrawWireDisc(position, Vector3.up, 1f);
        Handles.Label(position + Vector3.up * 1.6f, $"Z{zone.Number} Sliding Door");

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            PlaceDoor(position, rotation, zone);
            e.Use();
        }

        sceneView.Repaint();
    }

    private void LoadPrefab()
    {
        doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
        if (doorPrefab == null)
            Debug.LogError($"Sliding door prefab not found: {DoorPrefabPath}");
    }

    private static void OpenScene()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static void SaveScene()
    {
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }

    private void UpdatePreview(Vector3 position, Quaternion rotation, ZoneInfo zone)
    {
        if (previewInstance == null)
        {
            previewInstance = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab);
            previewInstance.name = "PREVIEW_" + DoorNamePrefix;
            previewInstance.hideFlags = HideFlags.HideAndDontSave;
            foreach (var collider in previewInstance.GetComponentsInChildren<Collider>())
                collider.enabled = false;
        }

        previewInstance.transform.SetPositionAndRotation(position, rotation);
        SnapBottomToFloor(previewInstance.transform, position.y);
    }

    private void PlaceDoor(Vector3 position, Quaternion rotation, ZoneInfo zone)
    {
        Transform container = FindOrCreateDoorContainer(zone.Root);
        GameObject door = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab, container);
        Undo.RegisterCreatedObjectUndo(door, "Place Sliding Door");

        door.name = BuildUniqueDoorName(container, zone.Number);
        door.transform.SetPositionAndRotation(position, rotation);
        SnapBottomToFloor(door.transform, position.y);
        ConfigureDoor(door);

        EditorUtility.SetDirty(door);
        EditorSceneManager.MarkSceneDirty(door.scene);
        Selection.activeGameObject = door;
    }

    private void ConfigureDoor(GameObject door)
    {
        var networkDoor = door.GetComponentInChildren<NetworkSlidingDoor>(true);
        if (networkDoor == null)
            return;

        var serializedDoor = new SerializedObject(networkDoor);
        serializedDoor.FindProperty("_startsOpen").boolValue = startsOpen;
        serializedDoor.FindProperty("_startsLocked").boolValue = startsLocked;
        serializedDoor.FindProperty("_startsBroken").boolValue = false;
        serializedDoor.FindProperty("_animationDuration").floatValue = animationDuration;
        serializedDoor.ApplyModifiedPropertiesWithoutUndo();
    }

    private bool TryResolveZone(Transform source, out ZoneInfo zone)
    {
        for (Transform current = source; current != null; current = current.parent)
        {
            if (current.name == Zone2RootName)
            {
                zone = new ZoneInfo { Number = 2, Root = current };
                return true;
            }

            if (current.name == Zone3RootName)
            {
                zone = new ZoneInfo { Number = 3, Root = current };
                return true;
            }
        }

        zone = default;
        return false;
    }

    private static bool IsFloorSurface(Transform source)
    {
        for (Transform current = source; current != null; current = current.parent)
        {
            if (current.name.IndexOf("Floor", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static Transform FindOrCreateDoorContainer(Transform zoneRoot)
    {
        Transform existing = FindDoorContainer(zoneRoot);
        if (existing != null)
            return existing;

        var container = new GameObject(DoorContainerName);
        Undo.RegisterCreatedObjectUndo(container, "Create Door Container");
        container.transform.SetParent(zoneRoot, false);
        return container.transform;
    }

    private static Transform FindDoorContainer(Transform zoneRoot)
    {
        for (int i = 0; i < zoneRoot.childCount; i++)
        {
            Transform child = zoneRoot.GetChild(i);
            if (child.name == DoorContainerName)
                return child;
        }

        return null;
    }

    private static string BuildUniqueDoorName(Transform container, int zoneNumber)
    {
        int index = 1;
        string name;
        do
        {
            name = $"{DoorNamePrefix} (Z{zoneNumber}-{index:000})";
            index++;
        }
        while (container.Find(name) != null);

        return name;
    }

    private static void SnapBottomToFloor(Transform root, float floorY)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 position = root.position;
        position.y += floorY - bounds.min.y;
        root.position = position;
    }

    private void ClearToolDoors(int zoneNumber)
    {
        string zoneName = zoneNumber == 2 ? Zone2RootName : Zone3RootName;
        GameObject zoneRoot = GameObject.Find(zoneName);
        if (zoneRoot == null)
        {
            Debug.LogWarning($"Zone root not found: {zoneName}");
            return;
        }

        Transform container = FindDoorContainer(zoneRoot.transform);
        if (container == null)
            return;

        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Transform child = container.GetChild(i);
            if (child.name.StartsWith($"{DoorNamePrefix} (Z{zoneNumber}-", StringComparison.Ordinal))
                Undo.DestroyObjectImmediate(child.gameObject);
        }

        EditorSceneManager.MarkSceneDirty(zoneRoot.scene);
    }

    private void DestroyPreview()
    {
        if (previewInstance == null)
            return;

        DestroyImmediate(previewInstance);
        previewInstance = null;
    }
}
#endif
