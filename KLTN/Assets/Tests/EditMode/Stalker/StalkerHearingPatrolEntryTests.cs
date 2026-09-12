using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerHearingPatrolEntryTests
    {
        private const string ControllerTypeName =
            "EchoProtocol.AI.Stalker.StalkerController";

        private const string StateTypeName =
            "EchoProtocol.AI.Stalker.StalkerState";

        private const string HearingObservationTypeName =
            "EchoProtocol.AI.Listener.Perception.HearingObservation";

        private const string RuntimeNoiseEventOrderKeyTypeName =
            "EchoProtocol.AI.Listener.Noise.RuntimeNoiseEventOrderKey";

        private const string RuntimeNoiseTypeName =
            "EchoProtocol.AI.Listener.Noise.RuntimeNoiseType";

        private const string OcclusionClassTypeName =
            "EchoProtocol.AI.Listener.Perception.ListenerOcclusionClass";

        [Test]
        public void PatrolHearing_BeginsHeardNoiseSearchWithoutMutatingVisualKnowledge()
        {
            var gameObject =
                new GameObject(
                    "STK_Hearing_PatrolEntry");

            try
            {
                var controller =
                    gameObject.AddComponent(
                        ResolveType(
                            ControllerTypeName));

                SetField(
                    controller,
                    "currentState",
                    Enum.Parse(
                        ResolveType(StateTypeName),
                        "PATROL"));

                var visualSentinel =
                    new Vector3(
                        -100f,
                        3f,
                        250f);

                SetField(
                    controller,
                    "lastKnownPosition",
                    visualSentinel);

                var memory =
                    GetField(
                        controller,
                        "_memory");

                Assert.That(
                    Read<bool>(
                        memory,
                        "HasLastKnownPosition"),
                    Is.False);

                AssertPlayerIdInvalid(
                    Read(
                        memory,
                        "CurrentTargetId"));

                AssertPlayerIdInvalid(
                    Read(
                        memory,
                        "DetectionTargetId"));

                var now =
                    DateTime.UtcNow;

                var noisePosition =
                    new Vector3(
                        12f,
                        0f,
                        7f);

                var observation =
                    CreateObservation(
                        "stalker-heard-1",
                        noisePosition,
                        now,
                        0.85d);

                SetField(
                    controller,
                    "_currentHearingObservations",
                    ObservationArray(
                        observation));

                SetField(
                    controller,
                    "_currentHearingEvaluationTimeUtc",
                    now);

                InvokePrivate(
                    controller,
                    "TryBeginHeardNoiseSearchFromCurrentFrame");

                Assert.That(
                    GetField(
                        controller,
                        "currentState")
                    .ToString(),
                    Is.EqualTo("SEARCH"));

                var searchContext =
                    GetField(
                        controller,
                        "_searchContext");

                Assert.That(
                    searchContext,
                    Is.Not.Null);

                Assert.That(
                    Read(
                        searchContext,
                        "Source")
                    .ToString(),
                    Is.EqualTo("HeardNoise"));

                Assert.That(
                    Read<Vector3>(
                        searchContext,
                        "SearchOriginPosition"),
                    Is.EqualTo(noisePosition));

                var hearingMemory =
                    GetField(
                        controller,
                        "_hearingMemory");

                Assert.That(
                    Read<bool>(
                        hearingMemory,
                        "HasActiveNoiseInvestigation"),
                    Is.True);

                Assert.That(
                    Read<string>(
                        hearingMemory,
                        "ActiveNoiseEventId"),
                    Is.EqualTo("stalker-heard-1"));

                Assert.That(
                    Read<Vector3>(
                        hearingMemory,
                        "InvestigationPosition"),
                    Is.EqualTo(noisePosition));

                //
                // Hearing must not overwrite legacy visual LKP.
                //
                Assert.That(
                    (Vector3)GetField(
                        controller,
                        "lastKnownPosition"),
                    Is.EqualTo(visualSentinel));

                //
                // Hearing must not manufacture visual target knowledge.
                //
                Assert.That(
                    Read<bool>(
                        memory,
                        "HasLastKnownPosition"),
                    Is.False);

                AssertPlayerIdInvalid(
                    Read(
                        memory,
                        "CurrentTargetId"));

                AssertPlayerIdInvalid(
                    Read(
                        memory,
                        "DetectionTargetId"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(
                    gameObject);
            }
        }

        private static object CreateObservation(
            string noiseEventId,
            Vector3 position,
            DateTime heardAtUtc,
            double effectiveIntensity)
        {
            var orderKey =
                Activator.CreateInstance(
                    ResolveType(
                        RuntimeNoiseEventOrderKeyTypeName),
                    new object[]
                    {
                        200L,
                        1UL
                    });

            var noiseType =
                Enum.Parse(
                    ResolveType(
                        RuntimeNoiseTypeName),
                    "INTERACTION");

            var occlusion =
                Enum.Parse(
                    ResolveType(
                        OcclusionClassTypeName),
                    "CLEAR");

            return Activator.CreateInstance(
                ResolveType(
                    HearingObservationTypeName),
                new object[]
                {
                    noiseEventId,
                    orderKey,
                    noiseType,
                    position,
                    heardAtUtc,
                    heardAtUtc,
                    heardAtUtc.AddSeconds(5),
                    (double)position.magnitude,
                    1d,
                    effectiveIntensity,
                    occlusion
                });
        }

        private static Array ObservationArray(
            params object[] observations)
        {
            var observationType =
                ResolveType(
                    HearingObservationTypeName);

            var array =
                Array.CreateInstance(
                    observationType,
                    observations.Length);

            for (var i = 0;
                 i < observations.Length;
                 i++)
            {
                array.SetValue(
                    observations[i],
                    i);
            }

            return array;
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

        private static void SetField(
            object target,
            string fieldName,
            object value)
        {
            var field =
                FindField(
                    target.GetType(),
                    fieldName);

            Assert.That(
                field,
                Is.Not.Null,
                $"Missing field '{fieldName}'.");

            field.SetValue(
                target,
                value);
        }

        private static object GetField(
            object target,
            string fieldName)
        {
            var field =
                FindField(
                    target.GetType(),
                    fieldName);

            Assert.That(
                field,
                Is.Not.Null,
                $"Missing field '{fieldName}'.");

            return field.GetValue(
                target);
        }

        private static FieldInfo FindField(
            Type type,
            string fieldName)
        {
            while (type != null)
            {
                var field =
                    type.GetField(
                        fieldName,
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.Instance);

                if (field != null)
                {
                    return field;
                }

                type =
                    type.BaseType;
            }

            return null;
        }

        private static object InvokePrivate(
            object target,
            string methodName)
        {
            var method =
                target.GetType().GetMethod(
                    methodName,
                    BindingFlags.NonPublic |
                    BindingFlags.Instance);

            Assert.That(
                method,
                Is.Not.Null,
                $"Missing method '{methodName}'.");

            try
            {
                return method.Invoke(
                    target,
                    null);
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
                $"Missing property '{propertyName}'.");

            return property.GetValue(
                target);
        }

        private static T Read<T>(
            object target,
            string propertyName)
        {
            return (T)Read(
                target,
                propertyName);
        }

        private static Type ResolveType(
            string fullTypeName)
        {
            foreach (var assembly
                     in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type =
                    assembly.GetType(
                        fullTypeName,
                        false);

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
