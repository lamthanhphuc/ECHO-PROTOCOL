using System.Collections;
using System.Reflection;
using EchoProtocol.Networking;
using EchoProtocol.Gameplay;
using EchoProtocol.AI.Stalker;
using EchoProtocol.AI.Stalker.Networking;
using EchoProtocol.AI.Stalker.Spatial;
using EchoProtocol.Tools.Scanner;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Assert=NUnit.Framework.Assert;

public sealed class PurgeDirectorTests
{
    private NetworkRunner runner;
    private static void Set(NetworkMatchState m,string property,object value) => typeof(NetworkMatchState).GetProperty(property).SetValue(m,value);
    [Test]
    public void PurgeDetectionTimesMatchRageTiers()
    {
        var methodFill = typeof(StalkerController).GetMethod("GetDetectionFillRate", BindingFlags.NonPublic | BindingFlags.Instance);
        var methodDecay = typeof(StalkerController).GetMethod("GetDetectionDecayRate", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(methodFill, Is.Not.Null);
        Assert.That(methodDecay, Is.Not.Null);
    }
    public IEnumerator DirectorRageUvSupplyAndCatchRunOnStateAuthority()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/Scene_B.unity", LoadSceneMode.Single);
        runner=new GameObject("PurgeDirector_TestRunner").AddComponent<NetworkRunner>();
        var sceneManager=runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
        var startup=runner.StartGame(new StartGameArgs{GameMode=GameMode.Single,SceneManager=sceneManager,
            Scene=SceneRef.FromIndex(UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/Scene_B.unity")),PlayerCount=4});
        while(!startup.IsCompleted)yield return null;
        Assert.That(startup.Result.Ok,Is.True);
        yield return null;
        var marker=GameObject.Find("MonsterSpawn_Stalker_EMPTY");
        Assert.That(marker,Is.Not.Null);
        var bossPrefab=AssetDatabase.LoadAssetAtPath<NetworkObject>("Assets/Prefabs/StalkerNetwork_Lizard.prefab");
        var graph=AssetDatabase.LoadAssetAtPath<RegionGraphAsset>("Assets/AI/Stalker/Map2/AI_Stalker_Map2_Hunt_RegionGraph.asset");
        var spawner=new GameObject("Purge_TestSpawner").AddComponent<PlayerSpawner>();
        var flags=BindingFlags.NonPublic|BindingFlags.Instance;
        typeof(PlayerSpawner).GetField("_map2LizardPrefab",flags).SetValue(spawner,bossPrefab);
        typeof(PlayerSpawner).GetField("_map2HuntRegionGraph",flags).SetValue(spawner,graph);
        typeof(PlayerSpawner).GetMethod("EnsureWorldStateExamples",flags).Invoke(spawner,new object[]{runner});
        var match=NetworkMatchState.Instance;
        Assert.That(runner.TryFindObject(match.PurgeBossId,out var boss),Is.True,"Production spawner must bind a Purge Lizard.");
        var spawnMinions=typeof(PlayerSpawner).GetMethod("MaintainCreepMinionPopulation",flags);
        spawnMinions.Invoke(spawner,new object[]{runner});
        Assert.That(Object.FindObjectsByType<EchoProtocol.AI.Minions.CreepMinionRuntime>(FindObjectsSortMode.None).Length,Is.Zero,
            "No active gameplay player means no active spawn zone yet.");
        float deadline=Time.realtimeSinceStartup+10;
        while(!match.HuntReady && Time.realtimeSinceStartup<deadline)yield return null;
        Assert.That((bool)match.HuntReady,Is.True);
        Assert.That(match.PurgeCoopAidEnabled,Is.False);
        Assert.That(match.PurgeAidAvailable,Is.Zero);
        for(int i=0;i<9;i++) Assert.That(match.PurgeAid[i].Id.IsValid,Is.False,"Solo must not spawn medkits.");
        AlienHuntMission.TryDescribe(out _,out var soloHud,out _);
        StringAssert.DoesNotContain("UV:",soloHud);
        StringAssert.DoesNotContain("Sơ cứu:",soloHud);
        Assert.That(match.PurgeMood,Is.EqualTo(PurgeBossMood.Calm));
        match.RecordHuntBurn();
        Assert.That(match.HuntBurned, Is.EqualTo(1));
        Assert.That(match.PurgeRageTier, Is.EqualTo(0));
        Assert.That(match.PurgeMood, Is.EqualTo(PurgeBossMood.Calm));
        Assert.That(match.PurgeHuntsStarted, Is.Zero);
        Assert.That(match.PurgeChaseSpeed, Is.EqualTo(6.6f).Within(.01f));
        match.RecordHuntBurn();
        Assert.That(match.HuntBurned, Is.EqualTo(2));
        Assert.That(match.PurgeRageTier, Is.EqualTo(1));
        Assert.That(match.PurgeMood, Is.EqualTo(PurgeBossMood.Warning));
        Assert.That(match.PurgeHuntSeconds, Is.EqualTo(105f));
        Assert.That(match.PurgeHuntsStarted, Is.EqualTo(1));
        Assert.That(match.PurgeChaseSpeed, Is.EqualTo(7f).Within(.01f));
        Assert.That(match.PurgeLastRitualElapsedSeconds,Is.GreaterThanOrEqualTo(0f));
        Set(match,"PurgeMoodTimer",TickTimer.CreateFromSeconds(runner,.01f));
        yield return new WaitForSeconds(.1f);
        Assert.That(match.PurgeMood,Is.EqualTo(PurgeBossMood.Hunting));
        Set(match,"PurgeUvExposure",2.5f);
        yield return new WaitForSeconds(.08f);
        Assert.That(match.PurgeMood,Is.EqualTo(PurgeBossMood.Staggered));
        Assert.That(match.PurgeMoodTimer.RemainingTime(runner),Is.InRange(0.1f,3.5f));
        Assert.That(match.PurgeStaggers,Is.EqualTo(1));
        Assert.That(match.PurgeUvStaggersTotal,Is.EqualTo(1));
        Set(match,"PurgeStaggers",match.PurgeStaggersRequired);
        Set(match,"PurgeMoodTimer",TickTimer.CreateFromSeconds(runner,.01f));
        yield return new WaitForSeconds(.1f);
        Assert.That(match.PurgeMood,Is.EqualTo(PurgeBossMood.Calm));
        Assert.That(match.PurgeHuntsEndedByUv,Is.EqualTo(1));
        Assert.That(match.PurgeHuntsEndedByTimeout,Is.Zero);
        Assert.That(match.PurgeUvImmunity.IsRunning,Is.True);
        Assert.That(match.PurgeUvImmunity.RemainingTime(runner),Is.InRange(0.1f,20f));
        match.ProvokePurgeBoss();
        Assert.That(match.PurgeMood,Is.EqualTo(PurgeBossMood.Calm),
            "The post-hunt calm grace period must prevent a nearby player from immediately restarting the hunt.");
        // Calm may investigate players, but a timer alone must not start a new hunt.
        Set(match,"PurgeMoodTimer",TickTimer.CreateFromSeconds(runner,.01f));
        yield return new WaitForSeconds(.1f);
        Assert.That(match.PurgeMood,Is.EqualTo(PurgeBossMood.Calm));
        yield return new WaitForSeconds(.1f);
        Assert.That(match.PurgeAidAvailable,Is.Zero,"Solo must not refill medkits.");
        for (int i = 2; i < 6; i++) match.RecordHuntBurn();
        Assert.That(match.HuntBurned, Is.EqualTo(6));
        Assert.That(match.PurgeRageTier, Is.EqualTo(2));
        Assert.That(match.PurgeChaseSpeed, Is.EqualTo(7.8f).Within(.01f));
        for (int i = 6; i < 8; i++) match.RecordHuntBurn();
        Assert.That(match.HuntBurned,Is.EqualTo(8));
        Assert.That(match.PurgeHuntSeconds,Is.EqualTo(195f));
        Assert.That(match.PurgeHuntTimer.RemainingTime(runner),Is.GreaterThan(185f));
        Assert.That(match.PurgeHuntsStarted,Is.EqualTo(2));
        Assert.That(match.PurgeStaggersRequired,Is.EqualTo(4));
        Assert.That(match.PurgeMinionCap,Is.EqualTo(3));
        spawnMinions.Invoke(spawner,new object[]{runner});spawnMinions.Invoke(spawner,new object[]{runner});
        Assert.That(Object.FindObjectsByType<EchoProtocol.AI.Minions.CreepMinionRuntime>(FindObjectsSortMode.None).Length,Is.Zero,
            "A larger cap must not spawn Fishcat into an empty match.");
        Assert.That(match.PurgeRageTier, Is.EqualTo(3));
        Assert.That(match.PurgeChaseSpeed, Is.EqualTo(9f).Within(.01f));
        // UV immunity must reject another immediate stagger.
        Set(match,"PurgeMood",PurgeBossMood.Hunting);Set(match,"PurgeUvExposure",2.5f);
        Set(match,"PurgeUvImmunity",TickTimer.CreateFromSeconds(runner,4));
        yield return new WaitForSeconds(.08f);
        Assert.That(match.PurgeMood,Is.EqualTo(PurgeBossMood.Hunting));
        Assert.That(match.PurgeUvExposure,Is.Zero);
        var controller=boss.GetComponent<StalkerController>();
        controller.ApplyPurgePolicy(false,false,7);
        Assert.That(controller.SpecialEncounterOverrideActive,Is.False);
        controller.ApplyPurgePolicy(true,true,7);
        Assert.That(controller.AppliedSearchDuration,Is.EqualTo(75));
        Assert.That(controller.SpecialEncounterOverrideActive,Is.True);
        match.PacifyPurgeBoss();
        yield return new WaitForSeconds(.08f);
        Assert.That(controller.SpecialEncounterOverrideActive,Is.False);
        // Validate the actual boss-only UV query and occlusion, rather than just the exposure accumulator.
        var playerPrefab=AssetDatabase.LoadAssetAtPath<NetworkObject>("Assets/Prefabs/PlayerNetwork.prefab");
        Vector3 playerPosition=default;bool clearSpot=false;
        var targetCollider=boss.GetComponentInChildren<Collider>();
        for(int n=0;n<32;n++)
        {
            float angle=n*Mathf.PI/16;
            var candidate=boss.transform.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*3;
            if(!UnityEngine.AI.NavMesh.SamplePosition(candidate,out var floor,.5f,UnityEngine.AI.NavMesh.AllAreas))continue;
            var eye=floor.position+Vector3.up*1.6f;var ray=targetCollider.bounds.center-eye;
            var hits=Physics.SphereCastAll(eye,.75f,ray.normalized,ray.magnitude,~0,QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            bool blocked=false,found=false;
            foreach(var hit in hits)
            {if(hit.collider.gameObject.layer==4)continue;found=hit.collider.GetComponentInParent<StalkerFusionRuntime>()==boss.GetComponent<StalkerFusionRuntime>();break;}
            if(!found)continue;
            var muzzle=floor.position+Vector3.up*.9f;var sight=targetCollider.bounds.center-muzzle;
            foreach(var hit in Physics.RaycastAll(muzzle,sight.normalized,sight.magnitude,~0,QueryTriggerInteraction.Ignore))
                if(hit.collider.gameObject.layer!=4&&hit.collider.GetComponentInParent<StalkerFusionRuntime>()!=boss.GetComponent<StalkerFusionRuntime>())blocked=true;
            if(blocked)continue;
            playerPosition=floor.position;clearSpot=true;break;
        }
        Assert.That(clearSpot,Is.True,"Need a genuinely unobstructed UV test position.");
        var player=runner.Spawn(playerPrefab,playerPosition,Quaternion.identity,runner.LocalPlayer,
            (_,o)=>o.GetComponent<LobbyPlayerState>().InitializeAuthoritativeSelection(0,0,true));
        runner.SetPlayerObject(runner.LocalPlayer,player);
        yield return new WaitForSeconds(.1f); // Let the real flashlight muzzle follow the spawned rig.
        spawnMinions.Invoke(spawner,new object[]{runner});
        var fishcats=Object.FindObjectsByType<EchoProtocol.AI.Minions.CreepMinionRuntime>(FindObjectsSortMode.None);
        Assert.That(fishcats.Length,Is.EqualTo(1),"First population check should add only one Fishcat.");
        Assert.That(fishcats[0].Role,Is.EqualTo(EchoProtocol.AI.Minions.CreepMinionRole.Catfish));
        spawnMinions.Invoke(spawner,new object[]{runner});
        Assert.That(Object.FindObjectsByType<EchoProtocol.AI.Minions.CreepMinionRuntime>(FindObjectsSortMode.None).Length,Is.EqualTo(1),
            "Repeated checks may not bypass the 16-second refill cooldown at 8/9.");
        var refillSchedule=(PurgeFishcatRefillSchedule)typeof(PlayerSpawner).GetField("_purgeRefillSchedule",flags).GetValue(spawner);
        typeof(PurgeFishcatRefillSchedule).GetField("_refillStartedAt",flags).SetValue(refillSchedule,Time.time-17f);
        spawnMinions.Invoke(spawner,new object[]{runner});
        fishcats=Object.FindObjectsByType<EchoProtocol.AI.Minions.CreepMinionRuntime>(FindObjectsSortMode.None);
        Assert.That(fishcats.Length,Is.EqualTo(2));
        var fishPrefab=AssetDatabase.LoadAssetAtPath<NetworkObject>("Assets/Resources/PF_CreepMinionNetwork.prefab");
        Vector3 thirdPosition=player.transform.position+(boss.transform.position-player.transform.position).normalized*1.5f;
        var thirdFish=runner.Spawn(fishPrefab,thirdPosition,Quaternion.LookRotation(player.transform.position-thirdPosition),PlayerRef.None,
            (_,o)=>o.GetComponent<EchoProtocol.AI.Minions.CreepMinionRuntime>().ConfigureBeforeSpawn(
                RegionSemanticZone.Zone01,EchoProtocol.AI.Minions.CreepMinionRole.Catfish,0));
        var third=thirdFish.GetComponent<EchoProtocol.AI.Minions.CreepMinionRuntime>();
        var targetProperty=typeof(EchoProtocol.AI.Minions.CreepMinionRuntime).GetProperty("TargetPlayer");
        targetProperty.SetValue(fishcats[0],runner.LocalPlayer);
        targetProperty.SetValue(fishcats[1],runner.LocalPlayer);
        Physics.SyncTransforms();
        var findTarget=typeof(EchoProtocol.AI.Minions.CreepMinionRuntime).GetMethod("FindBestVisibleTarget",flags);
        Assert.That((PlayerRef)findTarget.Invoke(third,null),Is.EqualTo(PlayerRef.None),
            "A third Fishcat must not reserve a player already pursued by two others.");
        targetProperty.SetValue(fishcats[1],PlayerRef.None);
        Assert.That((PlayerRef)findTarget.Invoke(third,null),Is.EqualTo(runner.LocalPlayer),
            "Releasing one pursuer must make a visible player eligible again.");
        int capBeforeDown=match.PurgeMinionCap;
        typeof(NetworkPlayerLifeState).GetProperty("Status").SetValue(player.GetComponent<NetworkPlayerLifeState>(),NetworkPlayerLifeStatus.Downed);
        Assert.That(match.PurgeMinionCap,Is.EqualTo(capBeforeDown),"Downed players must remain in party scaling.");
        typeof(NetworkPlayerLifeState).GetProperty("Status").SetValue(player.GetComponent<NetworkPlayerLifeState>(),NetworkPlayerLifeStatus.Alive);
        runner.Despawn(thirdFish);
        foreach(var fishcat in fishcats)runner.Despawn(fishcat.Object); // Isolate the following boss UV checks.
        var uv=player.GetComponent<NetworkPlayerFlashlight>();
        var bossCollider=boss.GetComponentInChildren<Collider>();
        var origin=player.transform.position+Vector3.up*1.6f;
        var direction=(bossCollider.bounds.center-origin).normalized;
        var angles=Quaternion.LookRotation(direction).eulerAngles;
        var input=new NetworkPlayerInput{LookYaw=angles.y,LookPitch=Mathf.DeltaAngle(0,angles.x)};
        Physics.SyncTransforms();
        var resolveUv=typeof(NetworkPlayerFlashlight).GetMethod("ResolveResonanceTarget",flags);
        resolveUv.Invoke(uv,new object[]{input});
        Assert.That(uv.PurgeBossTarget,Is.EqualTo(boss.GetComponent<StalkerFusionRuntime>()),
            "UV origin="+origin+" muzzle="+uv.BeamTransform.position+" boss="+bossCollider.bounds.center
            +" hits="+string.Join(",",System.Array.ConvertAll(Physics.SphereCastAll(origin,.75f,direction,24,~0,QueryTriggerInteraction.Ignore),h=>h.collider.name)));
        // The presentation signal follows live contact, not the exposure that lingers after release.
        Set(match,"PurgeMood",PurgeBossMood.Hunting);
        Set(match,"PurgeUvImmunity",TickTimer.None);
        float speedBeforeUv=match.PurgeChaseSpeed;
        typeof(NetworkPlayerFlashlight).GetProperty("ResonanceActive").SetValue(uv,(NetworkBool)true);
        var tickPurge=typeof(NetworkMatchState).GetMethod("TickPurgeDirector",flags);
        tickPurge.Invoke(match,null);
        Assert.That((bool)match.PurgeUvContact,Is.True);
        Assert.That(match.PurgeUvExposure,Is.GreaterThan(0f));
        Assert.That(match.PurgeChaseSpeed,Is.EqualTo(speedBeforeUv*.6f).Within(.01f));
        var presenter=boss.GetComponent<EchoProtocol.AI.Stalker.Presentation.StalkerAnimatorPresenter>();
        var animator=System.Array.Find(boss.GetComponentsInChildren<Animator>(true),a=>a.enabled);
        var updatePresentation=typeof(EchoProtocol.AI.Stalker.Presentation.StalkerAnimatorPresenter).GetMethod("Update",flags);
        Assert.That(presenter,Is.Not.Null);
        Assert.That(animator,Is.Not.Null);
        updatePresentation.Invoke(presenter,null);
        animator.Update(.2f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).fullPathHash,
            Is.EqualTo(Animator.StringToHash("Base Layer.PurgeHit")));
        Set(match,"PurgeUvExposure",1f);
        typeof(NetworkPlayerFlashlight).GetProperty("ResonanceActive").SetValue(uv,(NetworkBool)false);
        tickPurge.Invoke(match,null);
        Assert.That((bool)match.PurgeUvContact,Is.False,"UV hit animation must end on beam release.");
        Assert.That(match.PurgeUvExposure,Is.GreaterThan(0f),"Exposure may remain briefly without holding the hit animation.");
        Assert.That(match.PurgeChaseSpeed,Is.EqualTo(speedBeforeUv).Within(.01f));
        updatePresentation.Invoke(presenter,null);
        animator.Update(.2f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).fullPathHash,
            Is.Not.EqualTo(Animator.StringToHash("Base Layer.PurgeHit")));
        Set(match,"PurgeUvExposure",2.24f);
        typeof(NetworkPlayerFlashlight).GetProperty("ResonanceActive").SetValue(uv,(NetworkBool)true);
        tickPurge.Invoke(match,null);
        Assert.That(match.PurgeMood,Is.EqualTo(PurgeBossMood.Staggered));
        Assert.That((bool)match.PurgeUvContact,Is.False);
        updatePresentation.Invoke(presenter,null);
        animator.Update(.2f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).fullPathHash,
            Is.EqualTo(Animator.StringToHash("Base Layer.PurgeStunned")));
        typeof(NetworkPlayerFlashlight).GetProperty("ResonanceActive").SetValue(uv,(NetworkBool)false);
        Set(match,"PurgeUvExposure",0f);
        Set(match,"PurgeMood",PurgeBossMood.Calm);
        updatePresentation.Invoke(presenter,null);
        animator.Update(.2f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).fullPathHash,
            Is.Not.EqualTo(Animator.StringToHash("Base Layer.PurgeStunned")));
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position=(origin+bossCollider.bounds.center)*.5f;
        wall.transform.localScale=Vector3.one;
        Physics.SyncTransforms();
        typeof(NetworkPlayerFlashlight).GetProperty("PurgeBossTarget").SetValue(uv,null);
        resolveUv.Invoke(uv,new object[]{input});
        Assert.That(uv.PurgeBossTarget,Is.Null,"UV must not affect a boss through a wall.");
        Object.DestroyImmediate(wall);
        var interactor=player.GetComponent<NetworkPlayerInteractor>();
        var selection=player.GetComponent<LobbyPlayerState>();
        Assert.That(match.PurgeCoopAidEnabled,Is.False);
        Assert.That(match.PurgeAidAvailable,Is.Zero);
        var special=boss.GetComponent<EchoProtocol.AI.Stalker.Special.StalkerSpecialEncounterRuntime>();
        var specialFlags=BindingFlags.Instance|BindingFlags.NonPublic;
        typeof(EchoProtocol.AI.Stalker.Special.StalkerSpecialEncounterRuntime).GetField("_phase",specialFlags)
            .SetValue(special,EchoProtocol.AI.Stalker.Special.StalkerSpecialEncounterPhase.HiddenTransfer);
        special.SuppressForPurge();
        Assert.That(special.IsActive,Is.False);
        Assert.That(special.PresentationVisible,Is.True);
        Set(match,"PurgeMood",PurgeBossMood.Warning);
        yield return new WaitForSeconds(.08f);
        Assert.That(controller.SpecialEncounterOverrideActive,Is.True,"Warning freeze must survive encounter suppression.");
        Set(match,"PurgeMood",PurgeBossMood.Calm);
        yield return new WaitForSeconds(.08f);
        Assert.That(interactor.CanStartFirstAidReviveAuthoritative(selection),Is.False,"No kit means no revival.");
        selection.SetGameplayToolId(LobbyPlayerState.FirstAidKitToolId);
        Assert.That(interactor.CanStartFirstAidReviveAuthoritative(selection),Is.True,"A living player with a kit can begin revival.");
        selection.SetGameplayToolId(0);
        // Catching a player ends this hunt, and the same catch cannot be applied a second time.
        player.transform.position=boss.transform.position;
        Set(match,"PurgeMood",PurgeBossMood.Hunting);
        typeof(StalkerController).GetField("currentState",flags).SetValue(controller,StalkerState.ATTACK);
        var runtime=boss.GetComponent<StalkerFusionRuntime>();
        var life=player.GetComponent<NetworkPlayerLifeState>();
        Assert.That((int)typeof(NetworkPlayerLifeState).GetProperty("MaximumRevivesPerZone",flags).GetValue(life),
            Is.EqualTo(int.MaxValue),"PURGE may revive repeatedly while still consuming a first-aid kit each time.");
        Assert.That(runtime.TryCatchPlayer(life),Is.True);
        Assert.That((bool)(NetworkBool)typeof(NetworkPlayerLifeState).GetProperty("CatchEndsInDeath",flags).GetValue(life),Is.False);
        Assert.That(match.PurgeMood,Is.EqualTo(PurgeBossMood.Calm));
        Assert.That(runtime.TryCatchPlayer(life),Is.False);
        // Ninth objective ends the match once; further callbacks cannot overcount it.
        match.RecordHuntBurn();match.RecordHuntBurn();
        Assert.That(match.HuntBurned,Is.EqualTo(9));Assert.That(match.IsEnded,Is.True);
        Assert.That(match.Result,Is.EqualTo(NetworkMatchResult.Win));
        var shutdown=runner.Shutdown();while(!shutdown.IsCompleted)yield return null;
    }

    public IEnumerator Cleanup()
    {
        if (runner && runner.IsRunning)
        {
            var task = runner.Shutdown();
            while (!task.IsCompleted) yield return null;
        }
        if (runner) Object.Destroy(runner.gameObject);
    }
}

namespace EchoProtocol.Networking.Tests
{
    public sealed class PurgeFishcatBalanceTests
    {
        [TestCase(1, MatchDifficulty.Easy, 1f)]
        [TestCase(1, MatchDifficulty.Normal, 1f)]
        [TestCase(1, MatchDifficulty.Hard, 1f)]
        [TestCase(2, MatchDifficulty.Easy, 1f)]
        [TestCase(2, MatchDifficulty.Normal, 1f)]
        [TestCase(4, MatchDifficulty.Hard, 1f)]
        public void FishcatUvThresholdIsOneSecond(int partySize, MatchDifficulty difficulty, float expected)
        {
            Assert.That(PurgeFishcatBalance.UvDeathSeconds(partySize, difficulty), Is.EqualTo(expected));
        }

        [Test]
        public void RefillAllowsOneSpawnThenWaitsAndFailedPlacementCanRetry()
        {
            var schedule = new PurgeFishcatRefillSchedule();
            Assert.That(schedule.IsDue(0f, 0, 3, 35f), Is.True);
            schedule.Spawned(0f);
            Assert.That(schedule.IsDue(0f, 1, 3, 35f), Is.False);
            Assert.That(schedule.IsDue(34.9f, 1, 3, 35f), Is.False);
            Assert.That(schedule.IsDue(35f, 1, 3, 35f), Is.True);
            Assert.That(schedule.IsDue(36f, 1, 3, 35f), Is.True, "Failed placement must not restart a full cooldown.");
            schedule.Spawned(36f);
            Assert.That(schedule.IsDue(36f, 2, 3, 35f), Is.False);
        }

        [Test]
        public void KillStartsCooldownAtLossAndTierGrowthDoesNotFillImmediately()
        {
            var schedule = new PurgeFishcatRefillSchedule();
            schedule.IsDue(0f, 0, 1, 35f);
            schedule.Spawned(0f);
            Assert.That(schedule.IsDue(100f, 1, 1, 35f), Is.False);
            Assert.That(schedule.IsDue(101f, 0, 1, 35f), Is.False);
            Assert.That(schedule.IsDue(135.9f, 0, 1, 35f), Is.False);
            Assert.That(schedule.IsDue(136f, 0, 1, 35f), Is.True);
            schedule.Spawned(136f);
            Assert.That(schedule.IsDue(136f, 1, 8, 16f), Is.False);
            Assert.That(schedule.IsDue(152f, 1, 8, 16f), Is.True);
            schedule.Spawned(152f);
            Assert.That(schedule.IsDue(152f, 2, 8, 16f), Is.False);
            schedule.Reset();
            Assert.That(schedule.IsDue(0f, 0, 1, 35f), Is.True);
        }

        [TestCase(0, 1, 1)] [TestCase(0, 2, 2)] [TestCase(0, 3, 2)] [TestCase(0, 4, 3)]
        [TestCase(3, 1, 2)] [TestCase(3, 2, 3)] [TestCase(3, 3, 4)] [TestCase(3, 4, 5)]
        [TestCase(6, 1, 3)] [TestCase(6, 2, 4)] [TestCase(6, 3, 5)] [TestCase(6, 4, 6)]
        [TestCase(8, 1, 3)] [TestCase(8, 2, 5)] [TestCase(8, 3, 6)] [TestCase(8, 4, 8)]
        public void NormalPopulationMatchesApprovedPartyAndProgressTable(int burned, int players, int expected)
        {
            Assert.That(PurgeFishcatBalance.Cap(burned, players, MatchDifficulty.Normal), Is.EqualTo(expected));
        }

        [TestCase(0, 35f)] [TestCase(2, 35f)] [TestCase(3, 28f)] [TestCase(5, 28f)]
        [TestCase(6, 22f)] [TestCase(7, 22f)] [TestCase(8, 16f)]
        public void RefillCooldownChangesOnlyAtApprovedProgressBoundaries(int burned, float expected)
        {
            Assert.That(PurgeFishcatBalance.SpawnCooldown(burned), Is.EqualTo(expected));
        }

        [Test]
        public void PopulationRemainsBoundedAndDifficultyNeverReversesPressure()
        {
            Assert.That(PurgeFishcatBalance.Cap(0, 0, MatchDifficulty.Normal), Is.EqualTo(1));
            Assert.That(PurgeFishcatBalance.Cap(8, 99, MatchDifficulty.Normal), Is.EqualTo(8));
            for (int players = 1; players <= 4; players++)
            {
                int previous = 0;
                for (int burned = 0; burned <= 8; burned++)
                {
                    int normal = PurgeFishcatBalance.Cap(burned, players, MatchDifficulty.Normal);
                    Assert.That(normal, Is.GreaterThanOrEqualTo(previous));
                    Assert.That(PurgeFishcatBalance.Cap(burned, players, MatchDifficulty.Easy), Is.InRange(1, normal));
                    Assert.That(PurgeFishcatBalance.Cap(burned, players, MatchDifficulty.Hard), Is.GreaterThanOrEqualTo(normal));
                    previous = normal;
                }
            }
        }
    }

}
