using System.Collections;
using System.Linq;
using EchoProtocol.Networking;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Assert = NUnit.Framework.Assert;

public sealed class AlienHuntSceneTests
{
    [Test]
    public void PreparedSceneHasNineSingleColorAliensAndUsableFurnaces()
    {
        const string scenePath = "Assets/Scenes/Scene_B.unity";
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
        if (scene.isLoaded)
        {
            Validate();
            return;
        }

        scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        try { Validate(); }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    public static string Validate()
    {
        var mission = Object.FindAnyObjectByType<AlienHuntMission>();
        Assert.That(mission, Is.Not.Null);
        Assert.That(mission.gameObject.scene.path, Is.EqualTo("Assets/Scenes/Scene_B.unity"));
        Assert.That(mission.Actors.Length, Is.EqualTo(AlienHuntMission.EntityCount));
        Assert.That(mission.Actors.Select(a => a.Index), Is.EqualTo(Enumerable.Range(0, AlienHuntMission.EntityCount)));
        Assert.That(mission.Actors.Count(a => a.Kind == HuntKind.Alien), Is.EqualTo(9));
        Assert.That(mission.Actors.Count(a => a.Kind == HuntKind.Jerrycan), Is.EqualTo(18));
        Assert.That(mission.Actors.Count(a => a.Kind == HuntKind.Meat), Is.EqualTo(AlienHuntMission.MeatCount));
        foreach (var fuel in mission.Actors.Where(a => a.Kind == HuntKind.Jerrycan))
        {
            Assert.That(fuel.GetComponentsInChildren<Collider>().Length, Is.GreaterThan(0), fuel.name);
            Assert.That(fuel.GetComponentInChildren<Renderer>().bounds.size.y, Is.InRange(.60f, .69f),
                fuel.name + " should be large enough to see and pick up.");
        }
        Assert.That(mission.Furnaces.Length, Is.EqualTo(3));
        foreach (var furnace in mission.Furnaces)
        {
            Assert.That(furnace.Pan, Is.Not.Null);
            Assert.That(furnace.GetComponentsInChildren<Collider>().Length, Is.GreaterThan(0));
        }
        var aliens = mission.Actors.Take(9).ToArray();
        foreach (var meat in mission.Actors.Where(a => a.Kind == HuntKind.Meat))
        {
            var meatSize = meat.GetComponentInChildren<Renderer>().bounds.size;
            Assert.That(meatSize.x, Is.InRange(1.20f, 1.44f), meat.name + " should be clearly visible at ground level.");
            Assert.That(meat.GetComponent<BoxCollider>().size.x, Is.InRange(1.20f, 1.44f),
                meat.name + " pickup collider should match the model.");
            Assert.That(NavMesh.SamplePosition(meat.transform.position, out _, .2f, NavMesh.AllAreas), Is.True);
            foreach (var alien in aliens)
                Assert.That(Vector2.Distance(new Vector2(meat.transform.position.x, meat.transform.position.z),
                    new Vector2(alien.transform.parent.position.x, alien.transform.parent.position.z)), Is.GreaterThanOrEqualTo(25.9f));
        }
        var materials = aliens.SelectMany(a => a.GetComponentsInChildren<Renderer>()).SelectMany(r => r.sharedMaterials).Distinct().ToArray();
        Assert.That(materials.Length, Is.EqualTo(1));
        Assert.That(materials[0].GetTexture("_BaseMap"), Is.Not.Null);
        var path = new NavMeshPath();
        foreach (var alien in aliens)
        {
            Assert.That(NavMesh.SamplePosition(alien.transform.position, out var hit, .2f, NavMesh.AllAreas), Is.True, alien.name);
            Assert.That(NavMesh.CalculatePath(aliens[0].transform.position, hit.position, NavMesh.AllAreas, path), Is.True);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
            var animator = alien.GetComponentInChildren<Animator>();
            Assert.That(animator.runtimeAnimatorController.animationClips.Length, Is.EqualTo(5));
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    if (binding.type == typeof(Transform) && !string.IsNullOrEmpty(binding.path))
                        Assert.That(animator.transform.Find(binding.path), Is.Not.Null, clip.name + ": " + binding.path);
        }
        Assert.That(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == "Assets/Scenes/Scene_B.unity"), Is.True);
        return "PASS 9 connected alien spawns, one material, five correctly bound animations, 18 fuel, 12 meat and 3 furnaces.";
    }

    public IEnumerator BaitCaptureFuelAndBurnCycleConsumesExactlyOneAlien()
    {
        const string scenePath = "Assets/Scenes/Scene_B.unity";
        Vector3[] authoredHomes = null;
        void CaptureHomes(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != scenePath) return;
            // sceneLoaded runs after Awake and before Start relocates the nests.
            authoredHomes = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<AlienHuntActor>(true))
                .Where(actor => actor.Kind == HuntKind.Alien)
                .OrderBy(actor => actor.Index)
                .Select(actor => actor.transform.position)
                .ToArray();
        }

        SceneManager.sceneLoaded += CaptureHomes;
        try { yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single); }
        finally { SceneManager.sceneLoaded -= CaptureHomes; }
        Assert.That(authoredHomes, Is.Not.Null);
        float initializationDeadline = Time.realtimeSinceStartup + 10f;
        while ((!AlienHuntMission.Instance || !AlienHuntMission.Instance.Ready)
            && Time.realtimeSinceStartup < initializationDeadline) yield return null;
        var mission = AlienHuntMission.Instance;
        Assert.That(mission, Is.Not.Null, "Scene_B mission did not initialize after entering Play mode.");
        Assert.That(mission.Ready, Is.True);
        Assert.That(authoredHomes.Length, Is.EqualTo(9));

        int relocated = 0;

        for (int i = 0; i < 9; i++)
        {
            Vector3 home = mission.Actors[i].Home;

            if (Vector3.Distance(home, authoredHomes[i]) > 1f)
                relocated++;

            Assert.That(
                NavMesh.SamplePosition(
                    home, out _, 1f, NavMesh.AllAreas),
                Is.True);

            for (int j = 0; j < i; j++)
            {
                Assert.That(
                    Vector3.Distance(
                        home, mission.Actors[j].Home),
                    Is.GreaterThanOrEqualTo(18f));
            }
        }

        Assert.That(relocated, Is.GreaterThan(0),
            "All nine nests remained at their authored positions.");
        Assert.That(Enumerable.Range(9, 18).Count(i => mission.Read(i).Stage == 0), Is.EqualTo(10));
        Assert.That(Enumerable.Range(9, 18).Count(i => mission.Read(i).Stage == 3), Is.EqualTo(8));
        var player = new GameObject("AlienHunt_TestInteractor");
        int inactiveFuel = Enumerable.Range(9, 18).First(i => mission.Read(i).Stage == 3);
        player.transform.position = mission.Actors[inactiveFuel].transform.position;
        Assert.That(mission.Act(PlayerRef.FromIndex(0), player.transform, inactiveFuel, -1), Is.False);

        var anchorComponent = player.AddComponent<PlayerHeldItemAnchor>();
        var testHand = new GameObject("TestRightHand").transform; testHand.SetParent(player.transform,false);
        testHand.localPosition = new Vector3(.4f,1.2f,.5f);
        var anchorSerialized = new SerializedObject(anchorComponent);
        anchorSerialized.FindProperty("rightHandAnchor").objectReferenceValue = testHand;
        anchorSerialized.ApplyModifiedPropertiesWithoutUndo();
        var actor = PlayerRef.FromIndex(0);
        player.transform.position = mission.Actors[27].transform.position;
        var groundMeatPosition = mission.Actors[27].transform.GetChild(0).localPosition;
        var groundMeatRotation = mission.Actors[27].transform.GetChild(0).localRotation;
        mission.Request(27, -1, player);
        Assert.That(mission.HeldIndex(actor), Is.EqualTo(27));
        Assert.That(AlienHuntMission.IsCarrying(player), Is.True);
        mission.Actors[27].Present(mission.Read(27), false, .02f);
        var heldMeatVisual = mission.Actors[27].transform.GetChild(0);
        Assert.That(heldMeatVisual.localScale.x, Is.EqualTo(720f).Within(.01f),
            "Carrying meat must preserve its full world scale.");
        Assert.That(heldMeatVisual.localPosition, Is.EqualTo(new Vector3(-.111f, .588f, .12f)));
        Assert.That(Quaternion.Angle(heldMeatVisual.localRotation,
            Quaternion.Euler(10.643f, -141.51f, -117.218f)), Is.LessThan(.01f));
        mission.HeldPose(actor,HuntKind.Meat,out var heldPosition,out var heldRotation,true);
        var actualAnchor = PlayerHeldItemAnchor.ResolveRightHandAnchor(player);
        var hand = actualAnchor.position + player.transform.forward * .08f;
        Assert.That(Vector3.Distance(heldPosition + heldRotation * new Vector3(-.04f,.54f,0), hand),
            Is.LessThan(.001f), "The meat bone must meet the right hand.");
        Assert.That(Vector3.Angle(heldRotation * Vector3.right, Vector3.up), Is.LessThan(25f),
            "The meat should rise from the gripped bone.");
        Assert.That(Vector3.Dot(heldRotation * Vector3.right, player.transform.forward), Is.GreaterThan(.2f),
            "The meat should lean forward, away from the player's face.");
        mission.HeldPose(actor,HuntKind.Alien,out var alienPosition,out var alienRotation,true);
        Assert.That(Vector3.Distance(alienPosition + alienRotation * new Vector3(.05f,.61f,.15f), hand),
            Is.LessThan(.001f), "The alien torso must meet the right hand.");
        Assert.That(Vector3.Angle(alienRotation * Vector3.up, Vector3.up), Is.LessThan(.01f),
            "Carried aliens must stand upright.");
        mission.HeldPose(actor,HuntKind.Jerrycan,out var canPosition,out var canRotation,true);
        Assert.That(Vector3.Distance(canPosition + canRotation * new Vector3(0,.39f * AlienHuntActor.JerrycanVisualScale,0), hand),
            Is.LessThan(.001f), "The can handle must meet the right hand.");
        Assert.That(Vector3.Angle(canRotation * Vector3.up, Vector3.up), Is.LessThan(.01f),
            "Carried cans must stand upright.");

        bool foundBait = false;
        var origin = mission.Actors[0].transform.position;
        var baitPath = new NavMeshPath();
        for (int i = 0; i < 16; i++)
        {
            float angle = i * Mathf.PI / 8f;
            var point = origin + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 2f;
            if (!NavMesh.SamplePosition(point, out var hit, .4f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y-origin.y) > .4f) continue;
            if (!NavMesh.CalculatePath(origin, hit.position, NavMesh.AllAreas, baitPath) || baitPath.status != NavMeshPathStatus.PathComplete) continue;
            player.transform.position = hit.position;
            player.transform.rotation = Quaternion.LookRotation(origin-hit.position);
            foundBait = true; break;
        }
        Assert.That(foundBait, Is.True, "No same-floor reachable bait spot beside the alien.");
        Assert.That(mission.Act(actor, player.transform, -2, -1), Is.True);
        Assert.That(mission.Read(27).Stage, Is.EqualTo(2));
        mission.Actors[27].Present(mission.Read(27), false, .02f);
        Assert.That(heldMeatVisual.localPosition, Is.EqualTo(groundMeatPosition));
        Assert.That(Quaternion.Angle(heldMeatVisual.localRotation, groundMeatRotation), Is.LessThan(.01f));
        float deadline = Time.realtimeSinceStartup + 25f;
        bool ate = false;
        while (Time.realtimeSinceStartup < deadline && mission.Read(0).Stage != (int)AlienStage.Eating)
        {
            ate |= mission.Read(0).Stage == (int)AlienStage.Eating;
            yield return null;
        }
        var debugState = mission.Read(0);
        var debugAgent = mission.Actors[0].Agent;
        ate |= mission.Read(0).Stage == (int)AlienStage.Eating;
        Assert.That(ate, Is.True, "Alien did not reach and eat the placed bait. stage=" + debugState.Stage
            + " target=" + debugState.Target + " position=" + debugState.Position + " bait=" + mission.Read(27).Position
            + " agent=" + debugAgent.enabled + "/" + debugAgent.isOnNavMesh + " path=" + debugAgent.pathStatus
            + " remaining=" + (debugAgent.enabled ? debugAgent.remainingDistance : -1) + " timeScale=" + Time.timeScale
            + " states=" + string.Join(",", Enumerable.Range(0, 9).Select(i => i + ":" + mission.Read(i).Stage + "/" + mission.Read(i).Target)));
        Assert.That(mission.Read(0).Stage, Is.EqualTo((int)AlienStage.Eating));
        player.transform.position = mission.Actors[0].transform.position;
        Assert.That(mission.Act(actor, player.transform, 0, -1), Is.True);
        Assert.That(mission.Read(27).Stage, Is.EqualTo(3));
        Assert.That(mission.Act(PlayerRef.FromIndex(1), player.transform, 0, -1), Is.False, "Two players captured the same alien.");
        player.transform.position = mission.Furnaces[0].transform.position;
        Assert.That(mission.Act(actor, player.transform, -1, 0), Is.True);
        Assert.That(mission.Read(0).Stage, Is.EqualTo((int)AlienStage.OnPan));
        Assert.That(mission.Act(actor, player.transform, -1, 0), Is.False, "An unfueled pan burned an alien.");
        int fuelIndex = Enumerable.Range(9, 18).First(i => mission.Read(i).Stage == 0);
        player.transform.position = mission.Actors[fuelIndex].transform.position;
        Assert.That(mission.Act(actor, player.transform, fuelIndex, -1), Is.True);
        player.transform.position = mission.Furnaces[0].transform.position;
        Assert.That(mission.Act(actor, player.transform, -1, 0), Is.True);
        Assert.That(mission.Read(fuelIndex).Stage, Is.EqualTo(3));
        Assert.That(
            mission.Read(fuelIndex).Remaining,
            Is.EqualTo(90f).Within(.1f));
        Assert.That(mission.ReadFurnace(0).Fuel, Is.EqualTo(1));
        Assert.That(mission.Act(actor, player.transform, -1, 0), Is.True);
        Assert.That(mission.ReadFurnace(0).Fuel, Is.EqualTo(1), "Oil stays reserved until this alien finishes burning.");
        yield return null;
        Assert.That(mission.Furnaces[0].OilVisible, Is.True);
        Assert.That(mission.Furnaces[0].FireVisible, Is.True);
        Assert.That(mission.Act(actor, player.transform, -1, 0), Is.False, "A burning pan accepted a second ignition.");
        deadline = Time.time + 20f;
        while (mission.Burned == 0 && Time.time < deadline) yield return null;
        Assert.That(mission.Burned, Is.EqualTo(1));
        Assert.That(mission.Read(0).Stage, Is.EqualTo((int)AlienStage.Burned));
        Assert.That(mission.ReadFurnace(0).AlienSlot, Is.Zero);
        Assert.That(mission.ReadFurnace(0).BurnedCount, Is.EqualTo(1));
        Assert.That(mission.ReadFurnace(0).Fuel, Is.Zero);
        var write = typeof(AlienHuntMission).GetMethod(
            "Write",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);
        var consumedCan = mission.Read(fuelIndex);
        consumedCan.Remaining = .01f;
        write.Invoke(mission, new object[] { fuelIndex, consumedCan });

        mission.Tick(.02f);

        Assert.That(mission.Read(fuelIndex).Stage, Is.EqualTo(0));
        Assert.That(
            mission.Read(fuelIndex).Position,
            Is.EqualTo(mission.Actors[fuelIndex].Home));

        Assert.That(
            Enumerable.Range(9, 18)
                .Count(i => mission.Read(i).Stage == 0 ||
                            mission.Read(i).Stage == 1),
            Is.LessThanOrEqualTo(AlienHuntMission.JerrycanCount));
        yield return null;
        Assert.That(mission.Furnaces[0].OilVisible, Is.False);
        Assert.That(mission.Furnaces[0].FireVisible, Is.False);
        // Reproduce recovery after a dropped capture, with the burned alien's nest now vacant.
        var recovering = mission.Read(1);
        recovering.Stage = (int)AlienStage.Stunned; recovering.Remaining = .01f;
        write.Invoke(mission, new object[] { 1, recovering });
        mission.Tick(.02f);
        recovering = mission.Read(1);
        Assert.That(recovering.Stage, Is.EqualTo((int)AlienStage.Wandering));
        Assert.That(recovering.NestIndex, Is.EqualTo(0), "Must choose the other vacant nest.");
        mission.Tick(.02f);
        Assert.That(mission.Actors[1].Agent.speed, Is.EqualTo(1f), "Recovery must use walking speed.");
        var lure = mission.Read(28); lure.Stage = 2; lure.Position = mission.Read(1).Position;
        write.Invoke(mission, new object[] { 28, lure });
        mission.Tick(.02f);
        Assert.That(mission.Read(1).Target, Is.EqualTo(28), "Returning alien must still accept bait.");
        recovering = mission.Read(1); recovering.Stage = (int)AlienStage.OnPan; recovering.Target = -1;
        write.Invoke(mission, new object[] { 1, recovering });
        // Exhaust supply and verify emergency replenishment respects its minimum delay.
        for (int i = 27; i < AlienHuntMission.EntityCount; i++)
        {
            var meat = mission.Read(i); meat.Stage = 3; meat.Remaining = 60;
            write.Invoke(mission, new object[] { i, meat });
        }
        mission.Tick(14f);
        Assert.That(mission.Read(29).Stage, Is.EqualTo(3));
        mission.Tick(1.1f);
        Assert.That(Enumerable.Range(27, AlienHuntMission.MeatCount).Count(i => mission.Read(i).Stage == 0), Is.EqualTo(3));
        mission.Tick(45f);
        Assert.That(Enumerable.Range(27, AlienHuntMission.MeatCount).Count(i => mission.Read(i).Stage == 0), Is.EqualTo(AlienHuntMission.MeatCount));
        // Oil can be poured first, but a second can must remain in the player's hand.
        int firstCan = Enumerable.Range(9,18).First(i => mission.Read(i).Stage == 0);
        player.transform.position = mission.Actors[firstCan].transform.position;
        Assert.That(mission.Act(actor,player.transform,firstCan,-1),Is.True);
        player.transform.position = mission.Furnaces[1].transform.position;
        Assert.That(mission.Act(actor,player.transform,-1,1),Is.True);
        yield return null;
        Assert.That(mission.Furnaces[1].OilVisible,Is.True);
        Assert.That(mission.Furnaces[1].FireVisible,Is.False);
        int secondCan = Enumerable.Range(9,18).First(i => mission.Read(i).Stage == 0);
        player.transform.position = mission.Actors[secondCan].transform.position;
        Assert.That(mission.Act(actor,player.transform,secondCan,-1),Is.True);
        player.transform.position = mission.Furnaces[1].transform.position;
        Assert.That(mission.Act(actor,player.transform,-1,1),Is.False);
        Assert.That(mission.HeldIndex(actor),Is.EqualTo(secondCan));
        // Use the actual offline inventory drop entry point, which must consume the input.
        player.transform.position = mission.Actors[2].transform.position;
        var dropInput = player.AddComponent<PlayerInventoryDropInput>();
        Assert.That(dropInput.DropCurrentItem(),Is.True);
        Assert.That(mission.HeldIndex(actor),Is.EqualTo(-1));
        var panAlien = mission.Read(8); panAlien.Stage=(int)AlienStage.Stunned; panAlien.Remaining=30;
        write.Invoke(mission,new object[]{8,panAlien});
        player.transform.position = mission.Actors[8].transform.position;
        Assert.That(mission.Act(actor,player.transform,8,-1),Is.True);
        player.transform.position = mission.Furnaces[1].transform.position;
        Assert.That(mission.Act(actor,player.transform,-1,1),Is.True);
        Assert.That(mission.ReadFurnace(1).AlienSlot,Is.EqualTo(9));
        // Simulate no progress: release the bait claim instead of keeping it forever.
        var stuck = mission.Read(2); stuck.Stage=(int)AlienStage.Running; stuck.Target=29;
        stuck.StuckSeconds=8; stuck.ProgressPosition=mission.Actors[2].transform.position;
        write.Invoke(mission,new object[]{2,stuck});
        var abandoned = mission.Read(29); abandoned.Stage=2; abandoned.Remaining=120;
        write.Invoke(mission,new object[]{29,abandoned});
        mission.Tick(.02f);
        Assert.That(mission.Read(2).Target,Is.EqualTo(-1));
        Assert.That(mission.IsBaitClaimed(29),Is.False);
        Assert.That(mission.Read(2).BaitCooldown,Is.GreaterThan(0));
        abandoned.Remaining=.01f; write.Invoke(mission,new object[]{29,abandoned});
        mission.Tick(.02f);
        Assert.That(mission.Read(29).Stage,Is.Zero,"Abandoned bait must return to a supply point.");
        // Missing the meal window must not leave a naturally stunned, catchable alien.
        var missed = mission.Read(7); missed.Stage=(int)AlienStage.Eating; missed.Target=33; missed.Remaining=.01f;
        var missedBait=mission.Read(33); missedBait.Stage=2;
        write.Invoke(mission,new object[]{33,missedBait});
        write.Invoke(mission,new object[]{7,missed});
        mission.Tick(.02f);
        Assert.That(mission.Read(7).Stage,Is.EqualTo((int)AlienStage.Wandering));
        Assert.That((bool)mission.Read(7).Returning,Is.True);
        Assert.That((bool)mission.Read(7).Fleeing,Is.True);
        Assert.That(mission.Read(33).Stage,Is.EqualTo(3));
        player.transform.position=mission.Actors[7].transform.position;
        Assert.That(mission.Actors[7].CanInteract(player),Is.False);
        Assert.That(mission.Act(actor,player.transform,7,-1),Is.False);
        // Catch during eating or recapture a previously dropped, stunned alien; the claimed bait must be consumed once.
        var eatingAlien = mission.Read(6); eatingAlien.Stage=(int)AlienStage.Eating;
        eatingAlien.Target=32; eatingAlien.Remaining=4f;
        var eatingBait=mission.Read(32); eatingBait.Stage=2; eatingBait.Remaining=120;
        write.Invoke(mission,new object[]{32,eatingBait});
        write.Invoke(mission,new object[]{6,eatingAlien});
        player.transform.position=mission.Actors[6].transform.position;
        Assert.That(mission.Actors[6].CanInteract(player),Is.True);
        Assert.That(mission.Act(actor,player.transform,6,-1),Is.True);
        Assert.That(mission.Read(6).Stage,Is.EqualTo((int)AlienStage.Carried));
        Assert.That(mission.Read(6).Target,Is.EqualTo(-1));
        Assert.That(mission.Read(32).Stage,Is.EqualTo(3));
        Assert.That(mission.Read(32).Remaining,Is.EqualTo(60f));
        Assert.That(mission.IsBaitClaimed(32),Is.False);
        Assert.That(mission.Act(PlayerRef.FromIndex(1),player.transform,6,-1),Is.False);
        Assert.That(mission.Act(actor,player.transform,-2,-1),Is.True);
        Assert.That(mission.Read(6).Stage,Is.EqualTo((int)AlienStage.Stunned));
        // Food can be placed on a real solid floor that has no baked navigation.
        player.transform.position = mission.Actors[31].transform.position;
        mission.Request(31,-1,player);
        var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = "HuntTest_UnbakedFloor";
        platform.transform.position = player.transform.position + Vector3.up*10;
        platform.transform.localScale = new Vector3(5,.2f,5);
        player.transform.position = platform.transform.position + Vector3.up*.1f;
        Physics.SyncTransforms();
        Assert.That(mission.Act(actor,player.transform,-2,-1),Is.True);
        Assert.That(Mathf.Abs(mission.Read(31).Position.y-player.transform.position.y),Is.LessThan(.15f));
        // An alien cannot be stranded on that isolated floor.
        var isolatedAlien=mission.Read(4); isolatedAlien.Stage=(int)AlienStage.Stunned; isolatedAlien.Remaining=30;
        write.Invoke(mission,new object[]{4,isolatedAlien});
        var platformPosition=player.transform.position;
        player.transform.position=mission.Actors[4].transform.position;
        Assert.That(mission.Act(actor,player.transform,4,-1),Is.True);
        player.transform.position=platformPosition;
        Assert.That(mission.Act(actor,player.transform,-2,-1),Is.False);
        Assert.That(mission.HeldIndex(actor),Is.EqualTo(4));
        player.transform.position=mission.Actors[5].transform.position;
        Assert.That(mission.Act(actor,player.transform,-2,-1),Is.True);
        Object.DestroyImmediate(platform);
        // Completing a third burn permanently closes only this furnace.
        var writeFurnace=typeof(AlienHuntMission).GetMethod("WriteFurnace",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
        var finalBurn=mission.ReadFurnace(0); finalBurn.BurnedCount=2; finalBurn.AlienSlot=6;
        finalBurn.Fuel=1; finalBurn.BurnRemaining=.01f;
        var burningAlien=mission.Read(5); burningAlien.Stage=(int)AlienStage.Burning;
        write.Invoke(mission,new object[]{5,burningAlien});
        writeFurnace.Invoke(mission,new object[]{0,finalBurn});
        mission.Tick(.02f);
        Assert.That(mission.ReadFurnace(0).BurnedCount,Is.EqualTo(3));
        Assert.That(mission.ReadFurnace(0).Fuel,Is.Zero);
        mission.Tick(.02f);
        Assert.That(mission.ReadFurnace(0).BurnedCount,Is.EqualTo(3),"A completed burn must count once.");
        int extraCan=Enumerable.Range(9,18).First(i=>mission.Read(i).Stage==0);
        player.transform.position=mission.Actors[extraCan].transform.position;
        Assert.That(mission.Act(actor,player.transform,extraCan,-1),Is.True);
        player.transform.position=mission.Furnaces[0].transform.position;
        Assert.That(mission.Act(actor,player.transform,-1,0),Is.False);
        Assert.That(mission.HeldIndex(actor),Is.EqualTo(extraCan),"A full furnace must not consume another can.");
        Assert.That(mission.Furnaces[0].InteractionPrompt,Does.Contain("3/3"));
        Assert.That(mission.ReadFurnace(2).BurnedCount,Is.Zero);
        player.transform.position=mission.Actors[3].transform.position;
        Assert.That(mission.Act(actor,player.transform,-2,-1),Is.True);
        // A lost carrier drops on the last nearby floor rather than at the original supply point.
        player.transform.position=mission.Actors[30].transform.position;
        mission.Request(30,-1,player);
        Assert.That(mission.HeldIndex(actor),Is.EqualTo(30));
        player.transform.position=mission.Actors[3].transform.position;
        mission.Tick(.02f);
        var lastFloor=mission.Read(30).LastGroundPosition;
        Object.DestroyImmediate(player);
        mission.Tick(.02f);
        Assert.That(mission.Read(30).Carrier.IsNone,Is.True);
        Assert.That(Vector3.Distance(mission.Read(30).Position,lastFloor),Is.LessThan(4f));
        Assert.That(mission.Read(30).Stage,Is.EqualTo(2));
        int recoverableCan=Enumerable.Range(9,18).First(i=>mission.Read(i).Stage==0);
        var droppedCan=mission.Read(recoverableCan);
        droppedCan.Position=mission.Actors[recoverableCan].Home+Vector3.forward*3f;
        droppedCan.Remaining=.01f;
        write.Invoke(mission,new object[]{recoverableCan,droppedCan});
        mission.Tick(.02f);
        Assert.That(Vector3.Distance(mission.Read(recoverableCan).Position,mission.Actors[recoverableCan].Home),
            Is.LessThan(.01f),"An abandoned can must return to its authored pickup point.");
        int neededFuel=AlienHuntMission.AlienCount-mission.Burned;
        int usableFuel=Enumerable.Range(9,18).Count(i=>mission.Read(i).Stage==0||mission.Read(i).Stage==1)
            +Enumerable.Range(0,3).Sum(i=>mission.ReadFurnace(i).Fuel);
        for(int i=9;i<27&&usableFuel>=neededFuel;i++)
        {
            var can=mission.Read(i);
            if(can.Stage!=0)continue;
            can.Stage=3;write.Invoke(mission,new object[]{i,can});usableFuel--;
        }
        Assert.That(usableFuel,Is.LessThan(neededFuel));
        mission.Tick(.02f);
        usableFuel=Enumerable.Range(9,18).Count(i=>mission.Read(i).Stage==0||mission.Read(i).Stage==1)
            +Enumerable.Range(0,3).Sum(i=>mission.ReadFurnace(i).Fuel);
        Assert.That(usableFuel,Is.GreaterThanOrEqualTo(neededFuel),
            "Reserve canisters must prevent an otherwise unwinnable fuel shortage.");
        int roamingAlien=Enumerable.Range(0,9).First(i=>mission.Read(i).Stage==(int)AlienStage.Wandering);
        var stuckAlien=mission.Read(roamingAlien);
        int otherNest=roamingAlien==0?1:0;
        stuckAlien.NestIndex=otherNest;stuckAlien.Returning=true;stuckAlien.Fleeing=false;
        stuckAlien.BaitCooldown=100f;stuckAlien.ProgressPosition=mission.Actors[roamingAlien].transform.position;
        stuckAlien.StuckSeconds=7.99f;stuckAlien.TravelSeconds=1f;
        write.Invoke(mission,new object[]{roamingAlien,stuckAlien});
        mission.Tick(.02f);
        Assert.That(Vector3.Distance(mission.Read(roamingAlien).Position,mission.Actors[otherNest].Home),
            Is.LessThan(2f),"An alien still stuck after a repath must recover at a connected nest.");
    }
}
