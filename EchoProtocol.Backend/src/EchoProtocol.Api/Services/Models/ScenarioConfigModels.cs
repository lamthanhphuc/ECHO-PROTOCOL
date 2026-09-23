using EchoProtocol.Api.Entities;

namespace EchoProtocol.Api.Services.Models;

public sealed record ScenarioConfigValidationResult(
    bool IsValid,
    IReadOnlyList<string> ReasonCodes);

public sealed record ScenarioConfigResolution(
    ScenarioConfigDefinition Config,
    bool UsedFixedFallback,
    IReadOnlyList<string> RequestedConfigValidationErrors);
