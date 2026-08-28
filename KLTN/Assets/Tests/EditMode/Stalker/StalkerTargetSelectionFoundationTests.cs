using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerTargetSelectionFoundationTests
    {
        private const string PlayerIdTypeName = "EchoProtocol.AI.Common.PlayerId";
        private const string AiSimulationTimeTypeName = "EchoProtocol.AI.Common.AiSimulationTime";
        private const string VisionObservationTypeName = "EchoProtocol.AI.Stalker.VisionObservation";
        private const string EligibilitySnapshotTypeName = "EchoProtocol.AI.Stalker.StalkerTargetEligibilitySnapshot";
        private const string EligibilityResultTypeName = "EchoProtocol.AI.Stalker.StalkerTargetEligibilityResult";
        private const string EligibilityServiceTypeName = "EchoProtocol.AI.Stalker.StalkerTargetEligibility";
        private const string TargetCandidateTypeName = "EchoProtocol.AI.Stalker.StalkerTargetCandidate";
        private const string TargetSelectorTypeName = "EchoProtocol.AI.Stalker.StalkerTargetSelector";

        [Test]
        public void STK_ELIG_ActiveConnectedStandingPlayer_IsEligible()
        {
            var result = EvaluateEligibility(CreateSnapshot(true, true, false, false, false));

            AssertEligibility(result, true, "Eligible");
        }

        [Test]
        public void STK_ELIG_InvalidPlayerStates_AreRejectedWithCanonicalReason()
        {
            AssertEligibility(EvaluateEligibility(CreateSnapshot(false, true, false, false, false)), false, "NotInActiveSession");
            AssertEligibility(EvaluateEligibility(CreateSnapshot(true, false, false, false, false)), false, "Disconnected");
            AssertEligibility(EvaluateEligibility(CreateSnapshot(true, true, true, false, false)), false, "Downed");
            AssertEligibility(EvaluateEligibility(CreateSnapshot(true, true, false, true, false)), false, "Eliminated");
            AssertEligibility(EvaluateEligibility(CreateSnapshot(true, true, false, false, true)), false, "OtherGameplayState");
            AssertEligibility(Activator.CreateInstance(ResolveType(EligibilityResultTypeName)), false, "NotInActiveSession");
        }

        [Test]
        public void STK_SEL_NearestEligibleVisiblePlayer_IsSelected()
        {
            var selected = Select(
                0f,
                CreateEligibleCandidate(1, 6f),
                CreateEligibleCandidate(2, 2f),
                CreateEligibleCandidate(3, 4f));

            AssertPlayerIdValue(GetProperty(selected, "PlayerId"), 2);
        }

        [Test]
        public void STK_SEL_IneligibleNearestPlayer_DoesNotBlockEligibleSelection()
        {
            var selected = Select(
                0f,
                CreateCandidate(1, 1f, CreateSnapshot(true, true, true, false, false)),
                CreateEligibleCandidate(2, 3f));

            AssertPlayerIdValue(GetProperty(selected, "PlayerId"), 2);

            Assert.That(
                TrySelect(
                    0f,
                    out _,
                    CreateCandidate(1, 1f, CreateSnapshot(true, true, true, false, false)),
                    CreateCandidate(2, 3f, CreateSnapshot(true, false, false, false, false))),
                Is.False);
        }

        [Test]
        public void STK_SEL_EffectiveNearestTie_UsesStablePlayerIdRegardlessOfInputOrder()
        {
            var playerFive = CreateEligibleCandidate(5, 2f);
            var playerTwo = CreateEligibleCandidate(2, 2.05f);

            AssertPlayerIdValue(GetProperty(Select(0.10f, playerFive, playerTwo), "PlayerId"), 2);
            AssertPlayerIdValue(GetProperty(Select(0.10f, playerTwo, playerFive), "PlayerId"), 2);
        }

        [Test]
        public void STK_SEL_TieWindowAnchorsToTrueMinimum_AndIsOrderIndependent()
        {
            var playerThirty = CreateEligibleCandidate(30, 1f);
            var playerTwenty = CreateEligibleCandidate(20, 1.05f);
            var playerOne = CreateEligibleCandidate(1, 1.10f);

            AssertPlayerIdValue(GetProperty(Select(0.06f, playerThirty, playerTwenty, playerOne), "PlayerId"), 20);
            AssertPlayerIdValue(GetProperty(Select(0.06f, playerOne, playerThirty, playerTwenty), "PlayerId"), 20);
            AssertPlayerIdValue(GetProperty(Select(0.06f, playerTwenty, playerOne, playerThirty), "PlayerId"), 20);
        }

        private static object Select(float epsilon, params object[] candidates)
        {
            Assert.That(TrySelect(epsilon, out var selected, candidates), Is.True);
            return selected;
        }

        private static bool TrySelect(float epsilon, out object selectedObservation, params object[] candidates)
        {
            var candidateArray = CreateCandidateArray(candidates);
            var args = new object[] { candidateArray, epsilon, null };
            var method = ResolveType(TargetSelectorTypeName).GetMethod(
                "TrySelectNearestEligibleVisible",
                BindingFlags.Static | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "Missing StalkerTargetSelector.TrySelectNearestEligibleVisible.");

            var result = method.Invoke(null, args);
            Assert.That(result, Is.TypeOf<bool>());

            selectedObservation = args[2];
            return (bool)result;
        }

        private static Array CreateCandidateArray(object[] candidates)
        {
            var candidateType = ResolveType(TargetCandidateTypeName);
            var candidateArray = Array.CreateInstance(candidateType, candidates.Length);
            for (var i = 0; i < candidates.Length; i++)
            {
                candidateArray.SetValue(candidates[i], i);
            }

            return candidateArray;
        }

        private static object CreateEligibleCandidate(int playerId, float distance)
        {
            return CreateCandidate(playerId, distance, CreateSnapshot(true, true, false, false, false));
        }

        private static object CreateCandidate(int playerId, float distance, object snapshot)
        {
            var player = CreatePlayerId(playerId);
            var observation = Activator.CreateInstance(
                ResolveType(VisionObservationTypeName),
                player,
                new Vector3(distance, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                CreateSimulationTime(10, 1d),
                distance);
            var eligibility = EvaluateEligibility(snapshot);

            return Activator.CreateInstance(
                ResolveType(TargetCandidateTypeName),
                observation,
                eligibility);
        }

        private static object CreateSnapshot(
            bool isInActiveSession,
            bool isConnected,
            bool isDowned,
            bool isEliminated,
            bool hasOtherInvalidGameplayState)
        {
            return Activator.CreateInstance(
                ResolveType(EligibilitySnapshotTypeName),
                isInActiveSession,
                isConnected,
                isDowned,
                isEliminated,
                hasOtherInvalidGameplayState);
        }

        private static object EvaluateEligibility(object snapshot)
        {
            var method = ResolveType(EligibilityServiceTypeName).GetMethod(
                "Evaluate",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { ResolveType(EligibilitySnapshotTypeName) },
                null);
            Assert.That(method, Is.Not.Null, "Missing StalkerTargetEligibility.Evaluate.");

            return method.Invoke(null, new[] { snapshot });
        }

        private static object CreatePlayerId(int value)
        {
            return Activator.CreateInstance(ResolveType(PlayerIdTypeName), value);
        }

        private static object CreateSimulationTime(long tick, double seconds)
        {
            return Activator.CreateInstance(ResolveType(AiSimulationTimeTypeName), tick, seconds);
        }

        private static void AssertEligibility(object result, bool expectedEligible, string expectedReason)
        {
            Assert.That(GetBoolProperty(result, "Eligible"), Is.EqualTo(expectedEligible));
            Assert.That(GetProperty(result, "Reason").ToString(), Is.EqualTo(expectedReason));
        }

        private static void AssertPlayerIdValue(object playerId, int expectedValue)
        {
            Assert.That(GetBoolProperty(playerId, "IsValid"), Is.True);
            Assert.That(GetProperty(playerId, "Value"), Is.EqualTo(expectedValue));
        }

        private static bool GetBoolProperty(object target, string propertyName)
        {
            var value = GetProperty(target, propertyName);
            Assert.That(value, Is.TypeOf<bool>(), $"Property '{propertyName}' must return bool.");
            return (bool)value;
        }

        private static object GetProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing public property '{propertyName}' on '{target.GetType().FullName}'.");

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

            Assert.Fail($"Could not find production type '{fullTypeName}' in the loaded Unity AppDomain.");
            return null;
        }
    }
}
