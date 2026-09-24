using System;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

public static class IgnoreCentralJunctionRouteCeilings
{
    private const string RoutePath =
        "GameObject/Zone01_ResearchStorage/03_Central_Junction/route";

    [MenuItem("Tools/ECHO/NavMesh/Ignore Central Junction Route Ceilings")]
    private static void Apply()
    {
        var route = GameObject.Find(RoutePath);

        if (route == null)
        {
            Debug.LogError(
                $"[NavMesh] Cannot find route: {RoutePath}");
            return;
        }

        var transforms =
            route.GetComponentsInChildren<Transform>(true);

        var changed = 0;

        foreach (var target in transforms)
        {
            if (target == route.transform)
            {
                continue;
            }

            if (target.name.IndexOf(
                    "Ceiling",
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            // Chỉ xử lý object thực sự có geometry.
            if (target.GetComponent<MeshFilter>() == null &&
                target.GetComponent<Renderer>() == null)
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
                    "Ignore Route Ceiling From NavMesh");
            }

            modifier.ignoreFromBuild = true;
            modifier.applyToChildren = false;

            EditorUtility.SetDirty(modifier);
            changed++;
        }

        Debug.Log(
            $"[NavMesh] Central Junction route ceilings ignored: {changed}");
    }
}
