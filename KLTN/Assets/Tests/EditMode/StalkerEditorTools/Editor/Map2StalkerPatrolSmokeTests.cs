#if UNITY_EDITOR
using System.Collections;
using EchoProtocol.AI.Common;
using EchoProtocol.AI.Stalker;
using EchoProtocol.AI.Stalker.Spatial;
using EchoProtocol.AI.Stalker.Spatial.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace EchoProtocol.AI.Stalker.EditorTools.Tests
{
    public sealed class Map2StalkerPatrolSmokeTests
    {
        private MatchFlowPlayModeSettings playModeSettings;

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            try
            {
                if (Application.isPlaying) yield return new ExitPlayMode();
            }
            finally
            {
                playModeSettings?.Restore();
                playModeSettings = null;
            }
        }

        [UnityTest]
        public IEnumerator Map2_LizardPrefabPatrolMovesOnSceneNavMesh() =>
            PatrolMoves("Assets/Scenes/Scene_A.unity", Map2RegionGraphAuthoring.AssetPath);

        [UnityTest]
        public IEnumerator Map2_HuntLizardPatrolMovesOnItsOwnSceneNavMesh() =>
            PatrolMoves("Assets/Scenes/Scene_B.unity", Map2RegionGraphAuthoring.HuntAssetPath);

        [UnityTest]
        public IEnumerator Map2_LizardPatrolMovesFromReportedStuckLocation() =>
            PatrolMoves("Assets/Scenes/Scene_A.unity", Map2RegionGraphAuthoring.AssetPath,
                new Vector3(513.495f, 16.781f, 423.165f));

        [UnityTest]
        public IEnumerator Map2_LizardFallbackReturnsFromReportedIsolatedStart() =>
            PatrolMoves("Assets/Scenes/Scene_A.unity", Map2RegionGraphAuthoring.AssetPath,
                new Vector3(490.142f, 16.844f, 544.636f));

        private IEnumerator PatrolMoves(string scenePath, string graphPath, Vector3? startPosition = null)
        {
            playModeSettings = new MatchFlowPlayModeSettings();
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            yield return new EnterPlayMode(false);

            var water = GameObject.Find("WaterPlane 2").GetComponent<Renderer>().bounds;
            foreach (var vertex in NavMesh.CalculateTriangulation().vertices)
                if (water.Contains(new Vector3(vertex.x, water.center.y, vertex.z)))
                    Assert.That(vertex.y, Is.GreaterThan(water.max.y), "Map2 NavMesh contains submerged ground.");
            var marker = GameObject.Find("MonsterSpawn_Stalker_EMPTY").transform;
            Assert.That(NavMesh.SamplePosition(startPosition ?? marker.position, out var spawn, 1f, NavMesh.AllAreas), Is.True);
            var asset = AssetDatabase.LoadAssetAtPath<RegionGraphAsset>(graphPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/StalkerNetwork_Lizard.prefab");
            var holder = new GameObject("Map2 patrol smoke test");
            holder.SetActive(false);
            GameObject instance;
            if (startPosition.HasValue)
            {
                // Exercise the controller at the captured position without starting an unspawned Fusion object.
                instance = new GameObject("Reported stuck patrol fixture");
                instance.transform.SetParent(holder.transform);
                instance.transform.SetPositionAndRotation(spawn.position, marker.rotation);
                EditorUtility.CopySerialized(prefab.GetComponent<NavMeshAgent>(), instance.AddComponent<NavMeshAgent>());
                EditorUtility.CopySerialized(prefab.GetComponent<StalkerVisionSensor>(), instance.AddComponent<StalkerVisionSensor>());
                EditorUtility.CopySerialized(prefab.GetComponent<StalkerController>(), instance.AddComponent<StalkerController>());
                EditorUtility.CopySerialized(prefab.GetComponent<CapsuleCollider>(), instance.AddComponent<CapsuleCollider>());
            }
            else instance = Object.Instantiate(prefab, spawn.position, marker.rotation, holder.transform);
            foreach (var behaviour in instance.GetComponentsInChildren<Fusion.NetworkBehaviour>(true)) behaviour.enabled = false;
            var controller = instance.GetComponent<StalkerController>();
            controller.ConfigureRegionGraph(asset, RegionSemanticZone.Zone01);
            controller.SuppressLegacyUpdateSimulation = true;
            holder.SetActive(true);
            var agent = instance.GetComponent<NavMeshAgent>();
            // Fusion enables the prefab's initially disabled agent only on the Host.
            agent.enabled = true;
            controller.SetAuthoritativeLocomotion(true);
            Assert.That(agent.isOnNavMesh, Is.True);
            var start = instance.transform.position;

            const float delta = 1f / 30f;
            for (var tick = 0; tick < 450 && (Vector3.Distance(start, instance.transform.position) < 2f
                || controller.RegionGraphFallbackReason != RegionGraphFallbackReason.None); tick++)
            {
                Assert.That(controller.Simulate(new StalkerSimulationInput(
                    new AiSimulationStep(new AiSimulationTime(tick, tick * delta), delta), null)), Is.True);
                controller.TickAuthoritativeLocomotion(delta);
                Assert.That(instance.transform.position.y, Is.GreaterThan(water.max.y), "Stalker entered the water.");
                yield return null;
            }
            Assert.That(controller.CurrentState, Is.EqualTo(StalkerState.PATROL));
            Assert.That(controller.RegionGraphFallbackReason, Is.EqualTo(RegionGraphFallbackReason.None));
            Assert.That(Vector3.Distance(start, instance.transform.position), Is.GreaterThanOrEqualTo(2f),
                $"state={controller.CurrentState}, graph={controller.RegionGraphFallbackReason}, navigation={controller.NavigationFailureReason}");
            Object.Destroy(holder);
            yield return new ExitPlayMode();
        }
    }
}
#endif
