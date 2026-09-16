using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerTargetHistoryMemoryTests
    {
        private const string PlayerIdTypeName =
            "EchoProtocol.AI.Common.PlayerId";
        private const string SimulationTimeTypeName =
            "EchoProtocol.AI.Common.AiSimulationTime";
        private const string HistoryTypeName =
            "EchoProtocol.AI.Stalker.StalkerTargetHistoryMemory";
        private const string VisionObservationTypeName =
            "EchoProtocol.AI.Stalker.VisionObservation";
        private const string TargetCandidateTypeName =
            "EchoProtocol.AI.Stalker.StalkerTargetCandidate";
        private const string EligibilityResultTypeName =
            "EchoProtocol.AI.Stalker.StalkerTargetEligibilityResult";
        private const string EligibilityReasonTypeName =
            "EchoProtocol.AI.Stalker.StalkerTargetEligibilityReason";

        [Test]
        public void STK_HISTORY_RecentDetection_DecaysBySimulationTime()
        {
            var history = CreateHistory();
            Invoke(
                history,
                "RecordVisibleObservation",
                CreatePlayerId(1),
                CreateTime(0L, 0d));

            Assert.That(
                GetSignal(
                    history,
                    "GetRecentDetection01",
                    1,
                    5d,
                    10f),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(
                GetSignal(
                    history,
                    "GetRecentDetection01",
                    1,
                    10d,
                    10f),
                Is.EqualTo(0f));
        }

        [Test]
        public void STK_HISTORY_TargetPreference_DecaysAndExpires()
        {
            var history = CreateHistory();
            Invoke(
                history,
                "RecordTargetAcquired",
                CreatePlayerId(2),
                CreateTime(0L, 0d));

            Assert.That(
                GetSignal(
                    history,
                    "GetTargetHistory01",
                    2,
                    15d,
                    30f),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(
                GetSignal(
                    history,
                    "GetTargetHistory01",
                    2,
                    31d,
                    30f),
                Is.EqualTo(0f));
        }

        [Test]
        public void STK_HISTORY_Capacity_IsBounded()
        {
            var history = Activator.CreateInstance(
                ResolveType(HistoryTypeName),
                4);
            for (var playerId = 1; playerId <= 6; playerId++)
            {
                Invoke(
                    history,
                    "RecordVisibleObservation",
                    CreatePlayerId(playerId),
                    CreateTime(playerId, playerId));
            }

            Assert.That(
                GetProperty(history, "Count"),
                Is.EqualTo(4));
            Assert.That(
                GetSignal(
                    history,
                    "GetRecentDetection01",
                    1,
                    6d,
                    10f),
                Is.EqualTo(0f));
            Assert.That(
                GetSignal(
                    history,
                    "GetRecentDetection01",
                    6,
                    6d,
                    10f),
                Is.EqualTo(1f));
        }

        [Test]
        public void STK_HISTORY_RecordVisibleFrame_IgnoresIneligibleCandidate()
        {
            var history = CreateHistory();

            var ineligibleCandidate = CreateCandidate(
                playerId: 1,
                seconds: 1d,
                eligible: false);

            var eligibleCandidate = CreateCandidate(
                playerId: 2,
                seconds: 1d,
                eligible: true);

            var candidateType =
                ResolveType(TargetCandidateTypeName);

            var candidates = Array.CreateInstance(
                candidateType,
                2);

            candidates.SetValue(
                ineligibleCandidate,
                0);

            candidates.SetValue(
                eligibleCandidate,
                1);

            Invoke(
                history,
                "RecordVisibleFrame",
                candidates);

            Assert.That(
                GetProperty(history, "Count"),
                Is.EqualTo(1));

            Assert.That(
                GetSignal(
                    history,
                    "GetRecentDetection01",
                    1,
                    1d,
                    10f),
                Is.EqualTo(0f),
                "Ineligible visual candidates must not create target history.");

            Assert.That(
                GetSignal(
                    history,
                    "GetRecentDetection01",
                    2,
                    1d,
                    10f),
                Is.EqualTo(1f),
                "Eligible visual candidates should still create target history.");
        }

        private static object CreateCandidate(
            int playerId,
            double seconds,
            bool eligible)
        {
            var observation = Activator.CreateInstance(
                ResolveType(VisionObservationTypeName),
                CreatePlayerId(playerId),
                Vector3.zero,
                Vector3.forward,
                CreateTime((long)seconds, seconds),
                5f);

            object eligibility;

            var eligibilityType =
                ResolveType(EligibilityResultTypeName);

            if (eligible)
            {
                var method = eligibilityType.GetMethod(
                    "EligibleTarget",
                    BindingFlags.Static | BindingFlags.Public);

                Assert.That(method, Is.Not.Null);

                eligibility = method.Invoke(
                    null,
                    null);
            }
            else
            {
                var reasonType =
                    ResolveType(EligibilityReasonTypeName);

                var downedReason = Enum.Parse(
                    reasonType,
                    "Downed");

                var method = eligibilityType.GetMethod(
                    "Ineligible",
                    BindingFlags.Static | BindingFlags.Public);

                Assert.That(method, Is.Not.Null);

                eligibility = method.Invoke(
                    null,
                    new[]
                    {
                        downedReason
                    });
            }

            return Activator.CreateInstance(
                ResolveType(TargetCandidateTypeName),
                observation,
                eligibility);
        }

        private static object CreateHistory()
        {
            return Activator.CreateInstance(
                ResolveType(HistoryTypeName));
        }

        private static float GetSignal(
            object history,
            string methodName,
            int playerId,
            double seconds,
            float window)
        {
            return (float)Invoke(
                history,
                methodName,
                CreatePlayerId(playerId),
                CreateTime((long)seconds, seconds),
                window);
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
            return target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public).GetValue(target);
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
        [Test]
        public void STK_HISTORY_Reset_ClearsAllLearnedTargetHistory()
        {
            var history = CreateHistory();

            Invoke(
                history,
                "RecordVisibleObservation",
                CreatePlayerId(1),
                CreateTime(1L, 1d));

            Invoke(
                history,
                "RecordTargetAcquired",
                CreatePlayerId(2),
                CreateTime(2L, 2d));

            Assert.That(
                GetProperty(history, "Count"),
                Is.EqualTo(2));

            Invoke(
                history,
                "Reset");

            Assert.That(
                GetProperty(history, "Count"),
                Is.EqualTo(0));

            Assert.That(
                GetSignal(
                    history,
                    "GetRecentDetection01",
                    1,
                    2d,
                    10f),
                Is.EqualTo(0f));

            Assert.That(
                GetSignal(
                    history,
                    "GetTargetHistory01",
                    2,
                    2d,
                    45f),
                Is.EqualTo(0f));
        }
    }
}
