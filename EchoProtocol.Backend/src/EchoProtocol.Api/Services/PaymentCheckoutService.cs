using System.Globalization;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Payments;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using EchoProtocol.Api.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Services;

public sealed class PaymentCheckoutService(
    AppDbContext db,
    IPaymentProviderRegistry providerRegistry,
    TimeProvider timeProvider) : IPaymentCheckoutService
{
    public async Task<ServiceResult<PaymentCheckoutResponse>> CreateCheckoutAsync(
        Guid userId,
        Guid paymentOrderId,
        CancellationToken cancellationToken = default)
    {
        var reservation = await ReserveAsync(userId, paymentOrderId, cancellationToken);
        if (!reservation.IsSuccess) return reservation.Error!;
        if (reservation.ReadyResponse is not null)
            return ServiceResult<PaymentCheckoutResponse>.Success(
                reservation.ReadyResponse with { IsReplay = true }, "Checkout already exists");

        var provider = providerRegistry.Resolve(reservation.Order!.Provider);
        if (provider is null || !provider.IsConfigured)
            return Fail("Payment provider is unavailable or not configured",
                ErrorCodes.PaymentProviderConfigurationInvalid);

        PaymentProviderCheckoutResult? providerResult;
        try
        {
            providerResult = reservation.IsNew
                ? await provider.CreateCheckoutAsync(ToProviderRequest(reservation), cancellationToken)
                : await provider.QueryPaymentAsync(
                    reservation.Checkout!.ProviderOrderId, cancellationToken);
            if (providerResult is null)
                providerResult = await provider.CreateCheckoutAsync(
                    ToProviderRequest(reservation), cancellationToken);
        }
        catch (PaymentProviderException)
        {
            try
            {
                providerResult = await provider.QueryPaymentAsync(
                    reservation.Checkout!.ProviderOrderId, cancellationToken);
            }
            catch (PaymentProviderException)
            {
                providerResult = null;
            }

            if (providerResult is null)
                return Fail("Payment provider checkout request failed", ErrorCodes.PaymentProviderUnavailable);
        }

        if (providerResult is null)
            return Fail("Checkout reservation is awaiting provider recovery",
                ErrorCodes.PaymentCheckoutRecoveryPending);
        if (!MatchesProviderEvidence(reservation, providerResult))
            return Fail("Payment provider checkout response does not match the order",
                ErrorCodes.PaymentProviderEvidenceMismatch);

        return await FinalizeAsync(
            userId, paymentOrderId, providerResult, cancellationToken);
    }

    private async Task<ReservationResult> ReserveAsync(
        Guid userId,
        Guid paymentOrderId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var order = await LoadOrderForUpdateAsync(paymentOrderId, cancellationToken);
        if (order is null)
            return ReservationResult.Fail(Fail("Payment order not found", ErrorCodes.PaymentOrderNotFound));
        if (order.UserId != userId)
            return ReservationResult.Fail(Fail("Payment order does not belong to the authenticated user",
                ErrorCodes.PaymentOrderForbidden));

        var checkout = await db.PaymentCheckouts.SingleOrDefaultAsync(
            item => item.PaymentOrderId == paymentOrderId, cancellationToken);
        if (order.Status == PaymentOrderStatus.PENDING_PAYMENT && checkout?.Status == PaymentCheckoutStatus.READY)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ReservationResult.Ready(Map(order, checkout, true));
        }
        if (order.Status != PaymentOrderStatus.CREATED)
            return ReservationResult.Fail(Fail("Only a CREATED payment order can start checkout",
                ErrorCodes.PaymentInvalidStateTransition));

        if (order.Amount != decimal.Truncate(order.Amount) || order.Amount is <= 0 or > long.MaxValue)
            return ReservationResult.Fail(Fail("payOS requires a positive integer amount",
                ErrorCodes.PaymentProviderEvidenceMismatch));
        if (!string.Equals(order.Currency, "VND", StringComparison.Ordinal))
            return ReservationResult.Fail(Fail("payOS checkout currently requires VND",
                ErrorCodes.PaymentProviderEvidenceMismatch));

        var isNew = checkout is null;
        if (checkout is null)
        {
            checkout = new PaymentCheckout
            {
                PaymentOrderId = order.PaymentOrderId,
                Provider = order.Provider,
                ProviderOrderId = string.Empty,
                Status = PaymentCheckoutStatus.RESERVED,
                ReservedAtUtc = timeProvider.GetUtcNow().UtcDateTime
            };
            db.PaymentCheckouts.Add(checkout);
            await db.SaveChangesAsync(cancellationToken);
            checkout.ProviderOrderId = checkout.CheckoutSequenceId.ToString(CultureInfo.InvariantCulture);
            order.ProviderOrderId = checkout.ProviderOrderId;
            order.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return ReservationResult.Pending(order, checkout, isNew);
    }

    private async Task<ServiceResult<PaymentCheckoutResponse>> FinalizeAsync(
        Guid userId,
        Guid paymentOrderId,
        PaymentProviderCheckoutResult result,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var order = await LoadOrderForUpdateAsync(paymentOrderId, cancellationToken);
        var checkout = await db.PaymentCheckouts.SingleAsync(
            item => item.PaymentOrderId == paymentOrderId, cancellationToken);
        if (order is null || order.UserId != userId)
            return Fail("Payment order is no longer available to the authenticated user",
                ErrorCodes.PaymentOrderForbidden);
        if (checkout.Status == PaymentCheckoutStatus.READY && order.Status == PaymentOrderStatus.PENDING_PAYMENT)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<PaymentCheckoutResponse>.Success(Map(order, checkout, true));
        }
        if (order.Status != PaymentOrderStatus.CREATED ||
            !PaymentOrderStateMachine.CanTransition(order.Status, PaymentOrderStatus.PENDING_PAYMENT))
            return Fail("Payment order state changed while checkout was being created",
                ErrorCodes.PaymentInvalidStateTransition);

        checkout.ProviderPaymentLinkId = result.ProviderPaymentLinkId;
        checkout.CheckoutUrl = result.CheckoutUrl.AbsoluteUri;
        checkout.Status = PaymentCheckoutStatus.READY;
        checkout.ReadyAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        order.Status = PaymentOrderStatus.PENDING_PAYMENT;
        order.UpdatedAtUtc = checkout.ReadyAtUtc.Value;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<PaymentCheckoutResponse>.Success(Map(order, checkout, false),
            "Payment checkout created");
    }

    private static PaymentProviderCheckoutRequest ToProviderRequest(ReservationResult reservation)
    {
        var code = reservation.Checkout!.CheckoutSequenceId;
        return new PaymentProviderCheckoutRequest(
            code,
            decimal.ToInt64(reservation.Order!.Amount),
            $"EP{code % 10_000_000:D7}",
            reservation.Order.ExpiresAtUtc);
    }

    private static bool MatchesProviderEvidence(
        ReservationResult reservation,
        PaymentProviderCheckoutResult result) =>
        string.Equals(result.ProviderOrderId, reservation.Checkout!.ProviderOrderId, StringComparison.Ordinal) &&
        result.Amount == decimal.ToInt64(reservation.Order!.Amount) &&
        string.Equals(result.Currency, reservation.Order.Currency, StringComparison.OrdinalIgnoreCase);

    private async Task<PaymentOrder?> LoadOrderForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        if (db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
            return await db.PaymentOrders.FromSqlInterpolated(
                $"SELECT * FROM \"PaymentOrders\" WHERE \"PaymentOrderId\" = {id} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
        return await db.PaymentOrders.SingleOrDefaultAsync(
            item => item.PaymentOrderId == id, cancellationToken);
    }

    private static PaymentCheckoutResponse Map(
        PaymentOrder order, PaymentCheckout checkout, bool replay) => new(
        order.PaymentOrderId, order.Provider, checkout.ProviderOrderId,
        checkout.CheckoutUrl!, order.ExpiresAtUtc, order.Status, replay);

    private static ServiceResult<PaymentCheckoutResponse> Fail(string message, string code) =>
        ServiceResult<PaymentCheckoutResponse>.Failure(message, code);

    private sealed record ReservationResult(
        PaymentOrder? Order,
        PaymentCheckout? Checkout,
        bool IsNew,
        PaymentCheckoutResponse? ReadyResponse,
        ServiceResult<PaymentCheckoutResponse>? Error)
    {
        public bool IsSuccess => Error is null;
        public static ReservationResult Pending(PaymentOrder order, PaymentCheckout checkout, bool isNew) =>
            new(order, checkout, isNew, null, null);
        public static ReservationResult Ready(PaymentCheckoutResponse response) =>
            new(null, null, false, response, null);
        public static ReservationResult Fail(ServiceResult<PaymentCheckoutResponse> error) =>
            new(null, null, false, null, error);
    }
}
