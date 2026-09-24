using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Payments;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EchoProtocol.Api.Services;

public sealed class PaymentFulfillmentService(
    AppDbContext db,
    IOptions<PaymentCatalogSettings> catalogOptions,
    TimeProvider timeProvider) : IPaymentFulfillmentService
{
    private readonly PaymentCatalogSettings _catalog = catalogOptions.Value;

    public async Task<ServiceResult<PaymentFulfillmentResponse>> FulfillAsync(
        Guid paymentOrderId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var order = await LoadOrderForUpdateAsync(paymentOrderId, cancellationToken);
        if (order is null)
            return Fail("Payment order not found", ErrorCodes.PaymentOrderNotFound);

        var existing = await db.PaymentFulfillments.AsNoTracking()
            .SingleOrDefaultAsync(item => item.PaymentOrderId == paymentOrderId, cancellationToken);
        if (existing is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Success(order, existing, true);
        }
        if (order.Status != PaymentOrderStatus.PAID)
            return Fail("Only a PAID payment order can be fulfilled",
                ErrorCodes.PaymentInvalidStateTransition);

        var product = ResolveProduct(order);
        if (!product.IsSuccess)
            return Fail(product.Message, product.ErrorCode!);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var reference = $"PAYMENT:{order.PaymentOrderId:N}";
        var fulfillment = new PaymentFulfillment
        {
            PaymentOrderId = order.PaymentOrderId,
            FulfillmentReference = reference,
            Kind = product.Kind,
            CompletedAtUtc = now
        };

        if (product.Kind == PaymentFulfillmentKind.WALLET_CREDIT)
        {
            var wallet = await LoadWalletForUpdateAsync(order.UserId, cancellationToken);
            if (wallet is null)
                return Fail("Wallet required by fulfillment was not found",
                    ErrorCodes.PaymentFulfillmentTargetInvalid);
            var amount = product.Product!.WalletCreditAmount!.Value;
            var balanceAfter = checked(wallet.Balance + amount);
            var walletTransaction = new WalletTransaction
            {
                Id = Guid.NewGuid(),
                WalletId = wallet.Id,
                Type = WalletTransactionType.PAYMENT_FULFILLMENT,
                Amount = amount,
                BalanceBefore = wallet.Balance,
                BalanceAfter = balanceAfter,
                ReferenceId = order.PaymentOrderId,
                Description = $"Payment fulfillment {order.PaymentOrderId:D}",
                CreatedAtUtc = now
            };
            db.WalletTransactions.Add(walletTransaction);
            wallet.Balance = balanceAfter;
            wallet.UpdatedAt = now;
            fulfillment.WalletTransactionId = walletTransaction.Id;
        }
        else
        {
            var shopItemId = product.Product!.ShopItemId!.Value;
            var itemExists = await db.ShopItems.AsNoTracking()
                .AnyAsync(item => item.ItemId == shopItemId, cancellationToken);
            if (!itemExists)
                return Fail("Inventory fulfillment item does not exist",
                    ErrorCodes.PaymentFulfillmentTargetInvalid);
            var inventoryItem = new InventoryItem
            {
                InventoryItemId = Guid.NewGuid(),
                UserId = order.UserId,
                ShopItemId = shopItemId,
                Source = InventoryAcquisitionSource.PAYMENT,
                AcquiredAtUtc = now
            };
            db.InventoryItems.Add(inventoryItem);
            fulfillment.InventoryItemId = inventoryItem.InventoryItemId;
        }

        db.PaymentFulfillments.Add(fulfillment);
        order.Status = PaymentOrderStatus.FULFILLED;
        order.FulfillmentReference = reference;
        order.FulfilledAtUtc = now;
        order.UpdatedAtUtc = now;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Success(order, fulfillment, false);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            existing = await db.PaymentFulfillments.AsNoTracking()
                .SingleOrDefaultAsync(item => item.PaymentOrderId == paymentOrderId, cancellationToken);
            if (existing is null)
                return Fail("Payment fulfillment conflicted with another grant",
                    ErrorCodes.PaymentFulfillmentConflict);
            var storedOrder = await db.PaymentOrders.AsNoTracking().SingleAsync(
                item => item.PaymentOrderId == paymentOrderId, cancellationToken);
            return Success(storedOrder, existing, true);
        }
    }

    private ProductResolution ResolveProduct(PaymentOrder order)
    {
        var matches = _catalog.Products.Where(item =>
            string.Equals(item.ProductReference.Trim(), order.ProductReference, StringComparison.Ordinal) &&
            string.Equals(item.Purpose.Trim(), order.Purpose, StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1 || string.IsNullOrWhiteSpace(matches[0].FulfillmentKind))
            return ProductResolution.Fail("Payment fulfillment catalog is not configured for this product",
                ErrorCodes.PaymentFulfillmentNotConfigured);
        if (!Enum.TryParse<PaymentFulfillmentKind>(matches[0].FulfillmentKind, true, out var kind))
            return ProductResolution.Fail("Payment fulfillment kind is invalid",
                ErrorCodes.PaymentFulfillmentNotConfigured);
        if (kind == PaymentFulfillmentKind.WALLET_CREDIT && matches[0].WalletCreditAmount is not > 0)
            return ProductResolution.Fail("Wallet credit fulfillment amount is invalid",
                ErrorCodes.PaymentFulfillmentNotConfigured);
        if (kind == PaymentFulfillmentKind.INVENTORY_ITEM && matches[0].ShopItemId is null)
            return ProductResolution.Fail("Inventory fulfillment target is invalid",
                ErrorCodes.PaymentFulfillmentNotConfigured);
        return ProductResolution.Ok(matches[0], kind);
    }

    private async Task<PaymentOrder?> LoadOrderForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        if (db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
            return await db.PaymentOrders.FromSqlInterpolated(
                $"SELECT * FROM \"PaymentOrders\" WHERE \"PaymentOrderId\" = {id} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
        return await db.PaymentOrders.SingleOrDefaultAsync(
            item => item.PaymentOrderId == id, cancellationToken);
    }

    private async Task<Wallet?> LoadWalletForUpdateAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
            return await db.Wallets.FromSqlInterpolated(
                $"SELECT * FROM \"Wallets\" WHERE \"UserId\" = {userId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
        return await db.Wallets.SingleOrDefaultAsync(
            item => item.UserId == userId, cancellationToken);
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static ServiceResult<PaymentFulfillmentResponse> Success(
        PaymentOrder order, PaymentFulfillment fulfillment, bool replay) =>
        ServiceResult<PaymentFulfillmentResponse>.Success(new PaymentFulfillmentResponse(
            order.PaymentOrderId, fulfillment.FulfillmentReference,
            fulfillment.Kind, order.Status, replay),
            replay ? "Payment fulfillment already completed" : "Payment fulfillment completed");

    private static ServiceResult<PaymentFulfillmentResponse> Fail(string message, string code) =>
        ServiceResult<PaymentFulfillmentResponse>.Failure(message, code);

    private sealed record ProductResolution(
        bool IsSuccess,
        PaymentProductSettings? Product,
        PaymentFulfillmentKind Kind,
        string Message,
        string? ErrorCode)
    {
        public static ProductResolution Ok(PaymentProductSettings product, PaymentFulfillmentKind kind) =>
            new(true, product, kind, string.Empty, null);
        public static ProductResolution Fail(string message, string code) =>
            new(false, null, default, message, code);
    }
}
