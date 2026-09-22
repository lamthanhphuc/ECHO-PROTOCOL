using System;
using System.Globalization;
using System.IO;
using EchoProtocol.AI.Common.AED;
using UnityEngine;

namespace EchoProtocol.AI.AED
{
    public sealed class AdaptiveDecisionLocalAuditStore
    {
        [Serializable]
        private sealed class AuditDto
        {
            public string finalizedAtUtc;
            public string decisionId;
            public string decisionSemanticFingerprint;
            public string targetMatchId;

            public string resolutionMode;
            public string decisionPoint;
            public string phaseContext;
            public string experimentCondition;

            public string inputStatus;
            public string[] inputReasons;

            public string snapshotId;
            public string snapshotContentFingerprint;
            public string rosterIdentity;
            public string[] sourceProfileRevisions;

            public string survivalObservedMean;
            public string survivalBand;

            public string noiseObservedMean;
            public string noiseBand;

            public string selectedPolicyRuleId;
            public string adaptationIntent;
            public string policyNoChangeReason;

            public string[] requestedChanges;

            public string candidateValidationStatus;

            public string baseConfigId;
            public string baseScenarioConfigVersion;
            public string baseContentFingerprint;
            public string baseKind;

            public string resolvedBeforeScenarioConfigVersion;
            public string resolvedBeforeFingerprint;

            public string resultingScenarioConfigVersion;
            public string resultingScenarioConfigFingerprint;
            public string resultingConfigSource;

            public string result;
            public string reasonCode;
            public string[] detailReasonCodes;
            public string fallbackAction;

            public string policyVersion;
            public string policyConfigVersion;
            public string evidencePolicyVersion;
            public string parameterRegistryVersion;
            public string contentWhitelistVersion;

            public string fallbackConfigId;
            public string fallbackConfigVersion;

            public string commitDisposition;
            public string guardReasonCode;

            public bool staleInputDetected;
            public bool staleBaseConfigDetected;
            public bool decisionWindowValid;
            public bool hasStateAuthority;
        }

        private readonly string _path;

        public AdaptiveDecisionLocalAuditStore(
            string path)
        {
            _path = path;
        }

        public void TryAppend(
            ScenarioResolutionRecord record)
        {
            if (record == null
                || string.IsNullOrWhiteSpace(_path))
            {
                return;
            }

            try
            {
                var directory =
                    Path.GetDirectoryName(_path);

                if (!string.IsNullOrWhiteSpace(
                        directory))
                {
                    Directory.CreateDirectory(
                        directory);
                }

                var dto =
                    CreateDto(record);

                var json =
                    JsonUtility.ToJson(dto);

                File.AppendAllText(
                    _path,
                    json + Environment.NewLine);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[AED] Local audit append failed: "
                    + exception.Message);
            }
        }

        private static AuditDto CreateDto(
            ScenarioResolutionRecord record)
        {
            var revisions =
                new string[
                    record.SourceProfileRevisions.Count];

            for (var i = 0;
                 i < revisions.Length;
                 i++)
            {
                var revision =
                    record.SourceProfileRevisions[i];

                revisions[i] =
                    revision.UserId
                    + ":"
                    + revision.ProfileRevision
                        .ToString(
                            CultureInfo.InvariantCulture);
            }

            var changes =
                new string[
                    record.RequestedChanges.Count];

            for (var i = 0;
                 i < changes.Length;
                 i++)
            {
                var change =
                    record.RequestedChanges[i];

                changes[i] =
                    change.Key
                    + "|"
                    + change.Before.ToString(
                        "R",
                        CultureInfo.InvariantCulture)
                    + "|"
                    + change.RequestedAfter.ToString(
                        "R",
                        CultureInfo.InvariantCulture)
                    + "|"
                    + change.RuleId
                    + "|"
                    + change.AdjustmentRuleId;
            }

            return new AuditDto
            {
                finalizedAtUtc =
                    record.FinalizedAtUtc.ToString(
                        "O",
                        CultureInfo.InvariantCulture),

                decisionId =
                    record.DecisionId.ToString("D"),

                decisionSemanticFingerprint =
                    record.DecisionSemanticFingerprint,

                targetMatchId =
                    record.TargetMatchId.ToString("D"),

                resolutionMode =
                    record.ResolutionMode.ToString(),

                decisionPoint =
                    record.DecisionPoint.ToString(),

                phaseContext =
                    record.PhaseContext,

                experimentCondition =
                    record.ExperimentConditionRef,

                inputStatus =
                    record.InputStatus.HasValue
                        ? record.InputStatus.Value.ToString()
                        : string.Empty,

                inputReasons =
                    ToArray(record.InputReasons),

                snapshotId =
                    record.SnapshotId == Guid.Empty
                        ? string.Empty
                        : record.SnapshotId.ToString("D"),

                snapshotContentFingerprint =
                    record.SnapshotContentFingerprint,

                rosterIdentity =
                    record.RosterIdentity,

                sourceProfileRevisions =
                    revisions,

                survivalObservedMean =
                    Number(record.SurvivalObservedMean),

                survivalBand =
                    record.SurvivalBand.HasValue
                        ? record.SurvivalBand.Value.ToString()
                        : string.Empty,

                noiseObservedMean =
                    Number(record.NoiseObservedMean),

                noiseBand =
                    record.NoiseBand.HasValue
                        ? record.NoiseBand.Value.ToString()
                        : string.Empty,

                selectedPolicyRuleId =
                    record.SelectedPolicyRuleId,

                adaptationIntent =
                    record.AdaptationIntent.ToString(),

                policyNoChangeReason =
                    record.PolicyNoChangeReason.ToString(),

                requestedChanges =
                    changes,

                candidateValidationStatus =
                    record.CandidateValidationStatus
                        .ToString(),

                baseConfigId =
                    record.BaseRef.BaseConfigId,

                baseScenarioConfigVersion =
                    record.BaseRef.BaseScenarioConfigVersion,

                baseContentFingerprint =
                    record.BaseRef.BaseContentFingerprint,

                baseKind =
                    record.BaseRef.BaseKind.ToString(),

                resolvedBeforeScenarioConfigVersion =
                    record.ResolvedBeforeScenarioConfigVersion,

                resolvedBeforeFingerprint =
                    record.ResolvedBeforeFingerprint,

                resultingScenarioConfigVersion =
                    record.ResultingScenarioConfigVersion,

                resultingScenarioConfigFingerprint =
                    record.ResultingScenarioConfigFingerprint,

                resultingConfigSource =
                    record.ResultingConfigSource.HasValue
                        ? record.ResultingConfigSource.Value
                            .ToString()
                        : string.Empty,

                result =
                    record.Result.HasValue
                        ? record.Result.Value.ToString()
                        : string.Empty,

                reasonCode =
                    record.ReasonCode,

                detailReasonCodes =
                    ToArray(record.DetailReasonCodes),

                fallbackAction =
                    record.FallbackAction.ToString(),

                policyVersion =
                    record.PolicyVersion,

                policyConfigVersion =
                    record.PolicyConfigVersion,

                evidencePolicyVersion =
                    record.EvidencePolicyVersion,

                parameterRegistryVersion =
                    record.ParameterRegistryVersion,

                contentWhitelistVersion =
                    record.ContentWhitelistVersion,

                fallbackConfigId =
                    record.FallbackConfigId,

                fallbackConfigVersion =
                    record.FallbackConfigVersion,

                commitDisposition =
                    record.CommitDisposition.ToString(),

                guardReasonCode =
                    record.GuardReasonCode,

                staleInputDetected =
                    record.StaleInputDetected,

                staleBaseConfigDetected =
                    record.StaleBaseConfigDetected,

                decisionWindowValid =
                    record.DecisionWindowValid,

                hasStateAuthority =
                    record.HasStateAuthority
            };
        }

        private static string Number(
            double? value)
        {
            return value.HasValue
                ? value.Value.ToString(
                    "R",
                    CultureInfo.InvariantCulture)
                : string.Empty;
        }

        private static string[] ToArray(
            System.Collections.Generic
                .IReadOnlyList<string> values)
        {
            if (values == null)
            {
                return Array.Empty<string>();
            }

            var copy =
                new string[values.Count];

            for (var i = 0;
                 i < values.Count;
                 i++)
            {
                copy[i] =
                    values[i] ?? string.Empty;
            }

            return copy;
        }
    }
}
