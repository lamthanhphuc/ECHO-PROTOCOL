using System;
using System.Reflection;
using NUnit.Framework;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerTargetPolicyContextFoundationTests
    {
        private const string PlayerIdTypeName =
            "EchoProtocol.AI.Common.PlayerId";

        private const string AiSimulationTimeTypeName =
            "EchoProtocol.AI.Common.AiSimulationTime";

        private const string TargetCandidateTypeName =
            "EchoProtocol.AI.Stalker.StalkerTargetCandidate";

        private const string PolicySignalsTypeName =
            "EchoProtocol.AI.Stalker.StalkerTargetPolicySignals";

        private const string PolicyCandidateTypeName =
            "EchoProtocol.AI.Stalker.StalkerTargetPolicyCandidate";

        private const string PolicyContextTypeName =
            "EchoProtocol.AI.Stalker.StalkerTargetPolicyContext";

        [Test]
        public void STK_POLICY_CONTRACT_NoneSignals_AreNeutral()
        {
            var signalsType = ResolveType(PolicySignalsTypeName);

            var noneProperty = signalsType.GetProperty(
                "None",
                BindingFlags.Static | BindingFlags.Public);

            Assert.That(
                noneProperty,
                Is.Not.Null,
                "Missing StalkerTargetPolicySignals.None.");

            var signals = noneProperty.GetValue(null);

            Assert.That(
                GetProperty(signals, "IsObjectiveCarrier"),
                Is.EqualTo(false));

            Assert.That(
                GetProperty(signals, "VisibleIsolation01"),
                Is.EqualTo(0f));

            Assert.That(
                GetProperty(signals, "RecentDetection01"),
                Is.EqualTo(0f));

            Assert.That(
                GetProperty(signals, "ConfirmedNoisyBehavior01"),
                Is.EqualTo(0f));

            Assert.That(
                GetProperty(signals, "TargetHistory01"),
                Is.EqualTo(0f));
        }

        [Test]
        public void STK_POLICY_CONTRACT_NormalizedSignals_PreserveLegalValues()
        {
            var signals = CreateSignals(
                true,
                0.25f,
                0.50f,
                0.75f,
                1.00f);

            Assert.That(
                GetProperty(signals, "IsObjectiveCarrier"),
                Is.EqualTo(true));

            Assert.That(
                GetProperty(signals, "VisibleIsolation01"),
                Is.EqualTo(0.25f));

            Assert.That(
                GetProperty(signals, "RecentDetection01"),
                Is.EqualTo(0.50f));

            Assert.That(
                GetProperty(signals, "ConfirmedNoisyBehavior01"),
                Is.EqualTo(0.75f));

            Assert.That(
                GetProperty(signals, "TargetHistory01"),
                Is.EqualTo(1.00f));
        }

        [Test]
        public void STK_POLICY_CONTRACT_InvalidNormalizedSignals_AreRejected()
        {
            AssertInvalidSignals(
                false,
                -0.01f,
                0f,
                0f,
                0f);

            AssertInvalidSignals(
                false,
                1.01f,
                0f,
                0f,
                0f);

            AssertInvalidSignals(
                false,
                0f,
                float.NaN,
                0f,
                0f);

            AssertInvalidSignals(
                false,
                0f,
                0f,
                float.PositiveInfinity,
                0f);

            AssertInvalidSignals(
                false,
                0f,
                0f,
                0f,
                float.NegativeInfinity);
        }

        [Test]
        public void STK_POLICY_CONTRACT_Context_RequiresValidSimulationTime()
        {
            var contextType = ResolveType(PolicyContextTypeName);

            var invalidTime = Activator.CreateInstance(
                ResolveType(AiSimulationTimeTypeName));

            var invalidPlayerId = Activator.CreateInstance(
                ResolveType(PlayerIdTypeName));

            var exception = Assert.Throws<TargetInvocationException>(
                () => Activator.CreateInstance(
                    contextType,
                    invalidTime,
                    invalidPlayerId));

            Assert.That(
                exception.InnerException,
                Is.TypeOf<ArgumentException>());
        }

        [Test]
        public void STK_POLICY_CONTRACT_Context_PreservesSimulationTimeAndCurrentTarget()
        {
            var simulationTime = Activator.CreateInstance(
                ResolveType(AiSimulationTimeTypeName),
                42L,
                1.25d);

            var playerId = Activator.CreateInstance(
                ResolveType(PlayerIdTypeName),
                3);

            var context = Activator.CreateInstance(
                ResolveType(PolicyContextTypeName),
                simulationTime,
                playerId);

            var storedTime = GetProperty(
                context,
                "SimulationTime");

            Assert.That(
                GetProperty(storedTime, "IsValid"),
                Is.EqualTo(true));

            Assert.That(
                GetProperty(storedTime, "Tick"),
                Is.EqualTo(42L));

            var storedPlayerId = GetProperty(
                context,
                "CurrentTargetId");

            Assert.That(
                GetProperty(storedPlayerId, "Value"),
                Is.EqualTo(3));
        }

        [Test]
        public void STK_POLICY_CONTRACT_NoDirectHiddenWorldState_IsExposed()
        {
            AssertNoForbiddenDirectProperties(
                ResolveType(PolicySignalsTypeName));

            AssertNoForbiddenDirectProperties(
                ResolveType(PolicyCandidateTypeName));

            AssertNoForbiddenDirectProperties(
                ResolveType(PolicyContextTypeName));
        }

        [Test]
        public void STK_POLICY_CONTRACT_Candidate_WrapsTargetAndSignalsOnly()
        {
            var targetCandidate = Activator.CreateInstance(
                ResolveType(TargetCandidateTypeName));

            var signals = CreateSignals(
                true,
                0.20f,
                0.40f,
                0.60f,
                0.80f);

            var policyCandidate = Activator.CreateInstance(
                ResolveType(PolicyCandidateTypeName),
                targetCandidate,
                signals);

            var storedSignals = GetProperty(
                policyCandidate,
                "Signals");

            Assert.That(
                GetProperty(storedSignals, "IsObjectiveCarrier"),
                Is.EqualTo(true));

            Assert.That(
                GetProperty(storedSignals, "VisibleIsolation01"),
                Is.EqualTo(0.20f));

            Assert.That(
                GetProperty(storedSignals, "RecentDetection01"),
                Is.EqualTo(0.40f));

            Assert.That(
                GetProperty(storedSignals, "ConfirmedNoisyBehavior01"),
                Is.EqualTo(0.60f));

            Assert.That(
                GetProperty(storedSignals, "TargetHistory01"),
                Is.EqualTo(0.80f));
        }

        private static object CreateSignals(
            bool isObjectiveCarrier,
            float visibleIsolation01,
            float recentDetection01,
            float confirmedNoisyBehavior01,
            float targetHistory01)
        {
            return Activator.CreateInstance(
                ResolveType(PolicySignalsTypeName),
                isObjectiveCarrier,
                visibleIsolation01,
                recentDetection01,
                confirmedNoisyBehavior01,
                targetHistory01);
        }

        private static void AssertInvalidSignals(
            bool isObjectiveCarrier,
            float visibleIsolation01,
            float recentDetection01,
            float confirmedNoisyBehavior01,
            float targetHistory01)
        {
            var exception = Assert.Throws<TargetInvocationException>(
                () => CreateSignals(
                    isObjectiveCarrier,
                    visibleIsolation01,
                    recentDetection01,
                    confirmedNoisyBehavior01,
                    targetHistory01));

            Assert.That(
                exception.InnerException,
                Is.TypeOf<ArgumentOutOfRangeException>());
        }

        private static void AssertNoForbiddenDirectProperties(
            Type type)
        {
            var forbiddenFragments = new[]
            {
                "Transform",
                "GameObject",
                "Occupancy",
                "Locker",
                "HiddenPosition",
                "HidingSpot"
            };

            var properties = type.GetProperties(
                BindingFlags.Instance | BindingFlags.Public);

            for (var i = 0; i < properties.Length; i++)
            {
                var property = properties[i];

                for (var j = 0; j < forbiddenFragments.Length; j++)
                {
                    Assert.That(
                        property.Name,
                        Does.Not.Contain(forbiddenFragments[j]),
                        $"{type.FullName}.{property.Name} exposes forbidden target-policy state.");

                    Assert.That(
                        property.PropertyType.FullName ?? string.Empty,
                        Does.Not.Contain(forbiddenFragments[j]),
                        $"{type.FullName}.{property.Name} exposes forbidden target-policy type.");
                }
            }
        }

        private static object GetProperty(
            object target,
            string propertyName)
        {
            Assert.That(
                target,
                Is.Not.Null);

            var property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);

            Assert.That(
                property,
                Is.Not.Null,
                $"Missing public property '{propertyName}' on '{target.GetType().FullName}'.");

            return property.GetValue(target);
        }

        private static Type ResolveType(
            string fullTypeName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (var i = 0; i < assemblies.Length; i++)
            {
                var type = assemblies[i].GetType(
                    fullTypeName,
                    false);

                if (type != null)
                {
                    return type;
                }
            }

            Assert.Fail(
                $"Could not find production type '{fullTypeName}' in the loaded Unity AppDomain.");

            return null;
        }
    }
}
