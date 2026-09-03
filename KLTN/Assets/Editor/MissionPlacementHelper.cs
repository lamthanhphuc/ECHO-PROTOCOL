using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MissionPlacementHelper
{
    private const string RunMarker = "Assets/Editor/.run_mission_placement_helper";

    [InitializeOnLoadMethod]
    private static void OnInitialize()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(RunMarker)) return;
            File.Delete(RunMarker);
            DumpSceneHierarchy();
        };
    }

    [MenuItem("Tools/ECHO Protocol/Dump Mission Hierarchy")]
    public static void DumpSceneHierarchy()
    {
        using (StreamWriter writer = new StreamWriter("Assets/Editor/scene_hierarchy_dump.txt"))
        {
            writer.WriteLine("=== SCENE ROOTS ===");
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                writer.WriteLine(root.name);
                if (root.name == "Environment" || root.name == "Objectives" || root.name.StartsWith("Zone0"))
                {
                    DumpChildren(root.transform, 1, writer);
                }
            }
        }
        Debug.Log("[MissionPlacementHelper] Hierarchy dumped to Assets/Editor/scene_hierarchy_dump.txt");
    }

    private static void DumpChildren(Transform parent, int depth, StreamWriter writer)
    {
        if (depth > 3) return;
        string indent = new string(' ', depth * 2);
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            writer.WriteLine($"{indent}- {child.name} (pos: {child.position})");
            if (child.childCount > 0 && depth < 3)
            {
                DumpChildren(child, depth + 1, writer);
            }
        }
    }
}
