using System;
using System.Linq;
using EchoProtocol.AI.AED;
using EchoProtocol.Networking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class AuthorAEDZoneEntries
{
    public static string Run()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/SciFi.unity");
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/SciFi.unity", OpenSceneMode.Additive);
        try
        {
            var all = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).ToArray();
            var zone1 = all.Single(x => x.name == "AED_Z1_RESOURCE_ENTRY");
            var zone2 = all.Select(x => x.GetComponent<StalkerZone2EntryTrigger>()).Single(x => x != null).transform;
            var exits = AEDV02SceneSource.Zone2ExitGates(scene);
            if (exits.Length < 2) throw new InvalidOperationException("Production Zone2 exit references are missing.");
            // Match the existing Zone2 director's second authored exit, rather than debug teleport coordinates.
            var zone3 = all.FirstOrDefault(x => x.name == "AED_Z3_RESOURCE_ENTRY");
            if (zone3 == null) { zone3 = new GameObject("AED_Z3_RESOURCE_ENTRY").transform; SceneManager.MoveGameObjectToScene(zone3.gameObject, scene); zone3.SetParent(exits[1], false); }
            if (!NavMesh.SamplePosition(exits[1].position, out var floor, exits[1].GetComponentsInChildren<Renderer>(true).Select(x => x.bounds.size.magnitude).DefaultIfEmpty(1).Max(), NavMesh.AllAreas))
                throw new InvalidOperationException("Production Zone3 exit has no baked NavMesh projection.");
            zone3.position = floor.position;
            var entries = new[] { zone1, zone2, zone3 };
            for (int i = 0; i < entries.Length; i++)
            {
                var marker = entries[i].GetComponent<AEDZoneEntryMarker>() ?? entries[i].gameObject.AddComponent<AEDZoneEntryMarker>();
                var serialized = new SerializedObject(marker); serialized.FindProperty("zone").intValue = i + 1; serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(marker);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            return string.Join(";", entries.Select((x, i) => "zone=" + (i + 1) + " position=" + x.position));
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
}
