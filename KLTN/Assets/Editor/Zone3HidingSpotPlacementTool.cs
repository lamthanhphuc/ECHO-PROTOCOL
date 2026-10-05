using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class Zone3HidingSpotPlacementTool : EditorWindow
{
    private const string ScenePath = "Assets/Scenes/SciFi.unity";
    private const string Zone3RootName = "Zone03_SecurityContainment";
    private const string CleanPrefabPath = "Assets/Prefabs/Gameplay/Imported/PF_LockerHidingSpot_Clean.prefab";
    private const string RustyPrefabPath = "Assets/Prefabs/Gameplay/Imported/PF_LockerHidingSpot_Rusty.prefab";

    private enum LockerType { Clean, Rusty }

    [SerializeField] private LockerType lockerType = LockerType.Clean;
    [SerializeField] private float yaw;
    [SerializeField] private float rotationStep = 15f;
    [SerializeField] private float surfaceOffset = 0.01f;
    [SerializeField] private bool placeEnabled = true;

    private GameObject _preview;
    private GameObject _loadedPrefab;
    private bool _hasValidPlacement;
    private Vector3 _previewPosition;
    private Quaternion _previewRotation;
    private Transform _previewParent;

    [MenuItem("Tools/ECHO Protocol/Zone 3/Hiding Spot Placer")]
    private static void Open() => GetWindow<Zone3HidingSpotPlacementTool>("Zone 3 Hiding Spots");

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        ReloadPrefab();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        DestroyPreview();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("ZONE 3 HIDING SPOT PLACER", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        lockerType = (LockerType)EditorGUILayout.EnumPopup("Locker", lockerType);
        rotationStep = EditorGUILayout.FloatField("Rotation Step", rotationStep);
        surfaceOffset = EditorGUILayout.FloatField("Floor Offset", surfaceOffset);
        yaw = EditorGUILayout.FloatField("Yaw", yaw);
        placeEnabled = EditorGUILayout.Toggle("Placement Enabled", placeEnabled);
        if (EditorGUI.EndChangeCheck())
        {
            ReloadPrefab();
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Open SciFi Scene")) OpenSciFiScene();
        if (GUILayout.Button("Clear Zone 3 Hiding Spots")) ClearPlacedHidingSpots();
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Mouse: move preview\nLeft Click: place\nQ / E: rotate\nShift + Q / E: rotate 90°\nEsc: disable placement",
            MessageType.Info);
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!placeEnabled)
        {
            DestroyPreview();
            return;
        }

        Event evt = Event.current;
        if (evt == null) return;

        if (evt.type == EventType.KeyDown)
        {
            if (evt.keyCode == KeyCode.Q)
            {
                yaw -= evt.shift ? 90f : rotationStep;
                evt.Use();
                SceneView.RepaintAll();
            }
            else if (evt.keyCode == KeyCode.E)
            {
                yaw += evt.shift ? 90f : rotationStep;
                evt.Use();
                SceneView.RepaintAll();
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                placeEnabled = false;
                DestroyPreview();
                Repaint();
                evt.Use();
                return;
            }
        }

        UpdatePlacementPreview(evt);
        if (!_hasValidPlacement) return;

        Handles.color = new Color(0.2f, 1f, 0.35f, 1f);
        Handles.DrawWireDisc(_previewPosition, Vector3.up, 0.5f);
        if (evt.type == EventType.MouseDown && evt.button == 0 && !evt.alt)
        {
            PlaceHidingSpot();
            evt.Use();
        }
    }

    private void UpdatePlacementPreview(Event evt)
    {
        _hasValidPlacement = false;
        _previewParent = null;
        Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null
                || (_preview != null && hit.collider.transform.IsChildOf(_preview.transform))) continue;

            Transform zone3 = FindZone3Ancestor(hit.collider.transform);
            if (zone3 == null || Vector3.Dot(hit.normal, Vector3.up) < 0.65f) continue;

            _previewParent = ResolvePlacementParent(hit.collider.transform, zone3);
            _previewRotation = Quaternion.Euler(0f, yaw, 0f);
            _previewPosition = hit.point + Vector3.up * surfaceOffset;
            _hasValidPlacement = true;
            EnsurePreview();
            if (_preview != null)
            {
                _preview.transform.SetPositionAndRotation(_previewPosition, _previewRotation);
                SnapVisualBottomToFloor(_preview, hit.point.y + surfaceOffset);
            }
            return;
        }

        DestroyPreview();
    }

    private void PlaceHidingSpot()
    {
        if (!_hasValidPlacement || _loadedPrefab == null || _previewParent == null) return;
        GameObject instance = PrefabUtility.InstantiatePrefab(
            _loadedPrefab, _previewParent.gameObject.scene) as GameObject;
        if (instance == null) return;

        Undo.RegisterCreatedObjectUndo(instance, "Place Zone 3 Hiding Spot");
        instance.transform.SetParent(_previewParent, true);
        instance.transform.SetPositionAndRotation(_previewPosition, _previewRotation);
        SnapVisualBottomToFloor(instance, _previewPosition.y);
        instance.name = BuildUniqueName(_previewParent, lockerType);
        EditorUtility.SetDirty(instance);
        EditorSceneManager.MarkSceneDirty(instance.scene);
        Selection.activeGameObject = instance;
    }

    private static string BuildUniqueName(Transform parent, LockerType type)
    {
        string prefix = type == LockerType.Clean
            ? "HidingSpot_Zone3_Clean"
            : "HidingSpot_Zone3_Rusty";
        int index = 1;
        while (parent.Find($"{prefix}_{index:00}") != null) index++;
        return $"{prefix}_{index:00}";
    }

    private static Transform ResolvePlacementParent(Transform hit, Transform zone3Root)
    {
        Transform current = hit;
        while (current != null && current != zone3Root)
        {
            if (IsZone3Room(current.name)) return current;
            current = current.parent;
        }

        Transform rooms = zone3Root.Find("Rooms");
        return rooms != null ? rooms : zone3Root;
    }

    private static bool IsZone3Room(string objectName)
    {
        return objectName == "02_Security_Junction_EMPTY"
            || objectName == "03_Security_Terminal_ST_EMPTY"
            || objectName == "04_Containment_Hall_EMPTY"
            || objectName == "05_Service_Emergency_Bypass_Pocket_EMPTY"
            || objectName == "06_Exit_Area_E_EMPTY";
    }

    private static Transform FindZone3Ancestor(Transform current)
    {
        while (current != null)
        {
            if (current.name == Zone3RootName) return current;
            current = current.parent;
        }
        return null;
    }

    private void EnsurePreview()
    {
        if (_preview != null) return;
        if (_loadedPrefab == null) ReloadPrefab();
        if (_loadedPrefab == null) return;

        _preview = PrefabUtility.InstantiatePrefab(_loadedPrefab) as GameObject;
        if (_preview == null) return;
        _preview.name = "__Zone3_HidingSpot_Preview";
        _preview.hideFlags = HideFlags.HideAndDontSave;
        DisablePreviewGameplay(_preview);
    }

    private static void DisablePreviewGameplay(GameObject preview)
    {
        HidingSpot[] hidingSpots = preview.GetComponentsInChildren<HidingSpot>(true);
        for (int i = 0; i < hidingSpots.Length; i++) hidingSpots[i].enabled = false;
        Collider[] colliders = preview.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;
    }

    private static void SnapVisualBottomToFloor(GameObject instance, float floorY)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        instance.transform.position += Vector3.up * (floorY - bounds.min.y);
    }

    private void ReloadPrefab()
    {
        DestroyPreview();
        string path = lockerType == LockerType.Clean ? CleanPrefabPath : RustyPrefabPath;
        _loadedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (_loadedPrefab == null)
            Debug.LogError($"[Zone3HidingSpotPlacementTool] Missing prefab: {path}");
    }

    private void DestroyPreview()
    {
        if (_preview == null) return;
        DestroyImmediate(_preview);
        _preview = null;
    }

    private static void OpenSciFiScene()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.path == ScenePath) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static void ClearPlacedHidingSpots()
    {
        Transform zone3 = FindSceneTransform(Zone3RootName);
        if (zone3 == null)
        {
            Debug.LogError("[Zone3HidingSpotPlacementTool] Zone03_SecurityContainment not found.");
            return;
        }

        HidingSpot[] spots = zone3.GetComponentsInChildren<HidingSpot>(true);
        int removed = 0;
        for (int i = spots.Length - 1; i >= 0; i--)
        {
            HidingSpot spot = spots[i];
            if (spot == null || !spot.gameObject.name.StartsWith("HidingSpot_Zone3_")) continue;
            Undo.DestroyObjectImmediate(spot.gameObject);
            removed++;
        }

        EditorSceneManager.MarkSceneDirty(zone3.gameObject.scene);
        Debug.Log($"[Zone3HidingSpotPlacementTool] Removed {removed} Zone 3 hiding spots.");
    }

    private static Transform FindSceneTransform(string objectName)
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] children = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < children.Length; j++)
                if (children[j].name == objectName) return children[j];
        }
        return null;
    }
}
