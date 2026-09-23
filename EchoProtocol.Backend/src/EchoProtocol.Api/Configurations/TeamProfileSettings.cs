namespace EchoProtocol.Api.Configurations;

public sealed class TeamProfileSettings
{
    public const string SectionName = "TeamProfile";

    public string ProfileFormulaVersion { get; init; } = "PROFILE_FORMULA_V1_1";
    public string? TeamPerformanceFormulaVersion { get; init; }
    public string? PhaseRegistryVersion { get; init; }
    public string? NormalizationConfigVersion { get; init; }
    public string[] ObjectiveBearingPhases { get; init; } = [];
    public string[] AllowedOverlapPairs { get; init; } = [];
    public decimal? ObjectiveTimeMin { get; init; }
    public decimal? ObjectiveTimeMax { get; init; }
    public decimal? ObjectiveWeight { get; init; }
    public decimal? SurvivalWeight { get; init; }
    public decimal? TeamworkWeight { get; init; }
    public decimal? ResourceWeight { get; init; }
}
