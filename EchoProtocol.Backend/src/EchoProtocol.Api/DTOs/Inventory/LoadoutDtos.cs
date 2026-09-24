using System.Text.Json;
using System.Text.Json.Serialization;

namespace EchoProtocol.Api.DTOs.Inventory;

public sealed class EquipLoadoutRequest
{
    public string SlotId { get; set; } = string.Empty;
    public Guid ItemId { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class UnequipLoadoutRequest
{
    public string SlotId { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class PlayerLoadoutResponse
{
    public LoadoutItemResponse? Character { get; init; }
    public IReadOnlyList<LoadoutItemResponse> TeamTools { get; init; } = [];
}

public sealed class LoadoutItemResponse
{
    public string SlotId { get; init; } = string.Empty;
    public Guid ItemId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string AssetReference { get; init; } = string.Empty;
    public DateTime EquippedAtUtc { get; init; }
}

public sealed class UnequipLoadoutResponse
{
    public string SlotId { get; init; } = string.Empty;
    public bool WasEquipped { get; init; }
}
