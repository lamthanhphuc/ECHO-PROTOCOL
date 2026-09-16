using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class AdaptiveStalkerTargetPolicyTests
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
        private const string SignalsTypeName =
            "EchoProtocol.AI.Stalker.StalkerTargetPolicySignals";
        private const string PolicyCandidateTypeName =
            "EchoProtocol.AI.Stalker.StalkerTargetPolicyCandidate";
        private const string ContextTypeName =
            "EchoProtocol.AI.Stalker.StalkerTargetPolicyContext";
        private const string WeightsTypeName =
            "EchoProtocol.AI.Stalker.StalkerTargetPolicyWeights";
        private const string PolicyTypeName =
            "EchoProtocol.AI.Stalker.AdaptiveStalkerTargetPolicy";

        [Test]
        public void STK_ADAPTIVE_NullCandidates_AreRejected()
        {
            var policy = CreatePolicy();
            var method = GetPolicyMethod(policy);
            var exception = Assert.Throws<TargetInvocationException>(
                () => method.Invoke(
                    policy,
                    new[] { null, CreateContext(), null }));

            Assert.That(
                exception.InnerException,
                Is.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void STK_ADAPTIVE_IneligibleHighSignalTarget_NeverWins()
        {
            var selected = Select(
                CreateCandidate(1, 1f, false, true, 1f, 1f, 1f, 1f),
                CreateCandidate(2, 5f, true, false, 0f, 0f, 0f, 0f));

            AssertPlayerId(selected, 2);
        }

        [Test]
        public void STK_ADAPTIVE_NearerTarget_WinsWhenSignalsMatch()
        {
            AssertPlayerId(
                Select(
                    CreateCandidate(1, 5f, true),
                    CreateCandidate(2, 2f, true)),
                2);
        }

        [Test]
        public void STK_ADAPTIVE_IsolationCanOutweighDistance()
        {
            var weights = CreateWeights(1f, 2f, 0f, 0f, 0f, 0f, 0.0001f);
            AssertPlayerId(
                SelectWithPolicy(
                    CreatePolicy(weights),
                    CreateCandidate(1, 1f, true, false, 0f, 0f, 0f, 0f),
                    CreateCandidate(2, 3f, true, false, 1f, 0f, 0f, 0f)),
                2);
        }

        [Test]
        public void STK_ADAPTIVE_ObjectiveCarrierBonus_AffectsSelection()
        {
            var weights = CreateWeights(0f, 0f, 1f, 0f, 0f, 0f, 0.0001f);
            AssertPlayerId(
                SelectWithPolicy(
                    CreatePolicy(weights),
                    CreateCandidate(1, 1f, true),
                    CreateCandidate(2, 5f, true, true)),
                2);
        }

        [Test]
        public void STK_ADAPTIVE_ConfirmedNoiseSignal_AffectsSelection()
        {
            var weights = CreateWeights(0f, 0f, 0f, 0f, 1f, 0f, 0.0001f);
            AssertPlayerId(
                SelectWithPolicy(
                    CreatePolicy(weights),
                    CreateCandidate(1, 1f, true),
                    CreateCandidate(2, 5f, true, false, 0f, 0f, 1f, 0f)),
                2);
        }

        [Test]
        public void STK_ADAPTIVE_TargetHistorySignal_AffectsSelection()
        {
            var weights = CreateWeights(0f, 0f, 0f, 0f, 0f, 1f, 0.0001f);
            AssertPlayerId(
                SelectWithPolicy(
                    CreatePolicy(weights),
                    CreateCandidate(1, 1f, true),
                    CreateCandidate(2, 5f, true, false, 0f, 0f, 0f, 1f)),
                2);
        }

        [Test]
        public void STK_ADAPTIVE_RecentDetectionSignal_AffectsSelection()
        {
            var weights = CreateWeights(0f, 0f, 0f, 1f, 0f, 0f, 0.0001f);
            AssertPlayerId(
                SelectWithPolicy(
                    CreatePolicy(weights),
                    CreateCandidate(1, 1f, true),
                    CreateCandidate(2, 5f, true, false, 0f, 1f, 0f, 0f)),
                2);
        }

        [Test]
        public void STK_ADAPTIVE_AllScoreComponents_AreFinite()
        {
            var policy = CreatePolicy();
            var candidate = CreateCandidate(
                1,
                float.MaxValue,
                true,
                true,
                1f,
                1f,
                1f,
                1f);
            var breakdown = policy.GetType().GetMethod(
                "CalculateScore",
                BindingFlags.Instance | BindingFlags.Public).Invoke(
                    policy,
                    new[] { candidate });

            var propertyNames = new[]
            {
                "DistanceContribution",
                "IsolationContribution",
                "ObjectiveCarrierContribution",
                "RecentDetectionContribution",
                "ConfirmedNoiseContribution",
                "TargetHistoryContribution",
                "TotalScore"
            };

            for (var i = 0; i < propertyNames.Length; i++)
            {
                var value = (float)GetProperty(
                    breakdown,
                    propertyNames[i]);
                Assert.That(float.IsNaN(value), Is.False);
                Assert.That(float.IsInfinity(value), Is.False);
            }
        }

        [Test]
        public void STK_ADAPTIVE_EqualScores_UseStablePlayerIdRegardlessOfOrder()
        {
            var playerFive = CreateCandidate(5, 2f, true);
            var playerTwo = CreateCandidate(2, 2f, true);

            AssertPlayerId(Select(playerFive, playerTwo), 2);
            AssertPlayerId(Select(playerTwo, playerFive), 2);
        }

        [Test]
        public void STK_ADAPTIVE_ZeroSignals_PreserveDistanceBehavior()
        {
            AssertPlayerId(
                Select(
                    CreateCandidate(3, 8f, true),
                    CreateCandidate(2, 4f, true),
                    CreateCandidate(1, 6f, true)),
                2);
        }

        [Test]
        public void STK_ADAPTIVE_InvalidWeights_AreRejected()
        {
            AssertInvalidWeight(float.NaN);
            AssertInvalidWeight(float.PositiveInfinity);
            AssertInvalidWeight(-0.01f);
        }

        [Test]
        public void STK_ADAPTIVE_Selection_DoesNotMutateCandidates()
        {
            var first = CreateCandidate(4, 3f, true, false, 0.2f, 0.3f, 0.4f, 0.5f);
            var second = CreateCandidate(2, 2f, true, true, 0.6f, 0.7f, 0.8f, 0.9f);
            var candidates = CreateCandidateArray(first, second);

            SelectArray(CreatePolicy(), candidates);

            Assert.That(GetPlayerId(candidates.GetValue(0)), Is.EqualTo(4));
            Assert.That(GetPlayerId(candidates.GetValue(1)), Is.EqualTo(2));
            Assert.That(
                GetProperty(
                    GetProperty(candidates.GetValue(0), "Signals"),
                    "TargetHistory01"),
                Is.EqualTo(0.5f));
        }

        private static object Select(params object[] candidates)
        {
            return SelectWithPolicy(CreatePolicy(), candidates);
        }

        private static object SelectWithPolicy(
            object policy,
            params object[] candidates)
        {
            return SelectArray(
                policy,
                CreateCandidateArray(candidates));
        }

        private static object SelectArray(
            object policy,
            Array candidates)
        {
            var args = new object[]
            {
                candidates,
                CreateContext(),
                null
            };
            var selected = GetPolicyMethod(policy).Invoke(policy, args);
            Assert.That(selected, Is.EqualTo(true));
            return args[2];
        }

        private static MethodInfo GetPolicyMethod(object policy)
        {
            var method = policy.GetType().GetMethod(
                "TrySelectTarget",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            return method;
        }

        private static object CreatePolicy(object weights = null)
        {
            return weights == null
                ? Activator.CreateInstance(ResolveType(PolicyTypeName))
                : Activator.CreateInstance(
                    ResolveType(PolicyTypeName),
                    weights);
        }

        private static object CreateWeights(
            float distance,
            float isolation,
            float objective,
            float recent,
            float noise,
            float history,
            float epsilon)
        {
            return Activator.CreateInstance(
                ResolveType(WeightsTypeName),
                distance,
                isolation,
                objective,
                recent,
                noise,
                history,
                epsilon);
        }

        private static void AssertInvalidWeight(float value)
        {
            var exception = Assert.Throws<TargetInvocationException>(
                () => CreateWeights(value, 0f, 0f, 0f, 0f, 0f, 0f));
            Assert.That(
                exception.InnerException,
                Is.TypeOf<ArgumentOutOfRangeException>());
        }

        private static object CreateCandidate(
            int playerId,
            float distance,
            bool eligible,
            bool objective = false,
            float isolation = 0f,
            float recent = 0f,
            float noise = 0f,
            float history = 0f)
        {
            var observation = Activator.CreateInstance(
                ResolveType(ObservationTypeName),
                CreatePlayerId(playerId),
                new Vector3(playerId, 0f, 0f),
                Vector3.forward,
                CreateTime(10L, 1d),
                distance);
            var eligibility = eligible
                ? ResolveType(EligibilityTypeName).GetMethod(
                    "EligibleTarget",
                    BindingFlags.Static | BindingFlags.Public).Invoke(
                        null,
                        null)
                : Activator.CreateInstance(
                    ResolveType(EligibilityTypeName));
            var target = Activator.CreateInstance(
                ResolveType(TargetCandidateTypeName),
                observation,
                eligibility);
            var signals = Activator.CreateInstance(
                ResolveType(SignalsTypeName),
                objective,
                isolation,
                recent,
                noise,
                history);

            return Activator.CreateInstance(
                ResolveType(PolicyCandidateTypeName),
                target,
                signals);
        }

        private static Array CreateCandidateArray(params object[] candidates)
        {
            var result = Array.CreateInstance(
                ResolveType(PolicyCandidateTypeName),
                candidates.Length);
            for (var i = 0; i < candidates.Length; i++)
            {
                result.SetValue(candidates[i], i);
            }

            return result;
        }

        private static object CreateContext()
        {
            return Activator.CreateInstance(
                ResolveType(ContextTypeName),
                CreateTime(20L, 2d),
                Activator.CreateInstance(
                    ResolveType(PlayerIdTypeName)));
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

        private static int GetPlayerId(object policyCandidate)
        {
            return (int)GetProperty(
                GetProperty(policyCandidate, "PlayerId"),
                "Value");
        }

        private static void AssertPlayerId(
            object observation,
            int expected)
        {
            Assert.That(
                (int)GetProperty(
                    GetProperty(observation, "PlayerId"),
                    "Value"),
                Is.EqualTo(expected));
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
