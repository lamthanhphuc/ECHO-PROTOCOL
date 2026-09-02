using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EchoProtocol.Api.DTOs.Telemetry;

public sealed class TelemetryBatchRequest
{
    [Required]
    [MinLength(1)]
    public List<TelemetryEventRequest> Events { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class TelemetryEventRequest
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public Guid? UserId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public JsonElement Ts { get; set; }

    public JsonElement ValueJson { get; set; }

    public string? ReasonCode { get; set; }

    public string SchemaVersion { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
