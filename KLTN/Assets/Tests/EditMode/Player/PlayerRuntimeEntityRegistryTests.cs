using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.Player.Tests
{
    public sealed class PlayerRuntimeEntityRegistryTests
    {
        private const string PlayerIdTypeName = "EchoProtocol.AI.Common.PlayerId";
        private const string IdentityTypeName = "EchoProtocol.Player.PlayerRuntimeIdentity";
        private const string RegistryTypeName = "EchoProtocol.Player.PlayerRuntimeEntityRegistry";

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
        public void FND_PLAYER_ID_DefaultIdentity_IsUnbound()
        {
            var identity = CreateIdentity("FND_Player_Default");

            Assert.That(GetBoolProperty(identity, "IsBound"), Is.False);
            Assert.That(IsValidPlayerId(GetProperty(identity, "PlayerId")), Is.False);
            Assert.That(GetTransformProperty(identity, "EntityRoot"), Is.SameAs(((Component)identity).transform));
            Assert.That(GetTransformProperty(identity, "VisionTargetPoint"), Is.SameAs(((Component)identity).transform));
        }

        [Test]
        public void FND_PLAYER_ID_InvalidBinding_IsRejected()
        {
            var identity = CreateIdentity("FND_Player_InvalidBind");

            Assert.That(TryBind(identity, GetStaticProperty(ResolveType(PlayerIdTypeName), "Invalid")), Is.False);

            Assert.That(GetBoolProperty(identity, "IsBound"), Is.False);
            Assert.That(IsValidPlayerId(GetProperty(identity, "PlayerId")), Is.False);
        }

        [Test]
        public void FND_PLAYER_ID_Binding_IsIdempotentButCannotBeOverwritten()
        {
            var identity = CreateIdentity("FND_Player_Binding");
            var playerOne = CreatePlayerId(1);
            var playerTwo = CreatePlayerId(2);

            Assert.That(TryBind(identity, playerOne), Is.True);
            Assert.That(TryBind(identity, playerOne), Is.True);
            Assert.That(TryBind(identity, playerTwo), Is.False);
            AssertPlayerIdValue(GetProperty(identity, "PlayerId"), 1);

            InvokeInstanceMethod(identity, "ClearBinding", Type.EmptyTypes, Array.Empty<object>());

            Assert.That(GetBoolProperty(identity, "IsBound"), Is.False);
            Assert.That(IsValidPlayerId(GetProperty(identity, "PlayerId")), Is.False);
        }

        [Test]
        public void FND_PLAYER_ENTITY_RegisteredIdentity_ResolvesByPlayerId()
        {
            var registry = CreateRegistry();
            var identity = CreateBoundIdentity("FND_Player_Registered", 2);

            Assert.That(TryRegister(registry, identity), Is.True);

            Assert.That(GetIntProperty(registry, "Count"), Is.EqualTo(1));
            Assert.That(TryGetEntity(registry, CreatePlayerId(2), out var resolved), Is.True);
            Assert.That(resolved, Is.SameAs(identity));
        }

        [Test]
        public void FND_PLAYER_ENTITY_DuplicatePlayerIdEntity_IsRejected()
        {
            var registry = CreateRegistry();
            var identityA = CreateBoundIdentity("FND_Player_Duplicate_A", 1);
            var identityB = CreateBoundIdentity("FND_Player_Duplicate_B", 1);

            Assert.That(TryRegister(registry, identityA), Is.True);
            Assert.That(TryRegister(registry, identityB), Is.False);

            Assert.That(TryGetEntity(registry, CreatePlayerId(1), out var resolved), Is.True);
            Assert.That(resolved, Is.SameAs(identityA));
            Assert.That(GetIntProperty(registry, "Count"), Is.EqualTo(1));
        }

        [Test]
        public void FND_PLAYER_ENTITY_RootAndChildTransforms_ResolveRegisteredPlayerId()
        {
            var registry = CreateRegistry();
            var identity = CreateBoundIdentity("FND_Player_TransformResolve", 3);
            var child = CreateChild(((Component)identity).transform, "Child");
            Assert.That(TryRegister(registry, identity), Is.True);

            Assert.That(TryResolvePlayerId(registry, ((Component)identity).transform, out var rootId), Is.True);
            AssertPlayerIdValue(rootId, 3);
            Assert.That(TryResolvePlayerId(registry, child, out var childId), Is.True);
            AssertPlayerIdValue(childId, 3);

            var unregistered = CreateBoundIdentity("FND_Player_UnregisteredResolve", 4);
            var unregisteredChild = CreateChild(((Component)unregistered).transform, "Child");
            Assert.That(TryResolvePlayerId(registry, ((Component)unregistered).transform, out var missingRootId), Is.False);
            Assert.That(IsValidPlayerId(missingRootId), Is.False);
            Assert.That(TryResolvePlayerId(registry, unregisteredChild, out var missingChildId), Is.False);
            Assert.That(IsValidPlayerId(missingChildId), Is.False);
        }

        [Test]
        public void FND_PLAYER_ENTITY_ExplicitVisionTargetPoint_UsesChildAndResolvesIdentity()
        {
            var registry = CreateRegistry();
            var identity = CreateBoundIdentity("FND_Player_VisionTarget", 5);
            var visionTarget = CreateChild(((Component)identity).transform, "VisionTarget");
            SetPrivateField(identity, "visionTargetPoint", visionTarget);
            Assert.That(TryRegister(registry, identity), Is.True);

            Assert.That(GetTransformProperty(identity, "VisionTargetPoint"), Is.SameAs(visionTarget));
            Assert.That(TryResolvePlayerId(registry, visionTarget, out var resolvedId), Is.True);
            AssertPlayerIdValue(resolvedId, 5);
        }

        [Test]
        public void FND_PLAYER_ENTITY_Unregister_RemovesEntityAndTransformResolution()
        {
            var registry = CreateRegistry();
            var identity = CreateBoundIdentity("FND_Player_Unregister", 6);
            var child = CreateChild(((Component)identity).transform, "Child");
            Assert.That(TryRegister(registry, identity), Is.True);

            Assert.That(Unregister(registry, identity), Is.True);

            Assert.That(GetIntProperty(registry, "Count"), Is.EqualTo(0));
            Assert.That(TryGetEntity(registry, CreatePlayerId(6), out _), Is.False);
            Assert.That(TryResolvePlayerId(registry, ((Component)identity).transform, out _), Is.False);
            Assert.That(TryResolvePlayerId(registry, child, out _), Is.False);
            Assert.That(GetBoolProperty(identity, "IsBound"), Is.True);
        }

        [Test]
        public void FND_PLAYER_ENTITY_DestroyedEntity_FailsClosedAndIsPruned()
        {
            var registry = CreateRegistry();
            var identity = CreateBoundIdentity("FND_Player_Destroyed", 7);
            var root = ((Component)identity).gameObject;
            Assert.That(TryRegister(registry, identity), Is.True);
            _createdObjects.Remove(root);

            UnityEngine.Object.DestroyImmediate(root);

            Assert.That(TryGetEntity(registry, CreatePlayerId(7), out _), Is.False);
            var results = CreateIdentityList();
            Assert.That(CollectActiveEntities(registry, results), Is.EqualTo(0));
            Assert.That(GetIntProperty(registry, "Count"), Is.EqualTo(0));
        }

        [Test]
        public void FND_PLAYER_ENTITY_ActiveEnumeration_IsStableByPlayerId()
        {
            var registry = CreateRegistry();
            var identityThree = CreateBoundIdentity("FND_Player_Enum_3", 3);
            var identityOne = CreateBoundIdentity("FND_Player_Enum_1", 1);
            var identityTwo = CreateBoundIdentity("FND_Player_Enum_2", 2);
            Assert.That(TryRegister(registry, identityThree), Is.True);
            Assert.That(TryRegister(registry, identityOne), Is.True);
            Assert.That(TryRegister(registry, identityTwo), Is.True);
            var results = CreateIdentityList();

            Assert.That(CollectActiveEntities(registry, results), Is.EqualTo(3));
            AssertPlayerIdValue(GetProperty(GetListItem(results, 0), "PlayerId"), 1);
            AssertPlayerIdValue(GetProperty(GetListItem(results, 1), "PlayerId"), 2);
            AssertPlayerIdValue(GetProperty(GetListItem(results, 2), "PlayerId"), 3);

            Assert.That(Unregister(registry, identityTwo), Is.True);

            Assert.That(CollectActiveEntities(registry, results), Is.EqualTo(2));
            AssertPlayerIdValue(GetProperty(GetListItem(results, 0), "PlayerId"), 1);
            AssertPlayerIdValue(GetProperty(GetListItem(results, 1), "PlayerId"), 3);
        }

        private object CreateBoundIdentity(string name, int playerIdValue)
        {
            var identity = CreateIdentity(name);
            Assert.That(TryBind(identity, CreatePlayerId(playerIdValue)), Is.True);
            return identity;
        }

        private object CreateIdentity(string name)
        {
            var root = new GameObject(name);
            _createdObjects.Add(root);
            return root.AddComponent(ResolveType(IdentityTypeName));
        }

        private Transform CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static object CreateRegistry()
        {
            return Activator.CreateInstance(ResolveType(RegistryTypeName));
        }

        private static object CreatePlayerId(int value)
        {
            return Activator.CreateInstance(ResolveType(PlayerIdTypeName), value);
        }

        private static bool TryBind(object identity, object playerId)
        {
            return (bool)InvokeInstanceMethod(
                identity,
                "TryBind",
                new[] { ResolveType(PlayerIdTypeName) },
                new[] { playerId });
        }

        private static bool TryRegister(object registry, object identity)
        {
            return (bool)InvokeInstanceMethod(
                registry,
                "TryRegister",
                new[] { ResolveType(IdentityTypeName) },
                new[] { identity });
        }

        private static bool TryGetEntity(object registry, object playerId, out object identity)
        {
            var args = new[] { playerId, null };
            var result = InvokeInstanceMethod(
                registry,
                "TryGetEntity",
                new[] { ResolveType(PlayerIdTypeName), ResolveType(IdentityTypeName).MakeByRefType() },
                args);
            identity = args[1];
            return (bool)result;
        }

        private static bool Unregister(object registry, object identity)
        {
            return (bool)InvokeInstanceMethod(
                registry,
                "Unregister",
                new[] { ResolveType(IdentityTypeName) },
                new[] { identity });
        }

        private static bool TryResolvePlayerId(object registry, Transform candidate, out object playerId)
        {
            var args = new object[] { candidate, null };
            var result = InvokeInstanceMethod(
                registry,
                "TryResolvePlayerId",
                new[] { typeof(Transform), ResolveType(PlayerIdTypeName).MakeByRefType() },
                args);
            playerId = args[1];
            return (bool)result;
        }

        private static int CollectActiveEntities(object registry, object results)
        {
            return (int)InvokeInstanceMethod(
                registry,
                "CollectActiveEntities",
                new[] { typeof(List<>).MakeGenericType(ResolveType(IdentityTypeName)) },
                new[] { results });
        }

        private static object CreateIdentityList()
        {
            return Activator.CreateInstance(typeof(List<>).MakeGenericType(ResolveType(IdentityTypeName)));
        }

        private static object GetListItem(object list, int index)
        {
            return list.GetType().GetProperty("Item").GetValue(list, new object[] { index });
        }

        private static object InvokeInstanceMethod(object target, string methodName, Type[] parameterTypes, object[] args)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null, $"Missing public method '{methodName}' on '{target.GetType().FullName}'.");
            return method.Invoke(target, args);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}' on '{target.GetType().FullName}'.");
            field.SetValue(target, value);
        }

        private static bool IsValidPlayerId(object playerId)
        {
            return GetBoolProperty(playerId, "IsValid");
        }

        private static void AssertPlayerIdValue(object playerId, int expectedValue)
        {
            Assert.That(IsValidPlayerId(playerId), Is.True);
            Assert.That(GetProperty(playerId, "Value"), Is.EqualTo(expectedValue));
        }

        private static bool GetBoolProperty(object target, string propertyName)
        {
            return (bool)GetProperty(target, propertyName);
        }

        private static int GetIntProperty(object target, string propertyName)
        {
            return (int)GetProperty(target, propertyName);
        }

        private static Transform GetTransformProperty(object target, string propertyName)
        {
            return (Transform)GetProperty(target, propertyName);
        }

        private static object GetProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing public property '{propertyName}' on '{target.GetType().FullName}'.");
            return property.GetValue(target);
        }

        private static object GetStaticProperty(Type type, string propertyName)
        {
            var property = type.GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing public static property '{propertyName}' on '{type.FullName}'.");
            return property.GetValue(null);
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
    }
}
