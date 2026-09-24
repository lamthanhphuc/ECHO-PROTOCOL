using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.DTOs.Payments;

public sealed record PaymentOrderResponse(
    Guid PaymentOrderId,
    string Provider,
    string? ProviderOrderId,
    string Purpose,
    string ProductReference,
    decimal Amount,
    string Currency,
    PaymentOrderStatus Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? ExpiresAtUtc,
    DateTime? PaidAtUtc,
    DateTime? FulfilledAtUtc,
    bool IsReplay)
{
    public static PaymentOrderResponse From(PaymentOrder order, bool isReplay) => new(
        order.PaymentOrderId,
        order.Provider,
        order.ProviderOrderId,
        order.Purpose,
        order.ProductReference,
        order.Amount,
        order.Currency,
        order.Status,
        order.CreatedAtUtc,
        order.UpdatedAtUtc,
        order.ExpiresAtUtc,
        order.PaidAtUtc,
        order.FulfilledAtUtc,
        isReplay);
}
