using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class AdaptiveStalkerTargetPolicyTests
    {
        private const string Prefix = "EchoProtocol.AI.Stalker.";
        private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;

        [Test]
        public void STK_ADAPTIVE_NullCandidates_AreRejected()
        {
            var error = Assert.Throws<TargetInvocationException>(
                () => PolicyMethod.Invoke(null, new object[] { null, null }));
            Assert.That(error.InnerException, Is.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void STK_ADAPTIVE_IneligibleHighSignalTarget_NeverWins()
        {
            AssertPlayerId(Select(
                Candidate(1, 1f, false, true, 1f, 1f, 0f),
                Candidate(2, 5f, true)), 2);
        }

        [Test]
        public void STK_ADAPTIVE_DistanceAndSignals_SelectProductionPriorities()
        {
            AssertPlayerId(Select(Candidate(1, 1f, true), Candidate(2, 2f, true)), 1);
            AssertPlayerId(Select(Candidate(1, 1f, true), Candidate(2, 3f, true, false, 1f)), 2);
            AssertPlayerId(Select(Candidate(1, 1f, true), Candidate(2, 5f, true, true)), 2);
            AssertPlayerId(Select(Candidate(1, 1f, true), Candidate(2, 5f, true, false, 0f, 1f)), 2);
            AssertPlayerId(Select(Candidate(1, 1f, true, false, 0f, 0f, 1f), Candidate(2, 3f, true)), 2);
        }

        [Test]
        public void STK_ADAPTIVE_EqualScores_UseStablePlayerIdRegardlessOfOrder()
        {
            var five = Candidate(5, 2f, true);
            var two = Candidate(2, 2f, true);
            AssertPlayerId(Select(five, two), 2);
            AssertPlayerId(Select(two, five), 2);
        }

        [Test]
        public void STK_ADAPTIVE_NoEligibleCandidate_ReturnsFalse()
        {
            var args = new object[] { Candidates(Candidate(1, 1f, false)), null };
            Assert.That(PolicyMethod.Invoke(null, args), Is.EqualTo(false));
        }

        [Test]
        public void STK_ADAPTIVE_ScoreIsFiniteForMaximumDistance()
        {
            var method = Resolve(Prefix + "AdaptiveStalkerTargetPolicy").GetMethod(
                "CalculateScore", BindingFlags.Static | BindingFlags.NonPublic);
            var score = (float)method.Invoke(null, new[] { Candidate(1, float.MaxValue, true) });
            Assert.That(float.IsNaN(score) || float.IsInfinity(score), Is.False);
        }

        [Test]
        public void STK_ADAPTIVE_Selection_DoesNotMutateCandidates()
        {
            var candidates = Candidates(Candidate(4, 3f, true), Candidate(2, 2f, true));
            var args = new object[] { candidates, null };
            Assert.That(PolicyMethod.Invoke(null, args), Is.EqualTo(true));
            Assert.That(Id(GetProperty(candidates.GetValue(0), "PlayerId")), Is.EqualTo(4));
            Assert.That(Id(GetProperty(candidates.GetValue(1), "PlayerId")), Is.EqualTo(2));
        }

        private static MethodInfo PolicyMethod => Resolve(Prefix + "AdaptiveStalkerTargetPolicy")
            .GetMethod("TrySelectTarget", PublicStatic);

        private static object Select(params object[] candidates)
        {
            var args = new object[] { Candidates(candidates), null };
            Assert.That(PolicyMethod.Invoke(null, args), Is.EqualTo(true));
            return args[1];
        }

        private static Array Candidates(params object[] values)
        {
            var array = Array.CreateInstance(Resolve(Prefix + "StalkerTargetPolicyCandidate"), values.Length);
            for (var i = 0; i < values.Length; i++) array.SetValue(values[i], i);
            return array;
        }

        private static object Candidate(int id, float distance, bool eligible,
            bool carrier = false, float isolation = 0f, float recent = 0f, float history = 0f)
        {
            var player = Activator.CreateInstance(Resolve("EchoProtocol.AI.Common.PlayerId"), id);
            var time = Activator.CreateInstance(Resolve("EchoProtocol.AI.Common.AiSimulationTime"), 10L, 1d);
            var observation = Activator.CreateInstance(Resolve(Prefix + "VisionObservation"),
                player, new Vector3(distance, 0f, 0f), Vector3.forward, time, distance);
            var eligibilityType = Resolve(Prefix + "StalkerTargetEligibilityResult");
            var eligibility = eligible
                ? eligibilityType.GetMethod("EligibleTarget", PublicStatic).Invoke(null, null)
                : Activator.CreateInstance(eligibilityType);
            var target = Activator.CreateInstance(Resolve(Prefix + "StalkerTargetCandidate"),
                observation, eligibility);
            var signals = Activator.CreateInstance(Resolve(Prefix + "StalkerTargetPolicySignals"),
                carrier, isolation, recent, history);
            return Activator.CreateInstance(Resolve(Prefix + "StalkerTargetPolicyCandidate"),
                target, signals);
        }

        private static void AssertPlayerId(object observation, int expected)
        {
            Assert.That(Id(GetProperty(observation, "PlayerId")), Is.EqualTo(expected));
        }

        private static int Id(object playerId) => (int)GetProperty(playerId, "Value");

        private static object GetProperty(object value, string name)
        {
            Assert.That(value, Is.Not.Null);
            var property = value.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            return property.GetValue(value);
        }

        private static Type Resolve(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }

            Assert.Fail($"Could not resolve production type {fullName}.");
            return null;
        }
    }
}
