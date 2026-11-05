using EchoProtocol.Api.Common;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Services.Models;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IScenarioConfigRegistry
{
    Task<ServiceResult<ScenarioConfigDefinition>> GetActiveAsync(
        string scenarioConfigId,
        string scenarioConfigVersion,
        string unityCompatibilityVersion,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ScenarioConfigResolution>> ResolveWithFixedFallbackAsync(
        string scenarioConfigId,
        string scenarioConfigVersion,
        string unityCompatibilityVersion,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ScenarioConfigDefinition>> GetProductionFixedFallbackAsync(
        string unityCompatibilityVersion,
        CancellationToken cancellationToken = default);
}
