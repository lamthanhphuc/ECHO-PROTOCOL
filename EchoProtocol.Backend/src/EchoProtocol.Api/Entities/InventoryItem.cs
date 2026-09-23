using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.Entities;

public sealed class InventoryItem
{
    public Guid InventoryItemId { get; set; }
    public Guid UserId { get; set; }
    public Guid ShopItemId { get; set; }
    public InventoryAcquisitionSource Source { get; set; }
    public Guid? PurchaseId { get; set; }
    public DateTime AcquiredAtUtc { get; set; }

    public User User { get; set; } = null!;
    public ShopItem ShopItem { get; set; } = null!;
    public PurchaseTransaction? PurchaseTransaction { get; set; }
    public ICollection<PlayerLoadoutItem> LoadoutItems { get; set; } = [];
}
