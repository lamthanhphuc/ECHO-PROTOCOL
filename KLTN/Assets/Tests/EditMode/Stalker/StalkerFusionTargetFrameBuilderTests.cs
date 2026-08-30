using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerFusionTargetFrameBuilderTests
    {
        private const string BuilderTypeName = "EchoProtocol.AI.Stalker.Networking.StalkerFusionTargetFrameBuilder";
        private const string ControllerTypeName = "EchoProtocol.AI.Stalker.StalkerController";
        private const string LifecycleTypeName = "EchoProtocol.Networking.FusionPlayerLifecycle";
        private const string PlayerIdTypeName = "EchoProtocol.AI.Common.PlayerId";
        private const string PlayerRefTypeName = "Fusion.PlayerRef";
        private const string PlayerRuntimeIdentityTypeName = "EchoProtocol.Player.PlayerRuntimeIdentity";
        private const string PerceptionSnapshotTypeName = "EchoProtocol.AI.Stalker.StalkerPerceptionTargetSnapshot";
        private const string TargetStatusTypeName = "EchoProtocol.AI.Stalker.StalkerTargetStatus";
        private const string EligibilityReasonTypeName = "EchoProtocol.AI.Stalker.StalkerTargetEligibilityReason";

        private readonly List<GameObject> _createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void FRAME_01_ActivePlayersAreEmittedInPlayerIdAscendingOrder()
        {
            var fixture = CreateLifecycleFixture();
            RegisterLogicalPlayer(fixture.Lifecycle, 1);
            RegisterLogicalPlayer(fixture.Lifecycle, 2);
            RegisterLogicalPlayer(fixture.Lifecycle, 3);
            RegisterEntity(fixture.Lifecycle, 3);
            RegisterEntity(fixture.Lifecycle, 1);
            RegisterEntity(fixture.Lifecycle, 2);
            var frame = CreateFrameLists();

            Assert.That(TryBuild(fixture.Lifecycle, InvalidPlayerId(), InvalidPlayerId(), frame), Is.True);

            AssertStatusPlayerIds(frame.Statuses, 1, 2, 3);
            AssertSnapshotPlayerIds(frame.PerceptionSnapshots, 1, 2, 3);
        }

        [Test]
        public void FRAME_02_ActiveStatusIsEligibleUnderPhaseTwoAdapter()
        {
            var fixture = CreateLifecycleFixture();
            RegisterActivePlayer(fixture.Lifecycle, 1);
            var frame = CreateFrameLists();

            Assert.That(TryBuild(fixture.Lifecycle, InvalidPlayerId(), InvalidPlayerId(), frame), Is.True);

            Assert.That(GetBoolProperty(GetProperty(GetListItem(frame.Statuses, 0), "Eligibility"), "Eligible"), Is.True);
        }

        [Test]
        public void FRAME_03_DisconnectedCurrentTargetIdGetsOneExplicitDisconnectedStatus()
        {
            var fixture = CreateLifecycleFixture();
            RegisterLogicalPlayer(fixture.Lifecycle, 1);
            UnregisterLogicalPlayer(fixture.Lifecycle, 1);
            RegisterActivePlayer(fixture.Lifecycle, 2);
            var frame = CreateFrameLists();

            Assert.That(TryBuild(fixture.Lifecycle, InvalidPlayerId(), CreatePlayerId(9), frame), Is.True);

            AssertStatusPlayerIds(frame.Statuses, 2, 9);
            AssertEligibilityReason(GetListItem(frame.Statuses, 1), "NotInActiveSession");
        }

        [Test]
        public void FRAME_04_DisconnectedDetectionTargetIdGetsOneExplicitDisconnectedStatus()
        {
            var fixture = CreateLifecycleFixture();
            RegisterLogicalPlayer(fixture.Lifecycle, 1);
            UnregisterLogicalPlayer(fixture.Lifecycle, 1);
            RegisterActivePlayer(fixture.Lifecycle, 2);
            var frame = CreateFrameLists();

            Assert.That(TryBuild(fixture.Lifecycle, CreatePlayerId(8), InvalidPlayerId(), frame), Is.True);

            AssertStatusPlayerIds(frame.Statuses, 2, 8);
            AssertEligibilityReason(GetListItem(frame.Statuses, 1), "NotInActiveSession");
        }

        [Test]
        public void FRAME_05_SameMissingCurrentAndDetectionTargetProducesOneDisconnectedStatus()
        {
            var fixture = CreateLifecycleFixture();
            var frame = CreateFrameLists();

            Assert.That(TryBuild(fixture.Lifecycle, CreatePlayerId(4), CreatePlayerId(4), frame), Is.True);

            AssertStatusPlayerIds(frame.Statuses, 4);
            Assert.That(GetListCount(frame.PerceptionSnapshots), Is.EqualTo(0));
        }

        [Test]
        public void FRAME_06_NoDuplicatePlayerIdStatuses()
        {
            var fixture = CreateLifecycleFixture();
            RegisterActivePlayer(fixture.Lifecycle, 1);
            var frame = CreateFrameLists();

            Assert.That(TryBuild(fixture.Lifecycle, CreatePlayerId(1), CreatePlayerId(1), frame), Is.True);

            AssertStatusPlayerIds(frame.Statuses, 1);
        }

        [Test]
        public void FRAME_07_ActiveIdMissingEntityMappingFailsAndClearsOutputs()
        {
            var fixture = CreateLifecycleFixture();
            RegisterLogicalPlayer(fixture.Lifecycle, 1);
            var frame = CreateFrameLists();

            Assert.That(TryBuild(fixture.Lifecycle, InvalidPlayerId(), InvalidPlayerId(), frame), Is.False);

            Assert.That(GetListCount(frame.Statuses), Is.EqualTo(0));
            Assert.That(GetListCount(frame.PerceptionSnapshots), Is.EqualTo(0));
        }

        [Test]
        public void FRAME_10_ActiveIdWithUnboundIdentityFailsAndClearsOutputs()
        {
            var fixture = CreateLifecycleFixture();
            RegisterActivePlayer(fixture.Lifecycle, 1);
            var entityRegistry = GetProperty(fixture.Lifecycle, "EntityRegistry");
            Assert.That(InvokeInstanceMethod(
                entityRegistry,
                "TryGetEntity",
                new[] { ResolveType(PlayerIdTypeName), ResolveType(PlayerRuntimeIdentityTypeName).MakeByRefType() },
                new object[] { CreatePlayerId(1), null }),
                Is.EqualTo(true));
            InvokeInstanceMethod(
                GetListBackedEntity(entityRegistry, 1),
                "ClearBinding",
                Type.EmptyTypes,
                Array.Empty<object>());
            var frame = CreateFrameLists();

            Assert.That(TryBuild(fixture.Lifecycle, InvalidPlayerId(), InvalidPlayerId(), frame), Is.False);

            Assert.That(GetListCount(frame.Statuses), Is.EqualTo(0));
            Assert.That(GetListCount(frame.PerceptionSnapshots), Is.EqualTo(0));
        }

        [Test]
        public void FRAME_11_ActiveIdWithMismatchedIdentityFailsAndClearsOutputs()
        {
            var fixture = CreateLifecycleFixture();
            var identity = RegisterActivePlayer(fixture.Lifecycle, 1);
            SetPrivateField(identity, "_playerId", CreatePlayerId(2));
            var frame = CreateFrameLists();

            Assert.That(TryBuild(fixture.Lifecycle, InvalidPlayerId(), InvalidPlayerId(), frame), Is.False);

            Assert.That(GetListCount(frame.Statuses), Is.EqualTo(0));
            Assert.That(GetListCount(frame.PerceptionSnapshots), Is.EqualTo(0));
        }

        [Test]
        public void FRAME_12_LegitimatelyEmptyActiveRegistrySucceedsWithEmptyTypedFrame()
        {
            var fixture = CreateLifecycleFixture();
            var frame = CreateFrameLists();

            Assert.That(TryBuild(fixture.Lifecycle, InvalidPlayerId(), InvalidPlayerId(), frame), Is.True);

            Assert.That(GetListCount(frame.Statuses), Is.EqualTo(0));
            Assert.That(GetListCount(frame.PerceptionSnapshots), Is.EqualTo(0));
        }

        [Test]
        public void FRAME_13_DisconnectedLockedIdNotInActiveRegistrySucceedsAndEmitsStatus()
        {
            var fixture = CreateLifecycleFixture();
            var frame = CreateFrameLists();

            Assert.That(TryBuild(fixture.Lifecycle, CreatePlayerId(3), InvalidPlayerId(), frame), Is.True);

            AssertStatusPlayerIds(frame.Statuses, 3);
            AssertEligibilityReason(GetListItem(frame.Statuses, 0), "NotInActiveSession");
        }

        [Test]
        public void FRAME_14_FinalStatusOrderIsGloballyPlayerIdAscendingWithMixedDisconnectedIds()
        {
            var fixture = CreateLifecycleFixture();
            RegisterLogicalPlayer(fixture.Lifecycle, 1);
            UnregisterLogicalPlayer(fixture.Lifecycle, 1);
            RegisterActivePlayer(fixture.Lifecycle, 2);
            RegisterLogicalPlayer(fixture.Lifecycle, 3);
            RegisterLogicalPlayer(fixture.Lifecycle, 4);
            UnregisterLogicalPlayer(fixture.Lifecycle, 3);
            UnregisterLogicalPlayer(fixture.Lifecycle, 4);
            RegisterActivePlayer(fixture.Lifecycle, 5);
            var frame = CreateFrameLists();

            Assert.That(TryBuild(fixture.Lifecycle, CreatePlayerId(9), CreatePlayerId(1), frame), Is.True);

            AssertStatusPlayerIds(frame.Statuses, 1, 2, 5, 9);
            AssertSnapshotPlayerIds(frame.PerceptionSnapshots, 2, 5);
        }

        [Test]
        public void FRAME_08_CallerOwnedFrameBuffersAreClearedAndRebuilt()
        {
            var fixture = CreateLifecycleFixture();
            RegisterActivePlayer(fixture.Lifecycle, 1);
            var frame = CreateFrameLists();

            Assert.That(TryBuild(fixture.Lifecycle, CreatePlayerId(5), InvalidPlayerId(), frame), Is.True);
            AssertStatusPlayerIds(frame.Statuses, 1, 5);

            RegisterActivePlayer(fixture.Lifecycle, 2);
            Assert.That(TryBuild(fixture.Lifecycle, InvalidPlayerId(), InvalidPlayerId(), frame), Is.True);

            AssertStatusPlayerIds(frame.Statuses, 1, 2);
            AssertSnapshotPlayerIds(frame.PerceptionSnapshots, 1, 2);
        }

        [Test]
        public void FRAME_09_FrameBuilderDoesNotMutateStalkerTargetMemory()
        {
            var fixture = CreateLifecycleFixture();
            RegisterLogicalPlayer(fixture.Lifecycle, 1);
            UnregisterLogicalPlayer(fixture.Lifecycle, 1);
            RegisterActivePlayer(fixture.Lifecycle, 2);
            var controller = CreateController();
            SetCurrentTarget(controller, 7);
            var frame = CreateFrameLists();

            Assert.That(TryBuild(
                fixture.Lifecycle,
                GetProperty(controller, "DetectionTargetId"),
                GetProperty(controller, "CurrentTargetId"),
                frame),
                Is.True);

            AssertInvalidPlayerId(GetProperty(controller, "DetectionTargetId"));
            AssertPlayerIdValue(GetProperty(controller, "CurrentTargetId"), 7);
        }

        private Component CreateController()
        {
            var root = new GameObject("FRAME_Controller");
            _createdObjects.Add(root);
            return (Component)root.AddComponent(ResolveType(ControllerTypeName));
        }

        private LifecycleFixture CreateLifecycleFixture()
        {
            var root = new GameObject("FRAME_Lifecycle");
            _createdObjects.Add(root);
            var lifecycle = root.AddComponent(ResolveType(LifecycleTypeName));
            return new LifecycleFixture(lifecycle);
        }

        private Component RegisterActivePlayer(Component lifecycle, int playerIdValue)
        {
            RegisterLogicalPlayer(lifecycle, playerIdValue);
            return RegisterEntity(lifecycle, playerIdValue);
        }

        private Component RegisterEntity(Component lifecycle, int playerIdValue)
        {
            var identity = CreateIdentity($"FRAME_Player_{playerIdValue}", playerIdValue);
            var entityRegistry = GetProperty(lifecycle, "EntityRegistry");
            Assert.That(InvokeInstanceMethod(
                entityRegistry,
                "TryRegister",
                new[] { ResolveType(PlayerRuntimeIdentityTypeName) },
                new object[] { identity }),
                Is.EqualTo(true));
            return identity;
        }

        private void RegisterLogicalPlayer(Component lifecycle, int playerRefIndex)
        {
            var identityRegistry = GetProperty(lifecycle, "IdentityRegistry");
            var args = new[] { CreatePlayerRef(playerRefIndex), null };
            Assert.That(InvokeInstanceMethod(
                identityRegistry,
                "TryRegister",
                new[] { ResolveType(PlayerRefTypeName), ResolveType(PlayerIdTypeName).MakeByRefType() },
                args),
                Is.EqualTo(true));
        }

        private static void UnregisterLogicalPlayer(Component lifecycle, int playerRefIndex)
        {
            var identityRegistry = GetProperty(lifecycle, "IdentityRegistry");
            Assert.That(InvokeInstanceMethod(
                identityRegistry,
                "Unregister",
                new[] { ResolveType(PlayerRefTypeName) },
                new[] { CreatePlayerRef(playerRefIndex) }),
                Is.EqualTo(true));
        }

        private Component CreateIdentity(string name, int playerIdValue)
        {
            var root = new GameObject(name);
            _createdObjects.Add(root);
            var identity = (Component)root.AddComponent(ResolveType(PlayerRuntimeIdentityTypeName));
            Assert.That(InvokeInstanceMethod(
                identity,
                "TryBind",
                new[] { ResolveType(PlayerIdTypeName) },
                new[] { CreatePlayerId(playerIdValue) }),
                Is.EqualTo(true));
            return identity;
        }

        private static bool TryBuild(Component lifecycle, object detectionTargetId, object currentTargetId, FrameLists frame)
        {
            var builder = Activator.CreateInstance(ResolveType(BuilderTypeName));
            return (bool)InvokeInstanceMethod(
                builder,
                "TryBuild",
                new[]
                {
                    ResolveType(LifecycleTypeName),
                    ResolveType(PlayerIdTypeName),
                    ResolveType(PlayerIdTypeName),
                    typeof(List<>).MakeGenericType(ResolveType(PerceptionSnapshotTypeName)),
                    typeof(List<>).MakeGenericType(ResolveType(TargetStatusTypeName))
                },
                new[] { lifecycle, detectionTargetId, currentTargetId, frame.PerceptionSnapshots, frame.Statuses });
        }

        private static FrameLists CreateFrameLists()
        {
            return new FrameLists(
                Activator.CreateInstance(typeof(List<>).MakeGenericType(ResolveType(PerceptionSnapshotTypeName))),
                Activator.CreateInstance(typeof(List<>).MakeGenericType(ResolveType(TargetStatusTypeName))));
        }

        private static object CreatePlayerRef(int index)
        {
            return ResolveType(PlayerRefTypeName)
                .GetMethod("FromIndex", BindingFlags.Static | BindingFlags.Public)
                .Invoke(null, new object[] { index });
        }

        private static object CreatePlayerId(int value)
        {
            return Activator.CreateInstance(ResolveType(PlayerIdTypeName), value);
        }

        private static object InvalidPlayerId()
        {
            return ResolveType(PlayerIdTypeName)
                .GetProperty("Invalid", BindingFlags.Static | BindingFlags.Public)
                .GetValue(null);
        }

        private static void SetCurrentTarget(Component controller, int playerId)
        {
            var memory = GetPrivateField(controller, "_memory");
            InvokeInstanceMethod(memory, "SetCurrentTarget", new[] { ResolveType(PlayerIdTypeName) }, new[] { CreatePlayerId(playerId) });
        }

        private static object GetListBackedEntity(object entityRegistry, int playerId)
        {
            var args = new object[] { CreatePlayerId(playerId), null };
            Assert.That(InvokeInstanceMethod(
                entityRegistry,
                "TryGetEntity",
                new[] { ResolveType(PlayerIdTypeName), ResolveType(PlayerRuntimeIdentityTypeName).MakeByRefType() },
                args),
                Is.EqualTo(true));
            return args[1];
        }

        private static void AssertStatusPlayerIds(object statuses, params int[] expected)
        {
            Assert.That(GetListCount(statuses), Is.EqualTo(expected.Length));
            for (var i = 0; i < expected.Length; i++)
            {
                AssertPlayerIdValue(GetProperty(GetListItem(statuses, i), "PlayerId"), expected[i]);
            }
        }

        private static void AssertSnapshotPlayerIds(object snapshots, params int[] expected)
        {
            Assert.That(GetListCount(snapshots), Is.EqualTo(expected.Length));
            for (var i = 0; i < expected.Length; i++)
            {
                AssertPlayerIdValue(GetProperty(GetListItem(snapshots, i), "PlayerId"), expected[i]);
            }
        }

        private static void AssertEligibilityReason(object status, string expected)
        {
            Assert.That(GetProperty(GetProperty(status, "Eligibility"), "Reason").ToString(), Is.EqualTo(expected));
        }

        private static object InvokeInstanceMethod(object target, string methodName, Type[] parameterTypes, object[] args)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null, $"Missing method '{methodName}' on '{target.GetType().FullName}'.");
            return method.Invoke(target, args);
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return field.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private static object GetListItem(object list, int index)
        {
            return list.GetType().GetProperty("Item").GetValue(list, new object[] { index });
        }

        private static int GetListCount(object list)
        {
            return (int)list.GetType().GetProperty("Count").GetValue(list);
        }

        private static bool GetBoolProperty(object target, string propertyName)
        {
            return (bool)GetProperty(target, propertyName);
        }

        private static object GetProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing public property '{propertyName}' on '{target.GetType().FullName}'.");
            return property.GetValue(target);
        }

        private static void AssertPlayerIdValue(object playerId, int expectedValue)
        {
            Assert.That(GetBoolProperty(playerId, "IsValid"), Is.True);
            Assert.That(GetProperty(playerId, "Value"), Is.EqualTo(expectedValue));
        }

        private static void AssertInvalidPlayerId(object playerId)
        {
            Assert.That(GetBoolProperty(playerId, "IsValid"), Is.False);
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

        private readonly struct LifecycleFixture
        {
            public LifecycleFixture(Component lifecycle)
            {
                Lifecycle = lifecycle;
            }

            public Component Lifecycle { get; }
        }

        private readonly struct FrameLists
        {
            public FrameLists(object perceptionSnapshots, object statuses)
            {
                PerceptionSnapshots = perceptionSnapshots;
                Statuses = statuses;
            }

            public object PerceptionSnapshots { get; }

            public object Statuses { get; }
        }
    }
}
