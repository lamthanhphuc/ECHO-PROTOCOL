namespace EchoProtocol.Api.Services.Interfaces;

public interface ITeamProfilePolicy
{
    string ProfileFormulaVersion { get; }
    string? TeamPerformanceFormulaVersion { get; }
    string? PhaseRegistryVersion { get; }
    string? NormalizationConfigVersion { get; }
    decimal? ObjectiveTimeMin { get; }
    decimal? ObjectiveTimeMax { get; }
    IReadOnlySet<string> ObjectiveBearingPhases { get; }

    bool IsOverlapAllowed(string firstPhase, string secondPhase);
    bool TryValidatePhaseRegistry(out string reason);
    bool TryValidateObjectiveNormalization(out string reason);
    bool TryValidateTeamPerformanceWeights(out string reason);
}
