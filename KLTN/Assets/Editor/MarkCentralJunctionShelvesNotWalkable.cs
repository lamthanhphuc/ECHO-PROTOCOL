using System;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class MarkGameplayShelvesNotWalkable
{
    [MenuItem("Tools/ECHO/NavMesh/Mark All Gameplay Shelves Not Walkable")]
    private static void Apply()
    {
        var scene = SceneManager.GetActiveScene();

        GameObject gameplayRoot = null;

        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "GameObject")
            {
                gameplayRoot = root;
                break;
            }
        }

        if (gameplayRoot == null)
        {
            Debug.LogError(
                "[NavMesh] Cannot find gameplay root 'GameObject'.");
            return;
        }

        int area = NavMesh.GetAreaFromName("Not Walkable");

        if (area < 0)
        {
            Debug.LogError(
                "[NavMesh] Area 'Not Walkable' does not exist.");
            return;
        }

        int changed = 0;

        foreach (Transform target in
                 gameplayRoot.GetComponentsInChildren<Transform>(true))
        {
            if (!target.name.StartsWith(
                    "Shelf Variation",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var modifier =
                target.GetComponent<NavMeshModifier>();

            if (modifier == null)
            {
                modifier =
                    Undo.AddComponent<NavMeshModifier>(
                        target.gameObject);
            }
            else
            {
                Undo.RecordObject(
                    modifier,
                    "Mark Gameplay Shelf Not Walkable");
            }

            modifier.ignoreFromBuild = false;
            modifier.applyToChildren = true;
            modifier.overrideArea = true;
            modifier.area = area;

            EditorUtility.SetDirty(modifier);
            changed++;
        }

        Debug.Log(
            $"[NavMesh] Gameplay shelves marked Not Walkable: {changed}");
    }
}
