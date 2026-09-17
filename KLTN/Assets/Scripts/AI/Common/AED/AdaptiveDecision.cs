using System;
using System.Collections.Generic;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class AdaptiveRequestedChange
    {
        public AdaptiveRequestedChange(
            ScenarioConfigKey key,
            double before,
            double requestedAfter,
            string ruleId,
            string adjustmentRuleId)
        {
            ScenarioMonsterParameters.RequireFinite(
                before,
                nameof(before));

            ScenarioMonsterParameters.RequireFinite(
                requestedAfter,
                nameof(requestedAfter));

            Key = key;
            Before = before;
            RequestedAfter = requestedAfter;

            RuleId =
                ruleId ?? string.Empty;

            AdjustmentRuleId =
                adjustmentRuleId ?? string.Empty;
        }

        public ScenarioConfigKey Key { get; }

        public double Before { get; }

        public double RequestedAfter { get; }

        public string RuleId { get; }

        public string AdjustmentRuleId { get; }
    }

    public sealed class AdaptiveDecision
    {
        public AdaptiveDecision(
            Guid decisionId,
            AdaptiveDecisionResult result,
            ScenarioFallbackAction fallbackAction,
            string ruleId,
            AdaptationIntent intent,
            PolicyNoChangeReason noChangeReason,
            ScenarioConfig appliedConfig,
            IReadOnlyList<string> reasonCodes,
            AEDInputGateStatus? inputStatus = null,
            IReadOnlyList<string> inputReasons = null,
            IReadOnlyList<AdaptiveRequestedChange> requestedChanges = null,
            CandidateValidationStatus candidateValidationStatus =
                CandidateValidationStatus.NotEvaluated,
            ScoreBand? survivalBand = null,
            ScoreBand? noiseBand = null)
        {
            if (decisionId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Decision id is required.",
                    nameof(decisionId));
            }

            DecisionId = decisionId;
            Result = result;
            FallbackAction = fallbackAction;

            RuleId =
                ruleId ?? string.Empty;

            Intent = intent;
            NoChangeReason = noChangeReason;
            AppliedConfig = appliedConfig;

            ReasonCodes =
                CopyStrings(reasonCodes);

            InputStatus = inputStatus;

            InputReasons =
                CopyStrings(inputReasons);

            RequestedChanges =
                CopyChanges(requestedChanges);

            CandidateValidationStatus =
                candidateValidationStatus;

            SurvivalBand = survivalBand;
            NoiseBand = noiseBand;
        }

        public Guid DecisionId { get; }

        public AdaptiveDecisionResult Result { get; }

        public ScenarioFallbackAction FallbackAction { get; }

        public string RuleId { get; }

        public AdaptationIntent Intent { get; }

        public PolicyNoChangeReason NoChangeReason { get; }

        public ScenarioConfig AppliedConfig { get; }

        public IReadOnlyList<string> ReasonCodes { get; }

        public AEDInputGateStatus? InputStatus { get; }

        public IReadOnlyList<string> InputReasons { get; }

        public IReadOnlyList<AdaptiveRequestedChange>
            RequestedChanges { get; }

        public CandidateValidationStatus
            CandidateValidationStatus { get; }

        public ScoreBand? SurvivalBand { get; }

        public ScoreBand? NoiseBand { get; }

        private static IReadOnlyList<string> CopyStrings(
            IReadOnlyList<string> values)
        {
            if (values == null)
            {
                return Array.Empty<string>();
            }

            var copy =
                new List<string>(values.Count);

            for (var i = 0; i < values.Count; i++)
            {
                copy.Add(
                    values[i] ?? string.Empty);
            }

            return copy.AsReadOnly();
        }

        private static IReadOnlyList<AdaptiveRequestedChange>
            CopyChanges(
                IReadOnlyList<AdaptiveRequestedChange> values)
        {
            if (values == null)
            {
                return Array.Empty<AdaptiveRequestedChange>();
            }

            var copy =
                new List<AdaptiveRequestedChange>(
                    values.Count);

            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] != null)
                {
                    copy.Add(values[i]);
                }
            }

            return copy.AsReadOnly();
        }
    }
}
