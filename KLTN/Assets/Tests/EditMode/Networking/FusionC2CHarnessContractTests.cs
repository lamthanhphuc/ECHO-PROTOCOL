using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Networking.Tests
{
    public sealed class FusionC2CHarnessContractTests
    {
        private const string HarnessScenePath = "Assets/Scenes/Networking/FND003C2CHarness.unity";
        private const string RunnerPrefabPath = "Assets/Prefabs/NetworkRunner.prefab";
        private const string PlayerPrefabPath = "Assets/Prefabs/PlayerNetwork.prefab";
        private const string ControllerTypeName = "EchoProtocol.Networking.Diagnostics.FusionC2CHarnessController";
        private const string ProbeTypeName = "EchoProtocol.Networking.Diagnostics.FusionPlayerLifecycleProbe";
        private const string LifecycleTypeName = "EchoProtocol.Networking.FusionPlayerLifecycle";
        private const string NetworkRunnerTypeName = "Fusion.NetworkRunner";

        [Test]
        public void FND_C2C_HARNESS_Controller_DefaultSession_IsDeterministic()
        {
            var controllerType = ResolveType(ControllerTypeName);
            var defaultSession = controllerType.GetField("DefaultSessionName", BindingFlags.Public | BindingFlags.Static);
            Assert.That(defaultSession, Is.Not.Null);
            Assert.That(defaultSession.GetValue(null), Is.EqualTo("EchoProtocol-FND003C2C"));

            var resolved = InvokeResolveSessionName(controllerType, " ");
            Assert.That(resolved, Is.EqualTo("EchoProtocol-FND003C2C"));
        }

        [Test]
        public void FND_C2C_HARNESS_Controller_ParsesHostAndClientModes()
        {
            var controllerType = ResolveType(ControllerTypeName);
            Assert.That(InvokeTryParseMode(controllerType, "host", out var hostMode), Is.True);
            Assert.That(hostMode.ToString(), Is.EqualTo("Host"));

            Assert.That(InvokeTryParseMode(controllerType, "client", out var clientMode), Is.True);
            Assert.That(clientMode.ToString(), Is.EqualTo("Client"));

            Assert.That(InvokeTryParseMode(controllerType, "shared", out _), Is.False);
        }

        [Test]
        public void FND_C2C_HARNESS_Scene_UsesProductionRunnerPrefab()
        {
            var scene = OpenHarnessScene();
            try
            {
                var controller = FindSingleComponent(scene, ControllerTypeName);
                Assert.That(controller, Is.Not.Null);

                var serializedController = new SerializedObject(controller);
                var runnerPrefab = serializedController.FindProperty("runnerPrefab").objectReferenceValue;
                Assert.That(runnerPrefab, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(runnerPrefab), Is.EqualTo(RunnerPrefabPath));

                Assert.That(FindComponents(scene, NetworkRunnerTypeName), Is.EqualTo(0));
                Assert.That(ContainsRootNamed(scene, "PlayerNetwork"), Is.False);
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath), Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void FND_C2C_HARNESS_Probe_IsReadOnlyProductionLifecycleObserver()
        {
            var scene = OpenHarnessScene();
            try
            {
                Assert.That(FindComponents(scene, ProbeTypeName), Is.EqualTo(1));
                Assert.That(FindComponents(scene, ControllerTypeName), Is.EqualTo(1));

                var runnerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RunnerPrefabPath);
                Assert.That(runnerPrefab, Is.Not.Null);
                Assert.That(GetComponentByTypeName(runnerPrefab, LifecycleTypeName), Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Scene OpenHarnessScene()
        {
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(HarnessScenePath),
                Is.Not.Null,
                "Create the C2C harness scene via Tools/ECHO Protocol/C2C/Create Harness Scene before running this test.");
            return EditorSceneManager.OpenScene(HarnessScenePath, OpenSceneMode.Additive);
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

        private static Component GetComponentByTypeName(GameObject root, string fullTypeName)
        {
            var components = root.GetComponents<Component>();
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

        private static string InvokeResolveSessionName(Type controllerType, string value)
        {
            var method = controllerType.GetMethod("ResolveSessionName", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return (string)method.Invoke(null, new object[] { value });
        }

        private static bool InvokeTryParseMode(Type controllerType, string value, out object parsedMode)
        {
            var method = controllerType.GetMethod("TryParseMode", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            var args = new object[] { value, null };
            var result = (bool)method.Invoke(null, args);
            parsedMode = args[1];
            return result;
        }

        private static Type ResolveType(string fullTypeName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                var type = assemblies[i].GetType(fullTypeName, false);
                if (type != null)
                {
                    return type;
                }
            }

            Assert.Fail($"Could not find type '{fullTypeName}' in the loaded Unity AppDomain.");
            return null;
        }
    }
}
