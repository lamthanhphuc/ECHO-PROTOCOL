using System.Text.Json;
using System.Text.Json.Serialization;

namespace EchoProtocol.Api.DTOs.Payments;

public sealed class CreatePaymentOrderRequest
{
    public string ProductReference { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
