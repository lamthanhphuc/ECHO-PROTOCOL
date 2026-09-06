using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Zone1ServerRoomDecorBuilder
{
    internal const string RunMarker = "Assets/Editor/run_zone1_server_room_decor_builder.txt";
    private const string ScenePath = "Assets/Scenes/SciFi.unity";
    private const string StartRoomName = "01_Start_Area_EMPTY";
    private const string SourceDecorName = "06_Props_Decor (1)";
    private const string ServerRoomName = "04_Server_Room_C2_EMPTY";
    private const string TargetDecorName = "06_Props_Decor_ServerVariant";
    private const float SourceRoomColumns = 5f;
    private const float SourceRoomRows = 5f;
    private const float ServerRoomColumns = 4f;
    private const float ServerRoomRows = 5f;
    private const float InteriorMargin = 0.55f;

    [InitializeOnLoadMethod]
    private static void RunRequestedBuild()
    {
        EditorApplication.delayCall += () =>
        {
            string markerPath = ToAbsolutePath(RunMarker);
            if (!File.Exists(markerPath))
            {
                return;
            }

            File.Delete(markerPath);
            BuildServerRoomDecor();
        };
    }

    [MenuItem("Tools/ECHO Protocol/Build Zone 1 Server Room Decor")]
    public static void BuildServerRoomDecor()
    {
        DeleteRunMarker();

        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath);
        }

        GameObject startRoom = FindGameObject(StartRoomName);
        GameObject serverRoom = FindGameObject(ServerRoomName);
        if (startRoom == null || serverRoom == null)
        {
            Debug.LogError("[Zone1ServerRoomDecorBuilder] Missing Start or Server room in SciFi scene.");
            return;
        }

        Transform sourceDecor = FindDeepChild(startRoom.transform, SourceDecorName);
        if (sourceDecor == null)
        {
            Debug.LogError("[Zone1ServerRoomDecorBuilder] Missing source decor group: " + SourceDecorName);
            return;
        }

        Transform existing = serverRoom.transform.Find(TargetDecorName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        Bounds sourceBounds = CalculateRendererBounds(sourceDecor.gameObject);
        Bounds targetBounds = CalculateFloorBounds(serverRoom);
        if (sourceBounds.size.x <= 0.01f || sourceBounds.size.z <= 0.01f ||
            targetBounds.size.x <= 0.01f || targetBounds.size.z <= 0.01f)
        {
            Debug.LogError("[Zone1ServerRoomDecorBuilder] Could not calculate source or target room bounds.");
            return;
        }

        GameObject targetDecor = new GameObject(TargetDecorName);
        Undo.RegisterCreatedObjectUndo(targetDecor, "Build Server Room Decor");
        targetDecor.transform.SetParent(serverRoom.transform, false);
        targetDecor.transform.localPosition = Vector3.zero;
        targetDecor.transform.localRotation = Quaternion.identity;
        targetDecor.transform.localScale = Vector3.one;

        int placedCount = 0;
        List<Transform> sourceChildren = GetActiveDirectChildren(sourceDecor);
        for (int i = 0; i < sourceChildren.Count; i++)
        {
            Transform sourceChild = sourceChildren[i];
            GameObject copy = InstantiateDecorChild(sourceChild, targetDecor.transform);
            if (copy == null)
            {
                continue;
            }

            copy.name = "ServerDecor_" + SanitizeCloneName(sourceChild.name);
            ApplyServerRoomVariant(sourceChild, copy.transform, sourceBounds, targetBounds, i);
            placedCount++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = targetDecor;

        Debug.Log("[Zone1ServerRoomDecorBuilder] Decorated " + ServerRoomName + " with " + placedCount + " prop groups based on " + SourceDecorName + ".");
    }

    private static GameObject InstantiateDecorChild(Transform sourceChild, Transform parent)
    {
        GameObject sourcePrefab = PrefabUtility.GetCorrespondingObjectFromSource(sourceChild.gameObject);
        GameObject copy = sourcePrefab != null
            ? PrefabUtility.InstantiatePrefab(sourcePrefab, parent) as GameObject
            : Object.Instantiate(sourceChild.gameObject, parent);

        if (copy == null)
        {
            return null;
        }

        copy.transform.localScale = sourceChild.localScale;
        return copy;
    }

    private static void ApplyServerRoomVariant(Transform sourceChild, Transform copy, Bounds sourceBounds, Bounds targetBounds, int index)
    {
        bool wallMounted = IsWallMounted(sourceChild.name);
        Vector3 normalized = new Vector3(
            Mathf.Approximately(sourceBounds.size.x, 0f) ? 0f : (sourceChild.position.x - sourceBounds.center.x) / sourceBounds.size.x,
            0f,
            Mathf.Approximately(sourceBounds.size.z, 0f) ? 0f : (sourceChild.position.z - sourceBounds.center.z) / sourceBounds.size.z);

        normalized = FitStartLayoutToServerRoom(normalized, index, wallMounted);

        float xMargin = Mathf.Min(InteriorMargin, targetBounds.size.x * 0.08f);
        float zMargin = Mathf.Min(InteriorMargin, targetBounds.size.z * 0.08f);
        float sourceFloorY = sourceBounds.min.y;
        float targetFloorY = targetBounds.max.y;

        Vector3 targetPosition = new Vector3(
            targetBounds.center.x + normalized.x * Mathf.Max(0.01f, targetBounds.size.x - xMargin * 2f),
            targetFloorY + Mathf.Max(0f, sourceChild.position.y - sourceFloorY),
            targetBounds.center.z + normalized.z * Mathf.Max(0.01f, targetBounds.size.z - zMargin * 2f));

        copy.position = targetPosition;
        copy.rotation = wallMounted
            ? sourceChild.rotation
            : Quaternion.Euler(
                sourceChild.eulerAngles.x,
                sourceChild.eulerAngles.y + GentleRotationOffset(index),
                sourceChild.eulerAngles.z);

        float scaleJitter = wallMounted ? 1f : 1f + ScaleOffset(index);
        copy.localScale = new Vector3(
            sourceChild.localScale.x * scaleJitter,
            sourceChild.localScale.y,
            sourceChild.localScale.z * scaleJitter);
    }

    private static Vector3 FitStartLayoutToServerRoom(Vector3 normalized, int index, bool wallMounted)
    {
        float widthRatio = ServerRoomColumns / SourceRoomColumns;
        float depthRatio = ServerRoomRows / SourceRoomRows;
        normalized.x *= wallMounted ? widthRatio : widthRatio * 0.92f;
        normalized.z *= wallMounted ? depthRatio : depthRatio * 0.9f;

        if (!wallMounted)
        {
            normalized.x += HorizontalVariantOffset(index);
            normalized.z += DepthVariantOffset(index);
        }

        normalized.x = Mathf.Clamp(normalized.x, -0.46f, 0.46f);
        normalized.z = Mathf.Clamp(normalized.z, -0.46f, 0.46f);
        return normalized;
    }

    private static float HorizontalVariantOffset(int index)
    {
        switch (index % 6)
        {
            case 0: return 0.06f;
            case 1: return -0.05f;
            case 2: return 0.02f;
            case 3: return -0.02f;
            default: return 0f;
        }
    }

    private static float DepthVariantOffset(int index)
    {
        switch (index % 5)
        {
            case 0: return 0.05f;
            case 1: return -0.04f;
            case 2: return 0.02f;
            case 3: return -0.03f;
            default: return 0f;
        }
    }

    private static float GentleRotationOffset(int index)
    {
        switch (index % 8)
        {
            case 0: return 0f;
            case 1: return 8f;
            case 2: return -8f;
            case 3: return 12f;
            case 4: return -12f;
            case 5: return 5f;
            case 6: return -5f;
            default: return 0f;
        }
    }

    private static float ScaleOffset(int index)
    {
        switch (index % 4)
        {
            case 0: return -0.08f;
            case 1: return 0.06f;
            case 2: return 0.02f;
            default: return -0.03f;
        }
    }

    private static bool IsWallMounted(string sourceName)
    {
        string lower = sourceName.ToLowerInvariant();
        return lower.Contains("pipe") || lower.Contains("vent") || lower.Contains("cable") || lower.Contains("wall");
    }

    private static Bounds CalculateFloorBounds(GameObject room)
    {
        Renderer[] floorRenderers = room.GetComponentsInChildren<Renderer>(true);
        List<Renderer> selected = new List<Renderer>();
        foreach (Renderer renderer in floorRenderers)
        {
            string name = renderer.name.ToLowerInvariant();
            if (name.Contains("floor") || name.Contains("tile"))
            {
                selected.Add(renderer);
            }
        }

        return selected.Count > 0 ? CalculateBounds(selected) : CalculateRendererBounds(room);
    }

    private static Bounds CalculateRendererBounds(GameObject root)
    {
        return CalculateBounds(new List<Renderer>(root.GetComponentsInChildren<Renderer>(true)));
    }

    private static Bounds CalculateBounds(List<Renderer> renderers)
    {
        if (renderers.Count == 0)
        {
            return new Bounds(Vector3.zero, Vector3.zero);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Count; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static List<Transform> GetActiveDirectChildren(Transform parent)
    {
        List<Transform> children = new List<Transform>();
        foreach (Transform child in parent)
        {
            if (child.gameObject.activeSelf)
            {
                children.Add(child);
            }
        }

        return children;
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindDeepChild(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static GameObject FindGameObject(string objectName)
    {
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        foreach (GameObject gameObject in allObjects)
        {
            if (gameObject.scene == SceneManager.GetActiveScene() && gameObject.name == objectName)
            {
                return gameObject;
            }
        }

        return null;
    }

    private static string SanitizeCloneName(string sourceName)
    {
        return sourceName.Replace("(Clone)", string.Empty).Trim();
    }

    internal static string ToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, assetPath);
    }

    internal static bool HasRunMarker()
    {
        return File.Exists(ToAbsolutePath(RunMarker));
    }

    private static void DeleteRunMarker()
    {
        string markerPath = ToAbsolutePath(RunMarker);
        if (File.Exists(markerPath))
        {
            File.Delete(markerPath);
        }
    }

}

public sealed class Zone1ServerRoomDecorBuilderAssetPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (Zone1ServerRoomDecorBuilder.HasRunMarker())
        {
            EditorApplication.delayCall += Zone1ServerRoomDecorBuilder.BuildServerRoomDecor;
        }
    }
}
