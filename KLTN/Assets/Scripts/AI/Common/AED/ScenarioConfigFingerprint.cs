using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace EchoProtocol.AI.Common.AED
{
    public static class ScenarioConfigFingerprint
    {
        public static string Compute(ScenarioConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            var text =
                config.ScenarioConfigVersion + "|" +
                config.PolicyVersion + "|" +
                config.ConfigSource + "|" +
                config.MapId + "|" +
                config.MonsterType + "|" +
                config.ObjectiveSpawnSetId + "|" +
                config.SupportItemBudget + "|" +
                config.MonsterParameters.DetectionFillRate.ToString("R", CultureInfo.InvariantCulture) + "|" +
                config.MonsterParameters.DetectionDecayRate.ToString("R", CultureInfo.InvariantCulture) + "|" +
                config.MonsterParameters.ChaseSpeed.ToString("R", CultureInfo.InvariantCulture) + "|" +
                config.MonsterParameters.SearchDuration.ToString("R", CultureInfo.InvariantCulture) + "|" +
                config.RouteModifier + "|" +
                config.FinalHuntParameters.EscapeDoorTimerSeconds.ToString("R", CultureInfo.InvariantCulture) + "|" +
                config.FallbackConfigId;
            return Sha256(text);
        }

        public static Guid DeterministicGuid(string text)
        {
            var hash = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
            var bytes = new byte[16];
            Array.Copy(hash, bytes, bytes.Length);
            bytes[7] = (byte)((bytes[7] & 0x0f) | 0x50);
            bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
            return new Guid(bytes);
        }

        public static string CreateVersion(
            Guid matchId,
            Guid resolutionId,
            ScenarioConfig baseConfig,
            ScenarioConfigKey key,
            double value)
        {
            var hash = Sha256(matchId.ToString("N")
                              + "|"
                              + resolutionId.ToString("N")
                              + "|"
                              + Compute(baseConfig)
                              + "|"
                              + key
                              + "|"
                              + value.ToString("R", CultureInfo.InvariantCulture));
            return "AED_ADAPTIVE_" + hash.Substring(0, 16).ToUpperInvariant();
        }

        public static string ComputeDecisionSemanticFingerprint(
            ScenarioResolutionEngineInput input,
            AdaptiveDecision decision)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (decision == null) throw new ArgumentNullException(nameof(decision));

            var builder = new StringBuilder();

            Append(builder, "TargetMatchId", input.Request.TargetMatchId.ToString("N"));
            Append(builder, "ResolutionMode", input.Request.ResolutionMode.ToString());
            Append(builder, "DecisionPoint", input.Request.DecisionPoint.ToString());
            Append(builder, "PhaseContext", input.Request.PhaseContext);
            Append(builder, "ExperimentCondition", input.Request.ExperimentCondition);

            Append(
                builder,
                "SnapshotFingerprint",
                input.AdaptiveInputSnapshot != null
                    ? input.AdaptiveInputSnapshot.SnapshotContentFingerprint
                    : "NONE");

            var baseConfig =
                input.CurrentAppliedConfig
                ?? FixedDirector.CreateFixedBaseline();

            var baseRef =
                input.Request.DecisionPoint
                    == ScenarioDecisionPoint.PreMatch
                    ? FixedDirector.CreatePreMatchBaseRef(
                        baseConfig)
                    : FixedDirector.CreateAppliedBaseRef(
                        baseConfig);

            Append(
                builder,
                "BaseConfigId",
                baseRef.BaseConfigId);

            Append(
                builder,
                "BaseScenarioConfigVersion",
                baseRef.BaseScenarioConfigVersion);

            Append(
                builder,
                "BaseContentFingerprint",
                baseRef.BaseContentFingerprint);

            Append(
                builder,
                "BaseKind",
                baseRef.BaseKind.ToString());

            Append(
                builder,
                "BaseRouteModifier",
                baseConfig.RouteModifier);

            Append(
                builder,
                "BaseObjectiveSpawnSetId",
                baseConfig.ObjectiveSpawnSetId);

            if (input.PolicyConfig != null)
            {
                Append(builder, "PolicyVersion", input.PolicyConfig.PolicyVersion);
                Append(builder, "PolicyConfigVersion", input.PolicyConfig.PolicyConfigVersion);
                Append(builder, "EvidencePolicyVersion", input.PolicyConfig.EvidencePolicyVersion);
                Append(builder, "ParameterRegistryVersion", input.PolicyConfig.ParameterRegistryVersion);
                Append(builder, "ContentWhitelistVersion", input.PolicyConfig.ContentWhitelistVersion);

                Append(
                    builder,
                    "SurvivalLowThreshold",
                    input.PolicyConfig.SurvivalThresholds.LowThreshold
                        .ToString("R", CultureInfo.InvariantCulture));

                Append(
                    builder,
                    "SurvivalHighThreshold",
                    input.PolicyConfig.SurvivalThresholds.HighThreshold
                        .ToString("R", CultureInfo.InvariantCulture));

                Append(
                    builder,
                    "NoiseLowThreshold",
                    input.PolicyConfig.NoiseThresholds.LowThreshold
                        .ToString("R", CultureInfo.InvariantCulture));

                Append(
                    builder,
                    "NoiseHighThreshold",
                    input.PolicyConfig.NoiseThresholds.HighThreshold
                        .ToString("R", CultureInfo.InvariantCulture));
            }
            else
            {
                Append(builder, "PolicyConfig", "NONE");
            }

            if (input.EvidencePolicy != null)
            {
                Append(
                    builder,
                    "EvidenceMinimumSurvival",
                    input.EvidencePolicy.MinimumSurvivalSampleCountPerObservedPlayer
                        .ToString(CultureInfo.InvariantCulture));

                Append(
                    builder,
                    "EvidenceMinimumNoise",
                    input.EvidencePolicy.MinimumNoiseSampleCountPerObservedPlayer
                        .ToString(CultureInfo.InvariantCulture));

                Append(
                    builder,
                    "RequireFullRoster",
                    input.EvidencePolicy.RequireFullRosterObservedCoverage.ToString());
            }
            else
            {
                Append(builder, "EvidencePolicy", "NONE");
            }

            if (input.ParameterRegistry != null)
            {
                Append(
                    builder,
                    "RegistryVersion",
                    input.ParameterRegistry.ParameterRegistryVersion);

                var rules = input.ParameterRegistry.Rules;

                for (var i = 0; i < rules.Count; i++)
                {
                    var rule = rules[i];

                    Append(builder, "RuleKey", rule.Key.ToString());

                    Append(
                        builder,
                        "RuleDefault",
                        rule.DefaultValue.ToString("R", CultureInfo.InvariantCulture));

                    Append(
                        builder,
                        "RuleMin",
                        rule.MinValue.ToString("R", CultureInfo.InvariantCulture));

                    Append(
                        builder,
                        "RuleMax",
                        rule.MaxValue.ToString("R", CultureInfo.InvariantCulture));

                    for (var j = 0; j < rule.CandidateValues.Count; j++)
                    {
                        Append(
                            builder,
                            "Candidate",
                            rule.CandidateValues[j]
                                .ToString("R", CultureInfo.InvariantCulture));
                    }

                    for (var j = 0; j < rule.AllowedTiming.Count; j++)
                    {
                        Append(
                            builder,
                            "AllowedTiming",
                            rule.AllowedTiming[j].ToString());
                    }

                    Append(
                        builder,
                        "PressureAxis",
                        rule.PressureAxis.ToString());

                    Append(
                        builder,
                        "AdjustmentRuleId",
                        rule.AdjustmentRuleId);
                }
            }
            else
            {
                Append(builder, "ParameterRegistry", "NONE");
            }

            Append(builder, "DecisionResult", decision.Result.ToString());
            Append(builder, "FallbackAction", decision.FallbackAction.ToString());
            Append(builder, "RuleId", decision.RuleId);
            Append(builder, "Intent", decision.Intent.ToString());
            Append(builder, "NoChangeReason", decision.NoChangeReason.ToString());

            Append(
                builder,
                "CandidateValidationStatus",
                decision.CandidateValidationStatus.ToString());

            Append(
                builder,
                "InputStatus",
                decision.InputStatus.HasValue
                    ? decision.InputStatus.Value.ToString()
                    : "NOT_EVALUATED");

            for (var i = 0;
                 i < decision.InputReasons.Count;
                 i++)
            {
                Append(
                    builder,
                    "InputReason",
                    decision.InputReasons[i]);
            }

            for (var i = 0;
                 i < decision.RequestedChanges.Count;
                 i++)
            {
                var change =
                    decision.RequestedChanges[i];

                Append(
                    builder,
                    "RequestedKey",
                    change.Key.ToString());

                Append(
                    builder,
                    "RequestedBefore",
                    change.Before.ToString(
                        "R",
                        CultureInfo.InvariantCulture));

                Append(
                    builder,
                    "RequestedAfter",
                    change.RequestedAfter.ToString(
                        "R",
                        CultureInfo.InvariantCulture));

                Append(
                    builder,
                    "RequestedRuleId",
                    change.RuleId);

                Append(
                    builder,
                    "RequestedAdjustmentRuleId",
                    change.AdjustmentRuleId);
            }

            for (var i = 0;
                 i < decision.ReasonCodes.Count;
                 i++)
            {
                Append(
                    builder,
                    "DecisionDetailReason",
                    decision.ReasonCodes[i]);
            }

            Append(
                builder,
                "FallbackConfigVersion",
                FixedDirector.FixedBaselineScenarioVersion);

            Append(
                builder,
                "AppliedConfig",
                decision.AppliedConfig != null
                    ? Compute(decision.AppliedConfig)
                    : "NONE");

            return Sha256(builder.ToString());
        }

        private static void Append(
            StringBuilder builder,
            string name,
            string value)
        {
            value = value ?? string.Empty;

            builder.Append(name.Length);
            builder.Append(':');
            builder.Append(name);
            builder.Append('=');
            builder.Append(value.Length);
            builder.Append(':');
            builder.Append(value);
            builder.Append(';');
        }

        private static string Sha256(string text)
        {
            var bytes = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(text));
            var builder = new StringBuilder(bytes.Length * 2);
            for (var i = 0; i < bytes.Length; i++) builder.Append(bytes[i].ToString("x2"));
            return builder.ToString();
        }
    }
}
