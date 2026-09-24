using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.Entities;

/// <summary>Versioned designer-authored ScenarioConfig registry entry.</summary>
public sealed class ScenarioConfigDefinition
{
    public string ScenarioConfigId { get; set; } = string.Empty;
    public string ScenarioConfigVersion { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = string.Empty;
    public string PolicyVersion { get; set; } = string.Empty;
    public ScenarioConfigSource ConfigSource { get; set; }
    public string MapId { get; set; } = string.Empty;
    public string MonsterType { get; set; } = string.Empty;
    public string ObjectiveSpawnSetId { get; set; } = string.Empty;
    public int SupportItemBudget { get; set; }
    public double DetectionFillRate { get; set; }
    public double DetectionDecayRate { get; set; }
    public double ChaseSpeed { get; set; }
    public double SearchDuration { get; set; }
    public string RouteModifier { get; set; } = string.Empty;
    public double EscapeDoorTimerSeconds { get; set; }
    public string FallbackConfigId { get; set; } = string.Empty;
    public string FallbackConfigVersion { get; set; } = string.Empty;
    public string ContentWhitelistVersion { get; set; } = string.Empty;
    public string UnityCompatibilityVersion { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsProductionApproved { get; set; }
    public bool IsFixedFallback { get; set; }
    public string Provenance { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
