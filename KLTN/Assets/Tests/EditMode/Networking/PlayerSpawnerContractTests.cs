using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Networking.Tests
{
    public sealed class PlayerSpawnerContractTests
    {
        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string LobbyPlayerStateScriptPath = "Assets/_Project/Scripts/Networking/Player/LobbyPlayerState.cs";
        private const string PlayerSpawnerScriptPath = "Assets/_Project/Scripts/Networking/Player/PlayerSpawner.cs";
        private const string PlayerSpawnerTypeName = "EchoProtocol.Networking.PlayerSpawner";

        [Test]
        public void FND_NET_PLAYER_SPAWNER_DoesNotOwnPlayerNetworkObjectLifecycle()
        {
            var source = LoadSpawnerSource();

            StringAssert.DoesNotContain("_playerPrefab", source);
            StringAssert.DoesNotContain("SetPlayerObject", source);
            StringAssert.DoesNotContain("Despawn(", source);
            StringAssert.Contains("ConfigureExistingPlayerObject", source);
            StringAssert.Contains("TryGetPlayerObject", source);
            StringAssert.Contains("PlayerObjectCommitted", source);
        }

        [Test]
        public void FND_NET_PLAYER_SPAWNER_UsesLifecycleCommittedObjectPathForLateJoinPlacement()
        {
            var source = LoadSpawnerSource();

            StringAssert.Contains("TryAttachLifecycle", source);
            StringAssert.Contains("PlayerObjectCommitted += HandleLifecyclePlayerObjectCommitted", source);
            StringAssert.Contains("HandleLifecyclePlayerObjectCommitted", source);
            StringAssert.Contains("ConfigureExistingPlayerObject(commit.Player, commit.PlayerObject, gameplay)", source);
        }

        [Test]
        public void FND_NET_PLAYER_SPAWNER_PlacesExistingPlayersOnGameplaySceneLoad()
        {
            var source = LoadSpawnerSource();

            StringAssert.Contains("HandleNetworkSceneLoadDone", source);
            StringAssert.Contains("foreach (var player in runner.ActivePlayers)", source);
            StringAssert.Contains("runner.TryGetPlayerObject(player, out var playerObject)", source);
            StringAssert.Contains("ConfigureExistingPlayerObject(player, playerObject, gameplay: true)", source);
            StringAssert.Contains("InitializeAuthoritativeSelection(state.TeamId, state.ToolId, gameplay)", source);
        }

        [Test]
        public void FND_NET_PLAYER_SPAWNER_PreservesLobbyTeamToolAndUsesReadyResetApi()
        {
            var spawnerSource = LoadSpawnerSource();
            var lobbyStateSource = LoadLobbyPlayerStateSource();

            StringAssert.Contains("state.InitializeAuthoritativeSelection(state.TeamId, state.ToolId, gameplay)", spawnerSource);
            StringAssert.Contains("public void InitializeAuthoritativeSelection(int teamId, int toolId, bool isGameplayPlayer)", lobbyStateSource);
            StringAssert.Contains("TeamId = teamId;", lobbyStateSource);
            StringAssert.Contains("ToolId = toolId;", lobbyStateSource);
            StringAssert.Contains("IsReady = false;", lobbyStateSource);
            StringAssert.Contains("IsGameplayPlayer = isGameplayPlayer;", lobbyStateSource);
        }

        [Test]
        public void FND_NET_PLAYER_SPAWNER_TeleportsThroughReplicatedCharacterControllerFirst()
        {
            var source = LoadSpawnerSource();

            var characterControllerIndex = source.IndexOf("TryGetComponent<NetworkCharacterController>", System.StringComparison.Ordinal);
            var networkTransformIndex = source.IndexOf("TryGetComponent<NetworkTransform>", System.StringComparison.Ordinal);

            Assert.That(characterControllerIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(networkTransformIndex, Is.GreaterThan(characterControllerIndex));
            StringAssert.Contains("characterController.Teleport(pose.Position, pose.Rotation)", source);
            StringAssert.Contains("networkTransform.Teleport(pose.Position)", source);
        }

        [Test]
        public void FND_NET_PLAYER_SPAWNER_DeterministicSpawnSlotsRemainBoundedAndStable()
        {
            var source = LoadSpawnerSource();

            StringAssert.Contains("private const int SupportedPlayerCount = 4;", source);
            StringAssert.Contains("for (var slot = 0; slot < SupportedPlayerCount; slot++)", source);
            StringAssert.Contains("_spawnSlots[player] = slot", source);
            StringAssert.Contains("_spawnSlots.Remove(player)", source);
            StringAssert.Contains("GetFallbackPose(slot)", source);
        }

        [Test]
        public void FND_NET_PLAYER_SPAWNER_BootstrapSceneHasNoObsoletePlayerPrefabReference()
        {
            var scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Additive);
            try
            {
                var spawner = FindSingleComponent(scene, PlayerSpawnerTypeName);
                Assert.That(spawner, Is.Not.Null);

                var serializedSpawner = new SerializedObject(spawner);
                Assert.That(serializedSpawner.FindProperty("_playerPrefab"), Is.Null);
                Assert.That(serializedSpawner.FindProperty("_doorPrefab"), Is.Not.Null);
                Assert.That(serializedSpawner.FindProperty("_pickupItemPrefab"), Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static string LoadSpawnerSource()
        {
            Assert.That(File.Exists(PlayerSpawnerScriptPath), Is.True);
            return File.ReadAllText(PlayerSpawnerScriptPath);
        }

        private static string LoadLobbyPlayerStateSource()
        {
            Assert.That(File.Exists(LobbyPlayerStateScriptPath), Is.True);
            return File.ReadAllText(LobbyPlayerStateScriptPath);
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
    }
}
