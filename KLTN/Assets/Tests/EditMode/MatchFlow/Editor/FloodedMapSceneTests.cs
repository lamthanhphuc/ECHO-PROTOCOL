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

    [Test]
    public void TC_MF_02_02_03_EscortAcceptsOnlyTheAuthoredDock()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Scene_A.unity");
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/Scene_A.unity", OpenSceneMode.Additive);
        try
        {
            var mission = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<FloodedMapMission>(true)).Single();
            var dock = mission.Destination.GetComponent<BoxCollider>();
            Assert.That(mission.IsEscortInDock(mission.Route.Last().position), Is.True);
            Assert.That(mission.IsEscortInDock(mission.Route.First().position), Is.False,
                "The escort's departure point must not satisfy dock arrival.");
            var outside = dock.transform.TransformPoint(dock.center + Vector3.right * (dock.size.x * .5f + 1f));
            Assert.That(mission.IsEscortInDock(outside), Is.False,
                "An escort outside the authored dock must not advance the route.");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    [Test]
    public void DebugSkipsResolveActualMissionTargetsWithoutZoneEntryTriggers()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Scene_A.unity");
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/Scene_A.unity", OpenSceneMode.Additive);
        try
        {
            var roots = scene.GetRootGameObjects();
            var mission = roots.SelectMany(g => g.GetComponentsInChildren<FloodedMapMission>(true)).Single();
            var terminal = roots.SelectMany(g => g.GetComponentsInChildren<Zone2MissionDirector>(true)).Single().SecurityTerminal;
            Physics.SyncTransforms();
            foreach (var phase in new[] { NetworkMatchPhase.Zone2Objective, NetworkMatchPhase.Zone3FindFrigate })
                for (int player = 0; player < 4; player++)
                {
                    Assert.That(mission.TryGetDebugMissionPose(phase, player, out var position, out var rotation), Is.True,
                        $"No usable debug destination for {phase}, player {player}");
                    var target = phase == NetworkMatchPhase.Zone2Objective ? terminal.GetComponentInChildren<Collider>() : mission.ChargeTarget;
                    Assert.That(Vector3.Distance(target.ClosestPoint(position), position), Is.LessThan(3f));
                    Assert.That(UnityEngine.AI.NavMesh.SamplePosition(position, out var nav, .2f, UnityEngine.AI.NavMesh.AllAreas), Is.True);
                    Assert.That(Vector3.Distance(nav.position, position), Is.LessThan(.2f));
                    Assert.That(Vector3.Dot(rotation * Vector3.forward,
                        Vector3.ProjectOnPlane(target.bounds.center - position, Vector3.up).normalized), Is.GreaterThan(.99f));
                }
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    [Test]
    public void DebugApproachRejectsUnavailableTarget()
    {
        Assert.That(FloodedMapMission.TryGetDebugApproachPose(null, 0, out _, out _), Is.False);
        var go = new GameObject("Disabled debug target");
        try
        {
            var collider = go.AddComponent<BoxCollider>();
            collider.enabled = false;
            Assert.That(FloodedMapMission.TryGetDebugApproachPose(collider, 0, out _, out _), Is.False);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void SectorStationsBlockBakedPathsAndKeepWalkableApproaches()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Scene_A.unity");
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/Scene_A.unity", OpenSceneMode.Additive);
        try
        {
            var roots = scene.GetRootGameObjects();
            var surface = roots.SelectMany(g => g.GetComponentsInChildren<Unity.AI.Navigation.NavMeshSurface>(true)).Single();
            var filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = surface.agentTypeID, areaMask = UnityEngine.AI.NavMesh.AllAreas };
            Physics.SyncTransforms();
            foreach (var station in roots.SelectMany(g => g.GetComponentsInChildren<SectorBox>(true)))
            {
                var collider = station.GetComponent<Collider>();
                Assert.That(FloodedMapMission.TryGetDebugApproachPose(collider, 0, out _, out _), Is.True, station.name);
                var bounds = collider.bounds;
                foreach (var direction in new[] { Vector3.right, Vector3.forward })
                {
                    float extent = direction.x != 0 ? bounds.extents.x : bounds.extents.z;
                    var from = bounds.center - direction * (extent + 2f);
                    var to = bounds.center + direction * (extent + 2f);
                    from.y = to.y = bounds.min.y;
                    Assert.That(UnityEngine.AI.NavMesh.SamplePosition(from, out var a, 2f, filter), Is.True);
                    Assert.That(UnityEngine.AI.NavMesh.SamplePosition(to, out var b, 2f, filter), Is.True);
                    Assert.That(UnityEngine.AI.NavMesh.Raycast(a.position, b.position, out _, filter), Is.True,
                        $"Baked navigation passes through {station.name} along {direction}");
                }
            }
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
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
            var highestRoutePoint = mission.Route.OrderByDescending(t => t.position.y).First();
            Assert.That(highestRoutePoint.position.y, Is.LessThanOrEqualTo(mission.Escort.position.y + 3.05f),
                $"Escort route point {highestRoutePoint.name} at {highestRoutePoint.position} should only rise slightly over barriers.");
            var lowestRoutePoint = mission.Route.OrderBy(t => t.position.y).First();
            Assert.That(lowestRoutePoint.position.y, Is.GreaterThanOrEqualTo(mission.Escort.position.y - .01f),
                $"Escort route point {lowestRoutePoint.name} at {lowestRoutePoint.position} fell below the escort deck.");
            Assert.That(mission.ChargeTarget.GetComponent<IInteractable>(), Is.TypeOf<FloodedChargeInteraction>());
            var director = roots.SelectMany(g => g.GetComponentsInChildren<Zone2MissionDirector>(true)).Single();
            Assert.That(director.RelayA1 && director.RelayA2 && director.RelayB1 && director.RelayB2, Is.True);
            Assert.That(director.SecurityTerminal, Is.Not.Null);
            Assert.That(director.DistributionPanel1, Is.Not.Null);
            Assert.That(director.DistributionPanel1.GetComponentInParent<PowerPuzzleStation>(), Is.Not.Null);
            var boxes = roots.SelectMany(g => g.GetComponentsInChildren<EnergyCoreObjectiveProgress>(true)).ToArray();
            Assert.That(boxes.Length, Is.EqualTo(2));
            Physics.SyncTransforms();
            var points = new[] { mission.Escort }.Concat(mission.Route).ToArray();
            for (int i = 1; i < points.Length; i++)
            {
                int samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(points[i - 1].position, points[i].position) / .5f));
                for (int sample = 0; sample <= samples; sample++)
                {
                    var p = Vector3.Lerp(points[i - 1].position, points[i].position, sample / (float)samples);
                    foreach (var hit in Physics.OverlapBox(p, new Vector3(3.5f, 1.2f, 3.5f), Quaternion.identity,
                                 Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        if (hit.gameObject.scene == scene && !(hit is TerrainCollider)
                            && !hit.transform.IsChildOf(mission.Escort) && hit.excludeLayers.value != -1)
                            Assert.Fail($"Escort route segment {i} ({points[i - 1].name} {points[i - 1].position} -> {points[i].name} {points[i].position}) intersects {hit.name} bounds {hit.bounds} at {p}");
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
