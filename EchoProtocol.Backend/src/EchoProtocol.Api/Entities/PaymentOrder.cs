using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.Entities;

public sealed class PaymentOrder
{
    public Guid PaymentOrderId { get; set; }
    public Guid UserId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? ProviderOrderId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string ProductReference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public PaymentOrderStatus Status { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestFingerprint { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public DateTime? FulfilledAtUtc { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? FulfillmentReference { get; set; }

    public User User { get; set; } = null!;
    public PaymentCheckout? Checkout { get; set; }
    public PaymentFulfillment? Fulfillment { get; set; }
    public ICollection<PaymentProviderEvent> ProviderEvents { get; set; } = [];
}
