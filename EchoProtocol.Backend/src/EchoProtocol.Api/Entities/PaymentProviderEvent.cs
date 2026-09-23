using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.Entities;

public sealed class PaymentProviderEvent
{
    public Guid PaymentProviderEventId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderEventId { get; set; } = string.Empty;
    public Guid? PaymentOrderId { get; set; }
    public string ProviderOrderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public PaymentProviderEventStatus NormalizedStatus { get; set; }
    public string SemanticFingerprint { get; set; } = string.Empty;
    public string VerificationStatus { get; set; } = "VERIFIED";
    public DateTime ReceivedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public PaymentProviderEventOutcome ProcessingOutcome { get; set; }

    public PaymentOrder? PaymentOrder { get; set; }
}
