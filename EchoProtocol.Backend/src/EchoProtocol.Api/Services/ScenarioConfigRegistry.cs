using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using EchoProtocol.Api.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Services;

public sealed class ScenarioConfigRegistry(AppDbContext db) : IScenarioConfigRegistry
{
    public async Task<ServiceResult<ScenarioConfigDefinition>> GetActiveAsync(
        string scenarioConfigId,
        string scenarioConfigVersion,
        string unityCompatibilityVersion,
        CancellationToken cancellationToken = default)
    {
        var config = await db.ScenarioConfigs.AsNoTracking().SingleOrDefaultAsync(item =>
            item.ScenarioConfigId == scenarioConfigId
            && item.ScenarioConfigVersion == scenarioConfigVersion,
            cancellationToken);
        if (config is null)
            return ServiceResult<ScenarioConfigDefinition>.Failure("Scenario config was not found", ErrorCodes.ScenarioConfigNotFound);

        var validation = await ValidateAsync(config, unityCompatibilityVersion, cancellationToken);
        return validation.IsValid
            ? ServiceResult<ScenarioConfigDefinition>.Success(config)
            : ServiceResult<ScenarioConfigDefinition>.Failure(
                string.Join(",", validation.ReasonCodes), ErrorCodes.ScenarioConfigInvalid);
    }

    public async Task<ServiceResult<ScenarioConfigResolution>> ResolveWithFixedFallbackAsync(
        string scenarioConfigId,
        string scenarioConfigVersion,
        string unityCompatibilityVersion,
        CancellationToken cancellationToken = default)
    {
        var requested = await db.ScenarioConfigs.AsNoTracking().SingleOrDefaultAsync(item =>
            item.ScenarioConfigId == scenarioConfigId
            && item.ScenarioConfigVersion == scenarioConfigVersion,
            cancellationToken);
        var requestedValidation = await ValidateAsync(requested, unityCompatibilityVersion, cancellationToken);
        if (requestedValidation.IsValid)
            return ServiceResult<ScenarioConfigResolution>.Success(
                new(requested!, false, Array.Empty<string>()));

        var fallbacks = await db.ScenarioConfigs.AsNoTracking()
            .Where(item => item.ConfigSource == ScenarioConfigSource.Fixed
                && item.IsFixedFallback && item.IsActive && item.IsProductionApproved
                && item.UnityCompatibilityVersion == unityCompatibilityVersion)
            .OrderBy(item => item.ScenarioConfigId)
            .ThenBy(item => item.ScenarioConfigVersion)
            .ToArrayAsync(cancellationToken);
        if (fallbacks.Length != 1)
            return ServiceResult<ScenarioConfigResolution>.Failure(
                "A unique production-approved fixed fallback is not configured",
                ErrorCodes.ScenarioFixedFallbackNotConfigured);

        var fallbackValidation = await ValidateAsync(fallbacks[0], unityCompatibilityVersion, cancellationToken);
        if (!fallbackValidation.IsValid)
            return ServiceResult<ScenarioConfigResolution>.Failure(
                string.Join(",", fallbackValidation.ReasonCodes), ErrorCodes.ScenarioFixedFallbackInvalid);

        return ServiceResult<ScenarioConfigResolution>.Success(
            new(fallbacks[0], true, requestedValidation.ReasonCodes),
            "Requested config rejected; deterministic fixed fallback resolved");
    }

    public async Task<ServiceResult<ScenarioConfigDefinition>> GetProductionFixedFallbackAsync(
        string unityCompatibilityVersion,
        CancellationToken cancellationToken = default)
    {
        var fallbacks = await db.ScenarioConfigs.AsNoTracking()
            .Where(item => item.ConfigSource == ScenarioConfigSource.Fixed
                && item.IsFixedFallback && item.IsActive && item.IsProductionApproved
                && item.UnityCompatibilityVersion == unityCompatibilityVersion)
            .ToArrayAsync(cancellationToken);
        if (fallbacks.Length != 1)
            return ServiceResult<ScenarioConfigDefinition>.Failure(
                "A unique production-approved fixed fallback is not configured",
                ErrorCodes.ScenarioFixedFallbackNotConfigured);
        var validation = await ValidateAsync(fallbacks[0], unityCompatibilityVersion, cancellationToken);
        return validation.IsValid
            ? ServiceResult<ScenarioConfigDefinition>.Success(fallbacks[0])
            : ServiceResult<ScenarioConfigDefinition>.Failure(
                string.Join(",", validation.ReasonCodes), ErrorCodes.ScenarioFixedFallbackInvalid);
    }

    private async Task<ScenarioConfigValidationResult> ValidateAsync(
        ScenarioConfigDefinition? config,
        string unityCompatibilityVersion,
        CancellationToken cancellationToken)
    {
        if (config is null)
            return ScenarioConfigValidator.Validate(null, Array.Empty<ScenarioContentDefinition>(), unityCompatibilityVersion);

        var content = await db.ScenarioContentDefinitions.AsNoTracking()
            .Where(item => item.ContentWhitelistVersion == config.ContentWhitelistVersion)
            .ToArrayAsync(cancellationToken);
        return ScenarioConfigValidator.Validate(config, content, unityCompatibilityVersion);
    }
}
