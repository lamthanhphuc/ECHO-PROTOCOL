#if UNITY_EDITOR
using System.Text.RegularExpressions;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ApplyMap2DoorNavMeshModifiers
{
    private static readonly Regex DoorName = new Regex(@"^[A-Za-z0-9]+_Door_[A-Z]( \(\d+\))?$", RegexOptions.Compiled);

    [MenuItem("Tools/ECHO/NavMesh/Include Static Map 2 Doors In Bake")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid()
            || (scene.path != "Assets/Scenes/Scene_A.unity" && scene.path != "Assets/Scenes/Scene_B.unity"))
        {
            Debug.LogError("[Map 2 NavMesh] Open Scene_A or Scene_B outside Play Mode before running this tool.");
            return;
        }

        NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (surfaces.Length != 1 || surfaces[0].gameObject.scene != scene)
        {
            Debug.LogError("[Map 2 NavMesh] Expected one Map 2 NavMeshSurface. Nothing changed.");
            return;
        }

        int doors = 0, changed = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (!DoorName.IsMatch(child.name) || child.GetComponent<Collider>() == null)
                    continue;

                doors++;
                NavMeshModifier modifier = child.GetComponent<NavMeshModifier>();
                // Static door geometry must block the bake at its authored open/closed pose.
                if (modifier != null && modifier.ignoreFromBuild)
                {
                    Undo.RecordObject(modifier, "Include static Map 2 door in NavMesh bake");
                    modifier.ignoreFromBuild = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(modifier);
                    changed++;
                }
            }
        }

        if (changed > 0)
            EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[Map 2 NavMesh] Found {doors} static doors; changed {changed}. Rebuild NavMesh and the scene's RegionGraph, then save {scene.name}.");
    }
}
#endif
