using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EchoProtocol.Player.Tests
{
    public sealed class PlayerNetworkPrefabContractTests
    {
        private const string PrefabPath = "Assets/Prefabs/PlayerNetwork.prefab";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string RootName = "PlayerNetwork";
        private const string NetworkObjectTypeName = "Fusion.NetworkObject";
        private const string NetworkTransformTypeName = "Fusion.NetworkTransform";
        private const string NetworkCharacterControllerTypeName = "Fusion.NetworkCharacterController";
        private const string LobbyPlayerStateTypeName = "EchoProtocol.Networking.LobbyPlayerState";
        private const string NetworkPlayerMovementTypeName = "EchoProtocol.Networking.NetworkPlayerMovement";
        private const string NetworkPlayerInteractorTypeName = "EchoProtocol.Networking.NetworkPlayerInteractor";
        private const string RuntimeIdentityTypeName = "EchoProtocol.Player.PlayerRuntimeIdentity";
        private const string PlayerMovementTypeName = "PlayerMovement";
        private const string PlayerCameraTypeName = "PlayerCamera";
        private const string StalkerNamespacePrefix = "EchoProtocol.AI.Stalker";

        [Test]
        public void FND_PLAYER_NET_PREFAB_RequiredRuntimeComponents_ArePresent()
        {
            var prefab = LoadPrefab();

            Assert.That(prefab.name, Is.EqualTo(RootName));
            Assert.That(GetComponentByTypeName(prefab, NetworkObjectTypeName), Is.Not.Null);
            Assert.That(GetComponentByTypeName(prefab, NetworkCharacterControllerTypeName), Is.Not.Null);
            Assert.That(GetComponentByTypeName(prefab, RuntimeIdentityTypeName), Is.Not.Null);
            Assert.That(GetComponentByTypeName(prefab, LobbyPlayerStateTypeName), Is.Not.Null);
            Assert.That(GetComponentByTypeName(prefab, NetworkPlayerMovementTypeName), Is.Not.Null);
            Assert.That(GetComponentByTypeName(prefab, NetworkPlayerInteractorTypeName), Is.Not.Null);
            Assert.That(prefab.GetComponent<CharacterController>(), Is.Not.Null);
        }

        [Test]
        public void FND_PLAYER_NET_PREFAB_NetworkCharacterControllerIsSoleReplicatedTransformProvider()
        {
            var prefab = LoadPrefab();

            Assert.That(GetComponentByTypeName(prefab, NetworkCharacterControllerTypeName), Is.Not.Null);
            Assert.That(GetComponentByTypeName(prefab, NetworkTransformTypeName), Is.Null);
        }

        [Test]
        public void FND_PLAYER_NET_PREFAB_NetworkedBehavioursIncludeGameplayComponents()
        {
            var prefab = LoadPrefab();
            var networkObject = GetComponentByTypeName(prefab, NetworkObjectTypeName);
            Assert.That(networkObject, Is.Not.Null);

            var serializedObject = new SerializedObject(networkObject);
            var behaviours = serializedObject.FindProperty("NetworkedBehaviours");
            Assert.That(behaviours, Is.Not.Null);

            Assert.That(SerializedReferenceArrayContains(behaviours, GetComponentByTypeName(prefab, LobbyPlayerStateTypeName)), Is.True);
            Assert.That(SerializedReferenceArrayContains(behaviours, GetComponentByTypeName(prefab, NetworkCharacterControllerTypeName)), Is.True);
            Assert.That(SerializedReferenceArrayContains(behaviours, GetComponentByTypeName(prefab, NetworkPlayerMovementTypeName)), Is.True);
            Assert.That(SerializedReferenceArrayContains(behaviours, GetComponentByTypeName(prefab, NetworkPlayerInteractorTypeName)), Is.True);
        }

        [Test]
        public void FND_PLAYER_NET_PREFAB_GameplayComponentsUseCanonicalInputActions()
        {
            var prefab = LoadPrefab();
            var inputActions = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(InputActionsPath);
            Assert.That(inputActions, Is.Not.Null);

            Assert.That(GetSerializedObjectReference(
                GetComponentByTypeName(prefab, NetworkPlayerMovementTypeName),
                "_inputActions"), Is.SameAs(inputActions));
            Assert.That(GetSerializedObjectReference(
                GetComponentByTypeName(prefab, NetworkPlayerInteractorTypeName),
                "_inputActions"), Is.SameAs(inputActions));
        }

        [Test]
        public void FND_PLAYER_NET_PREFAB_IdentityStartsUnboundAndUsesVisionTargetPoint()
        {
            var prefab = LoadPrefab();
            var identity = GetComponentByTypeName(prefab, RuntimeIdentityTypeName);
            Assert.That(identity, Is.Not.Null);

            Assert.That(GetBoolProperty(identity, "IsBound"), Is.False);
            Assert.That(GetBoolProperty(GetProperty(identity, "PlayerId"), "IsValid"), Is.False);

            var visionTargetPoint = prefab.transform.Find("VisionTargetPoint");
            Assert.That(visionTargetPoint, Is.Not.Null);
            Assert.That(visionTargetPoint.IsChildOf(prefab.transform), Is.True);
            Assert.That(GetTransformProperty(identity, "VisionTargetPoint"), Is.SameAs(visionTargetPoint));
            Assert.That(GetTransformProperty(identity, "EntityRoot"), Is.SameAs(prefab.transform));
        }

        [Test]
        public void FND_PLAYER_NET_PREFAB_ContainsNoLocalMovementCameraOrStalkerComponents()
        {
            var prefab = LoadPrefab();
            var components = prefab.GetComponentsInChildren<Component>(true);

            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                Assert.That(component, Is.Not.Null, "Prefab must not contain missing scripts.");

                var type = component.GetType();
                Assert.That(type.Name, Is.Not.EqualTo(PlayerMovementTypeName));
                Assert.That(type.Name, Is.Not.EqualTo(PlayerCameraTypeName));
                Assert.That(type.Namespace == null || !type.Namespace.StartsWith(StalkerNamespacePrefix, StringComparison.Ordinal), Is.True);
            }
        }

        private static GameObject LoadPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, $"Missing prefab at {PrefabPath}.");
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

        private static bool SerializedReferenceArrayContains(SerializedProperty array, UnityEngine.Object expected)
        {
            Assert.That(expected, Is.Not.Null);
            for (var i = 0; i < array.arraySize; i++)
            {
                if (array.GetArrayElementAtIndex(i).objectReferenceValue == expected)
                {
                    return true;
                }
            }

            return false;
        }

        private static UnityEngine.Object GetSerializedObjectReference(Component component, string propertyName)
        {
            Assert.That(component, Is.Not.Null);
            var serializedObject = new SerializedObject(component);
            var property = serializedObject.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null);
            return property.objectReferenceValue;
        }

        private static bool GetBoolProperty(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.TypeOf<bool>(), $"Property '{propertyName}' must return bool.");
            return (bool)value;
        }

        private static Transform GetTransformProperty(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.TypeOf<Transform>(), $"Property '{propertyName}' must return Transform.");
            return (Transform)value;
        }

        private static object GetProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName);
            Assert.That(property, Is.Not.Null, $"Missing public property '{propertyName}' on '{target.GetType().FullName}'.");
            return property.GetValue(target);
        }
    }
}
