using System.Linq;
using EchoProtocol.AI.Stalker.Spatial;
using EchoProtocol.AI.Stalker.Special;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class InspectAEDCabinets
{
    public static string Run()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SciFi.unity", OpenSceneMode.Additive);
        try
        {
            var roots = scene.GetRootGameObjects();
            var spots = roots.SelectMany(x => x.GetComponentsInChildren<global::HidingSpot>(true)).ToArray();
            var regions = roots.SelectMany(x => x.GetComponentsInChildren<RegionDefinition>(true)).ToArray();
            var graph = AssetDatabase.LoadAssetAtPath<RegionGraphAsset>("Assets/AI/Stalker/Station/AI_Stalker_FullStation_RegionGraph.asset").BuildRuntimeGraph();
            var entries = roots.SelectMany(x => x.GetComponentsInChildren<StalkerJumpEntryPoint>(true)).ToArray();
            return "spots=" + spots.Length + " entries=" + entries.Length + " regions=" + regions.Length + ";" +
                string.Join(";", spots.Take(8).Select(x => x.name + " pos=" + x.transform.position + " hide=" + x.HidePoint.position +
                    " inspect=" + (x.InspectPoint != null) + " exit=" + (x.ExitPoint != null) + " regions=" + string.Join(",", regions.Where(r => r.ContainsWorldPoint(x.HidePoint.position))
                        .Select(r => r.RegionId + ":" + (graph.TryGetRegionSemanticMetadata(r.RegionId, out var m) ? m.Zone.ToString() : "MISSING")))));
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }
}
