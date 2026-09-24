using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.DTOs.Inventory;

public sealed class InventoryResponse
{
    public IReadOnlyList<InventoryItemResponse> Items { get; init; } = [];
}

public sealed class InventoryItemResponse
{
    public Guid InventoryItemId { get; init; }
    public Guid ItemId { get; init; }
    public string ItemName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string AssetReference { get; init; } = string.Empty;
    public InventoryAcquisitionSource Source { get; init; }
    public Guid? PurchaseId { get; init; }
    public DateTime AcquiredAtUtc { get; init; }
}
