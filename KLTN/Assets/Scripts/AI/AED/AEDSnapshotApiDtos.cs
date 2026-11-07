using System;

namespace EchoProtocol.AI.AED
{
    [Serializable]
    public sealed class AEDPhase8ApprovalRequestV1
    {
        public string decisionId;
        public int completedPhaseOrdinal;
        public string directorKind;
        public string policyVersion;
        public string rosterIdentity;
        public string snapshotFingerprint;
        public string evidenceFingerprint;
        public string sourceFingerprint;
        public string payloadFingerprint;
        public long expectedRevision;
        public string payloadJson;
    }

    [Serializable]
    public sealed class AEDPhase8ApprovalDataV1
    {
        public string matchId;
        public string decisionId;
        public int completedPhaseOrdinal;
        public string directorKind;
        public string policyVersion;
        public string rosterIdentity;
        public string snapshotFingerprint;
        public string evidenceFingerprint;
        public string sourceFingerprint;
        public string payloadFingerprint;
        public long expectedRevision;
        public string approvalScope;
        public string status;
        public string reasonCode;
        public bool isReplay;
        public bool gameplayApproved;
    }

    [Serializable]
    public sealed class AEDPhase8ApprovalResponseV1
    {
        public bool success;
        public string message;
        public string errorCode;
        public AEDPhase8ApprovalDataV1 data;
    }

    [Serializable]
    public sealed class AEDPhase8EvidenceProbeRequestV1
    {
        public int completedEvidencePhaseOrdinal;
        public string evidenceFingerprint;
    }

    [Serializable]
    public sealed class AEDPhase8EvidenceProbeDataV1
    {
        public string matchId;
        public int completedEvidencePhaseOrdinal;
        public string evidenceFingerprint;
        public bool canonicalEvidenceVerified;
        public bool extendedMetricsVerified;
        public bool resourceLedgerVerified;
        public bool gameplayApproved;
        public string reasonCode;
    }

    [Serializable]
    public sealed class AEDPhase8EvidenceProbeResponseV1
    {
        public bool success;
        public string message;
        public string errorCode;
        public AEDPhase8EvidenceProbeDataV1 data;
    }

    [Serializable]
    public sealed class AEDSnapshotApiResponse
    {
        public bool success;
        public string message;
        public string errorCode;
        public AEDSnapshotApiData data;
    }

    [Serializable]
    public sealed class AEDSnapshotApiData
    {
        public string snapshotId;
        public string targetMatchId;
        public string decisionPoint;
        public string phaseContext;
        public string snapshotContentFingerprint;
        public string rosterIdentity;
        public int teamSize;
        public string snapshotValidity;
        public string[] reasonCodes;
        public string createdAtUtc;
        public string profileFormulaSemanticId;
        public string fingerprintVersion;
        public string survivalComparisonKey;
        public string noiseComparisonKey;
        public string survivalAggregationStatus;
        public string noiseAggregationStatus;
        public double survivalMeanObservedScore;
        public double noiseMeanObservedScore;
        public bool survivalMeanObservedScorePresent;
        public bool noiseMeanObservedScorePresent;
        public int survivalObservedActiveCount;
        public int noiseObservedActiveCount;
        public string objectiveComparisonKey;
        public string toolUsageComparisonKey;
        public string objectiveAggregationStatus;
        public string toolUsageAggregationStatus;
        public double objectiveMeanObservedScore;
        public double toolUsageMeanObservedScore;
        public bool objectiveMeanObservedScorePresent;
        public bool toolUsageMeanObservedScorePresent;
        public int objectiveObservedActiveCount;
        public int toolUsageObservedActiveCount;
        public AEDSnapshotPlayerDto[] players;
        public bool rosterCurrent;
        public bool profileRevisionsCurrent;
        public bool snapshotFingerprintValid;
        public bool profileSemanticsSupported;
        public bool targetMatchCurrent;
        public bool decisionPointCurrent;
        public bool phaseContextCurrent;
    }

    [Serializable]
    public sealed class AEDSnapshotPlayerDto
    {
        public string userId;
        public bool profileAvailable;
        public string profileLineageId;
        public long profileRevision;
        public bool profileRevisionPresent;
        public double survivalScore;
        public bool survivalScorePresent;
        public string survivalStatus;
        public int survivalSampleCount;
        public string survivalComparisonKey;
        public double noiseScore;
        public bool noiseScorePresent;
        public string noiseStatus;
        public int noiseSampleCount;
        public string noiseComparisonKey;
        public double objectiveScore;
        public bool objectiveScorePresent;
        public string objectiveStatus;
        public int objectiveSampleCount;
        public string objectiveComparisonKey;
        public double toolUsageScore;
        public bool toolUsageScorePresent;
        public string toolUsageStatus;
        public int toolUsageSampleCount;
        public string toolUsageComparisonKey;
    }

    [Serializable]
    public sealed class AEDResolvePreMatchRequest
    {
        public string decisionId;
        public string resolutionMode;
        public string unityCompatibilityVersion;
        public string experimentCondition;
        public string fallbackFromDecisionId;
    }

    [Serializable]
    public sealed class AEDResolvePreMatchResponse
    {
        public bool success;
        public string errorCode;
        public string message;
        public AEDResolvePreMatchData data;
    }

    [Serializable]
    public sealed class AEDBackendScenarioConfigDto
    {
        public string scenarioConfigId;
        public string scenarioConfigVersion;
        public string schemaVersion;
        public string policyVersion;
        public string configSource;
        public string mapId;
        public string monsterType;
        public string objectiveSpawnSetId;
        public int supportItemBudget;
        public double detectionFillRate;
        public double detectionDecayRate;
        public double chaseSpeed;
        public double searchDuration;
        public string routeModifier;
        public double escapeDoorTimerSeconds;
        public string fallbackConfigId;
        public string fallbackConfigVersion;
        public string contentWhitelistVersion;
        public string unityCompatibilityVersion;
        public string contentFingerprint;
    }

    [Serializable]
    public sealed class AEDResolvePreMatchData
    {
        public string decisionId;
        public string matchId;
        public string snapshotId;
        public string snapshotContentFingerprint;
        public string rosterIdentity;
        public string resolutionResult;
        public bool usedFixedFallback;
        public string fallbackReasonCode;
        public string policyConfigVersion;
        public string preMatchRule;
        public bool IsManaged => policyConfigVersion?.StartsWith("AED_ADMIN_", StringComparison.Ordinal) == true;
        public string candidateValidationStatus;
        public string decisionStatus;
        public string unityApplyStatus;
        public AEDBackendScenarioConfigDto config;
    }

    [Serializable]
    public sealed class AEDConfirmScenarioAppliedRequest
    {
        public string scenarioConfigId;
        public string scenarioConfigVersion;
        public string scenarioConfigFingerprint;
    }

    [Serializable]
    public sealed class AEDPlanV2Request
    {
        public string decisionId;
        public int phaseOrdinal;
        public string decisionPoint;
        public string policyVersion;
        public string baselineVersion;
        public string previousPlanFingerprint;
        public string resultingPlanFingerprint;
        public string changedKey;
        public double previousValue;
        public double appliedValue;
        public string adaptationIntent;
        public string decisionReason;
        public string snapshotId;
        public string snapshotFingerprint;
        public string evidenceFingerprint;
        public string rosterIdentity;
        public string commitStatus;
        public double[] planValues;
    }

    [Serializable]
    public sealed class AEDPlanV2Response
    {
        public bool success;
        public AEDPlanV2Data data;
    }

    [Serializable]
    public sealed class AEDPlanV2Data
    {
        public string matchId;
        public string decisionId;
        public int phaseOrdinal;
        public string decisionPoint;
        public string previousPlanFingerprint;
        public string resultingPlanFingerprint;
        public string changedKey;
        public double previousValue;
        public double appliedValue;
        public string snapshotId;
        public string snapshotFingerprint;
        public string evidenceFingerprint;
        public string rosterIdentity;
        public string commitStatus;
        public string applyStatus;
    }

    [Serializable]
    public sealed class AEDPlanV2AppliedRequest
    {
        public string planFingerprint;
    }

    [Serializable] public sealed class AEDPhase8SourceReferenceDtoV1 { public string eventId; public long eventSequence; public string eventType; public string occurrenceKey; }
    [Serializable] public sealed class AEDPhase8MetricDtoV1 { public string userId; public string metricId; public string metricVersion; public string comparisonContextKey; public int eligible; public int resolvedSuccess; public int resolvedFailure; public int censored; [NonSerialized] public double? normalizedValue; public string confidenceStatus; public string[] sourceOccurrenceKeys; }
    [Serializable] public sealed class AEDPhase8SupplyDtoV1 { public string instanceId; public int toolId; public int zone; public string origin; public string location; public string ownerUserId; [NonSerialized] public int? remainingCharges; public bool reachable; public long sourceTick; public string sourceOccurrenceKey; }
    [Serializable] public sealed class AEDPhase8MinionDtoV1 { public string networkInstanceId; public int zone; public string state; public long sourceTick; public string sourceOccurrenceKey; }
    [Serializable] public sealed class AEDPhase8SourceUploadDtoV1 { public int completedPhaseOrdinal; public string evidenceFingerprint; public string rosterIdentity; public string comparisonContextKey; public string sourceVersion; public AEDPhase8SourceReferenceDtoV1[] sourceEvents; public AEDPhase8MetricDtoV1[] metrics; public AEDPhase8SupplyDtoV1[] supply; public AEDPhase8MinionDtoV1[] minions; }
    [Serializable] public sealed class AEDPhase8SourceVerificationDtoV1
    {
        public bool canonicalVerified;
        public bool historicalSkillVerified;
        public bool pressureVerified;
        public bool supplyLedgerVerified;
        public bool minionPopulationVerified;
        public string reasonCode;

        public bool CompleteForGameplay => canonicalVerified && historicalSkillVerified &&
            pressureVerified && supplyLedgerVerified && minionPopulationVerified;
    }
    [Serializable] public sealed class AEDPhase8SourceVerificationResponseV1 { public bool success; public string message; public string errorCode; public AEDPhase8SourceVerificationDtoV1 data; }
}
