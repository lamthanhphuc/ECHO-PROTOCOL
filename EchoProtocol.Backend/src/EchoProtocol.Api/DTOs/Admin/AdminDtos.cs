using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.DTOs.Admin;

public class AdminPagedQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class AdminUsersQuery : AdminPagedQuery
{
    public string? Search { get; set; }
}

public sealed class AdminPaymentsQuery : AdminPagedQuery
{
    public PaymentOrderStatus? Status { get; set; }
    public string? Provider { get; set; }
    public Guid? UserId { get; set; }
    public string? ProductReference { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public sealed class AdminWalletTransactionsQuery : AdminPagedQuery
{
    public Guid? UserId { get; set; }
    public WalletTransactionType? Type { get; set; }
    public Guid? Reference { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public sealed class AdminPurchasesQuery : AdminPagedQuery
{
    public Guid? UserId { get; set; }
    public Guid? ShopItemId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public sealed class AdminPagedResponse<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
}

public sealed class AdminUserResponse
{
    public Guid UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string Email { get; init; } = string.Empty;
    public UserRole Role { get; init; }
    public UserStatus Status { get; init; }
    public int? WalletBalance { get; init; }
    public int? TotalMatches { get; init; }
    public int? TotalWins { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class AdminPaymentSummary
{
    public Guid PaymentOrderId { get; init; }
    public Guid UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string ProductReference { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public PaymentOrderStatus Status { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? PaidAtUtc { get; init; }
    public DateTime? FulfilledAtUtc { get; init; }
}

public sealed class AdminPaymentDetailResponse
{
    public AdminPaymentOrderDetail Order { get; init; } = null!;
    public AdminPaymentCheckoutSummary? Checkout { get; init; }
    public IReadOnlyList<AdminPaymentProviderEventSummary> ProviderEvents { get; init; } = [];
    public AdminPaymentFulfillmentSummary? Fulfillment { get; init; }
    public AdminWalletTransactionResponse? WalletTransaction { get; init; }
}

public sealed class AdminPaymentOrderDetail
{
    public Guid PaymentOrderId { get; init; }
    public Guid UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string? ProviderOrderId { get; init; }
    public string? ProviderTransactionId { get; init; }
    public string ProductReference { get; init; } = string.Empty;
    public string Purpose { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public PaymentOrderStatus Status { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public DateTime? PaidAtUtc { get; init; }
    public DateTime? FulfilledAtUtc { get; init; }
    public string? FulfillmentReference { get; init; }
}

public sealed class AdminPaymentCheckoutSummary
{
    public long CheckoutSequenceId { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string ProviderOrderId { get; init; } = string.Empty;
    public string? ProviderPaymentLinkId { get; init; }
    public string? CheckoutUrl { get; init; }
    public PaymentCheckoutStatus Status { get; init; }
    public DateTime ReservedAtUtc { get; init; }
    public DateTime? ReadyAtUtc { get; init; }
}

public sealed class AdminPaymentProviderEventSummary
{
    public Guid PaymentProviderEventId { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string ProviderEventId { get; init; } = string.Empty;
    public string ProviderOrderId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string? Currency { get; init; }
    public PaymentProviderEventStatus NormalizedStatus { get; init; }
    public PaymentProviderEventOutcome ProcessingOutcome { get; init; }
    public string VerificationStatus { get; init; } = string.Empty;
    public DateTime ReceivedAtUtc { get; init; }
    public DateTime? ProcessedAtUtc { get; init; }
}

public sealed class AdminPaymentFulfillmentSummary
{
    public string FulfillmentReference { get; init; } = string.Empty;
    public PaymentFulfillmentKind Kind { get; init; }
    public Guid? WalletTransactionId { get; init; }
    public Guid? InventoryItemId { get; init; }
    public DateTime CompletedAtUtc { get; init; }
}

public sealed class AdminWalletTransactionResponse
{
    public Guid TransactionId { get; init; }
    public Guid UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public WalletTransactionType Type { get; init; }
    public int Amount { get; init; }
    public int BalanceBefore { get; init; }
    public int BalanceAfter { get; init; }
    public Guid Reference { get; init; }
    public string Description { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class AdminPurchaseResponse
{
    public Guid PurchaseId { get; init; }
    public Guid UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public Guid ShopItemId { get; init; }
    public string ShopItemName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public int PriceAtPurchase { get; init; }
    public Guid WalletTransactionId { get; init; }
    public PurchaseTransactionStatus Status { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
