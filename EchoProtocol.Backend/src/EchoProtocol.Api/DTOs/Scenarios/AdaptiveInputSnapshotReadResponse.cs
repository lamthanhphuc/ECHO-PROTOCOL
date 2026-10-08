namespace EchoProtocol.Api.DTOs.Scenarios;

public sealed record AdaptiveInputSnapshotReadResponse(
    Guid SnapshotId, Guid TargetMatchId, string DecisionPoint, string PhaseContext,
    string SnapshotContentFingerprint, string RosterIdentity, int TeamSize,
    string SnapshotValidity, string[] ReasonCodes, DateTime CreatedAtUtc,
    string? ProfileFormulaSemanticId, string? SurvivalComparisonKey, string? NoiseComparisonKey,
    string SurvivalAggregationStatus, string NoiseAggregationStatus,
    decimal? SurvivalMeanObservedScore, decimal? NoiseMeanObservedScore,
    int SurvivalObservedActiveCount, int NoiseObservedActiveCount,
    AdaptiveInputPlayerReadResponse[] Players,
    bool RosterCurrent, bool ProfileRevisionsCurrent, bool SnapshotFingerprintValid,
    bool ProfileSemanticsSupported,
    bool TargetMatchCurrent, bool DecisionPointCurrent, bool PhaseContextCurrent)
{
    public bool SurvivalMeanObservedScorePresent => SurvivalMeanObservedScore.HasValue;
    public bool NoiseMeanObservedScorePresent => NoiseMeanObservedScore.HasValue;
}

public sealed record AdaptiveInputPlayerReadResponse(
    Guid UserId, bool ProfileAvailable, Guid? ProfileLineageId, long? ProfileRevision,
    decimal? SurvivalScore, string SurvivalStatus, int? SurvivalSampleCount,
    string? SurvivalComparisonKey, decimal? NoiseScore, string NoiseStatus,
    int? NoiseSampleCount, string? NoiseComparisonKey)
{
    public bool ProfileRevisionPresent => ProfileRevision.HasValue;
    public bool SurvivalScorePresent => SurvivalScore.HasValue;
    public bool NoiseScorePresent => NoiseScore.HasValue;
}
