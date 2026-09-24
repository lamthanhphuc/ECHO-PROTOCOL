#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene-instance-only NavMesh bake setup for SciFi sliding doors.
/// Does NOT change the prefab asset, colliders, door scripts, or bake data.
/// </summary>
public static class ApplySciFiSlidingDoorNavMeshModifiers
{
    private const string ExpectedScene = "SciFi";
    private const string ContainerName = "Door";
    private const string DoorPrefix = "PF_SciFiSlidingDoor";
    private static readonly string[] MeshChildren = { "Door_Left", "Door_Right", "Frame" };

    [MenuItem("Tools/Stalker/Doors/Apply NavMesh Modifiers (SciFi)")]
    private static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[Door NavMesh] Stop Play Mode before applying modifiers.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.name != ExpectedScene)
        {
            Debug.LogError("[Door NavMesh] Open the SciFi scene before using this tool.");
            return;
        }

        List<Transform> containers = FindDoorContainers(scene);
        if (containers.Count != 1)
        {
            Debug.LogError($"[Door NavMesh] Expected exactly one Door container with sliding-door children; found {containers.Count}. Nothing changed.");
            return;
        }

        Transform container = containers[0];
        var doorInstances = new List<Transform>();
        for (int i = 0; i < container.childCount; i++)
        {
            Transform child = container.GetChild(i);
            if (child.name == DoorPrefix || child.name.StartsWith(DoorPrefix + " (", StringComparison.Ordinal))
                doorInstances.Add(child);
        }

        if (doorInstances.Count == 0)
        {
            Debug.LogError("[Door NavMesh] No PF_SciFiSlidingDoor scene instances found directly below Door. Nothing changed.");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Apply SciFi door NavMesh modifiers",
                $"Scene: {scene.name}\nDoor container: {GetPath(container)}\nSliding door instances: {doorInstances.Count}\n\n" +
                "Apply Remove Object to Door_Left, Door_Right and Frame of each scene instance. " +
                "This does not bake NavMesh, change prefab assets, or disable colliders. Continue?",
                "Apply to scene instances", "Cancel"))
            return;

        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Configure sliding doors for NavMesh bake");
        int added = 0, updated = 0, unchanged = 0, missing = 0, warnings = 0;
        foreach (Transform door in doorInstances)
        {
            foreach (string childName in MeshChildren)
            {
                Transform meshChild = door.Find(childName);
                if (meshChild == null)
                {
                    missing++;
                    Debug.LogWarning($"[Door NavMesh] Missing {GetPath(door)}/{childName}; skipped.");
                    continue;
                }

                NavMeshModifier modifier = meshChild.GetComponent<NavMeshModifier>();
                if (modifier == null)
                {
                    modifier = Undo.AddComponent<NavMeshModifier>(meshChild.gameObject);
                    modifier.ignoreFromBuild = true;
                    modifier.applyToChildren = false;
                    modifier.enabled = true;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(modifier);
                    added++;
                }
                else
                {
                    // Do not change other NavMeshModifier settings (notably Affected Agents).
                    bool change = !modifier.ignoreFromBuild || modifier.applyToChildren || !modifier.enabled;
                    if (change)
                    {
                        Undo.RecordObject(modifier, "Exclude door mesh from NavMesh bake");
                        modifier.ignoreFromBuild = true;
                        modifier.applyToChildren = false;
                        modifier.enabled = true;
                        EditorUtility.SetDirty(modifier);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(modifier);
                        updated++;
                    }
                    else
                    {
                        unchanged++;
                    }
                }

                // The project screenshot uses Humanoid, whose agentTypeID is commonly 0.
                // Do not silently overwrite any intentionally configured agent filter.
                if (!modifier.AffectsAgentType(0))
                {
                    warnings++;
                    Debug.LogWarning($"[Door NavMesh] {GetPath(meshChild)}: Affected Agents does not include agent type 0. Check the Modifier's Affected Agents setting in Inspector.");
                }
            }
        }

        Undo.CollapseUndoOperations(group);
        if (added > 0 || updated > 0)
            EditorSceneManager.MarkSceneDirty(scene);

        string summary = $"[Door NavMesh] Scene={scene.name}; doors={doorInstances.Count}; " +
                         $"added={added}; updated={updated}; alreadyCorrect={unchanged}; missing={missing}; agentWarnings={warnings}. " +
                         "Save the scene, then manually Bake Navigation and rerun the C4 path audit. " +
                         "RegionGraph is NOT rebaked by this tool.";
        Debug.Log(summary);
        EditorUtility.DisplayDialog("Door NavMesh setup finished", summary, "OK");
    }

    private static List<Transform> FindDoorContainers(Scene scene)
    {
        var found = new List<Transform>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name != ContainerName) continue;
                for (int i = 0; i < candidate.childCount; i++)
                {
                    string name = candidate.GetChild(i).name;
                    if (name == DoorPrefix || name.StartsWith(DoorPrefix + " (", StringComparison.Ordinal))
                    {
                        found.Add(candidate);
                        break;
                    }
                }
            }
        }
        return found;
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
#endif
