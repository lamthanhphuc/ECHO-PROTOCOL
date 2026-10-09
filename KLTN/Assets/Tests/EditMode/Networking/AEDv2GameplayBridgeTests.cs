using System;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.Networking.Tests
{
    public sealed class AEDv2GameplayBridgeTests
    {
        private static Type Bridge => Type.GetType("EchoProtocol.AI.AED.AEDv2GameplayBridge, Assembly-CSharp", true);

        [Test]
        public void NormalDifficultyMatchesAEDv2Baseline()
        {
            Assert.That((bool)Bridge.GetMethod("IsNormalCompatible").Invoke(null, null), Is.True);
        }

        [Test]
        public void FixedBaselineContentIsBoundToGameplayBuild()
        {
            var contract = Type.GetType(
                "EchoProtocol.AI.AED.AEDGameplayContentContract, Assembly-CSharp", true);
            var arguments = new object[] { FixedDirector.CreateFixedBaseline(), null };
            Assert.That(contract.GetMethod("TryValidate").Invoke(null, arguments), Is.True,
                arguments[1]?.ToString());
        }

        [Test]
        public void EveryDifficultyParameterMatchesNormal()
        {
            NormalDifficultyMatchesAEDv2Baseline();
            Assert.That(AEDv2Plan.Normal().Get(AEDv2Key.ChaseSpeed), Is.EqualTo(7.5));
        }

        [Test]
        public void NormalPlanDoesNotChangeGameplay()
        {
            var plan = AEDv2Plan.Normal();
            var profile = Bridge.GetMethod("ToNormalDifficultyProfile").Invoke(null, new object[] { plan });
            var difficulty = Type.GetType("EchoProtocol.Gameplay.MatchDifficulty, Assembly-CSharp", true);
            var profiles = Type.GetType("EchoProtocol.Gameplay.MatchDifficultyProfiles, Assembly-CSharp", true);
            var normal = profiles.GetMethod("Get").Invoke(null, new[] { Enum.Parse(difficulty, "Normal") });
            foreach (var property in profile.GetType().GetProperties())
                Assert.That(property.GetValue(profile), Is.EqualTo(property.GetValue(normal)), property.Name);
        }

        [Test]
        public void ReliefAndPressureCandidatesRemainBounded()
        {
            foreach (var spec in AEDv2Catalog.All)
            {
                Assert.That(spec.IsRegistered(spec.Baseline), Is.True, spec.Key.ToString());
                if (spec.HasTarget(AdaptationIntent.Relieve))
                    Assert.That(spec.IsRegistered(spec.Relief), Is.True, spec.Key.ToString());
                if (spec.HasTarget(AdaptationIntent.IncreasePressure))
                    Assert.That(spec.IsRegistered(spec.Pressure), Is.True, spec.Key.ToString());
            }
        }

        [Test]
        public void MissingSettingsFallsBackSafely()
        {
            var request = new ScenarioResolutionRequest(Guid.NewGuid(), Guid.NewGuid(),
                ScenarioResolutionMode.Adaptive, ScenarioDecisionPoint.PreMatch, "PRE_MATCH", "");
            var result = new ScenarioResolutionEngine().Resolve(new ScenarioResolutionEngineInput(
                request, null, null, null, null, null, null, "AED_RUNTIME_SETTINGS_MISSING"));
            Assert.That(result.Decision.Result, Is.EqualTo(AdaptiveDecisionResult.FixedFallback));
            Assert.That(result.Decision.ReasonCodes, Does.Contain("AED_RUNTIME_SETTINGS_MISSING"));
        }

        [Test]
        public void FixedModeNeverAppliesAEDv2()
        {
            var request = new ScenarioResolutionRequest(Guid.NewGuid(), Guid.NewGuid(),
                ScenarioResolutionMode.Fixed, ScenarioDecisionPoint.PreMatch, "PRE_MATCH", "");
            var decision = AEDv2Policy.Evaluate(request, null, null, null);
            Assert.That(decision.Changed, Is.False);
            Assert.That(new ScenarioResolutionEngine().Resolve(new ScenarioResolutionEngineInput(
                request, null, null, null, null, null, null)).Decision.Result,
                Is.EqualTo(AdaptiveDecisionResult.Applied));
        }

        [Test]
        public void RuntimeSettingsAssetIsValid()
        {
            var asset = Resources.Load("AED/AEDRuntimeSettings");
            Assert.That(asset, Is.Not.Null);
            var type = asset.GetType();
            foreach (var method in new[] { "TryBuildPolicyConfig", "TryBuildEvidencePolicy", "TryBuildParameterRegistry" })
            {
                var args = new object[] { null, null };
                Assert.That((bool)type.GetMethod(method).Invoke(asset, args), Is.True, method + ": " + args[1]);
            }
        }

        [Test]
        public void RuntimeSettingsDefaultsAreDormant()
        {
            var asset = Resources.Load("AED/AEDRuntimeSettings");
            Assert.That(asset, Is.Not.Null);

            var type = asset.GetType();
            var defaults = ScriptableObject.CreateInstance(type);

            try
            {
                Assert.That(
                    type.GetProperty("ExtendedPolicyShadowEnabled")
                        .GetValue(defaults),
                    Is.False);

                Assert.That(
                    type.GetProperty("ExtendedPolicyGameplayEnabled")
                        .GetValue(defaults),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(defaults);
            }
        }
    }
}
