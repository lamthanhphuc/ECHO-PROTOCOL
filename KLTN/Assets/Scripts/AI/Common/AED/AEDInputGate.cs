using System;
using System.Collections.Generic;
using EchoProtocol.AI.Common.Profile;

namespace EchoProtocol.AI.Common.AED
{
    public enum AEDInputGateStatus { Eligible, Ineligible, Invalid }

    public sealed class AdaptiveInputCurrencyValidation
    {
        public AdaptiveInputCurrencyValidation(
            bool targetMatchCurrent,
            bool decisionPointCurrent,
            bool phaseContextCurrent,
            bool rosterIdentityCurrent,
            bool sourceProfileRevisionsCurrent,
            bool snapshotFingerprintValid,
            bool profileSemanticsSupported)
        {
            TargetMatchCurrent = targetMatchCurrent;
            DecisionPointCurrent = decisionPointCurrent;
            PhaseContextCurrent = phaseContextCurrent;
            RosterIdentityCurrent = rosterIdentityCurrent;
            SourceProfileRevisionsCurrent = sourceProfileRevisionsCurrent;
            SnapshotFingerprintValid = snapshotFingerprintValid;
            ProfileSemanticsSupported = profileSemanticsSupported;
        }

        public bool TargetMatchCurrent { get; }
        public bool DecisionPointCurrent { get; }
        public bool PhaseContextCurrent { get; }
        public bool RosterIdentityCurrent { get; }
        public bool SourceProfileRevisionsCurrent { get; }
        public bool SnapshotFingerprintValid { get; }
        public bool ProfileSemanticsSupported { get; }
        public bool IsCurrent => TargetMatchCurrent
                                 && DecisionPointCurrent
                                 && PhaseContextCurrent
                                 && RosterIdentityCurrent
                                 && SourceProfileRevisionsCurrent
                                 && SnapshotFingerprintValid
                                 && ProfileSemanticsSupported;
    }

    public sealed class AEDInputGateResult
    {
        public AEDInputGateResult(
            AEDInputGateStatus status,
            IReadOnlyList<string> reasons,
            Guid snapshotId,
            string snapshotContentFingerprint,
            Guid targetMatchId,
            ScenarioDecisionPoint decisionPoint,
            string rosterIdentity,
            IReadOnlyList<ProfileRevisionRef> sourceProfileRevisions,
            string resolvedEvidencePolicyVersion,
            string resolvedPolicyVersion)
        {
            Status = status;
            Reasons = CopyStrings(reasons);

            SnapshotId = snapshotId;

            SnapshotContentFingerprint =
                snapshotContentFingerprint ?? string.Empty;

            TargetMatchId = targetMatchId;
            DecisionPoint = decisionPoint;

            RosterIdentity =
                rosterIdentity ?? string.Empty;

            SourceProfileRevisions =
                CopyRevisions(sourceProfileRevisions);

            ResolvedEvidencePolicyVersion =
                resolvedEvidencePolicyVersion ?? string.Empty;

            ResolvedPolicyVersion =
                resolvedPolicyVersion ?? string.Empty;
        }

        public AEDInputGateStatus Status { get; }

        public IReadOnlyList<string> Reasons { get; }

        public Guid SnapshotId { get; }

        public string SnapshotContentFingerprint { get; }

        public Guid TargetMatchId { get; }

        public ScenarioDecisionPoint DecisionPoint { get; }

        public string RosterIdentity { get; }

        public IReadOnlyList<ProfileRevisionRef>
            SourceProfileRevisions { get; }

        public string ResolvedEvidencePolicyVersion { get; }

        public string ResolvedPolicyVersion { get; }

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
    }

    public static class AEDInputGate
    {
        public static AEDInputGateResult Evaluate(
            AdaptiveInputSnapshot snapshot,
            ScenarioResolutionRequest request,
            AEDPolicyConfig policyConfig,
            AEDEvidencePolicy evidencePolicy,
            AdaptiveInputCurrencyValidation currency)
        {
            if (request == null)
            {
                throw new ArgumentNullException(
                    nameof(request));
            }

            if (policyConfig == null
                || !policyConfig.IsValid)
            {
                return Result(
                    AEDInputGateStatus.Invalid,
                    AEDReasonCodes.PolicyConfigInvalid,
                    snapshot,
                    request,
                    policyConfig,
                    evidencePolicy);
            }

            if (evidencePolicy == null
                || !evidencePolicy.IsValid
                || !string.Equals(
                    evidencePolicy.EvidencePolicyVersion,
                    policyConfig.EvidencePolicyVersion,
                    StringComparison.Ordinal))
            {
                return Result(
                    AEDInputGateStatus.Invalid,
                    AEDReasonCodes.PolicyConfigInvalid,
                    snapshot,
                    request,
                    policyConfig,
                    evidencePolicy);
            }

            if (snapshot == null)
            {
                return Result(
                    AEDInputGateStatus.Ineligible,
                    AEDReasonCodes.InputIncomplete,
                    null,
                    request,
                    policyConfig,
                    evidencePolicy);
            }

            if (currency == null)
            {
                return Result(
                    AEDInputGateStatus.Invalid,
                    AEDReasonCodes.StaleInput,
                    snapshot,
                    request,
                    policyConfig,
                    evidencePolicy);
            }

            if (!currency.ProfileSemanticsSupported)
            {
                return Result(
                    AEDInputGateStatus.Invalid,
                    AEDReasonCodes.UnsupportedVersion,
                    snapshot,
                    request,
                    policyConfig,
                    evidencePolicy);
            }

            if (!currency.SnapshotFingerprintValid)
            {
                return Result(
                    AEDInputGateStatus.Invalid,
                    AEDReasonCodes.InputInvalid,
                    snapshot,
                    request,
                    policyConfig,
                    evidencePolicy);
            }

            if (!currency.TargetMatchCurrent
                || !currency.DecisionPointCurrent
                || !currency.PhaseContextCurrent
                || !currency.RosterIdentityCurrent
                || !currency.SourceProfileRevisionsCurrent
                || snapshot.TargetMatchId
                    != request.TargetMatchId
                || snapshot.DecisionPoint
                    != request.DecisionPoint
                || !string.Equals(
                    snapshot.PhaseContext,
                    request.PhaseContext,
                    StringComparison.Ordinal))
            {
                return Result(
                    AEDInputGateStatus.Invalid,
                    AEDReasonCodes.StaleInput,
                    snapshot,
                    request,
                    policyConfig,
                    evidencePolicy);
            }

            if (snapshot.SnapshotValidity
                == SnapshotValidity.Invalid)
            {
                return Result(
                    AEDInputGateStatus.Invalid,
                    AEDReasonCodes.InputInvalid,
                    snapshot,
                    request,
                    policyConfig,
                    evidencePolicy);
            }

            if (snapshot.SnapshotValidity
                == SnapshotValidity.Partial)
            {
                return Result(
                    AEDInputGateStatus.Ineligible,
                    AEDReasonCodes.InputIncomplete,
                    snapshot,
                    request,
                    policyConfig,
                    evidencePolicy);
            }

            var structuralStatus =
                ValidateSnapshotStructure(
                    snapshot,
                    out var structuralReason);

            if (structuralStatus
                != AEDInputGateStatus.Eligible)
            {
                return Result(
                    structuralStatus,
                    structuralReason,
                    snapshot,
                    request,
                    policyConfig,
                    evidencePolicy);
            }

            if (!HasSufficientEvidence(
                    snapshot,
                    evidencePolicy))
            {
                return Result(
                    AEDInputGateStatus.Ineligible,
                    AEDReasonCodes.InputIncomplete,
                    snapshot,
                    request,
                    policyConfig,
                    evidencePolicy);
            }

            return Result(
                AEDInputGateStatus.Eligible,
                string.Empty,
                snapshot,
                request,
                policyConfig,
                evidencePolicy);
        }

        private static AEDInputGateResult Result(
            AEDInputGateStatus status,
            string reason,
            AdaptiveInputSnapshot snapshot,
            ScenarioResolutionRequest request,
            AEDPolicyConfig policyConfig,
            AEDEvidencePolicy evidencePolicy)
        {
            var reasons =
                string.IsNullOrWhiteSpace(reason)
                    ? Array.Empty<string>()
                    : new[] { reason };

            return new AEDInputGateResult(
                status,
                reasons,
                snapshot != null
                    ? snapshot.SnapshotId
                    : Guid.Empty,
                snapshot != null
                    ? snapshot.SnapshotContentFingerprint
                    : string.Empty,
                snapshot != null
                    ? snapshot.TargetMatchId
                    : request != null
                        ? request.TargetMatchId
                        : Guid.Empty,
                snapshot != null
                    ? snapshot.DecisionPoint
                    : request != null
                        ? request.DecisionPoint
                        : ScenarioDecisionPoint.PreMatch,
                snapshot != null
                    ? snapshot.RosterIdentity
                    : string.Empty,
                snapshot != null
                    && snapshot.Provenance != null
                    ? snapshot.Provenance.SourceProfileRevisions
                    : null,
                evidencePolicy != null
                    ? evidencePolicy.EvidencePolicyVersion
                    : policyConfig != null
                        ? policyConfig.EvidencePolicyVersion
                        : string.Empty,
                policyConfig != null
                    ? policyConfig.PolicyVersion
                    : string.Empty);
        }

        private static AEDInputGateStatus
            ValidateSnapshotStructure(
                AdaptiveInputSnapshot snapshot,
                out string reason)
        {
            reason = string.Empty;

            if (snapshot.RosterProfileSummary == null
                || snapshot.Provenance == null)
            {
                reason = AEDReasonCodes.InputInvalid;
                return AEDInputGateStatus.Invalid;
            }

            var summary =
                snapshot.RosterProfileSummary;

            if (summary.TeamSize != snapshot.TeamSize
                || snapshot.PlayerProfileSnapshots.Count
                    != snapshot.TeamSize
                || !string.Equals(
                    summary.RosterIdentity,
                    snapshot.RosterIdentity,
                    StringComparison.Ordinal))
            {
                reason = AEDReasonCodes.InputInvalid;
                return AEDInputGateStatus.Invalid;
            }

            if (!string.Equals(
                    summary.Survival.ComparisonSemanticKey,
                    snapshot.Provenance.SurvivalComparisonKey,
                    StringComparison.Ordinal)
                || !string.Equals(
                    summary.Noise.ComparisonSemanticKey,
                    snapshot.Provenance.NoiseComparisonKey,
                    StringComparison.Ordinal))
            {
                reason = AEDReasonCodes.UnsupportedVersion;
                return AEDInputGateStatus.Invalid;
            }

            var userIds =
                new HashSet<string>(
                    StringComparer.Ordinal);

            for (var i = 0;
                 i < snapshot.PlayerProfileSnapshots.Count;
                 i++)
            {
                var player =
                    snapshot.PlayerProfileSnapshots[i];

                if (player == null
                    || !userIds.Add(player.UserId))
                {
                    reason =
                        AEDReasonCodes.InputInvalid;

                    return AEDInputGateStatus.Invalid;
                }

                if (!string.Equals(
                        player.Survival.ComparisonSemanticKey,
                        snapshot.Provenance.SurvivalComparisonKey,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        player.Noise.ComparisonSemanticKey,
                        snapshot.Provenance.NoiseComparisonKey,
                        StringComparison.Ordinal))
                {
                    reason =
                        AEDReasonCodes.UnsupportedVersion;

                    return AEDInputGateStatus.Invalid;
                }
            }

            var sourceRevisions =
                snapshot.Provenance.SourceProfileRevisions;

            if (sourceRevisions == null
                || sourceRevisions.Count
                    != snapshot.TeamSize)
            {
                reason = AEDReasonCodes.InputInvalid;
                return AEDInputGateStatus.Invalid;
            }

            var revisionByUser =
                new Dictionary<string, long>(
                    StringComparer.Ordinal);

            for (var i = 0;
                 i < sourceRevisions.Count;
                 i++)
            {
                var revision =
                    sourceRevisions[i];

                if (revision == null
                    || revisionByUser.ContainsKey(
                        revision.UserId))
                {
                    reason =
                        AEDReasonCodes.InputInvalid;

                    return AEDInputGateStatus.Invalid;
                }

                revisionByUser.Add(
                    revision.UserId,
                    revision.ProfileRevision);
            }

            for (var i = 0;
                 i < snapshot.PlayerProfileSnapshots.Count;
                 i++)
            {
                var player =
                    snapshot.PlayerProfileSnapshots[i];

                if (!revisionByUser.TryGetValue(
                        player.UserId,
                        out var revision)
                    || revision
                        != player.ProfileRevision)
                {
                    reason =
                        AEDReasonCodes.InputInvalid;

                    return AEDInputGateStatus.Invalid;
                }
            }

            return AEDInputGateStatus.Eligible;
        }

        private static bool HasSufficientEvidence(
            AdaptiveInputSnapshot snapshot,
            AEDEvidencePolicy evidencePolicy)
        {
            var summary =
                snapshot.RosterProfileSummary;

            if (!evidencePolicy
                    .RequireFullRosterObservedCoverage)
            {
                return false;
            }

            if (summary.Survival.AggregationStatus
                    != RosterAggregationStatus.Available
                || summary.Noise.AggregationStatus
                    != RosterAggregationStatus.Available
                || summary.Survival.ObservedActiveCount
                    != snapshot.TeamSize
                || summary.Noise.ObservedActiveCount
                    != snapshot.TeamSize
                || summary.Survival.ColdStartCount != 0
                || summary.Noise.ColdStartCount != 0
                || summary.Survival.MissingCount != 0
                || summary.Noise.MissingCount != 0
                || summary.Survival.UnsupportedCount != 0
                || summary.Noise.UnsupportedCount != 0
                || summary.Survival.ObservedCoverageRatio != 1d
                || summary.Noise.ObservedCoverageRatio != 1d
                || !summary.Survival.MeanObservedScore.HasValue
                || !summary.Noise.MeanObservedScore.HasValue)
            {
                return false;
            }

            for (var i = 0;
                 i < snapshot.PlayerProfileSnapshots.Count;
                 i++)
            {
                var player =
                    snapshot.PlayerProfileSnapshots[i];

                if (player.Survival.Status
                        != PlayerDimensionStatus.Active
                    || player.Noise.Status
                        != PlayerDimensionStatus.Active
                    || !player.Survival.Score.HasValue
                    || !player.Noise.Score.HasValue
                    || player.Survival.SampleCount
                        < evidencePolicy
                            .MinimumSurvivalSampleCountPerObservedPlayer
                    || player.Noise.SampleCount
                        < evidencePolicy
                            .MinimumNoiseSampleCountPerObservedPlayer)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
