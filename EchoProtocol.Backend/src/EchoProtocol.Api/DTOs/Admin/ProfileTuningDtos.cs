using System.ComponentModel.DataAnnotations;

namespace EchoProtocol.Api.DTOs.Admin;

public sealed record ProfileTuning(
    decimal SurvivalAlpha, decimal NoiseAlpha, decimal ObjectiveAlpha, decimal ToolUsageAlpha,
    decimal NoiseMin, decimal NoiseMax, decimal ToolUsageMax, string[] NoiseTypes,
    decimal ObjectiveTimeMin, decimal ObjectiveTimeMax,
    int HistoryMatches, int HistoryOpportunities, double HistoryMaxAgeDays, double HistoryHalfLifeDays);

public sealed record PublishProfileTuning(string? ExpectedVersion, [Required] ProfileTuning Settings,
    [Required, StringLength(64, MinimumLength = 64)] string PreviewFingerprint);

public sealed record PreviewProfileTuning(
    [Required] ProfileTuning Settings, bool Survived, bool CompletedObjective, int ToolsUsed,
    [Required] Dictionary<string, int> NoiseCounts,
    [Range(0, 7200)] decimal ObjectiveSeconds,
    [Range(0, 100)] decimal? PreviousScore);
