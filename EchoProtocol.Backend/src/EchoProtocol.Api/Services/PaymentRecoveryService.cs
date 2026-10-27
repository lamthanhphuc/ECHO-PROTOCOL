using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Payments;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Services;

/// <summary>Retries fulfillment only for orders backed by verified, matching PAID evidence.</summary>
public sealed class PaymentRecoveryService(
    AppDbContext db, IPaymentFulfillmentService fulfillment, TimeProvider timeProvider)
{
    public async Task<ServiceResult<PaymentFulfillmentResponse>> RecoverAsync(Guid orderId, CancellationToken ct)
    {
        var order = await db.PaymentOrders.AsNoTracking().SingleOrDefaultAsync(x => x.PaymentOrderId == orderId, ct);
        if (order is null)
            return ServiceResult<PaymentFulfillmentResponse>.Failure("Payment order not found", ErrorCodes.PaymentOrderNotFound);
        if (order.Status is not (PaymentOrderStatus.PAID or PaymentOrderStatus.FULFILLED))
            return ServiceResult<PaymentFulfillmentResponse>.Failure("Only a verified PAID order can be recovered", ErrorCodes.PaymentInvalidStateTransition);

        var verified = await db.PaymentProviderEvents.AnyAsync(e => e.PaymentOrderId == orderId
            && e.VerificationStatus == "VERIFIED" && e.NormalizedStatus == PaymentProviderEventStatus.PAID
            && e.Provider == order.Provider && e.ProviderEventId == order.ProviderTransactionId
            && e.Amount == order.Amount && e.Currency == order.Currency, ct);
        if (!verified)
            return ServiceResult<PaymentFulfillmentResponse>.Failure("Matching verified payment evidence is required", ErrorCodes.PaymentProviderEvidenceMismatch);

        var result = await fulfillment.FulfillAsync(orderId, ct);
        var events = await db.PaymentProviderEvents.Where(e => e.PaymentOrderId == orderId
            && e.VerificationStatus == "VERIFIED" && e.ProviderEventId == order.ProviderTransactionId
            && e.NormalizedStatus == PaymentProviderEventStatus.PAID).ToListAsync(ct);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var evidence in events)
        {
            evidence.ProcessingOutcome = result.IsSuccess
                ? PaymentProviderEventOutcome.FULFILLED : PaymentProviderEventOutcome.FULFILLMENT_PENDING;
            evidence.ProcessedAtUtc = now;
        }
        var trackedOrder = await db.PaymentOrders.SingleAsync(x => x.PaymentOrderId == orderId, ct);
        trackedOrder.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct);
        return result;
    }

    public Task<List<Guid>> PendingAsync(CancellationToken ct) => db.PaymentOrders.AsNoTracking()
        .Where(order => order.Status == PaymentOrderStatus.PAID
            && db.PaymentProviderEvents.Any(e => e.PaymentOrderId == order.PaymentOrderId
                && e.VerificationStatus == "VERIFIED" && e.NormalizedStatus == PaymentProviderEventStatus.PAID
                && e.ProviderEventId == order.ProviderTransactionId && e.Provider == order.Provider
                && e.Amount == order.Amount && e.Currency == order.Currency))
        .OrderBy(order => order.UpdatedAtUtc).Select(order => order.PaymentOrderId).Take(20).ToListAsync(ct);
}
