using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Admin;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Services;

public sealed class AdminQueryService(AppDbContext db) : IAdminQueryService
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;

    public async Task<ServiceResult<AdminPagedResponse<AdminUserResponse>>> GetUsersAsync(
        AdminUsersQuery query,
        CancellationToken cancellationToken = default)
    {
        var paginationError = ValidatePagination(query);
        if (paginationError is not null)
            return Fail<AdminPagedResponse<AdminUserResponse>>(paginationError);

        var users = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            if (search.Length > 255)
                return Fail<AdminPagedResponse<AdminUserResponse>>(
                    "Search must not exceed 255 characters");
            var normalized = search.ToLowerInvariant();
            var isUserId = Guid.TryParse(search, out var userId);
            users = users.Where(user =>
                (isUserId && user.Id == userId) ||
                user.Email.ToLower().Contains(normalized) ||
                user.Username.ToLower().Contains(normalized) ||
                (user.PlayerProfile != null &&
                 user.PlayerProfile.DisplayName.ToLower().Contains(normalized)));
        }

        var projected = users.Select(user => new AdminUserResponse
        {
            UserId = user.Id,
            Username = user.Username,
            DisplayName = user.PlayerProfile == null ? null : user.PlayerProfile.DisplayName,
            Email = user.Email,
            Role = user.Role,
            Status = user.Status,
            WalletBalance = user.Wallet == null ? null : user.Wallet.Balance,
            TotalMatches = user.PlayerProfile == null ? null : user.PlayerProfile.TotalMatches,
            TotalWins = user.PlayerProfile == null ? null : user.PlayerProfile.TotalWins,
            CreatedAtUtc = user.CreatedAt
        });
        return await PageAsync(
            projected.OrderBy(user => user.CreatedAtUtc).ThenBy(user => user.UserId),
            query, "Admin users retrieved", cancellationToken);
    }

    public async Task<ServiceResult<AdminUserResponse>> GetUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await db.Users.AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => new AdminUserResponse
            {
                UserId = item.Id,
                Username = item.Username,
                DisplayName = item.PlayerProfile == null ? null : item.PlayerProfile.DisplayName,
                Email = item.Email,
                Role = item.Role,
                Status = item.Status,
                WalletBalance = item.Wallet == null ? null : item.Wallet.Balance,
                TotalMatches = item.PlayerProfile == null ? null : item.PlayerProfile.TotalMatches,
                TotalWins = item.PlayerProfile == null ? null : item.PlayerProfile.TotalWins,
                CreatedAtUtc = item.CreatedAt
            })
            .SingleOrDefaultAsync(cancellationToken);
        return user is null
            ? ServiceResult<AdminUserResponse>.Failure("User not found", ErrorCodes.NotFound)
            : ServiceResult<AdminUserResponse>.Success(user, "Admin user retrieved");
    }

    public async Task<ServiceResult<AdminPagedResponse<AdminPaymentSummary>>> GetPaymentsAsync(
        AdminPaymentsQuery query,
        CancellationToken cancellationToken = default)
    {
        var error = ValidatePagination(query) ?? ValidateRange(query.FromUtc, query.ToUtc);
        if (error is not null)
            return Fail<AdminPagedResponse<AdminPaymentSummary>>(error);

        var payments = db.PaymentOrders.AsNoTracking();
        if (query.Status is not null)
            payments = payments.Where(item => item.Status == query.Status.Value);
        if (query.UserId is not null)
            payments = payments.Where(item => item.UserId == query.UserId.Value);
        if (!string.IsNullOrWhiteSpace(query.Provider))
        {
            var provider = query.Provider.Trim().ToUpperInvariant();
            if (provider.Length > 50)
                return Fail<AdminPagedResponse<AdminPaymentSummary>>(
                    "Provider must not exceed 50 characters");
            payments = payments.Where(item => item.Provider == provider);
        }
        if (!string.IsNullOrWhiteSpace(query.ProductReference))
        {
            var product = query.ProductReference.Trim();
            if (product.Length > 128)
                return Fail<AdminPagedResponse<AdminPaymentSummary>>(
                    "ProductReference must not exceed 128 characters");
            payments = payments.Where(item => item.ProductReference == product);
        }
        if (query.FromUtc is not null)
            payments = payments.Where(item => item.CreatedAtUtc >= query.FromUtc.Value);
        if (query.ToUtc is not null)
            payments = payments.Where(item => item.CreatedAtUtc <= query.ToUtc.Value);

        var projected = payments.Select(item => new AdminPaymentSummary
        {
            PaymentOrderId = item.PaymentOrderId,
            UserId = item.UserId,
            Username = item.User.Username,
            DisplayName = item.User.PlayerProfile == null ? null : item.User.PlayerProfile.DisplayName,
            Provider = item.Provider,
            ProductReference = item.ProductReference,
            Amount = item.Amount,
            Currency = item.Currency,
            Status = item.Status,
            CreatedAtUtc = item.CreatedAtUtc,
            PaidAtUtc = item.PaidAtUtc,
            FulfilledAtUtc = item.FulfilledAtUtc
        });
        return await PageAsync(
            projected.OrderByDescending(item => item.CreatedAtUtc)
                .ThenByDescending(item => item.PaymentOrderId),
            query, "Admin payments retrieved", cancellationToken);
    }

    public async Task<ServiceResult<AdminPaymentDetailResponse>> GetPaymentAsync(
        Guid paymentOrderId,
        CancellationToken cancellationToken = default)
    {
        var order = await db.PaymentOrders.AsNoTracking()
            .Where(item => item.PaymentOrderId == paymentOrderId)
            .Select(item => new AdminPaymentOrderDetail
            {
                PaymentOrderId = item.PaymentOrderId,
                UserId = item.UserId,
                Username = item.User.Username,
                DisplayName = item.User.PlayerProfile == null ? null : item.User.PlayerProfile.DisplayName,
                Email = item.User.Email,
                Provider = item.Provider,
                ProviderOrderId = item.ProviderOrderId,
                ProviderTransactionId = item.ProviderTransactionId,
                ProductReference = item.ProductReference,
                Purpose = item.Purpose,
                Amount = item.Amount,
                Currency = item.Currency,
                Status = item.Status,
                CreatedAtUtc = item.CreatedAtUtc,
                UpdatedAtUtc = item.UpdatedAtUtc,
                ExpiresAtUtc = item.ExpiresAtUtc,
                PaidAtUtc = item.PaidAtUtc,
                FulfilledAtUtc = item.FulfilledAtUtc,
                FulfillmentReference = item.FulfillmentReference
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (order is null)
            return ServiceResult<AdminPaymentDetailResponse>.Failure(
                "Payment order not found", ErrorCodes.PaymentOrderNotFound);

        var checkout = await db.PaymentCheckouts.AsNoTracking()
            .Where(item => item.PaymentOrderId == paymentOrderId)
            .Select(item => new AdminPaymentCheckoutSummary
            {
                CheckoutSequenceId = item.CheckoutSequenceId,
                Provider = item.Provider,
                ProviderOrderId = item.ProviderOrderId,
                ProviderPaymentLinkId = item.ProviderPaymentLinkId,
                CheckoutUrl = item.CheckoutUrl,
                Status = item.Status,
                ReservedAtUtc = item.ReservedAtUtc,
                ReadyAtUtc = item.ReadyAtUtc
            })
            .SingleOrDefaultAsync(cancellationToken);
        var events = await db.PaymentProviderEvents.AsNoTracking()
            .Where(item => item.PaymentOrderId == paymentOrderId)
            .OrderByDescending(item => item.ReceivedAtUtc)
            .ThenByDescending(item => item.PaymentProviderEventId)
            .Select(item => new AdminPaymentProviderEventSummary
            {
                PaymentProviderEventId = item.PaymentProviderEventId,
                Provider = item.Provider,
                ProviderEventId = item.ProviderEventId,
                ProviderOrderId = item.ProviderOrderId,
                Amount = item.Amount,
                Currency = item.Currency,
                NormalizedStatus = item.NormalizedStatus,
                ProcessingOutcome = item.ProcessingOutcome,
                VerificationStatus = item.VerificationStatus,
                ReceivedAtUtc = item.ReceivedAtUtc,
                ProcessedAtUtc = item.ProcessedAtUtc
            })
            .ToArrayAsync(cancellationToken);
        var fulfillment = await db.PaymentFulfillments.AsNoTracking()
            .Where(item => item.PaymentOrderId == paymentOrderId)
            .Select(item => new AdminPaymentFulfillmentSummary
            {
                FulfillmentReference = item.FulfillmentReference,
                Kind = item.Kind,
                WalletTransactionId = item.WalletTransactionId,
                InventoryItemId = item.InventoryItemId,
                CompletedAtUtc = item.CompletedAtUtc
            })
            .SingleOrDefaultAsync(cancellationToken);
        AdminWalletTransactionResponse? walletTransaction = null;
        if (fulfillment?.WalletTransactionId is Guid walletTransactionId)
        {
            walletTransaction = await WalletTransactions()
                .SingleOrDefaultAsync(item => item.TransactionId == walletTransactionId, cancellationToken);
        }

        return ServiceResult<AdminPaymentDetailResponse>.Success(new AdminPaymentDetailResponse
        {
            Order = order,
            Checkout = checkout,
            ProviderEvents = events,
            Fulfillment = fulfillment,
            WalletTransaction = walletTransaction
        }, "Admin payment detail retrieved");
    }

    public async Task<ServiceResult<AdminPagedResponse<AdminWalletTransactionResponse>>>
        GetWalletTransactionsAsync(
            AdminWalletTransactionsQuery query,
            CancellationToken cancellationToken = default)
    {
        var error = ValidatePagination(query) ?? ValidateRange(query.FromUtc, query.ToUtc);
        if (error is not null)
            return Fail<AdminPagedResponse<AdminWalletTransactionResponse>>(error);

        var transactions = WalletTransactions();
        if (query.UserId is not null)
            transactions = transactions.Where(item => item.UserId == query.UserId.Value);
        if (query.Type is not null)
            transactions = transactions.Where(item => item.Type == query.Type.Value);
        if (query.Reference is not null)
            transactions = transactions.Where(item => item.Reference == query.Reference.Value);
        if (query.FromUtc is not null)
            transactions = transactions.Where(item => item.CreatedAtUtc >= query.FromUtc.Value);
        if (query.ToUtc is not null)
            transactions = transactions.Where(item => item.CreatedAtUtc <= query.ToUtc.Value);
        return await PageAsync(
            transactions.OrderByDescending(item => item.CreatedAtUtc)
                .ThenByDescending(item => item.TransactionId),
            query, "Admin wallet transactions retrieved", cancellationToken);
    }

    public async Task<ServiceResult<AdminPagedResponse<AdminPurchaseResponse>>> GetPurchasesAsync(
        AdminPurchasesQuery query,
        CancellationToken cancellationToken = default)
    {
        var error = ValidatePagination(query) ?? ValidateRange(query.FromUtc, query.ToUtc);
        if (error is not null)
            return Fail<AdminPagedResponse<AdminPurchaseResponse>>(error);

        var purchases = db.PurchaseTransactions.AsNoTracking();
        if (query.UserId is not null)
            purchases = purchases.Where(item => item.UserId == query.UserId.Value);
        if (query.ShopItemId is not null)
            purchases = purchases.Where(item => item.ShopItemId == query.ShopItemId.Value);
        if (query.FromUtc is not null)
            purchases = purchases.Where(item => item.CreatedAtUtc >= query.FromUtc.Value);
        if (query.ToUtc is not null)
            purchases = purchases.Where(item => item.CreatedAtUtc <= query.ToUtc.Value);

        var projected = purchases.Select(item => new AdminPurchaseResponse
        {
            PurchaseId = item.PurchaseId,
            UserId = item.UserId,
            Username = item.User.Username,
            DisplayName = item.User.PlayerProfile == null ? null : item.User.PlayerProfile.DisplayName,
            ShopItemId = item.ShopItemId,
            ShopItemName = item.ShopItem.ItemName,
            Category = item.ShopItem.Category,
            PriceAtPurchase = item.PriceAtPurchase,
            WalletTransactionId = item.WalletTransactionId,
            Status = item.Status,
            CreatedAtUtc = item.CreatedAtUtc
        });
        return await PageAsync(
            projected.OrderByDescending(item => item.CreatedAtUtc)
                .ThenByDescending(item => item.PurchaseId),
            query, "Admin purchases retrieved", cancellationToken);
    }

    private IQueryable<AdminWalletTransactionResponse> WalletTransactions() =>
        db.WalletTransactions.AsNoTracking().Select(item => new AdminWalletTransactionResponse
        {
            TransactionId = item.Id,
            UserId = item.Wallet.UserId,
            Username = item.Wallet.User.Username,
            DisplayName = item.Wallet.User.PlayerProfile == null
                ? null
                : item.Wallet.User.PlayerProfile.DisplayName,
            Type = item.Type,
            Amount = item.Amount,
            BalanceBefore = item.BalanceBefore,
            BalanceAfter = item.BalanceAfter,
            Reference = item.ReferenceId,
            Description = item.Description,
            CreatedAtUtc = item.CreatedAtUtc
        });

    private static async Task<ServiceResult<AdminPagedResponse<T>>> PageAsync<T>(
        IOrderedQueryable<T> query,
        AdminPagedQuery pagination,
        string message,
        CancellationToken cancellationToken)
    {
        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query.Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToArrayAsync(cancellationToken);
        return ServiceResult<AdminPagedResponse<T>>.Success(new AdminPagedResponse<T>
        {
            Items = items,
            Page = pagination.Page,
            PageSize = pagination.PageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0
                ? 0
                : (int)Math.Ceiling(totalItems / (double)pagination.PageSize)
        }, message);
    }

    private static string? ValidatePagination(AdminPagedQuery query)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > MaximumPageSize)
            return $"Page must be at least 1 and pageSize must be between 1 and {MaximumPageSize}";
        return (long)(query.Page - 1) * query.PageSize > int.MaxValue
            ? "Requested page is outside the supported pagination range"
            : null;
    }

    private static string? ValidateRange(DateTime? fromUtc, DateTime? toUtc) =>
        fromUtc is not null && toUtc is not null && fromUtc > toUtc
            ? "FromUtc must be earlier than or equal to ToUtc"
            : null;

    private static ServiceResult<T> Fail<T>(string message) =>
        ServiceResult<T>.Failure(message, ErrorCodes.ValidationError);
}
