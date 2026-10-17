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
        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator Map2_SharedStalkerPrefabPatrolMovesOnSceneNavMesh()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Scene_A.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();

            var water = GameObject.Find("WaterPlane 2").GetComponent<Renderer>().bounds;
            foreach (var vertex in NavMesh.CalculateTriangulation().vertices)
                if (water.Contains(new Vector3(vertex.x, water.center.y, vertex.z)))
                    Assert.That(vertex.y, Is.GreaterThan(water.max.y), "Map2 NavMesh contains submerged ground.");
            var marker = GameObject.Find("MonsterSpawn_Stalker_EMPTY").transform;
            Assert.That(NavMesh.SamplePosition(marker.position, out var spawn, 1f, NavMesh.AllAreas), Is.True);
            var asset = AssetDatabase.LoadAssetAtPath<RegionGraphAsset>(Map2RegionGraphAuthoring.AssetPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/StalkerNetwork.prefab");
            var holder = new GameObject("Map2 patrol smoke test");
            holder.SetActive(false);
            var instance = Object.Instantiate(prefab, spawn.position, marker.rotation, holder.transform);
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
            for (var tick = 0; tick < 450 && Vector3.Distance(start, instance.transform.position) < 2f; tick++)
            {
                Assert.That(controller.Simulate(new StalkerSimulationInput(
                    new AiSimulationStep(new AiSimulationTime(tick, tick * delta), delta), null)), Is.True);
                controller.TickAuthoritativeLocomotion(delta);
                Assert.That(instance.transform.position.y, Is.GreaterThan(water.max.y), "Stalker entered the water.");
                yield return null;
            }
            Assert.That(controller.CurrentState, Is.EqualTo(StalkerState.PATROL));
            Assert.That(controller.RegionGraphFallbackReason, Is.Not.EqualTo(RegionGraphFallbackReason.SpatialGraphCompatibilityMismatch));
            Assert.That(Vector3.Distance(start, instance.transform.position), Is.GreaterThanOrEqualTo(2f),
                $"state={controller.CurrentState}, graph={controller.RegionGraphFallbackReason}, navigation={controller.NavigationFailureReason}");
            Object.Destroy(holder);
            yield return new ExitPlayMode();
        }
    }
}
#endif
