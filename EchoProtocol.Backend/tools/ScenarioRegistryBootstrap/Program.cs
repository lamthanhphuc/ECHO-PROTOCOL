using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, WriteIndented = true };
json.Converters.Add(new JsonStringEnumConverter());
try
{
    var argsMap = Parse(args);
    var mode = argsMap.GetValueOrDefault("mode", "Audit");
    if (mode is not ("Audit" or "Plan" or "Prepare") || argsMap.GetValueOrDefault("environment") != "Local")
        throw new InvalidOperationException("Specify --environment Local and --mode Audit|Plan|Prepare.");
    if (mode == "Prepare" && !argsMap.ContainsKey("write-authorized"))
        throw new InvalidOperationException("Prepare requires --write-authorized.");
    var manifestPath = argsMap.GetValueOrDefault("manifest") ?? throw new InvalidOperationException("--manifest is required.");
    var manifest = JsonSerializer.Deserialize<Manifest>(await File.ReadAllTextAsync(manifestPath), json)
        ?? throw new InvalidOperationException("Manifest is empty.");
    if (manifest.Scope != "DISPOSABLE_LOCAL_TECHNICAL_QUALIFICATION" || manifest.Compatibility != "0.1.0" ||
        manifest.Fixed is null || manifest.Content is not { Length: 4 } ||
        string.IsNullOrWhiteSpace(manifest.ReliefId) || string.IsNullOrWhiteSpace(manifest.ReliefVersion))
        throw new InvalidOperationException("Manifest scope or required definitions are invalid.");
    var connection = Environment.GetEnvironmentVariable("ECHO_M4_POSTGRES_CONNECTION")
        ?? throw new InvalidOperationException("ECHO_M4_POSTGRES_CONNECTION is required.");
    var target = new NpgsqlConnectionStringBuilder(connection);
    if (target.Host is not ("127.0.0.1" or "localhost" or "::1") || target.Port != 5544 ||
        target.Database != "aedv02_native_test")
        throw new InvalidOperationException("Only loopback:5544/aedv02_native_test is allowed; production rejected.");
    await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    await using var transaction = mode == "Prepare"
        ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;

    var fixedConfig = manifest.Fixed;
    var relief = Clone(fixedConfig, json);
    relief.ScenarioConfigId = manifest.ReliefId;
    relief.ScenarioConfigVersion = manifest.ReliefVersion;
    relief.ConfigSource = ScenarioConfigSource.Adaptive;
    relief.IsFixedFallback = false;
    relief.SupportItemBudget = fixedConfig.SupportItemBudget + 1;
    relief.Provenance = "LOCAL_TECHNICAL_RELIEF_FROM_FIXEDDIRECTOR_20261127";

    var content = manifest.Content;
    var configs = new[] { fixedConfig, relief };
    if (!AdaptiveConfigurationService.IsApprovedFixed(fixedConfig) ||
        !AdaptiveConfigurationService.IsApprovedCandidate(AEDPreMatchPolicyV1.SurvivalRelief, fixedConfig, relief))
        throw new InvalidOperationException("Fixed fallback or Relief candidate is not approved by existing selection rules.");
    foreach (var config in configs)
    {
        var validation = ScenarioConfigValidator.Validate(config, content, manifest.Compatibility);
        if (!validation.IsValid) throw new InvalidOperationException(string.Join(",", validation.ReasonCodes));
    }
    if (content.Select(x => (x.ContentType, x.ContentId, x.ContentWhitelistVersion, x.UnityCompatibilityVersion)).Distinct().Count() != 4)
        throw new InvalidOperationException("Whitelist identities must be unique.");

    var existingConfigs = await db.ScenarioConfigs.AsNoTracking().Where(x => x.UnityCompatibilityVersion == manifest.Compatibility).ToArrayAsync();
    var existingContent = await db.ScenarioContentDefinitions.AsNoTracking()
        .Where(x => x.ContentWhitelistVersion == fixedConfig.ContentWhitelistVersion && x.UnityCompatibilityVersion == manifest.Compatibility)
        .ToArrayAsync();
    var otherFallback = existingConfigs.Where(x => x.IsFixedFallback && x.IsActive && x.IsProductionApproved &&
        (x.ScenarioConfigId != fixedConfig.ScenarioConfigId || x.ScenarioConfigVersion != fixedConfig.ScenarioConfigVersion)).ToArray();
    if (otherFallback.Length != 0) throw new InvalidOperationException("Conflicting active Fixed fallback exists.");

    var addConfigs = new List<ScenarioConfigDefinition>();
    var addContent = new List<ScenarioContentDefinition>();
    foreach (var proposed in content)
    {
        var existing = existingContent.SingleOrDefault(x => x.ContentType == proposed.ContentType && x.ContentId == proposed.ContentId);
        if (existing is null) addContent.Add(proposed);
        else if (!EquivalentContent(existing, proposed, json)) throw new InvalidOperationException($"Conflicting immutable content: {proposed.ContentType}/{proposed.ContentId}");
    }
    foreach (var proposed in configs)
    {
        var existing = existingConfigs.SingleOrDefault(x => x.ScenarioConfigId == proposed.ScenarioConfigId && x.ScenarioConfigVersion == proposed.ScenarioConfigVersion);
        if (existing is null) addConfigs.Add(proposed);
        else if (!Equivalent(existing, proposed, json)) throw new InvalidOperationException($"Conflicting immutable config: {proposed.ScenarioConfigId}/{proposed.ScenarioConfigVersion}");
    }
    if (mode == "Prepare")
    {
        var now = DateTime.UtcNow;
        foreach (var item in addContent) { item.Id = Guid.NewGuid(); item.CreatedAtUtc = now; item.UpdatedAtUtc = now; }
        foreach (var item in addConfigs) { item.CreatedAtUtc = now; item.UpdatedAtUtc = now; }
        db.ScenarioContentDefinitions.AddRange(addContent);
        db.ScenarioConfigs.AddRange(addConfigs);
        await db.SaveChangesAsync();
        await transaction!.CommitAsync();
    }
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        mode, environment = "Local", database = target.Database, manifest.Scope, manifest.Source,
        compatibility = manifest.Compatibility, fixedId = fixedConfig.ScenarioConfigId,
        reliefId = relief.ScenarioConfigId, existingConfigs = existingConfigs.Length,
        existingContent = existingContent.Length, addedConfigs = mode == "Prepare" ? addConfigs.Count : 0,
        addedContent = mode == "Prepare" ? addContent.Count : 0,
        plannedConfigs = addConfigs.Select(x => x.ScenarioConfigId),
        plannedContent = addContent.Select(x => x.ContentId),
        applicationEligibilityFlag = "IsProductionApproved=true in disposable Local only; no production rollout approval"
    }, json));
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}

static Dictionary<string, string> Parse(string[] arguments)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < arguments.Length; i++)
    {
        if (!arguments[i].StartsWith("--", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected argument.");
        var key = arguments[i][2..];
        if (key == "write-authorized") result.Add(key, "true");
        else if (++i < arguments.Length) result.Add(key, arguments[i]);
        else throw new InvalidOperationException($"Missing value for --{key}.");
    }
    return result;
}

static T Clone<T>(T value, JsonSerializerOptions options) =>
    JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, options), options)!;

static bool Equivalent(ScenarioConfigDefinition existing, ScenarioConfigDefinition proposed, JsonSerializerOptions options)
{
    var copy = Clone(proposed, options);
    copy.CreatedAtUtc = existing.CreatedAtUtc;
    copy.UpdatedAtUtc = existing.UpdatedAtUtc;
    return JsonSerializer.Serialize(existing, options) == JsonSerializer.Serialize(copy, options);
}

static bool EquivalentContent(ScenarioContentDefinition existing, ScenarioContentDefinition proposed, JsonSerializerOptions options)
{
    var copy = Clone(proposed, options);
    copy.Id = existing.Id;
    copy.CreatedAtUtc = existing.CreatedAtUtc;
    copy.UpdatedAtUtc = existing.UpdatedAtUtc;
    return JsonSerializer.Serialize(existing, options) == JsonSerializer.Serialize(copy, options);
}

sealed record Manifest(string Scope, string Source, string Compatibility, ScenarioConfigDefinition Fixed,
    string ReliefId, string ReliefVersion, ScenarioContentDefinition[] Content);
