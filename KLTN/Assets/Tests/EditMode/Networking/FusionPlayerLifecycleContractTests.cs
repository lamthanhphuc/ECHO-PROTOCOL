using NUnit.Framework;
using System;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace EchoProtocol.Networking.Tests
{
    public sealed class FusionPlayerLifecycleContractTests
    {
        private const string RunnerPrefabPath = "Assets/Prefabs/NetworkRunner.prefab";
        private const string PlayerPrefabPath = "Assets/Prefabs/PlayerNetwork.prefab";
        private const string LifecycleScriptPath = "Assets/Scripts/Networking/FusionPlayerLifecycle.cs";
        private const string BootstrapScriptPath = "Assets/_Project/Scripts/Networking/Session/NetworkBootstrap.cs";
        private const string LobbyManagerScriptPath = "Assets/_Project/Scripts/Networking/Session/LobbyManager.cs";
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

        [Test]
        public void FND_NET_LIFECYCLE_SpawnedPlayerSurvivesNetworkSceneTransition()
        {
            var source = LoadLifecycleSource();

            StringAssert.Contains("runner.Spawn(", source);
            StringAssert.Contains("NetworkSpawnFlags.DontDestroyOnLoad", source);
            StringAssert.Contains("PlayerRuntimeIdentity", source);
            StringAssert.Contains("_entityRegistry.TryRegister(identity)", source);
            StringAssert.Contains("runner.SetPlayerObject(player, spawnedObject)", source);
        }

        [Test]
        public void FND_NET_LIFECYCLE_RemainsHostOnlyPlayerObjectLifecycleOwner()
        {
            var source = LoadLifecycleSource();

            StringAssert.Contains("private bool CanMutateLifecycle(NetworkRunner runner)", source);
            StringAssert.Contains("runner == _runner", source);
            StringAssert.Contains("runner.IsServer", source);
            StringAssert.Contains("FPL|JOIN_REJECT", source);
            StringAssert.Contains("reason=Authority", source);
        }

        [Test]
        public void FND_NET_LIFECYCLE_CommittedNotificationFiresOnlyAfterTransactionalCommit()
        {
            var source = LoadLifecycleSource().Replace("\r\n", "\n");

            StringAssert.Contains("public event Action<FusionPlayerObjectCommit> PlayerObjectCommitted", source);
            StringAssert.Contains("new FusionPlayerObjectCommit(player, spawnedObject, playerId)", source);
            StringAssert.Contains("NotifyPlayerObjectCommitted(commit)", source);
            StringAssert.DoesNotContain("PlayerObjectCommitted?.Invoke", source);

            var spawnIndex = source.IndexOf("spawnedObject = runner.Spawn(", System.StringComparison.Ordinal);
            var bindIndex = source.IndexOf("identity.TryBind(playerId)", System.StringComparison.Ordinal);
            var registerIndex = source.IndexOf("_entityRegistry.TryRegister(identity)", System.StringComparison.Ordinal);
            var setPlayerObjectIndex = source.IndexOf("runner.SetPlayerObject(player, spawnedObject)", System.StringComparison.Ordinal);
            var verifyIndex = source.IndexOf("Runner.SetPlayerObject did not commit", System.StringComparison.Ordinal);
            var catchIndex = source.IndexOf("catch (Exception ex)", System.StringComparison.Ordinal);
            var rollbackIndex = source.IndexOf("RollbackJoin(runner, player, playerId, identity, spawnedObject, entityRegistered, playerObjectCommitted)", System.StringComparison.Ordinal);
            var catchReturnIndex = source.IndexOf("return;\n            }\n\n            var commit", System.StringComparison.Ordinal);
            var commitLogIndex = source.IndexOf("FPL|JOIN_COMMIT", System.StringComparison.Ordinal);
            var notifyIndex = source.IndexOf("NotifyPlayerObjectCommitted(commit)", System.StringComparison.Ordinal);

            Assert.That(spawnIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(bindIndex, Is.GreaterThan(spawnIndex));
            Assert.That(registerIndex, Is.GreaterThan(bindIndex));
            Assert.That(setPlayerObjectIndex, Is.GreaterThan(registerIndex));
            Assert.That(verifyIndex, Is.GreaterThan(setPlayerObjectIndex));
            Assert.That(catchIndex, Is.GreaterThan(verifyIndex));
            Assert.That(rollbackIndex, Is.GreaterThan(catchIndex));
            Assert.That(catchReturnIndex, Is.GreaterThan(rollbackIndex));
            Assert.That(commitLogIndex, Is.GreaterThan(catchReturnIndex));
            Assert.That(notifyIndex, Is.GreaterThan(commitLogIndex));
        }

        [Test]
        public void FND_NET_LIFECYCLE_CommittedObserverExceptionsAreIsolatedPerSubscriber()
        {
            var root = new GameObject("FND_NET_LIFECYCLE_ObserverIsolation");
            try
            {
                var lifecycle = root.AddComponent(ResolveType(LifecycleTypeName));
                var commitType = ResolveType("EchoProtocol.Networking.FusionPlayerObjectCommit");
                var actionType = typeof(Action<>).MakeGenericType(commitType);
                var secondSubscriberCalled = new StrongBox<bool>(false);

                var playerObjectCommitted = lifecycle.GetType().GetEvent("PlayerObjectCommitted");
                Assert.That(playerObjectCommitted, Is.Not.Null);
                playerObjectCommitted.AddEventHandler(lifecycle, CreateThrowingCommitObserver(actionType, commitType));
                playerObjectCommitted.AddEventHandler(lifecycle, CreateRecordingCommitObserver(actionType, commitType, secondSubscriberCalled));

                LogAssert.Expect(
                    LogType.Error,
                    new Regex("FPL\\|COMMIT_OBSERVER_ERROR\\|.*observer=.*InvalidOperationException:observer boom"));

                Assert.DoesNotThrow(() => InvokePrivateMethod(
                    lifecycle,
                    "NotifyPlayerObjectCommitted",
                    new[] { commitType },
                    new[] { CreateCommitValue(commitType) }));
                Assert.That(secondSubscriberCalled.Value, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FND_NET_LIFECYCLE_RollbackStillOwnsPlayerObjectCleanup()
        {
            var source = LoadLifecycleSource();

            StringAssert.Contains("RollbackJoin", source);
            StringAssert.Contains("_entityRegistry.Unregister", source);
            StringAssert.Contains("identity.ClearBinding()", source);
            StringAssert.Contains("TryClearPlayerObject(runner, player)", source);
            StringAssert.Contains("runner.Despawn(spawnedObject)", source);
            StringAssert.Contains("_identityRegistry.Unregister(player)", source);
        }

        [Test]
        public void FND_NET_LIFECYCLE_ReconnectRequiresExactToken()
        {
            var lifecycleType = ResolveType(LifecycleTypeName);
            var matches = lifecycleType.GetMethod("TokensMatch", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(matches, Is.Not.Null);
            Assert.That(matches.Invoke(null, new object[] { new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 3 } }), Is.True);
            Assert.That(matches.Invoke(null, new object[] { new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 4 } }), Is.False);
            Assert.That(matches.Invoke(null, new object[] { new byte[] { 1, 2, 3 }, new byte[] { 1, 2 } }), Is.False);
        }

        [Test]
        public void HOST_MIGRATION_InputLifecycleUsesFusionAuthorityCallbacks()
        {
            var movementType = ResolveType(
                "EchoProtocol.Networking.NetworkPlayerMovement");

            Assert.That(
                ResolveType("Fusion.IInputAuthorityGained")
                    .IsAssignableFrom(movementType),
                Is.True);

            Assert.That(
                ResolveType("Fusion.IInputAuthorityLost")
                    .IsAssignableFrom(movementType),
                Is.True);
        }

        [Test]
        public void HOST_MIGRATION_ResumeSnapshotAccessIsServerGuarded()
        {
            var source = File.ReadAllText(BootstrapScriptPath)
                .Replace("\r\n", "\n");

            var resumeStart = source.IndexOf(
                "private void ResumeHostMigration(",
                StringComparison.Ordinal);

            var snapshotAccess = source.IndexOf(
                "GetResumeSnapshotNetworkObjectPlayerObjects()",
                resumeStart,
                StringComparison.Ordinal);

            var serverGuard = source.IndexOf(
                "|| !migrationRunner.IsServer",
                resumeStart,
                StringComparison.Ordinal);

            Assert.That(resumeStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(serverGuard, Is.GreaterThan(resumeStart));
            Assert.That(snapshotAccess, Is.GreaterThan(serverGuard));
        }

        [Test]
        public void HOST_MIGRATION_RestoresOnlyCanonicalMatchStateNetworkId()
        {
            var source = File.ReadAllText(BootstrapScriptPath);

            StringAssert.Contains(
                "_hostMigrationAuthoritativeMatchStateId",
                source);

            StringAssert.Contains(
                "resumeObject.Id != canonicalMatchStateId",
                source);

            StringAssert.Contains(
                "Stale migrated NetworkMatchState snapshot ignored",
                source);
        }

        [Test]
        public void HOST_MIGRATION_MatchStartRequiresCurrentCommittedRoster()
        {
            var source = File.ReadAllText(LobbyManagerScriptPath);

            StringAssert.Contains(
                "TryValidateCommittedRoster(",
                source);

            StringAssert.Contains(
                "playerObject.InputAuthority != player",
                source);

            StringAssert.Contains(
                "lobbyState.FusionActorNumber != actorNumber",
                source);

            StringAssert.Contains(
                "authority.IsPlayerBound(",
                source);
        }

        [Test]
        public void HOST_MIGRATION_GracefulHostLeavePushesSnapshot()
        {
            var source = File.ReadAllText(BootstrapScriptPath);

            StringAssert.Contains(
                "PushGracefulHostMigrationSnapshotAsync(",
                source);

            StringAssert.Contains(
                "ShutdownReason.HostMigration",
                source);
        }

        [Test]
        public void HOST_MIGRATION_ReclaimsPlayerObjectsByStableConnectionToken()
        {
            var source = File.ReadAllText(BootstrapScriptPath);

            StringAssert.Contains(
                "FusionPlayerLifecycle.ReconnectTokenMatches(",
                source);

            StringAssert.Contains(
                "Migrated PlayerObject reconciled by connection token",
                source);
        }

        [Test]
        public void HOST_MIGRATION_RepositionsLobbyPlayersIntoUniqueSlots()
        {
            var source = File.ReadAllText(
                "Assets/_Project/Scripts/Networking/Player/PlayerSpawner.cs");

            StringAssert.Contains(
                "RebuildMigratedLobbyPlacementAuthoritative(",
                source);

            StringAssert.Contains(
                "PSP|MIGRATION_LOBBY_PLACEMENT",
                source);
        }

        [Test]
        public void HOST_MIGRATION_BackendCleanupNeverDisconnectsReusedActiveActor()
        {
            var source = File.ReadAllText(
                "Assets/_Project/Scripts/Networking/Authority/MatchAuthorityRuntime.cs");

            var reconcileStart = source.IndexOf(
                "ReconcileMigratedDisconnectedActorsAsync(",
                StringComparison.Ordinal);

            var activeGuard = source.IndexOf(
                "_boundPlayers.ContainsKey(",
                reconcileStart,
                StringComparison.Ordinal);

            var disconnectCall = source.IndexOf(
                "await _api.DisconnectPlayerAsync(",
                reconcileStart,
                StringComparison.Ordinal);

            Assert.That(reconcileStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(activeGuard, Is.GreaterThan(reconcileStart));
            Assert.That(disconnectCall, Is.GreaterThan(activeGuard));
        }

        [Test]
        public void HOST_MIGRATION_NewHostBindingDoesNotDependOnReplicatedIdentityTiming()
        {
            var authoritySource = File.ReadAllText(
                "Assets/_Project/Scripts/Networking/Authority/MatchAuthorityRuntime.cs");
            var bootstrapSource = File.ReadAllText(BootstrapScriptPath);

            StringAssert.Contains(
                "player == runner.LocalPlayer",
                authoritySource);
            StringAssert.Contains(
                "AuthSession.CurrentUserId",
                authoritySource);
            StringAssert.Contains(
                "pendingIdentityCount++",
                authoritySource);
            StringAssert.Contains(
                "bindingRebuildDeadline",
                bootstrapSource);
        }

        [Test]
        public void JOIN_PROOF_RetryStopsOnlyAfterStateAuthorityConfirmsBinding()
        {
            var authoritySource = File.ReadAllText(
                "Assets/_Project/Scripts/Networking/Authority/MatchAuthorityRuntime.cs");
            var playerStateSource = File.ReadAllText(
                "Assets/_Project/Scripts/Networking/Player/LobbyPlayerState.cs");

            StringAssert.DoesNotContain(
                "_bootstrap\n                ?.MarkReconnectProofSubmitted();",
                authoritySource);
            StringAssert.Contains(
                "RpcConfirmBackendIdentity(",
                playerStateSource);
            StringAssert.Contains(
                "MarkReconnectProofSubmitted();",
                playerStateSource);
        }

        [Test]
        public void MATCH_LOOP_QueuedResultForcesFreshBackendMatch()
        {
            var source = File.ReadAllText(
                "Assets/_Project/Scripts/Networking/Authority/MatchAuthorityRuntime.cs");

            StringAssert.Contains(
                "|| HasQueuedResult(MatchId);",
                source);
        }

        private static GameObject LoadPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, $"Missing prefab at {path}.");
            return prefab;
        }

        private static string LoadLifecycleSource()
        {
            Assert.That(File.Exists(LifecycleScriptPath), Is.True);
            return File.ReadAllText(LifecycleScriptPath);
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

        private static object InvokePrivateMethod(object target, string methodName, Type[] parameterTypes, object[] args)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}' on '{target.GetType().FullName}'.");
            return method.Invoke(target, args);
        }

        private static Delegate CreateThrowingCommitObserver(Type actionType, Type commitType)
        {
            var commit = Expression.Parameter(commitType, "commit");
            var exception = Expression.New(
                typeof(InvalidOperationException).GetConstructor(new[] { typeof(string) }),
                Expression.Constant("observer boom"));
            return Expression.Lambda(actionType, Expression.Throw(exception), commit).Compile();
        }

        private static Delegate CreateRecordingCommitObserver(
            Type actionType,
            Type commitType,
            StrongBox<bool> secondSubscriberCalled)
        {
            var commit = Expression.Parameter(commitType, "commit");
            var assign = Expression.Assign(
                Expression.Field(Expression.Constant(secondSubscriberCalled), nameof(StrongBox<bool>.Value)),
                Expression.Constant(true));
            return Expression.Lambda(actionType, assign, commit).Compile();
        }

        private static object CreateCommitValue(Type commitType)
        {
            var playerRefType = ResolveType("Fusion.PlayerRef");
            var networkObjectType = ResolveType("Fusion.NetworkObject");
            var playerIdType = ResolveType("EchoProtocol.AI.Common.PlayerId");
            var playerRef = playerRefType.GetProperty("None", BindingFlags.Static | BindingFlags.Public).GetValue(null);
            var playerId = playerIdType.GetProperty("Invalid", BindingFlags.Static | BindingFlags.Public).GetValue(null);
            return Activator.CreateInstance(commitType, playerRef, null, playerId);
        }

        private static Type ResolveType(string fullTypeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullTypeName);
                if (type != null)
                {
                    return type;
                }
            }

            Assert.Fail($"Unable to resolve type '{fullTypeName}'.");
            return null;
        }

    }
}
