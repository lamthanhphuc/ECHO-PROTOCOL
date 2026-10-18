using System.Linq;
using EchoProtocol.MatchFlow;
using EchoProtocol.Networking;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class FloodedMapSceneTests
{
    [Test]
    public void AuthoredMissionHasUsableBindingsAndClearFlightSegments()
    {
        Validate();
    }

    [Test]
    public void ClientPresentationBlendIsFrameRateIndependent()
    {
        float at30 = 0f, at120 = 0f;
        for (int i = 0; i < 30; i++) at30 = Mathf.Lerp(at30, 1f, FloodedMapMission.PresentationBlend(1f / 30f));
        for (int i = 0; i < 120; i++) at120 = Mathf.Lerp(at120, 1f, FloodedMapMission.PresentationBlend(1f / 120f));
        Assert.That(at30, Is.EqualTo(at120).Within(.00001f));
        Assert.That(FloodedMapMission.PresentationBlend(0f), Is.Zero);
    }

    public static string Validate()
    {
        var original = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Scene_A.unity");
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/Scene_A.unity", OpenSceneMode.Additive);
        try
        {
            var roots = scene.GetRootGameObjects();
            var mission = roots.SelectMany(g => g.GetComponentsInChildren<FloodedMapMission>(true)).Single();
            Assert.That(mission.Escort, Is.Not.Null);
            Assert.That(mission.ExitTarget, Is.Not.Null);
            Assert.That(Vector3.Distance(mission.ExitTarget.transform.localPosition,
                new Vector3(19.6f, .837554f, 86.5f)), Is.LessThan(.01f));
            Assert.That(mission.ExitTarget.GetComponentsInChildren<Collider>().Length, Is.GreaterThan(0));
            var dock = mission.Destination.GetComponent<BoxCollider>();
            Assert.That(dock.size, Is.EqualTo(new Vector3(7.4f, .1f, 10.9f)));
            Assert.That(dock.center, Is.EqualTo(new Vector3(0f, .05f, 0f)));
            Assert.That(mission.IsEscortInDock(mission.Route.Last().position), Is.True);
            Assert.That(mission.ChargeTarget, Is.Not.Null);
            Assert.That(mission.Destination.gameObject.activeInHierarchy, Is.True);
            Assert.That(mission.Route.Length, Is.GreaterThan(2));
            Assert.That(mission.Route.All(t => t && t.gameObject.scene == scene), Is.True);
            Assert.That(mission.Route.Max(t => t.position.y), Is.LessThanOrEqualTo(mission.Escort.position.y + 3.05f),
                "Escort should only rise slightly over barriers, never return to the old high flight route.");
            Assert.That(mission.Route.Min(t => t.position.y), Is.GreaterThanOrEqualTo(mission.Escort.position.y - .01f));
            Assert.That(mission.ChargeTarget.GetComponent<IInteractable>(), Is.TypeOf<FloodedChargeInteraction>());
            var director = roots.SelectMany(g => g.GetComponentsInChildren<Zone2MissionDirector>(true)).Single();
            Assert.That(director.RelayA1 && director.RelayA2 && director.RelayB1 && director.RelayB2, Is.True);
            Assert.That(director.SecurityTerminal, Is.Not.Null);
            Assert.That(director.DistributionPanel1, Is.Not.Null);
            Assert.That(director.DistributionPanel1.GetComponentInParent<PowerPuzzleStation>(), Is.Not.Null);
            var boxes = roots.SelectMany(g => g.GetComponentsInChildren<EnergyCoreObjectiveProgress>(true)).ToArray();
            Assert.That(boxes.Length, Is.EqualTo(2));
            Physics.SyncTransforms();
            var points = new[] { mission.Escort.position }.Concat(mission.Route.Select(t => t.position)).ToArray();
            for (int i = 1; i < points.Length; i++)
            {
                int samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(points[i - 1], points[i]) / .5f));
                for (int sample = 0; sample <= samples; sample++)
                {
                    var p = Vector3.Lerp(points[i - 1], points[i], sample / (float)samples);
                    foreach (var hit in Physics.OverlapBox(p, new Vector3(3.5f, 1.2f, 3.5f), Quaternion.identity,
                                 Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        if (hit.gameObject.scene == scene && !(hit is TerrainCollider)
                            && !hit.transform.IsChildOf(mission.Escort) && hit.excludeLayers.value != -1)
                            Assert.Fail($"Escort route segment {i} intersects {hit.name} at {p}");
                }
            }
            return "PASS: mission bindings, four relays, keypad, charge interaction and swept flight clearance.";
        }
        finally
        {
            if (original.isLoaded) SceneManager.SetActiveScene(original);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
