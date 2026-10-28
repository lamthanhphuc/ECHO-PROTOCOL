using EchoProtocol.Api.Data;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using EchoProtocol.Api.Services.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace EchoProtocol.Api.Services;

/// <summary>Reconciles provider-confirmed payments and terminal unpaid orders.</summary>
public sealed class PaymentOrderReconciliationService(
    AppDbContext db, IPaymentProviderRegistry providers,
    IPaymentWebhookService webhook, TimeProvider timeProvider)
{
    public Task<List<Guid>> DueAsync(CancellationToken ct, int offset = 0)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return db.PaymentOrders.AsNoTracking()
            .Where(order => order.Status == PaymentOrderStatus.PENDING_PAYMENT ||
                order.Status == PaymentOrderStatus.CREATED && order.ExpiresAtUtc <= now)
            .OrderBy(order => order.UpdatedAtUtc).ThenBy(order => order.PaymentOrderId)
            .Select(order => order.PaymentOrderId).Skip(Math.Max(0, offset)).Take(30).ToListAsync(ct);
    }

    public async Task<PaymentOrderStatus?> ReconcileAsync(Guid orderId, CancellationToken ct)
    {
        var order = await db.PaymentOrders.AsNoTracking().SingleOrDefaultAsync(
            item => item.PaymentOrderId == orderId, ct);
        if (order is null || order.Status is not (PaymentOrderStatus.CREATED or PaymentOrderStatus.PENDING_PAYMENT))
            return null;

        var expired = order.ExpiresAtUtc is { } expiry && expiry <= timeProvider.GetUtcNow().UtcDateTime;
        if (order.ProviderOrderId is null)
            return expired ? await ApplyTerminalAsync(orderId, PaymentOrderStatus.EXPIRED, ct) : null;

        var provider = providers.Resolve(order.Provider);
        if (provider is null || !provider.IsConfigured) return null;
        var payment = await provider.QueryPaymentAsync(order.ProviderOrderId, ct);
        if (payment is null)
            return expired ? await ApplyTerminalAsync(orderId, PaymentOrderStatus.EXPIRED, ct) : null;
        if (payment.ProviderOrderId != order.ProviderOrderId || payment.Amount != order.Amount ||
            !string.Equals(payment.Currency, order.Currency, StringComparison.OrdinalIgnoreCase))
            return null;

        if (string.Equals(payment.Status, "PAID", StringComparison.OrdinalIgnoreCase))
        {
            // Provider query is authenticated; process its evidence through the same
            // idempotent transition and fulfillment path as a verified webhook.
            var identity = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes($"{order.Provider}|{order.ProviderOrderId}")));
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                $"{order.Provider}|{order.ProviderOrderId}|{payment.Amount}|{payment.Currency}|PAID")));
            await webhook.ProcessVerifiedAsync(new NormalizedPaymentProviderEvent(
                order.Provider, $"reconcile:{identity}", payment.ProviderOrderId,
                payment.Amount, payment.Currency, PaymentProviderEventStatus.PAID,
                null, fingerprint), ct);
            var updated = await db.PaymentOrders.AsNoTracking().SingleAsync(
                item => item.PaymentOrderId == orderId, ct);
            return updated.Status != PaymentOrderStatus.PENDING_PAYMENT ? updated.Status : null;
        }

        var terminal = PaymentProviderStatusRules.TerminalOrderStatus(payment.Status);
        return terminal is { } target ? await ApplyTerminalAsync(orderId, target, ct) : null;
    }

    private async Task<PaymentOrderStatus?> ApplyTerminalAsync(
        Guid orderId, PaymentOrderStatus target, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        PaymentOrder? order;
        if (db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
            order = await db.PaymentOrders.FromSqlInterpolated(
                $"SELECT * FROM \"PaymentOrders\" WHERE \"PaymentOrderId\" = {orderId} FOR UPDATE")
                .SingleOrDefaultAsync(ct);
        else
            order = await db.PaymentOrders.SingleOrDefaultAsync(item => item.PaymentOrderId == orderId, ct);
        if (order is null || !PaymentOrderStateMachine.CanTransition(order.Status, target))
            return null;

        order.Status = target;
        order.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return target;
    }
}
