using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common.Profile;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class AEDDebugSnapshot
    {
        public AEDDebugSnapshot(
            ScenarioResolutionRecord record)
        {
            Record =
                record
                ?? throw new ArgumentNullException(
                    nameof(record));
        }

        public ScenarioResolutionRecord Record { get; }

        public Guid DecisionId =>
            Record.DecisionId;

        public ScenarioResolutionMode ResolutionMode =>
            Record.ResolutionMode;

        public ScenarioDecisionPoint DecisionPoint =>
            Record.DecisionPoint;

        public Guid TargetMatchId =>
            Record.TargetMatchId;

        public string PhaseContext =>
            Record.PhaseContext;

        public Guid SnapshotId =>
            Record.SnapshotId;

        public string SnapshotContentFingerprint =>
            Record.SnapshotContentFingerprint;

        public AEDInputGateStatus? InputGateStatus =>
            Record.InputStatus;

        public IReadOnlyList<string> InputReasons =>
            Record.InputReasons;

        public double? SurvivalObservedMean =>
            Record.SurvivalObservedMean;

        public ScoreBand? SurvivalBand =>
            Record.SurvivalBand;

        public double? NoiseObservedMean =>
            Record.NoiseObservedMean;

        public ScoreBand? NoiseBand =>
            Record.NoiseBand;

        public string PolicyVersion =>
            Record.PolicyVersion;

        public string PolicyConfigVersion =>
            Record.PolicyConfigVersion;

        public string EvidencePolicyVersion =>
            Record.EvidencePolicyVersion;

        public string SelectedRuleId =>
            Record.SelectedPolicyRuleId;

        public AdaptationIntent AdaptationIntent =>
            Record.AdaptationIntent;

        public PolicyNoChangeReason PolicyNoChangeReason =>
            Record.PolicyNoChangeReason;

        public ScenarioConfigBaseRef BaseRef =>
            Record.BaseRef;

        public IReadOnlyList<AdaptiveRequestedChange>
            RequestedChanges =>
                Record.RequestedChanges;

        public CandidateValidationStatus
            CandidateValidationStatus =>
                Record.CandidateValidationStatus;

        public AdaptiveDecisionResult? Result =>
            Record.Result;

        public string ReasonCode =>
            Record.ReasonCode;

        public ScenarioFallbackAction FallbackAction =>
            Record.FallbackAction;

        public string ResultingScenarioConfigVersion =>
            Record.ResultingScenarioConfigVersion;

        public string ParameterRegistryVersion =>
            Record.ParameterRegistryVersion;

        public string ContentWhitelistVersion =>
            Record.ContentWhitelistVersion;

        public string FallbackConfigVersion =>
            Record.FallbackConfigVersion;

        public bool StaleInputDetected =>
            Record.StaleInputDetected;

        public bool StaleBaseConfigDetected =>
            Record.StaleBaseConfigDetected;

        public bool DecisionWindowValid =>
            Record.DecisionWindowValid;

        public bool HasStateAuthority =>
            Record.HasStateAuthority;
    }
}
