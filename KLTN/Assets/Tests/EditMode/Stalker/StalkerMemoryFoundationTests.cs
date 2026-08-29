using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerMemoryFoundationTests
    {
        private const string PlayerIdTypeName = "EchoProtocol.AI.Common.PlayerId";
        private const string AiSimulationTimeTypeName = "EchoProtocol.AI.Common.AiSimulationTime";
        private const string VisionObservationTypeName = "EchoProtocol.AI.Stalker.VisionObservation";
        private const string StalkerMemoryTypeName = "EchoProtocol.AI.Stalker.StalkerMemory";

        [Test]
        public void STK_MEM_DefaultMemory_HasNoTargetsOrObservedKnowledge()
        {
            var memory = CreateMemory();

            Assert.That(IsValidPlayerId(GetProperty(memory, "DetectionTargetId")), Is.False);
            Assert.That(IsValidPlayerId(GetProperty(memory, "CurrentTargetId")), Is.False);
            Assert.That(GetFloatProperty(memory, "DetectionMeter"), Is.EqualTo(0f));
            AssertObservedKnowledgeAbsent(memory);
        }

        [Test]
        public void STK_MEM_SetDetectionTarget_ResetsDetectionMeter()
        {
            var memory = CreateMemory();
            var targetA = CreatePlayerId(1);
            var targetB = CreatePlayerId(2);

            InvokeMethod(memory, "SetDetectionTarget", new[] { ResolveType(PlayerIdTypeName) }, new[] { targetA });
            InvokeMethod(memory, "SetDetectionMeter", new[] { typeof(float) }, new object[] { 3.5f });
            InvokeMethod(memory, "SetDetectionTarget", new[] { ResolveType(PlayerIdTypeName) }, new[] { targetB });

            AssertPlayerIdValue(GetProperty(memory, "DetectionTargetId"), 2);
            Assert.That(GetFloatProperty(memory, "DetectionMeter"), Is.EqualTo(0f));

            InvokeMethod(memory, "ClearDetectionTarget", Type.EmptyTypes, Array.Empty<object>());

            Assert.That(IsValidPlayerId(GetProperty(memory, "DetectionTargetId")), Is.False);
            Assert.That(GetFloatProperty(memory, "DetectionMeter"), Is.EqualTo(0f));
        }

        [Test]
        public void STK_MEM_CurrentTargetObservation_UpdatesKnowledgeAtomically()
        {
            var memory = CreateMemory();
            var player = CreatePlayerId(1);
            var observedAt = CreateSimulationTime(10, 1.25d);
            var position = new Vector3(2f, 1f, 4f);
            var direction = new Vector3(3f, 0f, 4f);
            var observation = CreateVisionObservation(player, position, direction, observedAt, 5f);

            InvokeMethod(memory, "SetCurrentTarget", new[] { ResolveType(PlayerIdTypeName) }, new[] { player });
            var accepted = InvokeMethod(memory, "TryAcceptCurrentTargetObservation", new[] { ResolveType(VisionObservationTypeName) }, new[] { observation });

            Assert.That(accepted, Is.EqualTo(true));
            Assert.That(GetVector3Property(memory, "LastKnownPosition"), Is.EqualTo(position));
            Assert.That(GetVector3Property(memory, "LastSeenDirection"), Is.EqualTo(direction.normalized));
            Assert.That(GetProperty(memory, "TargetLastSeenTime"), Is.EqualTo(observedAt));
            Assert.That(GetProperty(memory, "LastCurrentTargetObservation"), Is.EqualTo(observation));
            AssertObservedKnowledgePresent(memory);
        }

        [Test]
        public void STK_MEM_DetectionTargetObservation_DoesNotCreateCurrentTargetKnowledge()
        {
            var memory = CreateMemory();
            var player = CreatePlayerId(1);
            var observedAt = CreateSimulationTime(10, 1.25d);
            var position = new Vector3(2f, 1f, 4f);
            var direction = new Vector3(3f, 0f, 4f);
            var observation = CreateVisionObservation(player, position, direction, observedAt, 5f);

            InvokeMethod(memory, "SetDetectionTarget", new[] { ResolveType(PlayerIdTypeName) }, new[] { player });
            var accepted = InvokeMethod(memory, "TryAcceptDetectionTargetObservation", new[] { ResolveType(VisionObservationTypeName) }, new[] { observation });

            Assert.That(accepted, Is.EqualTo(true));
            AssertPlayerIdValue(GetProperty(memory, "DetectionTargetId"), 1);
            Assert.That(IsValidPlayerId(GetProperty(memory, "CurrentTargetId")), Is.False);
            Assert.That(GetBoolProperty(memory, "HasLastDetectionTargetObservation"), Is.True);
            Assert.That(GetProperty(memory, "LastDetectionTargetObservation"), Is.EqualTo(observation));
            Assert.That(GetBoolProperty(memory, "HasLastKnownPosition"), Is.False);
            Assert.That(GetBoolProperty(memory, "HasLastSeenDirection"), Is.False);
            Assert.That(GetBoolProperty(memory, "HasTargetLastSeenTime"), Is.False);
            Assert.That(GetBoolProperty(memory, "HasLastCurrentTargetObservation"), Is.False);
        }

        [Test]
        public void STK_MEM_DifferentPlayerObservation_CannotMutateDetectionTargetKnowledge()
        {
            var memory = CreateMemory();
            var detectionTarget = CreatePlayerId(1);
            var differentPlayer = CreatePlayerId(2);
            var observation = CreateVisionObservation(
                differentPlayer,
                new Vector3(2f, 1f, 4f),
                new Vector3(0f, 0f, 1f),
                CreateSimulationTime(10, 1.25d),
                5f);

            InvokeMethod(memory, "SetDetectionTarget", new[] { ResolveType(PlayerIdTypeName) }, new[] { detectionTarget });
            var accepted = InvokeMethod(memory, "TryAcceptDetectionTargetObservation", new[] { ResolveType(VisionObservationTypeName) }, new[] { observation });

            Assert.That(accepted, Is.EqualTo(false));
            AssertObservedKnowledgeAbsent(memory);
            Assert.That(GetBoolProperty(memory, "HasLastDetectionTargetObservation"), Is.False);
        }

        [Test]
        public void STK_MEM_SetDetectionTarget_NewPlayer_ClearsPreviousDetectionObservation()
        {
            var memory = CreateMemory();
            var playerOne = CreatePlayerId(1);
            var playerTwo = CreatePlayerId(2);
            var newerPlayerOneObservation = CreateVisionObservation(
                playerOne,
                new Vector3(1f, 1f, 1f),
                Vector3.forward,
                CreateSimulationTime(20, 2d),
                2f);
            var olderPlayerTwoObservation = CreateVisionObservation(
                playerTwo,
                new Vector3(2f, 1f, 2f),
                Vector3.right,
                CreateSimulationTime(10, 1d),
                3f);

            InvokeMethod(memory, "SetDetectionTarget", new[] { ResolveType(PlayerIdTypeName) }, new[] { playerOne });
            Assert.That(InvokeMethod(memory, "TryAcceptDetectionTargetObservation", new[] { ResolveType(VisionObservationTypeName) }, new[] { newerPlayerOneObservation }), Is.EqualTo(true));

            InvokeMethod(memory, "SetDetectionTarget", new[] { ResolveType(PlayerIdTypeName) }, new[] { playerTwo });

            AssertPlayerIdValue(GetProperty(memory, "DetectionTargetId"), 2);
            Assert.That(GetFloatProperty(memory, "DetectionMeter"), Is.EqualTo(0f));
            AssertObservedKnowledgeAbsent(memory);
            Assert.That(GetBoolProperty(memory, "HasLastDetectionTargetObservation"), Is.False);
            Assert.That(InvokeMethod(memory, "TryAcceptDetectionTargetObservation", new[] { ResolveType(VisionObservationTypeName) }, new[] { olderPlayerTwoObservation }), Is.EqualTo(true));
            Assert.That(GetBoolProperty(memory, "HasLastDetectionTargetObservation"), Is.True);
            Assert.That(GetProperty(memory, "LastDetectionTargetObservation"), Is.EqualTo(olderPlayerTwoObservation));
            Assert.That(GetBoolProperty(memory, "HasLastKnownPosition"), Is.False);
            Assert.That(GetBoolProperty(memory, "HasTargetLastSeenTime"), Is.False);
        }

        [Test]
        public void STK_MEM_SetDetectionTarget_SamePlayer_PreservesDetectionObservationHistory()
        {
            var memory = CreateMemory();
            var playerOne = CreatePlayerId(1);
            var observedAt = CreateSimulationTime(20, 2d);
            var position = new Vector3(3f, 1f, 5f);
            var direction = Vector3.right;
            var observation = CreateVisionObservation(playerOne, position, direction, observedAt, 6f);

            InvokeMethod(memory, "SetDetectionTarget", new[] { ResolveType(PlayerIdTypeName) }, new[] { playerOne });
            Assert.That(InvokeMethod(memory, "TryAcceptDetectionTargetObservation", new[] { ResolveType(VisionObservationTypeName) }, new[] { observation }), Is.EqualTo(true));
            InvokeMethod(memory, "SetDetectionMeter", new[] { typeof(float) }, new object[] { 2.5f });

            InvokeMethod(memory, "SetDetectionTarget", new[] { ResolveType(PlayerIdTypeName) }, new[] { playerOne });

            AssertPlayerIdValue(GetProperty(memory, "DetectionTargetId"), 1);
            Assert.That(GetFloatProperty(memory, "DetectionMeter"), Is.EqualTo(0f));
            Assert.That(GetBoolProperty(memory, "HasLastDetectionTargetObservation"), Is.True);
            Assert.That(GetProperty(memory, "LastDetectionTargetObservation"), Is.EqualTo(observation));
            Assert.That(GetBoolProperty(memory, "HasLastKnownPosition"), Is.False);
            Assert.That(GetBoolProperty(memory, "HasLastSeenDirection"), Is.False);
            Assert.That(GetBoolProperty(memory, "HasTargetLastSeenTime"), Is.False);
            Assert.That(GetBoolProperty(memory, "HasLastCurrentTargetObservation"), Is.False);
        }

        [Test]
        public void STK_MEM_DifferentPlayerObservation_CannotMutateCurrentTargetKnowledge()
        {
            var memory = CreateMemory();
            var currentTarget = CreatePlayerId(1);
            var differentPlayer = CreatePlayerId(2);
            var observation = CreateVisionObservation(
                differentPlayer,
                new Vector3(2f, 1f, 4f),
                new Vector3(0f, 0f, 1f),
                CreateSimulationTime(10, 1.25d),
                5f);

            InvokeMethod(memory, "SetCurrentTarget", new[] { ResolveType(PlayerIdTypeName) }, new[] { currentTarget });
            var accepted = InvokeMethod(memory, "TryAcceptCurrentTargetObservation", new[] { ResolveType(VisionObservationTypeName) }, new[] { observation });

            Assert.That(accepted, Is.EqualTo(false));
            AssertObservedKnowledgeAbsent(memory);
        }

        [Test]
        public void STK_MEM_OlderObservation_CannotOverwriteNewerKnowledge()
        {
            var memory = CreateMemory();
            var player = CreatePlayerId(1);
            var newerTime = CreateSimulationTime(20, 2d);
            var olderTime = CreateSimulationTime(10, 1d);
            var newerPosition = new Vector3(2f, 1f, 4f);
            var newerDirection = new Vector3(3f, 0f, 4f);
            var newerObservation = CreateVisionObservation(player, newerPosition, newerDirection, newerTime, 5f);
            var olderObservation = CreateVisionObservation(
                player,
                new Vector3(9f, 0f, 9f),
                new Vector3(1f, 0f, 0f),
                olderTime,
                2f);

            InvokeMethod(memory, "SetCurrentTarget", new[] { ResolveType(PlayerIdTypeName) }, new[] { player });
            Assert.That(InvokeMethod(memory, "TryAcceptCurrentTargetObservation", new[] { ResolveType(VisionObservationTypeName) }, new[] { newerObservation }), Is.EqualTo(true));
            var acceptedOlder = InvokeMethod(memory, "TryAcceptCurrentTargetObservation", new[] { ResolveType(VisionObservationTypeName) }, new[] { olderObservation });

            Assert.That(acceptedOlder, Is.EqualTo(false));
            Assert.That(GetVector3Property(memory, "LastKnownPosition"), Is.EqualTo(newerPosition));
            Assert.That(GetVector3Property(memory, "LastSeenDirection"), Is.EqualTo(newerDirection.normalized));
            Assert.That(GetProperty(memory, "TargetLastSeenTime"), Is.EqualTo(newerTime));
            Assert.That(GetProperty(memory, "LastCurrentTargetObservation"), Is.EqualTo(newerObservation));
            AssertObservedKnowledgePresent(memory);
        }

        private static object CreateMemory()
        {
            return Activator.CreateInstance(ResolveType(StalkerMemoryTypeName));
        }

        private static object CreatePlayerId(int value)
        {
            return Activator.CreateInstance(ResolveType(PlayerIdTypeName), value);
        }

        private static object CreateSimulationTime(long tick, double seconds)
        {
            return Activator.CreateInstance(ResolveType(AiSimulationTimeTypeName), tick, seconds);
        }

        private static object CreateVisionObservation(
            object playerId,
            Vector3 observedPosition,
            Vector3 observedDirection,
            object observedAt,
            float distance)
        {
            return Activator.CreateInstance(
                ResolveType(VisionObservationTypeName),
                playerId,
                observedPosition,
                observedDirection,
                observedAt,
                distance);
        }

        private static object InvokeMethod(object target, string methodName, Type[] parameterTypes, object[] args)
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

        private static object GetProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing public property '{propertyName}' on '{target.GetType().FullName}'.");

            return property.GetValue(target);
        }

        private static float GetFloatProperty(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.TypeOf<float>(), $"Property '{propertyName}' must return float.");
            return (float)value;
        }

        private static Vector3 GetVector3Property(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.TypeOf<Vector3>(), $"Property '{propertyName}' must return Vector3.");
            return (Vector3)value;
        }

        private static bool GetBoolProperty(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.TypeOf<bool>(), $"Property '{propertyName}' must return bool.");
            return (bool)value;
        }

        private static bool IsValidPlayerId(object playerId)
        {
            return GetBoolProperty(playerId, "IsValid");
        }

        private static void AssertPlayerIdValue(object playerId, int expectedValue)
        {
            Assert.That(IsValidPlayerId(playerId), Is.True);
            var value = GetProperty(playerId, "Value");
            Assert.That(value, Is.EqualTo(expectedValue));
        }

        private static void AssertObservedKnowledgeAbsent(object memory)
        {
            Assert.That(GetBoolProperty(memory, "HasLastKnownPosition"), Is.False);
            Assert.That(GetBoolProperty(memory, "HasLastSeenDirection"), Is.False);
            Assert.That(GetBoolProperty(memory, "HasTargetLastSeenTime"), Is.False);
            Assert.That(GetBoolProperty(memory, "HasLastCurrentTargetObservation"), Is.False);
            Assert.That(GetBoolProperty(memory, "HasLastDetectionTargetObservation"), Is.False);
        }

        private static void AssertObservedKnowledgePresent(object memory)
        {
            Assert.That(GetBoolProperty(memory, "HasLastKnownPosition"), Is.True);
            Assert.That(GetBoolProperty(memory, "HasLastSeenDirection"), Is.True);
            Assert.That(GetBoolProperty(memory, "HasTargetLastSeenTime"), Is.True);
            Assert.That(GetBoolProperty(memory, "HasLastCurrentTargetObservation"), Is.True);
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

            Assert.Fail($"Could not find production type '{fullTypeName}' in the loaded Unity AppDomain.");
            return null;
        }
    }
}
