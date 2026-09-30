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
        private const string SciFiScenePath = "Assets/Scenes/SciFi.unity";
        private const string LobbyPlayerStateScriptPath = "Assets/_Project/Scripts/Networking/Player/LobbyPlayerState.cs";
        private const string PlayerSpawnerScriptPath = "Assets/_Project/Scripts/Networking/Player/PlayerSpawner.cs";
        private const string PlayerSpawnerTypeName = "EchoProtocol.Networking.PlayerSpawner";

        [Test]
        public void FND_NET_PLAYER_SPAWNER_DoesNotOwnPlayerNetworkObjectLifecycle()
        {
            var source = LoadSpawnerSource();

            StringAssert.DoesNotContain("_playerPrefab", source);
            StringAssert.DoesNotContain("SetPlayerObject", source);
            StringAssert.DoesNotContain("Despawn(playerObject", source);
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
            StringAssert.Contains("InitializeAuthoritativeSelection(teamId, toolId, gameplay)", source);
        }

        [Test]
        public void FND_NET_PLAYER_SPAWNER_PreservesLobbyTeamToolAndUsesReadyResetApi()
        {
            var spawnerSource = LoadSpawnerSource();
            var lobbyStateSource = LoadLobbyPlayerStateSource();

            StringAssert.Contains("state.InitializeAuthoritativeSelection(teamId, toolId, gameplay)", spawnerSource);
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
            StringAssert.Contains(
                "GetGroundedPlayerSpawnPosition(playerObject, pose.Position)",
                source);
            StringAssert.Contains(
                "characterController.Teleport(position, pose.Rotation)",
                source);
            StringAssert.Contains(
                "networkTransform.Teleport(position, pose.Rotation)",
                source);
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

        [Test]
        public void SCIFI_PlayerSpawns_AllFaceStartRoomExit()
        {
            var scene = EditorSceneManager.OpenScene(SciFiScenePath, OpenSceneMode.Additive);
            try
            {
                var points = System.Array.FindAll(
                    Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include),
                    point => point.gameObject.scene == scene
                        && point.GetType().FullName == "EchoProtocol.Networking.NetworkPlayerSpawnPoint"
                        && point.name.StartsWith("PlayerStart_"));
                Assert.That(points.Length, Is.EqualTo(4));
                System.Array.Sort(points, (a, b) =>
                    new SerializedObject(a).FindProperty("_order").intValue.CompareTo(
                        new SerializedObject(b).FindProperty("_order").intValue));
                for (var i = 0; i < points.Length; i++)
                {
                    Assert.That(new SerializedObject(points[i]).FindProperty("_order").intValue, Is.EqualTo(i));
                    Assert.That(Vector3.Dot(points[i].transform.forward, Vector3.back),
                        Is.GreaterThan(0.98f), $"{points[i].name} must face the start room's south exit.");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void SCIFI_EnergyCoreSpawnCandidates_HaveThreePointsPerRoom()
        {
            var scene =
                EditorSceneManager.OpenScene(
                    SciFiScenePath,
                    OpenSceneMode.Additive);

            try
            {
                var rooms =
                    new System.Collections.Generic.Dictionary<string, int>();
                var roots =
                    scene.GetRootGameObjects();

                for (int i = 0; i < roots.Length; i++)
                {
                    var transforms =
                        roots[i].GetComponentsInChildren<Transform>(true);

                    for (int j = 0; j < transforms.Length; j++)
                    {
                        string name =
                            transforms[j].name;

                        if (!name.StartsWith(
                                "CoreSpawn_C",
                                System.StringComparison.OrdinalIgnoreCase)
                            || name.IndexOf(
                                "_EMPTY",
                                System.StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            continue;
                        }

                        const string prefix =
                            "CoreSpawn_C";
                        int start =
                            prefix.Length;
                        int end =
                            name.IndexOf(
                                '_',
                                start);

                        Assert.That(
                            end,
                            Is.GreaterThan(start),
                            name);

                        string room =
                            "C" + name.Substring(
                                start,
                                end - start);

                        rooms.TryGetValue(
                            room,
                            out int count);
                        rooms[room] =
                            count + 1;
                    }
                }

                Assert.That(
                    rooms.Count,
                    Is.EqualTo(6));

                for (int room = 1; room <= 6; room++)
                {
                    string key =
                        $"C{room}";

                    Assert.That(
                        rooms.ContainsKey(key),
                        Is.True,
                        $"Missing {key}.");
                    Assert.That(
                        rooms[key],
                        Is.EqualTo(3),
                        $"{key} must have exactly 3 Energy Core spawn points.");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(
                    scene,
                    true);
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
