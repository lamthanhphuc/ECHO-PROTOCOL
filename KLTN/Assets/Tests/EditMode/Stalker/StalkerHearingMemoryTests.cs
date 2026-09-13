using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerHearingMemoryTests
    {
        private const string HearingMemoryTypeName =
            "EchoProtocol.AI.Stalker.Hearing.StalkerHearingMemory";

        private const string HearingObservationTypeName =
            "EchoProtocol.AI.Listener.Perception.HearingObservation";

        private const string RuntimeNoiseEventOrderKeyTypeName =
            "EchoProtocol.AI.Listener.Noise.RuntimeNoiseEventOrderKey";

        private const string RuntimeNoiseTypeName =
            "EchoProtocol.AI.Listener.Noise.RuntimeNoiseType";

        private const string ListenerOcclusionClassTypeName =
            "EchoProtocol.AI.Listener.Perception.ListenerOcclusionClass";

        private const string StalkerMemoryTypeName =
            "EchoProtocol.AI.Stalker.StalkerMemory";

        [Test]
        public void HearingMemory_StartsEmpty()
        {
            var memory = Create(HearingMemoryTypeName);

            Assert.That(
                Read<bool>(memory, "HasLastHeardObservation"),
                Is.False);

            Assert.That(
                Read<bool>(memory, "HasActiveNoiseInvestigation"),
                Is.False);

            Assert.That(
                Read<string>(memory, "ActiveNoiseEventId"),
                Is.Empty);

            Assert.That(
                Read<double>(memory, "CommittedEffectiveIntensity"),
                Is.EqualTo(0d));
        }

        [Test]
        public void BeginNoiseInvestigation_StoresNoiseKnowledgeWithoutPlayerIdentity()
        {
            var memory = Create(HearingMemoryTypeName);

            var observation = CreateObservation(
                "noise-1",
                new Vector3(4f, 0f, 7f),
                DateTime.UtcNow,
                effectiveIntensity: 0.75d);

            Invoke(
                memory,
                "BeginNoiseInvestigation",
                observation);

            Assert.That(
                Read<bool>(memory, "HasActiveNoiseInvestigation"),
                Is.True);

            Assert.That(
                Read<string>(memory, "ActiveNoiseEventId"),
                Is.EqualTo("noise-1"));

            Assert.That(
                Read<Vector3>(memory, "InvestigationPosition"),
                Is.EqualTo(new Vector3(4f, 0f, 7f)));

            Assert.That(
                Read<double>(memory, "CommittedEffectiveIntensity"),
                Is.EqualTo(0.75d));

            var hearingMemoryType =
                ResolveType(HearingMemoryTypeName);

            Assert.That(
                hearingMemoryType.GetProperty(
                    "CurrentTargetId",
                    BindingFlags.Public |
                    BindingFlags.Instance),
                Is.Null);

            Assert.That(
                hearingMemoryType.GetProperty(
                    "PlayerId",
                    BindingFlags.Public |
                    BindingFlags.Instance),
                Is.Null);
        }

        [Test]
        public void HearingInvestigation_DoesNotMutateVisualStalkerMemory()
        {
            var visualMemory = Create(StalkerMemoryTypeName);
            var hearingMemory = Create(HearingMemoryTypeName);

            AssertPlayerIdInvalid(
                Read(visualMemory, "CurrentTargetId"));

            AssertPlayerIdInvalid(
                Read(visualMemory, "DetectionTargetId"));

            Assert.That(
                Read<bool>(
                    visualMemory,
                    "HasLastKnownPosition"),
                Is.False);

            Assert.That(
                Read<bool>(
                    visualMemory,
                    "HasLastSeenDirection"),
                Is.False);

            Assert.That(
                Read<bool>(
                    visualMemory,
                    "HasTargetLastSeenTime"),
                Is.False);

            Invoke(
                hearingMemory,
                "BeginNoiseInvestigation",
                CreateObservation(
                    "noise-heard",
                    new Vector3(12f, 0f, -3f),
                    DateTime.UtcNow,
                    effectiveIntensity: 0.9d));

            Assert.That(
                Read<bool>(
                    hearingMemory,
                    "HasActiveNoiseInvestigation"),
                Is.True);

            //
            // Hearing must not manufacture visual player knowledge.
            //
            AssertPlayerIdInvalid(
                Read(visualMemory, "CurrentTargetId"));

            AssertPlayerIdInvalid(
                Read(visualMemory, "DetectionTargetId"));

            Assert.That(
                Read<bool>(
                    visualMemory,
                    "HasLastKnownPosition"),
                Is.False);

            Assert.That(
                Read<bool>(
                    visualMemory,
                    "HasLastSeenDirection"),
                Is.False);

            Assert.That(
                Read<bool>(
                    visualMemory,
                    "HasTargetLastSeenTime"),
                Is.False);

            Assert.That(
                Read<bool>(
                    visualMemory,
                    "HasLastCurrentTargetObservation"),
                Is.False);
        }

        [Test]
        public void RecordHeardObservation_KeepsDeterministicallyHigherRankedObservation()
        {
            var memory = Create(HearingMemoryTypeName);
            var now = DateTime.UtcNow;

            var stronger = CreateObservation(
                "noise-strong",
                new Vector3(3f, 0f, 0f),
                now,
                effectiveIntensity: 0.9d,
                authoritativeTick: 10,
                ordinal: 1);

            var weaker = CreateObservation(
                "noise-weak",
                new Vector3(1f, 0f, 0f),
                now.AddMilliseconds(1),
                effectiveIntensity: 0.3d,
                authoritativeTick: 10,
                ordinal: 2);

            Invoke(
                memory,
                "RecordHeardObservation",
                stronger);

            Invoke(
                memory,
                "RecordHeardObservation",
                weaker);

            var retainedObservation =
                Read(memory, "LastHeardObservation");

            Assert.That(
                Read<string>(
                    retainedObservation,
                    "NoiseEventId"),
                Is.EqualTo("noise-strong"));

            Assert.That(
                Read<double>(
                    retainedObservation,
                    "EffectiveIntensity"),
                Is.EqualTo(0.9d));
        }

        [Test]
        public void UpdateNoiseInvestigation_PreservesPeakCommittedIntensity()
        {
            var memory = Create(HearingMemoryTypeName);
            var now = DateTime.UtcNow;

            Invoke(
                memory,
                "BeginNoiseInvestigation",
                CreateObservation(
                    "noise-root",
                    new Vector3(2f, 0f, 2f),
                    now,
                    effectiveIntensity: 0.8d));

            Invoke(
                memory,
                "UpdateNoiseInvestigation",
                CreateObservation(
                    "noise-support",
                    new Vector3(3f, 0f, 2f),
                    now.AddMilliseconds(100),
                    effectiveIntensity: 0.5d));

            Assert.That(
                Read<string>(
                    memory,
                    "ActiveNoiseEventId"),
                Is.EqualTo("noise-support"));

            Assert.That(
                Read<Vector3>(
                    memory,
                    "InvestigationPosition"),
                Is.EqualTo(
                    new Vector3(3f, 0f, 2f)));

            Assert.That(
                Read<double>(
                    memory,
                    "CommittedEffectiveIntensity"),
                Is.EqualTo(0.8d));
        }

        [Test]
        public void ClearNoiseInvestigation_ClearsActiveHypothesisButKeepsLastHeardObservation()
        {
            var memory = Create(HearingMemoryTypeName);

            Invoke(
                memory,
                "BeginNoiseInvestigation",
                CreateObservation(
                    "noise-1",
                    new Vector3(5f, 0f, 1f),
                    DateTime.UtcNow,
                    effectiveIntensity: 0.6d));

            Invoke(
                memory,
                "ClearNoiseInvestigation");

            Assert.That(
                Read<bool>(
                    memory,
                    "HasActiveNoiseInvestigation"),
                Is.False);

            Assert.That(
                Read<string>(
                    memory,
                    "ActiveNoiseEventId"),
                Is.Empty);

            Assert.That(
                Read<bool>(
                    memory,
                    "HasLastHeardObservation"),
                Is.True);

            var lastObservation =
                Read(memory, "LastHeardObservation");

            Assert.That(
                Read<string>(
                    lastObservation,
                    "NoiseEventId"),
                Is.EqualTo("noise-1"));
        }

        [Test]
        public void Reset_ClearsAllHearingKnowledge()
        {
            var memory = Create(HearingMemoryTypeName);

            Invoke(
                memory,
                "BeginNoiseInvestigation",
                CreateObservation(
                    "noise-reset",
                    new Vector3(1f, 0f, 6f),
                    DateTime.UtcNow,
                    effectiveIntensity: 1d));

            Invoke(memory, "Reset");

            Assert.That(
                Read<bool>(
                    memory,
                    "HasActiveNoiseInvestigation"),
                Is.False);

            Assert.That(
                Read<bool>(
                    memory,
                    "HasLastHeardObservation"),
                Is.False);

            Assert.That(
                Read<string>(
                    memory,
                    "ActiveNoiseEventId"),
                Is.Empty);

            Assert.That(
                Read<double>(
                    memory,
                    "CommittedEffectiveIntensity"),
                Is.EqualTo(0d));
        }

        private static object CreateObservation(
            string noiseEventId,
            Vector3 position,
            DateTime emittedAtUtc,
            double effectiveIntensity,
            long authoritativeTick = 1,
            ulong ordinal = 1)
        {
            var orderKeyType =
                ResolveType(
                    RuntimeNoiseEventOrderKeyTypeName);

            var orderKey =
                Activator.CreateInstance(
                    orderKeyType,
                    new object[]
                    {
                        authoritativeTick,
                        ordinal
                    });

            var runtimeNoiseType =
                Enum.Parse(
                    ResolveType(RuntimeNoiseTypeName),
                    "INTERACTION");

            var occlusionClass =
                Enum.Parse(
                    ResolveType(
                        ListenerOcclusionClassTypeName),
                    "CLEAR");

            var observationType =
                ResolveType(
                    HearingObservationTypeName);

            return Activator.CreateInstance(
                observationType,
                new object[]
                {
                    noiseEventId,
                    orderKey,
                    runtimeNoiseType,
                    position,
                    emittedAtUtc,
                    emittedAtUtc,
                    emittedAtUtc.AddSeconds(5),
                    (double)position.magnitude,
                    1d,
                    effectiveIntensity,
                    occlusionClass
                });
        }

        private static object Create(
            string fullTypeName)
        {
            return Activator.CreateInstance(
                ResolveType(fullTypeName));
        }

        private static object Invoke(
            object target,
            string methodName,
            params object[] arguments)
        {
            var method =
                target.GetType().GetMethod(
                    methodName,
                    BindingFlags.Public |
                    BindingFlags.Instance);

            Assert.That(
                method,
                Is.Not.Null,
                $"Missing method {methodName}.");

            try
            {
                return method.Invoke(
                    target,
                    arguments);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException
                    ?? exception;
            }
        }

        private static object Read(
            object target,
            string propertyName)
        {
            var property =
                target.GetType().GetProperty(
                    propertyName,
                    BindingFlags.Public |
                    BindingFlags.Instance);

            Assert.That(
                property,
                Is.Not.Null,
                $"Missing property {propertyName}.");

            return property.GetValue(target);
        }

        private static T Read<T>(
            object target,
            string propertyName)
        {
            return (T)Read(
                target,
                propertyName);
        }

        private static void AssertPlayerIdInvalid(
            object playerId)
        {
            Assert.That(
                Read<bool>(
                    playerId,
                    "IsValid"),
                Is.False);
        }

        private static Type ResolveType(
            string fullTypeName)
        {
            var assemblies =
                AppDomain.CurrentDomain.GetAssemblies();

            for (var i = 0;
                 i < assemblies.Length;
                 i++)
            {
                var type =
                    assemblies[i].GetType(
                        fullTypeName,
                        throwOnError: false);

                if (type != null)
                {
                    return type;
                }
            }

            Assert.Fail(
                $"Could not resolve type '{fullTypeName}'.");

            return null;
        }
    }
}
