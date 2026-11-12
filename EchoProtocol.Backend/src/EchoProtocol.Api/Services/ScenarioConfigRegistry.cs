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
    // The 0.1.1 gameplay build still uses the 0.1.0 authored scenario registry.
    internal static string RegistryCompatibility(string gameVersion) => gameVersion == "0.1.1" ? "0.1.0" : gameVersion;

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

        var registryVersion = RegistryCompatibility(unityCompatibilityVersion);
        var fallbacks = await db.ScenarioConfigs.AsNoTracking()
            .Where(item => item.ConfigSource == ScenarioConfigSource.Fixed
                && item.IsFixedFallback && item.IsActive && item.IsProductionApproved
                && item.UnityCompatibilityVersion == registryVersion)
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
        var registryVersion = RegistryCompatibility(unityCompatibilityVersion);
        var fallbacks = await db.ScenarioConfigs.AsNoTracking()
            .Where(item => item.ConfigSource == ScenarioConfigSource.Fixed
                && item.IsFixedFallback && item.IsActive && item.IsProductionApproved
                && item.UnityCompatibilityVersion == registryVersion)
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

    public async Task<ServiceResult<IReadOnlyList<ScenarioConfigDefinition>>>
        GetProductionAdaptiveCandidatesAsync(
            string unityCompatibilityVersion,
            CancellationToken cancellationToken = default)
    {
        var registryVersion = RegistryCompatibility(unityCompatibilityVersion);
        var candidates = await db.ScenarioConfigs.AsNoTracking()
            .Where(x =>
                x.ConfigSource == ScenarioConfigSource.Adaptive &&
                x.IsActive &&
                x.IsProductionApproved &&
                !x.IsFixedFallback &&
                x.UnityCompatibilityVersion == registryVersion)
            .OrderBy(x => x.ScenarioConfigId)
            .ThenBy(x => x.ScenarioConfigVersion)
            .ToArrayAsync(cancellationToken);

        if (candidates.Length == 0)
        {
            return ServiceResult<IReadOnlyList<ScenarioConfigDefinition>>
                .Failure(
                    "No approved Adaptive config is registered",
                    ErrorCodes.ScenarioConfigNotFound);
        }

        var fallback = await GetProductionFixedFallbackAsync(
            unityCompatibilityVersion, cancellationToken);

        if (!fallback.IsSuccess)
        {
            return ServiceResult<IReadOnlyList<ScenarioConfigDefinition>>
                .Failure(fallback.Message, fallback.ErrorCode!);
        }

        foreach (var candidate in candidates)
        {
            var validation = await ValidateAsync(
                candidate, unityCompatibilityVersion, cancellationToken);

            if (!validation.IsValid ||
                candidate.FallbackConfigId != fallback.Data!.ScenarioConfigId ||
                candidate.FallbackConfigVersion != fallback.Data.ScenarioConfigVersion)
            {
                return ServiceResult<IReadOnlyList<ScenarioConfigDefinition>>
                    .Failure(
                        $"Invalid Adaptive config: {candidate.ScenarioConfigId} " +
                        string.Join(",", validation.ReasonCodes),
                        ErrorCodes.ScenarioConfigInvalid);
            }
        }

        return ServiceResult<IReadOnlyList<ScenarioConfigDefinition>>
            .Success(candidates);
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
        return ScenarioConfigValidator.Validate(config, content, RegistryCompatibility(unityCompatibilityVersion));
    }
}
