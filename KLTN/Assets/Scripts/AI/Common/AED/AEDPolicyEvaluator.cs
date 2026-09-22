using System;
using EchoProtocol.AI.Common.Profile;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class AEDPolicyEvaluationResult
    {
        public AEDPolicyEvaluationResult(
            bool evaluated,
            string ruleId,
            AdaptationIntent intent,
            ScenarioConfigKey? requestedKey,
            PolicyNoChangeReason noChangeReason,
            string reasonCode,
            ScoreBand? survivalBand = null,
            ScoreBand? noiseBand = null)
        {
            Evaluated = evaluated;
            RuleId = ruleId ?? string.Empty;
            Intent = intent;
            RequestedKey = requestedKey;
            NoChangeReason = noChangeReason;
            ReasonCode = reasonCode ?? string.Empty;
            SurvivalBand = survivalBand;
            NoiseBand = noiseBand;
        }

        public bool Evaluated { get; }
        public string RuleId { get; }
        public AdaptationIntent Intent { get; }
        public ScenarioConfigKey? RequestedKey { get; }
        public PolicyNoChangeReason NoChangeReason { get; }
        public string ReasonCode { get; }
        public ScoreBand? SurvivalBand { get; }
        public ScoreBand? NoiseBand { get; }
    }

    public static class AEDPolicyEvaluator
    {
        public static AEDPolicyEvaluationResult Evaluate(
            ScenarioResolutionRequest request,
            AdaptiveInputSnapshot snapshot,
            AEDPolicyConfig policyConfig)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            if (policyConfig == null || !policyConfig.IsValid)
            {
                return new AEDPolicyEvaluationResult(false, string.Empty, AdaptationIntent.Hold, null, PolicyNoChangeReason.None, AEDReasonCodes.PolicyConfigInvalid);
            }

            if (request.DecisionPoint == ScenarioDecisionPoint.AllowedPhaseBoundary)
            {
                return new AEDPolicyEvaluationResult(true, "AED-BND-100-HOLD", AdaptationIntent.Hold, null, PolicyNoChangeReason.HoldRule, "HOLD_RULE");
            }

            if (request.DecisionPoint == ScenarioDecisionPoint.FinalHuntSetup)
            {
                return new AEDPolicyEvaluationResult(true, "AED-FH-110-HOLD", AdaptationIntent.Hold, null, PolicyNoChangeReason.HoldRule, "HOLD_RULE");
            }

            var survivalScore = snapshot.RosterProfileSummary.Survival.MeanObservedScore;
            var noiseScore = snapshot.RosterProfileSummary.Noise.MeanObservedScore;
            if (!survivalScore.HasValue || !noiseScore.HasValue)
            {
                return new AEDPolicyEvaluationResult(false, string.Empty, AdaptationIntent.Hold, null, PolicyNoChangeReason.None, AEDReasonCodes.InputInvalid);
            }

            var survival = policyConfig.SurvivalThresholds.Classify(survivalScore.Value);
            var noise = policyConfig.NoiseThresholds.Classify(noiseScore.Value);

            if (survival == ScoreBand.Low)
            {
                return new AEDPolicyEvaluationResult(true, "AED-PRE-010-SURVIVAL-LOW-RELIEVE", AdaptationIntent.Relieve, ScenarioConfigKey.SupportItemBudget, PolicyNoChangeReason.None, string.Empty, survival, noise);
            }

            if (noise == ScoreBand.Low)
            {
                return new AEDPolicyEvaluationResult(true, "AED-PRE-020-NOISE-LOW-RELIEVE", AdaptationIntent.Relieve, ScenarioConfigKey.SupportItemBudget, PolicyNoChangeReason.None, string.Empty, survival, noise);
            }

            if (survival == ScoreBand.High && noise == ScoreBand.High)
            {
                return new AEDPolicyEvaluationResult(true, "AED-PRE-030-BOTH-HIGH-INCREASE", AdaptationIntent.IncreasePressure, ScenarioConfigKey.ChaseSpeed, PolicyNoChangeReason.None, string.Empty, survival, noise);
            }

            return new AEDPolicyEvaluationResult(true, "AED-PRE-040-MIXED-HOLD", AdaptationIntent.Hold, null, PolicyNoChangeReason.HoldRule, "HOLD_RULE", survival, noise);
        }
    }
}
