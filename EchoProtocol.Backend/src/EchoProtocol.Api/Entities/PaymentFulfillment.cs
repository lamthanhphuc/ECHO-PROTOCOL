using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.Entities;

public sealed class PaymentFulfillment
{
    public Guid PaymentOrderId { get; set; }
    public string FulfillmentReference { get; set; } = string.Empty;
    public PaymentFulfillmentKind Kind { get; set; }
    public Guid? WalletTransactionId { get; set; }
    public Guid? InventoryItemId { get; set; }
    public DateTime CompletedAtUtc { get; set; }

    public PaymentOrder PaymentOrder { get; set; } = null!;
    public WalletTransaction? WalletTransaction { get; set; }
    public InventoryItem? InventoryItem { get; set; }
}
