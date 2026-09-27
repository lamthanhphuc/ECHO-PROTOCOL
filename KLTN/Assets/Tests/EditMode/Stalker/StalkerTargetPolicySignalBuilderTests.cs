using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerTargetPolicySignalBuilderTests
    {
        private const string PlayerIdTypeName =
            "EchoProtocol.AI.Common.PlayerId";
        private const string SimulationTimeTypeName =
            "EchoProtocol.AI.Common.AiSimulationTime";
        private const string ObservationTypeName =
            "EchoProtocol.AI.Stalker.VisionObservation";
        private const string EligibilityTypeName =
            "EchoProtocol.AI.Stalker.StalkerTargetEligibilityResult";
        private const string TargetCandidateTypeName =
            "EchoProtocol.AI.Stalker.StalkerTargetCandidate";
        private const string PolicyCandidateTypeName =
            "EchoProtocol.AI.Stalker.StalkerTargetPolicyCandidate";
        private const string HistoryTypeName =
            "EchoProtocol.AI.Stalker.StalkerTargetHistoryMemory";
        private const string BuilderTypeName =
            "EchoProtocol.AI.Stalker.StalkerTargetPolicySignalBuilder";

        [Test]
        public void STK_SIGNALS_Isolation_UsesOnlyCurrentVisibleEligibleList()
        {
            var history = CreateHistory();
            Invoke(
                history,
                "RecordVisibleObservation",
                CreatePlayerId(99),
                CreateTime(0L, 0d));
            var results = Build(
                history,
                0d,
                CreateCandidate(1, Vector3.zero, true),
                CreateCandidate(2, new Vector3(4f, 0f, 0f), true),
                CreateCandidate(3, new Vector3(100f, 0f, 0f), false));

            AssertSignal(results, 0, "VisibleIsolation01", 0.5f);
            AssertSignal(results, 1, "VisibleIsolation01", 0.5f);
            AssertSignal(results, 2, "VisibleIsolation01", 0f);
        }

        [Test]
        public void STK_SIGNALS_OneVisibleEligiblePlayer_IsFullyIsolated()
        {
            var results = Build(
                CreateHistory(),
                0d,
                CreateCandidate(1, Vector3.zero, true));

            AssertSignal(results, 0, "VisibleIsolation01", 1f);
        }

        [Test]
        public void STK_SIGNALS_RecentDetectionAndHistory_DecayBySimulationTime()
        {
            var history = CreateHistory();
            Invoke(
                history,
                "RecordVisibleObservation",
                CreatePlayerId(1),
                CreateTime(0L, 0d));
            Invoke(
                history,
                "RecordTargetAcquired",
                CreatePlayerId(1),
                CreateTime(0L, 0d));

            var results = Build(
                history,
                5d,
                CreateCandidate(1, Vector3.zero, true));

            AssertSignal(results, 0, "RecentDetection01", 0.5f);
            AssertSignal(
                results,
                0,
                "TargetHistory01",
                1f - (5f / 45f));
        }

        [Test]
        public void STK_SIGNALS_LegalObjectiveIdentity_IsUsed()
        {
            var results = BuildWithCarriers(
                CreateHistory(),
                0d,
                new[] { 1 },
                CreateCandidate(1, Vector3.zero, true));
            var signals = GetSignals(results, 0);

            Assert.That(
                GetProperty(signals, "IsObjectiveCarrier"),
                Is.EqualTo(true));
        }

        private static IList Build(
            object history,
            double seconds,
            params object[] candidates)
        {
            return BuildWithCarriers(
                history,
                seconds,
                null,
                candidates);
        }

        private static IList BuildWithCarriers(
            object history,
            double seconds,
            int[] carrierIds,
            params object[] candidates)
        {
            var policyCandidateType = ResolveType(
                PolicyCandidateTypeName);
            var listType = typeof(List<>).MakeGenericType(
                policyCandidateType);
            var results = Activator.CreateInstance(listType);
            var method = ResolveType(BuilderTypeName).GetMethod(
                "Build", BindingFlags.Static | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new object[]
            {
                CreateArray(TargetCandidateTypeName, candidates),
                CreatePlayerIdArray(carrierIds),
                CreateTime((long)seconds, seconds),
                history,
                results
            });

            return (IList)results;
        }

        private static Array CreatePlayerIdArray(int[] values)
        {
            if (values == null)
            {
                return null;
            }

            var result = Array.CreateInstance(
                ResolveType(PlayerIdTypeName),
                values.Length);
            for (var i = 0; i < values.Length; i++)
            {
                result.SetValue(CreatePlayerId(values[i]), i);
            }

            return result;
        }

        private static object CreateCandidate(
            int playerId,
            Vector3 position,
            bool eligible)
        {
            var observation = Activator.CreateInstance(
                ResolveType(ObservationTypeName),
                CreatePlayerId(playerId),
                position,
                Vector3.forward,
                CreateTime(0L, 0d),
                position.magnitude);
            var eligibility = eligible
                ? ResolveType(EligibilityTypeName).GetMethod(
                    "EligibleTarget",
                    BindingFlags.Static | BindingFlags.Public).Invoke(
                        null,
                        null)
                : Activator.CreateInstance(
                    ResolveType(EligibilityTypeName));

            return Activator.CreateInstance(
                ResolveType(TargetCandidateTypeName),
                observation,
                eligibility);
        }

        private static Array CreateArray(
            string elementTypeName,
            object[] values)
        {
            var result = Array.CreateInstance(
                ResolveType(elementTypeName),
                values.Length);
            for (var i = 0; i < values.Length; i++)
            {
                result.SetValue(values[i], i);
            }

            return result;
        }

        private static object CreateHistory()
        {
            return Activator.CreateInstance(
                ResolveType(HistoryTypeName));
        }

        private static void AssertSignal(
            IList results,
            int index,
            string propertyName,
            float expected)
        {
            Assert.That(
                GetProperty(GetSignals(results, index), propertyName),
                Is.EqualTo(expected).Within(0.0001f));
        }

        private static object GetSignals(IList results, int index)
        {
            return GetProperty(results[index], "Signals");
        }

        private static object Invoke(
            object target,
            string methodName,
            params object[] args)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(target, args);
        }

        private static object CreatePlayerId(int value)
        {
            return Activator.CreateInstance(
                ResolveType(PlayerIdTypeName),
                value);
        }

        private static object CreateTime(long tick, double seconds)
        {
            return Activator.CreateInstance(
                ResolveType(SimulationTimeTypeName),
                tick,
                seconds);
        }

        private static object GetProperty(
            object target,
            string propertyName)
        {
            Assert.That(target, Is.Not.Null);
            var property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            return property.GetValue(target);
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

            Assert.Fail($"Could not resolve production type '{fullTypeName}'.");
            return null;
        }
    }
}
