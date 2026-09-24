using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.Services.Models;

public sealed record PaymentOrderTransition(
    PaymentOrderStatus TargetStatus,
    string? ProviderOrderId = null,
    string? ProviderTransactionId = null,
    string? FulfillmentReference = null);
