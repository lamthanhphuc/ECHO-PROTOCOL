using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace EchoProtocol.Networking.Tests
{
    public sealed class FusionPlayerIdentityRegistryTests
    {
        private const string RegistryTypeName = "EchoProtocol.Networking.FusionPlayerIdentityRegistry";
        private const string PlayerRefTypeName = "Fusion.PlayerRef";
        private const string PlayerIdTypeName = "EchoProtocol.AI.Common.PlayerId";

        [Test]
        public void FND_NET_ID_FirstRegistrations_AllocateMonotonicLogicalIds()
        {
            var registry = CreateRegistry();
            var playerA = CreatePlayerRefFromIndex(0);
            var playerB = CreatePlayerRefFromIndex(1);

            Assert.That(TryRegister(registry, playerA, out var idA), Is.True);
            Assert.That(TryRegister(registry, playerB, out var idB), Is.True);

            AssertPlayerIdValue(idA, 1);
            AssertPlayerIdValue(idB, 2);
            Assert.That(GetIntProperty(registry, "Count"), Is.EqualTo(2));
            Assert.That(TryGetPlayerRef(registry, idA, out var resolvedA), Is.True);
            Assert.That(TryGetPlayerRef(registry, idB, out var resolvedB), Is.True);
            Assert.That(resolvedA, Is.EqualTo(playerA));
            Assert.That(resolvedB, Is.EqualTo(playerB));
        }

        [Test]
        public void FND_NET_ID_DuplicateRegistration_IsIdempotent()
        {
            var registry = CreateRegistry();
            var playerA = CreatePlayerRefFromIndex(0);
            var playerB = CreatePlayerRefFromIndex(1);

            Assert.That(TryRegister(registry, playerA, out var firstId), Is.True);
            Assert.That(TryRegister(registry, playerA, out var duplicateId), Is.True);

            Assert.That(duplicateId, Is.EqualTo(firstId));
            Assert.That(GetIntProperty(registry, "Count"), Is.EqualTo(1));

            Assert.That(TryRegister(registry, playerB, out var idB), Is.True);
            AssertPlayerIdValue(idB, 2);
        }

        [Test]
        public void FND_NET_ID_Unregister_RemovesBothDirections()
        {
            var registry = CreateRegistry();
            var playerA = CreatePlayerRefFromIndex(0);
            Assert.That(TryRegister(registry, playerA, out var idA), Is.True);

            Assert.That(Unregister(registry, playerA), Is.True);

            Assert.That(GetIntProperty(registry, "Count"), Is.EqualTo(0));
            Assert.That(TryGetPlayerId(registry, playerA, out var resolvedId), Is.False);
            Assert.That(GetBoolProperty(resolvedId, "IsValid"), Is.False);
            Assert.That(TryGetPlayerRef(registry, idA, out _), Is.False);
            Assert.That(Unregister(registry, playerA), Is.False);
        }

        [Test]
        public void FND_NET_ID_ReRegisteredPlayerRef_GetsNewLogicalId()
        {
            var registry = CreateRegistry();
            var playerA = CreatePlayerRefFromIndex(0);
            Assert.That(TryRegister(registry, playerA, out var oldId), Is.True);
            AssertPlayerIdValue(oldId, 1);
            Assert.That(Unregister(registry, playerA), Is.True);

            Assert.That(TryRegister(registry, playerA, out var newId), Is.True);

            AssertPlayerIdValue(newId, 2);
            Assert.That(TryGetPlayerRef(registry, oldId, out _), Is.False);
            Assert.That(TryGetPlayerId(registry, playerA, out var resolvedNewId), Is.True);
            Assert.That(resolvedNewId, Is.EqualTo(newId));
            Assert.That(TryGetPlayerRef(registry, newId, out var resolvedPlayer), Is.True);
            Assert.That(resolvedPlayer, Is.EqualTo(playerA));
        }

        [Test]
        public void FND_NET_ID_InvalidPlayerRef_FailsClosed()
        {
            var registry = CreateRegistry();

            AssertInvalidPlayerRef(registry, GetStaticProperty(ResolveType(PlayerRefTypeName), "None"));
            AssertInvalidPlayerRef(registry, Activator.CreateInstance(ResolveType(PlayerRefTypeName)));
            AssertInvalidPlayerRef(registry, GetStaticProperty(ResolveType(PlayerRefTypeName), "Invalid"));
            Assert.That(GetIntProperty(registry, "Count"), Is.EqualTo(0));
        }

        [Test]
        public void FND_NET_ID_ActiveIds_AreCollectedInStableAscendingOrder()
        {
            var registry = CreateRegistry();
            var playerA = CreatePlayerRefFromIndex(0);
            var playerB = CreatePlayerRefFromIndex(1);
            var playerC = CreatePlayerRefFromIndex(2);
            Assert.That(TryRegister(registry, playerA, out _), Is.True);
            Assert.That(TryRegister(registry, playerB, out _), Is.True);
            Assert.That(TryRegister(registry, playerC, out _), Is.True);
            Assert.That(Unregister(registry, playerB), Is.True);
            var results = CreatePlayerIdList();
            InvokeListAdd(results, CreatePlayerId(999));

            var count = CollectActivePlayerIds(registry, results);

            Assert.That(count, Is.EqualTo(2));
            Assert.That(GetListCount(results), Is.EqualTo(2));
            AssertPlayerIdValue(GetListItem(results, 0), 1);
            AssertPlayerIdValue(GetListItem(results, 1), 3);
        }

        [Test]
        public void FND_NET_ID_LookupUnknownLogicalId_FailsClosed()
        {
            var registry = CreateRegistry();
            Assert.That(TryRegister(registry, CreatePlayerRefFromIndex(0), out _), Is.True);

            Assert.That(TryGetPlayerRef(registry, CreatePlayerId(999), out _), Is.False);
            Assert.That(TryGetPlayerRef(registry, GetStaticProperty(ResolveType(PlayerIdTypeName), "Invalid"), out _), Is.False);
        }

        private static object CreateRegistry()
        {
            return Activator.CreateInstance(ResolveType(RegistryTypeName));
        }

        private static object CreatePlayerRefFromIndex(int index)
        {
            var method = ResolveType(PlayerRefTypeName).GetMethod(
                "FromIndex",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(int) },
                null);
            Assert.That(method, Is.Not.Null, "Missing Fusion.PlayerRef.FromIndex(int).");
            return method.Invoke(null, new object[] { index });
        }

        private static object CreatePlayerId(int value)
        {
            return Activator.CreateInstance(ResolveType(PlayerIdTypeName), value);
        }

        private static bool TryRegister(object registry, object playerRef, out object playerId)
        {
            var method = ResolveType(RegistryTypeName).GetMethod(
                "TryRegister",
                BindingFlags.Instance | BindingFlags.Public);
            var args = new[] { playerRef, null };
            var result = method.Invoke(registry, args);
            playerId = args[1];
            return (bool)result;
        }

        private static bool TryGetPlayerId(object registry, object playerRef, out object playerId)
        {
            var method = ResolveType(RegistryTypeName).GetMethod(
                "TryGetPlayerId",
                BindingFlags.Instance | BindingFlags.Public);
            var args = new[] { playerRef, null };
            var result = method.Invoke(registry, args);
            playerId = args[1];
            return (bool)result;
        }

        private static bool TryGetPlayerRef(object registry, object playerId, out object playerRef)
        {
            var method = ResolveType(RegistryTypeName).GetMethod(
                "TryGetPlayerRef",
                BindingFlags.Instance | BindingFlags.Public);
            var args = new[] { playerId, null };
            var result = method.Invoke(registry, args);
            playerRef = args[1];
            return (bool)result;
        }

        private static bool Unregister(object registry, object playerRef)
        {
            return (bool)ResolveType(RegistryTypeName)
                .GetMethod("Unregister", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(registry, new[] { playerRef });
        }

        private static int CollectActivePlayerIds(object registry, object results)
        {
            var method = ResolveType(RegistryTypeName).GetMethod(
                "CollectActivePlayerIds",
                BindingFlags.Instance | BindingFlags.Public);
            var result = method.Invoke(registry, new[] { results });
            return (int)result;
        }

        private static object CreatePlayerIdList()
        {
            return Activator.CreateInstance(typeof(List<>).MakeGenericType(ResolveType(PlayerIdTypeName)));
        }

        private static void InvokeListAdd(object list, object item)
        {
            list.GetType().GetMethod("Add").Invoke(list, new[] { item });
        }

        private static int GetListCount(object list)
        {
            return (int)list.GetType().GetProperty("Count").GetValue(list);
        }

        private static object GetListItem(object list, int index)
        {
            return list.GetType().GetProperty("Item").GetValue(list, new object[] { index });
        }

        private static void AssertPlayerIdValue(object playerId, int expectedValue)
        {
            Assert.That(GetBoolProperty(playerId, "IsValid"), Is.True);
            Assert.That(GetProperty(playerId, "Value"), Is.EqualTo(expectedValue));
        }

        private static void AssertInvalidPlayerRef(object registry, object invalidPlayerRef)
        {
            Assert.That(TryRegister(registry, invalidPlayerRef, out var registeredId), Is.False);
            Assert.That(GetBoolProperty(registeredId, "IsValid"), Is.False);
            Assert.That(TryGetPlayerId(registry, invalidPlayerRef, out var resolvedId), Is.False);
            Assert.That(GetBoolProperty(resolvedId, "IsValid"), Is.False);
            Assert.That(Unregister(registry, invalidPlayerRef), Is.False);
        }

        private static bool GetBoolProperty(object target, string propertyName)
        {
            return (bool)GetProperty(target, propertyName);
        }

        private static int GetIntProperty(object target, string propertyName)
        {
            return (int)GetProperty(target, propertyName);
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
