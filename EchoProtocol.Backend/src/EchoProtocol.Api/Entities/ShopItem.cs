namespace EchoProtocol.Api.Entities;

public sealed class ShopItem
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Price { get; set; }
    public string AssetReference { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<InventoryItem> InventoryItems { get; set; } = [];
    public ICollection<PurchaseTransaction> PurchaseTransactions { get; set; } = [];
}
