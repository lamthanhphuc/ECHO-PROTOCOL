using System.Text.Json;
using System.Text.Json.Serialization;

namespace EchoProtocol.Api.DTOs.Shop;

public sealed class PurchaseRequest
{
    public Guid ItemId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
