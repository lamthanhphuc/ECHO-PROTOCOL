using EchoProtocol.AI.Common.AED;
using EchoProtocol.AI.Common.Profile;
using NUnit.Framework;

namespace EchoProtocol.AI.Common.Tests
{
    public sealed class AEDInputGateContractTests
    {
        [Test]
        public void ValidSnapshot_WithSufficientEvidence_IsEligible()
        {
            var request = AEDTestFactory.Request();
            var snapshot =
                AEDTestFactory.Snapshot(
                    request,
                    50d,
                    50d);

            var result =
                AEDInputGate.Evaluate(
                    snapshot,
                    request,
                    AEDTestFactory.Policy(),
                    AEDTestFactory.Evidence(),
                    AEDTestFactory.CurrentCurrency());

            Assert.That(
                result.Status,
                Is.EqualTo(AEDInputGateStatus.Eligible));

            Assert.That(result.Reasons, Is.Empty);
        }

        [Test]
        public void PartialSnapshot_IsInputIncomplete()
        {
            var request = AEDTestFactory.Request();

            var result =
                AEDInputGate.Evaluate(
                    AEDTestFactory.Snapshot(
                        request,
                        50d,
                        50d,
                        SnapshotValidity.Partial),
                    request,
                    AEDTestFactory.Policy(),
                    AEDTestFactory.Evidence(),
                    AEDTestFactory.CurrentCurrency());

            Assert.That(
                result.Status,
                Is.EqualTo(AEDInputGateStatus.Ineligible));

            Assert.That(
                result.Reasons,
                Does.Contain(
                    AEDReasonCodes.InputIncomplete));
        }

        [Test]
        public void InvalidSnapshot_IsInputInvalid()
        {
            var request = AEDTestFactory.Request();

            var result =
                AEDInputGate.Evaluate(
                    AEDTestFactory.Snapshot(
                        request,
                        50d,
                        50d,
                        SnapshotValidity.Invalid),
                    request,
                    AEDTestFactory.Policy(),
                    AEDTestFactory.Evidence(),
                    AEDTestFactory.CurrentCurrency());

            Assert.That(
                result.Status,
                Is.EqualTo(AEDInputGateStatus.Invalid));

            Assert.That(
                result.Reasons,
                Does.Contain(
                    AEDReasonCodes.InputInvalid));
        }

        [Test]
        public void StaleProfileRevision_IsStaleInput()
        {
            var request = AEDTestFactory.Request();
            var snapshot =
                AEDTestFactory.Snapshot(
                    request,
                    50d,
                    50d);

            var currency =
                new AdaptiveInputCurrencyValidation(
                    true,
                    true,
                    true,
                    true,
                    false,
                    true,
                    true);

            var result =
                AEDInputGate.Evaluate(
                    snapshot,
                    request,
                    AEDTestFactory.Policy(),
                    AEDTestFactory.Evidence(),
                    currency);

            Assert.That(
                result.Status,
                Is.EqualTo(AEDInputGateStatus.Invalid));

            Assert.That(
                result.Reasons,
                Does.Contain(
                    AEDReasonCodes.StaleInput));
        }

        [Test]
        public void UnsupportedProfileSemantics_IsUnsupportedVersion()
        {
            var request = AEDTestFactory.Request();

            var currency =
                new AdaptiveInputCurrencyValidation(
                    true,
                    true,
                    true,
                    true,
                    true,
                    true,
                    false);

            var result =
                AEDInputGate.Evaluate(
                    AEDTestFactory.Snapshot(
                        request,
                        50d,
                        50d),
                    request,
                    AEDTestFactory.Policy(),
                    AEDTestFactory.Evidence(),
                    currency);

            Assert.That(
                result.Status,
                Is.EqualTo(AEDInputGateStatus.Invalid));

            Assert.That(
                result.Reasons,
                Does.Contain(
                    AEDReasonCodes.UnsupportedVersion));
        }

        [Test]
        public void InsufficientSamples_IsInputIncomplete()
        {
            var request = AEDTestFactory.Request();

            var result =
                AEDInputGate.Evaluate(
                    AEDTestFactory.Snapshot(
                        request,
                        50d,
                        50d,
                        SnapshotValidity.Valid,
                        sampleCount: 1),
                    request,
                    AEDTestFactory.Policy(),
                    AEDTestFactory.Evidence(
                        minimumSamples: 2),
                    AEDTestFactory.CurrentCurrency());

            Assert.That(
                result.Status,
                Is.EqualTo(AEDInputGateStatus.Ineligible));

            Assert.That(
                result.Reasons,
                Does.Contain(
                    AEDReasonCodes.InputIncomplete));
        }

        [Test]
        public void ComparisonSemanticMismatch_IsUnsupportedVersion()
        {
            var request = AEDTestFactory.Request();

            var result =
                AEDInputGate.Evaluate(
                    AEDTestFactory.Snapshot(
                        request,
                        50d,
                        50d,
                        survivalKey:
                            "UNSUPPORTED_SURVIVAL_KEY"),
                    request,
                    AEDTestFactory.Policy(),
                    AEDTestFactory.Evidence(),
                    AEDTestFactory.CurrentCurrency());

            Assert.That(
                result.Status,
                Is.EqualTo(AEDInputGateStatus.Invalid));

            Assert.That(
                result.Reasons,
                Does.Contain(
                    AEDReasonCodes.UnsupportedVersion));
        }
    }
}
