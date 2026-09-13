using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerHearingSearchUpdateTests
    {
        private const string ControllerTypeName =
            "EchoProtocol.AI.Stalker.StalkerController";

        private const string StateTypeName =
            "EchoProtocol.AI.Stalker.StalkerState";

        private const string SearchContextTypeName =
            "EchoProtocol.AI.Stalker.StalkerSearchContext";

        private const string SearchSourceTypeName =
            "EchoProtocol.AI.Stalker.StalkerSearchSource";

        private const string SearchEpisodeIdTypeName =
            "EchoProtocol.AI.Stalker.SearchEpisodeId";

        private const string SimulationTimeTypeName =
            "EchoProtocol.AI.Common.AiSimulationTime";

        private const string RegionIdTypeName =
            "EchoProtocol.AI.Common.Spatial.RegionId";

        private const string HearingObservationTypeName =
            "EchoProtocol.AI.Listener.Perception.HearingObservation";

        private const string RuntimeNoiseEventOrderKeyTypeName =
            "EchoProtocol.AI.Listener.Noise.RuntimeNoiseEventOrderKey";

        private const string RuntimeNoiseTypeName =
            "EchoProtocol.AI.Listener.Noise.RuntimeNoiseType";

        private const string OcclusionClassTypeName =
            "EchoProtocol.AI.Listener.Perception.ListenerOcclusionClass";

        [Test]
        public void RelatedSupport_RefinesHeardNoiseSearchWithoutCreatingPlayerKnowledge()
        {
            var gameObject =
                new GameObject(
                    "STK_Hearing_SearchUpdate");

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
                        "SEARCH"));

                var now =
                    DateTime.UtcNow;

                var rootPosition =
                    new Vector3(
                        10f,
                        0f,
                        10f);

                var updatedPosition =
                    new Vector3(
                        11f,
                        0f,
                        10f);

                SetField(
                    controller,
                    "_searchContext",
                    CreateSearchContext(
                        rootPosition));

                var hearingMemory =
                    GetField(
                        controller,
                        "_hearingMemory");

                InvokePublic(
                    hearingMemory,
                    "BeginNoiseInvestigation",
                    CreateObservation(
                        "noise-root",
                        rootPosition,
                        now,
                        0.8d,
                        300L,
                        1UL));

                var relatedSupport =
                    CreateObservation(
                        "noise-related",
                        updatedPosition,
                        now.AddMilliseconds(10),
                        0.5d,
                        300L,
                        2UL);

                SetField(
                    controller,
                    "_currentHearingObservations",
                    ObservationArray(
                        relatedSupport));

                SetField(
                    controller,
                    "_currentHearingEvaluationTimeUtc",
                    now.AddMilliseconds(20));

                InvokePrivate(
                    controller,
                    "TryUpdateHeardNoiseSearchFromCurrentFrame");

                Assert.That(
                    Read<Vector3>(
                        hearingMemory,
                        "InvestigationPosition"),
                    Is.EqualTo(updatedPosition));

                Assert.That(
                    Read<string>(
                        hearingMemory,
                        "ActiveNoiseEventId"),
                    Is.EqualTo("noise-related"));

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
                    Is.EqualTo(updatedPosition));

                var visualMemory =
                    GetField(
                        controller,
                        "_memory");

                AssertPlayerIdInvalid(
                    Read(
                        visualMemory,
                        "CurrentTargetId"));

                AssertPlayerIdInvalid(
                    Read(
                        visualMemory,
                        "DetectionTargetId"));

                Assert.That(
                    Read<bool>(
                        visualMemory,
                        "HasLastKnownPosition"),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(
                    gameObject);
            }
        }

        [Test]
        public void WeakUnrelatedNoise_DoesNotRedirectHeardNoiseSearch()
        {
            var gameObject =
                new GameObject(
                    "STK_Hearing_WeakNoise");

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
                        "SEARCH"));

                var now =
                    DateTime.UtcNow;

                var rootPosition =
                    Vector3.zero;

                SetField(
                    controller,
                    "_searchContext",
                    CreateSearchContext(
                        rootPosition));

                var hearingMemory =
                    GetField(
                        controller,
                        "_hearingMemory");

                InvokePublic(
                    hearingMemory,
                    "BeginNoiseInvestigation",
                    CreateObservation(
                        "noise-root",
                        rootPosition,
                        now,
                        0.7d,
                        400L,
                        1UL));

                var weakUnrelated =
                    CreateObservation(
                        "noise-weak",
                        new Vector3(
                            20f,
                            0f,
                            0f),
                        now.AddMilliseconds(10),
                        0.8d,
                        400L,
                        2UL);

                SetField(
                    controller,
                    "_currentHearingObservations",
                    ObservationArray(
                        weakUnrelated));

                SetField(
                    controller,
                    "_currentHearingEvaluationTimeUtc",
                    now.AddMilliseconds(20));

                InvokePrivate(
                    controller,
                    "TryUpdateHeardNoiseSearchFromCurrentFrame");

                Assert.That(
                    Read<string>(
                        hearingMemory,
                        "ActiveNoiseEventId"),
                    Is.EqualTo("noise-root"));

                Assert.That(
                    Read<Vector3>(
                        hearingMemory,
                        "InvestigationPosition"),
                    Is.EqualTo(rootPosition));

                var searchContext =
                    GetField(
                        controller,
                        "_searchContext");

                Assert.That(
                    Read<Vector3>(
                        searchContext,
                        "SearchOriginPosition"),
                    Is.EqualTo(rootPosition));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(
                    gameObject);
            }
        }

        [Test]
        public void StrongUnrelatedNoise_RedirectsHeardNoiseSearch()
        {
            var gameObject =
                new GameObject(
                    "STK_Hearing_StrongInterrupt");

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
                        "SEARCH"));

                var now =
                    DateTime.UtcNow;

                var rootPosition =
                    Vector3.zero;

                var interruptPosition =
                    new Vector3(
                        20f,
                        0f,
                        0f);

                SetField(
                    controller,
                    "_searchContext",
                    CreateSearchContext(
                        rootPosition));

                var hearingMemory =
                    GetField(
                        controller,
                        "_hearingMemory");

                InvokePublic(
                    hearingMemory,
                    "BeginNoiseInvestigation",
                    CreateObservation(
                        "noise-root",
                        rootPosition,
                        now,
                        0.5d,
                        500L,
                        1UL));

                var strongUnrelated =
                    CreateObservation(
                        "noise-interrupt",
                        interruptPosition,
                        now.AddMilliseconds(10),
                        0.8d,
                        500L,
                        2UL);

                SetField(
                    controller,
                    "_currentHearingObservations",
                    ObservationArray(
                        strongUnrelated));

                SetField(
                    controller,
                    "_currentHearingEvaluationTimeUtc",
                    now.AddMilliseconds(20));

                InvokePrivate(
                    controller,
                    "TryUpdateHeardNoiseSearchFromCurrentFrame");

                Assert.That(
                    Read<string>(
                        hearingMemory,
                        "ActiveNoiseEventId"),
                    Is.EqualTo("noise-interrupt"));

                Assert.That(
                    Read<Vector3>(
                        hearingMemory,
                        "InvestigationPosition"),
                    Is.EqualTo(interruptPosition));

                var searchContext =
                    GetField(
                        controller,
                        "_searchContext");

                Assert.That(
                    Read<Vector3>(
                        searchContext,
                        "SearchOriginPosition"),
                    Is.EqualTo(interruptPosition));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(
                    gameObject);
            }
        }

        private static object CreateSearchContext(
            Vector3 origin)
        {
            var sourceType =
                ResolveType(
                    SearchSourceTypeName);

            var source =
                Enum.Parse(
                    sourceType,
                    "HeardNoise");

            var episodeId =
                Activator.CreateInstance(
                    ResolveType(
                        SearchEpisodeIdTypeName),
                    new object[]
                    {
                        1L
                    });

            var simulationTime =
                Activator.CreateInstance(
                    ResolveType(
                        SimulationTimeTypeName),
                    new object[]
                    {
                        1L,
                        1d
                    });

            var regionId =
                Activator.CreateInstance(
                    ResolveType(
                        RegionIdTypeName),
                    new object[]
                    {
                        1
                    });

            var contextType =
                ResolveType(
                    SearchContextTypeName);

            var constructor =
                contextType.GetConstructor(
                    new[]
                    {
                        ResolveType(
                            SearchEpisodeIdTypeName),
                        sourceType,
                        typeof(Vector3),
                        typeof(Vector3),
                        ResolveType(
                            SimulationTimeTypeName),
                        ResolveType(
                            RegionIdTypeName)
                    });

            Assert.That(
                constructor,
                Is.Not.Null);

            return constructor.Invoke(
                new[]
                {
                    episodeId,
                    source,
                    origin,
                    Vector3.forward,
                    simulationTime,
                    regionId
                });
        }

        private static object CreateObservation(
            string noiseEventId,
            Vector3 position,
            DateTime heardAtUtc,
            double effectiveIntensity,
            long tick,
            ulong ordinal)
        {
            var orderKey =
                Activator.CreateInstance(
                    ResolveType(
                        RuntimeNoiseEventOrderKeyTypeName),
                    new object[]
                    {
                        tick,
                        ordinal
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

        private static void InvokePublic(
            object target,
            string methodName,
            object argument)
        {
            var method =
                target.GetType().GetMethod(
                    methodName,
                    BindingFlags.Public |
                    BindingFlags.Instance);

            Assert.That(
                method,
                Is.Not.Null);

            method.Invoke(
                target,
                new[]
                {
                    argument
                });
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

                type = type.BaseType;
            }

            return null;
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
