using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Networking.Tests
{
    public sealed class STK002FusionHarnessContractTests
    {
        private const string HarnessScenePath = "Assets/Scenes/Networking/STK002FusionHarness.unity";
        private const string FndHarnessScenePath = "Assets/Scenes/Networking/FND003C2CHarness.unity";
        private const string RunnerPrefabPath = "Assets/Prefabs/NetworkRunner.prefab";
        private const string StalkerPrefabPath = "Assets/Prefabs/StalkerNetwork.prefab";
        private const string ProbeScriptPath = "Assets/Scripts/AI/Stalker/Networking/Diagnostics/StalkerFusionC2CHarnessProbe.cs";
        private const string ControllerTypeName = "EchoProtocol.Networking.Diagnostics.FusionC2CHarnessController";
        private const string LifecycleProbeTypeName = "EchoProtocol.Networking.Diagnostics.FusionPlayerLifecycleProbe";
        private const string StalkerProbeTypeName = "EchoProtocol.AI.Stalker.Networking.Diagnostics.StalkerFusionC2CHarnessProbe";
        private const string LifecycleTypeName = "EchoProtocol.Networking.FusionPlayerLifecycle";
        private const string NavMeshSurfaceTypeName = "Unity.AI.Navigation.NavMeshSurface";
        private const string NetworkObjectTypeName = "Fusion.NetworkObject";
        private const string StalkerControllerTypeName = "EchoProtocol.AI.Stalker.StalkerController";
        private const string StalkerVisionSensorTypeName = "EchoProtocol.AI.Stalker.StalkerVisionSensor";
        private const string StalkerFusionRuntimeTypeName = "EchoProtocol.AI.Stalker.Networking.StalkerFusionRuntime";

        [Test]
        public void HARNESS_01_STK002HarnessSceneExists()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(HarnessScenePath), Is.Not.Null);
        }

        [Test]
        public void HARNESS_02_SceneReferencesProductionNetworkRunnerPrefabThroughC2CController()
        {
            var scene = OpenScene(HarnessScenePath);
            try
            {
                var controller = FindSingleComponent(scene, ControllerTypeName);
                Assert.That(controller, Is.Not.Null);

                var serializedController = new SerializedObject(controller);
                var runnerPrefab = serializedController.FindProperty("runnerPrefab").objectReferenceValue;
                Assert.That(runnerPrefab, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(runnerPrefab), Is.EqualTo(RunnerPrefabPath));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void HARNESS_03_SceneHasExactlyOneStalkerPhaseTwoHarnessProbe()
        {
            var scene = OpenScene(HarnessScenePath);
            try
            {
                Assert.That(FindComponents(scene, StalkerProbeTypeName), Is.EqualTo(1));
                Assert.That(FindComponents(scene, ControllerTypeName), Is.EqualTo(1));
                Assert.That(FindComponents(scene, LifecycleProbeTypeName), Is.EqualTo(1));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void HARNESS_04_StalkerNetworkPrefabExists()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(StalkerPrefabPath), Is.Not.Null);
        }

        [Test]
        public void HARNESS_05_StalkerNetworkPrefabContainsRequiredRuntimeComponents()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StalkerPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            Assert.That(GetComponentByTypeName(prefab, NetworkObjectTypeName), Is.Not.Null);
            Assert.That(GetComponentByTypeName(prefab, StalkerControllerTypeName), Is.Not.Null);
            Assert.That(GetComponentByTypeName(prefab, StalkerVisionSensorTypeName), Is.Not.Null);
            Assert.That(GetComponentByTypeName(prefab, StalkerFusionRuntimeTypeName), Is.Not.Null);
        }

        [Test]
        public void HARNESS_06_StalkerRuntimeUsesRunnerLifecycleWithoutDuplicateLifecycleOnStalker()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StalkerPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(GetComponentByTypeName(prefab, LifecycleTypeName), Is.Null);

            var runnerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RunnerPrefabPath);
            Assert.That(runnerPrefab, Is.Not.Null);
            Assert.That(GetComponentByTypeName(runnerPrefab, LifecycleTypeName), Is.Not.Null);

            var runtime = GetComponentByTypeName(prefab, StalkerFusionRuntimeTypeName);
            Assert.That(runtime, Is.Not.Null);
            var serializedRuntime = new SerializedObject(runtime);
            Assert.That(serializedRuntime.FindProperty("lifecycle").objectReferenceValue, Is.Null);
        }

        [Test]
        public void HARNESS_07_ExistingFND003C2CHarnessSceneContractRemainsValid()
        {
            var scene = OpenScene(FndHarnessScenePath);
            try
            {
                var controller = FindSingleComponent(scene, ControllerTypeName);
                Assert.That(controller, Is.Not.Null);

                var serializedController = new SerializedObject(controller);
                var runnerPrefab = serializedController.FindProperty("runnerPrefab").objectReferenceValue;
                Assert.That(runnerPrefab, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(runnerPrefab), Is.EqualTo(RunnerPrefabPath));
                Assert.That(FindComponents(scene, StalkerProbeTypeName), Is.EqualTo(0));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void HARNESS_08_StalkerPrefabVisionOriginHierarchyReferenceIsValid()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StalkerPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var sensor = GetComponentByTypeName(prefab, StalkerVisionSensorTypeName);
            Assert.That(sensor, Is.Not.Null);

            var serializedSensor = new SerializedObject(sensor);
            var origin = serializedSensor.FindProperty("visionOrigin").objectReferenceValue as Transform;
            Assert.That(origin, Is.Not.Null);
            Assert.That(origin == prefab.transform || origin.IsChildOf(prefab.transform), Is.True);
        }

        [Test]
        public void HARNESS_09_NoCheatOccluderAndTestMarkersExist()
        {
            var scene = OpenScene(HarnessScenePath);
            try
            {
                Assert.That(ContainsRootNamed(scene, "STK002_StalkerSpawn"), Is.True);
                Assert.That(ContainsRootNamed(scene, "STK002_PlayerVisibleMarker"), Is.True);
                Assert.That(ContainsRootNamed(scene, "STK002_PlayerHiddenMarker"), Is.True);
                Assert.That(ContainsRootNamed(scene, "STK002_NoCheatOccluder"), Is.True);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void HARNESS_10_STK002HarnessNavMeshBootstrapSurfaceContractExists()
        {
            var scene = OpenScene(HarnessScenePath);
            try
            {
                var surface = FindSingleComponent(scene, NavMeshSurfaceTypeName);
                Assert.That(surface, Is.Not.Null);

                var probe = FindSingleComponent(scene, StalkerProbeTypeName);
                Assert.That(probe, Is.Not.Null);
                var serializedProbe = new SerializedObject(probe);
                Assert.That(serializedProbe.FindProperty("navMeshSurface").objectReferenceValue, Is.EqualTo(surface));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void HARNESS_11_NoCheatHiddenMarkerIsBehindOccluderOnStalkerLine()
        {
            var scene = OpenScene(HarnessScenePath);
            try
            {
                AssertVectorNear(FindRootTransform(scene, "STK002_StalkerSpawn").position, new Vector3(0f, 0f, -4f));
                AssertVectorNear(FindRootTransform(scene, "STK002_PlayerVisibleMarker").position, new Vector3(0f, 1f, 4f));
                AssertVectorNear(FindRootTransform(scene, "STK002_PlayerHiddenMarker").position, new Vector3(0f, 1f, 6f));

                var occluder = FindRootTransform(scene, "STK002_NoCheatOccluder");
                AssertVectorNear(occluder.position, new Vector3(0f, 1f, 0f));
                AssertVectorNear(occluder.localScale, new Vector3(3f, 2f, 0.25f));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void HARNESS_12_NoCheatDiagnosticsIncludeDelayedHiddenVerificationEvidence()
        {
            Assert.That(File.Exists(ProbeScriptPath), Is.True);
            var source = File.ReadAllText(ProbeScriptPath);

            StringAssert.Contains("WaitForHiddenVerification", source);
            StringAssert.Contains("RequiredHiddenVerificationSimulationDelta", source);
            StringAssert.Contains("stage=HiddenVerified", source);
            StringAssert.Contains("result=", source);
            StringAssert.Contains("NetworkTransform", source);
            StringAssert.Contains("networkTransform.Teleport(position);", source);
            StringAssert.Contains("networkObject.HasStateAuthority", source);
            StringAssert.DoesNotContain("identity.EntityRoot.position = visiblePlayerMarker.position", source);
            StringAssert.DoesNotContain("identity.EntityRoot.position = hiddenPlayerMarker.position", source);
            StringAssert.Contains("baselineLastKnown=", source);
            StringAssert.Contains("moved=", source);
            StringAssert.Contains("reachedHidden=", source);
            StringAssert.Contains("frozen=", source);
            StringAssert.Contains("simDelta=", source);
            StringAssert.Contains("simDelta >= RequiredHiddenVerificationSimulationDelta", source);
        }

        [Test]
        public void HARNESS_13_NoCheatMaintainsPositionedPlayerIdContinuity()
        {
            Assert.That(File.Exists(ProbeScriptPath), Is.True);
            var source = File.ReadAllText(ProbeScriptPath);

            StringAssert.Contains("_noCheatPositionedPlayerId", source);
            StringAssert.Contains("controllerComponent.CurrentTargetId != _noCheatPositionedPlayerId", source);
            StringAssert.Contains("TryResolveHostTargetIdentity(runner, controllerComponent.CurrentTargetId", source);
            StringAssert.Contains("Vector3.Distance(targetPosition, lastKnown)", source);
        }

        [Test]
        public void HARNESS_14_NavMeshBootstrapValidatesConfiguredSpawnPoint()
        {
            Assert.That(File.Exists(ProbeScriptPath), Is.True);
            var source = File.ReadAllText(ProbeScriptPath);

            StringAssert.Contains("private const float NavMeshSpawnSampleRadius = 0.10f;", source);
            StringAssert.Contains("NavMesh.SamplePosition", source);
            StringAssert.Contains("NavMesh.SamplePosition(stalkerSpawnPoint.position, out var hit, NavMeshSpawnSampleRadius, NavMesh.AllAreas)", source);
            StringAssert.Contains("SpawnPointNotOnNavMesh", source);
            StringAssert.Contains("_navMeshSpawnPosition = hit.position;", source);
            StringAssert.Contains("_navMeshReady ? _navMeshSpawnPosition", source);
            StringAssert.Contains("runner.Spawn(stalkerPrefab, position, rotation, PlayerRef.None)", source);
        }

        [Test]
        public void HARNESS_15_StalkerRuntimeCacheRequiresSameRunner()
        {
            Assert.That(File.Exists(ProbeScriptPath), Is.True);
            var source = File.ReadAllText(ProbeScriptPath);

            StringAssert.Contains("RuntimeBelongsToRunner", source);
            StringAssert.Contains("runtime.Runner == runner", source);
            StringAssert.Contains("RuntimeBelongsToRunner(_cachedRuntime, runner)", source);
            StringAssert.Contains("RuntimeBelongsToRunner(found[i], runner)", source);
        }

        private static Scene OpenScene(string path)
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null);
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        }

        private static Component FindSingleComponent(Scene scene, string fullTypeName)
        {
            Component found = null;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var components = roots[i].GetComponentsInChildren<Component>(true);
                for (var j = 0; j < components.Length; j++)
                {
                    var component = components[j];
                    if (component == null || component.GetType().FullName != fullTypeName)
                    {
                        continue;
                    }

                    Assert.That(found, Is.Null, $"Expected exactly one component of type {fullTypeName}.");
                    found = component;
                }
            }

            return found;
        }

        private static int FindComponents(Scene scene, string fullTypeName)
        {
            var count = 0;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var components = roots[i].GetComponentsInChildren<Component>(true);
                for (var j = 0; j < components.Length; j++)
                {
                    var component = components[j];
                    if (component != null && component.GetType().FullName == fullTypeName)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static Component GetComponentByTypeName(GameObject root, string fullTypeName)
        {
            var components = root.GetComponentsInChildren<Component>(true);
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component != null && component.GetType().FullName == fullTypeName)
                {
                    return component;
                }
            }

            return null;
        }

        private static Transform FindRootTransform(Scene scene, string rootName)
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == rootName)
                {
                    return roots[i].transform;
                }
            }

            Assert.Fail($"Expected root object named {rootName}.");
            return null;
        }

        private static void AssertVectorNear(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.001f), $"Expected {expected}, got {actual}.");
        }

        private static bool ContainsRootNamed(Scene scene, string rootName)
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == rootName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
