using System;
using System.Linq;
using EchoProtocol.MatchFlow;
using EchoProtocol.AI.Stalker.Spatial;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class InspectAEDZoneEntries
{
    public static string Run()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/SciFi.unity");
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/SciFi.unity", OpenSceneMode.Additive);
        try
        {
            var roots = scene.GetRootGameObjects();
            var doors = roots.SelectMany(x => x.GetComponentsInChildren<Transform>(true)).Where(x => x.name == "DoorZone" || x.name == "LP_Bay_Door_snaps").Select(x => x.gameObject).ToArray();
            return string.Join("\n", doors.Where(x => x != null).Select(x => "EXIT " + x.name + " pos=" + x.transform.position + " bounds=" + x.GetComponentsInChildren<Collider>(true).Select(c => c.bounds.ToString()).FirstOrDefault())) +
                "\n" + string.Join("\n", roots.SelectMany(x => x.GetComponentsInChildren<RegionDefinition>(true)).Select(x => "REGION " + x.name + " pos=" + x.transform.position));
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
}
