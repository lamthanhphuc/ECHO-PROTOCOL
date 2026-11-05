namespace EchoProtocol.Api.DTOs.Scenarios;

public sealed record AdaptiveInputSnapshotReadResponse(
    Guid SnapshotId, Guid TargetMatchId, string DecisionPoint, string PhaseContext,
    string SnapshotContentFingerprint, string RosterIdentity, int TeamSize,
    string SnapshotValidity, string[] ReasonCodes, DateTime CreatedAtUtc,
    string FingerprintVersion,
    string? ProfileFormulaSemanticId, string? SurvivalComparisonKey, string? NoiseComparisonKey,
    string SurvivalAggregationStatus, string NoiseAggregationStatus,
    decimal? SurvivalMeanObservedScore, decimal? NoiseMeanObservedScore,
    int SurvivalObservedActiveCount, int NoiseObservedActiveCount,
    string ObjectiveAggregationStatus, string? ObjectiveComparisonKey,
    decimal? ObjectiveMeanObservedScore, int ObjectiveObservedActiveCount,
    string ToolUsageAggregationStatus, string? ToolUsageComparisonKey,
    decimal? ToolUsageMeanObservedScore, int ToolUsageObservedActiveCount,
    AdaptiveInputPlayerReadResponse[] Players,
    bool RosterCurrent, bool ProfileRevisionsCurrent, bool SnapshotFingerprintValid,
    bool ProfileSemanticsSupported,
    bool TargetMatchCurrent, bool DecisionPointCurrent, bool PhaseContextCurrent)
{
    public bool SurvivalMeanObservedScorePresent => SurvivalMeanObservedScore.HasValue;
    public bool NoiseMeanObservedScorePresent => NoiseMeanObservedScore.HasValue;
    public bool ObjectiveMeanObservedScorePresent => ObjectiveMeanObservedScore.HasValue;
    public bool ToolUsageMeanObservedScorePresent => ToolUsageMeanObservedScore.HasValue;
}

public sealed record AdaptiveInputPlayerReadResponse(
    Guid UserId, bool ProfileAvailable, Guid? ProfileLineageId, long? ProfileRevision,
    decimal? SurvivalScore, string SurvivalStatus, int? SurvivalSampleCount,
    string? SurvivalComparisonKey, decimal? NoiseScore, string NoiseStatus,
    int? NoiseSampleCount, string? NoiseComparisonKey,
    decimal? ObjectiveScore, string ObjectiveStatus, int ObjectiveSampleCount,
    string? ObjectiveComparisonKey, decimal? ToolUsageScore, string ToolUsageStatus,
    int ToolUsageSampleCount, string? ToolUsageComparisonKey)
{
    public bool ProfileRevisionPresent => ProfileRevision.HasValue;
    public bool SurvivalScorePresent => SurvivalScore.HasValue;
    public bool NoiseScorePresent => NoiseScore.HasValue;
    public bool ObjectiveScorePresent => ObjectiveScore.HasValue;
    public bool ToolUsageScorePresent => ToolUsageScore.HasValue;
}
