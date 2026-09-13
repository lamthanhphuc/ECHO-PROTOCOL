using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerHearingSelectorTests
    {
        private const string SelectorTypeName =
            "EchoProtocol.AI.Stalker.Hearing.StalkerHearingSelector";

        private const string HearingMemoryTypeName =
            "EchoProtocol.AI.Stalker.Hearing.StalkerHearingMemory";

        private const string HearingObservationTypeName =
            "EchoProtocol.AI.Listener.Perception.HearingObservation";

        private const string RuntimeNoiseEventOrderKeyTypeName =
            "EchoProtocol.AI.Listener.Noise.RuntimeNoiseEventOrderKey";

        private const string RuntimeNoiseTypeName =
            "EchoProtocol.AI.Listener.Noise.RuntimeNoiseType";

        private const string OcclusionClassTypeName =
            "EchoProtocol.AI.Listener.Perception.ListenerOcclusionClass";

        [Test]
        public void TrySelectInitial_SelectsStrongestRegardlessOfInputOrder()
        {
            var selector = CreateSelector();
            var now = DateTime.UtcNow;

            var weak = Observation(
                "weak",
                new Vector3(1f, 0f, 0f),
                now,
                0.25d,
                10,
                1);

            var strong = Observation(
                "strong",
                new Vector3(5f, 0f, 0f),
                now,
                0.9d,
                10,
                2);

            var medium = Observation(
                "medium",
                new Vector3(2f, 0f, 0f),
                now,
                0.5d,
                10,
                3);

            var selectionA = SelectInitial(
                selector,
                ObservationArray(
                    weak,
                    strong,
                    medium),
                now);

            var selectionB = SelectInitial(
                selector,
                ObservationArray(
                    medium,
                    weak,
                    strong),
                now);

            Assert.That(
                SelectionNoiseEventId(selectionA),
                Is.EqualTo("strong"));

            Assert.That(
                SelectionNoiseEventId(selectionB),
                Is.EqualTo("strong"));

            Assert.That(
                SelectionReason(selectionA),
                Is.EqualTo("InitialInvestigation"));
        }

        [Test]
        public void TrySelectInitial_IgnoresExpiredObservations()
        {
            var selector = CreateSelector();
            var now = DateTime.UtcNow;

            var expiredStrong = Observation(
                "expired",
                new Vector3(1f, 0f, 0f),
                now.AddSeconds(-10),
                1d,
                1,
                1,
                expiresAtUtc: now.AddSeconds(-1));

            var validWeak = Observation(
                "valid",
                new Vector3(2f, 0f, 0f),
                now,
                0.3d,
                2,
                1,
                expiresAtUtc: now.AddSeconds(5));

            var selection = SelectInitial(
                selector,
                ObservationArray(
                    expiredStrong,
                    validWeak),
                now);

            Assert.That(
                SelectionNoiseEventId(selection),
                Is.EqualTo("valid"));
        }

        [Test]
        public void InvestigationUpdate_UsesRelatedSupport()
        {
            var selector = CreateSelector();
            var memory = CreateMemory();
            var now = DateTime.UtcNow;

            BeginInvestigation(
                memory,
                Observation(
                    "root",
                    new Vector3(10f, 0f, 10f),
                    now,
                    0.7d));

            var related = Observation(
                "related",
                new Vector3(11f, 0f, 10f),
                now.AddMilliseconds(10),
                0.5d);

            var selection =
                SelectInvestigationUpdate(
                    selector,
                    memory,
                    ObservationArray(related),
                    now.AddMilliseconds(20));

            Assert.That(
                SelectionNoiseEventId(selection),
                Is.EqualTo("related"));

            Assert.That(
                SelectionReason(selection),
                Is.EqualTo("RelatedSupport"));
        }

        [Test]
        public void InvestigationUpdate_StrongerUnrelatedNoiseInterruptsCurrentHypothesis()
        {
            var selector = CreateSelector();
            var memory = CreateMemory();
            var now = DateTime.UtcNow;

            BeginInvestigation(
                memory,
                Observation(
                    "root",
                    Vector3.zero,
                    now,
                    0.5d));

            var relatedSupport = Observation(
                "support",
                new Vector3(1f, 0f, 0f),
                now.AddMilliseconds(10),
                0.6d);

            var strongerUnrelated = Observation(
                "interrupt",
                new Vector3(20f, 0f, 0f),
                now.AddMilliseconds(20),
                0.8d);

            var selection =
                SelectInvestigationUpdate(
                    selector,
                    memory,
                    ObservationArray(
                        relatedSupport,
                        strongerUnrelated),
                    now.AddMilliseconds(30));

            Assert.That(
                SelectionNoiseEventId(selection),
                Is.EqualTo("interrupt"));

            Assert.That(
                SelectionReason(selection),
                Is.EqualTo("StrongerUnrelatedInterrupt"));
        }

        [Test]
        public void InvestigationUpdate_WeakUnrelatedNoiseDoesNotRedirect()
        {
            var selector = CreateSelector();
            var memory = CreateMemory();
            var now = DateTime.UtcNow;

            BeginInvestigation(
                memory,
                Observation(
                    "root",
                    Vector3.zero,
                    now,
                    0.7d));

            var weakUnrelated = Observation(
                "weak-unrelated",
                new Vector3(20f, 0f, 0f),
                now.AddMilliseconds(10),
                0.8d);

            var result =
                TrySelectInvestigationUpdate(
                    selector,
                    memory,
                    ObservationArray(weakUnrelated),
                    now.AddMilliseconds(20),
                    out var selection);

            //
            // Default interrupt margin is 0.2:
            // required intensity = 0.7 + 0.2 = 0.9.
            //
            Assert.That(result, Is.False);
            Assert.That(selection, Is.Null);
        }

        [Test]
        public void InvestigationUpdate_PrioritizesStrongInterruptOverRelatedSupport()
        {
            var selector = CreateSelector();
            var memory = CreateMemory();
            var now = DateTime.UtcNow;

            BeginInvestigation(
                memory,
                Observation(
                    "root",
                    Vector3.zero,
                    now,
                    0.4d));

            var veryStrongRelated = Observation(
                "related",
                new Vector3(1f, 0f, 0f),
                now.AddMilliseconds(10),
                1d);

            var qualifyingInterrupt = Observation(
                "interrupt",
                new Vector3(10f, 0f, 0f),
                now.AddMilliseconds(20),
                0.7d);

            var selection =
                SelectInvestigationUpdate(
                    selector,
                    memory,
                    ObservationArray(
                        veryStrongRelated,
                        qualifyingInterrupt),
                    now.AddMilliseconds(30));

            Assert.That(
                SelectionNoiseEventId(selection),
                Is.EqualTo("interrupt"));

            Assert.That(
                SelectionReason(selection),
                Is.EqualTo("StrongerUnrelatedInterrupt"));
        }

        private static object CreateSelector()
        {
            return Activator.CreateInstance(
                ResolveType(SelectorTypeName),
                new object[]
                {
                    2.5f,
                    0.2d
                });
        }

        private static object CreateMemory()
        {
            return Activator.CreateInstance(
                ResolveType(HearingMemoryTypeName));
        }

        private static void BeginInvestigation(
            object memory,
            object observation)
        {
            var method =
                memory.GetType().GetMethod(
                    "BeginNoiseInvestigation",
                    BindingFlags.Public |
                    BindingFlags.Instance);

            Assert.That(method, Is.Not.Null);

            method.Invoke(
                memory,
                new[]
                {
                    observation
                });
        }

        private static object SelectInitial(
            object selector,
            Array observations,
            DateTime nowUtc)
        {
            var method =
                selector.GetType().GetMethod(
                    "TrySelectInitial",
                    BindingFlags.Public |
                    BindingFlags.Instance);

            Assert.That(method, Is.Not.Null);

            var arguments =
                new object[]
                {
                    observations,
                    nowUtc,
                    null
                };

            var result =
                (bool)method.Invoke(
                    selector,
                    arguments);

            Assert.That(result, Is.True);
            Assert.That(arguments[2], Is.Not.Null);

            return arguments[2];
        }

        private static object SelectInvestigationUpdate(
            object selector,
            object memory,
            Array observations,
            DateTime nowUtc)
        {
            var result =
                TrySelectInvestigationUpdate(
                    selector,
                    memory,
                    observations,
                    nowUtc,
                    out var selection);

            Assert.That(result, Is.True);
            Assert.That(selection, Is.Not.Null);

            return selection;
        }

        private static bool TrySelectInvestigationUpdate(
            object selector,
            object memory,
            Array observations,
            DateTime nowUtc,
            out object selection)
        {
            var method =
                selector.GetType().GetMethod(
                    "TrySelectInvestigationUpdate",
                    BindingFlags.Public |
                    BindingFlags.Instance);

            Assert.That(method, Is.Not.Null);

            var arguments =
                new object[]
                {
                    memory,
                    observations,
                    nowUtc,
                    null
                };

            var result =
                (bool)method.Invoke(
                    selector,
                    arguments);

            selection =
                result
                    ? arguments[3]
                    : null;

            return result;
        }

        private static string SelectionNoiseEventId(
            object selection)
        {
            var observation =
                Read(
                    selection,
                    "Observation");

            return (string)Read(
                observation,
                "NoiseEventId");
        }

        private static string SelectionReason(
            object selection)
        {
            return Read(
                    selection,
                    "Reason")
                .ToString();
        }

        private static Array ObservationArray(
            params object[] observations)
        {
            var elementType =
                ResolveType(
                    HearingObservationTypeName);

            var array =
                Array.CreateInstance(
                    elementType,
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

        private static object Observation(
            string noiseEventId,
            Vector3 position,
            DateTime emittedAtUtc,
            double effectiveIntensity,
            long authoritativeTick = 1,
            ulong ordinal = 1,
            DateTime? expiresAtUtc = null)
        {
            var orderKey =
                Activator.CreateInstance(
                    ResolveType(
                        RuntimeNoiseEventOrderKeyTypeName),
                    new object[]
                    {
                        authoritativeTick,
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
                    emittedAtUtc,
                    emittedAtUtc,
                    expiresAtUtc
                        ?? emittedAtUtc.AddSeconds(5),
                    (double)position.magnitude,
                    1d,
                    effectiveIntensity,
                    occlusion
                });
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

            return property.GetValue(target);
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
