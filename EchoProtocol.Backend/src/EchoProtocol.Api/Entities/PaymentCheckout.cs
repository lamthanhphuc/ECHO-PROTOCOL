using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.Entities;

public sealed class PaymentCheckout
{
    public long CheckoutSequenceId { get; set; }
    public Guid PaymentOrderId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderOrderId { get; set; } = string.Empty;
    public string? ProviderPaymentLinkId { get; set; }
    public string? CheckoutUrl { get; set; }
    public PaymentCheckoutStatus Status { get; set; }
    public DateTime ReservedAtUtc { get; set; }
    public DateTime? ReadyAtUtc { get; set; }

    public PaymentOrder PaymentOrder { get; set; } = null!;
}
