using System.Data;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Payments;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using EchoProtocol.Api.Services.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StoredPaymentProviderEvent = EchoProtocol.Api.Entities.PaymentProviderEvent;

namespace EchoProtocol.Api.Services;

public sealed class PaymentWebhookService(
    AppDbContext db,
    IPaymentFulfillmentService fulfillmentService,
    TimeProvider timeProvider) : IPaymentWebhookService
{
    public async Task<ServiceResult<PaymentWebhookResponse>> ProcessVerifiedAsync(
        NormalizedPaymentProviderEvent paymentEvent,
        CancellationToken cancellationToken = default)
    {
        var existing = await LoadEventAsync(
            paymentEvent.Provider, paymentEvent.ProviderEventId, cancellationToken);
        if (existing is not null)
            return await HandleReplayAsync(existing, paymentEvent, cancellationToken);

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var checkout = await db.PaymentCheckouts.AsNoTracking().SingleOrDefaultAsync(
                item => item.Provider == paymentEvent.Provider &&
                        item.ProviderOrderId == paymentEvent.ProviderOrderId,
                cancellationToken);
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var storedEvent = new StoredPaymentProviderEvent
            {
                PaymentProviderEventId = Guid.NewGuid(),
                Provider = paymentEvent.Provider,
                ProviderEventId = paymentEvent.ProviderEventId,
                PaymentOrderId = checkout?.PaymentOrderId,
                ProviderOrderId = paymentEvent.ProviderOrderId,
                Amount = paymentEvent.Amount,
                Currency = paymentEvent.Currency,
                NormalizedStatus = paymentEvent.Status,
                SemanticFingerprint = paymentEvent.SemanticFingerprint,
                VerificationStatus = "VERIFIED",
                ReceivedAtUtc = now,
                ProcessingOutcome = PaymentProviderEventOutcome.RECEIVED
            };
            db.PaymentProviderEvents.Add(storedEvent);

            if (checkout is null)
            {
                storedEvent.ProcessingOutcome = PaymentProviderEventOutcome.UNKNOWN_ORDER;
                storedEvent.ProcessedAtUtc = now;
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Fail("Verified webhook references an unknown provider order",
                    ErrorCodes.PaymentWebhookUnknownOrder);
            }

            var order = await LoadOrderForUpdateAsync(checkout.PaymentOrderId, cancellationToken);
            if (order is null || !EvidenceMatches(order, paymentEvent))
            {
                storedEvent.ProcessingOutcome = PaymentProviderEventOutcome.EVIDENCE_MISMATCH;
                storedEvent.ProcessedAtUtc = now;
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Fail("Verified payment evidence does not match the authoritative order",
                    ErrorCodes.PaymentProviderEvidenceMismatch);
            }

            var transactionIdentityUsed = await db.PaymentOrders.AsNoTracking().AnyAsync(
                item => item.PaymentOrderId != order.PaymentOrderId &&
                        item.Provider == paymentEvent.Provider &&
                        item.ProviderTransactionId == paymentEvent.ProviderEventId,
                cancellationToken);
            if (transactionIdentityUsed)
            {
                storedEvent.ProcessingOutcome = PaymentProviderEventOutcome.EVIDENCE_MISMATCH;
                storedEvent.ProcessedAtUtc = now;
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Fail("Provider transaction identity is already assigned to another order",
                    ErrorCodes.PaymentProviderEvidenceMismatch);
            }

            if (paymentEvent.Status != PaymentProviderEventStatus.PAID)
            {
                storedEvent.ProcessingOutcome = PaymentProviderEventOutcome.STATUS_IGNORED;
                storedEvent.ProcessedAtUtc = now;
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Success(storedEvent, false);
            }

            if (order.Status == PaymentOrderStatus.CREATED &&
                checkout.Status == PaymentCheckoutStatus.RESERVED)
            {
                storedEvent.ProcessingOutcome = PaymentProviderEventOutcome.RECEIVED;
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Fail(
                    "Verified payment arrived before checkout finalization; provider retry is required",
                    ErrorCodes.PaymentCheckoutRecoveryPending);
            }

            if (order.Status != PaymentOrderStatus.PENDING_PAYMENT)
            {
                storedEvent.ProcessingOutcome = PaymentProviderEventOutcome.LATE_TERMINAL_IGNORED;
                storedEvent.ProcessedAtUtc = now;
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Success(storedEvent, false);
            }

            order.Status = PaymentOrderStatus.PAID;
            order.ProviderTransactionId = paymentEvent.ProviderEventId;
            order.PaidAtUtc = paymentEvent.PaidAtUtc ?? now;
            order.UpdatedAtUtc = now;
            storedEvent.ProcessingOutcome = PaymentProviderEventOutcome.PAID;
            storedEvent.ProcessedAtUtc = now;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await transaction.DisposeAsync();

            return await AttemptFulfillmentAsync(storedEvent, false, cancellationToken);
        }
        catch (Exception exception) when (IsRetryableConcurrency(exception))
        {
            db.ChangeTracker.Clear();
            existing = await LoadEventAsync(
                paymentEvent.Provider, paymentEvent.ProviderEventId, cancellationToken);
            return existing is not null
                ? await HandleReplayAsync(existing, paymentEvent, cancellationToken)
                : Fail("Concurrent payment webhook could not be committed",
                    ErrorCodes.PaymentWebhookConflict);
        }
    }

    private async Task<ServiceResult<PaymentWebhookResponse>> HandleReplayAsync(
        StoredPaymentProviderEvent existing,
        NormalizedPaymentProviderEvent paymentEvent,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(existing.SemanticFingerprint, paymentEvent.SemanticFingerprint,
                StringComparison.OrdinalIgnoreCase))
            return Fail("Provider event identity was reused with conflicting evidence",
                ErrorCodes.PaymentWebhookConflict);
        if (existing.PaymentOrderId is not null)
        {
            var order = await db.PaymentOrders.AsNoTracking().SingleOrDefaultAsync(
                item => item.PaymentOrderId == existing.PaymentOrderId, cancellationToken);
            if (order?.Status == PaymentOrderStatus.PENDING_PAYMENT &&
                paymentEvent.Status == PaymentProviderEventStatus.PAID)
                return await ApplyDeferredPaidEventAsync(
                    existing, paymentEvent, cancellationToken);
            if (order?.Status == PaymentOrderStatus.PAID)
                return await AttemptFulfillmentAsync(existing, true, cancellationToken);
        }
        return Success(existing, true);
    }

    private async Task<ServiceResult<PaymentWebhookResponse>> ApplyDeferredPaidEventAsync(
        StoredPaymentProviderEvent existing,
        NormalizedPaymentProviderEvent paymentEvent,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var order = await LoadOrderForUpdateAsync(existing.PaymentOrderId!.Value, cancellationToken);
        var storedEvent = await db.PaymentProviderEvents.SingleAsync(
            item => item.PaymentProviderEventId == existing.PaymentProviderEventId,
            cancellationToken);
        if (order?.Status == PaymentOrderStatus.PAID)
        {
            await transaction.RollbackAsync(cancellationToken);
            await transaction.DisposeAsync();
            return await AttemptFulfillmentAsync(storedEvent, true, cancellationToken);
        }
        if (order?.Status != PaymentOrderStatus.PENDING_PAYMENT ||
            !EvidenceMatches(order, paymentEvent))
            return Success(storedEvent, true);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        order.Status = PaymentOrderStatus.PAID;
        order.ProviderTransactionId = paymentEvent.ProviderEventId;
        order.PaidAtUtc = paymentEvent.PaidAtUtc ?? now;
        order.UpdatedAtUtc = now;
        storedEvent.ProcessingOutcome = PaymentProviderEventOutcome.PAID;
        storedEvent.ProcessedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await transaction.DisposeAsync();
        return await AttemptFulfillmentAsync(storedEvent, true, cancellationToken);
    }

    private async Task<ServiceResult<PaymentWebhookResponse>> AttemptFulfillmentAsync(
        StoredPaymentProviderEvent storedEvent,
        bool replay,
        CancellationToken cancellationToken)
    {
        if (storedEvent.PaymentOrderId is null)
            return Success(storedEvent, replay);
        ServiceResult<PaymentFulfillmentResponse> fulfillment;
        try
        {
            fulfillment = await fulfillmentService.FulfillAsync(
                storedEvent.PaymentOrderId.Value, cancellationToken);
        }
        catch
        {
            db.ChangeTracker.Clear();
            var recoverableEvent = await db.PaymentProviderEvents.SingleAsync(
                item => item.PaymentProviderEventId == storedEvent.PaymentProviderEventId,
                cancellationToken);
            recoverableEvent.ProcessingOutcome = PaymentProviderEventOutcome.FULFILLMENT_PENDING;
            recoverableEvent.ProcessedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }
        var trackedEvent = db.PaymentProviderEvents.Local.FirstOrDefault(
            item => item.PaymentProviderEventId == storedEvent.PaymentProviderEventId)
            ?? await db.PaymentProviderEvents.SingleAsync(
                item => item.PaymentProviderEventId == storedEvent.PaymentProviderEventId,
                cancellationToken);
        trackedEvent.ProcessingOutcome = fulfillment.IsSuccess
            ? PaymentProviderEventOutcome.FULFILLED
            : PaymentProviderEventOutcome.FULFILLMENT_PENDING;
        trackedEvent.ProcessedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        return Success(trackedEvent, replay);
    }

    private Task<StoredPaymentProviderEvent?> LoadEventAsync(
        string provider,
        string providerEventId,
        CancellationToken cancellationToken) =>
        db.PaymentProviderEvents.AsNoTracking().SingleOrDefaultAsync(
            item => item.Provider == provider && item.ProviderEventId == providerEventId,
            cancellationToken);

    private async Task<PaymentOrder?> LoadOrderForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        if (db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
            return await db.PaymentOrders.FromSqlInterpolated(
                $"SELECT * FROM \"PaymentOrders\" WHERE \"PaymentOrderId\" = {id} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
        return await db.PaymentOrders.SingleOrDefaultAsync(
            item => item.PaymentOrderId == id, cancellationToken);
    }

    private static bool EvidenceMatches(
        PaymentOrder order,
        NormalizedPaymentProviderEvent paymentEvent) =>
        string.Equals(order.Provider, paymentEvent.Provider, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(order.ProviderOrderId, paymentEvent.ProviderOrderId, StringComparison.Ordinal) &&
        order.Amount == paymentEvent.Amount &&
        (paymentEvent.Currency is null ||
         string.Equals(order.Currency, paymentEvent.Currency, StringComparison.OrdinalIgnoreCase));

    private static bool IsRetryableConcurrency(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres &&
                postgres.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure)
                return true;
        }
        return false;
    }

    private static ServiceResult<PaymentWebhookResponse> Success(
        StoredPaymentProviderEvent paymentEvent, bool replay) =>
        ServiceResult<PaymentWebhookResponse>.Success(new PaymentWebhookResponse(
            paymentEvent.PaymentOrderId,
            paymentEvent.ProviderEventId,
            paymentEvent.ProcessingOutcome.ToString(),
            replay));

    private static ServiceResult<PaymentWebhookResponse> Fail(string message, string code) =>
        ServiceResult<PaymentWebhookResponse>.Failure(message, code);
}
