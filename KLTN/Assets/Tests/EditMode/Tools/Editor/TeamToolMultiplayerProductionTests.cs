using System;
using System.IO;
using System.Linq;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Gameplay;
using EchoProtocol.Networking;
using EchoProtocol.Tools.Scanner;
using EchoProtocol.TeamTools;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
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
        public void GOD_MODE_AllTeamToolsAreInfiniteAndCooldownFree()
        {
            string interactor = File.ReadAllText(
                "Assets/_Project/Scripts/Networking/Interaction/NetworkPlayerInteractor.cs");
            string scanner = File.ReadAllText(
                "Assets/Scripts/Tools/Scanner/NetworkFieldScanner.cs");
            string life = File.ReadAllText(
                "Assets/_Project/Scripts/Networking/Player/NetworkPlayerLifeState.cs");

            StringAssert.Contains("if (IsDebugGodModeActive)", interactor);
            StringAssert.Contains("return 0f;", interactor);
            StringAssert.Contains("TickTimer.None", interactor);
            StringAssert.Contains("if (IsDebugGodModeActive)", scanner);
            StringAssert.Contains("RequestDebugTeamTool", life);
            StringAssert.Contains("FieldScannerToolId", life);
            StringAssert.Contains("NoiseMakerToolId", life);
            StringAssert.Contains("FirstAidKitToolId", life);
            StringAssert.Contains("DoorJammerToolId", life);
            StringAssert.Contains("CoreStabilizerToolId", life);
        }

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
            StringAssert.Contains("zone3Plans", spawnSource);
            StringAssert.Contains("TeamToolWorldSpawnInitialized =", matchSource);
            StringAssert.Contains("TeamToolWorldSpawn.TrySpawnInitial", matchSource);
        }

        [Test]
        public void TEAM_TOOL_SpawnCount_UsesDifficultyBudget()
        {
            Assert.That(
                TeamToolWorldSpawn.RequiredToolCountPerZone,
                Is.EqualTo(5));

            Assert.That(
                MatchDifficultyProfiles
                    .Get(MatchDifficulty.Easy)
                    .TeamToolsPerZone,
                Is.EqualTo(6));

            Assert.That(
                MatchDifficultyProfiles
                    .Get(MatchDifficulty.Normal)
                    .TeamToolsPerZone,
                Is.EqualTo(5));

            Assert.That(
                MatchDifficultyProfiles
                    .Get(MatchDifficulty.Hard)
                    .TeamToolsPerZone,
                Is.EqualTo(5));
        }

        [Test]
        public void TEAM_TOOL_Runtime_IsClearedWhenToolLeavesPlayer()
        {
            string source =
                File.ReadAllText(
                    "Assets/_Project/Scripts/Networking/Interaction/NetworkPlayerInteractor.cs");

            StringAssert.Contains(
                "private void ResetTeamToolRuntimeAuthoritative()",
                source);

            StringAssert.Contains(
                "TeamToolCooldown =",
                source);

            StringAssert.Contains(
                "TickTimer.None",
                source);

            StringAssert.Contains(
                "CoreStabilizerActiveTimer =",
                source);

            StringAssert.Contains(
                "ClearStabilizerBuffedPlayers();",
                source);
        }

        [Test]
        public void TEAM_TOOL_HudShowsRemainingMultiUseCount()
        {
            string source =
                File.ReadAllText(
                    "Assets/Scripts/UI/HUD/HUDHotbar.cs");

            StringAssert.Contains(
                "TeamToolUsesRemaining",
                source);

            StringAssert.Contains(
                "LobbyPlayerState.AnyStateChanged += RefreshSlots",
                source);
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
                ValidateZone(points, TeamToolSpawnZone.Zone3);
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

        [Test]
        public void TEAM_TOOL_SciFi_ResearchPreviewUsesRealZoneEntries()
        {
            const string scenePath = "Assets/Scenes/SciFi.unity";
            var scene = SceneManager.GetSceneByPath(scenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            try
            {
                var roots = scene.GetRootGameObjects();
                var zone1Entries = roots
                    .SelectMany(root =>
                        root.GetComponentsInChildren<Transform>(true))
                    .Where(t => t.name == "AED_Z1_RESOURCE_ENTRY")
                    .ToArray();

                Assert.That(zone1Entries.Length, Is.EqualTo(1),
                    "Expected exactly one Zone1 resource entry anchor.");

                var zone1Entry = zone1Entries[0];

                Assert.That(
                    NavMesh.SamplePosition(
                        zone1Entry.position,
                        out var zone1NavHit,
                        1f,
                        NavMesh.AllAreas),
                    Is.True,
                    "Zone1 resource entry must be on reachable NavMesh.");
                var zone2 = roots.SelectMany(root => root.GetComponentsInChildren<StalkerZone2EntryTrigger>(true)).FirstOrDefault();
                Assert.That(zone2, Is.Not.Null);

                var zone3 = new Vector3(88.66215f, 2.080028f, -399.027f);
                string matchSource = File.ReadAllText("Assets/_Project/Scripts/Networking/Match/NetworkMatchState.cs");
                StringAssert.Contains("new Vector3(88.66215f, 2.080028f, -399.027f)", matchSource);
                var catalog = AssetDatabase.LoadAssetAtPath<TeamToolPickupCatalog>("Assets/ScriptableObjects/TeamTools/SO_TeamToolPickupCatalog.asset");
                Assert.That(catalog, Is.Not.Null);

                var proposal = AEDResourceDirectorV1.Evaluate(new AEDResourceRequestV1
                {
                    MatchId = Guid.NewGuid(), Difficulty = "Normal", ResolutionMode = "Fixed"
                });
                var entries = new[] { zone1NavHit.position, zone2.transform.position, zone3 };
                var triangulation = NavMesh.CalculateTriangulation();

                Debug.LogWarning(
                    $"[AED_P5_6] NavMesh triangles=" +
                    $"{triangulation.indices.Length / 3}");

                for (int i = 0; i < entries.Length; i++)
                {
                    bool sampled = NavMesh.SamplePosition(
                        entries[i],
                        out var hit,
                        2f,
                        NavMesh.AllAreas);

                    Debug.LogWarning(
                        $"[AED_P5_6] Zone{i + 1} " +
                        $"entry={entries[i]} " +
                        $"sampled={sampled} " +
                        $"nearest={(sampled ? hit.position.ToString() : "NONE")}");
                }
                var zone1Points = roots
                    .SelectMany(root =>
                        root.GetComponentsInChildren<TeamToolSpawnPoint>(true))
                    .Where(p => p.isActiveAndEnabled
                        && p.Zone == TeamToolSpawnZone.Zone1)
                    .Take(5)
                    .ToArray();

                foreach (var point in zone1Points)
                {
                    var raw = point.transform.position;
                    bool spawnOk = point.TryGetSpawnPosition(out _);
                    bool startOk = NavMesh.SamplePosition(
                        entries[0], out var start, 2f, NavMesh.AllAreas);
                    bool destinationOk = NavMesh.SamplePosition(
                        raw, out var destination, 1.5f, NavMesh.AllAreas);
                    var path = new NavMeshPath();
                    bool calculated = startOk && destinationOk &&
                        NavMesh.CalculatePath(
                            start.position,
                            destination.position,
                            NavMesh.AllAreas,
                            path);
                    float entryVertical = startOk
                        ? Mathf.Abs(start.position.y - entries[0].y)
                        : -1f;
                    float destinationVertical = destinationOk
                        ? Mathf.Abs(destination.position.y - raw.y)
                        : -1f;
                    float destinationHorizontal = destinationOk
                        ? Vector3.ProjectOnPlane(
                            destination.position - raw, Vector3.up).magnitude
                        : -1f;

                    Debug.LogWarning(
                        $"[AED_P5_6_Z1] point={point.name} " +
                        $"raw={raw} " +
                        $"spawnOK={spawnOk} " +
                        $"startOK={startOk} " +
                        $"destinationOK={destinationOk} " +
                        $"entryVertical={entryVertical:F3} " +
                        $"destinationVertical={destinationVertical:F3} " +
                        $"destinationHorizontal={destinationHorizontal:F3} " +
                        $"calculated={calculated} " +
                        $"pathStatus={path.status}");
                }

                bool success = TeamToolWorldSpawn.TryBuildResearchPreview(catalog, proposal, entries, 6f, out var receipts, scenePath);
                Assert.That(success, Is.True, "SciFi research preview failed: check NavMesh bake, zone entries, point reachability and tool masks.");
                Assert.That(receipts.Count, Is.EqualTo(15));
                Assert.That(receipts.Select(x => x.PointId).Distinct().Count(), Is.EqualTo(15));
                Assert.That(receipts.All(x => x.PointId.StartsWith(scenePath + "/", StringComparison.Ordinal)), Is.True);
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
