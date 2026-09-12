using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerHeardNoiseSearchTests
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
        public void HeardNoiseSearch_DoesNotRequireCurrentTargetId()
        {
            var gameObject =
                new GameObject(
                    "STK_HeardNoiseSearch_NoPlayerId");

            try
            {
                var controller =
                    gameObject.AddComponent(
                        ResolveType(
                            ControllerTypeName));

                SetPrivateField(
                    controller,
                    "currentState",
                    Enum.Parse(
                        ResolveType(StateTypeName),
                        "SEARCH"));

                SetPrivateField(
                    controller,
                    "_searchContext",
                    CreateHeardNoiseSearchContext(
                        new Vector3(8f, 0f, 4f)));

                //
                // Prevent this test from depending on authored
                // spatial graph / navigation candidates.
                //
                SetPrivateField(
                    controller,
                    "_searchCandidatePlanningExhausted",
                    true);

                BeginHearingInvestigation(
                    controller,
                    CreateObservation(
                        "heard-noise-1",
                        new Vector3(8f, 0f, 4f),
                        DateTime.UtcNow,
                        0.8d));

                //
                // CurrentTargetId remains invalid. This is the
                // invariant being tested.
                //
                var stalkerMemory =
                    GetPrivateField(
                        controller,
                        "_memory");

                var currentTargetId =
                    Read(
                        stalkerMemory,
                        "CurrentTargetId");

                Assert.That(
                    Read<bool>(
                        currentTargetId,
                        "IsValid"),
                    Is.False);

                InvokePrivate(
                    controller,
                    "TickSearch");

                //
                // HeardNoise SEARCH must remain SEARCH instead of
                // terminating because CurrentTargetId is invalid.
                //
                Assert.That(
                    GetPrivateField(
                        controller,
                        "currentState")
                    .ToString(),
                    Is.EqualTo("SEARCH"));

                var context =
                    GetPrivateField(
                        controller,
                        "_searchContext");

                Assert.That(
                    context,
                    Is.Not.Null);

                Assert.That(
                    Read(
                        context,
                        "Source")
                    .ToString(),
                    Is.EqualTo("HeardNoise"));

                //
                // Hearing must still not manufacture player identity.
                //
                currentTargetId =
                    Read(
                        stalkerMemory,
                        "CurrentTargetId");

                Assert.That(
                    Read<bool>(
                        currentTargetId,
                        "IsValid"),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(
                    gameObject);
            }
        }

        private static object CreateHeardNoiseSearchContext(
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
                        100L
                    });

            var simulationTime =
                Activator.CreateInstance(
                    ResolveType(
                        SimulationTimeTypeName),
                    new object[]
                    {
                        100L,
                        10d
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
            DateTime emittedAtUtc,
            double effectiveIntensity)
        {
            var orderKey =
                Activator.CreateInstance(
                    ResolveType(
                        RuntimeNoiseEventOrderKeyTypeName),
                    new object[]
                    {
                        100L,
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
                    emittedAtUtc,
                    emittedAtUtc,
                    emittedAtUtc.AddSeconds(5),
                    (double)position.magnitude,
                    1d,
                    effectiveIntensity,
                    occlusion
                });
        }

        private static void BeginHearingInvestigation(
            object controller,
            object observation)
        {
            var hearingMemory =
                GetPrivateField(
                    controller,
                    "_hearingMemory");

            var method =
                hearingMemory.GetType().GetMethod(
                    "BeginNoiseInvestigation",
                    BindingFlags.Public |
                    BindingFlags.Instance);

            Assert.That(
                method,
                Is.Not.Null);

            method.Invoke(
                hearingMemory,
                new[]
                {
                    observation
                });
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            var field =
                target.GetType().GetField(
                    fieldName,
                    BindingFlags.NonPublic |
                    BindingFlags.Instance);

            Assert.That(
                field,
                Is.Not.Null,
                $"Missing field '{fieldName}'.");

            field.SetValue(
                target,
                value);
        }

        private static object GetPrivateField(
            object target,
            string fieldName)
        {
            var field =
                target.GetType().GetField(
                    fieldName,
                    BindingFlags.NonPublic |
                    BindingFlags.Instance);

            Assert.That(
                field,
                Is.Not.Null,
                $"Missing field '{fieldName}'.");

            return field.GetValue(target);
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
