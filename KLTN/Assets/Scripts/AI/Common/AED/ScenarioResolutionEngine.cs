using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common.Profile;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class ScenarioResolutionEngineInput
    {
        public ScenarioResolutionEngineInput(
            ScenarioResolutionRequest request,
            ScenarioConfig currentAppliedConfig,
            AdaptiveInputSnapshot adaptiveInputSnapshot,
            AdaptiveInputCurrencyValidation currencyValidation,
            AEDPolicyConfig policyConfig,
            AEDEvidencePolicy evidencePolicy,
            AdaptiveParameterRegistry parameterRegistry,
            string adaptiveInputUnavailableReason)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            CurrentAppliedConfig = currentAppliedConfig;
            AdaptiveInputSnapshot = adaptiveInputSnapshot;
            CurrencyValidation = currencyValidation;
            PolicyConfig = policyConfig;
            EvidencePolicy = evidencePolicy;
            ParameterRegistry = parameterRegistry;
            AdaptiveInputUnavailableReason =
                adaptiveInputUnavailableReason
                ?? string.Empty;
        }

        public ScenarioResolutionEngineInput(
            ScenarioResolutionRequest request,
            ScenarioConfig currentAppliedConfig,
            AdaptiveInputSnapshot adaptiveInputSnapshot,
            AdaptiveInputCurrencyValidation currencyValidation,
            AEDPolicyConfig policyConfig,
            AEDEvidencePolicy evidencePolicy,
            AdaptiveParameterRegistry parameterRegistry)
            : this(
                request,
                currentAppliedConfig,
                adaptiveInputSnapshot,
                currencyValidation,
                policyConfig,
                evidencePolicy,
                parameterRegistry,
                string.Empty)
        {
        }

        public ScenarioResolutionRequest Request { get; }
        public ScenarioConfig CurrentAppliedConfig { get; }
        public AdaptiveInputSnapshot AdaptiveInputSnapshot { get; }
        public AdaptiveInputCurrencyValidation CurrencyValidation { get; }
        public AEDPolicyConfig PolicyConfig { get; }
        public AEDEvidencePolicy EvidencePolicy { get; }
        public AdaptiveParameterRegistry ParameterRegistry { get; }
        public string AdaptiveInputUnavailableReason { get; }
    }

    public enum ScenarioResolutionCommitDisposition
    {
        NewDecision,
        DuplicateNoOp,
        DecisionIdentityConflict,
        PrecommitRejected
    }

    public sealed class ScenarioResolutionEngineResult
    {
        public ScenarioResolutionEngineResult(
            AdaptiveDecision decision,
            AdaptiveDecision attemptedDecision,
            ScenarioResolutionCommitDisposition commitDisposition,
            string guardReasonCode,
            ScenarioResolutionContext context,
            string decisionSemanticFingerprint)
        {
            Decision =
                decision ?? throw new ArgumentNullException(nameof(decision));

            AttemptedDecision =
                attemptedDecision
                ?? throw new ArgumentNullException(nameof(attemptedDecision));

            Context =
                context
                ?? throw new ArgumentNullException(nameof(context));

            CommitDisposition = commitDisposition;
            GuardReasonCode = guardReasonCode ?? string.Empty;

            DecisionSemanticFingerprint =
                ScenarioConfig.RequireText(
                    decisionSemanticFingerprint,
                    nameof(decisionSemanticFingerprint));
        }

        public AdaptiveDecision Decision { get; }

        public AdaptiveDecision AttemptedDecision { get; }

        public ScenarioResolutionContext Context { get; }

        public string DecisionSemanticFingerprint { get; }

        public ScenarioConfigBaseRef CapturedBaseRef =>
            Context.BaseRef;

        public ScenarioConfig CapturedBaseConfig =>
            Context.BaseConfig;

        public Guid CapturedAdaptiveSnapshotId =>
            Context.AdaptiveInputSnapshot != null
                ? Context.AdaptiveInputSnapshot.SnapshotId
                : Guid.Empty;

        public string CapturedAdaptiveSnapshotFingerprint =>
            Context.AdaptiveInputSnapshot != null
                ? Context.AdaptiveInputSnapshot
                    .SnapshotContentFingerprint
                : string.Empty;

        public ScenarioResolutionCommitDisposition CommitDisposition { get; }

        public string GuardReasonCode { get; }

        public bool ShouldApplyConfig =>
            CommitDisposition == ScenarioResolutionCommitDisposition.NewDecision
            && Decision.AppliedConfig != null;

        public bool IsDuplicateNoOp =>
            CommitDisposition == ScenarioResolutionCommitDisposition.DuplicateNoOp;

        public bool HasDecisionIdentityConflict =>
            CommitDisposition ==
            ScenarioResolutionCommitDisposition.DecisionIdentityConflict;

        public bool IsPrecommitRejected =>
            CommitDisposition ==
            ScenarioResolutionCommitDisposition.PrecommitRejected;

        public ScenarioResolutionEngineResult WithPrecommitRejection(
            string guardReasonCode)
        {
            if (string.IsNullOrWhiteSpace(guardReasonCode))
            {
                throw new ArgumentException(
                    "Guard reason code is required.",
                    nameof(guardReasonCode));
            }

            return new ScenarioResolutionEngineResult(
                Decision,
                AttemptedDecision,
                ScenarioResolutionCommitDisposition.PrecommitRejected,
                guardReasonCode,
                Context,
                DecisionSemanticFingerprint);
        }
    }

    public sealed class ScenarioResolutionEngine
    {
        private readonly AdaptiveDecisionLedger _ledger = new AdaptiveDecisionLedger();
        private readonly AEDArtifactIntegrityLedger
    _artifactIntegrityLedger =
        new AEDArtifactIntegrityLedger();

        public ScenarioResolutionEngineResult Resolve(ScenarioResolutionEngineInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            var decision = input.Request.ResolutionMode == ScenarioResolutionMode.Fixed
                ? ResolveFixed(input)
                : ResolveAdaptive(input);

            var semanticFingerprint =
                ScenarioConfigFingerprint.ComputeDecisionSemanticFingerprint(
                    input,
                    decision);

            var lookup = _ledger.Lookup(
                decision.DecisionId,
                semanticFingerprint,
                out var existingDecision);

            if (lookup == AdaptiveDecisionLedgerLookup.ExactReplay)
            {
                return CreateResult(
                    input,
                    existingDecision,
                    decision,
                    semanticFingerprint,
                    ScenarioResolutionCommitDisposition.DuplicateNoOp,
                    AEDGuardCodes.DuplicateDecisionNoOp);
            }

            if (lookup == AdaptiveDecisionLedgerLookup.IdentityConflict)
            {
                return CreateResult(
                    input,
                    existingDecision,
                    decision,
                    semanticFingerprint,
                    ScenarioResolutionCommitDisposition.DecisionIdentityConflict,
                    AEDReasonCodes.DecisionIdentityConflict);
            }

            _ledger.TryRecord(
                decision,
                semanticFingerprint);

            return CreateResult(
                input,
                decision,
                decision,
                semanticFingerprint);
        }

        public void ClearLedger()
        {
            _ledger.Clear();
        }

        private static AdaptiveDecision ResolveFixed(ScenarioResolutionEngineInput input)
        {
            if (input.Request.DecisionPoint == ScenarioDecisionPoint.PreMatch)
            {
                var fixedConfig = FixedDirector.CreateFixedBaseline();
                var validation = ScenarioValidator.ValidateFixedBaseline(fixedConfig);
                if (!validation.IsValid)
                {
                    return new AdaptiveDecision(
                        input.Request.ResolutionId,
                        AdaptiveDecisionResult.FixedFallback,
                        ScenarioFallbackAction.FullFixedConfig,
                        string.Empty,
                        AdaptationIntent.Hold,
                        PolicyNoChangeReason.None,
                        null,
                        new[]
                        {
                            AEDReasonCodes.FallbackConfigInvalid
                        });
                }

                return new AdaptiveDecision(
                    input.Request.ResolutionId,
                    AdaptiveDecisionResult.Applied,
                    ScenarioFallbackAction.None,
                    string.Empty,
                    AdaptationIntent.Hold,
                    PolicyNoChangeReason.None,
                    fixedConfig,
                    Array.Empty<string>());
            }

            return new AdaptiveDecision(
                input.Request.ResolutionId,
                AdaptiveDecisionResult.NoChange,
                ScenarioFallbackAction.KeepLastValidConfig,
                string.Empty,
                AdaptationIntent.Hold,
                PolicyNoChangeReason.HoldRule,
                null,
                new[] { "FIXED_KEEP_LAST_VALID_CONFIG" });
        }

        private AdaptiveDecision ResolveAdaptive(
            ScenarioResolutionEngineInput input)
        {
            var fallbackAction =
                FixedDirector.FallbackFor(
                    input.Request.DecisionPoint);

            var fallbackConfig =
                fallbackAction
                    == ScenarioFallbackAction.FullFixedConfig
                    ? FixedDirector.CreateFixedBaseline()
                    : null;

            if (fallbackConfig != null)
            {
                var fixedValidation =
                    ScenarioValidator.ValidateFixedBaseline(
                        fallbackConfig);

                if (!fixedValidation.IsValid)
                {
                    return Fallback(
                        input,
                        fallbackAction,
                        null,
                        new[]
                        {
                            AEDReasonCodes.FallbackConfigInvalid
                        });
                }
            }

            if (input.PolicyConfig == null
                || !input.PolicyConfig.IsValid)
            {
                return Fallback(
                    input,
                    fallbackAction,
                    fallbackConfig,
                    new[]
                    {
                        AEDReasonCodes.PolicyConfigInvalid
                    });
            }

            if (!string.Equals(
                    input.PolicyConfig.ContentWhitelistVersion,
                    FixedDirector.FixedBaselineContentWhitelistVersion,
                    StringComparison.Ordinal))
            {
                return Fallback(
                    input,
                    fallbackAction,
                    fallbackConfig,
                    new[]
                    {
                        AEDReasonCodes.UnsupportedVersion
                    });
            }

            if (input.EvidencePolicy == null
                || !input.EvidencePolicy.IsValid
                || !string.Equals(
                    input.EvidencePolicy.EvidencePolicyVersion,
                    input.PolicyConfig.EvidencePolicyVersion,
                    StringComparison.Ordinal))
            {
                return Fallback(
                    input,
                    fallbackAction,
                    fallbackConfig,
                    new[]
                    {
                        AEDReasonCodes.PolicyConfigInvalid
                    });
            }

            if (input.ParameterRegistry == null
                || !input.ParameterRegistry
                    .HasCurrentPolicyActiveRules())
            {
                return Fallback(
                    input,
                    fallbackAction,
                    fallbackConfig,
                    new[]
                    {
                        AEDReasonCodes.ParameterRegistryInvalid
                    });
            }

            if (!string.Equals(
                    input.ParameterRegistry.ParameterRegistryVersion,
                    input.PolicyConfig.ParameterRegistryVersion,
                    StringComparison.Ordinal))
            {
                return Fallback(
                    input,
                    fallbackAction,
                    fallbackConfig,
                    new[]
                    {
                        AEDReasonCodes.ParameterRegistryInvalid
                    });
            }

            if (!_artifactIntegrityLedger.TryValidate(
        input.PolicyConfig,
        input.EvidencePolicy,
        input.ParameterRegistry,
        out var artifactReason))
            {
                return Fallback(
                    input,
                    fallbackAction,
                    fallbackConfig,
                    new[]
                    {
            artifactReason
                    });
            }

            if (input.AdaptiveInputSnapshot == null)
            {
                var timeout =
                    !string.IsNullOrWhiteSpace(
                        input.AdaptiveInputUnavailableReason)
                    && (input.AdaptiveInputUnavailableReason
                            .StartsWith(
                                "AED_TIMEOUT",
                                StringComparison.Ordinal)
                        || input.AdaptiveInputUnavailableReason
                            .StartsWith(
                                "ADAPTIVE_INPUT_PROVIDER_TIMEOUT",
                                StringComparison.Ordinal));

                var providerUnavailable =
                    !string.IsNullOrWhiteSpace(
                        input.AdaptiveInputUnavailableReason)
                    && (input.AdaptiveInputUnavailableReason
                            .StartsWith(
                                "ADAPTIVE_INPUT_PROVIDER_UNAVAILABLE",
                                StringComparison.Ordinal)
                        || input.AdaptiveInputUnavailableReason
                            .StartsWith(
                                "ADAPTIVE_INPUT_PROVIDER_EXCEPTION",
                                StringComparison.Ordinal));

                var finalReason =
                    timeout
                        ? AEDReasonCodes.AedTimeout
                        : providerUnavailable
                            ? AEDReasonCodes.AedUnavailable
                            : AEDReasonCodes.InputIncomplete;

                return Fallback(
                    input,
                    fallbackAction,
                    fallbackConfig,
                    new[]
                    {
                        finalReason
                    },
                    timeout || providerUnavailable
                        ? (AEDInputGateStatus?)null
                        : AEDInputGateStatus.Ineligible,
                    timeout || providerUnavailable
                        ? Array.Empty<string>()
                        : new[] { AEDReasonCodes.InputIncomplete });
            }

            var gate =
                AEDInputGate.Evaluate(
                    input.AdaptiveInputSnapshot,
                    input.Request,
                    input.PolicyConfig,
                    input.EvidencePolicy,
                    input.CurrencyValidation);

            if (gate.Status
                != AEDInputGateStatus.Eligible)
            {
                return Fallback(
                    input,
                    fallbackAction,
                    fallbackConfig,
                    gate.Reasons,
                    gate.Status,
                    gate.Reasons);
            }

            var baseConfig =
                input.CurrentAppliedConfig
                ?? FixedDirector.CreateFixedBaseline();

            if (!input.ParameterRegistry
                    .IsCurrentPolicyBaseCompatible(
                        baseConfig))
            {
                return Fallback(
                    input,
                    fallbackAction,
                    fallbackConfig,
                    new[]
                    {
                        AEDReasonCodes.ParameterRegistryInvalid
                    },
                    gate.Status,
                    gate.Reasons);
            }

            var policy =
                AEDPolicyEvaluator.Evaluate(
                    input.Request,
                    input.AdaptiveInputSnapshot,
                    input.PolicyConfig);
            if (!policy.Evaluated)
            {
                return Fallback(
                    input,
                    fallbackAction,
                    fallbackConfig,
                    new[] { policy.ReasonCode },
                    gate.Status,
                    gate.Reasons,
                    policy);
            }

            var baseRef = input.Request.DecisionPoint == ScenarioDecisionPoint.PreMatch
                ? FixedDirector.CreatePreMatchBaseRef(baseConfig)
                : FixedDirector.CreateAppliedBaseRef(baseConfig);
            var build = AdaptiveCandidateBuilder.Build(
                input.Request,
                baseConfig,
                baseRef,
                policy,
                input.ParameterRegistry);

            if (!build.Built
                && build.NoChangeReason
                    == PolicyNoChangeReason.None)
            {
                var reason =
                    AEDReasonCodes.IsControlled(
                        build.ReasonCode)
                        ? build.ReasonCode
                        : AEDReasonCodes.ParameterRegistryInvalid;

                return Fallback(
                    input,
                    fallbackAction,
                    fallbackConfig,
                    new[] { reason },
                    gate.Status,
                    gate.Reasons,
                    policy);
            }

            if (!build.Built)
            {
                return new AdaptiveDecision(
                    input.Request.ResolutionId,
                    AdaptiveDecisionResult.NoChange,
                    ScenarioFallbackAction.None,
                    policy.RuleId,
                    policy.Intent,
                    build.NoChangeReason,
                    null,
                    string.IsNullOrWhiteSpace(build.ReasonCode)
                        ? Array.Empty<string>()
                        : new[] { build.ReasonCode },
                    gate.Status,
                    gate.Reasons,
                    Array.Empty<AdaptiveRequestedChange>(),
                    CandidateValidationStatus.Valid,
                    policy.SurvivalBand,
                    policy.NoiseBand);
            }

            var requestedChanges =
                BuildRequestedChanges(
                    build.Candidate,
                    input.ParameterRegistry);

            var validation = ScenarioValidator.ValidateCandidate(
                build.Candidate,
                input.ParameterRegistry,
                input.Request.DecisionPoint,
                baseConfig);
            if (!validation.IsValid)
            {
                return Fallback(
                    input,
                    fallbackAction,
                    fallbackConfig,
                    validation.ReasonCodes,
                    gate.Status,
                    gate.Reasons,
                    policy,
                    requestedChanges,
                    validation.Status);
            }

            return new AdaptiveDecision(
                input.Request.ResolutionId,
                AdaptiveDecisionResult.Applied,
                ScenarioFallbackAction.None,
                policy.RuleId,
                policy.Intent,
                PolicyNoChangeReason.None,
                build.Candidate.Config,
                Array.Empty<string>(),
                gate.Status,
                gate.Reasons,
                requestedChanges,
                CandidateValidationStatus.Valid,
                policy.SurvivalBand,
                policy.NoiseBand);
        }

        private static IReadOnlyList<AdaptiveRequestedChange>
            BuildRequestedChanges(
                CandidateScenarioConfig candidate,
                AdaptiveParameterRegistry registry)
        {
            if (candidate == null
                || candidate.Change == null
                || registry == null
                || !registry.TryGetRule(
                    candidate.Change.Key,
                    out var rule))
            {
                return Array.Empty<AdaptiveRequestedChange>();
            }

            return new[]
            {
                new AdaptiveRequestedChange(
                    candidate.Change.Key,
                    candidate.Change.BeforeValue,
                    candidate.Change.AfterValue,
                    candidate.RuleId,
                    rule.AdjustmentRuleId)
            };
        }

        private static AdaptiveDecision Fallback(
            ScenarioResolutionEngineInput input,
            ScenarioFallbackAction fallbackAction,
            ScenarioConfig fallbackConfig,
            IReadOnlyList<string> reasons,
            AEDInputGateStatus? inputStatus = null,
            IReadOnlyList<string> inputReasons = null,
            AEDPolicyEvaluationResult policy = null,
            IReadOnlyList<AdaptiveRequestedChange> requestedChanges = null,
            CandidateValidationStatus candidateValidationStatus =
                CandidateValidationStatus.NotEvaluated)
        {
            return new AdaptiveDecision(
                input.Request.ResolutionId,
                AdaptiveDecisionResult.FixedFallback,
                fallbackAction,
                policy != null
                    ? policy.RuleId
                    : string.Empty,
                policy != null
                    ? policy.Intent
                    : AdaptationIntent.Hold,
                policy != null
                    ? policy.NoChangeReason
                    : PolicyNoChangeReason.None,
                fallbackConfig,
                reasons,
                inputStatus,
                inputReasons,
                requestedChanges,
                candidateValidationStatus,
                policy != null
                    ? policy.SurvivalBand
                    : null,
                policy != null
                    ? policy.NoiseBand
                    : null);
        }

        private static ScenarioResolutionEngineResult CreateResult(
            ScenarioResolutionEngineInput input,
            AdaptiveDecision effectiveDecision,
            AdaptiveDecision attemptedDecision,
            string semanticFingerprint,
            ScenarioResolutionCommitDisposition commitDisposition =
                ScenarioResolutionCommitDisposition.NewDecision,
            string guardReasonCode = "")
        {
            var baseConfig =
                input.CurrentAppliedConfig
                ?? FixedDirector.CreateFixedBaseline();

            var capturedBaseRef =
                input.Request.DecisionPoint == ScenarioDecisionPoint.PreMatch
                    ? FixedDirector.CreatePreMatchBaseRef(baseConfig)
                    : FixedDirector.CreateAppliedBaseRef(baseConfig);

            var context =
                new ScenarioResolutionContext(
                    input.Request,
                    baseConfig,
                    capturedBaseRef,
                    input.AdaptiveInputSnapshot,
                    input.PolicyConfig,
                    input.EvidencePolicy,
                    input.ParameterRegistry);

            return new ScenarioResolutionEngineResult(
                effectiveDecision,
                attemptedDecision,
                commitDisposition,
                guardReasonCode,
                context,
                semanticFingerprint);
        }
    }
}
