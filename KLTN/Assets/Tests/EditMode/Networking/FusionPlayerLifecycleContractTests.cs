using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EchoProtocol.Networking.Tests
{
    public sealed class FusionPlayerLifecycleContractTests
    {
        private const string RunnerPrefabPath = "Assets/Prefabs/NetworkRunner.prefab";
        private const string PlayerPrefabPath = "Assets/Prefabs/PlayerNetwork.prefab";
        private const string NetworkRunnerTypeName = "Fusion.NetworkRunner";
        private const string LifecycleTypeName = "EchoProtocol.Networking.FusionPlayerLifecycle";
        private const string NetworkObjectTypeName = "Fusion.NetworkObject";

        [Test]
        public void FND_NET_LIFECYCLE_RunnerPrefab_HasLifecycleComponent()
        {
            var runnerPrefab = LoadPrefab(RunnerPrefabPath);

            Assert.That(GetComponentByTypeName(runnerPrefab, NetworkRunnerTypeName), Is.Not.Null);
            Assert.That(GetComponentByTypeName(runnerPrefab, LifecycleTypeName), Is.Not.Null);
        }

        [Test]
        public void FND_NET_LIFECYCLE_PlayerPrefabReference_IsPlayerNetwork()
        {
            var runnerPrefab = LoadPrefab(RunnerPrefabPath);
            var playerPrefab = LoadPrefab(PlayerPrefabPath);
            var lifecycle = GetComponentByTypeName(runnerPrefab, LifecycleTypeName);
            Assert.That(lifecycle, Is.Not.Null);

            var serializedLifecycle = new SerializedObject(lifecycle);
            var playerPrefabProperty = serializedLifecycle.FindProperty("playerPrefab");
            Assert.That(playerPrefabProperty, Is.Not.Null);
            Assert.That(playerPrefabProperty.objectReferenceValue, Is.SameAs(GetComponentByTypeName(playerPrefab, NetworkObjectTypeName)));
        }

        [Test]
        public void FND_NET_LIFECYCLE_Registries_StartEmpty()
        {
            var runnerPrefab = LoadPrefab(RunnerPrefabPath);
            var lifecycle = GetComponentByTypeName(runnerPrefab, LifecycleTypeName);
            Assert.That(lifecycle, Is.Not.Null);

            Assert.That(GetIntProperty(GetProperty(lifecycle, "IdentityRegistry"), "Count"), Is.EqualTo(0));
            Assert.That(GetIntProperty(GetProperty(lifecycle, "EntityRegistry"), "Count"), Is.EqualTo(0));
        }

        private static GameObject LoadPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, $"Missing prefab at {path}.");
            return prefab;
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

        private static object GetProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName);
            Assert.That(property, Is.Not.Null, $"Missing public property '{propertyName}' on '{target.GetType().FullName}'.");
            return property.GetValue(target);
        }

        private static int GetIntProperty(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.TypeOf<int>(), $"Property '{propertyName}' must return int.");
            return (int)value;
        }

    }
}
