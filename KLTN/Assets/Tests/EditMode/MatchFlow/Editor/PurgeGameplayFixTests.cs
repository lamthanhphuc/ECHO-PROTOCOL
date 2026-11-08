using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EchoProtocol.Networking;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PurgeGameplayFixTests
{
    private readonly List<GameObject> _objects = new List<GameObject>();
    private static readonly Vector3 Origin = new Vector3(2000, 1000, 2000);
    private AlienHuntMission _originalMission;
    private object _originalBoundary;
    [SetUp] public void SaveSceneSingletons()
    {
        _originalMission = AlienHuntMission.Instance;
        _originalBoundary = typeof(FloodedWaterBoundary).GetField("_active", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
    }
    private GameObject New(string name) { var go = new GameObject(name); _objects.Add(go); return go; }
    private BoxCollider Box(string name, Vector3 position, Vector3 size)
    { var go = New(name); go.transform.position = position; var box = go.AddComponent<BoxCollider>(); box.size = size; return box; }
    [TearDown] public void Cleanup()
    {
        foreach (var go in _objects) if (go) UnityEngine.Object.DestroyImmediate(go); _objects.Clear();
        typeof(AlienHuntMission).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, _originalMission);
        typeof(FloodedWaterBoundary).GetField("_active", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, _originalBoundary);
    }
    private static object Query(string method, params object[] args)
    {
        var function = typeof(NetworkPlayerFlashlight).GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(function, Is.Not.Null, method);
        return function.Invoke(null, args);
    }

    [Test] public void UvFindsOverlappingTargetAndIgnoresOwnersLargeHead()
    {
        var owner = New("Jammo owner"); owner.transform.position = Origin;
        var head = Box("Large head", Origin, Vector3.one * 1.5f); head.transform.SetParent(owner.transform, true);
        var target = Box("Close Fishcat", Origin + Vector3.forward * .5f, Vector3.one * .6f);
        var behind = Box("Behind player", Origin - Vector3.forward, Vector3.one * .3f);
        Physics.SyncTransforms();
        var hits = (Collider[])Query("QueryResonanceCandidates", owner.transform, Origin, Vector3.forward);
        Assert.That(hits, Has.Member(target)); Assert.That(hits, Has.No.Member(head)); Assert.That(hits, Has.No.Member(behind));
        Assert.That(Query("ResonanceLineOfSight", owner.transform, Origin, target), Is.True);
    }

    [Test] public void UvCannotAffectTargetThroughWall()
    {
        var owner = New("UV owner"); owner.transform.position = Origin;
        var target = Box("Fishcat", Origin + Vector3.forward * 2, Vector3.one * .5f);
        Box("Wall", Origin + Vector3.forward, new Vector3(3, 3, .1f)); Physics.SyncTransforms();
        Assert.That(Query("ResonanceLineOfSight", owner.transform, Origin, target), Is.False);
    }

    [Test] public void FurnaceRejectsAlienBeforeFuelAndKeepsItCarried()
    {
        var mission = New("Mission").AddComponent<AlienHuntMission>();
        var alien = New("Alien").AddComponent<AlienHuntActor>(); alien.Configure(0, HuntKind.Alien, null);
        var furnace = New("Furnace").AddComponent<AlienHuntFurnace>(); furnace.Configure(0, furnace.transform);
        var owner = New("Player");
        typeof(AlienHuntMission).GetField("actors", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(mission, new[] { alien });
        typeof(AlienHuntMission).GetField("furnaces", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(mission, new[] { furnace });
        typeof(AlienHuntMission).GetField("_offlineReady", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(mission, true);
        var entities = (HuntEntityState[])typeof(AlienHuntMission).GetField("_offline", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(mission);
        var furnaces = (HuntFurnaceState[])typeof(AlienHuntMission).GetField("_offlineFurnaces", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(mission);
        var player = PlayerRef.FromIndex(0); entities[0] = new HuntEntityState { Carrier = player, Stage = (int)AlienStage.Carried };
        Assert.That(mission.Act(player, owner.transform, -1, 0), Is.False);
        Assert.That(entities[0].Carrier, Is.EqualTo(player)); Assert.That(furnaces[0].AlienSlot, Is.Zero);
        furnaces[0].Fuel = 1;
        Assert.That(mission.Act(player, owner.transform, -1, 0), Is.True);
        Assert.That(entities[0].Stage, Is.EqualTo((int)AlienStage.OnPan)); Assert.That(furnaces[0].AlienSlot, Is.EqualTo(1));
    }

    [Test] public void ReadyFurnaceIgnitesWhileCarryingMeatOrJerrycan()
    {
        var mission = New("Ignition mission").AddComponent<AlienHuntMission>();
        typeof(AlienHuntMission).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, mission);
        var meat = New("Meat").AddComponent<AlienHuntActor>(); meat.Configure(0, HuntKind.Meat, null);
        var alien = New("Alien").AddComponent<AlienHuntActor>(); alien.Configure(1, HuntKind.Alien, null);
        var can = New("Jerrycan").AddComponent<AlienHuntActor>(); can.Configure(2, HuntKind.Jerrycan, null);
        var furnace = New("Furnace").AddComponent<AlienHuntFurnace>(); furnace.Configure(0, furnace.transform);
        var owner = New("Player");
        typeof(AlienHuntMission).GetField("actors", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(mission, new[] { meat, alien, can });
        typeof(AlienHuntMission).GetField("furnaces", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(mission, new[] { furnace });
        typeof(AlienHuntMission).GetField("_offlineReady", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(mission, true);
        var entities = (HuntEntityState[])typeof(AlienHuntMission).GetField("_offline", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(mission);
        var furnaces = (HuntFurnaceState[])typeof(AlienHuntMission).GetField("_offlineFurnaces", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(mission);
        var player = PlayerRef.FromIndex(0);
        foreach (int held in new[] { 0, 2 })
        {
            furnaces[0] = new HuntFurnaceState { Fuel = 1, AlienSlot = 2 };
            entities[1] = new HuntEntityState { Carrier = PlayerRef.None, Stage = (int)AlienStage.OnPan };
            entities[held] = new HuntEntityState { Carrier = player, Stage = 2 };
            Assert.That(furnace.InteractionPrompt, Does.Contain("CHÂM LỬA"));
            Assert.That(mission.Act(player, owner.transform, -1, 0), Is.True);
            Assert.That(furnaces[0].BurnRemaining, Is.GreaterThan(0f));
            Assert.That(entities[1].Stage, Is.EqualTo((int)AlienStage.Burning));
            Assert.That(entities[held].Carrier, Is.EqualTo(player));
            entities[held] = default;
        }
    }

    [Test] public void WaterAllowsShallowSupportButRejectsDeepFloorAndRoof()
    {
        var go = New("Water boundary"); go.transform.position = Origin;
        var boundary = go.AddComponent<FloodedWaterBoundary>();
        var waterGo = GameObject.CreatePrimitive(PrimitiveType.Cube); _objects.Add(waterGo); waterGo.transform.position = Origin;
        waterGo.transform.localScale = new Vector3(30, .01f, 30); UnityEngine.Object.DestroyImmediate(waterGo.GetComponent<Collider>());
        typeof(FloodedWaterBoundary).GetField("waterSurface", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(boundary, waterGo.GetComponent<Renderer>());
        var support = Box("Shallow ground", Origin - Vector3.up * .2f, new Vector3(4, .1f, 4)); Physics.SyncTransforms();
        var check = typeof(FloodedWaterBoundary).GetMethod("HasDrySupport", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(check.Invoke(boundary, new object[] { Origin, Origin.y, .3f }), Is.True);
        support.size = new Vector3(.4f, .1f, 4); Physics.SyncTransforms();
        var traverse = typeof(FloodedWaterBoundary).GetMethod("CanTraverse", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(traverse.Invoke(boundary, new object[] { Origin, Vector3.forward * .2f, Origin.y, .5f, .3f }), Is.True, "A narrow bridge must not require dry ground on both sides");
        support.transform.position = Origin - Vector3.up * 2; Physics.SyncTransforms();
        Assert.That(check.Invoke(boundary, new object[] { Origin, Origin.y, .3f }), Is.False);
        support.transform.position = Origin + Vector3.up * 2; Physics.SyncTransforms();
        Assert.That(check.Invoke(boundary, new object[] { Origin, Origin.y, .3f }), Is.False);
    }

    [Test] public void NestUsesFloorBelowRoofAndRejectsUnsupportedFootprint()
    {
        var nest = New("Nest"); nest.transform.position = Origin;
        var egg = GameObject.CreatePrimitive(PrimitiveType.Cube); _objects.Add(egg); egg.name = "AlienEgg_Decoration";
        egg.transform.SetParent(nest.transform, false); egg.transform.localScale = Vector3.one * .5f;
        var floor = Box("Ground", Origin - Vector3.up * .1f, new Vector3(4, .2f, 4));
        Box("Roof", Origin + Vector3.up * 2, new Vector3(4, .1f, 4)); Physics.SyncTransforms();
        Assert.That(AlienHuntActor.TryNestGround(nest.transform, Vector3.zero, out float height), Is.True);
        Assert.That(height, Is.EqualTo(Origin.y).Within(.02f));
        UnityEngine.Object.DestroyImmediate(floor.gameObject); Physics.SyncTransforms();
        Assert.That(AlienHuntActor.TryNestGround(nest.transform, Vector3.zero, out _), Is.False);
    }

    [Test] public void PurgeStaminaOverridesOnlySceneB()
    {
        var previousScene = SceneManager.GetActiveScene();
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
            UnityEditor.SceneManagement.NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var movement = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerNetwork.prefab").GetComponent<NetworkPlayerMovement>();
            scene.name = "Scene_B";
            Assert.That(movement.MaxStamina, Is.EqualTo(250));
            Assert.That(typeof(NetworkPlayerMovement).GetProperty("SprintStaminaDrain", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(movement), Is.EqualTo(22f));
            Assert.That(typeof(NetworkPlayerMovement).GetProperty("StaminaRegen", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(movement), Is.EqualTo(50f));
            scene.name = "Stamina_OtherMode_Test";
            Assert.That(movement.MaxStamina, Is.EqualTo(200));
        }
        finally
        {
            SceneManager.SetActiveScene(previousScene);
            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test] public void EatingClipContainsPelvisMovementAndKeepsItsAssetReference()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/AlienHunt/Animations/AlienYoung_Eating.anim");
        var bindings = AnimationUtility.GetCurveBindings(clip);
        Assert.That(bindings.Any(b => b.path == "root/root.x" && b.propertyName == "m_LocalPosition.y"), Is.True);
        Assert.That(bindings.Any(b => b.path == "root/root.x" && b.propertyName.StartsWith("m_LocalRotation")), Is.True);
        Assert.That(clip.isLooping, Is.True);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/AlienHunt/Animations/AC_AlienYoung.controller");
        Assert.That(controller.layers[0].stateMachine.states.Single(s => s.state.name == "Eating").state.motion, Is.EqualTo(clip));
    }

    [Test] public void CatfishWalkRepeatsAndUsesOriginalWalkClip()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/Minion/AC_CatfishMinion.controller");
        var clip = controller.layers[0].stateMachine.states.Single(s => s.state.name == "Walk").state.motion as AnimationClip;
        Assert.That(clip, Is.Not.Null); Assert.That(clip.isLooping, Is.True);
        Assert.That(AssetDatabase.GetAssetPath(clip), Does.EndWith("Catfish@walk.FBX"));
    }
}
