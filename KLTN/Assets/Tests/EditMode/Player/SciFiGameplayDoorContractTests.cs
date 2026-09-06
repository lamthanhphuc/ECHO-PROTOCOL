using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EchoProtocol.Player.Tests
{
    public sealed class SciFiGameplayDoorContractTests
    {
        private const string DoorPrefabPath = "Assets/Prefabs/Environment/Door/PF_SciFiSlidingDoor.prefab";
        private const string DoorPrefabMetaPath = DoorPrefabPath + ".meta";
        private const string PlayerNetworkMetaPath = "Assets/Prefabs/PlayerNetwork.prefab.meta";
        private const string ScenePath = "Assets/Scenes/SciFi.unity";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        [Test]
        public void GAMEPLAY_SCENE_SciFiIsEnabledAndCanonical()
        {
            var lobbyManagerType = ResolveProductionType("EchoProtocol.Networking.LobbyManager");
            var sceneName = lobbyManagerType.GetField("GameSceneName")?.GetRawConstantValue();

            Assert.That(sceneName, Is.EqualTo("SciFi"));
            Assert.That(
                EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == ScenePath),
                Is.True,
                "SciFi must be enabled in Build Settings for Fusion scene loading.");
        }

        [Test]
        public void GAMEPLAY_SCENE_DoorIsBakedAndHasNoPlacedNetworkPlayer()
        {
            var sceneYaml = File.ReadAllText(ScenePath);
            var doorGuid = ReadGuid(DoorPrefabMetaPath);
            var playerNetworkGuid = ReadGuid(PlayerNetworkMetaPath);

            StringAssert.Contains($"guid: {doorGuid}", sceneYaml);
            StringAssert.Contains("propertyPath: SortKey", sceneYaml);
            StringAssert.DoesNotContain($"guid: {playerNetworkGuid}", sceneYaml);
        }

        [Test]
        public void GAMEPLAY_DOOR_HasFusionBehaviourPanelsAndBlockingCollider()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            foreach (var component in prefab.GetComponentsInChildren<Component>(true))
            {
                Assert.That(component, Is.Not.Null, "Door prefab must not contain missing scripts.");
            }

            var networkObject = GetComponentByTypeName(prefab, "Fusion.NetworkObject");
            var slidingDoor = GetComponentByTypeName(prefab, "EchoProtocol.Networking.NetworkSlidingDoor");
            Assert.That(networkObject, Is.Not.Null);
            Assert.That(slidingDoor, Is.Not.Null);

            var serializedDoor = new SerializedObject(slidingDoor);
            Assert.That(serializedDoor.FindProperty("_leftDoor")?.objectReferenceValue, Is.Not.Null);
            Assert.That(serializedDoor.FindProperty("_rightDoor")?.objectReferenceValue, Is.Not.Null);

            var blocker = serializedDoor.FindProperty("_blockingCollider")?.objectReferenceValue as BoxCollider;
            Assert.That(blocker, Is.Not.Null);
            Assert.That(blocker.isTrigger, Is.False);

            var serializedNetworkObject = new SerializedObject(networkObject);
            var behaviours = serializedNetworkObject.FindProperty("NetworkedBehaviours");
            Assert.That(behaviours, Is.Not.Null);
            Assert.That(SerializedReferenceArrayContains(behaviours, slidingDoor), Is.True);
        }

        [Test]
        public void GAMEPLAY_INPUT_InteractIsImmediateEPress()
        {
            var inputJson = File.ReadAllText(InputActionsPath);
            var interactActionStart = inputJson.IndexOf("\"name\": \"Interact\"", StringComparison.Ordinal);
            Assert.That(interactActionStart, Is.GreaterThanOrEqualTo(0));

            var nextActionStart = inputJson.IndexOf("\"name\": \"Crouch\"", interactActionStart, StringComparison.Ordinal);
            Assert.That(nextActionStart, Is.GreaterThan(interactActionStart));
            var interactActionJson = inputJson.Substring(
                interactActionStart,
                nextActionStart - interactActionStart);
            StringAssert.Contains("\"interactions\": \"\"", interactActionJson);

            var keyboardBindingStart = inputJson.IndexOf("\"path\": \"<Keyboard>/e\"", StringComparison.Ordinal);
            Assert.That(keyboardBindingStart, Is.GreaterThanOrEqualTo(0));
            var nextBindingStart = inputJson.IndexOf("\"isComposite\"", keyboardBindingStart, StringComparison.Ordinal);
            var keyboardBindingJson = inputJson.Substring(
                keyboardBindingStart,
                nextBindingStart - keyboardBindingStart);
            StringAssert.Contains("\"action\": \"Interact\"", keyboardBindingJson);
        }

        private static Type ResolveProductionType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }

            Assert.Fail($"Missing production type '{fullName}'.");
            return null;
        }

        private static string ReadGuid(string metaPath)
        {
            var line = File.ReadLines(metaPath).First(value => value.StartsWith("guid: ", StringComparison.Ordinal));
            return line.Substring("guid: ".Length).Trim();
        }

        private static Component GetComponentByTypeName(GameObject root, string fullTypeName)
        {
            return root.GetComponents<Component>()
                .FirstOrDefault(component => component != null && component.GetType().FullName == fullTypeName);
        }

        private static bool SerializedReferenceArrayContains(SerializedProperty array, UnityEngine.Object expected)
        {
            for (var i = 0; i < array.arraySize; i++)
            {
                if (array.GetArrayElementAtIndex(i).objectReferenceValue == expected)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
