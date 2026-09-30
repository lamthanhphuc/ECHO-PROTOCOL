using System;
using System.IO;
using System.Linq;
using EchoProtocol.Networking;
using EchoProtocol.Tools.Scanner;
using EchoProtocol.TeamTools;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Assert = NUnit.Framework.Assert;

namespace EchoProtocol.Player.Tests
{
    public sealed class TeamToolMultiplayerProductionTests
    {
        private const string PlayerNetworkPath = "Assets/Prefabs/PlayerNetwork.prefab";
        private const string MotionDecoyPickupPath = "Assets/Prefabs/Tools/PF_MotionDecoy_NetworkPickup.prefab";
        private const string CoreStabilizerPickupPath = "Assets/Prefabs/Tools/PF_CoreStabilizer_NetworkPickup.prefab";

        [Test]
        public void TEAM_TOOL_WorldSpawn_IsTransactional()
        {
            const string spawnSourcePath = "Assets/Scripts/TeamTools/TeamToolWorldSpawn.cs";
            const string matchSourcePath = "Assets/_Project/Scripts/Networking/Match/NetworkMatchState.cs";

            string spawnSource = File.ReadAllText(spawnSourcePath);
            string matchSource = File.ReadAllText(matchSourcePath);

            StringAssert.Contains("public static bool TrySpawnInitial", spawnSource);
            StringAssert.Contains("TryBuildZonePlan", spawnSource);
            StringAssert.Contains("RollbackSpawned", spawnSource);
            StringAssert.Contains("zone1Plans", spawnSource);
            StringAssert.Contains("zone2Plans", spawnSource);
            StringAssert.Contains("TeamToolWorldSpawnInitialized =", matchSource);
            StringAssert.Contains("TeamToolWorldSpawn.TrySpawnInitial", matchSource);
        }

        [Test]
        public void TEAM_TOOL_SciFiSpawnPoints_HaveValidRoomCoverage()
        {
            const string scenePath = "Assets/Scenes/SciFi.unity";
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var points = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<TeamToolSpawnPoint>(true))
                    .ToArray();
                ValidateZone(points, TeamToolSpawnZone.Zone1);
                ValidateZone(points, TeamToolSpawnZone.Zone2);
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void ValidateZone(TeamToolSpawnPoint[] points, TeamToolSpawnZone zone)
        {
            var zonePoints = points.Where(point => point.Zone == zone).ToArray();
            Assert.That(zonePoints.Length, Is.GreaterThanOrEqualTo(TeamToolWorldSpawn.RequiredToolCountPerZone));
            foreach (var point in zonePoints)
            {
                Assert.That(point.transform.parent.name, Is.EqualTo($"Room{point.RoomId:00}"));
                Assert.That(point.transform.parent.parent.name, Is.EqualTo(zone.ToString()));
            }

            var rooms = zonePoints.GroupBy(point => point.RoomId).ToArray();
            Assert.That(rooms.Length, Is.GreaterThanOrEqualTo(3), $"{zone} needs multiple rooms.");
            foreach (var room in rooms)
            {
                Assert.That(room.Key, Is.GreaterThan(0));
                Assert.That(room.Count(), Is.EqualTo(8), $"{zone} Room {room.Key:00} needs exactly 8 Team Tool spawn points.");
            }

            int[] requiredTools =
            {
                LobbyPlayerState.FieldScannerToolId,
                LobbyPlayerState.NoiseMakerToolId,
                LobbyPlayerState.FirstAidKitToolId,
                LobbyPlayerState.DoorJammerToolId,
                LobbyPlayerState.CoreStabilizerToolId,
            };
            foreach (int toolId in requiredTools)
                Assert.That(zonePoints.Any(point => point.Allows(toolId)), Is.True,
                    $"{zone} has no point for Team Tool {toolId}.");
        }

        [Test]
        public void TEAM_TOOL_Catalog_IsSharedBySpawnAndDrop_AndMapsCorrectPickups()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<TeamToolPickupCatalog>(
                "Assets/ScriptableObjects/TeamTools/SO_TeamToolPickupCatalog.asset");
            Assert.That(catalog, Is.Not.Null);

            var expected = new (int id, string path)[]
            {
                (LobbyPlayerState.FieldScannerToolId, "Assets/Prefabs/Tools/PF_FieldScanner_Pickup.prefab"),
                (LobbyPlayerState.NoiseMakerToolId, "Assets/Prefabs/Gameplay/Imported/PF_TeamToolPickup_NoiseMaker.prefab"),
                (LobbyPlayerState.FirstAidKitToolId, "Assets/Prefabs/Gameplay/Imported/PF_TeamToolPickup_FirstAid.prefab"),
                (LobbyPlayerState.DoorJammerToolId, "Assets/Prefabs/Gameplay/Imported/PF_Plank_Imported.prefab"),
                (LobbyPlayerState.CoreStabilizerToolId, CoreStabilizerPickupPath),
            };
            foreach (var (id, path) in expected)
            {
                var prefab = catalog.GetPrefab(id);
                Assert.That(prefab, Is.SameAs(AssetDatabase.LoadAssetAtPath<NetworkObject>(path)));
                var pickup = prefab.GetComponent<NetworkTeamToolPickup>();
                Assert.That(pickup != null ? pickup.ToolId : prefab.GetComponent<NetworkToolPickup>().ToolId,
                    Is.EqualTo(id));
            }

            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerNetworkPath)
                .GetComponentInChildren<NetworkPlayerInteractor>(true);
            var match = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Network/NetworkMatchState.prefab").GetComponent<NetworkMatchState>();
            Assert.That(new SerializedObject(player).FindProperty("_teamToolPickupCatalog").objectReferenceValue,
                Is.SameAs(catalog));
            Assert.That(new SerializedObject(match).FindProperty("_teamToolPickupCatalog").objectReferenceValue,
                Is.SameAs(catalog));
        }

        [Test]
        public void TOOL_ID_Constants_AreCanonical()
        {
            Assert.That(LobbyPlayerState.CoreStabilizerToolId, Is.EqualTo(6));
        }

        [Test]
        public void CORE_STABILIZER_GameplayPickup_UsesTeamToolPickup()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CoreStabilizerPickupPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkToolPickup>(), Is.Null);

            var pickup = prefab.GetComponent<NetworkTeamToolPickup>();
            Assert.That(pickup, Is.Not.Null);
            Assert.That(pickup.ToolId, Is.EqualTo(LobbyPlayerState.CoreStabilizerToolId));
        }

        [Test]
        public void INTERACTOR_ToolTypeFor_MapsCoreStabilizerAndRejectsDecoy()
        {
            var method = typeof(NetworkPlayerInteractor).GetMethod("ToolTypeFor",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "NetworkPlayerInteractor must have static ToolTypeFor method.");

            string tool5 = (string)method.Invoke(null, new object[] { 5 });
            string tool6 = (string)method.Invoke(null, new object[] { 6 });

            Assert.That(tool5, Is.Null, "Tool 5 (Motion Decoy) must not be mapped.");
            Assert.That(tool6, Is.EqualTo("CORE_STABILIZER"));
        }

        [Test]
        public void HELD_VIEW_ResolveToolPrefab_ResolvesCoreStabilizer()
        {
            var go = new GameObject("HeldViewTest", typeof(NetworkTeamToolHeldView));
            try
            {
                var heldView = go.GetComponent<NetworkTeamToolHeldView>();
                var method = typeof(NetworkTeamToolHeldView).GetMethod("ResolveToolPrefab",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.That(method, Is.Not.Null, "NetworkTeamToolHeldView must have ResolveToolPrefab method.");

                var dummy6 = new GameObject("Dummy6");
                try
                {
                    var so = new SerializedObject(heldView);
                    so.FindProperty("toolVisual_6").objectReferenceValue = dummy6;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    var res5 = (GameObject)method.Invoke(heldView, new object[] { 5 });
                    var res6 = (GameObject)method.Invoke(heldView, new object[] { 6 });

                    Assert.That(res5, Is.Null, "Tool 5 must not resolve to any visual.");
                    Assert.That(res6, Is.SameAs(dummy6));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(dummy6);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void MOTION_DECOY_IsRemovedFromProject()
        {
            var method = typeof(PlayerInventory).GetMethod("ResolveToolId",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            // MotionDecoyToolId constant should no longer exist on LobbyPlayerState
            var field = typeof(LobbyPlayerState).GetField("MotionDecoyToolId");
            Assert.That(field, Is.Null, "MotionDecoyToolId must be removed from LobbyPlayerState.");
        }

        [Test]
        public void CORE_STABILIZER_NetworkPickupPrefab_HasAllRequiredComponents()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CoreStabilizerPickupPath);
            Assert.That(prefab, Is.Not.Null, $"Missing prefab at {CoreStabilizerPickupPath}");

            var netObj = prefab.GetComponent<NetworkObject>();
            var toolPickup = prefab.GetComponent<NetworkTeamToolPickup>();
            var col = prefab.GetComponent<BoxCollider>();

            Assert.That(netObj, Is.Not.Null, "Must have NetworkObject.");
            Assert.That(toolPickup, Is.Not.Null, "Must have NetworkToolPickup.");
            Assert.That(col, Is.Not.Null, "Must have BoxCollider.");

            var so = new SerializedObject(toolPickup);
            Assert.That(so.FindProperty("_toolId").intValue, Is.EqualTo(6));
            Assert.That(so.FindProperty("_toolDisplayName").stringValue, Is.EqualTo("Core Stabilizer"));

            var netSo = new SerializedObject(netObj);
            var behaviours = netSo.FindProperty("NetworkedBehaviours");
            Assert.That(behaviours, Is.Not.Null);
            Assert.That(behaviours.arraySize, Is.GreaterThanOrEqualTo(1));
            Assert.That(behaviours.GetArrayElementAtIndex(0).objectReferenceValue, Is.SameAs(toolPickup));
        }

        [Test]
        public void PLAYER_NETWORK_Prefab_HasCoreStabilizerInventoryDefinition()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerNetworkPath);
            var inventory = prefab.GetComponentInChildren<PlayerInventory>(true);
            Assert.That(inventory, Is.Not.Null);

            var definition = new SerializedObject(inventory)
                .FindProperty("coreStabilizerDefinition").objectReferenceValue;
            var expected = AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(
                "Assets/ScriptableObjects/Inventory/TeamTools/SO_CoreStabilizer_TeamTool.asset");
            Assert.That(expected, Is.Not.Null);
            Assert.That(definition, Is.SameAs(expected));
        }

        [Test]
        public void PLAYER_NETWORK_Prefab_HasToolDefinition_6_AndNot_5()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerNetworkPath);
            Assert.That(prefab, Is.Not.Null, $"Missing prefab at {PlayerNetworkPath}");

            var lobbyState = prefab.GetComponent<LobbyPlayerState>();
            Assert.That(lobbyState, Is.Not.Null);

            var so = new SerializedObject(lobbyState);
            var toolsProp = so.FindProperty("_toolDefinitions");
            Assert.That(toolsProp, Is.Not.Null);

            bool hasTool5 = false;
            bool hasTool6 = false;

            for (int i = 0; i < toolsProp.arraySize; i++)
            {
                var elem = toolsProp.GetArrayElementAtIndex(i);
                int id = elem.FindPropertyRelative("_id").intValue;
                if (id == 5) hasTool5 = true;
                if (id == 6) hasTool6 = true;
            }

            Assert.That(hasTool5, Is.False, "PlayerNetwork must NOT have tool definition with ID 5.");
            Assert.That(hasTool6, Is.True, "PlayerNetwork must have tool definition with ID 6 (Core Stabilizer).");
        }

        [Test]
        public void PLAYER_NETWORK_Prefab_HasHeldVisual_6()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerNetworkPath);
            Assert.That(prefab, Is.Not.Null);

            var heldView = prefab.GetComponentInChildren<NetworkTeamToolHeldView>(true);
            Assert.That(heldView, Is.Not.Null);

            var so = new SerializedObject(heldView);
            var v6 = so.FindProperty("toolVisual_6").objectReferenceValue;

            Assert.That(v6, Is.Not.Null, "PlayerNetwork NetworkTeamToolHeldView must have toolVisual_6 assigned.");
        }

        [Test]
        public void PLAYER_NETWORK_Prefab_NetworkPlayerInteractor_HasCoreStabilizerAudioAssigned()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerNetworkPath);
            Assert.That(prefab, Is.Not.Null);

            var interactor = prefab.GetComponentInChildren<NetworkPlayerInteractor>(true);
            Assert.That(interactor, Is.Not.Null);

            var so = new SerializedObject(interactor);
            var pulseClip = so.FindProperty("_coreStabilizerPulseClip").objectReferenceValue;

            Assert.That(pulseClip, Is.Not.Null, "_coreStabilizerPulseClip must be assigned.");
        }

        [Test]
        public void PLAYER_NETWORK_Prefab_HeldVisuals_DoNotContainNetworkObjects()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerNetworkPath);
            Assert.That(prefab, Is.Not.Null);

            var heldView = prefab.GetComponentInChildren<NetworkTeamToolHeldView>(true);
            Assert.That(heldView, Is.Not.Null);

            var so = new SerializedObject(heldView);
            string[] visualProps = new[] { "toolVisual_1", "toolVisual_2", "toolVisual_3", "toolVisual_4", "toolVisual_6" };

            foreach (var propName in visualProps)
            {
                var prop = so.FindProperty(propName);
                if (prop != null && prop.objectReferenceValue is GameObject visualObj)
                {
                    var netObj = visualObj.GetComponentInChildren<NetworkObject>(true);
                    Assert.That(netObj, Is.Null,
                        $"{propName} ('{visualObj.name}') must NOT contain a NetworkObject component, as instantiating it locally causes native crashes (0xC0000005) in multiplayer.");
                }
            }
        }

        [Test]
        public void PLANK_HeldVisualPrefab_ExistsAndHasNoNetworkObject()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Imported/PF_Plank_HeldVisual.prefab");
            Assert.That(prefab, Is.Not.Null, "PF_Plank_HeldVisual.prefab must exist.");

            var netObj = prefab.GetComponentInChildren<NetworkObject>(true);
            Assert.That(netObj, Is.Null, "PF_Plank_HeldVisual must NOT contain a NetworkObject.");

            var col = prefab.GetComponentInChildren<Collider>(true);
            Assert.That(col, Is.Null, "PF_Plank_HeldVisual must NOT contain colliders.");
        }

        [Test]
        public void NOISE_MAKER_PreviewUsesDeployedPrefabWithoutGameplayComponents()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerNetworkPath);
            var source = player.GetComponentInChildren<NetworkPlayerInteractor>(true)
                .NoiseMakerPreviewPrefab;
            Assert.That(source, Is.EqualTo(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Gameplay/Imported/DistressBeaconDeployed.prefab")));

            var anchor = new GameObject("Preview test anchor");
            try
            {
                var visual = NetworkTeamToolHeldView.InstantiateHeldVisualSafely(
                    source, anchor.transform);
                Assert.That(visual, Is.Not.Null);
                Assert.That(visual.GetComponentsInChildren<Renderer>(true).Length,
                    Is.GreaterThan(0));
                Assert.That(visual.GetComponentInChildren<NetworkObject>(true), Is.Null);
                Assert.That(visual.GetComponentInChildren<Collider>(true), Is.Null);
                Assert.That(visual.GetComponentInChildren<AudioSource>(true), Is.Null);
                Assert.That(visual.GetComponentInChildren<NoiseMakerBeacon>(true), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(anchor);
            }
        }
    }
}
