using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.Entities;

public sealed class PurchaseTransaction
{
    public Guid PurchaseId { get; set; }
    public Guid UserId { get; set; }
    public Guid ShopItemId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public int PriceAtPurchase { get; set; }
    public Guid WalletTransactionId { get; set; }
    public PurchaseTransactionStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public ShopItem ShopItem { get; set; } = null!;
    public WalletTransaction WalletTransaction { get; set; } = null!;
    public InventoryItem InventoryItem { get; set; } = null!;
}
