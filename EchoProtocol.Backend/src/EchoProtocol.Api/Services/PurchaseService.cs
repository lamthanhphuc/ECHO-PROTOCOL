using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Shop;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EchoProtocol.Api.Services;

public sealed class PurchaseService : IPurchaseService
{
    private const int MaximumIdempotencyKeyLength = 100;

    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public PurchaseService(AppDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<ServiceResult<PurchaseResponse>> PurchaseAsync(
        Guid userId,
        PurchaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(request);
        if (validation is not null)
        {
            return Failure(validation, ErrorCodes.ValidationError);
        }

        var idempotencyKey = request.IdempotencyKey.Trim();
        var existing = await LoadPurchaseAsync(userId, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return ResolveReplay(existing, request.ItemId);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var item = await LoadShopItemForShareAsync(request.ItemId, cancellationToken);
            if (item is null)
            {
                return Failure("Shop item not found", ErrorCodes.ShopItemNotFound);
            }

            if (!item.IsActive)
            {
                return Failure("Shop item is not available for purchase", ErrorCodes.ShopItemInactive);
            }

            if (!ShopItemCategories.IsSupported(item.Category))
            {
                return Failure(
                    "Shop item category is not available for purchase",
                    ErrorCodes.ShopItemInactive);
            }

            var wallet = await LoadWalletForUpdateAsync(userId, cancellationToken);
            if (wallet is null)
            {
                return Failure("Wallet not found", ErrorCodes.PurchaseWalletNotFound);
            }

            existing = await LoadPurchaseAsync(userId, idempotencyKey, cancellationToken);
            if (existing is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                return ResolveReplay(existing, request.ItemId);
            }

            if (await _db.InventoryItems.AsNoTracking().AnyAsync(
                    inventory => inventory.UserId == userId &&
                                 inventory.ShopItemId == item.ItemId,
                    cancellationToken))
            {
                return Failure(
                    "Player already owns this cosmetic item",
                    ErrorCodes.ShopItemAlreadyOwned);
            }

            if (wallet.Balance < item.Price)
            {
                return Failure(
                    "Wallet balance is insufficient for this purchase",
                    ErrorCodes.InsufficientWalletBalance);
            }

            var balanceAfter = checked(wallet.Balance - item.Price);
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var purchaseId = Guid.NewGuid();
            var walletTransactionId = Guid.NewGuid();

            _db.WalletTransactions.Add(new WalletTransaction
            {
                Id = walletTransactionId,
                WalletId = wallet.Id,
                Type = WalletTransactionType.PURCHASE,
                Amount = -item.Price,
                BalanceBefore = wallet.Balance,
                BalanceAfter = balanceAfter,
                ReferenceId = purchaseId,
                Description = $"Purchase shop item {item.ItemId:D}",
                CreatedAtUtc = now
            });
            _db.PurchaseTransactions.Add(new PurchaseTransaction
            {
                PurchaseId = purchaseId,
                UserId = userId,
                ShopItemId = item.ItemId,
                IdempotencyKey = idempotencyKey,
                PriceAtPurchase = item.Price,
                WalletTransactionId = walletTransactionId,
                Status = PurchaseTransactionStatus.COMPLETED,
                CreatedAtUtc = now
            });
            _db.InventoryItems.Add(new InventoryItem
            {
                InventoryItemId = Guid.NewGuid(),
                UserId = userId,
                ShopItemId = item.ItemId,
                Source = InventoryAcquisitionSource.PURCHASE,
                PurchaseId = purchaseId,
                AcquiredAtUtc = now
            });

            wallet.Balance = balanceAfter;
            wallet.UpdatedAt = now;

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ServiceResult<PurchaseResponse>.Success(new PurchaseResponse
            {
                PurchaseId = purchaseId,
                WalletTransactionId = walletTransactionId,
                ItemId = item.ItemId,
                PricePaid = item.Price,
                WalletBalance = balanceAfter,
                PurchasedAtUtc = now,
                IsReplay = false
            }, "Purchase completed");
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            _db.ChangeTracker.Clear();

            existing = await LoadPurchaseAsync(userId, idempotencyKey, cancellationToken);
            if (existing is not null)
            {
                return ResolveReplay(existing, request.ItemId);
            }

            var ownsItem = await _db.InventoryItems.AsNoTracking().AnyAsync(
                inventory => inventory.UserId == userId &&
                             inventory.ShopItemId == request.ItemId,
                cancellationToken);
            return ownsItem
                ? Failure(
                    "Player already owns this cosmetic item",
                    ErrorCodes.ShopItemAlreadyOwned)
                : Failure(
                    "Purchase conflicted with another transaction",
                    ErrorCodes.PurchaseConflict);
        }
    }

    private async Task<ShopItem?> LoadShopItemForShareAsync(
        Guid itemId,
        CancellationToken cancellationToken)
    {
        if (_db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            return await _db.ShopItems
                .FromSqlInterpolated(
                    $"SELECT * FROM \"ShopItems\" WHERE \"ItemId\" = {itemId} FOR SHARE")
                .SingleOrDefaultAsync(cancellationToken);
        }

        return await _db.ShopItems.SingleOrDefaultAsync(
            item => item.ItemId == itemId,
            cancellationToken);
    }

    private async Task<Wallet?> LoadWalletForUpdateAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (_db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            return await _db.Wallets
                .FromSqlInterpolated(
                    $"SELECT * FROM \"Wallets\" WHERE \"UserId\" = {userId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
        }

        return await _db.Wallets.SingleOrDefaultAsync(
            wallet => wallet.UserId == userId,
            cancellationToken);
    }

    private async Task<PurchaseTransaction?> LoadPurchaseAsync(
        Guid userId,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        await _db.PurchaseTransactions.AsNoTracking()
            .Include(purchase => purchase.WalletTransaction)
            .SingleOrDefaultAsync(
                purchase => purchase.UserId == userId &&
                            purchase.IdempotencyKey == idempotencyKey,
                cancellationToken);

    private static ServiceResult<PurchaseResponse> ResolveReplay(
        PurchaseTransaction purchase,
        Guid requestedItemId)
    {
        if (purchase.ShopItemId != requestedItemId)
        {
            return Failure(
                "Idempotency key was already used for a different item",
                ErrorCodes.PurchaseIdempotencyConflict);
        }

        return ServiceResult<PurchaseResponse>.Success(new PurchaseResponse
        {
            PurchaseId = purchase.PurchaseId,
            WalletTransactionId = purchase.WalletTransactionId,
            ItemId = purchase.ShopItemId,
            PricePaid = purchase.PriceAtPurchase,
            WalletBalance = purchase.WalletTransaction.BalanceAfter,
            PurchasedAtUtc = purchase.CreatedAtUtc,
            IsReplay = true
        }, "Purchase already completed");
    }

    private static string? Validate(PurchaseRequest request)
    {
        if (request.ItemId == Guid.Empty)
        {
            return "ItemId is required";
        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) ||
            request.IdempotencyKey.Trim().Length > MaximumIdempotencyKeyLength)
        {
            return $"IdempotencyKey must contain between 1 and {MaximumIdempotencyKeyLength} characters";
        }

        return request.ExtensionData is { Count: > 0 }
            ? "Purchase request contains unsupported fields"
            : null;
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };

    private static ServiceResult<PurchaseResponse> Failure(string message, string code) =>
        ServiceResult<PurchaseResponse>.Failure(message, code);
}
