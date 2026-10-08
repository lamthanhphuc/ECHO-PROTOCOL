using System;

namespace EchoProtocol.AI.AED
{
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
    }

    [Serializable]
    public sealed class AEDResolvePreMatchResponse
    {
        public bool success;
        public string errorCode;
        public AEDResolvePreMatchData data;
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
}
