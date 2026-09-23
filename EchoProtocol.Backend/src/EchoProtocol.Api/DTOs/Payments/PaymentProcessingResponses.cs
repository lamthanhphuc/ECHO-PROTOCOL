using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.DTOs.Payments;

public sealed record PaymentCheckoutResponse(
    Guid PaymentOrderId,
    string Provider,
    string ProviderOrderId,
    string CheckoutUrl,
    DateTime? ExpiresAtUtc,
    PaymentOrderStatus Status,
    bool IsReplay);

public sealed record PaymentWebhookResponse(
    Guid? PaymentOrderId,
    string ProviderEventId,
    string Outcome,
    bool IsReplay);

public sealed record PaymentFulfillmentResponse(
    Guid PaymentOrderId,
    string FulfillmentReference,
    PaymentFulfillmentKind Kind,
    PaymentOrderStatus Status,
    bool IsReplay);
