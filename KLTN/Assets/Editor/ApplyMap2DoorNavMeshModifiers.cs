#if UNITY_EDITOR
using System.Text.RegularExpressions;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ApplyMap2DoorNavMeshModifiers
{
    private const string ScenePath = "Assets/Scenes/Scene_A.unity";
    private static readonly Regex DoorName = new Regex(@"^[A-Za-z0-9]+_Door_[A-Z]( \(\d+\))?$", RegexOptions.Compiled);

    [MenuItem("Tools/ECHO/NavMesh/Exclude Map 2 Doors From Bake")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() || scene.path != ScenePath)
        {
            Debug.LogError("[Map 2 NavMesh] Open Scene_A outside Play Mode before running this tool.");
            return;
        }

        NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (surfaces.Length != 1 || surfaces[0].gameObject.scene != scene || surfaces[0].agentTypeID != 0)
        {
            Debug.LogError("[Map 2 NavMesh] Expected one Map 2 NavMeshSurface for agent type 0. Nothing changed.");
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
                if (modifier == null)
                {
                    modifier = Undo.AddComponent<NavMeshModifier>(child.gameObject);
                    modifier.ignoreFromBuild = true;
                    modifier.applyToChildren = false;
                    changed++;
                }
                else if (!modifier.ignoreFromBuild || modifier.applyToChildren || !modifier.enabled)
                {
                    Undo.RecordObject(modifier, "Exclude Map 2 door from NavMesh bake");
                    modifier.ignoreFromBuild = true;
                    modifier.applyToChildren = false;
                    modifier.enabled = true;
                    changed++;
                }

                if (!modifier.AffectsAgentType(surfaces[0].agentTypeID))
                    Debug.LogWarning($"[Map 2 NavMesh] {child.name} modifier does not affect the Stalker agent type.", child);

                PrefabUtility.RecordPrefabInstancePropertyModifications(modifier);
            }
        }

        if (changed > 0)
            EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[Map 2 NavMesh] Found {doors} doors; changed {changed}. Save Scene_A and rebuild Map2_NavMesh. Door colliders remain enabled.");
    }
}
#endif
