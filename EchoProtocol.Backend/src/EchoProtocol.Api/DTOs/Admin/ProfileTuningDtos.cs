using System.ComponentModel.DataAnnotations;

namespace EchoProtocol.Api.DTOs.Admin;

public sealed record ProfileTuning(
    decimal SurvivalAlpha, decimal NoiseAlpha, decimal ObjectiveAlpha, decimal ToolUsageAlpha,
    decimal NoiseMin, decimal NoiseMax, decimal ToolUsageMax, string[] NoiseTypes,
    decimal ObjectiveTimeMin, decimal ObjectiveTimeMax,
    int HistoryMatches, int HistoryOpportunities, double HistoryMaxAgeDays, double HistoryHalfLifeDays);

public sealed record PublishProfileTuning(string? ExpectedVersion, [property: Required] ProfileTuning Settings,
    [property: Required, StringLength(64, MinimumLength = 64)] string PreviewFingerprint);

public sealed record PreviewProfileTuning(
    [property: Required] ProfileTuning Settings, bool Survived, bool CompletedObjective, int ToolsUsed,
    [property: Required] Dictionary<string, int> NoiseCounts,
    [property: Range(0, 7200)] decimal ObjectiveSeconds,
    [property: Range(0, 100)] decimal? PreviousScore);
