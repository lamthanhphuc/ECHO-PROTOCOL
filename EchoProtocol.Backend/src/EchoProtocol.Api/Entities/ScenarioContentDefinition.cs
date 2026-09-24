using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.Entities;

public sealed class ScenarioContentDefinition
{
    public Guid Id { get; set; }
    public ScenarioContentType ContentType { get; set; }
    public string ContentId { get; set; } = string.Empty;
    public string ContentWhitelistVersion { get; set; } = string.Empty;
    public string UnityCompatibilityVersion { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsProductionApproved { get; set; }
    public string Provenance { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
