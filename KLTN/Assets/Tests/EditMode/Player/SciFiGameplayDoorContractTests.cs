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
        private const string NetworkDoorScriptPath = "Assets/_Project/Scripts/Networking/Interaction/NetworkDoor.cs";
        private const string NetworkDoorJammerScriptPath = "Assets/_Project/Scripts/Networking/Interaction/NetworkDoorJammer.cs";
        private const string NetworkSlidingDoorScriptPath = "Assets/_Project/Scripts/Networking/Interaction/NetworkSlidingDoor.cs";
        private const string NetworkPlayerInteractorScriptPath = "Assets/_Project/Scripts/Networking/Interaction/NetworkPlayerInteractor.cs";
        private const string DoorJammerPrefabPath = "Assets/Resources/Network/PF_DoorJammer.prefab";

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
        public void GAMEPLAY_DOOR_NetworkDoorStateNumericContractIsPreserved()
        {
            var type = ResolveProductionType("EchoProtocol.Networking.NetworkDoorState");

            Assert.That(EnumValue(type, "Closed"), Is.EqualTo(0));
            Assert.That(EnumValue(type, "Open"), Is.EqualTo(1));
            Assert.That(EnumValue(type, "Locked"), Is.EqualTo(2));
        }

        [Test]
        public void GAMEPLAY_DOOR_BrokenAndJammerRuntimeContractsExist()
        {
            var jammerSource = File.ReadAllText(NetworkDoorJammerScriptPath);
            var slidingDoorSource = File.ReadAllText(NetworkSlidingDoorScriptPath);

            StringAssert.Contains("public bool IsBroken", slidingDoorSource);
            StringAssert.Contains("public bool BlocksTraversal", slidingDoorSource);
            StringAssert.Contains("public bool CanMonsterOpen", slidingDoorSource);
            StringAssert.Contains("public bool TryOpenForMonsterAuthoritative()", slidingDoorSource);
            StringAssert.Contains("public bool TryBreakAuthoritative()", slidingDoorSource);
            StringAssert.Contains("public bool CanAcceptJammer()", slidingDoorSource);
            StringAssert.Contains("public bool TryAttachJammerAuthoritative(NetworkDoorJammer jammer)", slidingDoorSource);
            StringAssert.Contains("public bool TryClearJammerAuthoritative(NetworkDoorJammer jammer)", slidingDoorSource);
            StringAssert.Contains("public bool TryGetJammerPlacement(out Vector3 position, out Quaternion rotation)", slidingDoorSource);
            StringAssert.Contains("public enum NetworkDoorJammerState", jammerSource);
            StringAssert.Contains("public sealed class NetworkDoorJammer", jammerSource);
            StringAssert.Contains("public float BreakDurationSeconds", jammerSource);
            StringAssert.Contains("public bool CompleteBreakAuthoritative()", jammerSource);
        }

        [Test]
        public void GAMEPLAY_DOOR_JammerStateNumericContractIsStable()
        {
            var type = ResolveProductionType("EchoProtocol.Networking.NetworkDoorJammerState");

            Assert.That(EnumValue(type, "NotDeployed"), Is.EqualTo(0));
            Assert.That(EnumValue(type, "Active"), Is.EqualTo(1));
            Assert.That(EnumValue(type, "Destroyed"), Is.EqualTo(2));
        }

        [Test]
        public void GAMEPLAY_DOOR_JammerDeploymentContractUsesBrokenDoorAndSingleActiveJammer()
        {
            var slidingDoorSource = File.ReadAllText(NetworkSlidingDoorScriptPath);

            StringAssert.Contains("return IsBroken && !HasActiveJammer;", slidingDoorSource);
            StringAssert.Contains("if (HasActiveJammer)", slidingDoorSource);
            StringAssert.Contains("return ActiveJammerId == jammer.Object.Id;", slidingDoorSource);
            StringAssert.Contains("if (!CanAcceptJammer() || !jammer.InitializeAuthoritative(Object.Id))", slidingDoorSource);
            StringAssert.Contains("ActiveJammerId = jammer.Object.Id;", slidingDoorSource);
        }

        [Test]
        public void GAMEPLAY_DOOR_BreakAndJammerDestructionAreAuthoritativeAndIdempotent()
        {
            var jammerSource = File.ReadAllText(NetworkDoorJammerScriptPath);
            var slidingDoorSource = File.ReadAllText(NetworkSlidingDoorScriptPath);

            StringAssert.Contains("if (!IsOnline)", slidingDoorSource);
            StringAssert.Contains("if (!Object.HasStateAuthority)", slidingDoorSource);
            StringAssert.Contains("if (IsBroken)", slidingDoorSource);
            StringAssert.Contains("State = NetworkDoorState.Open;", slidingDoorSource);
            StringAssert.Contains("_blockingCollider.enabled = DoorBlocksTraversal;", slidingDoorSource);
            StringAssert.Contains("if (!Object.HasStateAuthority)", jammerSource);
            StringAssert.Contains("if (State != NetworkDoorJammerState.Destroyed)", jammerSource);
            StringAssert.Contains("return true;", jammerSource);
            StringAssert.Contains("State = NetworkDoorJammerState.Destroyed;", jammerSource);
            StringAssert.Contains("TryReconcileDoorRelationAuthoritative()", jammerSource);
        }

        [Test]
        public void GAMEPLAY_DOOR_OfflineClosedUnlockedDoorCanBeOpenedByMonsterApi()
        {
            var door = CreateOfflineSlidingDoor(false, out var blocker);
            try
            {
                Assert.That(InvokeBool(door, "TryOpenForMonsterAuthoritative"), Is.True);

                Assert.That(
                    EnumValue(ResolveProductionType("EchoProtocol.Networking.NetworkDoorState"), "Open"),
                    Is.EqualTo(Convert.ToInt32(GetProperty(door, "CurrentState"))));
                Assert.That(GetBoolProperty(door, "DoorBlocksTraversal"), Is.False);
                Assert.That(blocker.enabled, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(door.gameObject);
            }
        }

        [Test]
        public void GAMEPLAY_DOOR_OfflineLockedDoorRejectsMonsterOpen()
        {
            var door = CreateOfflineSlidingDoor(false, out var blocker);
            try
            {
                Assert.That(InvokeBool(door, "SetLockedAuthoritative", true), Is.True);
                Assert.That(
                    EnumValue(ResolveProductionType("EchoProtocol.Networking.NetworkDoorState"), "Locked"),
                    Is.EqualTo(Convert.ToInt32(GetProperty(door, "CurrentState"))));

                Assert.That(InvokeBool(door, "TryOpenForMonsterAuthoritative"), Is.False);

                Assert.That(
                    EnumValue(ResolveProductionType("EchoProtocol.Networking.NetworkDoorState"), "Locked"),
                    Is.EqualTo(Convert.ToInt32(GetProperty(door, "CurrentState"))));
                Assert.That(GetBoolProperty(door, "DoorBlocksTraversal"), Is.True);
                Assert.That(blocker.enabled, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(door.gameObject);
            }
        }

        [Test]
        public void GAMEPLAY_DOOR_OfflineIntactDoorCanBeBrokenByMonsterApi()
        {
            var door = CreateOfflineSlidingDoor(true, out var blocker);
            try
            {
                Assert.That(InvokeBool(door, "TryBreakAuthoritative"), Is.True);

                Assert.That(GetBoolProperty(door, "IsBroken"), Is.True);
                Assert.That(
                    EnumValue(ResolveProductionType("EchoProtocol.Networking.NetworkDoorState"), "Open"),
                    Is.EqualTo(Convert.ToInt32(GetProperty(door, "CurrentState"))));
                Assert.That(GetBoolProperty(door, "DoorBlocksTraversal"), Is.False);
                Assert.That(blocker.enabled, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(door.gameObject);
            }
        }

        [Test]
        public void GAMEPLAY_DOOR_RepeatedOfflineBreakIsIdempotent()
        {
            var door = CreateOfflineSlidingDoor(false, out _);
            try
            {
                Assert.That(InvokeBool(door, "TryBreakAuthoritative"), Is.True);
                Assert.That(InvokeBool(door, "TryBreakAuthoritative"), Is.True);
                Assert.That(GetBoolProperty(door, "IsBroken"), Is.True);
                Assert.That(
                    EnumValue(ResolveProductionType("EchoProtocol.Networking.NetworkDoorState"), "Open"),
                    Is.EqualTo(Convert.ToInt32(GetProperty(door, "CurrentState"))));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(door.gameObject);
            }
        }

        [Test]
        public void GAMEPLAY_DOOR_OnlineProxyAuthorityGuardRemainsProtected()
        {
            var source = File.ReadAllText(NetworkSlidingDoorScriptPath);
            var openMethod = MethodBody(source, "public bool TryOpenForMonsterAuthoritative");
            var breakMethod = MethodBody(source, "public bool TryBreakAuthoritative");

            StringAssert.Contains("if (!IsOnline)", openMethod);
            StringAssert.Contains("if (!Object.HasStateAuthority)", openMethod);
            StringAssert.Contains("if (!IsOnline)", breakMethod);
            StringAssert.Contains("if (!Object.HasStateAuthority)", breakMethod);
        }

        [Test]
        public void GAMEPLAY_DOOR_DoorJammerTeamToolMappingAndDeploymentPathArePresent()
        {
            var interactorSource = File.ReadAllText(NetworkPlayerInteractorScriptPath);

            StringAssert.Contains("case 4: return \"DOOR_JAMMER\";", interactorSource);
            StringAssert.Contains("RpcRequestUseTeamTool(NextSequence(), targetId)", interactorSource);
            StringAssert.Contains("RpcRequestUseTeamTool(uint sequence, NetworkId targetId", interactorSource);
            StringAssert.Contains("TryDeployDoorJammerAuthoritative", interactorSource);
            StringAssert.Contains("TryDetectLocalDoorJammerTargetIntent", interactorSource);
            StringAssert.Contains("TryResolveDoorJammerTargetAuthoritative", interactorSource);
            StringAssert.Contains("door.CanAcceptJammer()", interactorSource);
            StringAssert.Contains("door.DoorJammerPrefab", interactorSource);
            StringAssert.DoesNotContain("_doorJammerPrefab", interactorSource);
            StringAssert.Contains("door.TryGetJammerPlacement(out var position, out var rotation)", interactorSource);
            StringAssert.Contains("Runner.Spawn(prefab, position, rotation)", interactorSource);
            StringAssert.Contains("door.TryAttachJammerAuthoritative(jammer)", interactorSource);
            StringAssert.Contains("ConsumeGameplayTeamTool(state)", interactorSource);
        }

        [Test]
        public void GAMEPLAY_DOOR_DoorJammerAuthorityResolvesNetworkIdInsteadOfHostCameraRaycast()
        {
            var interactorSource = File.ReadAllText(NetworkPlayerInteractorScriptPath);
            var authoritativeMethod = MethodBody(
                interactorSource,
                "private InteractionValidationResult TryDeployDoorJammerAuthoritative");
            var resolverMethod = MethodBody(
                interactorSource,
                "private InteractionValidationResult TryResolveDoorJammerTargetAuthoritative");

            StringAssert.Contains("NetworkId targetId", authoritativeMethod);
            StringAssert.Contains("Runner.TryFindObject(targetId", resolverMethod);
            StringAssert.Contains("targetObject.TryGetComponent(out door)", resolverMethod);
            StringAssert.DoesNotContain("GetLocalDetectionRay()", authoritativeMethod);
            StringAssert.DoesNotContain("TryDetectLocalDoorJammerTargetIntent", authoritativeMethod);
            StringAssert.DoesNotContain("Camera.main", authoritativeMethod);
            StringAssert.DoesNotContain("Camera.main", resolverMethod);
        }

        [Test]
        public void GAMEPLAY_DOOR_DoorJammerAuthorityValidatesRangeAndObstruction()
        {
            var interactorSource = File.ReadAllText(NetworkPlayerInteractorScriptPath);
            var resolverMethod = MethodBody(
                interactorSource,
                "private InteractionValidationResult TryResolveDoorJammerTargetAuthoritative");
            var rangeMethod = MethodBody(
                interactorSource,
                "private bool IsDoorJammerTargetInAuthoritativeRange");
            var obstructionMethod = MethodBody(
                interactorSource,
                "private bool HasUnobstructedDoorJammerInteraction");

            StringAssert.Contains("return InteractionValidationResult.InvalidTarget;", resolverMethod);
            StringAssert.Contains("return InteractionValidationResult.OutOfRange;", resolverMethod);
            StringAssert.Contains("_localDetectionDistance * _localDetectionDistance", rangeMethod);
            StringAssert.Contains("GetClosestDoorInteractionPoint(door, playerPosition)", rangeMethod);
            StringAssert.Contains("GetAuthoritativeInteractionOrigin()", obstructionMethod);
            StringAssert.Contains("Physics.RaycastAll", obstructionMethod);
            StringAssert.Contains("Array.Sort", obstructionMethod);
            StringAssert.Contains("IsSelfCollider(hitCollider)", obstructionMethod);
            StringAssert.Contains("hitCollider.GetComponentInParent<NetworkSlidingDoor>() == door", obstructionMethod);
        }

        [Test]
        public void GAMEPLAY_DOOR_DoorJammerConsumesToolOnlyAfterSuccessfulSpawnAndAttach()
        {
            var interactorSource = File.ReadAllText(NetworkPlayerInteractorScriptPath);
            var deployMethod = MethodBody(
                interactorSource,
                "private InteractionValidationResult TryDeployDoorJammerAuthoritative");

            var spawnIndex = deployMethod.IndexOf("Runner.Spawn(prefab, position, rotation)", StringComparison.Ordinal);
            var attachIndex = deployMethod.IndexOf("door.TryAttachJammerAuthoritative(jammer)", StringComparison.Ordinal);
            var consumeIndex = deployMethod.IndexOf("ConsumeGameplayTeamTool(state)", StringComparison.Ordinal);

            Assert.That(spawnIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(attachIndex, Is.GreaterThan(spawnIndex));
            Assert.That(consumeIndex, Is.GreaterThan(attachIndex));
        }

        [Test]
        public void GAMEPLAY_DOOR_OtherTeamToolsStillUseDefaultTargetIntent()
        {
            var interactorSource = File.ReadAllText(NetworkPlayerInteractorScriptPath);

            StringAssert.Contains("var targetId = default(NetworkId);", interactorSource);
            StringAssert.Contains("if (playerState != null", interactorSource);
            StringAssert.Contains("playerState.ToolId == 4", interactorSource);
            StringAssert.Contains("RpcRequestUseTeamTool(NextSequence(), targetId)", interactorSource);
            StringAssert.Contains("if (toolType == \"NOISE_MAKER\")", interactorSource);
        }

        [Test]
        public void GAMEPLAY_DOOR_PlayerNetworkMayKeepNullRayOrigin()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerNetwork.prefab");
            Assert.That(prefab, Is.Not.Null);

            var interactor = GetComponentByTypeName(prefab, "EchoProtocol.Networking.NetworkPlayerInteractor");
            Assert.That(interactor, Is.Not.Null);

            var serializedInteractor = new SerializedObject(interactor);
            Assert.That(serializedInteractor.FindProperty("_rayOrigin")?.objectReferenceValue, Is.Null);
        }

        [Test]
        public void GAMEPLAY_DOOR_JammerInitializationDoesNotRebindActiveDoor()
        {
            var source = File.ReadAllText(NetworkDoorJammerScriptPath);

            StringAssert.Contains("case NetworkDoorJammerState.NotDeployed:", source);
            StringAssert.Contains("DoorId = doorId;", source);
            StringAssert.Contains("case NetworkDoorJammerState.Active:", source);
            StringAssert.Contains("return DoorId == doorId;", source);
            StringAssert.Contains("case NetworkDoorJammerState.Destroyed:", source);
            StringAssert.Contains("return false;", source);
        }

        [Test]
        public void GAMEPLAY_DOOR_JammerDestroyedPresentationIsNonBlockingAndHidden()
        {
            var source = File.ReadAllText(NetworkDoorJammerScriptPath);

            StringAssert.Contains("_blockingCollider.enabled = BlocksTraversal;", source);
            StringAssert.Contains("_blockingCollider.isTrigger = false;", source);
            StringAssert.Contains("_visualRoot.gameObject.SetActive(State == NetworkDoorJammerState.Active);", source);
        }

        [Test]
        public void GAMEPLAY_DOOR_JammerPrefabExistsAndHasRequiredRuntimeComponents()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorJammerPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            foreach (var component in prefab.GetComponentsInChildren<Component>(true))
            {
                Assert.That(component, Is.Not.Null, "Door jammer prefab must not contain missing scripts.");
            }

            var networkObject = GetComponentByTypeName(prefab, "Fusion.NetworkObject");
            var jammer = GetComponentByTypeName(prefab, "EchoProtocol.Networking.NetworkDoorJammer");
            Assert.That(networkObject, Is.Not.Null);
            Assert.That(jammer, Is.Not.Null);

            var blocker = prefab.GetComponent<BoxCollider>();
            Assert.That(blocker, Is.Not.Null);
            Assert.That(blocker.isTrigger, Is.False);

            var serializedJammer = new SerializedObject(jammer);
            Assert.That(serializedJammer.FindProperty("_blockingCollider")?.objectReferenceValue, Is.SameAs(blocker));
            Assert.That(serializedJammer.FindProperty("_visualRoot")?.objectReferenceValue, Is.Not.Null);

            var serializedNetworkObject = new SerializedObject(networkObject);
            var behaviours = serializedNetworkObject.FindProperty("NetworkedBehaviours");
            Assert.That(behaviours, Is.Not.Null);
            Assert.That(SerializedReferenceArrayContains(behaviours, jammer), Is.True);
        }

        [Test]
        public void GAMEPLAY_DOOR_BreakingDoorDeactivatesLeftAndRightPanels()
        {
            var door = CreateOfflineSlidingDoor(startsLocked: false, out var blocker);
            var leftField = door.GetType().GetField("_leftDoor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var rightField = door.GetType().GetField("_rightDoor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var leftDoor = ((Transform)leftField.GetValue(door)).gameObject;
            var rightDoor = ((Transform)rightField.GetValue(door)).gameObject;

            try
            {
                Assert.That(leftDoor.activeSelf, Is.True);
                Assert.That(rightDoor.activeSelf, Is.True);
                Assert.That(blocker.enabled, Is.True);

                InvokeBool(door, "TryBreakAuthoritative");

                Assert.That(GetBoolProperty(door, "IsBroken"), Is.True);
                Assert.That(leftDoor.activeSelf, Is.False);
                Assert.That(rightDoor.activeSelf, Is.False);
                Assert.That(blocker.enabled, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(door.gameObject);
            }
        }

        [Test]
        public void GAMEPLAY_DOOR_PrefabHasAudioAndJammerMountConfigured()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            var slidingDoor = GetComponentByTypeName(prefab, "EchoProtocol.Networking.NetworkSlidingDoor");
            Assert.That(slidingDoor, Is.Not.Null);

            var serializedDoor = new SerializedObject(slidingDoor);
            Assert.That(serializedDoor.FindProperty("_jammerMount")?.objectReferenceValue, Is.Not.Null);
            Assert.That(serializedDoor.FindProperty("_doorJammerPrefab")?.objectReferenceValue, Is.Not.Null);
            Assert.That(serializedDoor.FindProperty("_doorBreakClip")?.objectReferenceValue, Is.Not.Null);
            Assert.That(serializedDoor.FindProperty("_jammerDeployClip")?.objectReferenceValue, Is.Not.Null);
        }

        [Test]
        public void GAMEPLAY_DOOR_JammerPrefabHasPlankVisualsAndBreakClip()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorJammerPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            var jammer = GetComponentByTypeName(prefab, "EchoProtocol.Networking.NetworkDoorJammer");
            Assert.That(jammer, Is.Not.Null);

            var serializedJammer = new SerializedObject(jammer);
            Assert.That(serializedJammer.FindProperty("_breakClip")?.objectReferenceValue, Is.Not.Null);

            var visualRoot = serializedJammer.FindProperty("_visualRoot")?.objectReferenceValue as Transform;
            Assert.That(visualRoot, Is.Not.Null);
            Assert.That(visualRoot.childCount, Is.GreaterThanOrEqualTo(3), "VisualRoot must contain at least 3 planks");
        }

        [Test]
        public void GAMEPLAY_DOOR_PlankItemDefinitionAndPickupAreConfigured()
        {
            var itemDef = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/ScriptableObjects/Inventory/TeamTools/SO_Plank_ItemDefinition.asset");
            Assert.That(itemDef, Is.Not.Null);
            var so = new SerializedObject(itemDef);
            Assert.That(so.FindProperty("itemId")?.stringValue, Is.EqualTo("plank"));
            Assert.That(so.FindProperty("itemType")?.intValue, Is.EqualTo(1));

            var pickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Imported/PF_Plank_Imported.prefab")
                ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tools/PF_Plank_Pickup.prefab");
            Assert.That(pickupPrefab, Is.Not.Null);
            var pickup = GetComponentByTypeName(pickupPrefab, "EchoProtocol.Tools.Scanner.NetworkToolPickup");
            Assert.That(pickup, Is.Not.Null);

            var serializedPickup = new SerializedObject(pickup);
            Assert.That(serializedPickup.FindProperty("_toolId")?.intValue, Is.EqualTo(4));
            Assert.That(serializedPickup.FindProperty("_toolItemDefinition")?.objectReferenceValue, Is.SameAs(itemDef));
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

        private static int EnumValue(Type type, string name)
        {
            return (int)Enum.Parse(type, name);
        }

        private static string MethodBody(string source, string signaturePrefix)
        {
            var start = source.IndexOf(signaturePrefix, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing method '{signaturePrefix}'.");

            var braceStart = source.IndexOf('{', start);
            Assert.That(braceStart, Is.GreaterThanOrEqualTo(0), $"Missing method body for '{signaturePrefix}'.");

            var depth = 0;
            for (var i = braceStart; i < source.Length; i++)
            {
                if (source[i] == '{')
                {
                    depth++;
                }
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return source.Substring(start, i - start + 1);
                    }
                }
            }

            Assert.Fail($"Unterminated method body for '{signaturePrefix}'.");
            return string.Empty;
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

        private static Component CreateOfflineSlidingDoor(bool startsLocked, out BoxCollider blocker)
        {
            var root = new GameObject("OfflineSlidingDoorTest");
            root.SetActive(false);
            blocker = root.AddComponent<BoxCollider>();
            var leftDoor = new GameObject("LeftDoor").transform;
            leftDoor.SetParent(root.transform);
            var rightDoor = new GameObject("RightDoor").transform;
            rightDoor.SetParent(root.transform);

            var doorType = ResolveProductionType("EchoProtocol.Networking.NetworkSlidingDoor");
            var door = root.AddComponent(doorType);
            SetPrivateField(door, "_blockingCollider", blocker);
            SetPrivateField(door, "_leftDoor", leftDoor);
            SetPrivateField(door, "_rightDoor", rightDoor);
            SetPrivateField(door, "_startsLocked", startsLocked);
            root.SetActive(true);
            return door;
        }

        private static bool InvokeBool(Component target, string methodName)
        {
            return (bool)target.GetType()
                .GetMethod(methodName)
                .Invoke(target, Array.Empty<object>());
        }

        private static bool InvokeBool(Component target, string methodName, bool value)
        {
            return (bool)target.GetType()
                .GetMethod(methodName, new[] { typeof(bool) })
                .Invoke(target, new object[] { value });
        }

        private static object GetProperty(Component target, string propertyName)
        {
            return target.GetType().GetProperty(propertyName).GetValue(target);
        }

        private static bool GetBoolProperty(Component target, string propertyName)
        {
            return (bool)GetProperty(target, propertyName);
        }

        private static void SetPrivateField(Component target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            field.SetValue(target, value);
        }
    }
}
