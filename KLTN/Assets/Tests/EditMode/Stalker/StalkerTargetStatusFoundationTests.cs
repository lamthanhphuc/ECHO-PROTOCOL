using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerTargetStatusFoundationTests
    {
        private const string PlayerIdTypeName = "EchoProtocol.AI.Common.PlayerId";
        private const string AiSimulationTimeTypeName = "EchoProtocol.AI.Common.AiSimulationTime";
        private const string AiSimulationStepTypeName = "EchoProtocol.AI.Common.AiSimulationStep";
        private const string VisionObservationTypeName = "EchoProtocol.AI.Stalker.VisionObservation";
        private const string TargetCandidateTypeName = "EchoProtocol.AI.Stalker.StalkerTargetCandidate";
        private const string TargetCandidateLookupTypeName = "EchoProtocol.AI.Stalker.StalkerTargetCandidateLookup";
        private const string TargetStatusTypeName = "EchoProtocol.AI.Stalker.StalkerTargetStatus";
        private const string TargetStatusLookupTypeName = "EchoProtocol.AI.Stalker.StalkerTargetStatusLookup";
        private const string EligibilityResultTypeName = "EchoProtocol.AI.Stalker.StalkerTargetEligibilityResult";
        private const string EligibilityReasonTypeName = "EchoProtocol.AI.Stalker.StalkerTargetEligibilityReason";
        private const string StalkerSimulationInputTypeName = "EchoProtocol.AI.Stalker.StalkerSimulationInput";

        [Test]
        public void STK_STATUS_UniquePlayerIdLookup_ReturnsExactEligibility()
        {
            var expected = CreateIneligibleResult("Downed");
            var statuses = CreateStatusList(
                CreateStatus(1, CreateEligibleResult()),
                CreateStatus(2, expected));

            Assert.That(TryGetUnique(statuses, CreatePlayerId(2), out var eligibility), Is.True);
            Assert.That(eligibility, Is.EqualTo(expected));
        }

        [Test]
        public void STK_STATUS_HiddenTargetStatus_DoesNotRequireVisionObservationOrTransform()
        {
            var status = CreateStatus(3, CreateIneligibleResult("Eliminated"));

            AssertPlayerIdValue(GetProperty(status, "PlayerId"), 3);
            Assert.That(GetBoolProperty(GetProperty(status, "Eligibility"), "Eligible"), Is.False);
            Assert.That(ResolveType(TargetStatusTypeName).GetProperty("Eligibility"), Is.Not.Null);
            Assert.That(ResolveType(TargetStatusTypeName).GetProperty("PlayerId"), Is.Not.Null);
            Assert.That(ResolveType(TargetStatusTypeName).GetProperty("TargetSample"), Is.Null);
            Assert.That(ResolveType(TargetStatusTypeName).GetProperty("TargetHierarchyRoot"), Is.Null);
            Assert.That(ResolveType(TargetStatusTypeName).GetProperty("Transform"), Is.Null);
        }

        [Test]
        public void STK_STATUS_UnknownPlayerIdLookup_ReturnsFalse()
        {
            var statuses = CreateStatusList(CreateStatus(1, CreateEligibleResult()));

            Assert.That(TryGetUnique(statuses, CreatePlayerId(2), out _), Is.False);
        }

        [Test]
        public void STK_STATUS_InvalidPlayerIdLookup_ReturnsFalse()
        {
            var statuses = CreateStatusList(CreateStatus(1, CreateEligibleResult()));

            Assert.That(TryGetUnique(statuses, GetStaticProperty(ResolveType(PlayerIdTypeName), "Invalid"), out _), Is.False);
        }

        [Test]
        public void STK_STATUS_NullStatusListLookup_ReturnsFalse()
        {
            Assert.That(TryGetUnique(null, CreatePlayerId(1), out _), Is.False);
        }

        [Test]
        public void STK_STATUS_DuplicatePlayerIdEntries_FailClosed()
        {
            var statuses = CreateStatusList(
                CreateStatus(1, CreateEligibleResult()),
                CreateStatus(1, CreateIneligibleResult("Downed")));

            Assert.That(TryGetUnique(statuses, CreatePlayerId(1), out var eligibility), Is.False);
            Assert.That(eligibility, Is.EqualTo(CreateDefaultEligibilityResult()));
        }

        [Test]
        public void STK_STATUS_Lookup_DoesNotMutateOrReorderCallerList()
        {
            var first = CreateStatus(2, CreateEligibleResult());
            var second = CreateStatus(1, CreateIneligibleResult("Downed"));
            var statuses = CreateStatusList(first, second);

            Assert.That(TryGetUnique(statuses, CreatePlayerId(1), out _), Is.True);

            Assert.That(GetListCount(statuses), Is.EqualTo(2));
            AssertPlayerIdValue(GetProperty(GetListItem(statuses, 0), "PlayerId"), 2);
            AssertPlayerIdValue(GetProperty(GetListItem(statuses, 1), "PlayerId"), 1);
        }

        [Test]
        public void STK_CANDIDATE_Lookup_ReturnsUniqueVisibleCandidate()
        {
            var candidates = CreateCandidateList(
                CreateCandidate(1, 4f, CreateEligibleResult()),
                CreateCandidate(2, 2f, CreateEligibleResult()));

            Assert.That(TryGetUniqueCandidate(candidates, CreatePlayerId(2), out var candidate, out var hasDuplicate), Is.True);
            Assert.That(hasDuplicate, Is.False);
            AssertPlayerIdValue(GetProperty(GetProperty(candidate, "Observation"), "PlayerId"), 2);
        }

        [Test]
        public void STK_CANDIDATE_Lookup_DuplicatePlayerIdFailsClosed()
        {
            var candidates = CreateCandidateList(
                CreateCandidate(1, 4f, CreateEligibleResult()),
                CreateCandidate(1, 2f, CreateEligibleResult()));

            Assert.That(TryGetUniqueCandidate(candidates, CreatePlayerId(1), out _, out var hasDuplicate), Is.False);
            Assert.That(hasDuplicate, Is.True);
        }

        [Test]
        public void STK_CANDIDATE_Lookup_InvalidPlayerId_ReturnsFalse()
        {
            var candidates = CreateCandidateList(CreateCandidate(1, 4f, CreateEligibleResult()));

            Assert.That(TryGetUniqueCandidate(candidates, GetStaticProperty(ResolveType(PlayerIdTypeName), "Invalid"), out _, out var hasDuplicate), Is.False);
            Assert.That(hasDuplicate, Is.False);
        }

        [Test]
        public void STK_CANDIDATE_Lookup_NullCandidates_ReturnsFalse()
        {
            Assert.That(TryGetUniqueCandidate(null, CreatePlayerId(1), out _, out var hasDuplicate), Is.False);
            Assert.That(hasDuplicate, Is.False);
        }

        [Test]
        public void STK_CANDIDATE_Lookup_MissingPlayerId_ReturnsFalse()
        {
            var candidates = CreateCandidateList(CreateCandidate(1, 4f, CreateEligibleResult()));

            Assert.That(TryGetUniqueCandidate(candidates, CreatePlayerId(2), out _, out var hasDuplicate), Is.False);
            Assert.That(hasDuplicate, Is.False);
        }

        [Test]
        public void STK_CANDIDATE_Lookup_DoesNotMutateOrReorderCallerList()
        {
            var first = CreateCandidate(3, 3f, CreateEligibleResult());
            var second = CreateCandidate(1, 1f, CreateEligibleResult());
            var third = CreateCandidate(2, 2f, CreateEligibleResult());
            var candidates = CreateCandidateList(first, second, third);

            Assert.That(TryGetUniqueCandidate(candidates, CreatePlayerId(1), out _, out var hasDuplicate), Is.True);
            Assert.That(hasDuplicate, Is.False);

            Assert.That(GetListCount(candidates), Is.EqualTo(3));
            AssertPlayerIdValue(GetProperty(GetProperty(GetListItem(candidates, 0), "Observation"), "PlayerId"), 3);
            AssertPlayerIdValue(GetProperty(GetProperty(GetListItem(candidates, 1), "Observation"), "PlayerId"), 1);
            AssertPlayerIdValue(GetProperty(GetProperty(GetListItem(candidates, 2), "Observation"), "PlayerId"), 2);
        }

        [Test]
        public void STK_SIM_INPUT_LegacyConstructor_TargetStatusesIsNull()
        {
            var input = Activator.CreateInstance(
                ResolveType(StalkerSimulationInputTypeName),
                CreateSimulationStep(),
                null);

            Assert.That(GetProperty(input, "TargetStatuses"), Is.Null);
        }

        [Test]
        public void STK_SIM_INPUT_TargetStatusesConstructor_PreservesCallerList()
        {
            var statuses = CreateStatusList(CreateStatus(1, CreateEligibleResult()));
            var input = Activator.CreateInstance(
                ResolveType(StalkerSimulationInputTypeName),
                CreateSimulationStep(),
                null,
                statuses);

            Assert.That(GetProperty(input, "TargetStatuses"), Is.SameAs(statuses));
            Assert.That(GetListCount(statuses), Is.EqualTo(1));
            AssertPlayerIdValue(GetProperty(GetListItem(statuses, 0), "PlayerId"), 1);
        }

        private static bool TryGetUnique(object statuses, object playerId, out object eligibility)
        {
            var args = new[] { statuses, playerId, null };
            var method = ResolveType(TargetStatusLookupTypeName).GetMethod(
                "TryGetUnique",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "Missing StalkerTargetStatusLookup.TryGetUnique.");

            var result = method.Invoke(null, args);
            Assert.That(result, Is.TypeOf<bool>());
            eligibility = args[2];
            return (bool)result;
        }

        private static bool TryGetUniqueCandidate(object candidates, object playerId, out object candidate, out bool hasDuplicate)
        {
            var args = new[] { candidates, playerId, null, null };
            var method = ResolveType(TargetCandidateLookupTypeName).GetMethod(
                "TryGetUnique",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "Missing StalkerTargetCandidateLookup.TryGetUnique.");

            var result = method.Invoke(null, args);
            Assert.That(result, Is.TypeOf<bool>());
            candidate = args[2];
            hasDuplicate = (bool)args[3];
            return (bool)result;
        }

        private static object CreateStatus(int playerId, object eligibility)
        {
            return Activator.CreateInstance(
                ResolveType(TargetStatusTypeName),
                CreatePlayerId(playerId),
                eligibility);
        }

        private static object CreateStatusList(params object[] statuses)
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(ResolveType(TargetStatusTypeName)));
            for (var i = 0; i < statuses.Length; i++)
            {
                list.Add(statuses[i]);
            }

            return list;
        }

        private static object CreateCandidate(int playerId, float distance, object eligibility)
        {
            var observation = Activator.CreateInstance(
                ResolveType(VisionObservationTypeName),
                CreatePlayerId(playerId),
                new UnityEngine.Vector3(distance, 0f, 0f),
                UnityEngine.Vector3.right,
                Activator.CreateInstance(ResolveType(AiSimulationTimeTypeName), 1L, 0d),
                distance);

            return Activator.CreateInstance(
                ResolveType(TargetCandidateTypeName),
                observation,
                eligibility);
        }

        private static object CreateCandidateList(params object[] candidates)
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(ResolveType(TargetCandidateTypeName)));
            for (var i = 0; i < candidates.Length; i++)
            {
                list.Add(candidates[i]);
            }

            return list;
        }

        private static object CreateEligibleResult()
        {
            return ResolveType(EligibilityResultTypeName)
                .GetMethod("EligibleTarget", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, Array.Empty<object>());
        }

        private static object CreateIneligibleResult(string reasonName)
        {
            var reason = Enum.Parse(ResolveType(EligibilityReasonTypeName), reasonName);
            return ResolveType(EligibilityResultTypeName)
                .GetMethod("Ineligible", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new[] { reason });
        }

        private static object CreateDefaultEligibilityResult()
        {
            return Activator.CreateInstance(ResolveType(EligibilityResultTypeName));
        }

        private static object CreatePlayerId(int value)
        {
            return Activator.CreateInstance(ResolveType(PlayerIdTypeName), value);
        }

        private static object CreateSimulationStep()
        {
            return Activator.CreateInstance(
                ResolveType(AiSimulationStepTypeName),
                Activator.CreateInstance(ResolveType(AiSimulationTimeTypeName), 1L, 0d),
                0.1f);
        }

        private static int GetListCount(object list)
        {
            return (int)list.GetType().GetProperty("Count").GetValue(list);
        }

        private static object GetListItem(object list, int index)
        {
            return list.GetType().GetProperty("Item").GetValue(list, new object[] { index });
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

        private static object GetStaticProperty(Type type, string propertyName)
        {
            var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            Assert.That(property, Is.Not.Null, $"Missing static property '{propertyName}' on '{type.FullName}'.");
            return property.GetValue(null);
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
