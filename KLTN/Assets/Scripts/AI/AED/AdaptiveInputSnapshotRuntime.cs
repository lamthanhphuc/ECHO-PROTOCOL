using System;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.AI.Common.Profile;

namespace EchoProtocol.AI.AED
{
    public interface IAdaptiveInputSnapshotProvider
    {
        bool TryGetSnapshot(
            ScenarioResolutionRequest request,
            out AdaptiveInputSnapshot snapshot,
            out AdaptiveInputCurrencyValidation currencyValidation,
            out string unavailableReason);
    }

    public static class AdaptiveInputSnapshotRuntime
    {
        private static IAdaptiveInputSnapshotProvider _provider;

        public static bool HasProvider => _provider != null;

        public static void BindProvider(IAdaptiveInputSnapshotProvider provider)
        {
            _provider = provider;
        }

        public static void ClearProvider(IAdaptiveInputSnapshotProvider provider)
        {
            if (ReferenceEquals(_provider, provider)) _provider = null;
        }

        public static bool TryGetSnapshot(
            ScenarioResolutionRequest request,
            out AdaptiveInputSnapshot snapshot,
            out AdaptiveInputCurrencyValidation currencyValidation,
            out string unavailableReason)
        {
            if (_provider == null)
            {
                snapshot = null;
                currencyValidation = null;
                unavailableReason = "ADAPTIVE_INPUT_PROVIDER_UNAVAILABLE";
                return false;
            }

            try
            {
                return _provider.TryGetSnapshot(
                    request,
                    out snapshot,
                    out currencyValidation,
                    out unavailableReason);
            }
            catch (Exception exception)
            {
                snapshot = null;
                currencyValidation = null;
                unavailableReason = "ADAPTIVE_INPUT_PROVIDER_EXCEPTION:" + exception.GetType().Name;
                return false;
            }
        }

        public static bool IsStillCurrent(
            ScenarioResolutionRequest request,
            Guid expectedSnapshotId,
            string expectedSnapshotFingerprint,
            out string reasonCode)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            reasonCode = string.Empty;

            if (expectedSnapshotId == Guid.Empty
                || string.IsNullOrWhiteSpace(expectedSnapshotFingerprint))
            {
                reasonCode = AEDReasonCodes.StaleInput;
                return false;
            }

            if (!TryGetSnapshot(
                    request,
                    out var currentSnapshot,
                    out var currentCurrency,
                    out _))
            {
                reasonCode = AEDReasonCodes.StaleInput;
                return false;
            }

            if (currentSnapshot == null
                || currentCurrency == null
                || !currentCurrency.TargetMatchCurrent
                || !currentCurrency.DecisionPointCurrent
                || !currentCurrency.PhaseContextCurrent
                || !currentCurrency.RosterIdentityCurrent
                || !currentCurrency.SourceProfileRevisionsCurrent
                || !currentCurrency.SnapshotFingerprintValid
                || !currentCurrency.ProfileSemanticsSupported)
            {
                reasonCode = AEDReasonCodes.StaleInput;
                return false;
            }

            if (currentSnapshot.SnapshotId != expectedSnapshotId
                || !string.Equals(
                    currentSnapshot.SnapshotContentFingerprint,
                    expectedSnapshotFingerprint,
                    StringComparison.Ordinal))
            {
                reasonCode = AEDReasonCodes.StaleInput;
                return false;
            }

            return true;
        }
    }
}
