using System;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.AI.Common.Profile;
using NUnit.Framework;

namespace EchoProtocol.AI.Common.Tests
{
    public sealed class AEDv2TuningContractTests
    {
        [Test]
        public void Catalog_AllKeysHaveRegisteredNormalBaseline()
        {
            var plan = AEDv2Plan.Normal();
            Assert.That(AEDv2Catalog.All.Count, Is.EqualTo(Enum.GetValues(typeof(AEDv2Key)).Length));
            Assert.That(plan.TrySingleBoundedChange(out _), Is.True);
            foreach (var spec in AEDv2Catalog.All)
                Assert.That(plan.Get(spec.Key), Is.EqualTo(spec.Baseline), spec.Key.ToString());
        }

        [Test]
        public void EveryEligibleKey_IsIndividuallyEvaluable_WithoutChangingOtherKeys()
        {
            var request = AEDTestFactory.Request();
            var snapshot = AEDTestFactory.Snapshot(request, 15d, 15d);
            var gate = AEDInputGate.Evaluate(snapshot, request,
                AEDTestFactory.Policy(), AEDTestFactory.Evidence(), AEDTestFactory.CurrentCurrency());
            Assert.That(gate.Status, Is.EqualTo(AEDInputGateStatus.Eligible));
            foreach (var spec in AEDv2Catalog.All)
            {
                foreach (var intent in new[] { AdaptationIntent.Relieve, AdaptationIntent.IncreasePressure })
                {
                    var decision = AEDv2Policy.EvaluateKey(request, gate, intent, spec.Key);
                    if (!spec.HasTarget(intent))
                    {
                        Assert.That(decision.Changed, Is.False, spec.Key + ":" + intent);
                        continue;
                    }
                    Assert.That(decision.Changed, Is.True, spec.Key + ":" + intent);
                    Assert.That(decision.Plan.TrySingleBoundedChange(out var changed), Is.True);
                    Assert.That(changed, Is.EqualTo(spec.Key));
                    Assert.That(decision.Plan.Get(spec.Key), Is.EqualTo(spec.Target(intent)));
                    foreach (var other in AEDv2Catalog.All)
                        if (other.Key != spec.Key)
                            Assert.That(decision.Plan.Get(other.Key), Is.EqualTo(other.Baseline));
                }
            }
        }

        [Test]
        public void CombatAndFlashlightContracts_AreNotPolicyKeys()
        {
            foreach (var name in Enum.GetNames(typeof(AEDv2Key)))
            {
                Assert.That(name, Is.Not.EqualTo("AttackRange"));
                Assert.That(name, Is.Not.EqualTo("AttackWindup"));
                Assert.That(name, Is.Not.EqualTo("AttackRecovery"));
                Assert.That(name, Is.Not.EqualTo("AttackDamage"));
                Assert.That(name, Is.Not.EqualTo("FlashlightReactionEnabled"));
                Assert.That(name, Is.Not.EqualTo("RequireEscapeRoute"));
                Assert.That(name, Is.Not.EqualTo("JumpInMinDistance"));
                Assert.That(name, Is.Not.EqualTo("JumpInMaxDistance"));
            }
        }

        [Test]
        public void InvalidValue_CannotEnterPlan()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AEDv2Plan.Normal().With(AEDv2Key.ChaseSpeed, 100d));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AEDv2Plan.Normal().With(AEDv2Key.ChaseSpeed, double.NaN));
        }

        [Test]
        public void ResourceBudgetAndReviveBonus_CannotBeReducedOrPressured()
        {
            foreach (var key in new[] { AEDv2Key.SupportBonus, AEDv2Key.ReviveBonusPerZone })
            {
                var spec = AEDv2Catalog.Find(key);
                Assert.That(spec.ReliefOnly, Is.True);
                Assert.That(spec.HasTarget(AdaptationIntent.IncreasePressure), Is.False);
                Assert.That(spec.Relief, Is.GreaterThanOrEqualTo(spec.Baseline));
            }
        }

        [Test]
        public void IdenticalDecisionAndSnapshot_ProducesIdenticalKeyAndFingerprint()
        {
            var request = AEDTestFactory.Request();
            var snapshot = AEDTestFactory.Snapshot(request, 90d, 90d);
            var config = AEDTestFactory.Policy();
            var gate = AEDInputGate.Evaluate(snapshot, request, config,
                AEDTestFactory.Evidence(), AEDTestFactory.CurrentCurrency());
            var a = AEDv2Policy.Evaluate(request, snapshot, gate, config);
            var b = AEDv2Policy.Evaluate(request, snapshot, gate, config);
            Assert.That(a.Changed, Is.True);
            Assert.That(a.Key, Is.EqualTo(b.Key));
            Assert.That(a.Plan.Fingerprint(), Is.EqualTo(b.Plan.Fingerprint()));
        }

        [Test]
        public void PartialOrStaleProfile_DoesNotAdapt()
        {
            var request = AEDTestFactory.Request();
            var snapshot = AEDTestFactory.Snapshot(request, 90d, 90d, SnapshotValidity.Partial);
            var config = AEDTestFactory.Policy();
            var gate = AEDInputGate.Evaluate(snapshot, request, config,
                AEDTestFactory.Evidence(), AEDTestFactory.CurrentCurrency());
            Assert.That(gate.Status, Is.Not.EqualTo(AEDInputGateStatus.Eligible));
            var decision = AEDv2Policy.Evaluate(request, snapshot, gate, config);
            Assert.That(decision.Changed, Is.False);
        }

        [Test]
        public void BoundaryDecisions_HoldUntilCurrentMatchEvidencePolicyExists()
        {
            var request = AEDTestFactory.Request(ScenarioDecisionPoint.AllowedPhaseBoundary);
            var snapshot = AEDTestFactory.Snapshot(request, 90d, 90d);
            var config = AEDTestFactory.Policy();
            var gate = AEDInputGate.Evaluate(snapshot, request, config,
                AEDTestFactory.Evidence(), AEDTestFactory.CurrentCurrency());
            var decision = AEDv2Policy.Evaluate(request, snapshot, gate, config);
            Assert.That(decision.Changed, Is.False);
            Assert.That(decision.Reason, Does.Contain("BOUNDARY_HOLD"));
        }

        [Test]
        public void ObjectiveNoiseAndCoreCarrierToggles_AreIndependent()
        {
            var a = AEDv2Plan.Normal()
                .With(AEDv2Key.ObjectiveNoiseInvestigationEnabled, 0);
            var b = AEDv2Plan.Normal()
                .With(AEDv2Key.CoreCarrierPressureEnabled, 0);
            Assert.That(a.Get(AEDv2Key.ObjectiveNoiseInvestigationEnabled), Is.EqualTo(0));
            Assert.That(a.Get(AEDv2Key.CoreCarrierPressureEnabled), Is.EqualTo(1));
            Assert.That(b.Get(AEDv2Key.ObjectiveNoiseInvestigationEnabled), Is.EqualTo(1));
            Assert.That(b.Get(AEDv2Key.CoreCarrierPressureEnabled), Is.EqualTo(0));
        }

        [Test]
        public void SpecialCooldown_NeverDropsBelowExistingHardFloor()
        {
            var spec = AEDv2Catalog.Find(AEDv2Key.SpecialCooldownSeconds);
            Assert.That(spec.Pressure, Is.GreaterThanOrEqualTo(300));
        }

        [Test]
        public void FixedScenario_CannotProduceAdaptiveTuning()
        {
            var request = AEDTestFactory.Request(mode: ScenarioResolutionMode.Fixed);
            var snapshot = AEDTestFactory.Snapshot(request, 90d, 90d);
            var policy = AEDTestFactory.Policy();
            var gate = AEDInputGate.Evaluate(snapshot, request,
                policy, AEDTestFactory.Evidence(), AEDTestFactory.CurrentCurrency());
            var decision = AEDv2Policy.Evaluate(request, snapshot, gate, policy);
            Assert.That(decision.Changed, Is.False);
        }

    }
}
