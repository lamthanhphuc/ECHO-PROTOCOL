using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace EchoProtocol.AI.Common.AED
{
    internal sealed class AEDArtifactIntegrityLedger
    {
        private readonly Dictionary<string, string>
            _policyConfigSignatures =
                new Dictionary<string, string>(
                    StringComparer.Ordinal);

        private readonly Dictionary<string, string>
            _evidencePolicySignatures =
                new Dictionary<string, string>(
                    StringComparer.Ordinal);

        private readonly Dictionary<string, string>
            _parameterRegistrySignatures =
                new Dictionary<string, string>(
                    StringComparer.Ordinal);

        public bool TryValidate(
            AEDPolicyConfig policyConfig,
            AEDEvidencePolicy evidencePolicy,
            AdaptiveParameterRegistry parameterRegistry,
            out string reasonCode)
        {
            reasonCode = string.Empty;

            if (policyConfig == null
                || evidencePolicy == null
                || parameterRegistry == null)
            {
                reasonCode =
                    AEDReasonCodes.PolicyConfigInvalid;

                return false;
            }

            if (!TryRemember(
                    _policyConfigSignatures,
                    policyConfig.PolicyConfigVersion,
                    BuildPolicyConfigSignature(
                        policyConfig)))
            {
                reasonCode =
                    AEDReasonCodes.PolicyConfigInvalid;

                return false;
            }

            if (!TryRemember(
                    _evidencePolicySignatures,
                    evidencePolicy.EvidencePolicyVersion,
                    BuildEvidencePolicySignature(
                        evidencePolicy)))
            {
                reasonCode =
                    AEDReasonCodes.PolicyConfigInvalid;

                return false;
            }

            if (!TryRemember(
                    _parameterRegistrySignatures,
                    parameterRegistry.ParameterRegistryVersion,
                    BuildParameterRegistrySignature(
                        parameterRegistry)))
            {
                reasonCode =
                    AEDReasonCodes.ParameterRegistryInvalid;

                return false;
            }

            return true;
        }

        private static bool TryRemember(
            IDictionary<string, string> ledger,
            string version,
            string signature)
        {
            if (ledger.TryGetValue(
                    version,
                    out var existing))
            {
                return string.Equals(
                    existing,
                    signature,
                    StringComparison.Ordinal);
            }

            ledger.Add(
                version,
                signature);

            return true;
        }

        private static string BuildPolicyConfigSignature(
            AEDPolicyConfig config)
        {
            var builder =
                new StringBuilder();

            // policyConfigVersion owns score-band
            // thresholds only. Dependency version refs
            // have their own version owners and are
            // intentionally not part of this signature.
            Append(
                builder,
                "SurvivalLow",
                Number(
                    config.SurvivalThresholds
                        .LowThreshold));

            Append(
                builder,
                "SurvivalHigh",
                Number(
                    config.SurvivalThresholds
                        .HighThreshold));

            Append(
                builder,
                "NoiseLow",
                Number(
                    config.NoiseThresholds
                        .LowThreshold));

            Append(
                builder,
                "NoiseHigh",
                Number(
                    config.NoiseThresholds
                        .HighThreshold));

            return builder.ToString();
        }

        private static string BuildEvidencePolicySignature(
            AEDEvidencePolicy policy)
        {
            var builder =
                new StringBuilder();

            Append(
                builder,
                "MinimumSurvivalSamples",
                policy
                    .MinimumSurvivalSampleCountPerObservedPlayer
                    .ToString(
                        CultureInfo.InvariantCulture));

            Append(
                builder,
                "MinimumNoiseSamples",
                policy
                    .MinimumNoiseSampleCountPerObservedPlayer
                    .ToString(
                        CultureInfo.InvariantCulture));

            Append(
                builder,
                "RequireFullRoster",
                policy.RequireFullRosterObservedCoverage
                    ? "1"
                    : "0");

            return builder.ToString();
        }

        private static string
            BuildParameterRegistrySignature(
                AdaptiveParameterRegistry registry)
        {
            var builder =
                new StringBuilder();

            for (var i = 0;
                 i < registry.Rules.Count;
                 i++)
            {
                var rule =
                    registry.Rules[i];

                Append(
                    builder,
                    "Key",
                    rule.Key.ToString());

                Append(
                    builder,
                    "Default",
                    Number(rule.DefaultValue));

                Append(
                    builder,
                    "Min",
                    Number(rule.MinValue));

                Append(
                    builder,
                    "Max",
                    Number(rule.MaxValue));

                Append(
                    builder,
                    "PressureAxis",
                    rule.PressureAxis.ToString());

                Append(
                    builder,
                    "AdjustmentRuleId",
                    rule.AdjustmentRuleId);

                for (var j = 0;
                     j < rule.CandidateValues.Count;
                     j++)
                {
                    Append(
                        builder,
                        "Candidate",
                        Number(
                            rule.CandidateValues[j]));
                }

                for (var j = 0;
                     j < rule.AllowedTiming.Count;
                     j++)
                {
                    Append(
                        builder,
                        "AllowedTiming",
                        rule.AllowedTiming[j]
                            .ToString());
                }
            }

            return builder.ToString();
        }

        private static string Number(
            double value)
        {
            return value.ToString(
                "R",
                CultureInfo.InvariantCulture);
        }

        private static void Append(
            StringBuilder builder,
            string key,
            string value)
        {
            value ??= string.Empty;

            builder
                .Append(key)
                .Append('=')
                .Append(value.Length)
                .Append(':')
                .Append(value)
                .Append('|');
        }
    }
}