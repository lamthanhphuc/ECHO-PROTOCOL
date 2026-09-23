using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Models;

namespace EchoProtocol.Api.Services;

public static class ScenarioConfigValidator
{
    public const string SupportedSchemaVersion = "1.1";
    public const string SupportedPolicyVersion = "AED_SCENARIO_POLICY_V1_1";
    public const double MinEscapeDoorTimerSeconds = 45d;
    public const double MaxEscapeDoorTimerSeconds = 60d;

    public static ScenarioConfigValidationResult Validate(
        ScenarioConfigDefinition? config,
        IReadOnlyCollection<ScenarioContentDefinition> content,
        string unityCompatibilityVersion)
    {
        var reasons = new List<string>();
        if (config is null)
        {
            return new(false, ["SCENARIO_CONFIG_UNKNOWN"]);
        }

        if (!config.IsActive) reasons.Add("SCENARIO_CONFIG_DISABLED");
        if (!config.IsProductionApproved) reasons.Add("SCENARIO_CONFIG_UNAPPROVED");
        if (!string.Equals(config.SchemaVersion, SupportedSchemaVersion, StringComparison.Ordinal))
            reasons.Add("SCENARIO_SCHEMA_VERSION_UNSUPPORTED");
        if (!string.Equals(config.PolicyVersion, SupportedPolicyVersion, StringComparison.Ordinal))
            reasons.Add("SCENARIO_POLICY_VERSION_UNSUPPORTED");
        if (string.IsNullOrWhiteSpace(unityCompatibilityVersion)
            || !string.Equals(config.UnityCompatibilityVersion, unityCompatibilityVersion, StringComparison.Ordinal))
            reasons.Add("SCENARIO_UNITY_VERSION_MISMATCH");
        if (HasMissingText(config)) reasons.Add("SCENARIO_REQUIRED_FIELD_MISSING");
        if (config.SupportItemBudget < 0
            || !IsFiniteNonNegative(config.DetectionFillRate)
            || !IsFiniteNonNegative(config.DetectionDecayRate)
            || !IsFiniteNonNegative(config.ChaseSpeed)
            || !IsFiniteNonNegative(config.SearchDuration)
            || !double.IsFinite(config.EscapeDoorTimerSeconds)
            || config.EscapeDoorTimerSeconds < MinEscapeDoorTimerSeconds
            || config.EscapeDoorTimerSeconds > MaxEscapeDoorTimerSeconds)
            reasons.Add("SCENARIO_VALUE_OUT_OF_RANGE");
        if (config.IsFixedFallback
            && (config.ConfigSource != ScenarioConfigSource.Fixed
                || !string.Equals(config.FallbackConfigId, config.ScenarioConfigId, StringComparison.Ordinal)
                || !string.Equals(config.FallbackConfigVersion, config.ScenarioConfigVersion, StringComparison.Ordinal)))
            reasons.Add("SCENARIO_FIXED_FALLBACK_IDENTITY_INVALID");

        ValidateContent(config, content, ScenarioContentType.Map, config.MapId, reasons);
        ValidateContent(config, content, ScenarioContentType.Monster, config.MonsterType, reasons);
        ValidateContent(config, content, ScenarioContentType.ObjectiveSpawnSet, config.ObjectiveSpawnSetId, reasons);
        ValidateContent(config, content, ScenarioContentType.RouteModifier, config.RouteModifier, reasons);
        return new(reasons.Count == 0, reasons.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static void ValidateContent(
        ScenarioConfigDefinition config,
        IReadOnlyCollection<ScenarioContentDefinition> content,
        ScenarioContentType type,
        string id,
        ICollection<string> reasons)
    {
        var match = content.FirstOrDefault(item =>
            item.ContentType == type
            && string.Equals(item.ContentId, id, StringComparison.Ordinal)
            && string.Equals(item.ContentWhitelistVersion, config.ContentWhitelistVersion, StringComparison.Ordinal)
            && string.Equals(item.UnityCompatibilityVersion, config.UnityCompatibilityVersion, StringComparison.Ordinal));
        if (match is null) reasons.Add("SCENARIO_CONTENT_UNKNOWN");
        else if (!match.IsActive || !match.IsProductionApproved) reasons.Add("SCENARIO_CONTENT_DISABLED_OR_UNAPPROVED");
    }

    private static bool HasMissingText(ScenarioConfigDefinition config) =>
        string.IsNullOrWhiteSpace(config.ScenarioConfigId)
        || string.IsNullOrWhiteSpace(config.ScenarioConfigVersion)
        || string.IsNullOrWhiteSpace(config.SchemaVersion)
        || string.IsNullOrWhiteSpace(config.PolicyVersion)
        || string.IsNullOrWhiteSpace(config.MapId)
        || string.IsNullOrWhiteSpace(config.MonsterType)
        || string.IsNullOrWhiteSpace(config.ObjectiveSpawnSetId)
        || string.IsNullOrWhiteSpace(config.RouteModifier)
        || string.IsNullOrWhiteSpace(config.FallbackConfigId)
        || string.IsNullOrWhiteSpace(config.FallbackConfigVersion)
        || string.IsNullOrWhiteSpace(config.ContentWhitelistVersion)
        || string.IsNullOrWhiteSpace(config.UnityCompatibilityVersion)
        || string.IsNullOrWhiteSpace(config.Provenance);

    private static bool IsFiniteNonNegative(double value) => double.IsFinite(value) && value >= 0d;
}
