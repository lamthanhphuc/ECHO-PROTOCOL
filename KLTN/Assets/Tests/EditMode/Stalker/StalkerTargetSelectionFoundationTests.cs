using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerTargetSelectionFoundationTests
    {
        private const string EligibilitySnapshotTypeName = "EchoProtocol.AI.Stalker.StalkerTargetEligibilitySnapshot";
        private const string EligibilityResultTypeName = "EchoProtocol.AI.Stalker.StalkerTargetEligibilityResult";
        private const string EligibilityServiceTypeName = "EchoProtocol.AI.Stalker.StalkerTargetEligibility";

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

        private static void AssertEligibility(object result, bool expectedEligible, string expectedReason)
        {
            Assert.That(GetBoolProperty(result, "Eligible"), Is.EqualTo(expectedEligible));
            Assert.That(GetProperty(result, "Reason").ToString(), Is.EqualTo(expectedReason));
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
