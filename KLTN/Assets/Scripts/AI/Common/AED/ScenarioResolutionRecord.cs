using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common.Profile;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class ScenarioResolutionContext
    {
        public ScenarioResolutionContext(
            ScenarioResolutionRequest request,
            ScenarioConfig baseConfig,
            ScenarioConfigBaseRef baseRef,
            AdaptiveInputSnapshot adaptiveInputSnapshot,
            AEDPolicyConfig policyConfig,
            AEDEvidencePolicy evidencePolicy,
            AdaptiveParameterRegistry parameterRegistry)
        {
            Request =
                request
                ?? throw new ArgumentNullException(
                    nameof(request));

            BaseConfig =
                baseConfig
                ?? throw new ArgumentNullException(
                    nameof(baseConfig));

            BaseRef =
                baseRef
                ?? throw new ArgumentNullException(
                    nameof(baseRef));

            AdaptiveInputSnapshot =
                adaptiveInputSnapshot;

            PolicyConfig =
                policyConfig;

            EvidencePolicy =
                evidencePolicy;

            ParameterRegistry =
                parameterRegistry;
        }

        public ScenarioResolutionRequest Request { get; }

        public ScenarioConfig BaseConfig { get; }

        public ScenarioConfigBaseRef BaseRef { get; }

        public AdaptiveInputSnapshot AdaptiveInputSnapshot { get; }

        public AEDPolicyConfig PolicyConfig { get; }

        public AEDEvidencePolicy EvidencePolicy { get; }

        public AdaptiveParameterRegistry ParameterRegistry { get; }
    }

    public sealed class ScenarioResolutionRecord
    {
        private ScenarioResolutionRecord()
        {
        }

        public DateTime FinalizedAtUtc { get; private set; }

        public Guid DecisionId { get; private set; }

        public string DecisionSemanticFingerprint
        {
            get;
            private set;
        }

        public Guid TargetMatchId { get; private set; }

        public ScenarioResolutionMode ResolutionMode
        {
            get;
            private set;
        }

        public ScenarioDecisionPoint DecisionPoint
        {
            get;
            private set;
        }

        public string PhaseContext { get; private set; }

        public string ExperimentConditionRef
        {
            get;
            private set;
        }

        public AEDInputGateStatus? InputStatus
        {
            get;
            private set;
        }

        public IReadOnlyList<string> InputReasons
        {
            get;
            private set;
        }

        public Guid SnapshotId { get; private set; }

        public string SnapshotContentFingerprint
        {
            get;
            private set;
        }

        public string RosterIdentity { get; private set; }

        public IReadOnlyList<ProfileRevisionRef>
            SourceProfileRevisions { get; private set; }

        public double? SurvivalObservedMean
        {
            get;
            private set;
        }

        public ScoreBand? SurvivalBand
        {
            get;
            private set;
        }

        public double? NoiseObservedMean
        {
            get;
            private set;
        }

        public ScoreBand? NoiseBand
        {
            get;
            private set;
        }

        public string SelectedPolicyRuleId
        {
            get;
            private set;
        }

        public AdaptationIntent AdaptationIntent
        {
            get;
            private set;
        }

        public PolicyNoChangeReason PolicyNoChangeReason
        {
            get;
            private set;
        }

        public IReadOnlyList<AdaptiveRequestedChange>
            RequestedChanges { get; private set; }

        public CandidateValidationStatus CandidateValidationStatus
        {
            get;
            private set;
        }

        public ScenarioConfigBaseRef BaseRef
        {
            get;
            private set;
        }

        public string ResolvedBeforeScenarioConfigVersion
        {
            get;
            private set;
        }

        public string ResolvedBeforeFingerprint
        {
            get;
            private set;
        }

        public string ResultingScenarioConfigVersion
        {
            get;
            private set;
        }

        public string ResultingScenarioConfigFingerprint
        {
            get;
            private set;
        }

        public ScenarioConfigSource? ResultingConfigSource
        {
            get;
            private set;
        }

        public AdaptiveDecisionResult? Result
        {
            get;
            private set;
        }

        public string ReasonCode { get; private set; }

        public IReadOnlyList<string> DetailReasonCodes
        {
            get;
            private set;
        }

        public ScenarioFallbackAction FallbackAction
        {
            get;
            private set;
        }

        public string PolicyVersion { get; private set; }

        public string PolicyConfigVersion
        {
            get;
            private set;
        }

        public string EvidencePolicyVersion
        {
            get;
            private set;
        }

        public string ParameterRegistryVersion
        {
            get;
            private set;
        }

        public string ContentWhitelistVersion
        {
            get;
            private set;
        }

        public string FallbackConfigId
        {
            get;
            private set;
        }

        public string FallbackConfigVersion
        {
            get;
            private set;
        }

        public ScenarioResolutionCommitDisposition CommitDisposition
        {
            get;
            private set;
        }

        public string GuardReasonCode { get; private set; }

        public bool StaleInputDetected { get; private set; }

        public bool StaleBaseConfigDetected
        {
            get;
            private set;
        }

        public bool DecisionWindowValid { get; private set; }

        public bool HasStateAuthority { get; private set; }

        public static ScenarioResolutionRecord Create(
            ScenarioResolutionEngineResult result,
            ScenarioConfig resultingConfig,
            DateTime finalizedAtUtc,
            bool hasStateAuthority)
        {
            if (result == null)
            {
                throw new ArgumentNullException(
                    nameof(result));
            }

            if (finalizedAtUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Finalization time must be UTC.",
                    nameof(finalizedAtUtc));
            }

            var context = result.Context;

            var request = context.Request;

            var decision = result.AttemptedDecision;

            var snapshot =
                context.AdaptiveInputSnapshot;

            var finalReason =
                DetermineFinalReason(
                    result,
                    decision,
                    request.ResolutionMode);

            var finalResult =
                result.HasDecisionIdentityConflict
                    ? (AdaptiveDecisionResult?)null
                    : result.IsPrecommitRejected
                        ? AdaptiveDecisionResult.FixedFallback
                        : decision.Result;

            var fallbackAction =
                result.IsPrecommitRejected
                    ? FixedDirector.FallbackFor(
                        request.DecisionPoint)
                    : result.HasDecisionIdentityConflict
                        ? ScenarioFallbackAction.None
                        : decision.FallbackAction;

            var candidateStatus =
                result.IsPrecommitRejected
                || result.HasDecisionIdentityConflict
                    ? CandidateValidationStatus.Invalid
                    : decision.CandidateValidationStatus;

            var record =
                new ScenarioResolutionRecord
                {
                    FinalizedAtUtc = finalizedAtUtc,

                    DecisionId =
                        decision.DecisionId,

                    DecisionSemanticFingerprint =
                        result.DecisionSemanticFingerprint,

                    TargetMatchId =
                        request.TargetMatchId,

                    ResolutionMode =
                        request.ResolutionMode,

                    DecisionPoint =
                        request.DecisionPoint,

                    PhaseContext =
                        request.PhaseContext,

                    ExperimentConditionRef =
                        request.ExperimentCondition,

                    InputStatus =
                        decision.InputStatus,

                    InputReasons =
                        CopyStrings(
                            decision.InputReasons),

                    SnapshotId =
                        snapshot != null
                            ? snapshot.SnapshotId
                            : Guid.Empty,

                    SnapshotContentFingerprint =
                        snapshot != null
                            ? snapshot.SnapshotContentFingerprint
                            : string.Empty,

                    RosterIdentity =
                        snapshot != null
                            ? snapshot.RosterIdentity
                            : string.Empty,

                    SourceProfileRevisions =
                        CopyRevisions(
                            snapshot != null
                            && snapshot.Provenance != null
                                ? snapshot.Provenance
                                    .SourceProfileRevisions
                                : null),

                    SurvivalObservedMean =
                        snapshot != null
                        && snapshot.RosterProfileSummary != null
                            ? snapshot.RosterProfileSummary
                                .Survival.MeanObservedScore
                            : null,

                    SurvivalBand =
                        decision.SurvivalBand,

                    NoiseObservedMean =
                        snapshot != null
                        && snapshot.RosterProfileSummary != null
                            ? snapshot.RosterProfileSummary
                                .Noise.MeanObservedScore
                            : null,

                    NoiseBand =
                        decision.NoiseBand,

                    SelectedPolicyRuleId =
                        decision.RuleId,

                    AdaptationIntent =
                        decision.Intent,

                    PolicyNoChangeReason =
                        decision.NoChangeReason,

                    RequestedChanges =
                        CopyChanges(
                            decision.RequestedChanges),

                    CandidateValidationStatus =
                        candidateStatus,

                    BaseRef =
                        context.BaseRef,

                    ResolvedBeforeScenarioConfigVersion =
                        context.BaseConfig
                            .ScenarioConfigVersion,

                    ResolvedBeforeFingerprint =
                        ScenarioConfigFingerprint.Compute(
                            context.BaseConfig),

                    ResultingScenarioConfigVersion =
                        resultingConfig != null
                            ? resultingConfig
                                .ScenarioConfigVersion
                            : string.Empty,

                    ResultingScenarioConfigFingerprint =
                        resultingConfig != null
                            ? ScenarioConfigFingerprint.Compute(
                                resultingConfig)
                            : string.Empty,

                    ResultingConfigSource =
                        resultingConfig != null
                            ? resultingConfig.ConfigSource
                            : (ScenarioConfigSource?)null,

                    Result =
                        finalResult,

                    ReasonCode =
                        finalReason,

                    DetailReasonCodes =
                        CopyStrings(
                            decision.ReasonCodes),

                    FallbackAction =
                        fallbackAction,

                    PolicyVersion =
                        context.PolicyConfig != null
                            ? context.PolicyConfig.PolicyVersion
                            : resultingConfig != null
                                ? resultingConfig.PolicyVersion
                                : context.BaseConfig.PolicyVersion,

                    PolicyConfigVersion =
                        context.PolicyConfig != null
                            ? context.PolicyConfig.PolicyConfigVersion
                            : string.Empty,

                    EvidencePolicyVersion =
                        context.EvidencePolicy != null
                            ? context.EvidencePolicy
                                .EvidencePolicyVersion
                            : string.Empty,

                    ParameterRegistryVersion =
                        context.ParameterRegistry != null
                            ? context.ParameterRegistry
                                .ParameterRegistryVersion
                            : string.Empty,

                    ContentWhitelistVersion =
                        context.PolicyConfig != null
                            ? context.PolicyConfig.ContentWhitelistVersion
                            : FixedDirector.FixedBaselineContentWhitelistVersion,

                    FallbackConfigId =
                        context.BaseConfig.FallbackConfigId,

                    // Current implementation has one concrete
                    // versioned fallback artifact:
                    // FIXED_BASELINE_V1.
                    FallbackConfigVersion =
                        FixedDirector
                            .FixedBaselineScenarioVersion,

                    CommitDisposition =
                        result.CommitDisposition,

                    GuardReasonCode =
                        result.GuardReasonCode,

                    StaleInputDetected =
                        string.Equals(
                            finalReason,
                            AEDReasonCodes.StaleInput,
                            StringComparison.Ordinal),

                    StaleBaseConfigDetected =
                        string.Equals(
                            finalReason,
                            AEDReasonCodes.StaleBaseConfig,
                            StringComparison.Ordinal),

                    DecisionWindowValid =
                        !string.Equals(
                            finalReason,
                            AEDReasonCodes.DecisionWindowClosed,
                            StringComparison.Ordinal),

                    HasStateAuthority =
                        hasStateAuthority
                };

            return record;
        }

        private static string DetermineFinalReason(
            ScenarioResolutionEngineResult result,
            AdaptiveDecision decision,
            ScenarioResolutionMode mode)
        {
            if ((result.IsPrecommitRejected
                 || result.HasDecisionIdentityConflict)
                && AEDReasonCodes.IsControlled(
                    result.GuardReasonCode))
            {
                return result.GuardReasonCode;
            }

            if (mode == ScenarioResolutionMode.Fixed)
            {
                if (decision.Result
                        == AdaptiveDecisionResult.FixedFallback
                    && decision.AppliedConfig == null)
                {
                    return AEDReasonCodes
                        .FallbackConfigInvalid;
                }

                return string.Empty;
            }

            switch (decision.Result)
            {
                case AdaptiveDecisionResult.Applied:
                    return AEDReasonCodes.AdaptiveApplied;

                case AdaptiveDecisionResult.NoChange:
                    return AEDReasonCodes.AdaptiveNoChange;

                case AdaptiveDecisionResult.FixedFallback:
                    for (var i = 0;
                         i < decision.ReasonCodes.Count;
                         i++)
                    {
                        if (AEDReasonCodes.IsControlled(
                                decision.ReasonCodes[i]))
                        {
                            return decision.ReasonCodes[i];
                        }
                    }

                    return AEDReasonCodes.FixedFallback;

                default:
                    return AEDReasonCodes.FixedFallback;
            }
        }

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

        private static IReadOnlyList<ProfileRevisionRef>
            CopyRevisions(
                IReadOnlyList<ProfileRevisionRef> values)
        {
            if (values == null)
            {
                return Array.Empty<ProfileRevisionRef>();
            }

            var copy =
                new List<ProfileRevisionRef>(
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
