using System;
using System.Linq;
using EchoProtocol.AI.AED;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class AuditAEDZoneGates
{
    public static string Run()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SciFi.unity", OpenSceneMode.Additive);
        try
        {
            var manifest = JsonUtility.FromJson<AEDV02SceneManifestData>(Resources.Load<TextAsset>("AED/Rev02SceneManifest").text).ToModel();
            return string.Join(";", AEDV02SceneSource.Zone2ExitGates(scene).Select(gate =>
            {
                var wall = gate.parent.Find("LP_Bay_Door_Wall_snaps");
                var collider = wall.GetComponents<BoxCollider>().OrderByDescending(x => x.size.z).First();
                var center = collider.transform.TransformPoint(collider.center);
                var id = AEDV02SceneSource.MarkerId(gate);
                var path = new NavMeshPath();
                var a = center - collider.transform.right * collider.size.x;
                var b = center + collider.transform.right * collider.size.x;
                return "gate=" + id + " pos=" + gate.position + " center=" + center + " size=" + collider.size +
                    " linked=" + manifest.NavLinks.Count(x => x.DoorIds.Contains(id)) + " path=" + NavMesh.CalculatePath(a, b, NavMesh.AllAreas, path) + ":" + path.status;
            }));
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }
}
